using System.Globalization;
using FileIntakeAssistant.Core.Models;
using FileIntakeAssistant.Core.Persistence;
using Microsoft.Data.Sqlite;

namespace FileIntakeAssistant.Infrastructure.Persistence;

public sealed class SqliteFileIntakeStore : IFileIntakeStore
{
    private readonly string _databasePath;

    public SqliteFileIntakeStore(string databasePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(databasePath);
        _databasePath = databasePath;
    }

    public async Task UpsertAppSettingAsync(AppSetting setting, CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO app_settings (key, value_json, updated_at)
            VALUES ($key, $value_json, $updated_at)
            ON CONFLICT(key) DO UPDATE SET
                value_json = excluded.value_json,
                updated_at = excluded.updated_at;
            """;
        Add(command, "$key", setting.Key);
        Add(command, "$value_json", setting.ValueJson);
        Add(command, "$updated_at", ToDb(setting.UpdatedAt));
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<AppSetting?> GetAppSettingAsync(string key, CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT key, value_json, updated_at FROM app_settings WHERE key = $key;";
        Add(command, "$key", key);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        return await reader.ReadAsync(cancellationToken).ConfigureAwait(false)
            ? new AppSetting(
                reader.GetString(0),
                reader.GetString(1),
                FromDb(reader.GetString(2)))
            : null;
    }

    public Task<long> AddIntakeFolderAsync(IntakeFolder folder, CancellationToken cancellationToken = default)
    {
        const string sql = """
            INSERT INTO intake_folders (path, display_name, enabled, folder_type, recursive, created_at, updated_at)
            VALUES ($path, $display_name, $enabled, $folder_type, $recursive, $created_at, $updated_at);
            """;

        return InsertAsync(sql, command => BindIntakeFolder(command, folder, includeId: false), cancellationToken);
    }

    public async Task UpdateIntakeFolderAsync(IntakeFolder folder, CancellationToken cancellationToken = default)
    {
        const string sql = """
            UPDATE intake_folders
            SET path = $path,
                display_name = $display_name,
                enabled = $enabled,
                folder_type = $folder_type,
                recursive = $recursive,
                created_at = $created_at,
                updated_at = $updated_at
            WHERE id = $id;
            """;

        await ExecuteAsync(sql, command => BindIntakeFolder(command, folder, includeId: true), cancellationToken)
            .ConfigureAwait(false);
    }

    public Task<IntakeFolder?> GetIntakeFolderAsync(long id, CancellationToken cancellationToken = default)
    {
        return SingleOrDefaultAsync(
            "SELECT * FROM intake_folders WHERE id = $id;",
            command => Add(command, "$id", id),
            ReadIntakeFolder,
            cancellationToken);
    }

    public Task<IntakeFolder?> GetIntakeFolderByPathAsync(string path, CancellationToken cancellationToken = default)
    {
        return SingleOrDefaultAsync(
            "SELECT * FROM intake_folders WHERE path = $path;",
            command => Add(command, "$path", path),
            ReadIntakeFolder,
            cancellationToken);
    }

    public Task<IReadOnlyList<IntakeFolder>> ListIntakeFoldersAsync(
        bool enabledOnly = false,
        CancellationToken cancellationToken = default)
    {
        var sql = enabledOnly
            ? "SELECT * FROM intake_folders WHERE enabled = 1 ORDER BY path;"
            : "SELECT * FROM intake_folders ORDER BY path;";

        return ListAsync(sql, _ => { }, ReadIntakeFolder, cancellationToken);
    }

    public Task<long> AddFileRecordAsync(FileRecord fileRecord, CancellationToken cancellationToken = default)
    {
        const string sql = """
            INSERT INTO file_records (
                sha256,
                original_filename,
                current_filename,
                original_path,
                current_path,
                extension,
                size_bytes,
                mime_type,
                source_intake_folder_id,
                first_seen_at,
                last_seen_at,
                stable_at,
                status,
                triage_category,
                triage_confidence,
                is_meaningful,
                notes_json)
            VALUES (
                $sha256,
                $original_filename,
                $current_filename,
                $original_path,
                $current_path,
                $extension,
                $size_bytes,
                $mime_type,
                $source_intake_folder_id,
                $first_seen_at,
                $last_seen_at,
                $stable_at,
                $status,
                $triage_category,
                $triage_confidence,
                $is_meaningful,
                $notes_json);
            """;

        return InsertAsync(sql, command => BindFileRecord(command, fileRecord, includeId: false), cancellationToken);
    }

    public async Task UpdateFileRecordAsync(FileRecord fileRecord, CancellationToken cancellationToken = default)
    {
        const string sql = """
            UPDATE file_records
            SET sha256 = $sha256,
                original_filename = $original_filename,
                current_filename = $current_filename,
                original_path = $original_path,
                current_path = $current_path,
                extension = $extension,
                size_bytes = $size_bytes,
                mime_type = $mime_type,
                source_intake_folder_id = $source_intake_folder_id,
                first_seen_at = $first_seen_at,
                last_seen_at = $last_seen_at,
                stable_at = $stable_at,
                status = $status,
                triage_category = $triage_category,
                triage_confidence = $triage_confidence,
                is_meaningful = $is_meaningful,
                notes_json = $notes_json
            WHERE id = $id;
            """;

        await ExecuteAsync(sql, command => BindFileRecord(command, fileRecord, includeId: true), cancellationToken)
            .ConfigureAwait(false);
    }

    public Task<FileRecord?> GetFileRecordAsync(long id, CancellationToken cancellationToken = default)
    {
        return SingleOrDefaultAsync(
            "SELECT * FROM file_records WHERE id = $id;",
            command => Add(command, "$id", id),
            ReadFileRecord,
            cancellationToken);
    }

    public Task<FileRecord?> GetFileRecordByCurrentPathAsync(
        string currentPath,
        CancellationToken cancellationToken = default)
    {
        return SingleOrDefaultAsync(
            "SELECT * FROM file_records WHERE current_path = $current_path;",
            command => Add(command, "$current_path", currentPath),
            ReadFileRecord,
            cancellationToken);
    }

    public Task<long> AddFileEventAsync(FileEventRecord fileEvent, CancellationToken cancellationToken = default)
    {
        const string sql = """
            INSERT INTO file_events (
                file_record_id,
                event_type,
                raw_path,
                old_path,
                new_path,
                observed_at,
                normalized_at,
                triage_category,
                triage_reason,
                batch_id,
                ignored,
                details_json)
            VALUES (
                $file_record_id,
                $event_type,
                $raw_path,
                $old_path,
                $new_path,
                $observed_at,
                $normalized_at,
                $triage_category,
                $triage_reason,
                $batch_id,
                $ignored,
                $details_json);
            """;

        return InsertAsync(sql, command => BindFileEvent(command, fileEvent), cancellationToken);
    }

    public Task<FileEventRecord?> GetFileEventAsync(long id, CancellationToken cancellationToken = default)
    {
        return SingleOrDefaultAsync(
            "SELECT * FROM file_events WHERE id = $id;",
            command => Add(command, "$id", id),
            ReadFileEvent,
            cancellationToken);
    }

    public Task<IReadOnlyList<FileEventRecord>> ListFileEventsAsync(
        int limit = 100,
        CancellationToken cancellationToken = default)
    {
        if (limit <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(limit), "Limit must be positive.");
        }

        return ListAsync(
            """
            SELECT *
            FROM file_events
            ORDER BY observed_at DESC, id DESC
            LIMIT $limit;
            """,
            command => Add(command, "$limit", limit),
            ReadFileEvent,
            cancellationToken);
    }

    public Task<long> AddEventBatchAsync(EventBatch batch, CancellationToken cancellationToken = default)
    {
        const string sql = """
            INSERT INTO event_batches (root_path, batch_type, started_at, ended_at, file_count, decision, details_json)
            VALUES ($root_path, $batch_type, $started_at, $ended_at, $file_count, $decision, $details_json);
            """;

        return InsertAsync(sql, command => BindEventBatch(command, batch), cancellationToken);
    }

    public Task<EventBatch?> GetEventBatchAsync(long id, CancellationToken cancellationToken = default)
    {
        return SingleOrDefaultAsync(
            "SELECT * FROM event_batches WHERE id = $id;",
            command => Add(command, "$id", id),
            ReadEventBatch,
            cancellationToken);
    }

    public Task<long> AddFolderRecordAsync(FolderRecord folderRecord, CancellationToken cancellationToken = default)
    {
        const string sql = """
            INSERT INTO folder_records (
                path,
                display_name,
                folder_type,
                source_intake_folder_id,
                created_at,
                updated_at,
                notes_json)
            VALUES (
                $path,
                $display_name,
                $folder_type,
                $source_intake_folder_id,
                $created_at,
                $updated_at,
                $notes_json);
            """;

        return InsertAsync(sql, command => BindFolderRecord(command, folderRecord), cancellationToken);
    }

    public Task<FolderRecord?> GetFolderRecordAsync(long id, CancellationToken cancellationToken = default)
    {
        return SingleOrDefaultAsync(
            "SELECT * FROM folder_records WHERE id = $id;",
            command => Add(command, "$id", id),
            ReadFolderRecord,
            cancellationToken);
    }

    public Task<long> AddMetadataEntryAsync(MetadataEntry metadataEntry, CancellationToken cancellationToken = default)
    {
        const string sql = """
            INSERT INTO metadata_entries (
                file_record_id,
                folder_record_id,
                user_note,
                transcript_text,
                relevance,
                project,
                topic,
                tags_json,
                source_url,
                referrer_url,
                agent_summary,
                classifier_confidence,
                created_at,
                updated_at)
            VALUES (
                $file_record_id,
                $folder_record_id,
                $user_note,
                $transcript_text,
                $relevance,
                $project,
                $topic,
                $tags_json,
                $source_url,
                $referrer_url,
                $agent_summary,
                $classifier_confidence,
                $created_at,
                $updated_at);
            """;

        return InsertAsync(sql, command => BindMetadataEntry(command, metadataEntry, includeId: false), cancellationToken);
    }

    public async Task UpdateMetadataEntryAsync(MetadataEntry metadataEntry, CancellationToken cancellationToken = default)
    {
        const string sql = """
            UPDATE metadata_entries
            SET file_record_id = $file_record_id,
                folder_record_id = $folder_record_id,
                user_note = $user_note,
                transcript_text = $transcript_text,
                relevance = $relevance,
                project = $project,
                topic = $topic,
                tags_json = $tags_json,
                source_url = $source_url,
                referrer_url = $referrer_url,
                agent_summary = $agent_summary,
                classifier_confidence = $classifier_confidence,
                created_at = $created_at,
                updated_at = $updated_at
            WHERE id = $id;
            """;

        await ExecuteAsync(sql, command => BindMetadataEntry(command, metadataEntry, includeId: true), cancellationToken)
            .ConfigureAwait(false);
    }

    public Task<MetadataEntry?> GetMetadataEntryAsync(long id, CancellationToken cancellationToken = default)
    {
        return SingleOrDefaultAsync(
            "SELECT * FROM metadata_entries WHERE id = $id;",
            command => Add(command, "$id", id),
            ReadMetadataEntry,
            cancellationToken);
    }

    public Task<long> AddActionAsync(FileActionRecord action, CancellationToken cancellationToken = default)
    {
        const string sql = """
            INSERT INTO actions (action_type, target_file_record_id, old_path, new_path, status, created_at, completed_at, details_json)
            VALUES ($action_type, $target_file_record_id, $old_path, $new_path, $status, $created_at, $completed_at, $details_json);
            """;

        return InsertAsync(sql, command => BindAction(command, action, includeId: false), cancellationToken);
    }

    public async Task UpdateActionAsync(FileActionRecord action, CancellationToken cancellationToken = default)
    {
        const string sql = """
            UPDATE actions
            SET action_type = $action_type,
                target_file_record_id = $target_file_record_id,
                old_path = $old_path,
                new_path = $new_path,
                status = $status,
                created_at = $created_at,
                completed_at = $completed_at,
                details_json = $details_json
            WHERE id = $id;
            """;

        await ExecuteAsync(sql, command => BindAction(command, action, includeId: true), cancellationToken)
            .ConfigureAwait(false);
    }

    public Task<FileActionRecord?> GetActionAsync(long id, CancellationToken cancellationToken = default)
    {
        return SingleOrDefaultAsync(
            "SELECT * FROM actions WHERE id = $id;",
            command => Add(command, "$id", id),
            ReadAction,
            cancellationToken);
    }

    public Task<long> AddUndoActionAsync(UndoActionRecord undoAction, CancellationToken cancellationToken = default)
    {
        const string sql = """
            INSERT INTO undo_actions (
                action_id,
                target_file_record_id,
                undo_type,
                original_path,
                resulting_path,
                file_identity_json,
                status,
                created_at,
                performed_at)
            VALUES (
                $action_id,
                $target_file_record_id,
                $undo_type,
                $original_path,
                $resulting_path,
                $file_identity_json,
                $status,
                $created_at,
                $performed_at);
            """;

        return InsertAsync(sql, command => BindUndoAction(command, undoAction, includeId: false), cancellationToken);
    }

    public async Task UpdateUndoActionAsync(UndoActionRecord undoAction, CancellationToken cancellationToken = default)
    {
        const string sql = """
            UPDATE undo_actions
            SET action_id = $action_id,
                target_file_record_id = $target_file_record_id,
                undo_type = $undo_type,
                original_path = $original_path,
                resulting_path = $resulting_path,
                file_identity_json = $file_identity_json,
                status = $status,
                created_at = $created_at,
                performed_at = $performed_at
            WHERE id = $id;
            """;

        await ExecuteAsync(sql, command => BindUndoAction(command, undoAction, includeId: true), cancellationToken)
            .ConfigureAwait(false);
    }

    public Task<UndoActionRecord?> GetUndoActionAsync(long id, CancellationToken cancellationToken = default)
    {
        return SingleOrDefaultAsync(
            "SELECT * FROM undo_actions WHERE id = $id;",
            command => Add(command, "$id", id),
            ReadUndoAction,
            cancellationToken);
    }

    public Task<IReadOnlyList<UndoActionRecord>> ListUndoActionsAsync(
        string? status = null,
        int limit = 100,
        CancellationToken cancellationToken = default)
    {
        if (limit <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(limit), "Limit must be positive.");
        }

        if (string.IsNullOrWhiteSpace(status))
        {
            return ListAsync(
                """
                SELECT *
                FROM undo_actions
                ORDER BY created_at DESC, id DESC
                LIMIT $limit;
                """,
                command => Add(command, "$limit", limit),
                ReadUndoAction,
                cancellationToken);
        }

        return ListAsync(
            """
            SELECT *
            FROM undo_actions
            WHERE status = $status
            ORDER BY created_at DESC, id DESC
            LIMIT $limit;
            """,
            command =>
            {
                Add(command, "$status", status);
                Add(command, "$limit", limit);
            },
            ReadUndoAction,
            cancellationToken);
    }

    public Task<long> AddTranscriptionJobAsync(TranscriptionJobRecord transcriptionJob, CancellationToken cancellationToken = default)
    {
        const string sql = """
            INSERT INTO transcription_jobs (
                provider,
                audio_path,
                duration_ms,
                transcript_text,
                status,
                error_message,
                created_at,
                completed_at,
                provider_metadata_json)
            VALUES (
                $provider,
                $audio_path,
                $duration_ms,
                $transcript_text,
                $status,
                $error_message,
                $created_at,
                $completed_at,
                $provider_metadata_json);
            """;

        return InsertAsync(sql, command => BindTranscriptionJob(command, transcriptionJob, includeId: false), cancellationToken);
    }

    public async Task UpdateTranscriptionJobAsync(TranscriptionJobRecord transcriptionJob, CancellationToken cancellationToken = default)
    {
        const string sql = """
            UPDATE transcription_jobs
            SET provider = $provider,
                audio_path = $audio_path,
                duration_ms = $duration_ms,
                transcript_text = $transcript_text,
                status = $status,
                error_message = $error_message,
                created_at = $created_at,
                completed_at = $completed_at,
                provider_metadata_json = $provider_metadata_json
            WHERE id = $id;
            """;

        await ExecuteAsync(sql, command => BindTranscriptionJob(command, transcriptionJob, includeId: true), cancellationToken)
            .ConfigureAwait(false);
    }

    public Task<TranscriptionJobRecord?> GetTranscriptionJobAsync(long id, CancellationToken cancellationToken = default)
    {
        return SingleOrDefaultAsync(
            "SELECT * FROM transcription_jobs WHERE id = $id;",
            command => Add(command, "$id", id),
            ReadTranscriptionJob,
            cancellationToken);
    }

    public Task<long> AddVoiceCommandAsync(VoiceCommandRecord voiceCommand, CancellationToken cancellationToken = default)
    {
        const string sql = """
            INSERT INTO voice_commands (raw_text, parsed_intent_json, status, result_count, executed_action, created_at, details_json)
            VALUES ($raw_text, $parsed_intent_json, $status, $result_count, $executed_action, $created_at, $details_json);
            """;

        return InsertAsync(sql, command => BindVoiceCommand(command, voiceCommand), cancellationToken);
    }

    public Task<VoiceCommandRecord?> GetVoiceCommandAsync(long id, CancellationToken cancellationToken = default)
    {
        return SingleOrDefaultAsync(
            "SELECT * FROM voice_commands WHERE id = $id;",
            command => Add(command, "$id", id),
            ReadVoiceCommand,
            cancellationToken);
    }

    public Task<long> AddSearchQueryAsync(SearchQueryRecord searchQuery, CancellationToken cancellationToken = default)
    {
        const string sql = """
            INSERT INTO search_queries (query_text, parsed_intent_json, provider, result_count, created_at)
            VALUES ($query_text, $parsed_intent_json, $provider, $result_count, $created_at);
            """;

        return InsertAsync(sql, command => BindSearchQuery(command, searchQuery), cancellationToken);
    }

    public Task<SearchQueryRecord?> GetSearchQueryAsync(long id, CancellationToken cancellationToken = default)
    {
        return SingleOrDefaultAsync(
            "SELECT * FROM search_queries WHERE id = $id;",
            command => Add(command, "$id", id),
            ReadSearchQuery,
            cancellationToken);
    }

    private async Task<long> InsertAsync(
        string sql,
        Action<SqliteCommand> bind,
        CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = $"{sql}{Environment.NewLine}SELECT last_insert_rowid();";
        bind(command);

        var result = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        return Convert.ToInt64(result, CultureInfo.InvariantCulture);
    }

    private async Task ExecuteAsync(
        string sql,
        Action<SqliteCommand> bind,
        CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        bind(command);

        var affected = await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        if (affected == 0)
        {
            throw new InvalidOperationException("No rows were updated.");
        }
    }

    private async Task<T?> SingleOrDefaultAsync<T>(
        string sql,
        Action<SqliteCommand> bind,
        Func<SqliteDataReader, T> read,
        CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        bind(command);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        return await reader.ReadAsync(cancellationToken).ConfigureAwait(false) ? read(reader) : default;
    }

    private async Task<IReadOnlyList<T>> ListAsync<T>(
        string sql,
        Action<SqliteCommand> bind,
        Func<SqliteDataReader, T> read,
        CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        bind(command);

        var values = new List<T>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            values.Add(read(reader));
        }

        return values;
    }

    private async Task<SqliteConnection> OpenConnectionAsync(CancellationToken cancellationToken)
    {
        var directory = Path.GetDirectoryName(Path.GetFullPath(_databasePath));
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = _databasePath,
            Mode = SqliteOpenMode.ReadWriteCreate
        }.ToString();

        var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        await using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA foreign_keys = ON;";
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);

        return connection;
    }

    private static void BindIntakeFolder(SqliteCommand command, IntakeFolder folder, bool includeId)
    {
        if (includeId)
        {
            Add(command, "$id", RequiredId(folder.Id));
        }

        Add(command, "$path", folder.Path);
        Add(command, "$display_name", folder.DisplayName);
        Add(command, "$enabled", ToDb(folder.Enabled));
        Add(command, "$folder_type", folder.FolderType);
        Add(command, "$recursive", ToDb(folder.Recursive));
        Add(command, "$created_at", ToDb(folder.CreatedAt));
        Add(command, "$updated_at", ToDb(folder.UpdatedAt));
    }

    private static void BindFileRecord(SqliteCommand command, FileRecord fileRecord, bool includeId)
    {
        if (includeId)
        {
            Add(command, "$id", RequiredId(fileRecord.Id));
        }

        Add(command, "$sha256", fileRecord.Sha256);
        Add(command, "$original_filename", fileRecord.OriginalFilename);
        Add(command, "$current_filename", fileRecord.CurrentFilename);
        Add(command, "$original_path", fileRecord.OriginalPath);
        Add(command, "$current_path", fileRecord.CurrentPath);
        Add(command, "$extension", fileRecord.Extension);
        Add(command, "$size_bytes", fileRecord.SizeBytes);
        Add(command, "$mime_type", fileRecord.MimeType);
        Add(command, "$source_intake_folder_id", fileRecord.SourceIntakeFolderId);
        Add(command, "$first_seen_at", ToDb(fileRecord.FirstSeenAt));
        Add(command, "$last_seen_at", ToDb(fileRecord.LastSeenAt));
        Add(command, "$stable_at", ToDb(fileRecord.StableAt));
        Add(command, "$status", fileRecord.Status);
        Add(command, "$triage_category", fileRecord.TriageCategory);
        Add(command, "$triage_confidence", fileRecord.TriageConfidence);
        Add(command, "$is_meaningful", ToDb(fileRecord.IsMeaningful));
        Add(command, "$notes_json", fileRecord.NotesJson);
    }

    private static void BindFileEvent(SqliteCommand command, FileEventRecord fileEvent)
    {
        Add(command, "$file_record_id", fileEvent.FileRecordId);
        Add(command, "$event_type", fileEvent.EventType);
        Add(command, "$raw_path", fileEvent.RawPath);
        Add(command, "$old_path", fileEvent.OldPath);
        Add(command, "$new_path", fileEvent.NewPath);
        Add(command, "$observed_at", ToDb(fileEvent.ObservedAt));
        Add(command, "$normalized_at", ToDb(fileEvent.NormalizedAt));
        Add(command, "$triage_category", fileEvent.TriageCategory);
        Add(command, "$triage_reason", fileEvent.TriageReason);
        Add(command, "$batch_id", fileEvent.BatchId);
        Add(command, "$ignored", ToDb(fileEvent.Ignored));
        Add(command, "$details_json", fileEvent.DetailsJson);
    }

    private static void BindEventBatch(SqliteCommand command, EventBatch batch)
    {
        Add(command, "$root_path", batch.RootPath);
        Add(command, "$batch_type", batch.BatchType);
        Add(command, "$started_at", ToDb(batch.StartedAt));
        Add(command, "$ended_at", ToDb(batch.EndedAt));
        Add(command, "$file_count", batch.FileCount);
        Add(command, "$decision", batch.Decision);
        Add(command, "$details_json", batch.DetailsJson);
    }

    private static void BindFolderRecord(SqliteCommand command, FolderRecord folderRecord)
    {
        Add(command, "$path", folderRecord.Path);
        Add(command, "$display_name", folderRecord.DisplayName);
        Add(command, "$folder_type", folderRecord.FolderType);
        Add(command, "$source_intake_folder_id", folderRecord.SourceIntakeFolderId);
        Add(command, "$created_at", ToDb(folderRecord.CreatedAt));
        Add(command, "$updated_at", ToDb(folderRecord.UpdatedAt));
        Add(command, "$notes_json", folderRecord.NotesJson);
    }

    private static void BindMetadataEntry(SqliteCommand command, MetadataEntry metadataEntry, bool includeId)
    {
        if (includeId)
        {
            Add(command, "$id", RequiredId(metadataEntry.Id));
        }

        Add(command, "$file_record_id", metadataEntry.FileRecordId);
        Add(command, "$folder_record_id", metadataEntry.FolderRecordId);
        Add(command, "$user_note", metadataEntry.UserNote);
        Add(command, "$transcript_text", metadataEntry.TranscriptText);
        Add(command, "$relevance", metadataEntry.Relevance);
        Add(command, "$project", metadataEntry.Project);
        Add(command, "$topic", metadataEntry.Topic);
        Add(command, "$tags_json", metadataEntry.TagsJson);
        Add(command, "$source_url", metadataEntry.SourceUrl);
        Add(command, "$referrer_url", metadataEntry.ReferrerUrl);
        Add(command, "$agent_summary", metadataEntry.AgentSummary);
        Add(command, "$classifier_confidence", metadataEntry.ClassifierConfidence);
        Add(command, "$created_at", ToDb(metadataEntry.CreatedAt));
        Add(command, "$updated_at", ToDb(metadataEntry.UpdatedAt));
    }

    private static void BindAction(SqliteCommand command, FileActionRecord action, bool includeId)
    {
        if (includeId)
        {
            Add(command, "$id", RequiredId(action.Id));
        }

        Add(command, "$action_type", action.ActionType);
        Add(command, "$target_file_record_id", action.TargetFileRecordId);
        Add(command, "$old_path", action.OldPath);
        Add(command, "$new_path", action.NewPath);
        Add(command, "$status", action.Status);
        Add(command, "$created_at", ToDb(action.CreatedAt));
        Add(command, "$completed_at", ToDb(action.CompletedAt));
        Add(command, "$details_json", action.DetailsJson);
    }

    private static void BindUndoAction(SqliteCommand command, UndoActionRecord undoAction, bool includeId)
    {
        if (includeId)
        {
            Add(command, "$id", RequiredId(undoAction.Id));
        }

        Add(command, "$action_id", undoAction.ActionId);
        Add(command, "$target_file_record_id", undoAction.TargetFileRecordId);
        Add(command, "$undo_type", undoAction.UndoType);
        Add(command, "$original_path", undoAction.OriginalPath);
        Add(command, "$resulting_path", undoAction.ResultingPath);
        Add(command, "$file_identity_json", undoAction.FileIdentityJson);
        Add(command, "$status", undoAction.Status);
        Add(command, "$created_at", ToDb(undoAction.CreatedAt));
        Add(command, "$performed_at", ToDb(undoAction.PerformedAt));
    }

    private static void BindTranscriptionJob(SqliteCommand command, TranscriptionJobRecord transcriptionJob, bool includeId)
    {
        if (includeId)
        {
            Add(command, "$id", RequiredId(transcriptionJob.Id));
        }

        Add(command, "$provider", transcriptionJob.Provider);
        Add(command, "$audio_path", transcriptionJob.AudioPath);
        Add(command, "$duration_ms", transcriptionJob.DurationMs);
        Add(command, "$transcript_text", transcriptionJob.TranscriptText);
        Add(command, "$status", transcriptionJob.Status);
        Add(command, "$error_message", transcriptionJob.ErrorMessage);
        Add(command, "$created_at", ToDb(transcriptionJob.CreatedAt));
        Add(command, "$completed_at", ToDb(transcriptionJob.CompletedAt));
        Add(command, "$provider_metadata_json", transcriptionJob.ProviderMetadataJson);
    }

    private static void BindVoiceCommand(SqliteCommand command, VoiceCommandRecord voiceCommand)
    {
        Add(command, "$raw_text", voiceCommand.RawText);
        Add(command, "$parsed_intent_json", voiceCommand.ParsedIntentJson);
        Add(command, "$status", voiceCommand.Status);
        Add(command, "$result_count", voiceCommand.ResultCount);
        Add(command, "$executed_action", voiceCommand.ExecutedAction);
        Add(command, "$created_at", ToDb(voiceCommand.CreatedAt));
        Add(command, "$details_json", voiceCommand.DetailsJson);
    }

    private static void BindSearchQuery(SqliteCommand command, SearchQueryRecord searchQuery)
    {
        Add(command, "$query_text", searchQuery.QueryText);
        Add(command, "$parsed_intent_json", searchQuery.ParsedIntentJson);
        Add(command, "$provider", searchQuery.Provider);
        Add(command, "$result_count", searchQuery.ResultCount);
        Add(command, "$created_at", ToDb(searchQuery.CreatedAt));
    }

    private static IntakeFolder ReadIntakeFolder(SqliteDataReader reader)
    {
        return new IntakeFolder(
            Id: GetInt64(reader, "id"),
            Path: GetString(reader, "path"),
            DisplayName: GetString(reader, "display_name"),
            Enabled: GetBoolean(reader, "enabled"),
            FolderType: GetString(reader, "folder_type"),
            Recursive: GetBoolean(reader, "recursive"),
            CreatedAt: GetDateTimeOffset(reader, "created_at"),
            UpdatedAt: GetDateTimeOffset(reader, "updated_at"));
    }

    private static FileRecord ReadFileRecord(SqliteDataReader reader)
    {
        return new FileRecord(
            Id: GetInt64(reader, "id"),
            Sha256: GetNullableString(reader, "sha256"),
            OriginalFilename: GetString(reader, "original_filename"),
            CurrentFilename: GetString(reader, "current_filename"),
            OriginalPath: GetString(reader, "original_path"),
            CurrentPath: GetString(reader, "current_path"),
            Extension: GetString(reader, "extension"),
            SizeBytes: GetInt64(reader, "size_bytes"),
            MimeType: GetNullableString(reader, "mime_type"),
            SourceIntakeFolderId: GetNullableInt64(reader, "source_intake_folder_id"),
            FirstSeenAt: GetDateTimeOffset(reader, "first_seen_at"),
            LastSeenAt: GetDateTimeOffset(reader, "last_seen_at"),
            StableAt: GetNullableDateTimeOffset(reader, "stable_at"),
            Status: GetString(reader, "status"),
            TriageCategory: GetString(reader, "triage_category"),
            TriageConfidence: GetDouble(reader, "triage_confidence"),
            IsMeaningful: GetBoolean(reader, "is_meaningful"),
            NotesJson: GetNullableString(reader, "notes_json"));
    }

    private static FileEventRecord ReadFileEvent(SqliteDataReader reader)
    {
        return new FileEventRecord(
            Id: GetInt64(reader, "id"),
            FileRecordId: GetNullableInt64(reader, "file_record_id"),
            EventType: GetString(reader, "event_type"),
            RawPath: GetString(reader, "raw_path"),
            OldPath: GetNullableString(reader, "old_path"),
            NewPath: GetNullableString(reader, "new_path"),
            ObservedAt: GetDateTimeOffset(reader, "observed_at"),
            NormalizedAt: GetDateTimeOffset(reader, "normalized_at"),
            TriageCategory: GetString(reader, "triage_category"),
            TriageReason: GetString(reader, "triage_reason"),
            BatchId: GetNullableInt64(reader, "batch_id"),
            Ignored: GetBoolean(reader, "ignored"),
            DetailsJson: GetNullableString(reader, "details_json"));
    }

    private static EventBatch ReadEventBatch(SqliteDataReader reader)
    {
        return new EventBatch(
            Id: GetInt64(reader, "id"),
            RootPath: GetString(reader, "root_path"),
            BatchType: GetString(reader, "batch_type"),
            StartedAt: GetDateTimeOffset(reader, "started_at"),
            EndedAt: GetNullableDateTimeOffset(reader, "ended_at"),
            FileCount: GetInt32(reader, "file_count"),
            Decision: GetString(reader, "decision"),
            DetailsJson: GetNullableString(reader, "details_json"));
    }

    private static FolderRecord ReadFolderRecord(SqliteDataReader reader)
    {
        return new FolderRecord(
            Id: GetInt64(reader, "id"),
            Path: GetString(reader, "path"),
            DisplayName: GetString(reader, "display_name"),
            FolderType: GetString(reader, "folder_type"),
            SourceIntakeFolderId: GetNullableInt64(reader, "source_intake_folder_id"),
            CreatedAt: GetDateTimeOffset(reader, "created_at"),
            UpdatedAt: GetDateTimeOffset(reader, "updated_at"),
            NotesJson: GetNullableString(reader, "notes_json"));
    }

    private static MetadataEntry ReadMetadataEntry(SqliteDataReader reader)
    {
        return new MetadataEntry(
            Id: GetInt64(reader, "id"),
            FileRecordId: GetNullableInt64(reader, "file_record_id"),
            FolderRecordId: GetNullableInt64(reader, "folder_record_id"),
            UserNote: GetNullableString(reader, "user_note"),
            TranscriptText: GetNullableString(reader, "transcript_text"),
            Relevance: GetNullableString(reader, "relevance"),
            Project: GetNullableString(reader, "project"),
            Topic: GetNullableString(reader, "topic"),
            TagsJson: GetNullableString(reader, "tags_json"),
            SourceUrl: GetNullableString(reader, "source_url"),
            ReferrerUrl: GetNullableString(reader, "referrer_url"),
            AgentSummary: GetNullableString(reader, "agent_summary"),
            ClassifierConfidence: GetNullableDouble(reader, "classifier_confidence"),
            CreatedAt: GetDateTimeOffset(reader, "created_at"),
            UpdatedAt: GetDateTimeOffset(reader, "updated_at"));
    }

    private static FileActionRecord ReadAction(SqliteDataReader reader)
    {
        return new FileActionRecord(
            Id: GetInt64(reader, "id"),
            ActionType: GetString(reader, "action_type"),
            TargetFileRecordId: GetNullableInt64(reader, "target_file_record_id"),
            OldPath: GetNullableString(reader, "old_path"),
            NewPath: GetNullableString(reader, "new_path"),
            Status: GetString(reader, "status"),
            CreatedAt: GetDateTimeOffset(reader, "created_at"),
            CompletedAt: GetNullableDateTimeOffset(reader, "completed_at"),
            DetailsJson: GetNullableString(reader, "details_json"));
    }

    private static UndoActionRecord ReadUndoAction(SqliteDataReader reader)
    {
        return new UndoActionRecord(
            Id: GetInt64(reader, "id"),
            ActionId: GetInt64(reader, "action_id"),
            TargetFileRecordId: GetInt64(reader, "target_file_record_id"),
            UndoType: GetString(reader, "undo_type"),
            OriginalPath: GetString(reader, "original_path"),
            ResultingPath: GetString(reader, "resulting_path"),
            FileIdentityJson: GetString(reader, "file_identity_json"),
            Status: GetString(reader, "status"),
            CreatedAt: GetDateTimeOffset(reader, "created_at"),
            PerformedAt: GetNullableDateTimeOffset(reader, "performed_at"));
    }

    private static TranscriptionJobRecord ReadTranscriptionJob(SqliteDataReader reader)
    {
        return new TranscriptionJobRecord(
            Id: GetInt64(reader, "id"),
            Provider: GetString(reader, "provider"),
            AudioPath: GetNullableString(reader, "audio_path"),
            DurationMs: GetNullableInt32(reader, "duration_ms"),
            TranscriptText: GetNullableString(reader, "transcript_text"),
            Status: GetString(reader, "status"),
            ErrorMessage: GetNullableString(reader, "error_message"),
            CreatedAt: GetDateTimeOffset(reader, "created_at"),
            CompletedAt: GetNullableDateTimeOffset(reader, "completed_at"),
            ProviderMetadataJson: GetNullableString(reader, "provider_metadata_json"));
    }

    private static VoiceCommandRecord ReadVoiceCommand(SqliteDataReader reader)
    {
        return new VoiceCommandRecord(
            Id: GetInt64(reader, "id"),
            RawText: GetString(reader, "raw_text"),
            ParsedIntentJson: GetNullableString(reader, "parsed_intent_json"),
            Status: GetString(reader, "status"),
            ResultCount: GetInt32(reader, "result_count"),
            ExecutedAction: GetNullableString(reader, "executed_action"),
            CreatedAt: GetDateTimeOffset(reader, "created_at"),
            DetailsJson: GetNullableString(reader, "details_json"));
    }

    private static SearchQueryRecord ReadSearchQuery(SqliteDataReader reader)
    {
        return new SearchQueryRecord(
            Id: GetInt64(reader, "id"),
            QueryText: GetString(reader, "query_text"),
            ParsedIntentJson: GetNullableString(reader, "parsed_intent_json"),
            Provider: GetString(reader, "provider"),
            ResultCount: GetInt32(reader, "result_count"),
            CreatedAt: GetDateTimeOffset(reader, "created_at"));
    }

    private static void Add(SqliteCommand command, string name, object? value)
    {
        command.Parameters.AddWithValue(name, value ?? DBNull.Value);
    }

    private static long RequiredId(long? id)
    {
        return id ?? throw new InvalidOperationException("The record must have an id for this operation.");
    }

    private static int ToDb(bool value)
    {
        return value ? 1 : 0;
    }

    private static string ToDb(DateTimeOffset value)
    {
        return value.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture);
    }

    private static object? ToDb(DateTimeOffset? value)
    {
        return value.HasValue ? ToDb(value.Value) : null;
    }

    private static string GetString(SqliteDataReader reader, string name)
    {
        return reader.GetString(reader.GetOrdinal(name));
    }

    private static string? GetNullableString(SqliteDataReader reader, string name)
    {
        var ordinal = reader.GetOrdinal(name);
        return reader.IsDBNull(ordinal) ? null : reader.GetString(ordinal);
    }

    private static long GetInt64(SqliteDataReader reader, string name)
    {
        return reader.GetInt64(reader.GetOrdinal(name));
    }

    private static long? GetNullableInt64(SqliteDataReader reader, string name)
    {
        var ordinal = reader.GetOrdinal(name);
        return reader.IsDBNull(ordinal) ? null : reader.GetInt64(ordinal);
    }

    private static int GetInt32(SqliteDataReader reader, string name)
    {
        return reader.GetInt32(reader.GetOrdinal(name));
    }

    private static int? GetNullableInt32(SqliteDataReader reader, string name)
    {
        var ordinal = reader.GetOrdinal(name);
        return reader.IsDBNull(ordinal) ? null : reader.GetInt32(ordinal);
    }

    private static double GetDouble(SqliteDataReader reader, string name)
    {
        return reader.GetDouble(reader.GetOrdinal(name));
    }

    private static double? GetNullableDouble(SqliteDataReader reader, string name)
    {
        var ordinal = reader.GetOrdinal(name);
        return reader.IsDBNull(ordinal) ? null : reader.GetDouble(ordinal);
    }

    private static bool GetBoolean(SqliteDataReader reader, string name)
    {
        return reader.GetInt64(reader.GetOrdinal(name)) != 0;
    }

    private static DateTimeOffset GetDateTimeOffset(SqliteDataReader reader, string name)
    {
        return FromDb(GetString(reader, name));
    }

    private static DateTimeOffset? GetNullableDateTimeOffset(SqliteDataReader reader, string name)
    {
        var value = GetNullableString(reader, name);
        return value is null ? null : FromDb(value);
    }

    private static DateTimeOffset FromDb(string value)
    {
        return DateTimeOffset.Parse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
    }
}
