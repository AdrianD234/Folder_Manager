using FileIntakeAssistant.Core.Batching;
using FileIntakeAssistant.Core.Intake;
using FileIntakeAssistant.Core.Models;
using FileIntakeAssistant.Core.Stability;
using FileIntakeAssistant.Core.Triage;
using FileIntakeAssistant.Infrastructure.Configuration;
using FileIntakeAssistant.Infrastructure.FileSystem;

namespace FileIntakeAssistant.Tests.Watcher;

public sealed class IntakeWatcherTests : IDisposable
{
    private static readonly DateTimeOffset BaseTime = new(2026, 5, 23, 5, 0, 0, TimeSpan.Zero);

    private readonly string _testRoot = Path.Combine(
        Path.GetTempPath(),
        "FileIntakeAssistant.Tests",
        Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        var fullRoot = Path.GetFullPath(_testRoot);
        var allowedRoot = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "FileIntakeAssistant.Tests"));

        if (fullRoot.StartsWith(allowedRoot, StringComparison.OrdinalIgnoreCase) && Directory.Exists(fullRoot))
        {
            Directory.Delete(fullRoot, recursive: true);
        }
    }

    [Fact]
    public void Watcher_ConfiguredWatcherUsesOnlyEnabledExplicitTempFolders()
    {
        var enabled = Path.Combine(_testRoot, "enabled-intake");
        var disabled = Path.Combine(_testRoot, "disabled-intake");
        Directory.CreateDirectory(enabled);
        Directory.CreateDirectory(disabled);

        using var watcher = new ConfiguredIntakeFolderWatcher(
        [
            IntakeFolder(enabled, enabled: true),
            IntakeFolder(disabled, enabled: false)
        ]);

        Assert.Contains(Path.GetFullPath(enabled).TrimEnd(Path.DirectorySeparatorChar), watcher.WatchedDirectories);
        Assert.DoesNotContain(Path.GetFullPath(disabled).TrimEnd(Path.DirectorySeparatorChar), watcher.WatchedDirectories);

        watcher.Start();

        Assert.Equal(1, watcher.ActiveWatcherCount);
    }

    [Fact]
    public void Watcher_DriveRootIsRejectedAsTooBroad()
    {
        var driveRoot = Path.GetPathRoot(Path.GetFullPath(_testRoot));

        Assert.False(string.IsNullOrWhiteSpace(driveRoot));
        Assert.Throws<ArgumentException>(() => new ConfiguredIntakeFolderWatcher(
        [
            IntakeFolder(driveRoot!, enabled: true)
        ]));
    }

    [Fact]
    public void Watcher_DefaultDownloadsSuggestionIsDisabledUntilUserAcceptsIt()
    {
        var suggestion = DefaultIntakeFolderProvider.CreateDownloadsSuggestion(BaseTime);

        Assert.Equal("Downloads", suggestion.DisplayName);
        Assert.Equal("Downloads", suggestion.FolderType);
        Assert.False(suggestion.Enabled);
        Assert.False(suggestion.Recursive);
    }

    [Fact]
    public void Watcher_UnconfiguredFolderEventIsIgnored()
    {
        var queue = new InMemoryIntakeCandidateQueue();
        var processor = new IntakeEventProcessor(new FileEventTriageEngine(), queue);

        var result = processor.Process(Request(
            path: @"C:\Other\Report.pdf",
            configuredFolders: [IntakeFolder(@"C:\Intake", enabled: true)]));

        Assert.Equal(IntakeProcessingOutcome.OutsideConfiguredFolders, result.Outcome);
        Assert.Equal(0, queue.Count);
        Assert.Null(result.Candidate);
        Assert.False(result.TriageDecision.PromptAllowed);
    }

    [Fact]
    public void Watcher_MeaningfulStableFileEntersCandidateQueue()
    {
        var queue = new InMemoryIntakeCandidateQueue();
        var processor = new IntakeEventProcessor(new FileEventTriageEngine(), queue);

        var result = processor.Process(Request(
            path: @"C:\Intake\Report.pdf",
            configuredFolders: [IntakeFolder(@"C:\Intake", id: 42, enabled: true)]));

        Assert.Equal(IntakeProcessingOutcome.CandidateQueued, result.Outcome);
        Assert.NotNull(result.Candidate);
        Assert.Equal(1, queue.Count);

        var candidate = Assert.Single(queue.Snapshot());
        Assert.Equal(@"C:\Intake\Report.pdf", candidate.Path);
        Assert.Equal("Report.pdf", candidate.FileName);
        Assert.Equal(".pdf", candidate.Extension);
        Assert.Equal(42, candidate.SourceIntakeFolderId);
        Assert.Equal(TriageCategory.MeaningfulOneOff, candidate.TriageCategory);
    }

    [Fact]
    public void Watcher_DuplicateCandidateEventsForSamePathAreDeduplicatedInsideWindow()
    {
        var queue = new InMemoryIntakeCandidateQueue(TimeSpan.FromSeconds(30));
        var processor = new IntakeEventProcessor(new FileEventTriageEngine(), queue);
        var request = Request(
            path: @"C:\Intake\Report.pdf",
            configuredFolders: [IntakeFolder(@"C:\Intake", id: 42, enabled: true)]);

        processor.Process(request);
        processor.Process(request);

        var candidate = Assert.Single(queue.Snapshot());
        Assert.Equal(@"C:\Intake\Report.pdf", candidate.Path);
    }

    [Fact]
    public void Watcher_DeduplicationDoesNotSuppressUnrelatedCandidatePath()
    {
        var queue = new InMemoryIntakeCandidateQueue(TimeSpan.FromSeconds(30));
        var processor = new IntakeEventProcessor(new FileEventTriageEngine(), queue);

        processor.Process(Request(
            path: @"C:\Intake\Report.pdf",
            configuredFolders: [IntakeFolder(@"C:\Intake", id: 42, enabled: true)]));
        processor.Process(Request(
            path: @"C:\Intake\Other.pdf",
            configuredFolders: [IntakeFolder(@"C:\Intake", id: 42, enabled: true)]));

        Assert.Equal(2, queue.Count);
    }

    [Fact]
    public void Watcher_PartialDownloadIsNotQueued()
    {
        var queue = new InMemoryIntakeCandidateQueue();
        var processor = new IntakeEventProcessor(new FileEventTriageEngine(), queue);

        var result = processor.Process(Request(
            path: @"C:\Intake\Report.pdf.crdownload",
            configuredFolders: [IntakeFolder(@"C:\Intake", enabled: true)],
            stabilityDecision: NotStable(FileStabilityStatus.PartialOrTemporary, "Partial download.")));

        Assert.Equal(IntakeProcessingOutcome.WaitingForStability, result.Outcome);
        Assert.Equal(TriageCategory.TemporaryOrPartial, result.TriageDecision.Category);
        Assert.Equal(0, queue.Count);
    }

    [Fact]
    public void Watcher_DevelopmentBuildNoiseIsNotQueued()
    {
        var queue = new InMemoryIntakeCandidateQueue();
        var processor = new IntakeEventProcessor(new FileEventTriageEngine(), queue);

        var result = processor.Process(Request(
            path: @"C:\Intake\Project\node_modules\package\index.js",
            configuredFolders: [IntakeFolder(@"C:\Intake", enabled: true, recursive: true)]));

        Assert.Equal(IntakeProcessingOutcome.Ignored, result.Outcome);
        Assert.Equal(TriageCategory.PackageInstallNoise, result.TriageDecision.Category);
        Assert.Equal(0, queue.Count);
    }

    [Fact]
    public void Watcher_BatchSuppressionPreventsCandidateQueue()
    {
        var queue = new InMemoryIntakeCandidateQueue();
        var processor = new IntakeEventProcessor(new FileEventTriageEngine(), queue);

        var result = processor.Process(Request(
            path: @"C:\Intake\Extracted\Report.pdf",
            configuredFolders: [IntakeFolder(@"C:\Intake", enabled: true, recursive: true)],
            batchDecision: Batch(
                @"C:\Intake\Extracted",
                EventBatchType.ArchiveExtractionBatch,
                BatchPromptDecision.SuppressIndividualPrompts,
                suppressIndividualPrompts: true)));

        Assert.Equal(IntakeProcessingOutcome.BatchSuppressed, result.Outcome);
        Assert.Equal(0, queue.Count);
        Assert.Null(result.Candidate);
    }

    [Fact]
    public void Watcher_OwnOperationEventIsSuppressed()
    {
        var queue = new InMemoryIntakeCandidateQueue();
        var processor = new IntakeEventProcessor(new FileEventTriageEngine(), queue);
        var operation = new OwnOperationSuppression(
            OldPath: @"C:\Intake\Report.pdf",
            NewPath: @"C:\Filed\Report.pdf",
            RegisteredAt: BaseTime.AddSeconds(-5),
            SuppressionWindow: OwnOperationSuppression.DefaultSuppressionWindow);

        var result = processor.Process(Request(
            path: @"C:\Intake\Report.pdf",
            configuredFolders: [IntakeFolder(@"C:\Intake", enabled: true)],
            ownOperations: [operation]));

        Assert.Equal(IntakeProcessingOutcome.Ignored, result.Outcome);
        Assert.Equal(TriageCategory.OwnOperation, result.TriageDecision.Category);
        Assert.Equal(0, queue.Count);
    }

    private static IntakeProcessingRequest Request(
        string path,
        IReadOnlyList<IntakeFolder> configuredFolders,
        FileEventKind eventKind = FileEventKind.Created,
        bool isDirectory = false,
        long sizeBytes = 1_024,
        FileStabilityDecision? stabilityDecision = null,
        BatchDetectionResult? batchDecision = null,
        IReadOnlyCollection<OwnOperationSuppression>? ownOperations = null)
    {
        return new IntakeProcessingRequest(
            Path: path,
            EventKind: eventKind,
            IsDirectory: isDirectory,
            SizeBytes: sizeBytes,
            ConfiguredFolders: configuredFolders,
            StabilityDecision: stabilityDecision ?? Stable(),
            BatchDecision: batchDecision ?? Batch(@"C:\Intake", EventBatchType.None, BatchPromptDecision.NoBatch),
            ObservedAt: BaseTime,
            OwnOperations: ownOperations);
    }

    private static FileStabilityDecision Stable()
    {
        return new FileStabilityDecision(
            FileStabilityStatus.Stable,
            IsStable: true,
            Reason: "Stable test file.",
            RequiredDebounceWindow: TimeSpan.FromSeconds(2),
            StableSince: BaseTime.AddSeconds(-3),
            HashPlan: HashPlan.ComputeSha256);
    }

    private static FileStabilityDecision NotStable(FileStabilityStatus status, string reason)
    {
        return new FileStabilityDecision(
            status,
            IsStable: false,
            Reason: reason,
            RequiredDebounceWindow: TimeSpan.FromSeconds(2),
            StableSince: null,
            HashPlan: HashPlan.NotReady);
    }

    private static BatchDetectionResult Batch(
        string rootPath,
        EventBatchType type,
        BatchPromptDecision decision,
        bool suppressIndividualPrompts = false)
    {
        return new BatchDetectionResult(
            RootPath: rootPath,
            BatchType: type,
            Decision: decision,
            FileCount: 1,
            StartedAt: BaseTime,
            EndedAt: BaseTime,
            SuppressIndividualPrompts: suppressIndividualPrompts,
            Reason: decision == BatchPromptDecision.NoBatch
                ? "No batch in test."
                : "Batch suppression in test.");
    }

    private static IntakeFolder IntakeFolder(
        string path,
        long? id = null,
        bool enabled = true,
        bool recursive = false)
    {
        return new IntakeFolder(
            Id: id,
            Path: path,
            DisplayName: Path.GetFileName(path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)),
            Enabled: enabled,
            FolderType: "Test",
            Recursive: recursive,
            CreatedAt: BaseTime,
            UpdatedAt: BaseTime);
    }
}
