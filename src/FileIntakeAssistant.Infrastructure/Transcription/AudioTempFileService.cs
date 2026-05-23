using FileIntakeAssistant.Core.Transcription;

namespace FileIntakeAssistant.Infrastructure.Transcription;

public enum AudioTempCleanupStatus
{
    Deleted,
    Retained,
    Missing,
    RefusedUnsafePath
}

public sealed record AudioTempCleanupResult(
    AudioTempCleanupStatus Status,
    string AudioPath,
    string Reason);

public sealed class AudioTempFileService
{
    private readonly string _tempAudioDirectory;

    public AudioTempFileService(string tempAudioDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tempAudioDirectory);
        _tempAudioDirectory = Path.GetFullPath(tempAudioDirectory);
    }

    public string CreateTempAudioPath(string extension = ".wav")
    {
        Directory.CreateDirectory(_tempAudioDirectory);

        var normalizedExtension = NormalizeExtension(extension);
        return Path.Combine(_tempAudioDirectory, $"{Guid.NewGuid():N}{normalizedExtension}");
    }

    public Task<AudioTempCleanupResult> ApplyRetentionPolicyAsync(
        string audioPath,
        TranscriptionProviderStatus finalStatus,
        TranscriptionOptions options,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(audioPath);
        ArgumentNullException.ThrowIfNull(options);
        cancellationToken.ThrowIfCancellationRequested();

        var fullPath = Path.GetFullPath(audioPath);
        if (!IsUnderTempAudioDirectory(fullPath))
        {
            return Task.FromResult(new AudioTempCleanupResult(
                AudioTempCleanupStatus.RefusedUnsafePath,
                fullPath,
                "Refused to apply audio cleanup outside the configured temp-audio directory."));
        }

        if (!File.Exists(fullPath))
        {
            return Task.FromResult(new AudioTempCleanupResult(
                AudioTempCleanupStatus.Missing,
                fullPath,
                "Audio temp file was already missing."));
        }

        var shouldDelete = finalStatus == TranscriptionProviderStatus.Succeeded
            && options.DeleteAudioAfterSuccessfulTranscription;

        if (!shouldDelete)
        {
            return Task.FromResult(new AudioTempCleanupResult(
                AudioTempCleanupStatus.Retained,
                fullPath,
                finalStatus == TranscriptionProviderStatus.Succeeded
                    ? "Audio retention was explicitly configured."
                    : "Audio was retained because transcription did not succeed."));
        }

        File.Delete(fullPath);
        return Task.FromResult(new AudioTempCleanupResult(
            AudioTempCleanupStatus.Deleted,
            fullPath,
            "Audio temp file was deleted after successful transcription."));
    }

    private bool IsUnderTempAudioDirectory(string fullPath)
    {
        var root = _tempAudioDirectory.TrimEnd(
            Path.DirectorySeparatorChar,
            Path.AltDirectorySeparatorChar);

        return string.Equals(fullPath, root, StringComparison.OrdinalIgnoreCase)
            || fullPath.StartsWith($"{root}{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase)
            || fullPath.StartsWith($"{root}{Path.AltDirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeExtension(string extension)
    {
        var value = string.IsNullOrWhiteSpace(extension) ? ".wav" : extension.Trim();
        value = value.StartsWith(".", StringComparison.Ordinal) ? value : $".{value}";

        var invalid = value.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0
            || value.Contains(Path.DirectorySeparatorChar)
            || value.Contains(Path.AltDirectorySeparatorChar);

        return invalid ? ".wav" : value;
    }
}
