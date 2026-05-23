using FileIntakeAssistant.Core.Models;
using FileIntakeAssistant.Core.Persistence;
using FileIntakeAssistant.Core.Search;
using FileIntakeAssistant.Infrastructure.Persistence;
using FileIntakeAssistant.Infrastructure.Search;
using Microsoft.Data.Sqlite;

namespace FileIntakeAssistant.Tests.Search;

public sealed class SqliteSearchProviderTests : IAsyncLifetime
{
    private static readonly DateTimeOffset FixedNow = new(2026, 5, 23, 12, 0, 0, TimeSpan.FromHours(12));

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
    public async Task Search_SQLiteSearchReturnsLastFiveExcelFilesAndWorkflowRequiresBulkConfirmation()
    {
        var store = await CreateStoreAsync();
        var intakeFolderId = await AddIntakeFolderAsync(store);
        for (var i = 0; i < 6; i++)
        {
            var seenAt = FixedNow.AddDays(-i);
            var fileId = await AddFileAsync(
                store,
                intakeFolderId,
                $"Budget {i}.xlsx",
                ".xlsx",
                seenAt);
            await AddMetadataAsync(store, fileId, relevance: "medium", project: "Finance", topic: "Budget");
        }

        var workflow = CreateWorkflow(store);
        var result = await workflow.ExecuteAsync("open the last five Excel files I saved", FixedNow);

        Assert.Equal(SearchExecutionOutcome.ShowBulkConfirmation, result.Outcome);
        Assert.True(result.RequiresConfirmation);
        Assert.Equal(5, result.Results.Count);
        Assert.Equal("Budget 0.xlsx", result.Results[0].DisplayName);
        Assert.Equal("Budget 4.xlsx", result.Results[^1].DisplayName);

        var voiceCommand = await store.GetVoiceCommandAsync(result.VoiceCommandId!.Value);
        var searchQuery = await store.GetSearchQueryAsync(result.SearchQueryId!.Value);
        Assert.NotNull(voiceCommand);
        Assert.Equal("ShowBulkConfirmation", voiceCommand.ExecutedAction);
        Assert.Equal(5, voiceCommand.ResultCount);
        Assert.NotNull(searchQuery);
        Assert.Equal("SQLite", searchQuery.Provider);
        Assert.Equal(5, searchQuery.ResultCount);
    }

    [Fact]
    public async Task Search_SQLiteSearchFiltersHighRelevancePdfsFromLastWeek()
    {
        var store = await CreateStoreAsync();
        var intakeFolderId = await AddIntakeFolderAsync(store);
        var wanted = await AddFileAsync(
            store,
            intakeFolderId,
            "Board Report.pdf",
            ".pdf",
            new DateTimeOffset(2026, 5, 13, 8, 0, 0, TimeSpan.FromHours(12)));
        await AddMetadataAsync(store, wanted, relevance: "high", project: "Governance", topic: "Board");

        var low = await AddFileAsync(
            store,
            intakeFolderId,
            "Low Report.pdf",
            ".pdf",
            new DateTimeOffset(2026, 5, 14, 8, 0, 0, TimeSpan.FromHours(12)));
        await AddMetadataAsync(store, low, relevance: "low", project: "Governance", topic: "Board");

        var today = await AddFileAsync(store, intakeFolderId, "Today Report.pdf", ".pdf", FixedNow);
        await AddMetadataAsync(store, today, relevance: "high", project: "Governance", topic: "Board");

        var intent = new SearchIntentParser().Parse("show high relevance PDFs from last week", FixedNow);
        var provider = new SqliteSearchProvider(DatabasePath);

        var result = await provider.SearchAsync(intent);

        Assert.Single(result.Results);
        Assert.Equal("Board Report.pdf", result.Results[0].DisplayName);
        Assert.Contains("high relevance", result.Results[0].MatchedReasons);
        Assert.Contains("last week", result.Results[0].MatchedReasons);
    }

    [Fact]
    public async Task Search_SQLiteSearchFindsDownloadedFinanceReportFromTwoWeeksAgo()
    {
        var store = await CreateStoreAsync();
        var intakeFolderId = await AddIntakeFolderAsync(store);
        var fileId = await AddFileAsync(
            store,
            intakeFolderId,
            "Finance Report.pdf",
            ".pdf",
            new DateTimeOffset(2026, 5, 5, 9, 0, 0, TimeSpan.FromHours(12)));
        await AddMetadataAsync(
            store,
            fileId,
            relevance: "high",
            project: "Finance",
            topic: "Capex",
            note: "Downloaded finance report for the board pack.");

        var intent = new SearchIntentParser().Parse(
            "find the finance report I downloaded two weeks ago",
            FixedNow);
        var provider = new SqliteSearchProvider(DatabasePath);

        var result = await provider.SearchAsync(intent);

        Assert.Single(result.Results);
        Assert.Equal("Finance Report.pdf", result.Results[0].DisplayName);
        Assert.Contains("downloaded intake source", result.Results[0].MatchedReasons);
        Assert.Contains("keyword finance", result.Results[0].MatchedReasons);
        Assert.Contains("keyword report", result.Results[0].MatchedReasons);
    }

    [Fact]
    public async Task Search_SQLiteSearchReturnsFolderForAiInfrastructureReports()
    {
        var store = await CreateStoreAsync();
        var intakeFolderId = await AddIntakeFolderAsync(store);
        var folderId = await store.AddFolderRecordAsync(new FolderRecord(
            Id: null,
            Path: Path.Combine(_testRoot, "filed", "AI Infrastructure Reports"),
            DisplayName: "AI Infrastructure Reports",
            FolderType: "Project",
            SourceIntakeFolderId: intakeFolderId,
            CreatedAt: FixedNow.AddDays(-12),
            UpdatedAt: FixedNow.AddDays(-1),
            NotesJson: """{"context":"AI infrastructure report folder"}"""));
        await store.AddMetadataEntryAsync(new MetadataEntry(
            Id: null,
            FileRecordId: null,
            FolderRecordId: folderId,
            UserNote: "Folder-level context for AI infrastructure reports.",
            TranscriptText: null,
            Relevance: "high",
            Project: "AI Infrastructure",
            Topic: "Reports",
            TagsJson: """["ai","infrastructure","reports"]""",
            SourceUrl: null,
            ReferrerUrl: null,
            AgentSummary: "Folder context.",
            ClassifierConfidence: 1.0,
            CreatedAt: FixedNow,
            UpdatedAt: FixedNow));

        var workflow = CreateWorkflow(store);
        var result = await workflow.ExecuteAsync("open the folder for my AI infrastructure reports", FixedNow);

        Assert.Equal(SearchExecutionOutcome.ShowSingleConfirmation, result.Outcome);
        Assert.True(result.RequiresConfirmation);
        Assert.Single(result.Results);
        Assert.Equal(SearchResultTarget.Folder, result.Results[0].Target);
        Assert.Equal("AI Infrastructure Reports", result.Results[0].DisplayName);
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

    private SearchWorkflowService CreateWorkflow(IFileIntakeStore store)
    {
        return new SearchWorkflowService(
            new SearchIntentParser(),
            new SqliteSearchProvider(DatabasePath),
            store);
    }

    private async Task<long> AddIntakeFolderAsync(IFileIntakeStore store)
    {
        return await store.AddIntakeFolderAsync(new IntakeFolder(
            Id: null,
            Path: Path.Combine(_testRoot, "downloads"),
            DisplayName: "Temp Downloads",
            Enabled: true,
            FolderType: "Downloads",
            Recursive: false,
            CreatedAt: FixedNow,
            UpdatedAt: FixedNow));
    }

    private async Task<long> AddFileAsync(
        IFileIntakeStore store,
        long intakeFolderId,
        string fileName,
        string extension,
        DateTimeOffset seenAt)
    {
        return await store.AddFileRecordAsync(new FileRecord(
            Id: null,
            Sha256: null,
            OriginalFilename: fileName,
            CurrentFilename: fileName,
            OriginalPath: Path.Combine(_testRoot, "downloads", fileName),
            CurrentPath: Path.Combine(_testRoot, "downloads", fileName),
            Extension: extension,
            SizeBytes: 1024,
            MimeType: null,
            SourceIntakeFolderId: intakeFolderId,
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
        string topic,
        string? note = null)
    {
        await store.AddMetadataEntryAsync(new MetadataEntry(
            Id: null,
            FileRecordId: fileRecordId,
            FolderRecordId: null,
            UserNote: note ?? $"Metadata for {project} {topic}.",
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
}
