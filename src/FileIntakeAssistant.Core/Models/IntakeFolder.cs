namespace FileIntakeAssistant.Core.Models;

public sealed record IntakeFolder(
    long? Id,
    string Path,
    string DisplayName,
    bool Enabled,
    string FolderType,
    bool Recursive,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);
