using Microsoft.Data.Sqlite;

namespace FileIntakeAssistant.Infrastructure.Persistence;

public sealed class SqliteMigrationRunner
{
    private static readonly SqliteMigration[] Migrations =
    [
        new(
            "001_initial_schema",
            """
            CREATE TABLE IF NOT EXISTS app_settings (
                key TEXT PRIMARY KEY,
                value_json TEXT NOT NULL,
                updated_at TEXT NOT NULL
            );

            CREATE TABLE IF NOT EXISTS intake_folders (
                id INTEGER PRIMARY KEY,
                path TEXT NOT NULL,
                display_name TEXT NOT NULL,
                enabled INTEGER NOT NULL DEFAULT 1,
                folder_type TEXT NOT NULL,
                recursive INTEGER NOT NULL DEFAULT 0,
                created_at TEXT NOT NULL,
                updated_at TEXT NOT NULL
            );

            CREATE UNIQUE INDEX IF NOT EXISTS ux_intake_folders_path ON intake_folders(path);
            CREATE INDEX IF NOT EXISTS idx_intake_folders_enabled ON intake_folders(enabled);

            CREATE TABLE IF NOT EXISTS file_records (
                id INTEGER PRIMARY KEY,
                sha256 TEXT NULL,
                original_filename TEXT NOT NULL,
                current_filename TEXT NOT NULL,
                original_path TEXT NOT NULL,
                current_path TEXT NOT NULL,
                extension TEXT NOT NULL,
                size_bytes INTEGER NOT NULL CHECK (size_bytes >= 0),
                mime_type TEXT NULL,
                source_intake_folder_id INTEGER NULL REFERENCES intake_folders(id),
                first_seen_at TEXT NOT NULL,
                last_seen_at TEXT NOT NULL,
                stable_at TEXT NULL,
                status TEXT NOT NULL,
                triage_category TEXT NOT NULL,
                triage_confidence REAL NOT NULL CHECK (triage_confidence >= 0 AND triage_confidence <= 1),
                is_meaningful INTEGER NOT NULL DEFAULT 0,
                notes_json TEXT NULL
            );

            CREATE INDEX IF NOT EXISTS idx_file_records_current_path ON file_records(current_path);
            CREATE INDEX IF NOT EXISTS idx_file_records_extension ON file_records(extension);
            CREATE INDEX IF NOT EXISTS idx_file_records_first_seen_at ON file_records(first_seen_at);
            CREATE INDEX IF NOT EXISTS idx_file_records_stable_at ON file_records(stable_at);
            CREATE INDEX IF NOT EXISTS idx_file_records_triage_category ON file_records(triage_category);
            CREATE INDEX IF NOT EXISTS idx_file_records_is_meaningful ON file_records(is_meaningful);
            CREATE INDEX IF NOT EXISTS idx_file_records_sha256_not_null ON file_records(sha256) WHERE sha256 IS NOT NULL;

            CREATE TABLE IF NOT EXISTS event_batches (
                id INTEGER PRIMARY KEY,
                root_path TEXT NOT NULL,
                batch_type TEXT NOT NULL,
                started_at TEXT NOT NULL,
                ended_at TEXT NULL,
                file_count INTEGER NOT NULL CHECK (file_count >= 0),
                decision TEXT NOT NULL,
                details_json TEXT NULL
            );

            CREATE INDEX IF NOT EXISTS idx_event_batches_root_path ON event_batches(root_path);
            CREATE INDEX IF NOT EXISTS idx_event_batches_started_at ON event_batches(started_at);
            CREATE INDEX IF NOT EXISTS idx_event_batches_batch_type ON event_batches(batch_type);
            CREATE INDEX IF NOT EXISTS idx_event_batches_decision ON event_batches(decision);

            CREATE TABLE IF NOT EXISTS file_events (
                id INTEGER PRIMARY KEY,
                file_record_id INTEGER NULL REFERENCES file_records(id),
                event_type TEXT NOT NULL,
                raw_path TEXT NOT NULL,
                old_path TEXT NULL,
                new_path TEXT NULL,
                observed_at TEXT NOT NULL,
                normalized_at TEXT NOT NULL,
                triage_category TEXT NOT NULL,
                triage_reason TEXT NOT NULL,
                batch_id INTEGER NULL REFERENCES event_batches(id),
                ignored INTEGER NOT NULL DEFAULT 0,
                details_json TEXT NULL
            );

            CREATE INDEX IF NOT EXISTS idx_file_events_raw_path ON file_events(raw_path);
            CREATE INDEX IF NOT EXISTS idx_file_events_observed_at ON file_events(observed_at);
            CREATE INDEX IF NOT EXISTS idx_file_events_triage_category ON file_events(triage_category);
            CREATE INDEX IF NOT EXISTS idx_file_events_batch_id ON file_events(batch_id);
            CREATE INDEX IF NOT EXISTS idx_file_events_ignored ON file_events(ignored);

            CREATE TABLE IF NOT EXISTS folder_records (
                id INTEGER PRIMARY KEY,
                path TEXT NOT NULL,
                display_name TEXT NOT NULL,
                folder_type TEXT NOT NULL,
                source_intake_folder_id INTEGER NULL REFERENCES intake_folders(id),
                created_at TEXT NOT NULL,
                updated_at TEXT NOT NULL,
                notes_json TEXT NULL
            );

            CREATE UNIQUE INDEX IF NOT EXISTS ux_folder_records_path ON folder_records(path);
            CREATE INDEX IF NOT EXISTS idx_folder_records_folder_type ON folder_records(folder_type);

            CREATE TABLE IF NOT EXISTS metadata_entries (
                id INTEGER PRIMARY KEY,
                file_record_id INTEGER NULL REFERENCES file_records(id),
                folder_record_id INTEGER NULL REFERENCES folder_records(id),
                user_note TEXT NULL,
                transcript_text TEXT NULL,
                relevance TEXT NULL,
                project TEXT NULL,
                topic TEXT NULL,
                tags_json TEXT NULL,
                source_url TEXT NULL,
                referrer_url TEXT NULL,
                agent_summary TEXT NULL,
                classifier_confidence REAL NULL CHECK (classifier_confidence IS NULL OR (classifier_confidence >= 0 AND classifier_confidence <= 1)),
                created_at TEXT NOT NULL,
                updated_at TEXT NOT NULL,
                CHECK (file_record_id IS NOT NULL OR folder_record_id IS NOT NULL)
            );

            CREATE INDEX IF NOT EXISTS idx_metadata_entries_file_record_id ON metadata_entries(file_record_id);
            CREATE INDEX IF NOT EXISTS idx_metadata_entries_folder_record_id ON metadata_entries(folder_record_id);
            CREATE INDEX IF NOT EXISTS idx_metadata_entries_relevance ON metadata_entries(relevance);
            CREATE INDEX IF NOT EXISTS idx_metadata_entries_project ON metadata_entries(project);
            CREATE INDEX IF NOT EXISTS idx_metadata_entries_topic ON metadata_entries(topic);
            CREATE INDEX IF NOT EXISTS idx_metadata_entries_created_at ON metadata_entries(created_at);

            CREATE TABLE IF NOT EXISTS actions (
                id INTEGER PRIMARY KEY,
                action_type TEXT NOT NULL,
                target_file_record_id INTEGER NULL REFERENCES file_records(id),
                old_path TEXT NULL,
                new_path TEXT NULL,
                status TEXT NOT NULL,
                created_at TEXT NOT NULL,
                completed_at TEXT NULL,
                details_json TEXT NULL
            );

            CREATE INDEX IF NOT EXISTS idx_actions_action_type ON actions(action_type);
            CREATE INDEX IF NOT EXISTS idx_actions_target_file_record_id ON actions(target_file_record_id);
            CREATE INDEX IF NOT EXISTS idx_actions_status ON actions(status);
            CREATE INDEX IF NOT EXISTS idx_actions_created_at ON actions(created_at);

            CREATE TABLE IF NOT EXISTS undo_actions (
                id INTEGER PRIMARY KEY,
                action_id INTEGER NOT NULL REFERENCES actions(id),
                target_file_record_id INTEGER NOT NULL REFERENCES file_records(id),
                undo_type TEXT NOT NULL,
                original_path TEXT NOT NULL,
                resulting_path TEXT NOT NULL,
                file_identity_json TEXT NOT NULL,
                status TEXT NOT NULL,
                created_at TEXT NOT NULL,
                performed_at TEXT NULL
            );

            CREATE INDEX IF NOT EXISTS idx_undo_actions_action_id ON undo_actions(action_id);
            CREATE INDEX IF NOT EXISTS idx_undo_actions_target_file_record_id ON undo_actions(target_file_record_id);
            CREATE INDEX IF NOT EXISTS idx_undo_actions_status ON undo_actions(status);

            CREATE TABLE IF NOT EXISTS transcription_jobs (
                id INTEGER PRIMARY KEY,
                provider TEXT NOT NULL,
                audio_path TEXT NULL,
                duration_ms INTEGER NULL,
                transcript_text TEXT NULL,
                status TEXT NOT NULL,
                error_message TEXT NULL,
                created_at TEXT NOT NULL,
                completed_at TEXT NULL,
                provider_metadata_json TEXT NULL
            );

            CREATE INDEX IF NOT EXISTS idx_transcription_jobs_provider ON transcription_jobs(provider);
            CREATE INDEX IF NOT EXISTS idx_transcription_jobs_status ON transcription_jobs(status);
            CREATE INDEX IF NOT EXISTS idx_transcription_jobs_created_at ON transcription_jobs(created_at);

            CREATE TABLE IF NOT EXISTS voice_commands (
                id INTEGER PRIMARY KEY,
                raw_text TEXT NOT NULL,
                parsed_intent_json TEXT NULL,
                status TEXT NOT NULL,
                result_count INTEGER NOT NULL DEFAULT 0 CHECK (result_count >= 0),
                executed_action TEXT NULL,
                created_at TEXT NOT NULL,
                details_json TEXT NULL
            );

            CREATE INDEX IF NOT EXISTS idx_voice_commands_status ON voice_commands(status);
            CREATE INDEX IF NOT EXISTS idx_voice_commands_created_at ON voice_commands(created_at);

            CREATE TABLE IF NOT EXISTS search_queries (
                id INTEGER PRIMARY KEY,
                query_text TEXT NOT NULL,
                parsed_intent_json TEXT NULL,
                provider TEXT NOT NULL,
                result_count INTEGER NOT NULL DEFAULT 0 CHECK (result_count >= 0),
                created_at TEXT NOT NULL
            );

            CREATE INDEX IF NOT EXISTS idx_search_queries_provider ON search_queries(provider);
            CREATE INDEX IF NOT EXISTS idx_search_queries_created_at ON search_queries(created_at);
            """)
    ];

    public IReadOnlyList<string> MigrationIds => Migrations.Select(migration => migration.Id).ToArray();

    public async Task ApplyMigrationsAsync(string databasePath, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(databasePath);

        var directory = Path.GetDirectoryName(Path.GetFullPath(databasePath));
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await using var connection = await OpenConnectionAsync(databasePath, cancellationToken).ConfigureAwait(false);
        await EnsureMigrationTableAsync(connection, cancellationToken).ConfigureAwait(false);

        foreach (var migration in Migrations)
        {
            if (await IsMigrationAppliedAsync(connection, migration.Id, cancellationToken).ConfigureAwait(false))
            {
                continue;
            }

            await using var transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
            await ExecuteNonQueryAsync(connection, migration.Sql, transaction, cancellationToken).ConfigureAwait(false);
            await RecordMigrationAsync(connection, migration.Id, transaction, cancellationToken).ConfigureAwait(false);
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    private static async Task<SqliteConnection> OpenConnectionAsync(string databasePath, CancellationToken cancellationToken)
    {
        var connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = databasePath,
            Mode = SqliteOpenMode.ReadWriteCreate
        }.ToString();

        var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await ExecuteNonQueryAsync(connection, "PRAGMA foreign_keys = ON;", transaction: null, cancellationToken).ConfigureAwait(false);
        return connection;
    }

    private static Task EnsureMigrationTableAsync(SqliteConnection connection, CancellationToken cancellationToken)
    {
        const string sql = """
            CREATE TABLE IF NOT EXISTS schema_migrations (
                id INTEGER PRIMARY KEY,
                migration_id TEXT NOT NULL UNIQUE,
                applied_at TEXT NOT NULL
            );
            """;

        return ExecuteNonQueryAsync(connection, sql, transaction: null, cancellationToken);
    }

    private static async Task<bool> IsMigrationAppliedAsync(
        SqliteConnection connection,
        string migrationId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT 1 FROM schema_migrations WHERE migration_id = $migration_id LIMIT 1;";
        command.Parameters.AddWithValue("$migration_id", migrationId);

        var result = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        return result is not null;
    }

    private static async Task RecordMigrationAsync(
        SqliteConnection connection,
        string migrationId,
        System.Data.Common.DbTransaction transaction,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = (SqliteTransaction)transaction;
        command.CommandText = """
            INSERT INTO schema_migrations (migration_id, applied_at)
            VALUES ($migration_id, $applied_at);
            """;
        command.Parameters.AddWithValue("$migration_id", migrationId);
        command.Parameters.AddWithValue("$applied_at", DateTimeOffset.UtcNow.ToString("O"));

        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private static async Task ExecuteNonQueryAsync(
        SqliteConnection connection,
        string sql,
        System.Data.Common.DbTransaction? transaction,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = (SqliteTransaction?)transaction;
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private sealed record SqliteMigration(string Id, string Sql);
}
