using FileIntakeAssistant.Core.Batching;
using FileIntakeAssistant.Core.Models;
using FileIntakeAssistant.Core.Stability;
using FileIntakeAssistant.Core.Triage;

namespace FileIntakeAssistant.Core.Intake;

public enum IntakeProcessingOutcome
{
    OutsideConfiguredFolders,
    WaitingForStability,
    WaitingForBatchDecision,
    BatchSuppressed,
    Ignored,
    CandidateQueued
}

public sealed record IntakeCandidate(
    string Path,
    string FileName,
    string Extension,
    long SizeBytes,
    long? SourceIntakeFolderId,
    string SourceIntakeFolderPath,
    DateTimeOffset ObservedAt,
    DateTimeOffset? StableAt,
    TriageCategory TriageCategory,
    string TriageReason,
    double TriageConfidence,
    HashPlan HashPlan);

public sealed record IntakeProcessingRequest(
    string Path,
    FileEventKind EventKind,
    bool IsDirectory,
    long SizeBytes,
    IReadOnlyList<IntakeFolder> ConfiguredFolders,
    FileStabilityDecision StabilityDecision,
    BatchDetectionResult BatchDecision,
    DateTimeOffset ObservedAt,
    IReadOnlyCollection<OwnOperationSuppression>? OwnOperations = null);

public sealed record IntakeProcessingResult(
    IntakeProcessingOutcome Outcome,
    string Reason,
    IntakeFolder? MatchedIntakeFolder,
    TriageDecision TriageDecision,
    FileStabilityDecision StabilityDecision,
    BatchDetectionResult BatchDecision,
    IntakeCandidate? Candidate);
