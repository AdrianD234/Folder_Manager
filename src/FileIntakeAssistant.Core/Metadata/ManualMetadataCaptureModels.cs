namespace FileIntakeAssistant.Core.Metadata;

public sealed record ManualFileSnapshot(
    string Path,
    string FileName,
    string Extension,
    long SizeBytes,
    DateTimeOffset LastWriteTimeUtc,
    string? MimeType,
    string? Sha256);

public sealed record ManualMetadataFields(
    string? UserNote,
    string? Relevance,
    string? Project,
    string? Topic,
    string? Tags,
    string? SourceUrl,
    string? TranscriptText = null);

public sealed record ManualMetadataCaptureContext(
    string Source,
    string FileStatus,
    string TriageCategory,
    double TriageConfidence,
    long? SourceIntakeFolderId,
    string ActionType,
    string Mode,
    string? NotesJson = null)
{
    public static ManualMetadataCaptureContext Manual { get; } = new(
        Source: "manual",
        FileStatus: "Captured",
        TriageCategory: "ManualCapture",
        TriageConfidence: 1.0,
        SourceIntakeFolderId: null,
        ActionType: "ManualMetadataCapture",
        Mode: "Manual");
}

public sealed record ManualMetadataCaptureResult(
    bool Succeeded,
    long? FileRecordId,
    long? MetadataEntryId,
    long? ActionId,
    string? FailureReason);
