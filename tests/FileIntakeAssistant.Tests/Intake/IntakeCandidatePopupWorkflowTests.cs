using FileIntakeAssistant.App.ViewModels;
using FileIntakeAssistant.Core.Intake;
using FileIntakeAssistant.Core.Metadata;
using FileIntakeAssistant.Core.Models;
using FileIntakeAssistant.Core.Stability;
using FileIntakeAssistant.Core.Triage;
using FileIntakeAssistant.Core.Transcription;
using FileIntakeAssistant.Infrastructure.FileSystem;
using FileIntakeAssistant.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;

namespace FileIntakeAssistant.Tests.Intake;

public sealed class IntakeCandidatePopupWorkflowTests : IAsyncLifetime
{
    private static readonly DateTimeOffset FixedNow = new(2026, 5, 23, 8, 0, 0, TimeSpan.Zero);

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
    public async Task IntakePopup_CandidateStateShowsFileTriageStabilityAndProviderStatus()
    {
        var source = CreateTempFile("Intake", "Candidate.pdf", "placeholder");
        var store = await CreateStoreAsync();
        var candidate = Candidate(source, sourceIntakeFolderId: 12);

        var viewModel = new IntakeCandidatePopupViewModel(
            candidate,
            CreateWorkflow(store),
            () => FixedNow);

        Assert.Equal("Candidate.pdf", viewModel.FileName);
        Assert.Equal(source, viewModel.Path);
        Assert.Equal(".pdf", viewModel.Extension);
        Assert.Contains("Stable", viewModel.StabilityEvidence, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("No suppressing batch", viewModel.BatchEvidence, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("OpenAI", viewModel.ProviderStatusText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("disabled", viewModel.ProviderStatusText, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("MeaningfulOneOff", viewModel.TriageCategory);
        Assert.False(viewModel.IsComplete);
    }

    [Fact]
    public async Task IntakePopup_SaveMetadataFromCandidateStoresReviewedTranscriptInSQLite()
    {
        var source = CreateTempFile("Intake", "Revenue Report.pdf", "placeholder report");
        var store = await CreateStoreAsync();
        var intakeFolderId = await AddIntakeFolderAsync(store, Path.GetDirectoryName(source)!);
        var viewModel = new IntakeCandidatePopupViewModel(
            Candidate(source, intakeFolderId),
            CreateWorkflow(store),
            () => FixedNow);

        viewModel.UserNote = "Downloaded for transport assumptions.";
        viewModel.TranscriptText = "Reviewed transcript for the intake popup.";
        viewModel.Relevance = "high";
        viewModel.Project = "Revenue Model";
        viewModel.Topic = "Transport assumptions";
        viewModel.Tags = "finance, transport";
        viewModel.SourceUrl = "https://example.test/report";

        await viewModel.SaveAsync();

        Assert.True(viewModel.IsComplete);
        Assert.NotNull(viewModel.MetadataEntryId);
        Assert.NotNull(viewModel.ActionId);
        Assert.NotNull(viewModel.ManualTranscriptionJobId);

        var metadata = await store.GetMetadataEntryAsync(viewModel.MetadataEntryId!.Value);
        var action = await store.GetActionAsync(viewModel.ActionId!.Value);
        var fileRecord = await store.GetFileRecordByCurrentPathAsync(source);
        var transcriptionJob = await store.GetTranscriptionJobAsync(viewModel.ManualTranscriptionJobId!.Value);

        Assert.NotNull(metadata);
        Assert.Equal("Downloaded for transport assumptions.", metadata.UserNote);
        Assert.Equal("Reviewed transcript for the intake popup.", metadata.TranscriptText);
        Assert.Equal("high", metadata.Relevance);
        Assert.Equal("Revenue Model", metadata.Project);
        Assert.Equal("Transport assumptions", metadata.Topic);
        Assert.Equal("https://example.test/report", metadata.SourceUrl);

        Assert.NotNull(fileRecord);
        Assert.Equal(intakeFolderId, fileRecord.SourceIntakeFolderId);
        Assert.Equal("MeaningfulOneOff", fileRecord.TriageCategory);
        Assert.True(fileRecord.IsMeaningful);

        Assert.NotNull(action);
        Assert.Equal("IntakeCandidateMetadataSaved", action.ActionType);
        Assert.Equal("Completed", action.Status);
        Assert.Equal(fileRecord.Id, action.TargetFileRecordId);

        Assert.NotNull(transcriptionJob);
        Assert.Equal("ManualText", transcriptionJob.Provider);
        Assert.Equal("Completed", transcriptionJob.Status);
        Assert.Null(transcriptionJob.AudioPath);
    }

    [Fact]
    public async Task IntakePopup_SkipCreatesAuditActionWithoutMetadataOrFileRecord()
    {
        var source = CreateTempFile("Intake", "Skip Me.pdf", "placeholder report");
        var store = await CreateStoreAsync();
        var viewModel = new IntakeCandidatePopupViewModel(
            Candidate(source, sourceIntakeFolderId: 4),
            CreateWorkflow(store),
            () => FixedNow);

        await viewModel.SkipAsync("Not relevant.");

        Assert.True(viewModel.IsComplete);
        Assert.NotNull(viewModel.ActionId);
        Assert.Null(viewModel.MetadataEntryId);
        Assert.Null(viewModel.ManualTranscriptionJobId);
        Assert.Null(await store.GetFileRecordByCurrentPathAsync(source));

        var action = await store.GetActionAsync(viewModel.ActionId!.Value);
        Assert.NotNull(action);
        Assert.Equal("IntakeCandidateSkipped", action.ActionType);
        Assert.Equal("Completed", action.Status);
        Assert.Null(action.TargetFileRecordId);
        Assert.Contains("Not relevant", action.DetailsJson!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task IntakePopup_NoKeyManualTranscriptFallbackDoesNotRequireProvider()
    {
        var source = CreateTempFile("Intake", "Transcript Only.pdf", "placeholder report");
        var store = await CreateStoreAsync();
        var viewModel = new IntakeCandidatePopupViewModel(
            Candidate(source, sourceIntakeFolderId: null),
            CreateWorkflow(store),
            () => FixedNow);

        viewModel.TranscriptText = "Typed context without a configured provider.";
        viewModel.Relevance = "medium";

        await viewModel.SaveAsync();

        Assert.True(viewModel.IsComplete);
        Assert.NotNull(viewModel.ManualTranscriptionJobId);

        var job = await store.GetTranscriptionJobAsync(viewModel.ManualTranscriptionJobId!.Value);
        var metadata = await store.GetMetadataEntryAsync(viewModel.MetadataEntryId!.Value);

        Assert.NotNull(job);
        Assert.Equal("ManualText", job.Provider);
        Assert.Equal("Completed", job.Status);
        Assert.Equal("Typed context without a configured provider.", job.TranscriptText);
        Assert.Null(job.AudioPath);

        Assert.NotNull(metadata);
        Assert.Equal("Typed context without a configured provider.", metadata.TranscriptText);
        Assert.Contains("disabled", viewModel.ProviderStatusText, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task IntakePopup_SaveDoesNotModifyTargetFileOrCreateSidecar()
    {
        var source = CreateTempFile("Intake", "Unchanged.pdf", "original content");
        var originalContent = await File.ReadAllTextAsync(source);
        var originalLastWriteUtc = File.GetLastWriteTimeUtc(source);
        var store = await CreateStoreAsync();
        var intakeFolderId = await AddIntakeFolderAsync(store, Path.GetDirectoryName(source)!);
        var viewModel = new IntakeCandidatePopupViewModel(
            Candidate(source, intakeFolderId),
            CreateWorkflow(store),
            () => FixedNow);

        viewModel.UserNote = "Save metadata only.";
        viewModel.TranscriptText = "Reviewed transcript.";

        await viewModel.SaveAsync();

        Assert.True(viewModel.IsComplete);
        Assert.True(File.Exists(source));
        Assert.Equal(originalContent, await File.ReadAllTextAsync(source));
        Assert.Equal(originalLastWriteUtc, File.GetLastWriteTimeUtc(source));
        Assert.Equal(new[] { source }, Directory.GetFiles(Path.GetDirectoryName(source)!));
    }

    private async Task<SqliteFileIntakeStore> CreateStoreAsync()
    {
        Assert.StartsWith(
            Path.GetFullPath(Path.GetTempPath()),
            Path.GetFullPath(DatabasePath),
            StringComparison.OrdinalIgnoreCase);

        await new SqliteMigrationRunner().ApplyMigrationsAsync(DatabasePath);
        return new SqliteFileIntakeStore(DatabasePath);
    }

    private IntakeCandidateWorkflowService CreateWorkflow(SqliteFileIntakeStore store)
    {
        var snapshotReader = new LocalManualFileSnapshotReader();
        return new IntakeCandidateWorkflowService(
            snapshotReader,
            new ManualMetadataCaptureService(store),
            new TranscriptionWorkflowService(store),
            store);
    }

    private static async Task<long> AddIntakeFolderAsync(SqliteFileIntakeStore store, string path)
    {
        return await store.AddIntakeFolderAsync(new IntakeFolder(
            Id: null,
            Path: path,
            DisplayName: Path.GetFileName(path),
            Enabled: true,
            FolderType: "Test",
            Recursive: false,
            CreatedAt: FixedNow,
            UpdatedAt: FixedNow));
    }

    private string CreateTempFile(string folderName, string fileName, string content)
    {
        var folder = Path.Combine(_testRoot, folderName);
        Directory.CreateDirectory(folder);
        var path = Path.Combine(folder, fileName);
        File.WriteAllText(path, content);
        return path;
    }

    private static IntakeCandidate Candidate(string path, long? sourceIntakeFolderId)
    {
        var info = new FileInfo(path);
        return new IntakeCandidate(
            Path: path,
            FileName: info.Name,
            Extension: info.Extension,
            SizeBytes: info.Length,
            SourceIntakeFolderId: sourceIntakeFolderId,
            SourceIntakeFolderPath: info.DirectoryName ?? Path.GetPathRoot(path) ?? path,
            ObservedAt: FixedNow.AddSeconds(-5),
            StableAt: FixedNow.AddSeconds(-3),
            TriageCategory: TriageCategory.MeaningfulOneOff,
            TriageReason: "Stable user-level PDF in configured intake folder.",
            TriageConfidence: 0.94,
            HashPlan: HashPlan.ComputeSha256);
    }
}
