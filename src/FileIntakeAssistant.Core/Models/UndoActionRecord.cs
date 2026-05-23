namespace FileIntakeAssistant.Core.Models;

public sealed record UndoActionRecord(
    long? Id,
    long ActionId,
    long TargetFileRecordId,
    string UndoType,
    string OriginalPath,
    string ResultingPath,
    string FileIdentityJson,
    string Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset? PerformedAt);
