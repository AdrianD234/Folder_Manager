namespace FileIntakeAssistant.Core.Stability;

public enum FileStabilityStatus
{
    Missing,
    WaitingForMoreObservations,
    WaitingForDebounce,
    Changing,
    Locked,
    ZeroByteTransient,
    PartialOrTemporary,
    Stable
}

public enum HashPlan
{
    NotReady,
    ComputeSha256,
    DeferLargeFile
}

public sealed record FileStabilityOptions(
    TimeSpan OrdinaryDebounceWindow,
    TimeSpan PartialTransitionDebounceWindow,
    long HashThresholdBytes,
    bool AllowZeroByteFiles)
{
    public static FileStabilityOptions Defaults { get; } = new(
        OrdinaryDebounceWindow: TimeSpan.FromSeconds(2),
        PartialTransitionDebounceWindow: TimeSpan.FromSeconds(10),
        HashThresholdBytes: 100L * 1024L * 1024L,
        AllowZeroByteFiles: false);
}

public sealed record FileStabilityObservation(
    string Path,
    bool Exists,
    bool IsDirectory,
    long SizeBytes,
    DateTimeOffset? LastWriteTimeUtc,
    bool IsLocked,
    DateTimeOffset ObservedAt,
    bool RequiresExtendedDebounce = false);

public sealed record FileStabilityRequest(
    IReadOnlyList<FileStabilityObservation> Observations,
    DateTimeOffset Now,
    FileStabilityOptions? Options = null);

public sealed record FileStabilityDecision(
    FileStabilityStatus Status,
    bool IsStable,
    string Reason,
    TimeSpan RequiredDebounceWindow,
    DateTimeOffset? StableSince,
    HashPlan HashPlan);

public interface IFileStabilitySnapshotReader
{
    Task<FileStabilityObservation> ReadAsync(
        string path,
        DateTimeOffset observedAt,
        bool requiresExtendedDebounce = false,
        CancellationToken cancellationToken = default);
}
