using System.Text.Json;
using System.Text.RegularExpressions;

namespace FileIntakeAssistant.Infrastructure.Logging;

public sealed class JsonLinesLocalAuditLog : ILocalAuditLog
{
    private static readonly Regex OpenAiStyleSecretPattern = new(
        "sk-[A-Za-z0-9_-]{20,}",
        RegexOptions.Compiled);

    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = false
    };

    private readonly string _logFilePath;
    private readonly Func<DateTimeOffset> _timestampProvider;
    private readonly SemaphoreSlim _writeLock = new(1, 1);

    public JsonLinesLocalAuditLog(string logsDirectory)
        : this(logsDirectory, "file-intake-audit.jsonl", () => DateTimeOffset.UtcNow)
    {
    }

    internal JsonLinesLocalAuditLog(
        string logsDirectory,
        string fileName,
        Func<DateTimeOffset> timestampProvider)
    {
        if (string.IsNullOrWhiteSpace(logsDirectory))
        {
            throw new ArgumentException("Logs directory is required.", nameof(logsDirectory));
        }

        if (string.IsNullOrWhiteSpace(fileName))
        {
            throw new ArgumentException("Log file name is required.", nameof(fileName));
        }

        _logFilePath = Path.Combine(logsDirectory, fileName);
        _timestampProvider = timestampProvider ?? throw new ArgumentNullException(nameof(timestampProvider));
    }

    public string LogFilePath => _logFilePath;

    public async Task WriteAsync(
        string eventType,
        string status,
        IReadOnlyDictionary<string, object?> fields,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(eventType);
        ArgumentException.ThrowIfNullOrWhiteSpace(status);

        var entry = new LocalAuditLogEntry(
            Timestamp: _timestampProvider(),
            EventType: eventType,
            Status: status,
            Fields: SanitizeFields(fields));

        var line = JsonSerializer.Serialize(entry, SerializerOptions) + Environment.NewLine;
        var directory = Path.GetDirectoryName(_logFilePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await _writeLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await File.AppendAllTextAsync(_logFilePath, line, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _writeLock.Release();
        }
    }

    private static IReadOnlyDictionary<string, object?> SanitizeFields(IReadOnlyDictionary<string, object?> fields)
    {
        var sanitized = new Dictionary<string, object?>(StringComparer.Ordinal);
        foreach (var field in fields)
        {
            sanitized[field.Key] = SanitizeValue(field.Key, field.Value);
        }

        return sanitized;
    }

    private static object? SanitizeValue(string key, object? value)
    {
        if (value is null)
        {
            return null;
        }

        if (IsSensitiveKey(key))
        {
            return "[redacted]";
        }

        if (IsPrivatePayloadKey(key))
        {
            return value is string privatePayload
                ? new
                {
                    redacted = true,
                    length = privatePayload.Length,
                    present = !string.IsNullOrWhiteSpace(privatePayload)
                }
                : "[redacted]";
        }

        if (value is string text)
        {
            return OpenAiStyleSecretPattern.Replace(text, "[redacted]");
        }

        return value;
    }

    private static bool IsSensitiveKey(string key)
    {
        return key.Contains("apiKey", StringComparison.OrdinalIgnoreCase)
            || key.Contains("secret", StringComparison.OrdinalIgnoreCase)
            || key.Contains("token", StringComparison.OrdinalIgnoreCase)
            || key.Contains("password", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsPrivatePayloadKey(string key)
    {
        return key.Contains("userNote", StringComparison.OrdinalIgnoreCase)
            || key.Contains("transcript", StringComparison.OrdinalIgnoreCase)
            || key.Contains("providerMetadata", StringComparison.OrdinalIgnoreCase)
            || key.Contains("audioPath", StringComparison.OrdinalIgnoreCase)
            || key.Contains("sourceUrl", StringComparison.OrdinalIgnoreCase)
            || key.Contains("referrerUrl", StringComparison.OrdinalIgnoreCase)
            || key.Equals("rawText", StringComparison.OrdinalIgnoreCase)
            || key.Contains("queryText", StringComparison.OrdinalIgnoreCase)
            || key.Contains("providerRequest", StringComparison.OrdinalIgnoreCase)
            || key.Contains("providerResponse", StringComparison.OrdinalIgnoreCase)
            || key.Contains("errorBody", StringComparison.OrdinalIgnoreCase);
    }

    private sealed record LocalAuditLogEntry(
        DateTimeOffset Timestamp,
        string EventType,
        string Status,
        IReadOnlyDictionary<string, object?> Fields);
}
