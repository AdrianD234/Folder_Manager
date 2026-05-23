namespace FileIntakeAssistant.Core.Models;

public sealed record FileEventRecord(
    long? Id,
    long? FileRecordId,
    string EventType,
    string RawPath,
    string? OldPath,
    string? NewPath,
    DateTimeOffset ObservedAt,
    DateTimeOffset NormalizedAt,
    string TriageCategory,
    string TriageReason,
    long? BatchId,
    bool Ignored,
    string? DetailsJson);
