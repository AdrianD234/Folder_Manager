namespace FileIntakeAssistant.Core.Batching;

public sealed class BatchDetector
{
    private static readonly string[] ArchiveExtractionSegments =
    [
        "__MACOSX",
        "extracted",
        "extract",
        "unzipped",
        "unpacked"
    ];

    private static readonly string[] OneDriveSegments =
    [
        "OneDrive",
        "OneDrive - Personal",
        "OneDrive - Business"
    ];

    private static readonly string[] InstallerSegments =
    [
        "bin",
        "lib",
        "resources",
        "locales",
        "runtimes",
        "plugins"
    ];

    private static readonly string[] PackageSegments =
    [
        "node_modules",
        "packages",
        ".nuget",
        ".gradle"
    ];

    private static readonly string[] BuildSegments =
    [
        "obj",
        "target",
        "dist",
        "build"
    ];

    public BatchDetectionResult Evaluate(BatchDetectionRequest request)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(request.RootPath);

        var options = request.Options ?? BatchDetectionOptions.Defaults;
        var rootPath = NormalizePath(request.RootPath);
        var events = request.Events
            .Where(fileEvent => IsUnderRoot(fileEvent.Path, rootPath))
            .OrderBy(fileEvent => fileEvent.ObservedAt)
            .ToArray();

        if (events.Length == 0)
        {
            return new BatchDetectionResult(
                RootPath: rootPath,
                BatchType: EventBatchType.None,
                Decision: BatchPromptDecision.NoBatch,
                FileCount: 0,
                StartedAt: null,
                EndedAt: null,
                SuppressIndividualPrompts: false,
                Reason: "No events were observed under the root path.");
        }

        var batchType = ClassifyBatchType(rootPath, events);
        var startedAt = events[0].ObservedAt;
        var endedAt = events[^1].ObservedAt;

        if (CountWithinWindow(events, options.BatchReviewOnlyWindow) > options.BatchReviewOnlyFileCountThreshold)
        {
            return Result(
                rootPath,
                batchType,
                BatchPromptDecision.BatchReviewOnly,
                events.Length,
                startedAt,
                endedAt,
                suppressIndividualPrompts: true,
                "More than 200 files were observed within 5 minutes; individual prompts are suppressed and batch review is required.");
        }

        if (CountWithinWindow(events, options.SuppressIndividualPromptsWindow) > options.SuppressIndividualPromptsFileCountThreshold)
        {
            return Result(
                rootPath,
                batchType,
                BatchPromptDecision.SuppressIndividualPrompts,
                events.Length,
                startedAt,
                endedAt,
                suppressIndividualPrompts: true,
                "More than 50 files were observed within 60 seconds; individual prompts are suppressed.");
        }

        if (CountWithinWindow(events, options.PossibleBatchWindow) > options.PossibleBatchFileCountThreshold)
        {
            return Result(
                rootPath,
                batchType,
                BatchPromptDecision.PossibleBatch,
                events.Length,
                startedAt,
                endedAt,
                suppressIndividualPrompts: false,
                "More than 10 files were observed within 10 seconds; this is a possible batch and prompting should wait for a final decision.");
        }

        return Result(
            rootPath,
            batchType == EventBatchType.None ? EventBatchType.None : batchType,
            BatchPromptDecision.NoBatch,
            events.Length,
            startedAt,
            endedAt,
            suppressIndividualPrompts: false,
            "The observed event count does not exceed the documented batch thresholds.");
    }

    private static BatchDetectionResult Result(
        string rootPath,
        EventBatchType batchType,
        BatchPromptDecision decision,
        int fileCount,
        DateTimeOffset startedAt,
        DateTimeOffset endedAt,
        bool suppressIndividualPrompts,
        string reason)
    {
        return new BatchDetectionResult(
            RootPath: rootPath,
            BatchType: batchType == EventBatchType.None && decision != BatchPromptDecision.NoBatch
                ? EventBatchType.UnknownBurst
                : batchType,
            Decision: decision,
            FileCount: fileCount,
            StartedAt: startedAt,
            EndedAt: endedAt,
            SuppressIndividualPrompts: suppressIndividualPrompts,
            Reason: reason);
    }

    private static int CountWithinWindow(IReadOnlyList<FileBatchEvent> events, TimeSpan window)
    {
        var maxCount = 0;

        for (var startIndex = 0; startIndex < events.Count; startIndex++)
        {
            var windowStart = events[startIndex].ObservedAt;
            var count = 0;

            for (var index = startIndex; index < events.Count; index++)
            {
                if (events[index].ObservedAt - windowStart <= window)
                {
                    count++;
                    continue;
                }

                break;
            }

            maxCount = Math.Max(maxCount, count);
        }

        return maxCount;
    }

    private static EventBatchType ClassifyBatchType(string rootPath, IReadOnlyCollection<FileBatchEvent> events)
    {
        var allSegments = events
            .SelectMany(fileEvent => SplitSegments(fileEvent.Path))
            .Concat(SplitSegments(rootPath))
            .ToArray();

        if (ContainsAnySegment(allSegments, OneDriveSegments))
        {
            return EventBatchType.OneDriveSyncBurst;
        }

        if (ContainsAnySegment(allSegments, PackageSegments))
        {
            return EventBatchType.PackageInstallNoise;
        }

        if (ContainsAnySegment(allSegments, ArchiveExtractionSegments) || LooksLikeArchiveExtraction(rootPath, events))
        {
            return EventBatchType.ArchiveExtractionBatch;
        }

        if (ContainsAnySegment(allSegments, InstallerSegments) && events.Any(IsInstallerLikeOutput))
        {
            return EventBatchType.InstallerOrUnpackerBurst;
        }

        if (ContainsAnySegment(allSegments, BuildSegments))
        {
            return EventBatchType.BuildOrCompilerNoise;
        }

        return EventBatchType.None;
    }

    private static bool LooksLikeArchiveExtraction(string rootPath, IReadOnlyCollection<FileBatchEvent> events)
    {
        var rootName = SplitSegments(rootPath).LastOrDefault() ?? string.Empty;
        if (rootName.EndsWith(".zip", StringComparison.OrdinalIgnoreCase)
            || rootName.EndsWith(".7z", StringComparison.OrdinalIgnoreCase)
            || rootName.EndsWith(".rar", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var nestedDirectoryCount = events
            .Select(fileEvent => NormalizePath(fileEvent.Path))
            .Select(path => path.StartsWith(rootPath, StringComparison.OrdinalIgnoreCase) ? path[rootPath.Length..] : path)
            .Select(relative => relative.Split('\\', StringSplitOptions.RemoveEmptyEntries).Length)
            .Count(segmentCount => segmentCount > 2);

        return nestedDirectoryCount >= Math.Min(10, events.Count);
    }

    private static bool IsInstallerLikeOutput(FileBatchEvent fileEvent)
    {
        var extension = GetExtension(fileEvent.Path);
        return string.Equals(extension, ".dll", StringComparison.OrdinalIgnoreCase)
            || string.Equals(extension, ".exe", StringComparison.OrdinalIgnoreCase)
            || string.Equals(extension, ".pdb", StringComparison.OrdinalIgnoreCase)
            || string.Equals(extension, ".dat", StringComparison.OrdinalIgnoreCase)
            || string.Equals(extension, ".pak", StringComparison.OrdinalIgnoreCase)
            || string.Equals(extension, ".json", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsUnderRoot(string path, string rootPath)
    {
        var normalizedPath = NormalizePath(path);
        return string.Equals(normalizedPath, rootPath, StringComparison.OrdinalIgnoreCase)
            || normalizedPath.StartsWith($"{rootPath}\\", StringComparison.OrdinalIgnoreCase);
    }

    private static bool ContainsAnySegment(IEnumerable<string> segments, IEnumerable<string> candidates)
    {
        return candidates.Any(candidate => segments.Any(segment => string.Equals(segment, candidate, StringComparison.OrdinalIgnoreCase)));
    }

    private static string[] SplitSegments(string path)
    {
        return NormalizePath(path).Split('\\', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }

    private static string GetExtension(string path)
    {
        var fileName = SplitSegments(path).LastOrDefault() ?? path;
        var lastDot = fileName.LastIndexOf('.');
        return lastDot <= 0 ? string.Empty : fileName[lastDot..];
    }

    private static string NormalizePath(string path)
    {
        return path.Trim().Replace('/', '\\').TrimEnd('\\');
    }
}
