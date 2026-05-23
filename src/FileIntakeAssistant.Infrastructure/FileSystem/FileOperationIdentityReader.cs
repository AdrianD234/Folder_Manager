using System.Security.Cryptography;
using FileIntakeAssistant.Core.FileOperations;

namespace FileIntakeAssistant.Infrastructure.FileSystem;

internal sealed class FileOperationIdentityReader
{
    private const long DefaultHashThresholdBytes = 100L * 1024L * 1024L;

    private readonly long _hashThresholdBytes;

    public FileOperationIdentityReader(long hashThresholdBytes = DefaultHashThresholdBytes)
    {
        _hashThresholdBytes = hashThresholdBytes;
    }

    public async Task<FileIdentitySnapshot> ReadAsync(
        string path,
        string identityPath,
        CancellationToken cancellationToken = default)
    {
        var info = new FileInfo(path);
        if (!info.Exists)
        {
            throw new FileNotFoundException("The file identity could not be read because the file does not exist.", path);
        }

        var sha256 = info.Length <= _hashThresholdBytes
            ? await ComputeSha256Async(path, cancellationToken).ConfigureAwait(false)
            : null;

        return new FileIdentitySnapshot(
            Path: Path.GetFullPath(identityPath),
            SizeBytes: info.Length,
            LastWriteTimeUtc: new DateTimeOffset(info.LastWriteTimeUtc, TimeSpan.Zero),
            Sha256: sha256);
    }

    public static bool Matches(FileIdentitySnapshot expected, FileIdentitySnapshot actual)
    {
        if (!string.Equals(expected.Path, actual.Path, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (expected.SizeBytes != actual.SizeBytes)
        {
            return false;
        }

        if (expected.LastWriteTimeUtc != actual.LastWriteTimeUtc)
        {
            return false;
        }

        return expected.Sha256 is null
            || string.Equals(expected.Sha256, actual.Sha256, StringComparison.OrdinalIgnoreCase);
    }

    private static async Task<string> ComputeSha256Async(string path, CancellationToken cancellationToken)
    {
        await using var stream = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 81920,
            useAsync: true);

        var hash = await SHA256.HashDataAsync(stream, cancellationToken).ConfigureAwait(false);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
