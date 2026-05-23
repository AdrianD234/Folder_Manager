using FileIntakeAssistant.Core.Models;
using FileIntakeAssistant.Core.Persistence;
using FileIntakeAssistant.Infrastructure.Logging;
using FileIntakeAssistant.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;

namespace FileIntakeAssistant.Tests.Logging;

public sealed class LocalAuditLoggingTests : IAsyncLifetime
{
    private static readonly DateTimeOffset FixedNow = new(2026, 5, 23, 7, 0, 0, TimeSpan.Zero);

    private readonly string _testRoot = Path.Combine(
        Path.GetTempPath(),
        "FileIntakeAssistant.Tests",
        Guid.NewGuid().ToString("N"));

    private string DatabasePath => Path.Combine(_testRoot, "File Intake Assistant", "data", "file-intake-test.db");

    private string LogsDirectory => Path.Combine(_testRoot, "File Intake Assistant", "logs");

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
    public async Task LocalAuditLog_RedactsSensitiveFieldsAndOpenAiStyleSecrets()
    {
        var log = new JsonLinesLocalAuditLog(LogsDirectory);

        await log.WriteAsync(
            "provider.call",
            "Failed",
            new Dictionary<string, object?>
            {
                ["apiKey"] = "sk-123456789012345678901234567890",
                ["message"] = "Provider returned sk-abcdefghijklmnopqrstuvwxyz123456",
                ["safeField"] = "kept"
            });

        var content = await File.ReadAllTextAsync(log.LogFilePath);

        Assert.Contains("provider.call", content);
        Assert.Contains("kept", content);
        Assert.Contains("[redacted]", content);
        Assert.DoesNotContain("sk-123456789012345678901234567890", content);
        Assert.DoesNotContain("sk-abcdefghijklmnopqrstuvwxyz123456", content);
    }

    [Fact]
    public async Task StructuredLoggingStore_MirrorsAuditRowsWithoutPrivatePayloads()
    {
        var store = await CreateLoggingStoreAsync();

        var actionId = await store.AddActionAsync(new FileActionRecord(
            Id: null,
            ActionType: "Move",
            TargetFileRecordId: null,
            OldPath: Path.Combine(_testRoot, "source.pdf"),
            NewPath: Path.Combine(_testRoot, "filed", "source.pdf"),
            Status: "Completed",
            CreatedAt: FixedNow,
            CompletedAt: FixedNow.AddSeconds(1),
            DetailsJson: """{"privateDetail":"do not mirror raw details"}"""));

        var jobId = await store.AddTranscriptionJobAsync(new TranscriptionJobRecord(
            Id: null,
            Provider: "Fake",
            AudioPath: Path.Combine(_testRoot, "temp-audio", "sample.wav"),
            DurationMs: 1_000,
            TranscriptText: "private spoken context",
            Status: "Completed",
            ErrorMessage: "Synthetic failure with sk-abcdefghijklmnopqrstuvwxyz123456",
            CreatedAt: FixedNow,
            CompletedAt: FixedNow.AddSeconds(2),
            ProviderMetadataJson: """{"apiKey":"sk-123456789012345678901234567890"}"""));

        var action = await store.GetActionAsync(actionId);
        var job = await store.GetTranscriptionJobAsync(jobId);
        Assert.NotNull(action);
        Assert.NotNull(job);

        var logPath = Path.Combine(LogsDirectory, "file-intake-audit.jsonl");
        var content = await File.ReadAllTextAsync(logPath);

        Assert.Contains("action.added", content);
        Assert.Contains("transcription_job.added", content);
        Assert.Contains("Move", content);
        Assert.Contains("hasTranscript", content);
        Assert.Contains("hasProviderMetadata", content);
        Assert.DoesNotContain("do not mirror raw details", content);
        Assert.DoesNotContain("private spoken context", content);
        Assert.DoesNotContain("sample.wav", content);
        Assert.DoesNotContain("sk-abcdefghijklmnopqrstuvwxyz123456", content);
        Assert.DoesNotContain("sk-123456789012345678901234567890", content);
    }

    private async Task<IFileIntakeStore> CreateLoggingStoreAsync()
    {
        var migrationRunner = new SqliteMigrationRunner();
        await migrationRunner.ApplyMigrationsAsync(DatabasePath);

        return new StructuredLoggingFileIntakeStore(
            new SqliteFileIntakeStore(DatabasePath),
            new JsonLinesLocalAuditLog(LogsDirectory));
    }
}
