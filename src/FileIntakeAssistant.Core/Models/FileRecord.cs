namespace FileIntakeAssistant.Core.Models;

public sealed record FileRecord(
    long? Id,
    string? Sha256,
    string OriginalFilename,
    string CurrentFilename,
    string OriginalPath,
    string CurrentPath,
    string Extension,
    long SizeBytes,
    string? MimeType,
    long? SourceIntakeFolderId,
    DateTimeOffset FirstSeenAt,
    DateTimeOffset LastSeenAt,
    DateTimeOffset? StableAt,
    string Status,
    string TriageCategory,
    double TriageConfidence,
    bool IsMeaningful,
    string? NotesJson);
