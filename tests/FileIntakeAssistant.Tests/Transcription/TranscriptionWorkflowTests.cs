using FileIntakeAssistant.Core.Persistence;
using FileIntakeAssistant.Core.Transcription;
using FileIntakeAssistant.Infrastructure.Persistence;
using FileIntakeAssistant.Infrastructure.Transcription;
using Microsoft.Data.Sqlite;

namespace FileIntakeAssistant.Tests.Transcription;

public sealed class TranscriptionWorkflowTests : IAsyncLifetime
{
    private static readonly DateTimeOffset FixedNow = new(2026, 5, 23, 6, 0, 0, TimeSpan.Zero);

    private readonly string _testRoot = Path.Combine(
        Path.GetTempPath(),
        "FileIntakeAssistant.Tests",
        Guid.NewGuid().ToString("N"));

    private string DatabasePath => Path.Combine(_testRoot, "File Intake Assistant", "data", "file-intake-test.db");

    public Task InitializeAsync()
    {
        return Task.CompletedTask;
    }

    public Task DisposeAsync()
    {
        var fullRoot = Path.GetFullPath(_testRoot);
        var allowedRoot = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "FileIntakeAssistant.Tests"));

        if (fullRoot.StartsWith(allowedRoot, StringComparison.OrdinalIgnoreCase) && Directory.Exists(fullRoot))
        {
            SqliteConnection.ClearAllPools();
            Directory.Delete(fullRoot, recursive: true);
        }

        return Task.CompletedTask;
    }

    [Fact]
    public async Task Transcription_FakeProviderSuccessRecordsCompletedJob()
    {
        var store = await CreateStoreAsync();
        var service = new TranscriptionWorkflowService(store);
        var audioPath = CreateTempAudioFile("success.wav");
        var provider = new FakeTranscriptionProvider(TranscriptionProviderResult.Success(
            "Transcribed context for the file.",
            confidence: 0.91,
            providerMetadataJson: """{"fake":true}"""));

        var result = await service.TranscribeWithFallbackAsync(new ProviderTranscriptionWorkflowRequest(
            Provider: provider,
            AudioPath: audioPath,
            RequestedAt: FixedNow,
            DurationMs: 1_500));

        Assert.Equal(TranscriptionWorkflowStatus.Succeeded, result.Status);
        Assert.False(result.UsedManualFallback);
        Assert.Equal("Fake", result.ProviderName);
        Assert.Equal("Transcribed context for the file.", result.TranscriptText);
        Assert.Equal(0.91, result.Confidence);

        var job = await store.GetTranscriptionJobAsync(result.PrimaryTranscriptionJobId!.Value);
        Assert.NotNull(job);
        Assert.Equal("Fake", job.Provider);
        Assert.Equal(audioPath, job.AudioPath);
        Assert.Equal("Completed", job.Status);
        Assert.Equal("Transcribed context for the file.", job.TranscriptText);
        Assert.DoesNotContain("API", job.ProviderMetadataJson ?? string.Empty, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Transcription_ProviderFailureFallsBackToManualText()
    {
        var store = await CreateStoreAsync();
        var service = new TranscriptionWorkflowService(store);
        var provider = new FakeTranscriptionProvider(TranscriptionProviderResult.Failed("Synthetic provider failure."));

        var result = await service.TranscribeWithFallbackAsync(new ProviderTranscriptionWorkflowRequest(
            Provider: provider,
            AudioPath: CreateTempAudioFile("failure.wav"),
            RequestedAt: FixedNow,
            ManualFallbackText: "Manual fallback transcript."));

        Assert.Equal(TranscriptionWorkflowStatus.Succeeded, result.Status);
        Assert.True(result.UsedManualFallback);
        Assert.Equal("ManualText", result.ProviderName);
        Assert.Equal("Manual fallback transcript.", result.TranscriptText);

        var providerJob = await store.GetTranscriptionJobAsync(result.PrimaryTranscriptionJobId!.Value);
        var fallbackJob = await store.GetTranscriptionJobAsync(result.ManualFallbackTranscriptionJobId!.Value);

        Assert.NotNull(providerJob);
        Assert.Equal("Failed", providerJob.Status);
        Assert.Equal("Synthetic provider failure.", providerJob.ErrorMessage);

        Assert.NotNull(fallbackJob);
        Assert.Equal("ManualText", fallbackJob.Provider);
        Assert.Equal("Completed", fallbackJob.Status);
        Assert.Null(fallbackJob.AudioPath);
        Assert.Equal("Manual fallback transcript.", fallbackJob.TranscriptText);
    }

    [Fact]
    public async Task Transcription_LocalProviderNoKeyModeFallsBackToManualText()
    {
        var store = await CreateStoreAsync();
        var service = new TranscriptionWorkflowService(store);

        var result = await service.TranscribeWithFallbackAsync(new ProviderTranscriptionWorkflowRequest(
            Provider: new LocalTranscriptionProvider(),
            AudioPath: CreateTempAudioFile("local.wav"),
            RequestedAt: FixedNow,
            ManualFallbackText: "No-key manual transcript."));

        Assert.Equal(TranscriptionWorkflowStatus.Succeeded, result.Status);
        Assert.True(result.UsedManualFallback);
        Assert.Equal("No-key manual transcript.", result.TranscriptText);

        var providerJob = await store.GetTranscriptionJobAsync(result.PrimaryTranscriptionJobId!.Value);
        Assert.NotNull(providerJob);
        Assert.Equal("Local", providerJob.Provider);
        Assert.Equal("NotConfigured", providerJob.Status);
        Assert.Contains("not configured", providerJob.ErrorMessage!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Transcription_ManualTextCaptureWorksWithoutAudioProvider()
    {
        var store = await CreateStoreAsync();
        var service = new TranscriptionWorkflowService(store);

        var result = await service.CaptureManualTextAsync(new ManualTextTranscriptionRequest(
            TranscriptText: "Typed transcript only.",
            CapturedAt: FixedNow));

        Assert.Equal(TranscriptionWorkflowStatus.Succeeded, result.Status);
        Assert.True(result.UsedManualFallback);
        Assert.Equal("ManualText", result.ProviderName);
        Assert.Equal("Typed transcript only.", result.TranscriptText);

        var job = await store.GetTranscriptionJobAsync(result.PrimaryTranscriptionJobId!.Value);
        Assert.NotNull(job);
        Assert.Equal("ManualText", job.Provider);
        Assert.Equal("Completed", job.Status);
        Assert.Null(job.AudioPath);
    }

    [Fact]
    public async Task Transcription_TempAudioCleanupDeletesSuccessfulAudioByDefault()
    {
        var audioService = CreateAudioService();
        var audioPath = audioService.CreateTempAudioPath(".wav");
        await File.WriteAllTextAsync(audioPath, "placeholder audio");

        var result = await audioService.ApplyRetentionPolicyAsync(
            audioPath,
            TranscriptionProviderStatus.Succeeded,
            new TranscriptionOptions());

        Assert.Equal(AudioTempCleanupStatus.Deleted, result.Status);
        Assert.False(File.Exists(audioPath));
    }

    [Fact]
    public async Task Transcription_TempAudioCleanupRetainsFailedOrExplicitlyRetainedAudio()
    {
        var audioService = CreateAudioService();
        var failedAudioPath = audioService.CreateTempAudioPath(".wav");
        var retainedAudioPath = audioService.CreateTempAudioPath(".wav");
        await File.WriteAllTextAsync(failedAudioPath, "failed audio");
        await File.WriteAllTextAsync(retainedAudioPath, "retained audio");

        var failedResult = await audioService.ApplyRetentionPolicyAsync(
            failedAudioPath,
            TranscriptionProviderStatus.Failed,
            new TranscriptionOptions());
        var retainedResult = await audioService.ApplyRetentionPolicyAsync(
            retainedAudioPath,
            TranscriptionProviderStatus.Succeeded,
            new TranscriptionOptions(DeleteAudioAfterSuccessfulTranscription: false));

        Assert.Equal(AudioTempCleanupStatus.Retained, failedResult.Status);
        Assert.Equal(AudioTempCleanupStatus.Retained, retainedResult.Status);
        Assert.True(File.Exists(failedAudioPath));
        Assert.True(File.Exists(retainedAudioPath));
    }

    [Fact]
    public async Task Transcription_TempAudioCleanupRefusesPathsOutsideTempAudioRoot()
    {
        var audioService = CreateAudioService();
        var outsidePath = Path.Combine(_testRoot, "outside.wav");
        Directory.CreateDirectory(_testRoot);
        await File.WriteAllTextAsync(outsidePath, "outside audio");

        var result = await audioService.ApplyRetentionPolicyAsync(
            outsidePath,
            TranscriptionProviderStatus.Succeeded,
            new TranscriptionOptions());

        Assert.Equal(AudioTempCleanupStatus.RefusedUnsafePath, result.Status);
        Assert.True(File.Exists(outsidePath));
    }

    private async Task<IFileIntakeStore> CreateStoreAsync()
    {
        Assert.StartsWith(
            Path.GetFullPath(Path.GetTempPath()),
            Path.GetFullPath(DatabasePath),
            StringComparison.OrdinalIgnoreCase);

        var migrationRunner = new SqliteMigrationRunner();
        await migrationRunner.ApplyMigrationsAsync(DatabasePath);
        return new SqliteFileIntakeStore(DatabasePath);
    }

    private AudioTempFileService CreateAudioService()
    {
        var tempAudioRoot = Path.Combine(_testRoot, "File Intake Assistant", "temp-audio");
        return new AudioTempFileService(tempAudioRoot);
    }

    private string CreateTempAudioFile(string fileName)
    {
        var tempAudioRoot = Path.Combine(_testRoot, "File Intake Assistant", "temp-audio");
        Directory.CreateDirectory(tempAudioRoot);
        var path = Path.Combine(tempAudioRoot, fileName);
        File.WriteAllText(path, "placeholder audio");
        return path;
    }

    private sealed class FakeTranscriptionProvider : ITranscriptionProvider
    {
        private readonly TranscriptionProviderResult _result;

        public FakeTranscriptionProvider(TranscriptionProviderResult result)
        {
            _result = result;
        }

        public string Name => "Fake";

        public Task<TranscriptionProviderResult> TranscribeAsync(
            TranscriptionRequest request,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(request);
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(_result);
        }
    }
}
