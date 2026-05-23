namespace FileIntakeAssistant.Core.Models;

public sealed record EventBatch(
    long? Id,
    string RootPath,
    string BatchType,
    DateTimeOffset StartedAt,
    DateTimeOffset? EndedAt,
    int FileCount,
    string Decision,
    string? DetailsJson);
