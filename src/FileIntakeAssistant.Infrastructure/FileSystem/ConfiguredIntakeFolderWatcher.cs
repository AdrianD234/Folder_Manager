using FileIntakeAssistant.Core.Configuration;
using FileIntakeAssistant.Core.Models;
using FileIntakeAssistant.Core.Triage;

namespace FileIntakeAssistant.Infrastructure.FileSystem;

public sealed record IntakeFileObservedEvent(
    string Path,
    FileEventKind EventKind,
    DateTimeOffset ObservedAt,
    bool IsDirectory,
    string? OldPath = null);

public sealed class ConfiguredIntakeFolderWatcher : IDisposable
{
    private readonly IReadOnlyList<WatchFolderSpec> _folderSpecs;
    private readonly List<FileSystemWatcher> _watchers = [];
    private bool _disposed;

    public ConfiguredIntakeFolderWatcher(IEnumerable<IntakeFolder> intakeFolders)
    {
        ArgumentNullException.ThrowIfNull(intakeFolders);

        _folderSpecs = intakeFolders
            .Where(folder => folder.Enabled)
            .Select(CreateWatchFolderSpec)
            .DistinctBy(spec => spec.Path, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public event EventHandler<IntakeFileObservedEvent>? FileObserved;

    public IReadOnlyList<string> WatchedDirectories => _folderSpecs
        .Select(spec => spec.Path)
        .ToArray();

    public int ActiveWatcherCount => _watchers.Count;

    public void Start()
    {
        ThrowIfDisposed();

        if (_watchers.Count > 0)
        {
            return;
        }

        foreach (var spec in _folderSpecs)
        {
            if (!Directory.Exists(spec.Path))
            {
                continue;
            }

            var watcher = new FileSystemWatcher(spec.Path)
            {
                IncludeSubdirectories = spec.Recursive,
                NotifyFilter = NotifyFilters.FileName
                    | NotifyFilters.DirectoryName
                    | NotifyFilters.Size
                    | NotifyFilters.LastWrite
                    | NotifyFilters.CreationTime
            };

            watcher.Created += OnCreatedOrChanged;
            watcher.Changed += OnCreatedOrChanged;
            watcher.Deleted += OnDeleted;
            watcher.Renamed += OnRenamed;
            watcher.EnableRaisingEvents = true;

            _watchers.Add(watcher);
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        foreach (var watcher in _watchers)
        {
            watcher.EnableRaisingEvents = false;
            watcher.Created -= OnCreatedOrChanged;
            watcher.Changed -= OnCreatedOrChanged;
            watcher.Deleted -= OnDeleted;
            watcher.Renamed -= OnRenamed;
            watcher.Dispose();
        }

        _watchers.Clear();
        _disposed = true;
    }

    private static WatchFolderSpec CreateWatchFolderSpec(IntakeFolder folder)
    {
        var fullPath = Path.GetFullPath(folder.Path).TrimEnd(
            Path.DirectorySeparatorChar,
            Path.AltDirectorySeparatorChar);

        EnsureRootIsNotBroad(fullPath);

        return new WatchFolderSpec(fullPath, folder.Recursive);
    }

    private static void EnsureRootIsNotBroad(string fullPath)
    {
        var validation = new IntakeFolderPathValidator().Validate(new IntakeFolderPathValidationRequest(
            fullPath,
            new IntakeFolderPathValidationOptions(
                UserProfilePath: Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                AppDataPath: null,
                LocalAppDataPath: null,
                ProgramFilesPath: Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                ProgramFilesX86Path: Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
                WindowsPath: Environment.GetFolderPath(Environment.SpecialFolder.Windows)),
            DirectoryExists: true,
            ContainsRepositoryMarker: Directory.Exists(Path.Combine(fullPath, ".git"))));

        if (!validation.IsValid)
        {
            throw new ArgumentException(validation.Reason);
        }
    }

    private void OnCreatedOrChanged(object sender, FileSystemEventArgs e)
    {
        var kind = e.ChangeType == WatcherChangeTypes.Changed
            ? FileEventKind.Changed
            : FileEventKind.Created;

        RaiseObserved(e.FullPath, kind, oldPath: null);
    }

    private void OnDeleted(object sender, FileSystemEventArgs e)
    {
        RaiseObserved(e.FullPath, FileEventKind.Deleted, oldPath: null);
    }

    private void OnRenamed(object sender, RenamedEventArgs e)
    {
        RaiseObserved(e.FullPath, FileEventKind.Renamed, e.OldFullPath);
    }

    private void RaiseObserved(string path, FileEventKind kind, string? oldPath)
    {
        var observed = new IntakeFileObservedEvent(
            Path: path,
            EventKind: kind,
            ObservedAt: DateTimeOffset.UtcNow,
            IsDirectory: Directory.Exists(path),
            OldPath: oldPath);

        FileObserved?.Invoke(this, observed);
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }

    private sealed record WatchFolderSpec(string Path, bool Recursive);
}
