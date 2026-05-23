using FileIntakeAssistant.Core.Models;
using FileIntakeAssistant.Core.Persistence;
using FileIntakeAssistant.Core.Search;
using FileIntakeAssistant.Infrastructure.Persistence;
using FileIntakeAssistant.Infrastructure.Search;
using Microsoft.Data.Sqlite;

namespace FileIntakeAssistant.Tests.Search;

public sealed class EverythingCliSearchProviderTests : IAsyncLifetime
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
    public async Task Everything_ProviderDisabledDoesNotRunProcess()
    {
        var runner = new FakeEverythingCliProcessRunner(string.Empty);
        var provider = new EverythingCliSearchProvider(
            new EverythingCliSearchProviderOptions
            {
                Enabled = false,
                ExecutablePath = Path.Combine(_testRoot, "es.exe")
            },
            runner,
            new FixedEverythingCliPathResolver(Path.Combine(_testRoot, "es.exe")));
        var intent = new SearchIntentParser().Parse("find files about capex", FixedNow);

        var result = await provider.SearchAsync(intent);

        Assert.Equal("EverythingCLI", result.Provider);
        Assert.Empty(result.Results);
        Assert.False(runner.WasCalled);
    }

    [Fact]
    public async Task Everything_MissingExecutableDoesNotRunProcess()
    {
        var runner = new FakeEverythingCliProcessRunner(string.Empty);
        var provider = new EverythingCliSearchProvider(
            new EverythingCliSearchProviderOptions
            {
                Enabled = true,
                AllowedRoots = [Path.Combine(_testRoot, "downloads")]
            },
            runner,
            new FixedEverythingCliPathResolver(null));
        var intent = new SearchIntentParser().Parse("find files about capex", FixedNow);

        var result = await provider.SearchAsync(intent);

        Assert.Empty(result.Results);
        Assert.False(runner.WasCalled);
    }

    [Fact]
    public async Task Everything_EnabledWithoutAllowedRootsDoesNotRunProcess()
    {
        var runner = new FakeEverythingCliProcessRunner(Path.Combine(_testRoot, "downloads", "Nvidia Capex.xlsx"));
        var provider = new EverythingCliSearchProvider(
            new EverythingCliSearchProviderOptions { Enabled = true },
            runner,
            new FixedEverythingCliPathResolver(Path.Combine(_testRoot, "es.exe")));
        var intent = new SearchIntentParser().Parse("find Nvidia capex", FixedNow);

        var result = await provider.SearchAsync(intent);

        Assert.Empty(result.Results);
        Assert.False(runner.WasCalled);
    }

    [Fact]
    public async Task Everything_FakeProcessOutputParsesAndFiltersResults()
    {
        var allowedRoot = Path.Combine(_testRoot, "downloads");
        var outsideRoot = Path.Combine(_testRoot, "outside");
        var wantedPath = Path.Combine(allowedRoot, "Nvidia Capex.xlsx");
        var outsidePath = Path.Combine(outsideRoot, "Nvidia Capex.xlsx");
        var wrongTypePath = Path.Combine(allowedRoot, "Nvidia Capex.txt");
        var output = string.Join(Environment.NewLine, wantedPath, outsidePath, wrongTypePath);
        var runner = new FakeEverythingCliProcessRunner(output);
        var provider = new EverythingCliSearchProvider(
            new EverythingCliSearchProviderOptions
            {
                Enabled = true,
                AllowedRoots = [allowedRoot]
            },
            runner,
            new FixedEverythingCliPathResolver(Path.Combine(_testRoot, "es.exe")));
        var intent = new SearchIntentParser().Parse("find Nvidia capex Excel files", FixedNow);

        var result = await provider.SearchAsync(intent);

        Assert.True(runner.WasCalled);
        Assert.Contains("-full-path-and-name", runner.LastArguments);
        Assert.Contains(runner.LastArguments, argument => argument.Contains("ext:xlsx", StringComparison.OrdinalIgnoreCase));
        var searchResult = Assert.Single(result.Results);
        Assert.Equal(SearchResultTarget.File, searchResult.Target);
        Assert.Equal(wantedPath, searchResult.Path);
        Assert.Equal(0, searchResult.RecordId);
        Assert.Contains("Everything CLI path match", searchResult.MatchedReasons);
        Assert.Contains("keyword nvidia", searchResult.MatchedReasons);
        Assert.Contains("keyword capex", searchResult.MatchedReasons);
    }

    [Fact]
    public async Task Everything_CompositeProviderMergesPathHitsWithSQLiteMetadata()
    {
        var store = await CreateStoreAsync();
        var intakeFolderId = await AddIntakeFolderAsync(store);
        var filePath = Path.Combine(_testRoot, "downloads", "Finance Report.pdf");
        var fileId = await AddFileAsync(store, intakeFolderId, filePath, "Finance Report.pdf", ".pdf", FixedNow.AddDays(-2));
        await AddMetadataAsync(store, fileId, relevance: "high", project: "Finance", topic: "Capex");

        var runner = new FakeEverythingCliProcessRunner(filePath);
        var everythingProvider = new EverythingCliSearchProvider(
            new EverythingCliSearchProviderOptions
            {
                Enabled = true,
                AllowedRoots = [Path.Combine(_testRoot, "downloads")]
            },
            runner,
            new FixedEverythingCliPathResolver(Path.Combine(_testRoot, "es.exe")));
        var compositeProvider = new CompositeFileSearchProvider(
            new SqliteSearchProvider(DatabasePath),
            everythingProvider);
        var intent = new SearchIntentParser().Parse("find finance report", FixedNow);

        var result = await compositeProvider.SearchAsync(intent);

        var searchResult = Assert.Single(result.Results);
        Assert.Equal("SQLite+EverythingCLI", result.Provider);
        Assert.Equal(fileId, searchResult.RecordId);
        Assert.Equal("high", searchResult.Relevance);
        Assert.Equal("Finance", searchResult.Project);
        Assert.Contains("Everything CLI path match", searchResult.MatchedReasons);
        Assert.True(runner.WasCalled);
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
        string filePath,
        string fileName,
        string extension,
        DateTimeOffset seenAt)
    {
        return await store.AddFileRecordAsync(new FileRecord(
            Id: null,
            Sha256: null,
            OriginalFilename: fileName,
            CurrentFilename: fileName,
            OriginalPath: filePath,
            CurrentPath: filePath,
            Extension: extension,
            SizeBytes: 2048,
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

    private sealed class FakeEverythingCliProcessRunner : IEverythingCliProcessRunner
    {
        private readonly string _output;

        public FakeEverythingCliProcessRunner(string output)
        {
            _output = output;
        }

        public bool WasCalled { get; private set; }

        public IReadOnlyList<string> LastArguments { get; private set; } = [];

        public Task<EverythingCliProcessResult> RunAsync(
            string executablePath,
            IReadOnlyList<string> arguments,
            TimeSpan timeout,
            CancellationToken cancellationToken = default)
        {
            WasCalled = true;
            LastArguments = arguments;
            return Task.FromResult(new EverythingCliProcessResult(0, _output, string.Empty));
        }
    }

    private sealed class FixedEverythingCliPathResolver : IEverythingCliPathResolver
    {
        private readonly string? _path;

        public FixedEverythingCliPathResolver(string? path)
        {
            _path = path;
        }

        public string? Resolve(EverythingCliSearchProviderOptions options)
        {
            return _path;
        }
    }
}
