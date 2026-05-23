namespace FileIntakeAssistant.Core.FileOperations;

public sealed class FileNameSanitizer
{
    private static readonly HashSet<string> ReservedNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "CON",
        "PRN",
        "AUX",
        "NUL",
        "COM1",
        "COM2",
        "COM3",
        "COM4",
        "COM5",
        "COM6",
        "COM7",
        "COM8",
        "COM9",
        "LPT1",
        "LPT2",
        "LPT3",
        "LPT4",
        "LPT5",
        "LPT6",
        "LPT7",
        "LPT8",
        "LPT9"
    };

    private static readonly HashSet<char> InvalidFileNameChars = new()
    {
        '<',
        '>',
        ':',
        '"',
        '/',
        '\\',
        '|',
        '?',
        '*'
    };

    public string Sanitize(
        string requestedFileName,
        string originalExtension,
        bool allowExtensionChange = false)
    {
        ArgumentNullException.ThrowIfNull(requestedFileName);

        var normalizedOriginalExtension = NormalizeExtension(originalExtension);
        var requestedName = Path.GetFileName(requestedFileName.Trim());
        if (string.IsNullOrWhiteSpace(requestedName))
        {
            requestedName = "untitled";
        }

        var requestedExtension = NormalizeExtension(Path.GetExtension(requestedName));
        var requestedBaseName = Path.GetFileNameWithoutExtension(requestedName);
        var sanitizedBaseName = SanitizeBaseName(requestedBaseName);
        var finalExtension = allowExtensionChange ? requestedExtension : normalizedOriginalExtension;

        if (string.IsNullOrWhiteSpace(finalExtension) && !string.IsNullOrWhiteSpace(normalizedOriginalExtension))
        {
            finalExtension = normalizedOriginalExtension;
        }

        return $"{sanitizedBaseName}{finalExtension}";
    }

    private static string SanitizeBaseName(string value)
    {
        var chars = value
            .Select(character => IsInvalidFileNameChar(character) ? '_' : character)
            .ToArray();

        var baseName = new string(chars).Trim().TrimEnd('.');
        if (string.IsNullOrWhiteSpace(baseName))
        {
            baseName = "untitled";
        }

        if (ReservedNames.Contains(baseName))
        {
            baseName = $"{baseName}_";
        }

        return baseName;
    }

    private static bool IsInvalidFileNameChar(char value)
    {
        return char.IsControl(value) || InvalidFileNameChars.Contains(value);
    }

    private static string NormalizeExtension(string? extension)
    {
        if (string.IsNullOrWhiteSpace(extension))
        {
            return string.Empty;
        }

        var sanitized = new string(extension
            .Trim()
            .Select(character => IsInvalidFileNameChar(character) ? '_' : character)
            .ToArray());

        if (!sanitized.StartsWith(".", StringComparison.Ordinal))
        {
            sanitized = $".{sanitized}";
        }

        return sanitized.TrimEnd('.', ' ');
    }
}
