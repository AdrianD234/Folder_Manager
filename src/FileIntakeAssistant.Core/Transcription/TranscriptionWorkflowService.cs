using System.Text.Json;
using FileIntakeAssistant.Core.Models;
using FileIntakeAssistant.Core.Persistence;

namespace FileIntakeAssistant.Core.Transcription;

public sealed class TranscriptionWorkflowService
{
    private const string ManualTextProviderName = "ManualText";

    private readonly IFileIntakeStore _store;

    public TranscriptionWorkflowService(IFileIntakeStore store)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
    }

    public async Task<TranscriptionWorkflowResult> CaptureManualTextAsync(
        ManualTextTranscriptionRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var transcript = NullIfWhiteSpace(request.TranscriptText);
        if (transcript is null)
        {
            return Failed(
                ManualTextProviderName,
                "Manual transcript text is required.",
                primaryJobId: null,
                manualFallbackJobId: null);
        }

        var jobId = await AddCompletedManualTextJobAsync(
            transcript,
            request.CapturedAt,
            cancellationToken).ConfigureAwait(false);

        return new TranscriptionWorkflowResult(
            Status: TranscriptionWorkflowStatus.Succeeded,
            ProviderName: ManualTextProviderName,
            TranscriptText: transcript,
            Confidence: 1.0,
            UsedManualFallback: true,
            PrimaryTranscriptionJobId: jobId,
            ManualFallbackTranscriptionJobId: jobId,
            ErrorMessage: null);
    }

    public async Task<TranscriptionWorkflowResult> TranscribeWithFallbackAsync(
        ProviderTranscriptionWorkflowRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Provider);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.AudioPath);

        var options = request.Options ?? new TranscriptionOptions();
        var pendingJobId = await _store.AddTranscriptionJobAsync(new TranscriptionJobRecord(
            Id: null,
            Provider: request.Provider.Name,
            AudioPath: request.AudioPath,
            DurationMs: request.DurationMs,
            TranscriptText: null,
            Status: "Pending",
            ErrorMessage: null,
            CreatedAt: request.RequestedAt,
            CompletedAt: null,
            ProviderMetadataJson: JsonSerializer.Serialize(new
            {
                options.Language,
                options.DeleteAudioAfterSuccessfulTranscription,
                options.RetainAudioOnFailure
            })),
            cancellationToken).ConfigureAwait(false);

        var providerResult = await request.Provider.TranscribeAsync(
            new TranscriptionRequest(request.AudioPath, options, request.DurationMs),
            cancellationToken).ConfigureAwait(false);

        var completedAt = request.RequestedAt;
        var statusText = ProviderStatusToJobStatus(providerResult.Status);
        await _store.UpdateTranscriptionJobAsync(new TranscriptionJobRecord(
            Id: pendingJobId,
            Provider: request.Provider.Name,
            AudioPath: request.AudioPath,
            DurationMs: request.DurationMs,
            TranscriptText: NullIfWhiteSpace(providerResult.TranscriptText),
            Status: statusText,
            ErrorMessage: NullIfWhiteSpace(providerResult.ErrorMessage),
            CreatedAt: request.RequestedAt,
            CompletedAt: completedAt,
            ProviderMetadataJson: providerResult.ProviderMetadataJson),
            cancellationToken).ConfigureAwait(false);

        var transcript = NullIfWhiteSpace(providerResult.TranscriptText);
        if (providerResult.Status == TranscriptionProviderStatus.Succeeded && transcript is not null)
        {
            return new TranscriptionWorkflowResult(
                Status: TranscriptionWorkflowStatus.Succeeded,
                ProviderName: request.Provider.Name,
                TranscriptText: transcript,
                Confidence: providerResult.Confidence,
                UsedManualFallback: false,
                PrimaryTranscriptionJobId: pendingJobId,
                ManualFallbackTranscriptionJobId: null,
                ErrorMessage: null);
        }

        var fallbackText = NullIfWhiteSpace(request.ManualFallbackText);
        if (fallbackText is not null)
        {
            var fallbackJobId = await AddCompletedManualTextJobAsync(
                fallbackText,
                request.RequestedAt,
                cancellationToken).ConfigureAwait(false);

            return new TranscriptionWorkflowResult(
                Status: TranscriptionWorkflowStatus.Succeeded,
                ProviderName: ManualTextProviderName,
                TranscriptText: fallbackText,
                Confidence: 1.0,
                UsedManualFallback: true,
                PrimaryTranscriptionJobId: pendingJobId,
                ManualFallbackTranscriptionJobId: fallbackJobId,
                ErrorMessage: null);
        }

        return Failed(
            request.Provider.Name,
            providerResult.ErrorMessage ?? "Transcription provider did not return a transcript.",
            pendingJobId,
            manualFallbackJobId: null);
    }

    private async Task<long> AddCompletedManualTextJobAsync(
        string transcript,
        DateTimeOffset capturedAt,
        CancellationToken cancellationToken)
    {
        return await _store.AddTranscriptionJobAsync(new TranscriptionJobRecord(
            Id: null,
            Provider: ManualTextProviderName,
            AudioPath: null,
            DurationMs: null,
            TranscriptText: transcript,
            Status: "Completed",
            ErrorMessage: null,
            CreatedAt: capturedAt,
            CompletedAt: capturedAt,
            ProviderMetadataJson: """{"mode":"manual"}"""),
            cancellationToken).ConfigureAwait(false);
    }

    private static TranscriptionWorkflowResult Failed(
        string providerName,
        string errorMessage,
        long? primaryJobId,
        long? manualFallbackJobId)
    {
        return new TranscriptionWorkflowResult(
            Status: TranscriptionWorkflowStatus.Failed,
            ProviderName: providerName,
            TranscriptText: null,
            Confidence: null,
            UsedManualFallback: false,
            PrimaryTranscriptionJobId: primaryJobId,
            ManualFallbackTranscriptionJobId: manualFallbackJobId,
            ErrorMessage: errorMessage);
    }

    private static string ProviderStatusToJobStatus(TranscriptionProviderStatus status)
    {
        return status switch
        {
            TranscriptionProviderStatus.Succeeded => "Completed",
            TranscriptionProviderStatus.Failed => "Failed",
            TranscriptionProviderStatus.NotConfigured => "NotConfigured",
            _ => "Failed"
        };
    }

    private static string? NullIfWhiteSpace(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
