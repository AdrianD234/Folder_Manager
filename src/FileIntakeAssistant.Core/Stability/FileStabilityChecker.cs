namespace FileIntakeAssistant.Core.Stability;

public sealed class FileStabilityChecker
{
    private static readonly string[] TemporaryExtensions =
    [
        ".crdownload",
        ".part",
        ".tmp",
        ".download",
        ".partial",
        ".lock",
        ".swp"
    ];

    public FileStabilityDecision Evaluate(FileStabilityRequest request)
    {
        if (request.Observations.Count == 0)
        {
            return NotStable(
                FileStabilityStatus.Missing,
                "No observations were provided.",
                FileStabilityOptions.Defaults.OrdinaryDebounceWindow);
        }

        var options = request.Options ?? FileStabilityOptions.Defaults;
        var observations = request.Observations
            .OrderBy(observation => observation.ObservedAt)
            .ToArray();
        var latest = observations[^1];
        var requiredDebounce = observations.Any(observation => observation.RequiresExtendedDebounce)
            ? options.PartialTransitionDebounceWindow
            : options.OrdinaryDebounceWindow;

        if (!latest.Exists)
        {
            return NotStable(
                FileStabilityStatus.Missing,
                "The path does not exist at the latest observation.",
                requiredDebounce);
        }

        if (latest.IsDirectory)
        {
            return NotStable(
                FileStabilityStatus.Missing,
                "The path is a directory, not a file stability candidate.",
                requiredDebounce);
        }

        if (IsTemporaryOrPartialPath(latest.Path))
        {
            return NotStable(
                FileStabilityStatus.PartialOrTemporary,
                "The file still has a temporary or partial download extension.",
                requiredDebounce);
        }

        if (latest.IsLocked)
        {
            return NotStable(
                FileStabilityStatus.Locked,
                "The file is locked by another process.",
                requiredDebounce);
        }

        if (latest.SizeBytes == 0 && !options.AllowZeroByteFiles)
        {
            return NotStable(
                FileStabilityStatus.ZeroByteTransient,
                "The file is zero bytes and may be a transient create event.",
                requiredDebounce);
        }

        if (observations.Length < 2)
        {
            return NotStable(
                FileStabilityStatus.WaitingForMoreObservations,
                "At least two observations are required to verify size and timestamp stability.",
                requiredDebounce);
        }

        var stableSince = FindStableSince(observations);
        if (stableSince == latest.ObservedAt)
        {
            return new FileStabilityDecision(
                FileStabilityStatus.Changing,
                IsStable: false,
                Reason: "The latest size or last-write timestamp changed from the previous observation.",
                RequiredDebounceWindow: requiredDebounce,
                StableSince: stableSince,
                HashPlan: HashPlan.NotReady);
        }

        var stableDuration = request.Now - stableSince;
        if (stableDuration < requiredDebounce)
        {
            return new FileStabilityDecision(
                FileStabilityStatus.WaitingForDebounce,
                IsStable: false,
                Reason: "The file has not remained unchanged for the required debounce window.",
                RequiredDebounceWindow: requiredDebounce,
                StableSince: stableSince,
                HashPlan: HashPlan.NotReady);
        }

        var hashPlan = latest.SizeBytes > options.HashThresholdBytes
            ? HashPlan.DeferLargeFile
            : HashPlan.ComputeSha256;

        return new FileStabilityDecision(
            FileStabilityStatus.Stable,
            IsStable: true,
            Reason: hashPlan == HashPlan.DeferLargeFile
                ? "The file is stable, but hashing is deferred because it exceeds the configured threshold."
                : "The file is stable and eligible for ordinary hash-on-stability.",
            RequiredDebounceWindow: requiredDebounce,
            StableSince: stableSince,
            HashPlan: hashPlan);
    }

    private static FileStabilityDecision NotStable(
        FileStabilityStatus status,
        string reason,
        TimeSpan requiredDebounce)
    {
        return new FileStabilityDecision(
            status,
            IsStable: false,
            Reason: reason,
            RequiredDebounceWindow: requiredDebounce,
            StableSince: null,
            HashPlan: HashPlan.NotReady);
    }

    private static DateTimeOffset FindStableSince(IReadOnlyList<FileStabilityObservation> observations)
    {
        var latest = observations[^1];
        var stableSince = latest.ObservedAt;

        for (var index = observations.Count - 2; index >= 0; index--)
        {
            var candidate = observations[index];
            if (candidate.Exists != latest.Exists
                || candidate.IsDirectory != latest.IsDirectory
                || candidate.SizeBytes != latest.SizeBytes
                || candidate.LastWriteTimeUtc != latest.LastWriteTimeUtc
                || candidate.IsLocked != latest.IsLocked)
            {
                break;
            }

            stableSince = candidate.ObservedAt;
        }

        return stableSince;
    }

    private static bool IsTemporaryOrPartialPath(string path)
    {
        var fileName = path.Replace('/', '\\').Split('\\', StringSplitOptions.RemoveEmptyEntries).LastOrDefault() ?? path;
        if (fileName.StartsWith("~$", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var lastDot = fileName.LastIndexOf('.');
        if (lastDot <= 0)
        {
            return false;
        }

        var extension = fileName[lastDot..];
        return TemporaryExtensions.Any(candidate => string.Equals(candidate, extension, StringComparison.OrdinalIgnoreCase));
    }
}
