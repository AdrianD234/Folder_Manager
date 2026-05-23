using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using FileIntakeAssistant.Core.Transcription;

namespace FileIntakeAssistant.Infrastructure.Transcription;

public sealed class OpenAiTranscriptionProvider : ITranscriptionProvider
{
    private readonly HttpClient _httpClient;
    private readonly OpenAiTranscriptionProviderOptions _options;
    private readonly IOpenAiApiKeyProvider _apiKeyProvider;

    public OpenAiTranscriptionProvider(
        HttpClient httpClient,
        OpenAiTranscriptionProviderOptions? options = null,
        IOpenAiApiKeyProvider? apiKeyProvider = null)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _options = options ?? new OpenAiTranscriptionProviderOptions();
        _apiKeyProvider = apiKeyProvider ?? new EnvironmentOpenAiApiKeyProvider();
    }

    public string Name => OpenAiTranscriptionDefaults.ProviderName;

    public async Task<TranscriptionProviderResult> TranscribeAsync(
        TranscriptionRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        if (!_options.Enabled)
        {
            return TranscriptionProviderResult.NotConfigured(
                "OpenAI transcription provider is disabled.");
        }

        var model = NormalizeOrNull(_options.Model);
        if (model is null)
        {
            return TranscriptionProviderResult.NotConfigured(
                "OpenAI transcription model is not configured.");
        }

        var keyEnvironmentVariable = NormalizeOrDefault(
            _options.ApiKeyEnvironmentVariableName,
            OpenAiTranscriptionDefaults.ApiKeyEnvironmentVariableName);
        var apiKey = NormalizeOrNull(_apiKeyProvider.GetApiKey(keyEnvironmentVariable));
        if (apiKey is null)
        {
            return TranscriptionProviderResult.NotConfigured(
                $"OpenAI API key environment variable '{keyEnvironmentVariable}' is not configured.");
        }

        if (!File.Exists(request.AudioPath))
        {
            return TranscriptionProviderResult.Failed("Audio file was not found.");
        }

        try
        {
            using var form = new MultipartFormDataContent();
            await using var audioStream = File.OpenRead(request.AudioPath);
            using var audioContent = new StreamContent(audioStream);
            audioContent.Headers.ContentType = new MediaTypeHeaderValue(GetContentType(request.AudioPath));

            form.Add(new StringContent(model), "model");
            form.Add(new StringContent(NormalizeOrDefault(_options.ResponseFormat, "json")), "response_format");

            var language = NormalizeOrNull(request.Options.Language);
            if (language is not null)
            {
                form.Add(new StringContent(language), "language");
            }

            form.Add(audioContent, "file", Path.GetFileName(request.AudioPath));

            using var httpRequest = new HttpRequestMessage(HttpMethod.Post, _options.Endpoint)
            {
                Content = form
            };
            httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

            using var response = await _httpClient.SendAsync(
                httpRequest,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken).ConfigureAwait(false);
            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken)
                .ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                return TranscriptionProviderResult.Failed(BuildFailureMessage(
                    response.StatusCode,
                    response.ReasonPhrase,
                    responseBody,
                    apiKey));
            }

            var transcriptText = ExtractTranscriptText(responseBody);
            if (transcriptText is null)
            {
                return TranscriptionProviderResult.Failed(
                    "OpenAI transcription response did not include transcript text.");
            }

            return TranscriptionProviderResult.Success(
                transcriptText,
                confidence: null,
                providerMetadataJson: BuildProviderMetadataJson(response.StatusCode, model, _options.Endpoint));
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex) when (ex is IOException or HttpRequestException or JsonException or InvalidOperationException)
        {
            var safeMessage = SecretRedactor.Redact(ex.Message, apiKey) ?? "Unknown provider error.";
            return TranscriptionProviderResult.Failed($"OpenAI transcription request failed: {safeMessage}");
        }
    }

    private static string? ExtractTranscriptText(string responseBody)
    {
        using var document = JsonDocument.Parse(responseBody);
        return document.RootElement.TryGetProperty("text", out var textElement)
            && textElement.ValueKind == JsonValueKind.String
            ? NormalizeOrNull(textElement.GetString())
            : null;
    }

    private static string BuildProviderMetadataJson(HttpStatusCode statusCode, string model, Uri endpoint)
    {
        return JsonSerializer.Serialize(new
        {
            provider = OpenAiTranscriptionDefaults.ProviderName,
            model,
            endpoint_host = endpoint.Host,
            http_status = (int)statusCode
        });
    }

    private static string BuildFailureMessage(
        HttpStatusCode statusCode,
        string? reasonPhrase,
        string responseBody,
        string apiKey)
    {
        var safeReason = SecretRedactor.Redact(reasonPhrase, apiKey);
        var safeBody = SecretRedactor.Redact(responseBody, apiKey);
        var bodySnippet = string.IsNullOrWhiteSpace(safeBody)
            ? "No provider response body was returned."
            : Truncate(safeBody.Trim(), maxLength: 300);

        return $"OpenAI transcription request failed with HTTP {(int)statusCode} {safeReason}. {bodySnippet}";
    }

    private static string GetContentType(string audioPath)
    {
        return Path.GetExtension(audioPath).ToLowerInvariant() switch
        {
            ".m4a" => "audio/mp4",
            ".mp3" => "audio/mpeg",
            ".mp4" => "audio/mp4",
            ".mpeg" => "audio/mpeg",
            ".mpga" => "audio/mpeg",
            ".ogg" => "audio/ogg",
            ".wav" => "audio/wav",
            ".webm" => "audio/webm",
            _ => "application/octet-stream"
        };
    }

    private static string NormalizeOrDefault(string? value, string fallback)
    {
        return NormalizeOrNull(value) ?? fallback;
    }

    private static string? NormalizeOrNull(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static string Truncate(string value, int maxLength)
    {
        return value.Length <= maxLength
            ? value
            : string.Concat(value.AsSpan(0, maxLength), "...");
    }
}
