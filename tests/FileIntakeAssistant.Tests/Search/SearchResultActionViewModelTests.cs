using FileIntakeAssistant.App.ViewModels;
using FileIntakeAssistant.Core.Models;
using FileIntakeAssistant.Core.Persistence;
using FileIntakeAssistant.Core.Search;
using FileIntakeAssistant.Infrastructure.Persistence;
using FileIntakeAssistant.Infrastructure.Search;
using Microsoft.Data.Sqlite;

namespace FileIntakeAssistant.Tests.Search;

public sealed class SearchResultActionViewModelTests : IAsyncLifetime
{
    private static readonly DateTimeOffset FixedNow = new(2026, 5, 23, 12, 0, 0, TimeSpan.Zero);

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
    public async Task Search_OpenSingleSelectedFileRequiresConfirmationAndLogsAction()
    {
        var store = await CreateStoreAsync();
        var launcher = new FakeFileLaunchService();
        var viewModel = CreateViewModel(store, launcher, userConfirms: true);
        var fileId = await AddExcelFileAsync(store, "Budget.xlsx", FixedNow);
        await AddMetadataAsync(store, fileId, relevance: "high", project: "Finance", topic: "Budget");

        viewModel.CommandText = "open the last five Excel files I saved";
        await viewModel.RunAsync();
        await viewModel.OpenSelectedFileAsync();

        Assert.True(viewModel.RequiresActionConfirmation);
        Assert.Single(viewModel.Results);
        Assert.Single(launcher.OpenedFiles);
        Assert.EndsWith("Budget.xlsx", launcher.OpenedFiles[0], StringComparison.OrdinalIgnoreCase);

        var action = await store.GetActionAsync(viewModel.LastActionId!.Value);
        Assert.Equal("OpenFileConfirmed", action!.ActionType);
        Assert.Equal("Completed", action.Status);
        Assert.Equal(fileId, action.TargetFileRecordId);
    }

    [Fact]
    public async Task Search_BulkOpenRefusalDoesNotLaunchAndLogsCancellation()
    {
        var store = await CreateStoreAsync();
        var launcher = new FakeFileLaunchService();
        var viewModel = CreateViewModel(store, launcher, userConfirms: false);
        var first = await AddExcelFileAsync(store, "Budget 1.xlsx", FixedNow);
        var second = await AddExcelFileAsync(store, "Budget 2.xlsx", FixedNow.AddMinutes(-1));
        await AddMetadataAsync(store, first, relevance: "high", project: "Finance", topic: "Budget");
        await AddMetadataAsync(store, second, relevance: "medium", project: "Finance", topic: "Budget");

        viewModel.CommandText = "open the last five Excel files I saved";
        await viewModel.RunAsync();
        await viewModel.OpenAllAsync();

        Assert.True(viewModel.HasAmbiguousOpenResults);
        Assert.Empty(launcher.OpenedFiles);
        Assert.NotNull(viewModel.LastActionId);

        var action = await store.GetActionAsync(viewModel.LastActionId!.Value);
        Assert.Equal("OpenFileMultipleCancelled", action!.ActionType);
        Assert.Equal("Cancelled", action.Status);
    }

    [Fact]
    public async Task Search_AmbiguousOpenCommandDisplaysResultsWithoutBlindOpening()
    {
        var store = await CreateStoreAsync();
        var launcher = new FakeFileLaunchService();
        var viewModel = CreateViewModel(store, launcher, userConfirms: true);
        var first = await AddExcelFileAsync(store, "Budget 1.xlsx", FixedNow);
        var second = await AddExcelFileAsync(store, "Budget 2.xlsx", FixedNow.AddMinutes(-1));
        await AddMetadataAsync(store, first, relevance: "high", project: "Finance", topic: "Budget");
        await AddMetadataAsync(store, second, relevance: "medium", project: "Finance", topic: "Budget");

        viewModel.CommandText = "open the last five Excel files I saved";
        await viewModel.RunAsync();

        Assert.True(viewModel.RequiresActionConfirmation);
        Assert.True(viewModel.HasAmbiguousOpenResults);
        Assert.Equal(2, viewModel.Results.Count);
        Assert.Empty(launcher.OpenedFiles);
        Assert.Contains("Confirmation required", viewModel.StatusMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Search_OpenContainingFolderUsesFolderLauncherAfterConfirmation()
    {
        var store = await CreateStoreAsync();
        var launcher = new FakeFileLaunchService();
        var viewModel = CreateViewModel(store, launcher, userConfirms: true);
        var fileId = await AddExcelFileAsync(store, "Budget.xlsx", FixedNow);
        await AddMetadataAsync(store, fileId, relevance: "high", project: "Finance", topic: "Budget");

        viewModel.CommandText = "open the last five Excel files I saved";
        await viewModel.RunAsync();
        await viewModel.OpenContainingFolderAsync();

        Assert.Empty(launcher.OpenedFiles);
        Assert.Single(launcher.OpenedFolders);
        Assert.Equal(Path.Combine(_testRoot, "downloads"), launcher.OpenedFolders[0]);

        var action = await store.GetActionAsync(viewModel.LastActionId!.Value);
        Assert.Equal("OpenContainingFolderConfirmed", action!.ActionType);
        Assert.Equal("Completed", action.Status);
    }

    private async Task<SqliteFileIntakeStore> CreateStoreAsync()
    {
        Assert.StartsWith(
            Path.GetFullPath(Path.GetTempPath()),
            Path.GetFullPath(DatabasePath),
            StringComparison.OrdinalIgnoreCase);

        var migrationRunner = new SqliteMigrationRunner();
        await migrationRunner.ApplyMigrationsAsync(DatabasePath);
        return new SqliteFileIntakeStore(DatabasePath);
    }

    private SearchCommandViewModel CreateViewModel(
        IFileIntakeStore store,
        FakeFileLaunchService launcher,
        bool userConfirms)
    {
        return new SearchCommandViewModel(
            new SearchWorkflowService(
                new SearchIntentParser(),
                new SqliteSearchProvider(DatabasePath),
                store),
            launcher,
            new FakeConfirmationService(userConfirms),
            store,
            () => FixedNow);
    }

    private async Task<long> AddExcelFileAsync(
        IFileIntakeStore store,
        string fileName,
        DateTimeOffset seenAt)
    {
        return await store.AddFileRecordAsync(new FileRecord(
            Id: null,
            Sha256: null,
            OriginalFilename: fileName,
            CurrentFilename: fileName,
            OriginalPath: Path.Combine(_testRoot, "downloads", fileName),
            CurrentPath: Path.Combine(_testRoot, "downloads", fileName),
            Extension: ".xlsx",
            SizeBytes: 1024,
            MimeType: null,
            SourceIntakeFolderId: null,
            FirstSeenAt: seenAt,
            LastSeenAt: seenAt,
            StableAt: seenAt,
            Status: "Candidate",
            TriageCategory: "MeaningfulOneOff",
            TriageConfidence: 0.95,
            IsMeaningful: true,
            NotesJson: null));
    }

    private static async Task AddMetadataAsync(
        IFileIntakeStore store,
        long fileRecordId,
        string relevance,
        string project,
        string topic)
    {
        await store.AddMetadataEntryAsync(new MetadataEntry(
            Id: null,
            FileRecordId: fileRecordId,
            FolderRecordId: null,
            UserNote: $"Metadata for {project} {topic}.",
            TranscriptText: $"Transcript for {project} {topic}.",
            Relevance: relevance,
            Project: project,
            Topic: topic,
            TagsJson: $"""["{project.ToLowerInvariant()}","{topic.ToLowerInvariant()}"]""",
            SourceUrl: null,
            ReferrerUrl: null,
            AgentSummary: $"{project} {topic} file.",
            ClassifierConfidence: 0.9,
            CreatedAt: FixedNow,
            UpdatedAt: FixedNow));
    }

    private sealed class FakeConfirmationService : IUserConfirmationService
    {
        private readonly bool _confirmed;

        public FakeConfirmationService(bool confirmed)
        {
            _confirmed = confirmed;
        }

        public Task<bool> ConfirmAsync(
            string title,
            string message,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_confirmed);
        }
    }

    private sealed class FakeFileLaunchService : IFileLaunchService
    {
        public List<string> OpenedFiles { get; } = [];

        public List<string> OpenedFolders { get; } = [];

        public Task<FileLaunchResult> OpenFileAsync(
            string path,
            CancellationToken cancellationToken = default)
        {
            OpenedFiles.Add(path);
            return Task.FromResult(new FileLaunchResult(true, null));
        }

        public Task<FileLaunchResult> OpenFolderAsync(
            string path,
            CancellationToken cancellationToken = default)
        {
            OpenedFolders.Add(path);
            return Task.FromResult(new FileLaunchResult(true, null));
        }
    }
}
