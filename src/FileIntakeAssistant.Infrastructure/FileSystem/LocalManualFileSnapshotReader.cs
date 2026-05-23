using FileIntakeAssistant.Core.Metadata;

namespace FileIntakeAssistant.Infrastructure.FileSystem;

public sealed class LocalManualFileSnapshotReader : IManualFileSnapshotReader
{
    public Task<ManualFileSnapshot?> ReadAsync(
        string path,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return Task.FromResult<ManualFileSnapshot?>(null);
        }

        string fullPath;
        try
        {
            fullPath = Path.GetFullPath(path);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return Task.FromResult<ManualFileSnapshot?>(null);
        }

        var fileInfo = new FileInfo(fullPath);
        if (!fileInfo.Exists)
        {
            return Task.FromResult<ManualFileSnapshot?>(null);
        }

        if ((fileInfo.Attributes & FileAttributes.Directory) == FileAttributes.Directory)
        {
            return Task.FromResult<ManualFileSnapshot?>(null);
        }

        return Task.FromResult<ManualFileSnapshot?>(new ManualFileSnapshot(
            Path: fullPath,
            FileName: fileInfo.Name,
            Extension: fileInfo.Extension,
            SizeBytes: fileInfo.Length,
            LastWriteTimeUtc: new DateTimeOffset(fileInfo.LastWriteTimeUtc, TimeSpan.Zero),
            MimeType: null,
            Sha256: null));
    }
}
