using FileIntakeAssistant.Core.Models;
using FileIntakeAssistant.Core.Persistence;

namespace FileIntakeAssistant.Infrastructure.Logging;

public sealed class StructuredLoggingFileIntakeStore : IFileIntakeStore
{
    private readonly IFileIntakeStore _inner;
    private readonly ILocalAuditLog _auditLog;

    public StructuredLoggingFileIntakeStore(IFileIntakeStore inner, ILocalAuditLog auditLog)
    {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        _auditLog = auditLog ?? throw new ArgumentNullException(nameof(auditLog));
    }

    public async Task UpsertAppSettingAsync(AppSetting setting, CancellationToken cancellationToken = default)
    {
        await _inner.UpsertAppSettingAsync(setting, cancellationToken).ConfigureAwait(false);
        await WriteAsync(
            "app_setting.upserted",
            "Completed",
            new Dictionary<string, object?>
            {
                ["key"] = setting.Key,
                ["valueJsonLength"] = setting.ValueJson.Length,
                ["updatedAt"] = setting.UpdatedAt
            },
            cancellationToken).ConfigureAwait(false);
    }

    public Task<AppSetting?> GetAppSettingAsync(string key, CancellationToken cancellationToken = default)
    {
        return _inner.GetAppSettingAsync(key, cancellationToken);
    }

    public async Task<long> AddIntakeFolderAsync(IntakeFolder folder, CancellationToken cancellationToken = default)
    {
        var id = await _inner.AddIntakeFolderAsync(folder, cancellationToken).ConfigureAwait(false);
        await WriteAsync(
            "intake_folder.added",
            "Completed",
            IntakeFolderFields(folder, id),
            cancellationToken).ConfigureAwait(false);
        return id;
    }

    public async Task UpdateIntakeFolderAsync(IntakeFolder folder, CancellationToken cancellationToken = default)
    {
        await _inner.UpdateIntakeFolderAsync(folder, cancellationToken).ConfigureAwait(false);
        await WriteAsync(
            "intake_folder.updated",
            "Completed",
            IntakeFolderFields(folder, folder.Id),
            cancellationToken).ConfigureAwait(false);
    }

    public Task<IntakeFolder?> GetIntakeFolderAsync(long id, CancellationToken cancellationToken = default)
    {
        return _inner.GetIntakeFolderAsync(id, cancellationToken);
    }

    public Task<IntakeFolder?> GetIntakeFolderByPathAsync(string path, CancellationToken cancellationToken = default)
    {
        return _inner.GetIntakeFolderByPathAsync(path, cancellationToken);
    }

    public Task<IReadOnlyList<IntakeFolder>> ListIntakeFoldersAsync(
        bool enabledOnly = false,
        CancellationToken cancellationToken = default)
    {
        return _inner.ListIntakeFoldersAsync(enabledOnly, cancellationToken);
    }

    public async Task<long> AddFileRecordAsync(FileRecord fileRecord, CancellationToken cancellationToken = default)
    {
        var id = await _inner.AddFileRecordAsync(fileRecord, cancellationToken).ConfigureAwait(false);
        await WriteAsync(
            "file_record.added",
            fileRecord.Status,
            FileRecordFields(fileRecord, id),
            cancellationToken).ConfigureAwait(false);
        return id;
    }

    public async Task UpdateFileRecordAsync(FileRecord fileRecord, CancellationToken cancellationToken = default)
    {
        await _inner.UpdateFileRecordAsync(fileRecord, cancellationToken).ConfigureAwait(false);
        await WriteAsync(
            "file_record.updated",
            fileRecord.Status,
            FileRecordFields(fileRecord, fileRecord.Id),
            cancellationToken).ConfigureAwait(false);
    }

    public Task<FileRecord?> GetFileRecordAsync(long id, CancellationToken cancellationToken = default)
    {
        return _inner.GetFileRecordAsync(id, cancellationToken);
    }

    public Task<FileRecord?> GetFileRecordByCurrentPathAsync(string currentPath, CancellationToken cancellationToken = default)
    {
        return _inner.GetFileRecordByCurrentPathAsync(currentPath, cancellationToken);
    }

    public async Task<long> AddFileEventAsync(FileEventRecord fileEvent, CancellationToken cancellationToken = default)
    {
        var id = await _inner.AddFileEventAsync(fileEvent, cancellationToken).ConfigureAwait(false);
        await WriteAsync(
            "file_event.added",
            fileEvent.Ignored ? "Ignored" : "Observed",
            new Dictionary<string, object?>
            {
                ["fileEventId"] = id,
                ["fileRecordId"] = fileEvent.FileRecordId,
                ["eventType"] = fileEvent.EventType,
                ["rawPath"] = fileEvent.RawPath,
                ["oldPath"] = fileEvent.OldPath,
                ["newPath"] = fileEvent.NewPath,
                ["observedAt"] = fileEvent.ObservedAt,
                ["normalizedAt"] = fileEvent.NormalizedAt,
                ["triageCategory"] = fileEvent.TriageCategory,
                ["triageReason"] = fileEvent.TriageReason,
                ["batchId"] = fileEvent.BatchId,
                ["ignored"] = fileEvent.Ignored,
                ["detailsJsonLength"] = fileEvent.DetailsJson?.Length
            },
            cancellationToken).ConfigureAwait(false);
        return id;
    }

    public Task<FileEventRecord?> GetFileEventAsync(long id, CancellationToken cancellationToken = default)
    {
        return _inner.GetFileEventAsync(id, cancellationToken);
    }

    public Task<IReadOnlyList<FileEventRecord>> ListFileEventsAsync(
        int limit = 100,
        CancellationToken cancellationToken = default)
    {
        return _inner.ListFileEventsAsync(limit, cancellationToken);
    }

    public async Task<long> AddEventBatchAsync(EventBatch batch, CancellationToken cancellationToken = default)
    {
        var id = await _inner.AddEventBatchAsync(batch, cancellationToken).ConfigureAwait(false);
        await WriteAsync(
            "event_batch.added",
            batch.Decision,
            new Dictionary<string, object?>
            {
                ["eventBatchId"] = id,
                ["rootPath"] = batch.RootPath,
                ["batchType"] = batch.BatchType,
                ["startedAt"] = batch.StartedAt,
                ["endedAt"] = batch.EndedAt,
                ["fileCount"] = batch.FileCount,
                ["detailsJsonLength"] = batch.DetailsJson?.Length
            },
            cancellationToken).ConfigureAwait(false);
        return id;
    }

    public Task<EventBatch?> GetEventBatchAsync(long id, CancellationToken cancellationToken = default)
    {
        return _inner.GetEventBatchAsync(id, cancellationToken);
    }

    public async Task<long> AddFolderRecordAsync(FolderRecord folderRecord, CancellationToken cancellationToken = default)
    {
        var id = await _inner.AddFolderRecordAsync(folderRecord, cancellationToken).ConfigureAwait(false);
        await WriteAsync(
            "folder_record.added",
            "Completed",
            new Dictionary<string, object?>
            {
                ["folderRecordId"] = id,
                ["path"] = folderRecord.Path,
                ["displayName"] = folderRecord.DisplayName,
                ["folderType"] = folderRecord.FolderType,
                ["sourceIntakeFolderId"] = folderRecord.SourceIntakeFolderId,
                ["hasNotesJson"] = !string.IsNullOrWhiteSpace(folderRecord.NotesJson)
            },
            cancellationToken).ConfigureAwait(false);
        return id;
    }

    public Task<FolderRecord?> GetFolderRecordAsync(long id, CancellationToken cancellationToken = default)
    {
        return _inner.GetFolderRecordAsync(id, cancellationToken);
    }

    public async Task<long> AddMetadataEntryAsync(MetadataEntry metadataEntry, CancellationToken cancellationToken = default)
    {
        var id = await _inner.AddMetadataEntryAsync(metadataEntry, cancellationToken).ConfigureAwait(false);
        await WriteAsync(
            "metadata_entry.added",
            "Completed",
            MetadataFields(metadataEntry, id),
            cancellationToken).ConfigureAwait(false);
        return id;
    }

    public async Task UpdateMetadataEntryAsync(MetadataEntry metadataEntry, CancellationToken cancellationToken = default)
    {
        await _inner.UpdateMetadataEntryAsync(metadataEntry, cancellationToken).ConfigureAwait(false);
        await WriteAsync(
            "metadata_entry.updated",
            "Completed",
            MetadataFields(metadataEntry, metadataEntry.Id),
            cancellationToken).ConfigureAwait(false);
    }

    public Task<MetadataEntry?> GetMetadataEntryAsync(long id, CancellationToken cancellationToken = default)
    {
        return _inner.GetMetadataEntryAsync(id, cancellationToken);
    }

    public async Task<long> AddActionAsync(FileActionRecord action, CancellationToken cancellationToken = default)
    {
        var id = await _inner.AddActionAsync(action, cancellationToken).ConfigureAwait(false);
        await WriteAsync(
            "action.added",
            action.Status,
            ActionFields(action, id),
            cancellationToken).ConfigureAwait(false);
        return id;
    }

    public async Task UpdateActionAsync(FileActionRecord action, CancellationToken cancellationToken = default)
    {
        await _inner.UpdateActionAsync(action, cancellationToken).ConfigureAwait(false);
        await WriteAsync(
            "action.updated",
            action.Status,
            ActionFields(action, action.Id),
            cancellationToken).ConfigureAwait(false);
    }

    public Task<FileActionRecord?> GetActionAsync(long id, CancellationToken cancellationToken = default)
    {
        return _inner.GetActionAsync(id, cancellationToken);
    }

    public async Task<long> AddUndoActionAsync(UndoActionRecord undoAction, CancellationToken cancellationToken = default)
    {
        var id = await _inner.AddUndoActionAsync(undoAction, cancellationToken).ConfigureAwait(false);
        await WriteAsync(
            "undo_action.added",
            undoAction.Status,
            UndoFields(undoAction, id),
            cancellationToken).ConfigureAwait(false);
        return id;
    }

    public async Task UpdateUndoActionAsync(UndoActionRecord undoAction, CancellationToken cancellationToken = default)
    {
        await _inner.UpdateUndoActionAsync(undoAction, cancellationToken).ConfigureAwait(false);
        await WriteAsync(
            "undo_action.updated",
            undoAction.Status,
            UndoFields(undoAction, undoAction.Id),
            cancellationToken).ConfigureAwait(false);
    }

    public Task<UndoActionRecord?> GetUndoActionAsync(long id, CancellationToken cancellationToken = default)
    {
        return _inner.GetUndoActionAsync(id, cancellationToken);
    }

    public Task<IReadOnlyList<UndoActionRecord>> ListUndoActionsAsync(
        string? status = null,
        int limit = 100,
        CancellationToken cancellationToken = default)
    {
        return _inner.ListUndoActionsAsync(status, limit, cancellationToken);
    }

    public async Task<long> AddTranscriptionJobAsync(
        TranscriptionJobRecord transcriptionJob,
        CancellationToken cancellationToken = default)
    {
        var id = await _inner.AddTranscriptionJobAsync(transcriptionJob, cancellationToken).ConfigureAwait(false);
        await WriteAsync(
            "transcription_job.added",
            transcriptionJob.Status,
            TranscriptionFields(transcriptionJob, id),
            cancellationToken).ConfigureAwait(false);
        return id;
    }

    public async Task UpdateTranscriptionJobAsync(
        TranscriptionJobRecord transcriptionJob,
        CancellationToken cancellationToken = default)
    {
        await _inner.UpdateTranscriptionJobAsync(transcriptionJob, cancellationToken).ConfigureAwait(false);
        await WriteAsync(
            "transcription_job.updated",
            transcriptionJob.Status,
            TranscriptionFields(transcriptionJob, transcriptionJob.Id),
            cancellationToken).ConfigureAwait(false);
    }

    public Task<TranscriptionJobRecord?> GetTranscriptionJobAsync(
        long id,
        CancellationToken cancellationToken = default)
    {
        return _inner.GetTranscriptionJobAsync(id, cancellationToken);
    }

    public async Task<long> AddVoiceCommandAsync(
        VoiceCommandRecord voiceCommand,
        CancellationToken cancellationToken = default)
    {
        var id = await _inner.AddVoiceCommandAsync(voiceCommand, cancellationToken).ConfigureAwait(false);
        await WriteAsync(
            "voice_command.added",
            voiceCommand.Status,
            new Dictionary<string, object?>
            {
                ["voiceCommandId"] = id,
                ["rawTextLength"] = voiceCommand.RawText.Length,
                ["hasParsedIntent"] = !string.IsNullOrWhiteSpace(voiceCommand.ParsedIntentJson),
                ["resultCount"] = voiceCommand.ResultCount,
                ["executedAction"] = voiceCommand.ExecutedAction,
                ["createdAt"] = voiceCommand.CreatedAt,
                ["detailsJsonLength"] = voiceCommand.DetailsJson?.Length
            },
            cancellationToken).ConfigureAwait(false);
        return id;
    }

    public Task<VoiceCommandRecord?> GetVoiceCommandAsync(long id, CancellationToken cancellationToken = default)
    {
        return _inner.GetVoiceCommandAsync(id, cancellationToken);
    }

    public async Task<long> AddSearchQueryAsync(
        SearchQueryRecord searchQuery,
        CancellationToken cancellationToken = default)
    {
        var id = await _inner.AddSearchQueryAsync(searchQuery, cancellationToken).ConfigureAwait(false);
        await WriteAsync(
            "search_query.added",
            "Completed",
            new Dictionary<string, object?>
            {
                ["searchQueryId"] = id,
                ["queryTextLength"] = searchQuery.QueryText.Length,
                ["hasParsedIntent"] = !string.IsNullOrWhiteSpace(searchQuery.ParsedIntentJson),
                ["provider"] = searchQuery.Provider,
                ["resultCount"] = searchQuery.ResultCount,
                ["createdAt"] = searchQuery.CreatedAt
            },
            cancellationToken).ConfigureAwait(false);
        return id;
    }

    public Task<SearchQueryRecord?> GetSearchQueryAsync(long id, CancellationToken cancellationToken = default)
    {
        return _inner.GetSearchQueryAsync(id, cancellationToken);
    }

    private Task WriteAsync(
        string eventType,
        string status,
        IReadOnlyDictionary<string, object?> fields,
        CancellationToken cancellationToken)
    {
        return _auditLog.WriteAsync(eventType, status, fields, cancellationToken);
    }

    private static Dictionary<string, object?> IntakeFolderFields(IntakeFolder folder, long? id)
    {
        return new Dictionary<string, object?>
        {
            ["intakeFolderId"] = id,
            ["path"] = folder.Path,
            ["displayName"] = folder.DisplayName,
            ["enabled"] = folder.Enabled,
            ["folderType"] = folder.FolderType,
            ["recursive"] = folder.Recursive,
            ["createdAt"] = folder.CreatedAt,
            ["updatedAt"] = folder.UpdatedAt
        };
    }

    private static Dictionary<string, object?> FileRecordFields(FileRecord fileRecord, long? id)
    {
        return new Dictionary<string, object?>
        {
            ["fileRecordId"] = id,
            ["originalFilename"] = fileRecord.OriginalFilename,
            ["currentFilename"] = fileRecord.CurrentFilename,
            ["originalPath"] = fileRecord.OriginalPath,
            ["currentPath"] = fileRecord.CurrentPath,
            ["extension"] = fileRecord.Extension,
            ["sizeBytes"] = fileRecord.SizeBytes,
            ["sourceIntakeFolderId"] = fileRecord.SourceIntakeFolderId,
            ["triageCategory"] = fileRecord.TriageCategory,
            ["triageConfidence"] = fileRecord.TriageConfidence,
            ["isMeaningful"] = fileRecord.IsMeaningful,
            ["hasSha256"] = !string.IsNullOrWhiteSpace(fileRecord.Sha256),
            ["hasNotesJson"] = !string.IsNullOrWhiteSpace(fileRecord.NotesJson)
        };
    }

    private static Dictionary<string, object?> MetadataFields(MetadataEntry metadataEntry, long? id)
    {
        return new Dictionary<string, object?>
        {
            ["metadataEntryId"] = id,
            ["fileRecordId"] = metadataEntry.FileRecordId,
            ["folderRecordId"] = metadataEntry.FolderRecordId,
            ["relevance"] = metadataEntry.Relevance,
            ["projectLength"] = metadataEntry.Project?.Length,
            ["topicLength"] = metadataEntry.Topic?.Length,
            ["hasUserNote"] = !string.IsNullOrWhiteSpace(metadataEntry.UserNote),
            ["hasTranscript"] = !string.IsNullOrWhiteSpace(metadataEntry.TranscriptText),
            ["hasTags"] = !string.IsNullOrWhiteSpace(metadataEntry.TagsJson),
            ["hasSourceUrl"] = !string.IsNullOrWhiteSpace(metadataEntry.SourceUrl),
            ["hasReferrerUrl"] = !string.IsNullOrWhiteSpace(metadataEntry.ReferrerUrl),
            ["hasAgentSummary"] = !string.IsNullOrWhiteSpace(metadataEntry.AgentSummary),
            ["classifierConfidence"] = metadataEntry.ClassifierConfidence,
            ["createdAt"] = metadataEntry.CreatedAt,
            ["updatedAt"] = metadataEntry.UpdatedAt
        };
    }

    private static Dictionary<string, object?> ActionFields(FileActionRecord action, long? id)
    {
        return new Dictionary<string, object?>
        {
            ["actionId"] = id,
            ["actionType"] = action.ActionType,
            ["targetFileRecordId"] = action.TargetFileRecordId,
            ["oldPath"] = action.OldPath,
            ["newPath"] = action.NewPath,
            ["createdAt"] = action.CreatedAt,
            ["completedAt"] = action.CompletedAt,
            ["detailsJsonLength"] = action.DetailsJson?.Length
        };
    }

    private static Dictionary<string, object?> UndoFields(UndoActionRecord undoAction, long? id)
    {
        return new Dictionary<string, object?>
        {
            ["undoActionId"] = id,
            ["actionId"] = undoAction.ActionId,
            ["targetFileRecordId"] = undoAction.TargetFileRecordId,
            ["undoType"] = undoAction.UndoType,
            ["originalPath"] = undoAction.OriginalPath,
            ["resultingPath"] = undoAction.ResultingPath,
            ["hasFileIdentity"] = !string.IsNullOrWhiteSpace(undoAction.FileIdentityJson),
            ["createdAt"] = undoAction.CreatedAt,
            ["performedAt"] = undoAction.PerformedAt
        };
    }

    private static Dictionary<string, object?> TranscriptionFields(TranscriptionJobRecord transcriptionJob, long? id)
    {
        return new Dictionary<string, object?>
        {
            ["transcriptionJobId"] = id,
            ["provider"] = transcriptionJob.Provider,
            ["hasAudioPath"] = !string.IsNullOrWhiteSpace(transcriptionJob.AudioPath),
            ["durationMs"] = transcriptionJob.DurationMs,
            ["hasTranscript"] = !string.IsNullOrWhiteSpace(transcriptionJob.TranscriptText),
            ["hasError"] = !string.IsNullOrWhiteSpace(transcriptionJob.ErrorMessage),
            ["createdAt"] = transcriptionJob.CreatedAt,
            ["completedAt"] = transcriptionJob.CompletedAt,
            ["hasProviderMetadata"] = !string.IsNullOrWhiteSpace(transcriptionJob.ProviderMetadataJson)
        };
    }
}
