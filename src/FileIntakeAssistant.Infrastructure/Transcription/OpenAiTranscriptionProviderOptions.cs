namespace FileIntakeAssistant.Infrastructure.Transcription;

public static class OpenAiTranscriptionDefaults
{
    public const string ProviderName = "OpenAI";
    public const string ApiKeyEnvironmentVariableName = "OPENAI_API_KEY";
    public const string Model = "gpt-4o-mini-transcribe";

    public static Uri TranscriptionsEndpoint { get; } = new("https://api.openai.com/v1/audio/transcriptions");
}

public sealed record OpenAiTranscriptionProviderOptions
{
    public bool Enabled { get; init; }

    public string ApiKeyEnvironmentVariableName { get; init; } =
        OpenAiTranscriptionDefaults.ApiKeyEnvironmentVariableName;

    public string Model { get; init; } = OpenAiTranscriptionDefaults.Model;

    public Uri Endpoint { get; init; } = OpenAiTranscriptionDefaults.TranscriptionsEndpoint;

    public string ResponseFormat { get; init; } = "json";
}
