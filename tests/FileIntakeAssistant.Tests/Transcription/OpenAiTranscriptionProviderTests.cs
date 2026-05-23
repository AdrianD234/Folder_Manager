using System.Net;
using FileIntakeAssistant.Core.Transcription;
using FileIntakeAssistant.Infrastructure.Transcription;

namespace FileIntakeAssistant.Tests.Transcription;

public sealed class OpenAiTranscriptionProviderTests : IDisposable
{
    private const string TestEnvironmentVariable = "FILE_INTAKE_ASSISTANT_TEST_OPENAI_KEY";
    private const string TestApiKey = "sk-test-secret-value";

    private readonly string _testRoot = Path.Combine(
        Path.GetTempPath(),
        "FileIntakeAssistant.Tests",
        Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        var fullRoot = Path.GetFullPath(_testRoot);
        var allowedRoot = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "FileIntakeAssistant.Tests"));

        if (fullRoot.StartsWith(allowedRoot, StringComparison.OrdinalIgnoreCase) && Directory.Exists(fullRoot))
        {
            Directory.Delete(fullRoot, recursive: true);
        }
    }

    [Fact]
    public async Task OpenAi_MissingApiKeyReportsNotConfiguredAndDoesNotCallHttp()
    {
        var handlerCalled = false;
        using var httpClient = new HttpClient(new DelegateHttpMessageHandler((_, _) =>
        {
            handlerCalled = true;
            throw new InvalidOperationException("HTTP should not be called without an API key.");
        }));
        var provider = CreateProvider(
            httpClient,
            apiKeyProvider: new FixedOpenAiApiKeyProvider(null));

        var result = await provider.TranscribeAsync(new TranscriptionRequest(
            AudioPath: CreateTempAudioFile("missing-key.wav"),
            Options: new TranscriptionOptions()));

        Assert.Equal(TranscriptionProviderStatus.NotConfigured, result.Status);
        Assert.Contains(TestEnvironmentVariable, result.ErrorMessage!, StringComparison.Ordinal);
        Assert.False(handlerCalled);
    }

    [Fact]
    public async Task OpenAi_DisabledProviderReportsNotConfiguredAndDoesNotCallHttp()
    {
        var handlerCalled = false;
        using var httpClient = new HttpClient(new DelegateHttpMessageHandler((_, _) =>
        {
            handlerCalled = true;
            throw new InvalidOperationException("HTTP should not be called while provider is disabled.");
        }));
        var provider = new OpenAiTranscriptionProvider(
            httpClient,
            new OpenAiTranscriptionProviderOptions
            {
                Enabled = false,
                ApiKeyEnvironmentVariableName = TestEnvironmentVariable
            },
            new FixedOpenAiApiKeyProvider(TestApiKey));

        var result = await provider.TranscribeAsync(new TranscriptionRequest(
            AudioPath: CreateTempAudioFile("disabled.wav"),
            Options: new TranscriptionOptions()));

        Assert.Equal(TranscriptionProviderStatus.NotConfigured, result.Status);
        Assert.Contains("disabled", result.ErrorMessage!, StringComparison.OrdinalIgnoreCase);
        Assert.False(handlerCalled);
    }

    [Fact]
    public async Task OpenAi_FakeHttpSuccessReturnsTranscriptAndSafeMetadata()
    {
        using var httpClient = new HttpClient(new DelegateHttpMessageHandler(async (request, cancellationToken) =>
        {
            Assert.Equal(HttpMethod.Post, request.Method);
            Assert.Equal(OpenAiTranscriptionDefaults.TranscriptionsEndpoint, request.RequestUri);
            Assert.Equal("Bearer", request.Headers.Authorization?.Scheme);
            Assert.Equal(TestApiKey, request.Headers.Authorization?.Parameter);

            var requestBody = await request.Content!.ReadAsStringAsync(cancellationToken);
            Assert.Contains(OpenAiTranscriptionDefaults.Model, requestBody, StringComparison.Ordinal);
            Assert.Contains("language", requestBody, StringComparison.Ordinal);
            Assert.Contains("en", requestBody, StringComparison.Ordinal);
            Assert.Contains("placeholder audio", requestBody, StringComparison.Ordinal);

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""{"text":"OpenAI transcript for review."}""")
            };
        }));
        var provider = CreateProvider(httpClient, new FixedOpenAiApiKeyProvider(TestApiKey));

        var result = await provider.TranscribeAsync(new TranscriptionRequest(
            AudioPath: CreateTempAudioFile("success.wav"),
            Options: new TranscriptionOptions(Language: "en")));

        Assert.Equal(TranscriptionProviderStatus.Succeeded, result.Status);
        Assert.Equal("OpenAI transcript for review.", result.TranscriptText);
        Assert.Null(result.ErrorMessage);
        Assert.DoesNotContain(TestApiKey, result.ProviderMetadataJson ?? string.Empty, StringComparison.Ordinal);
        Assert.Contains(OpenAiTranscriptionDefaults.ProviderName, result.ProviderMetadataJson!, StringComparison.Ordinal);
        Assert.Contains(OpenAiTranscriptionDefaults.Model, result.ProviderMetadataJson!, StringComparison.Ordinal);
    }

    [Fact]
    public async Task OpenAi_ErrorResponseRedactsApiKeyFromPersistableFailureFields()
    {
        using var httpClient = new HttpClient(new DelegateHttpMessageHandler((_, _) =>
        {
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.InternalServerError)
            {
                ReasonPhrase = $"server echoed {TestApiKey}",
                Content = new StringContent($$"""{"error":"bad key {{TestApiKey}}"}""")
            });
        }));
        var provider = CreateProvider(httpClient, new FixedOpenAiApiKeyProvider(TestApiKey));

        var result = await provider.TranscribeAsync(new TranscriptionRequest(
            AudioPath: CreateTempAudioFile("failure.wav"),
            Options: new TranscriptionOptions()));

        Assert.Equal(TranscriptionProviderStatus.Failed, result.Status);
        Assert.DoesNotContain(TestApiKey, result.ErrorMessage!, StringComparison.Ordinal);
        Assert.Contains(SecretRedactor.RedactedToken, result.ErrorMessage!, StringComparison.Ordinal);
        Assert.Null(result.ProviderMetadataJson);
    }

    [Fact]
    public async Task OpenAi_SuccessCanUseDefaultAudioCleanupPolicy()
    {
        var audioService = new AudioTempFileService(Path.Combine(_testRoot, "File Intake Assistant", "temp-audio"));
        var audioPath = audioService.CreateTempAudioPath(".wav");
        await File.WriteAllTextAsync(audioPath, "placeholder audio");

        using var httpClient = new HttpClient(new DelegateHttpMessageHandler((_, _) =>
        {
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""{"text":"Cleanup transcript."}""")
            });
        }));
        var provider = CreateProvider(httpClient, new FixedOpenAiApiKeyProvider(TestApiKey));

        var result = await provider.TranscribeAsync(new TranscriptionRequest(
            AudioPath: audioPath,
            Options: new TranscriptionOptions()));
        var cleanup = await audioService.ApplyRetentionPolicyAsync(
            audioPath,
            result.Status,
            new TranscriptionOptions());

        Assert.Equal(TranscriptionProviderStatus.Succeeded, result.Status);
        Assert.Equal(AudioTempCleanupStatus.Deleted, cleanup.Status);
        Assert.False(File.Exists(audioPath));
    }

    private static OpenAiTranscriptionProvider CreateProvider(
        HttpClient httpClient,
        IOpenAiApiKeyProvider apiKeyProvider)
    {
        return new OpenAiTranscriptionProvider(
            httpClient,
            new OpenAiTranscriptionProviderOptions
            {
                Enabled = true,
                ApiKeyEnvironmentVariableName = TestEnvironmentVariable
            },
            apiKeyProvider);
    }

    private string CreateTempAudioFile(string fileName)
    {
        var tempAudioRoot = Path.Combine(_testRoot, "File Intake Assistant", "temp-audio");
        Directory.CreateDirectory(tempAudioRoot);
        var path = Path.Combine(tempAudioRoot, fileName);
        File.WriteAllText(path, "placeholder audio");
        return path;
    }

    private sealed class FixedOpenAiApiKeyProvider : IOpenAiApiKeyProvider
    {
        private readonly string? _apiKey;

        public FixedOpenAiApiKeyProvider(string? apiKey)
        {
            _apiKey = apiKey;
        }

        public string? GetApiKey(string environmentVariableName)
        {
            Assert.Equal(TestEnvironmentVariable, environmentVariableName);
            return _apiKey;
        }
    }

    private sealed class DelegateHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> _sendAsync;

        public DelegateHttpMessageHandler(
            Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> sendAsync)
        {
            _sendAsync = sendAsync;
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            return _sendAsync(request, cancellationToken);
        }
    }
}
