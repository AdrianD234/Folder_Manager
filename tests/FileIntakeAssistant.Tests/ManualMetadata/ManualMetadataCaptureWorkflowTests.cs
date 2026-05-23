using System.Text.Json;
using FileIntakeAssistant.Core.Metadata;
using FileIntakeAssistant.Core.Models;
using FileIntakeAssistant.Core.Persistence;
using FileIntakeAssistant.Infrastructure.FileSystem;
using FileIntakeAssistant.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;

namespace FileIntakeAssistant.Tests.ManualMetadata;

public sealed class ManualMetadataCaptureWorkflowTests : IAsyncLifetime
{
    private static readonly DateTimeOffset FixedNow = new(2026, 5, 23, 5, 0, 0, TimeSpan.Zero);

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
    public async Task ManualMetadata_SaveForSelectedTempFileStoresMetadataInSQLiteAndDoesNotModifyFile()
    {
        var store = await CreateStoreAsync();
        var source = CreateTempFile("intake", "Revenue Report.pdf", "placeholder report");
        var originalContent = await File.ReadAllTextAsync(source);
        var originalLastWriteUtc = File.GetLastWriteTimeUtc(source);

        var snapshot = await new LocalManualFileSnapshotReader().ReadAsync(source);
        Assert.NotNull(snapshot);

        var service = new ManualMetadataCaptureService(store);
        var result = await service.CaptureAsync(
            snapshot,
            new ManualMetadataFields(
                UserNote: "Downloaded for transport revenue assumptions.",
                Relevance: "high",
                Project: "Revenue Model",
                Topic: "Transport assumptions",
                Tags: "finance, transport, model-inputs, finance",
                SourceUrl: "https://example.test/report",
                TranscriptText: "Reviewed manual transcript for the revenue report."),
            FixedNow);

        Assert.True(result.Succeeded, result.FailureReason);
        Assert.NotNull(result.FileRecordId);
        Assert.NotNull(result.MetadataEntryId);
        Assert.NotNull(result.ActionId);

        var fileRecord = await store.GetFileRecordAsync(result.FileRecordId.Value);
        var metadata = await store.GetMetadataEntryAsync(result.MetadataEntryId.Value);
        var action = await store.GetActionAsync(result.ActionId.Value);

        Assert.NotNull(fileRecord);
        Assert.Equal(source, fileRecord.CurrentPath);
        Assert.Equal("Captured", fileRecord.Status);
        Assert.Equal("ManualCapture", fileRecord.TriageCategory);
        Assert.True(fileRecord.IsMeaningful);

        Assert.NotNull(metadata);
        Assert.Equal(result.FileRecordId, metadata.FileRecordId);
        Assert.Equal("Downloaded for transport revenue assumptions.", metadata.UserNote);
        Assert.Equal("Reviewed manual transcript for the revenue report.", metadata.TranscriptText);
        Assert.Equal("high", metadata.Relevance);
        Assert.Equal("Revenue Model", metadata.Project);
        Assert.Equal("Transport assumptions", metadata.Topic);
        Assert.Equal("https://example.test/report", metadata.SourceUrl);
        Assert.Equal(new[] { "finance", "transport", "model-inputs" }, JsonSerializer.Deserialize<string[]>(metadata.TagsJson!));

        Assert.NotNull(action);
        Assert.Equal("ManualMetadataCapture", action.ActionType);
        Assert.Equal("Completed", action.Status);
        Assert.Equal(result.FileRecordId, action.TargetFileRecordId);

        Assert.True(File.Exists(source));
        Assert.Equal(originalContent, await File.ReadAllTextAsync(source));
        Assert.Equal(originalLastWriteUtc, File.GetLastWriteTimeUtc(source));
        Assert.Equal(new[] { source }, Directory.GetFiles(Path.GetDirectoryName(source)!));
    }

    [Fact]
    public async Task ManualMetadata_ReusesExistingFileRecordForAdditionalMetadata()
    {
        var store = await CreateStoreAsync();
        var source = CreateTempFile("intake", "Budget.xlsx", "placeholder budget");
        var existingFileRecordId = await AddFileRecordAsync(store, source);
        var snapshot = await new LocalManualFileSnapshotReader().ReadAsync(source);
        Assert.NotNull(snapshot);

        var service = new ManualMetadataCaptureService(store);
        var result = await service.CaptureAsync(
            snapshot,
            new ManualMetadataFields(
                UserNote: "Second note.",
                Relevance: null,
                Project: null,
                Topic: null,
                Tags: null,
                SourceUrl: null),
            FixedNow);

        Assert.True(result.Succeeded, result.FailureReason);
        Assert.Equal(existingFileRecordId, result.FileRecordId);

        var metadata = await store.GetMetadataEntryAsync(result.MetadataEntryId!.Value);
        Assert.Equal(existingFileRecordId, metadata!.FileRecordId);
        Assert.Equal("Second note.", metadata.UserNote);
    }

    [Fact]
    public async Task ManualMetadata_EmptyMetadataIsRejectedWithoutCreatingFileRecord()
    {
        var store = await CreateStoreAsync();
        var source = CreateTempFile("intake", "Empty Note.pdf", "placeholder report");
        var snapshot = await new LocalManualFileSnapshotReader().ReadAsync(source);
        Assert.NotNull(snapshot);

        var service = new ManualMetadataCaptureService(store);
        var result = await service.CaptureAsync(
            snapshot,
            new ManualMetadataFields(
                UserNote: " ",
                Relevance: null,
                Project: string.Empty,
                Topic: null,
                Tags: null,
                SourceUrl: null),
            FixedNow);

        Assert.False(result.Succeeded);
        Assert.Contains("metadata", result.FailureReason!, StringComparison.OrdinalIgnoreCase);
        Assert.Null(await store.GetFileRecordByCurrentPathAsync(source));
    }

    [Fact]
    public async Task ManualMetadata_LocalSnapshotReaderReturnsNullForMissingFile()
    {
        var missingPath = Path.Combine(_testRoot, "missing.pdf");

        var snapshot = await new LocalManualFileSnapshotReader().ReadAsync(missingPath);

        Assert.Null(snapshot);
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

    private string CreateTempFile(string folderName, string fileName, string content)
    {
        var folder = Path.Combine(_testRoot, folderName);
        Directory.CreateDirectory(folder);
        var path = Path.Combine(folder, fileName);
        File.WriteAllText(path, content);
        return path;
    }

    private static async Task<long> AddFileRecordAsync(IFileIntakeStore store, string source)
    {
        var info = new FileInfo(source);

        return await store.AddFileRecordAsync(new FileRecord(
            Id: null,
            Sha256: null,
            OriginalFilename: Path.GetFileName(source),
            CurrentFilename: Path.GetFileName(source),
            OriginalPath: source,
            CurrentPath: source,
            Extension: Path.GetExtension(source),
            SizeBytes: info.Length,
            MimeType: null,
            SourceIntakeFolderId: null,
            FirstSeenAt: FixedNow,
            LastSeenAt: FixedNow,
            StableAt: FixedNow,
            Status: "Candidate",
            TriageCategory: "MeaningfulOneOff",
            TriageConfidence: 0.95,
            IsMeaningful: true,
            NotesJson: null));
    }
}
