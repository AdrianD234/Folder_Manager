namespace FileIntakeAssistant.Infrastructure.Transcription;

public interface IOpenAiApiKeyProvider
{
    string? GetApiKey(string environmentVariableName);
}

public sealed class EnvironmentOpenAiApiKeyProvider : IOpenAiApiKeyProvider
{
    public string? GetApiKey(string environmentVariableName)
    {
        var name = string.IsNullOrWhiteSpace(environmentVariableName)
            ? OpenAiTranscriptionDefaults.ApiKeyEnvironmentVariableName
            : environmentVariableName.Trim();

        return Environment.GetEnvironmentVariable(name);
    }
}
