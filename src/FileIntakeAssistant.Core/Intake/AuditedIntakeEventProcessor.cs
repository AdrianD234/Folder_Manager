using System.Text.Json;
using FileIntakeAssistant.Core.Models;
using FileIntakeAssistant.Core.Persistence;
using FileIntakeAssistant.Core.Triage;

namespace FileIntakeAssistant.Core.Intake;

public sealed record AuditedIntakeProcessingResult(
    IntakeProcessingResult ProcessingResult,
    long FileEventId);

public sealed class AuditedIntakeEventProcessor
{
    private readonly IntakeEventProcessor _processor;
    private readonly IFileIntakeStore _store;
    private readonly Func<DateTimeOffset> _clock;

    public AuditedIntakeEventProcessor(
        IntakeEventProcessor processor,
        IFileIntakeStore store,
        Func<DateTimeOffset>? clock = null)
    {
        _processor = processor ?? throw new ArgumentNullException(nameof(processor));
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _clock = clock ?? (() => DateTimeOffset.UtcNow);
    }

    public async Task<AuditedIntakeProcessingResult> ProcessAndAuditAsync(
        IntakeProcessingRequest request,
        string? oldPath = null,
        CancellationToken cancellationToken = default)
    {
        var processingResult = _processor.Process(request);
        var normalizedAt = _clock();

        var fileEvent = new FileEventRecord(
            Id: null,
            FileRecordId: null,
            EventType: request.EventKind.ToString(),
            RawPath: NormalizePath(request.Path),
            OldPath: oldPath is null ? null : NormalizePath(oldPath),
            NewPath: request.EventKind == FileEventKind.Renamed ? NormalizePath(request.Path) : null,
            ObservedAt: request.ObservedAt,
            NormalizedAt: normalizedAt,
            TriageCategory: processingResult.TriageDecision.Category.ToString(),
            TriageReason: processingResult.Reason,
            BatchId: null,
            Ignored: IsIgnored(processingResult.Outcome),
            DetailsJson: JsonSerializer.Serialize(new
            {
                outcome = processingResult.Outcome.ToString(),
                stability = request.StabilityDecision.Status.ToString(),
                batchDecision = request.BatchDecision.Decision.ToString(),
                batchType = request.BatchDecision.BatchType.ToString(),
                promptAllowed = processingResult.TriageDecision.PromptAllowed,
                candidateQueued = processingResult.Candidate is not null
            }));

        var fileEventId = await _store.AddFileEventAsync(fileEvent, cancellationToken).ConfigureAwait(false);
        return new AuditedIntakeProcessingResult(processingResult, fileEventId);
    }

    private static bool IsIgnored(IntakeProcessingOutcome outcome)
    {
        return outcome is IntakeProcessingOutcome.OutsideConfiguredFolders
            or IntakeProcessingOutcome.BatchSuppressed
            or IntakeProcessingOutcome.Ignored;
    }

    private static string NormalizePath(string path)
    {
        return path.Trim().Replace('/', '\\').TrimEnd('\\');
    }
}
