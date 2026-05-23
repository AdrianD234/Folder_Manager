namespace FileIntakeAssistant.Core.Triage;

public sealed class FileEventTriageEngine
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

    private static readonly string[] MeaningfulExtensions =
    [
        ".pdf",
        ".doc",
        ".docx",
        ".rtf",
        ".txt",
        ".md",
        ".xls",
        ".xlsx",
        ".csv",
        ".tsv",
        ".ppt",
        ".pptx",
        ".png",
        ".jpg",
        ".jpeg",
        ".gif",
        ".webp",
        ".tif",
        ".tiff",
        ".zip",
        ".7z",
        ".rar"
    ];

    private static readonly string[] RepositorySegments =
    [
        ".git",
        ".svn",
        ".hg"
    ];

    private static readonly string[] PackageSegments =
    [
        "node_modules",
        "packages"
    ];

    private static readonly string[] DevelopmentSegments =
    [
        ".venv",
        "venv",
        ".tox",
        ".mypy_cache",
        ".pytest_cache",
        ".ruff_cache",
        ".vs",
        ".idea",
        ".gradle",
        ".next",
        ".nuxt",
        "coverage"
    ];

    private static readonly string[] BuildSegments =
    [
        "bin",
        "obj",
        "target",
        "dist",
        "build"
    ];

    private static readonly string[] InstallerSegments =
    [
        "lib",
        "resources",
        "locales",
        "runtimes",
        "plugins"
    ];

    private static readonly string[] BuildExtensions =
    [
        ".dll",
        ".exe",
        ".pdb",
        ".obj",
        ".o",
        ".class",
        ".cache",
        ".map"
    ];

    private static readonly string[] BrowserSegments =
    [
        "chrome",
        "edge",
        "firefox",
        "mozilla",
        "chromium",
        "brave",
        "vivaldi"
    ];

    public TriageDecision Evaluate(TriageRequest request)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Path);

        var normalizedPath = NormalizePath(request.Path);
        var segments = SplitSegments(normalizedPath);
        var fileName = GetFileName(normalizedPath);
        var extension = GetExtension(fileName);

        if (MatchesOwnOperation(normalizedPath, request))
        {
            return Decision(
                TriageCategory.OwnOperation,
                ProcessingState.Ignored,
                "Suppressed because the path matches a pending app-registered operation inside the suppression window.",
                0.99,
                promptAllowed: false,
                FolderContextRecommendation.None,
                "own-operation");
        }

        if (!request.IsUnderEnabledIntakeFolder)
        {
            return Decision(
                TriageCategory.UnknownSafeToIgnore,
                ProcessingState.Ignored,
                "Ignored because the path is outside enabled intake folders.",
                0.95,
                promptAllowed: false,
                FolderContextRecommendation.None,
                "outside-enabled-intake-folder");
        }

        if (request.EventKind == FileEventKind.Deleted)
        {
            return Decision(
                TriageCategory.UnknownSafeToIgnore,
                ProcessingState.Ignored,
                "Ignored because deleted paths are audit events, not intake candidates.",
                0.93,
                promptAllowed: false,
                FolderContextRecommendation.None,
                "deleted-event");
        }

        if (IsBrowserCachePath(segments))
        {
            return Decision(
                TriageCategory.BrowserCacheNoise,
                ProcessingState.Ignored,
                "Ignored because the path is inside a browser cache location.",
                0.96,
                promptAllowed: false,
                FolderContextRecommendation.None,
                "browser-cache-path");
        }

        if (IsSystemOrAppDataPath(normalizedPath, segments))
        {
            return Decision(
                TriageCategory.SystemOrAppDataNoise,
                ProcessingState.Ignored,
                "Ignored because the path is inside a system, program, AppData, or app-local data location.",
                0.97,
                promptAllowed: false,
                FolderContextRecommendation.None,
                "system-or-appdata-path");
        }

        if (IsTemporaryOrPartial(fileName, extension))
        {
            return Decision(
                TriageCategory.TemporaryOrPartial,
                ProcessingState.WaitingForStability,
                "Delayed because the file name or extension indicates a temporary, lock, or partial download file.",
                0.98,
                promptAllowed: false,
                FolderContextRecommendation.None,
                "temporary-or-partial-file");
        }

        var folderContext = FolderContextRecommendation.None;

        if (ContainsAnySegment(segments, RepositorySegments))
        {
            return Decision(
                TriageCategory.DevelopmentNoise,
                ProcessingState.Ignored,
                "Suppressed because the path is inside repository metadata; use folder-level context for the repo root instead of child-file tagging.",
                0.99,
                promptAllowed: false,
                FolderContextRecommendation.PreferFolderContext,
                "repository-metadata");
        }

        if (ContainsAnySegment(segments, PackageSegments))
        {
            return Decision(
                TriageCategory.PackageInstallNoise,
                ProcessingState.Ignored,
                "Suppressed because the path is inside a package/dependency folder; use folder-level context rather than individual child prompts.",
                0.97,
                promptAllowed: false,
                FolderContextRecommendation.PreferFolderContext,
                "package-folder");
        }

        if (ContainsAnySegment(segments, BuildSegments))
        {
            return Decision(
                TriageCategory.BuildOrCompilerNoise,
                ProcessingState.Ignored,
                "Suppressed because the path is inside a build or compiler output folder.",
                0.96,
                promptAllowed: false,
                FolderContextRecommendation.PreferFolderContext,
                "build-folder");
        }

        if (ContainsAnySegment(segments, DevelopmentSegments))
        {
            return Decision(
                TriageCategory.DevelopmentNoise,
                ProcessingState.Ignored,
                "Suppressed because the path is inside a development tooling, cache, or project metadata folder.",
                0.95,
                promptAllowed: false,
                FolderContextRecommendation.PreferFolderContext,
                "development-folder");
        }

        if (ContainsAnySegment(segments, InstallerSegments) && IsInstallerLikeExtension(fileName, extension))
        {
            return Decision(
                TriageCategory.InstallerOrUnpackerBurst,
                ProcessingState.WaitingForBatchDecision,
                "Delayed because the path looks like installer or unpacker output and should be batch-classified before prompting.",
                0.82,
                promptAllowed: false,
                FolderContextRecommendation.PreferFolderContext,
                "installer-or-unpacker-output");
        }

        if (request.IsDirectory)
        {
            return Decision(
                TriageCategory.UnknownSafeToIgnore,
                ProcessingState.Ignored,
                "Ignored as a directory event; folder-level context may be added explicitly instead of prompting as a file.",
                0.8,
                promptAllowed: false,
                FolderContextRecommendation.PreferFolderContext,
                "directory-event");
        }

        if (IsBuildGeneratedFile(fileName, extension))
        {
            return Decision(
                TriageCategory.BuildOrCompilerNoise,
                ProcessingState.Ignored,
                "Suppressed because the extension indicates generated build or compiler output.",
                0.87,
                promptAllowed: false,
                FolderContextRecommendation.ManualFileLevelOnly,
                "build-generated-extension");
        }

        if (!request.IsStable)
        {
            return Decision(
                TriageCategory.NeedsMoreObservation,
                ProcessingState.WaitingForStability,
                "Delayed because the file has not passed stability checks yet.",
                0.78,
                promptAllowed: false,
                folderContext,
                "not-stable");
        }

        if (IsMeaningfulExtension(extension))
        {
            return Decision(
                TriageCategory.MeaningfulOneOff,
                ProcessingState.Candidate,
                "Candidate because the file is stable, under an enabled intake folder, and has a user-level file type.",
                0.9,
                promptAllowed: true,
                folderContext,
                "meaningful-extension");
        }

        return Decision(
            TriageCategory.UnknownSafeToIgnore,
            ProcessingState.Ignored,
            "Ignored because the stable file does not match an initial meaningful user-level file type.",
            0.55,
            promptAllowed: false,
            folderContext,
            "unknown-extension");
    }

    private static TriageDecision Decision(
        TriageCategory category,
        ProcessingState processingState,
        string reason,
        double confidence,
        bool promptAllowed,
        FolderContextRecommendation folderContextRecommendation,
        string matchedRule)
    {
        return new TriageDecision(
            category,
            processingState,
            reason,
            confidence,
            promptAllowed,
            folderContextRecommendation,
            matchedRule);
    }

    private static bool MatchesOwnOperation(string normalizedPath, TriageRequest request)
    {
        if (request.OwnOperations is null)
        {
            return false;
        }

        foreach (var operation in request.OwnOperations)
        {
            if (request.ObservedAt < operation.RegisteredAt)
            {
                continue;
            }

            if (request.ObservedAt - operation.RegisteredAt > operation.SuppressionWindow)
            {
                continue;
            }

            if (PathEquals(normalizedPath, operation.OldPath) || PathEquals(normalizedPath, operation.NewPath))
            {
                return true;
            }
        }

        return false;
    }

    private static bool PathEquals(string normalizedPath, string? candidatePath)
    {
        return !string.IsNullOrWhiteSpace(candidatePath)
            && string.Equals(normalizedPath, NormalizePath(candidatePath), StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsTemporaryOrPartial(string fileName, string extension)
    {
        return fileName.StartsWith("~$", StringComparison.OrdinalIgnoreCase)
            || TemporaryExtensions.Any(candidate => string.Equals(candidate, extension, StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsMeaningfulExtension(string extension)
    {
        return MeaningfulExtensions.Any(candidate => string.Equals(candidate, extension, StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsBuildGeneratedFile(string fileName, string extension)
    {
        return fileName.EndsWith(".min.js", StringComparison.OrdinalIgnoreCase)
            || BuildExtensions.Any(candidate => string.Equals(candidate, extension, StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsInstallerLikeExtension(string fileName, string extension)
    {
        return IsBuildGeneratedFile(fileName, extension)
            || string.Equals(extension, ".dat", StringComparison.OrdinalIgnoreCase)
            || string.Equals(extension, ".pak", StringComparison.OrdinalIgnoreCase)
            || string.Equals(extension, ".json", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsSystemOrAppDataPath(string normalizedPath, IReadOnlyCollection<string> segments)
    {
        return StartsWithPath(normalizedPath, "C:\\Windows")
            || StartsWithPath(normalizedPath, "C:\\Program Files")
            || StartsWithPath(normalizedPath, "C:\\Program Files (x86)")
            || ContainsSegment(segments, "AppData")
            || (ContainsSegment(segments, "File Intake Assistant") && ContainsSegment(segments, "Local"));
    }

    private static bool IsBrowserCachePath(IReadOnlyCollection<string> segments)
    {
        if (ContainsSegment(segments, "INetCache") || ContainsSegment(segments, "Cache2"))
        {
            return true;
        }

        var hasBrowserSegment = BrowserSegments.Any(segment => ContainsSegment(segments, segment));
        if (!hasBrowserSegment)
        {
            return false;
        }

        return ContainsSegment(segments, "Cache")
            || ContainsSegment(segments, "Code Cache")
            || ContainsSegment(segments, "GPUCache")
            || ContainsSegment(segments, "DawnCache");
    }

    private static bool StartsWithPath(string normalizedPath, string candidateRoot)
    {
        var normalizedRoot = NormalizePath(candidateRoot);
        return string.Equals(normalizedPath, normalizedRoot, StringComparison.OrdinalIgnoreCase)
            || normalizedPath.StartsWith($"{normalizedRoot}\\", StringComparison.OrdinalIgnoreCase);
    }

    private static bool ContainsAnySegment(IReadOnlyCollection<string> segments, IEnumerable<string> candidates)
    {
        return candidates.Any(candidate => ContainsSegment(segments, candidate));
    }

    private static bool ContainsSegment(IReadOnlyCollection<string> segments, string candidate)
    {
        return segments.Any(segment => string.Equals(segment, candidate, StringComparison.OrdinalIgnoreCase));
    }

    private static string NormalizePath(string path)
    {
        return path.Trim().Replace('/', '\\').TrimEnd('\\');
    }

    private static string[] SplitSegments(string normalizedPath)
    {
        return normalizedPath
            .Split('\\', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }

    private static string GetFileName(string normalizedPath)
    {
        var lastSeparator = normalizedPath.LastIndexOf('\\');
        return lastSeparator >= 0 ? normalizedPath[(lastSeparator + 1)..] : normalizedPath;
    }

    private static string GetExtension(string fileName)
    {
        var lastDot = fileName.LastIndexOf('.');
        if (lastDot <= 0)
        {
            return string.Empty;
        }

        return fileName[lastDot..];
    }
}
