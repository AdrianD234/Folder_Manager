namespace FileIntakeAssistant.Core.Models;

public sealed record VoiceCommandRecord(
    long? Id,
    string RawText,
    string? ParsedIntentJson,
    string Status,
    int ResultCount,
    string? ExecutedAction,
    DateTimeOffset CreatedAt,
    string? DetailsJson);
