namespace FileIntakeAssistant.Core.Models;

public sealed record FolderRecord(
    long? Id,
    string Path,
    string DisplayName,
    string FolderType,
    long? SourceIntakeFolderId,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    string? NotesJson);
