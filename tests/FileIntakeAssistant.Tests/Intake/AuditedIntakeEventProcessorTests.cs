using FileIntakeAssistant.App.ViewModels;
using FileIntakeAssistant.Core.Batching;
using FileIntakeAssistant.Core.Intake;
using FileIntakeAssistant.Core.Models;
using FileIntakeAssistant.Core.Stability;
using FileIntakeAssistant.Core.Triage;
using FileIntakeAssistant.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;

namespace FileIntakeAssistant.Tests.Intake;

public sealed class AuditedIntakeEventProcessorTests : IAsyncLifetime
{
    private static readonly DateTimeOffset FixedNow = new(2026, 5, 23, 7, 0, 0, TimeSpan.Zero);

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
    public async Task IntakeAudit_IgnoredEventsCreateAuditRowsAndDoNotEnterCandidateQueue()
    {
        var store = await CreateStoreAsync();
        var queue = new InMemoryIntakeCandidateQueue();
        var audited = CreateAuditedProcessor(store, queue);

        var result = await audited.ProcessAndAuditAsync(Request(
            path: @"C:\Intake\node_modules\package\index.js",
            configuredFolders: [IntakeFolder(@"C:\Intake", enabled: true, recursive: true)]));

        Assert.Equal(IntakeProcessingOutcome.Ignored, result.ProcessingResult.Outcome);
        Assert.Equal(0, queue.Count);

        var fileEvent = await store.GetFileEventAsync(result.FileEventId);
        Assert.NotNull(fileEvent);
        Assert.True(fileEvent.Ignored);
        Assert.Equal("PackageInstallNoise", fileEvent.TriageCategory);
        Assert.Contains("outcome", fileEvent.DetailsJson!, StringComparison.OrdinalIgnoreCase);
        Assert.Single(await store.ListFileEventsAsync());
    }

    [Fact]
    public async Task IntakeAudit_MeaningfulCandidateCreatesAuditRowAndAppearsInUiFacingQueue()
    {
        var store = await CreateStoreAsync();
        var queue = new InMemoryIntakeCandidateQueue();
        var audited = CreateAuditedProcessor(store, queue);

        var result = await audited.ProcessAndAuditAsync(Request(
            path: @"C:\Intake\Report.pdf",
            configuredFolders: [IntakeFolder(@"C:\Intake", id: 9, enabled: true)]));

        Assert.Equal(IntakeProcessingOutcome.CandidateQueued, result.ProcessingResult.Outcome);
        Assert.Equal(1, queue.Count);

        var fileEvent = await store.GetFileEventAsync(result.FileEventId);
        Assert.NotNull(fileEvent);
        Assert.False(fileEvent.Ignored);
        Assert.Equal("MeaningfulOneOff", fileEvent.TriageCategory);

        var viewModel = new IntakeCandidateQueueViewModel(queue, store);
        await viewModel.RefreshAsync();

        var candidate = Assert.Single(viewModel.Candidates);
        Assert.Equal("Report.pdf", candidate.FileName);
        Assert.Contains("Report.pdf", viewModel.AuditText, StringComparison.OrdinalIgnoreCase);
    }

    private async Task<SqliteFileIntakeStore> CreateStoreAsync()
    {
        await new SqliteMigrationRunner().ApplyMigrationsAsync(DatabasePath);
        return new SqliteFileIntakeStore(DatabasePath);
    }

    private static AuditedIntakeEventProcessor CreateAuditedProcessor(
        SqliteFileIntakeStore store,
        IIntakeCandidateQueue queue)
    {
        return new AuditedIntakeEventProcessor(
            new IntakeEventProcessor(new FileEventTriageEngine(), queue),
            store,
            () => FixedNow.AddMilliseconds(10));
    }

    private static IntakeProcessingRequest Request(
        string path,
        IReadOnlyList<IntakeFolder> configuredFolders,
        FileEventKind eventKind = FileEventKind.Created,
        long sizeBytes = 1_024)
    {
        return new IntakeProcessingRequest(
            Path: path,
            EventKind: eventKind,
            IsDirectory: false,
            SizeBytes: sizeBytes,
            ConfiguredFolders: configuredFolders,
            StabilityDecision: Stable(),
            BatchDecision: Batch(@"C:\Intake"),
            ObservedAt: FixedNow);
    }

    private static FileStabilityDecision Stable()
    {
        return new FileStabilityDecision(
            FileStabilityStatus.Stable,
            IsStable: true,
            Reason: "Stable test file.",
            RequiredDebounceWindow: TimeSpan.FromSeconds(2),
            StableSince: FixedNow.AddSeconds(-3),
            HashPlan: HashPlan.ComputeSha256);
    }

    private static BatchDetectionResult Batch(string rootPath)
    {
        return new BatchDetectionResult(
            RootPath: rootPath,
            BatchType: EventBatchType.None,
            Decision: BatchPromptDecision.NoBatch,
            FileCount: 1,
            StartedAt: FixedNow,
            EndedAt: FixedNow,
            SuppressIndividualPrompts: false,
            Reason: "No batch in test.");
    }

    private static IntakeFolder IntakeFolder(
        string path,
        long? id = null,
        bool enabled = true,
        bool recursive = false)
    {
        return new IntakeFolder(
            Id: id,
            Path: path,
            DisplayName: Path.GetFileName(path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)),
            Enabled: enabled,
            FolderType: "Test",
            Recursive: recursive,
            CreatedAt: FixedNow,
            UpdatedAt: FixedNow);
    }
}
