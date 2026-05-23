namespace FileIntakeAssistant.Core.Batching;

public enum EventBatchType
{
    None,
    UnknownBurst,
    ArchiveExtractionBatch,
    OneDriveSyncBurst,
    InstallerOrUnpackerBurst,
    PackageInstallNoise,
    BuildOrCompilerNoise
}

public enum BatchPromptDecision
{
    NoBatch,
    PossibleBatch,
    SuppressIndividualPrompts,
    BatchReviewOnly
}

public sealed record BatchDetectionOptions(
    int PossibleBatchFileCountThreshold,
    TimeSpan PossibleBatchWindow,
    int SuppressIndividualPromptsFileCountThreshold,
    TimeSpan SuppressIndividualPromptsWindow,
    int BatchReviewOnlyFileCountThreshold,
    TimeSpan BatchReviewOnlyWindow)
{
    public static BatchDetectionOptions Defaults { get; } = new(
        PossibleBatchFileCountThreshold: 10,
        PossibleBatchWindow: TimeSpan.FromSeconds(10),
        SuppressIndividualPromptsFileCountThreshold: 50,
        SuppressIndividualPromptsWindow: TimeSpan.FromSeconds(60),
        BatchReviewOnlyFileCountThreshold: 200,
        BatchReviewOnlyWindow: TimeSpan.FromMinutes(5));
}

public sealed record FileBatchEvent(
    string Path,
    string RootPath,
    DateTimeOffset ObservedAt,
    bool IsDirectory = false);

public sealed record BatchDetectionRequest(
    string RootPath,
    IReadOnlyList<FileBatchEvent> Events,
    DateTimeOffset Now,
    BatchDetectionOptions? Options = null);

public sealed record BatchDetectionResult(
    string RootPath,
    EventBatchType BatchType,
    BatchPromptDecision Decision,
    int FileCount,
    DateTimeOffset? StartedAt,
    DateTimeOffset? EndedAt,
    bool SuppressIndividualPrompts,
    string Reason);
