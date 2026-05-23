namespace FileIntakeAssistant.Core.Triage;

public enum FileEventKind
{
    Created,
    Changed,
    Renamed,
    Deleted
}

public enum TriageCategory
{
    MeaningfulOneOff,
    TemporaryOrPartial,
    DevelopmentNoise,
    BuildOrCompilerNoise,
    PackageInstallNoise,
    ArchiveExtractionBatch,
    OneDriveSyncBurst,
    InstallerOrUnpackerBurst,
    OwnOperation,
    SystemOrAppDataNoise,
    BrowserCacheNoise,
    NeedsMoreObservation,
    UnknownSafeToIgnore
}

public enum ProcessingState
{
    Observed,
    Normalized,
    WaitingForStability,
    WaitingForBatchDecision,
    Ignored,
    BatchSuppressed,
    Candidate,
    PromptQueued,
    Captured,
    Filed,
    Failed
}

public enum FolderContextRecommendation
{
    None,
    PreferFolderContext,
    ManualFileLevelOnly
}

public sealed record OwnOperationSuppression(
    string? OldPath,
    string? NewPath,
    DateTimeOffset RegisteredAt,
    TimeSpan SuppressionWindow)
{
    public static readonly TimeSpan DefaultSuppressionWindow = TimeSpan.FromSeconds(30);
}

public sealed record TriageRequest(
    string Path,
    FileEventKind EventKind,
    bool IsDirectory,
    bool IsUnderEnabledIntakeFolder,
    bool IsStable,
    DateTimeOffset ObservedAt,
    IReadOnlyCollection<OwnOperationSuppression>? OwnOperations = null);

public sealed record TriageDecision(
    TriageCategory Category,
    ProcessingState ProcessingState,
    string Reason,
    double Confidence,
    bool PromptAllowed,
    FolderContextRecommendation FolderContextRecommendation,
    string MatchedRule);
