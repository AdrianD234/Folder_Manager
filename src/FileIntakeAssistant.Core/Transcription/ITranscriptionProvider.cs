namespace FileIntakeAssistant.Core.Transcription;

public interface ITranscriptionProvider
{
    string Name { get; }

    Task<TranscriptionProviderResult> TranscribeAsync(
        TranscriptionRequest request,
        CancellationToken cancellationToken = default);
}
