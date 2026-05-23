using FileIntakeAssistant.Core.Batching;
using FileIntakeAssistant.Core.Models;
using FileIntakeAssistant.Core.Triage;

namespace FileIntakeAssistant.Core.Intake;

public sealed class IntakeEventProcessor
{
    private readonly FileEventTriageEngine _triageEngine;
    private readonly IIntakeCandidateQueue _candidateQueue;

    public IntakeEventProcessor(
        FileEventTriageEngine triageEngine,
        IIntakeCandidateQueue candidateQueue)
    {
        _triageEngine = triageEngine ?? throw new ArgumentNullException(nameof(triageEngine));
        _candidateQueue = candidateQueue ?? throw new ArgumentNullException(nameof(candidateQueue));
    }

    public IntakeProcessingResult Process(IntakeProcessingRequest request)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Path);
        ArgumentNullException.ThrowIfNull(request.ConfiguredFolders);
        ArgumentNullException.ThrowIfNull(request.StabilityDecision);
        ArgumentNullException.ThrowIfNull(request.BatchDecision);

        var matchedFolder = FindMatchingEnabledFolder(request.Path, request.ConfiguredFolders);
        var triageDecision = _triageEngine.Evaluate(new TriageRequest(
            Path: request.Path,
            EventKind: request.EventKind,
            IsDirectory: request.IsDirectory,
            IsUnderEnabledIntakeFolder: matchedFolder is not null,
            IsStable: request.StabilityDecision.IsStable,
            ObservedAt: request.ObservedAt,
            OwnOperations: request.OwnOperations));

        if (matchedFolder is null)
        {
            return Result(
                IntakeProcessingOutcome.OutsideConfiguredFolders,
                "Ignored because the event path is outside explicitly configured enabled intake folders.",
                matchedFolder,
                triageDecision,
                request,
                candidate: null);
        }

        if (request.BatchDecision.SuppressIndividualPrompts)
        {
            return Result(
                IntakeProcessingOutcome.BatchSuppressed,
                request.BatchDecision.Reason,
                matchedFolder,
                triageDecision,
                request,
                candidate: null);
        }

        if (request.BatchDecision.Decision == BatchPromptDecision.PossibleBatch)
        {
            return Result(
                IntakeProcessingOutcome.WaitingForBatchDecision,
                request.BatchDecision.Reason,
                matchedFolder,
                triageDecision,
                request,
                candidate: null);
        }

        if (!request.StabilityDecision.IsStable)
        {
            return Result(
                IntakeProcessingOutcome.WaitingForStability,
                request.StabilityDecision.Reason,
                matchedFolder,
                triageDecision,
                request,
                candidate: null);
        }

        if (!triageDecision.PromptAllowed)
        {
            return Result(
                IntakeProcessingOutcome.Ignored,
                triageDecision.Reason,
                matchedFolder,
                triageDecision,
                request,
                candidate: null);
        }

        var candidate = new IntakeCandidate(
            Path: NormalizePath(request.Path),
            FileName: GetFileName(request.Path),
            Extension: GetExtension(request.Path),
            SizeBytes: request.SizeBytes,
            SourceIntakeFolderId: matchedFolder.Id,
            SourceIntakeFolderPath: NormalizePath(matchedFolder.Path),
            ObservedAt: request.ObservedAt,
            StableAt: request.StabilityDecision.StableSince,
            TriageCategory: triageDecision.Category,
            TriageReason: triageDecision.Reason,
            TriageConfidence: triageDecision.Confidence,
            HashPlan: request.StabilityDecision.HashPlan);

        _candidateQueue.Enqueue(candidate);

        return Result(
            IntakeProcessingOutcome.CandidateQueued,
            "Queued a meaningful stable intake candidate for user review.",
            matchedFolder,
            triageDecision,
            request,
            candidate);
    }

    private static IntakeProcessingResult Result(
        IntakeProcessingOutcome outcome,
        string reason,
        IntakeFolder? matchedFolder,
        TriageDecision triageDecision,
        IntakeProcessingRequest request,
        IntakeCandidate? candidate)
    {
        return new IntakeProcessingResult(
            Outcome: outcome,
            Reason: reason,
            MatchedIntakeFolder: matchedFolder,
            TriageDecision: triageDecision,
            StabilityDecision: request.StabilityDecision,
            BatchDecision: request.BatchDecision,
            Candidate: candidate);
    }

    private static IntakeFolder? FindMatchingEnabledFolder(string path, IReadOnlyList<IntakeFolder> configuredFolders)
    {
        var normalizedPath = NormalizePath(path);

        return configuredFolders
            .Where(folder => folder.Enabled)
            .Where(folder => IsUnderFolder(normalizedPath, folder))
            .OrderByDescending(folder => NormalizePath(folder.Path).Length)
            .FirstOrDefault();
    }

    private static bool IsUnderFolder(string normalizedPath, IntakeFolder folder)
    {
        var normalizedFolder = NormalizePath(folder.Path);
        if (string.Equals(normalizedPath, normalizedFolder, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (!normalizedPath.StartsWith($"{normalizedFolder}\\", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (folder.Recursive)
        {
            return true;
        }

        var parent = GetParentDirectory(normalizedPath);
        return string.Equals(parent, normalizedFolder, StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizePath(string path)
    {
        return path.Trim().Replace('/', '\\').TrimEnd('\\');
    }

    private static string GetFileName(string path)
    {
        return NormalizePath(path)
            .Split('\\', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .LastOrDefault() ?? string.Empty;
    }

    private static string GetExtension(string path)
    {
        var fileName = GetFileName(path);
        var lastDot = fileName.LastIndexOf('.');
        return lastDot <= 0 ? string.Empty : fileName[lastDot..];
    }

    private static string? GetParentDirectory(string normalizedPath)
    {
        var lastSeparator = normalizedPath.LastIndexOf('\\');
        return lastSeparator <= 0 ? null : normalizedPath[..lastSeparator];
    }
}
