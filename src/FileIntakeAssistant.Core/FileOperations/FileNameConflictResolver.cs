namespace FileIntakeAssistant.Core.FileOperations;

public sealed class FileNameConflictResolver
{
    public string ResolveNonConflictingPath(
        string destinationDirectory,
        string sanitizedFileName,
        IEnumerable<string> existingDestinationPaths)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(destinationDirectory);
        ArgumentException.ThrowIfNullOrWhiteSpace(sanitizedFileName);
        ArgumentNullException.ThrowIfNull(existingDestinationPaths);

        var existing = existingDestinationPaths
            .Select(NormalizePath)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var baseName = Path.GetFileNameWithoutExtension(sanitizedFileName);
        var extension = Path.GetExtension(sanitizedFileName);
        var candidate = Path.Combine(destinationDirectory, sanitizedFileName);
        if (!existing.Contains(NormalizePath(candidate)))
        {
            return candidate;
        }

        for (var suffix = 2; suffix < int.MaxValue; suffix++)
        {
            candidate = Path.Combine(destinationDirectory, $"{baseName} ({suffix}){extension}");
            if (!existing.Contains(NormalizePath(candidate)))
            {
                return candidate;
            }
        }

        throw new InvalidOperationException("No non-conflicting destination path could be generated.");
    }

    private static string NormalizePath(string path)
    {
        return Path.GetFullPath(path);
    }
}
