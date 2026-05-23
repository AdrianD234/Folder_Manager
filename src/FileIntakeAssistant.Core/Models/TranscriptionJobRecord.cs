namespace FileIntakeAssistant.Core.Models;

public sealed record TranscriptionJobRecord(
    long? Id,
    string Provider,
    string? AudioPath,
    int? DurationMs,
    string? TranscriptText,
    string Status,
    string? ErrorMessage,
    DateTimeOffset CreatedAt,
    DateTimeOffset? CompletedAt,
    string? ProviderMetadataJson);
