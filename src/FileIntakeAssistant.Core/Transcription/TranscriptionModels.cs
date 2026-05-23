namespace FileIntakeAssistant.Core.Transcription;

public enum TranscriptionProviderStatus
{
    Succeeded,
    Failed,
    NotConfigured
}

public enum TranscriptionWorkflowStatus
{
    Succeeded,
    Failed
}

public sealed record TranscriptionOptions(
    string? Language = null,
    bool DeleteAudioAfterSuccessfulTranscription = true,
    bool RetainAudioOnFailure = false);

public sealed record TranscriptionRequest(
    string AudioPath,
    TranscriptionOptions Options,
    int? DurationMs = null);

public sealed record TranscriptionProviderResult(
    TranscriptionProviderStatus Status,
    string? TranscriptText,
    double? Confidence,
    string? ErrorMessage,
    string? ProviderMetadataJson)
{
    public static TranscriptionProviderResult Success(
        string transcriptText,
        double? confidence = null,
        string? providerMetadataJson = null)
    {
        return new TranscriptionProviderResult(
            TranscriptionProviderStatus.Succeeded,
            transcriptText,
            confidence,
            null,
            providerMetadataJson);
    }

    public static TranscriptionProviderResult Failed(string errorMessage)
    {
        return new TranscriptionProviderResult(
            TranscriptionProviderStatus.Failed,
            null,
            null,
            errorMessage,
            null);
    }

    public static TranscriptionProviderResult NotConfigured(string errorMessage)
    {
        return new TranscriptionProviderResult(
            TranscriptionProviderStatus.NotConfigured,
            null,
            null,
            errorMessage,
            null);
    }
}

public sealed record ManualTextTranscriptionRequest(
    string TranscriptText,
    DateTimeOffset CapturedAt);

public sealed record ProviderTranscriptionWorkflowRequest(
    ITranscriptionProvider Provider,
    string AudioPath,
    DateTimeOffset RequestedAt,
    int? DurationMs = null,
    TranscriptionOptions? Options = null,
    string? ManualFallbackText = null);

public sealed record TranscriptionWorkflowResult(
    TranscriptionWorkflowStatus Status,
    string ProviderName,
    string? TranscriptText,
    double? Confidence,
    bool UsedManualFallback,
    long? PrimaryTranscriptionJobId,
    long? ManualFallbackTranscriptionJobId,
    string? ErrorMessage);
