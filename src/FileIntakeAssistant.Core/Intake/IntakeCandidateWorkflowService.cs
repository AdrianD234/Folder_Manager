using System.Text.Json;
using FileIntakeAssistant.Core.Metadata;
using FileIntakeAssistant.Core.Models;
using FileIntakeAssistant.Core.Persistence;
using FileIntakeAssistant.Core.Transcription;

namespace FileIntakeAssistant.Core.Intake;

public sealed class IntakeCandidateWorkflowService
{
    private readonly IManualFileSnapshotReader _snapshotReader;
    private readonly ManualMetadataCaptureService _metadataCaptureService;
    private readonly TranscriptionWorkflowService _transcriptionWorkflowService;
    private readonly IFileIntakeStore _store;

    public IntakeCandidateWorkflowService(
        IManualFileSnapshotReader snapshotReader,
        ManualMetadataCaptureService metadataCaptureService,
        TranscriptionWorkflowService transcriptionWorkflowService,
        IFileIntakeStore store)
    {
        _snapshotReader = snapshotReader ?? throw new ArgumentNullException(nameof(snapshotReader));
        _metadataCaptureService = metadataCaptureService ?? throw new ArgumentNullException(nameof(metadataCaptureService));
        _transcriptionWorkflowService = transcriptionWorkflowService ?? throw new ArgumentNullException(nameof(transcriptionWorkflowService));
        _store = store ?? throw new ArgumentNullException(nameof(store));
    }

    public async Task<IntakeCandidateSaveResult> SaveMetadataAsync(
        IntakeCandidate candidate,
        ManualMetadataFields fields,
        DateTimeOffset capturedAt,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        ArgumentNullException.ThrowIfNull(fields);

        var reviewedTranscript = NullIfWhiteSpace(fields.TranscriptText);
        long? manualTranscriptionJobId = null;
        if (reviewedTranscript is not null)
        {
            var transcriptionResult = await _transcriptionWorkflowService.CaptureManualTextAsync(
                new ManualTextTranscriptionRequest(reviewedTranscript, capturedAt),
                cancellationToken).ConfigureAwait(false);

            if (transcriptionResult.Status != TranscriptionWorkflowStatus.Succeeded)
            {
                return FailedSave(transcriptionResult.ErrorMessage ?? "Manual transcript could not be captured.");
            }

            reviewedTranscript = transcriptionResult.TranscriptText;
            manualTranscriptionJobId = transcriptionResult.PrimaryTranscriptionJobId;
        }

        var snapshot = await _snapshotReader.ReadAsync(candidate.Path, cancellationToken)
            .ConfigureAwait(false);
        if (snapshot is null)
        {
            return FailedSave("Candidate file was not found.");
        }

        var metadataResult = await _metadataCaptureService.CaptureAsync(
            snapshot,
            fields with { TranscriptText = reviewedTranscript },
            capturedAt,
            cancellationToken,
            BuildCandidateCaptureContext(candidate, manualTranscriptionJobId)).ConfigureAwait(false);

        if (!metadataResult.Succeeded)
        {
            return FailedSave(metadataResult.FailureReason ?? "Candidate metadata was not saved.");
        }

        return new IntakeCandidateSaveResult(
            Status: IntakeCandidateWorkflowStatus.Succeeded,
            FileRecordId: metadataResult.FileRecordId,
            MetadataEntryId: metadataResult.MetadataEntryId,
            ActionId: metadataResult.ActionId,
            ManualTranscriptionJobId: manualTranscriptionJobId,
            ReviewedTranscriptText: reviewedTranscript,
            ErrorMessage: null);
    }

    public async Task<IntakeCandidateSkipResult> SkipAsync(
        IntakeCandidate candidate,
        string? reason,
        DateTimeOffset skippedAt,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(candidate);

        var actionId = await _store.AddActionAsync(new FileActionRecord(
            Id: null,
            ActionType: "IntakeCandidateSkipped",
            TargetFileRecordId: null,
            OldPath: candidate.Path,
            NewPath: candidate.Path,
            Status: "Completed",
            CreatedAt: skippedAt,
            CompletedAt: skippedAt,
            DetailsJson: JsonSerializer.Serialize(new
            {
                reason = NullIfWhiteSpace(reason) ?? "User skipped candidate.",
                fileName = candidate.FileName,
                candidate.Extension,
                candidate.SizeBytes,
                candidate.SourceIntakeFolderId,
                candidate.SourceIntakeFolderPath,
                candidate.TriageReason,
                triageCategory = candidate.TriageCategory.ToString(),
                candidate.TriageConfidence,
                mode = "CandidatePopup"
            })),
            cancellationToken).ConfigureAwait(false);

        return new IntakeCandidateSkipResult(
            Status: IntakeCandidateWorkflowStatus.Succeeded,
            ActionId: actionId,
            ErrorMessage: null);
    }

    private static ManualMetadataCaptureContext BuildCandidateCaptureContext(
        IntakeCandidate candidate,
        long? manualTranscriptionJobId)
    {
        return new ManualMetadataCaptureContext(
            Source: "candidate-popup",
            FileStatus: "Captured",
            TriageCategory: candidate.TriageCategory.ToString(),
            TriageConfidence: candidate.TriageConfidence,
            SourceIntakeFolderId: candidate.SourceIntakeFolderId,
            ActionType: "IntakeCandidateMetadataSaved",
            Mode: "CandidatePopup",
            NotesJson: JsonSerializer.Serialize(new
            {
                source = "candidate-popup",
                candidateObservedAt = candidate.ObservedAt,
                candidateStableAt = candidate.StableAt,
                candidate.SourceIntakeFolderPath,
                candidate.TriageReason,
                triageCategory = candidate.TriageCategory.ToString(),
                candidate.TriageConfidence,
                hashPlan = candidate.HashPlan.ToString(),
                manualTranscriptionJobId
            }));
    }

    private static IntakeCandidateSaveResult FailedSave(string errorMessage)
    {
        return new IntakeCandidateSaveResult(
            Status: IntakeCandidateWorkflowStatus.Failed,
            FileRecordId: null,
            MetadataEntryId: null,
            ActionId: null,
            ManualTranscriptionJobId: null,
            ReviewedTranscriptText: null,
            ErrorMessage: errorMessage);
    }

    private static string? NullIfWhiteSpace(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
