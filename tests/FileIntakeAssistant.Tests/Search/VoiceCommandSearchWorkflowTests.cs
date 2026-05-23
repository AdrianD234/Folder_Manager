using FileIntakeAssistant.Core.Models;
using FileIntakeAssistant.Core.Persistence;
using FileIntakeAssistant.Core.Search;

namespace FileIntakeAssistant.Tests.Search;

public sealed class VoiceCommandSearchWorkflowTests
{
    private static readonly DateTimeOffset FixedNow = new(2026, 5, 23, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task VoiceCommand_UnsupportedDestructiveCommandIsLoggedWithoutSearching()
    {
        var provider = new FakeSearchProvider([]);
        var store = new InMemorySearchStore();
        var workflow = new SearchWorkflowService(new SearchIntentParser(), provider, store);

        var result = await workflow.ExecuteAsync("delete the old PDFs", FixedNow);

        Assert.Equal(SearchExecutionOutcome.Unsupported, result.Outcome);
        Assert.False(result.RequiresConfirmation);
        Assert.False(provider.WasCalled);
        Assert.Single(store.VoiceCommands);
        Assert.Empty(store.SearchQueries);
        Assert.Equal("Unsupported", store.VoiceCommands[0].Status);
        Assert.Contains("Destructive", store.VoiceCommands[0].ParsedIntentJson!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task VoiceCommand_OpenMultipleResultsRequiresConfirmationInsteadOfBlindOpening()
    {
        var provider = new FakeSearchProvider(
        [
            new SearchResult(SearchResultTarget.File, 1, "One.xlsx", "C:\\Temp\\One.xlsx", "C:\\Temp", ".xlsx", FixedNow, "high", "Finance", "Budget", ["file type Excel"], 10),
            new SearchResult(SearchResultTarget.File, 2, "Two.xlsx", "C:\\Temp\\Two.xlsx", "C:\\Temp", ".xlsx", FixedNow.AddMinutes(-1), "high", "Finance", "Budget", ["file type Excel"], 9)
        ]);
        var store = new InMemorySearchStore();
        var workflow = new SearchWorkflowService(new SearchIntentParser(), provider, store);

        var result = await workflow.ExecuteAsync("open the last five Excel files I saved", FixedNow);

        Assert.Equal(SearchExecutionOutcome.ShowBulkConfirmation, result.Outcome);
        Assert.True(result.RequiresConfirmation);
        Assert.True(provider.WasCalled);
        Assert.Equal(2, result.Results.Count);
        Assert.Single(store.VoiceCommands);
        Assert.Single(store.SearchQueries);
        Assert.Equal("ShowBulkConfirmation", store.VoiceCommands[0].ExecutedAction);
        Assert.Equal(2, store.SearchQueries[0].ResultCount);
    }

    private sealed class FakeSearchProvider : IFileSearchProvider
    {
        private readonly IReadOnlyList<SearchResult> _results;

        public FakeSearchProvider(IReadOnlyList<SearchResult> results)
        {
            _results = results;
        }

        public string Name => "FakeSearch";

        public bool WasCalled { get; private set; }

        public Task<SearchProviderResult> SearchAsync(
            SearchIntent intent,
            CancellationToken cancellationToken = default)
        {
            WasCalled = true;
            return Task.FromResult(new SearchProviderResult(Name, intent, _results));
        }
    }

    private sealed class InMemorySearchStore : IFileIntakeStore
    {
        public List<VoiceCommandRecord> VoiceCommands { get; } = [];

        public List<SearchQueryRecord> SearchQueries { get; } = [];

        public Task<long> AddVoiceCommandAsync(VoiceCommandRecord voiceCommand, CancellationToken cancellationToken = default)
        {
            var id = VoiceCommands.Count + 1;
            VoiceCommands.Add(voiceCommand with { Id = id });
            return Task.FromResult((long)id);
        }

        public Task<long> AddSearchQueryAsync(SearchQueryRecord searchQuery, CancellationToken cancellationToken = default)
        {
            var id = SearchQueries.Count + 1;
            SearchQueries.Add(searchQuery with { Id = id });
            return Task.FromResult((long)id);
        }

        public Task UpsertAppSettingAsync(AppSetting setting, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<AppSetting?> GetAppSettingAsync(string key, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<long> AddIntakeFolderAsync(IntakeFolder folder, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task UpdateIntakeFolderAsync(IntakeFolder folder, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IntakeFolder?> GetIntakeFolderAsync(long id, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IntakeFolder?> GetIntakeFolderByPathAsync(string path, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<IntakeFolder>> ListIntakeFoldersAsync(bool enabledOnly = false, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<long> AddFileRecordAsync(FileRecord fileRecord, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task UpdateFileRecordAsync(FileRecord fileRecord, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<FileRecord?> GetFileRecordAsync(long id, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<FileRecord?> GetFileRecordByCurrentPathAsync(string currentPath, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<long> AddFileEventAsync(FileEventRecord fileEvent, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<FileEventRecord?> GetFileEventAsync(long id, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<FileEventRecord>> ListFileEventsAsync(int limit = 100, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<long> AddEventBatchAsync(EventBatch batch, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<EventBatch?> GetEventBatchAsync(long id, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<long> AddFolderRecordAsync(FolderRecord folderRecord, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<FolderRecord?> GetFolderRecordAsync(long id, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<long> AddMetadataEntryAsync(MetadataEntry metadataEntry, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task UpdateMetadataEntryAsync(MetadataEntry metadataEntry, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<MetadataEntry?> GetMetadataEntryAsync(long id, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<long> AddActionAsync(FileActionRecord action, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task UpdateActionAsync(FileActionRecord action, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<FileActionRecord?> GetActionAsync(long id, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<long> AddUndoActionAsync(UndoActionRecord undoAction, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task UpdateUndoActionAsync(UndoActionRecord undoAction, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<UndoActionRecord?> GetUndoActionAsync(long id, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<UndoActionRecord>> ListUndoActionsAsync(string? status = null, int limit = 100, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<long> AddTranscriptionJobAsync(TranscriptionJobRecord transcriptionJob, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task UpdateTranscriptionJobAsync(TranscriptionJobRecord transcriptionJob, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<TranscriptionJobRecord?> GetTranscriptionJobAsync(long id, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<VoiceCommandRecord?> GetVoiceCommandAsync(long id, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<SearchQueryRecord?> GetSearchQueryAsync(long id, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
