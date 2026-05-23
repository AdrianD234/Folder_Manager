namespace FileIntakeAssistant.Infrastructure.Transcription;

public static class SecretRedactor
{
    public const string RedactedToken = "[REDACTED]";

    public static string? Redact(string? value, params string?[] secrets)
    {
        if (value is null)
        {
            return null;
        }

        var redacted = value;
        foreach (var secret in secrets)
        {
            if (string.IsNullOrWhiteSpace(secret))
            {
                continue;
            }

            redacted = redacted.Replace(secret, RedactedToken, StringComparison.Ordinal);
        }

        return redacted;
    }
}
