namespace FileIntakeAssistant.Core.Models;

public sealed record SearchQueryRecord(
    long? Id,
    string QueryText,
    string? ParsedIntentJson,
    string Provider,
    int ResultCount,
    DateTimeOffset CreatedAt);
