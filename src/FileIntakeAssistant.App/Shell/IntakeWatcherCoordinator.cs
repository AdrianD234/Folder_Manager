using System.IO;
using FileIntakeAssistant.Core.Batching;
using FileIntakeAssistant.Core.Intake;
using FileIntakeAssistant.Core.Models;
using FileIntakeAssistant.Core.Persistence;
using FileIntakeAssistant.Core.Stability;
using FileIntakeAssistant.Core.Triage;
using FileIntakeAssistant.Infrastructure.FileSystem;

namespace FileIntakeAssistant.App.Shell;

public sealed class IntakeWatcherCoordinator : IDisposable
{
    private readonly IFileIntakeStore _store;
    private readonly AuditedIntakeEventProcessor _auditedProcessor;
    private readonly IFileStabilitySnapshotReader _snapshotReader;
    private readonly FileStabilityChecker _stabilityChecker;
    private readonly BatchDetector _batchDetector;
    private readonly Func<DateTimeOffset> _clock;
    private readonly Func<Task>? _afterProcessedAsync;
    private readonly AppLifecycleAudit? _audit;
    private readonly object _sync = new();
    private readonly List<FileBatchEvent> _recentBatchEvents = [];

    private ConfiguredIntakeFolderWatcher? _watcher;
    private IReadOnlyList<IntakeFolder> _configuredFolders = [];
    private CancellationTokenSource _processingCancellation = new();
    private bool _disposed;

    public IntakeWatcherCoordinator(
        IFileIntakeStore store,
        AuditedIntakeEventProcessor auditedProcessor,
        IFileStabilitySnapshotReader snapshotReader,
        FileStabilityChecker stabilityChecker,
        BatchDetector batchDetector,
        Func<Task>? afterProcessedAsync = null,
        Func<DateTimeOffset>? clock = null,
        AppLifecycleAudit? audit = null)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _auditedProcessor = auditedProcessor ?? throw new ArgumentNullException(nameof(auditedProcessor));
        _snapshotReader = snapshotReader ?? throw new ArgumentNullException(nameof(snapshotReader));
        _stabilityChecker = stabilityChecker ?? throw new ArgumentNullException(nameof(stabilityChecker));
        _batchDetector = batchDetector ?? throw new ArgumentNullException(nameof(batchDetector));
        _afterProcessedAsync = afterProcessedAsync;
        _clock = clock ?? (() => DateTimeOffset.UtcNow);
        _audit = audit;
    }

    public IReadOnlyList<string> WatchedDirectories => _watcher?.WatchedDirectories ?? [];

    public async Task RestartAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        IReadOnlyList<IntakeFolder> configuredFolders = [];
        await WriteRestartAuditAsync("Started", configuredFolders, 0, cancellationToken).ConfigureAwait(false);

        try
        {
            configuredFolders = await _store.ListIntakeFoldersAsync(enabledOnly: true, cancellationToken)
                .ConfigureAwait(false);

            ConfiguredIntakeFolderWatcher? oldWatcher;
            CancellationTokenSource oldCancellation;
            lock (_sync)
            {
                oldWatcher = _watcher;
                oldCancellation = _processingCancellation;
                _processingCancellation = new CancellationTokenSource();
                _configuredFolders = configuredFolders;
                _recentBatchEvents.Clear();

                _watcher = new ConfiguredIntakeFolderWatcher(configuredFolders);
                _watcher.FileObserved += OnFileObserved;
                _watcher.Start();
            }

            oldCancellation.Cancel();
            oldCancellation.Dispose();
            if (oldWatcher is not null)
            {
                oldWatcher.FileObserved -= OnFileObserved;
                oldWatcher.Dispose();
            }

            await WriteRestartAuditAsync("Completed", configuredFolders, WatchedDirectories.Count, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            await WriteRestartAuditAsync(
                ex is OperationCanceledException ? "Canceled" : "Failed",
                configuredFolders,
                WatchedDirectories.Count,
                CancellationToken.None,
                ex.GetType().Name).ConfigureAwait(false);
            throw;
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _processingCancellation.Cancel();
        _processingCancellation.Dispose();
        if (_watcher is not null)
        {
            _watcher.FileObserved -= OnFileObserved;
            _watcher.Dispose();
        }

        _audit?.WriteFireAndForget(
            "intake_watcher.disposed",
            "Completed",
            new Dictionary<string, object?>
            {
                ["component"] = "intake_watcher",
                ["watchedDirectoryCount"] = WatchedDirectories.Count
            });
    }

    private Task WriteRestartAuditAsync(
        string status,
        IReadOnlyList<IntakeFolder> configuredFolders,
        int watchedDirectoryCount,
        CancellationToken cancellationToken,
        string? errorType = null)
    {
        if (_audit is null)
        {
            return Task.CompletedTask;
        }

        var fields = new Dictionary<string, object?>
        {
            ["component"] = "intake_watcher",
            ["enabledFolderCount"] = configuredFolders.Count(folder => folder.Enabled),
            ["recursiveFolderCount"] = configuredFolders.Count(folder => folder.Enabled && folder.Recursive),
            ["watchedDirectoryCount"] = watchedDirectoryCount
        };
        if (errorType is not null)
        {
            fields["errorType"] = errorType;
        }

        return _audit.WriteAsync("intake_watcher.restart", status, fields, cancellationToken);
    }

    private void OnFileObserved(object? sender, IntakeFileObservedEvent e)
    {
        _ = ProcessObservedAsync(e, _processingCancellation.Token);
    }

    private async Task ProcessObservedAsync(IntakeFileObservedEvent observedEvent, CancellationToken cancellationToken)
    {
        try
        {
            var firstObservation = await _snapshotReader.ReadAsync(
                observedEvent.Path,
                observedEvent.ObservedAt,
                cancellationToken: cancellationToken).ConfigureAwait(false);

            if (firstObservation.Exists && !firstObservation.IsDirectory)
            {
                await Task.Delay(FileStabilityOptions.Defaults.OrdinaryDebounceWindow, cancellationToken)
                    .ConfigureAwait(false);
            }

            var secondObservedAt = _clock();
            var secondObservation = await _snapshotReader.ReadAsync(
                observedEvent.Path,
                secondObservedAt,
                cancellationToken: cancellationToken).ConfigureAwait(false);
            var stabilityDecision = _stabilityChecker.Evaluate(new FileStabilityRequest(
                [firstObservation, secondObservation],
                _clock()));

            var configuredFolders = _configuredFolders;
            var rootPath = FindMatchingRoot(observedEvent.Path, configuredFolders)
                ?? GetParentDirectory(observedEvent.Path)
                ?? observedEvent.Path;
            var batchDecision = BuildBatchDecision(observedEvent, rootPath);

            await _auditedProcessor.ProcessAndAuditAsync(
                new IntakeProcessingRequest(
                    Path: observedEvent.Path,
                    EventKind: observedEvent.EventKind,
                    IsDirectory: observedEvent.IsDirectory,
                    SizeBytes: secondObservation.SizeBytes,
                    ConfiguredFolders: configuredFolders,
                    StabilityDecision: stabilityDecision,
                    BatchDecision: batchDecision,
                    ObservedAt: observedEvent.ObservedAt),
                observedEvent.OldPath,
                cancellationToken).ConfigureAwait(false);

            if (_afterProcessedAsync is not null)
            {
                await _afterProcessedAsync().ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
        }
    }

    private BatchDetectionResult BuildBatchDecision(IntakeFileObservedEvent observedEvent, string rootPath)
    {
        lock (_sync)
        {
            _recentBatchEvents.RemoveAll(fileEvent => _clock() - fileEvent.ObservedAt > BatchDetectionOptions.Defaults.BatchReviewOnlyWindow);
            _recentBatchEvents.Add(new FileBatchEvent(
                observedEvent.Path,
                rootPath,
                observedEvent.ObservedAt,
                observedEvent.IsDirectory));

            var events = _recentBatchEvents
                .Where(fileEvent => string.Equals(fileEvent.RootPath, rootPath, StringComparison.OrdinalIgnoreCase))
                .ToArray();

            return _batchDetector.Evaluate(new BatchDetectionRequest(rootPath, events, _clock()));
        }
    }

    private static string? FindMatchingRoot(string path, IReadOnlyList<IntakeFolder> configuredFolders)
    {
        var normalizedPath = NormalizePath(path);
        return configuredFolders
            .Where(folder => folder.Enabled)
            .Select(folder => NormalizePath(folder.Path))
            .Where(folderPath => string.Equals(normalizedPath, folderPath, StringComparison.OrdinalIgnoreCase)
                || normalizedPath.StartsWith($"{folderPath}\\", StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(folderPath => folderPath.Length)
            .FirstOrDefault();
    }

    private static string? GetParentDirectory(string path)
    {
        return Path.GetDirectoryName(path);
    }

    private static string NormalizePath(string path)
    {
        return path.Trim().Replace('/', '\\').TrimEnd('\\');
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }
}
