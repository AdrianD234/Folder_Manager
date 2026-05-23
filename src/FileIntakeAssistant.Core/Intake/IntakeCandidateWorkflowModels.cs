namespace FileIntakeAssistant.Core.Intake;

public enum IntakeCandidateWorkflowStatus
{
    Succeeded,
    Failed
}

public sealed record IntakeCandidateSaveResult(
    IntakeCandidateWorkflowStatus Status,
    long? FileRecordId,
    long? MetadataEntryId,
    long? ActionId,
    long? ManualTranscriptionJobId,
    string? ReviewedTranscriptText,
    string? ErrorMessage);

public sealed record IntakeCandidateSkipResult(
    IntakeCandidateWorkflowStatus Status,
    long? ActionId,
    string? ErrorMessage);
