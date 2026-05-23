namespace FileIntakeAssistant.Core.Models;

public sealed record FileActionRecord(
    long? Id,
    string ActionType,
    long? TargetFileRecordId,
    string? OldPath,
    string? NewPath,
    string Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset? CompletedAt,
    string? DetailsJson);
