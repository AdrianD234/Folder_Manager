using FileIntakeAssistant.Core.Stability;

namespace FileIntakeAssistant.Infrastructure.FileSystem;

public sealed class LocalFileStabilitySnapshotReader : IFileStabilitySnapshotReader
{
    public Task<FileStabilityObservation> ReadAsync(
        string path,
        DateTimeOffset observedAt,
        bool requiresExtendedDebounce = false,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        cancellationToken.ThrowIfCancellationRequested();

        var fileInfo = new FileInfo(path);
        if (!fileInfo.Exists)
        {
            return Task.FromResult(new FileStabilityObservation(
                Path: path,
                Exists: false,
                IsDirectory: false,
                SizeBytes: 0,
                LastWriteTimeUtc: null,
                IsLocked: false,
                ObservedAt: observedAt,
                RequiresExtendedDebounce: requiresExtendedDebounce));
        }

        return Task.FromResult(new FileStabilityObservation(
            Path: path,
            Exists: true,
            IsDirectory: false,
            SizeBytes: fileInfo.Length,
            LastWriteTimeUtc: fileInfo.LastWriteTimeUtc,
            IsLocked: IsLocked(path),
            ObservedAt: observedAt,
            RequiresExtendedDebounce: requiresExtendedDebounce));
    }

    private static bool IsLocked(string path)
    {
        try
        {
            using var stream = new FileStream(
                path,
                FileMode.Open,
                FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete);

            return false;
        }
        catch (IOException)
        {
            return true;
        }
        catch (UnauthorizedAccessException)
        {
            return true;
        }
    }
}
