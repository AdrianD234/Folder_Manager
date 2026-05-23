namespace FileIntakeAssistant.Core.Models;

public sealed record MetadataEntry(
    long? Id,
    long? FileRecordId,
    long? FolderRecordId,
    string? UserNote,
    string? TranscriptText,
    string? Relevance,
    string? Project,
    string? Topic,
    string? TagsJson,
    string? SourceUrl,
    string? ReferrerUrl,
    string? AgentSummary,
    double? ClassifierConfidence,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);
