using FileIntakeAssistant.Core.Models;

namespace FileIntakeAssistant.Core.Persistence;

public interface IFileIntakeStore
{
    Task UpsertAppSettingAsync(AppSetting setting, CancellationToken cancellationToken = default);

    Task<AppSetting?> GetAppSettingAsync(string key, CancellationToken cancellationToken = default);

    Task<long> AddIntakeFolderAsync(IntakeFolder folder, CancellationToken cancellationToken = default);

    Task UpdateIntakeFolderAsync(IntakeFolder folder, CancellationToken cancellationToken = default);

    Task<IntakeFolder?> GetIntakeFolderAsync(long id, CancellationToken cancellationToken = default);

    Task<IntakeFolder?> GetIntakeFolderByPathAsync(string path, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<IntakeFolder>> ListIntakeFoldersAsync(
        bool enabledOnly = false,
        CancellationToken cancellationToken = default);

    Task<long> AddFileRecordAsync(FileRecord fileRecord, CancellationToken cancellationToken = default);

    Task UpdateFileRecordAsync(FileRecord fileRecord, CancellationToken cancellationToken = default);

    Task<FileRecord?> GetFileRecordAsync(long id, CancellationToken cancellationToken = default);

    Task<FileRecord?> GetFileRecordByCurrentPathAsync(string currentPath, CancellationToken cancellationToken = default);

    Task<long> AddFileEventAsync(FileEventRecord fileEvent, CancellationToken cancellationToken = default);

    Task<FileEventRecord?> GetFileEventAsync(long id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<FileEventRecord>> ListFileEventsAsync(
        int limit = 100,
        CancellationToken cancellationToken = default);

    Task<long> AddEventBatchAsync(EventBatch batch, CancellationToken cancellationToken = default);

    Task<EventBatch?> GetEventBatchAsync(long id, CancellationToken cancellationToken = default);

    Task<long> AddFolderRecordAsync(FolderRecord folderRecord, CancellationToken cancellationToken = default);

    Task<FolderRecord?> GetFolderRecordAsync(long id, CancellationToken cancellationToken = default);

    Task<long> AddMetadataEntryAsync(MetadataEntry metadataEntry, CancellationToken cancellationToken = default);

    Task UpdateMetadataEntryAsync(MetadataEntry metadataEntry, CancellationToken cancellationToken = default);

    Task<MetadataEntry?> GetMetadataEntryAsync(long id, CancellationToken cancellationToken = default);

    Task<long> AddActionAsync(FileActionRecord action, CancellationToken cancellationToken = default);

    Task UpdateActionAsync(FileActionRecord action, CancellationToken cancellationToken = default);

    Task<FileActionRecord?> GetActionAsync(long id, CancellationToken cancellationToken = default);

    Task<long> AddUndoActionAsync(UndoActionRecord undoAction, CancellationToken cancellationToken = default);

    Task UpdateUndoActionAsync(UndoActionRecord undoAction, CancellationToken cancellationToken = default);

    Task<UndoActionRecord?> GetUndoActionAsync(long id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<UndoActionRecord>> ListUndoActionsAsync(
        string? status = null,
        int limit = 100,
        CancellationToken cancellationToken = default);

    Task<long> AddTranscriptionJobAsync(TranscriptionJobRecord transcriptionJob, CancellationToken cancellationToken = default);

    Task UpdateTranscriptionJobAsync(TranscriptionJobRecord transcriptionJob, CancellationToken cancellationToken = default);

    Task<TranscriptionJobRecord?> GetTranscriptionJobAsync(long id, CancellationToken cancellationToken = default);

    Task<long> AddVoiceCommandAsync(VoiceCommandRecord voiceCommand, CancellationToken cancellationToken = default);

    Task<VoiceCommandRecord?> GetVoiceCommandAsync(long id, CancellationToken cancellationToken = default);

    Task<long> AddSearchQueryAsync(SearchQueryRecord searchQuery, CancellationToken cancellationToken = default);

    Task<SearchQueryRecord?> GetSearchQueryAsync(long id, CancellationToken cancellationToken = default);
}
