using FileIntakeAssistant.Core.Transcription;

namespace FileIntakeAssistant.Infrastructure.Transcription;

public sealed class LocalTranscriptionProvider : ITranscriptionProvider
{
    public string Name => "Local";

    public Task<TranscriptionProviderResult> TranscribeAsync(
        TranscriptionRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        return Task.FromResult(TranscriptionProviderResult.NotConfigured(
            "Local transcription is not configured."));
    }
}
