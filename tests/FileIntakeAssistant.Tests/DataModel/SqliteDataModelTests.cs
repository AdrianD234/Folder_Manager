using FileIntakeAssistant.Core.Models;
using FileIntakeAssistant.Core.Persistence;
using FileIntakeAssistant.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;

namespace FileIntakeAssistant.Tests.DataModel;

public sealed class SqliteDataModelTests : IAsyncLifetime
{
    private static readonly DateTimeOffset FixedNow = new(2026, 5, 23, 3, 0, 0, TimeSpan.Zero);

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
    public async Task DataModel_MigrationCreatesDocumentedTablesAndIndexes()
    {
        Assert.StartsWith(
            Path.GetFullPath(Path.GetTempPath()),
            Path.GetFullPath(DatabasePath),
            StringComparison.OrdinalIgnoreCase);

        var migrationRunner = new SqliteMigrationRunner();

        await migrationRunner.ApplyMigrationsAsync(DatabasePath);
        await migrationRunner.ApplyMigrationsAsync(DatabasePath);

        var tables = await ReadSqliteObjectNamesAsync("table");
        var indexes = await ReadSqliteObjectNamesAsync("index");

        var expectedTables = new[]
        {
            "schema_migrations",
            "app_settings",
            "intake_folders",
            "file_records",
            "file_events",
            "event_batches",
            "folder_records",
            "metadata_entries",
            "actions",
            "undo_actions",
            "transcription_jobs",
            "voice_commands",
            "search_queries"
        };

        foreach (var expectedTable in expectedTables)
        {
            Assert.Contains(expectedTable, tables);
        }

        var expectedIndexes = new[]
        {
            "ux_intake_folders_path",
            "idx_intake_folders_enabled",
            "idx_file_records_current_path",
            "idx_file_records_extension",
            "idx_file_records_first_seen_at",
            "idx_file_records_stable_at",
            "idx_file_records_triage_category",
            "idx_file_records_is_meaningful",
            "idx_file_records_sha256_not_null",
            "idx_file_events_raw_path",
            "idx_file_events_observed_at",
            "idx_file_events_triage_category",
            "idx_file_events_batch_id",
            "idx_file_events_ignored",
            "idx_event_batches_root_path",
            "idx_event_batches_started_at",
            "idx_event_batches_batch_type",
            "idx_event_batches_decision",
            "ux_folder_records_path",
            "idx_folder_records_folder_type",
            "idx_metadata_entries_file_record_id",
            "idx_metadata_entries_folder_record_id",
            "idx_metadata_entries_relevance",
            "idx_metadata_entries_project",
            "idx_metadata_entries_topic",
            "idx_metadata_entries_created_at",
            "idx_actions_action_type",
            "idx_actions_target_file_record_id",
            "idx_actions_status",
            "idx_actions_created_at",
            "idx_undo_actions_action_id",
            "idx_undo_actions_target_file_record_id",
            "idx_undo_actions_status",
            "idx_transcription_jobs_provider",
            "idx_transcription_jobs_status",
            "idx_transcription_jobs_created_at",
            "idx_voice_commands_status",
            "idx_voice_commands_created_at",
            "idx_search_queries_provider",
            "idx_search_queries_created_at"
        };

        foreach (var expectedIndex in expectedIndexes)
        {
            Assert.Contains(expectedIndex, indexes);
        }

        Assert.Equal(new[] { "001_initial_schema" }, migrationRunner.MigrationIds);
        Assert.Equal(new[] { "001_initial_schema" }, await ReadAppliedMigrationIdsAsync());
    }

    [Fact]
    public async Task DataModel_StoreInsertsAndQueriesEveryCoreTable()
    {
        var store = await CreateStoreAsync();
        var ids = await InsertCompleteRecordGraphAsync(store);

        var setting = await store.GetAppSettingAsync("intake.defaultFolder");
        var intakeFolder = await store.GetIntakeFolderAsync(ids.IntakeFolderId);
        var enabledIntakeFolders = await store.ListIntakeFoldersAsync(enabledOnly: true);
        var eventBatch = await store.GetEventBatchAsync(ids.EventBatchId);
        var fileRecord = await store.GetFileRecordAsync(ids.FileRecordId);
        var folderRecord = await store.GetFolderRecordAsync(ids.FolderRecordId);
        var fileEvent = await store.GetFileEventAsync(ids.FileEventId);
        var metadataEntry = await store.GetMetadataEntryAsync(ids.MetadataEntryId);
        var action = await store.GetActionAsync(ids.ActionId);
        var undoAction = await store.GetUndoActionAsync(ids.UndoActionId);
        var transcriptionJob = await store.GetTranscriptionJobAsync(ids.TranscriptionJobId);
        var voiceCommand = await store.GetVoiceCommandAsync(ids.VoiceCommandId);
        var searchQuery = await store.GetSearchQueryAsync(ids.SearchQueryId);

        Assert.NotNull(setting);
        Assert.Equal("""{"path":"temp-downloads"}""", setting.ValueJson);

        Assert.NotNull(intakeFolder);
        Assert.Equal("Downloads Test", intakeFolder.DisplayName);
        Assert.True(intakeFolder.Enabled);
        Assert.Single(enabledIntakeFolders);
        Assert.Equal(ids.IntakeFolderId, enabledIntakeFolders[0].Id);

        Assert.NotNull(eventBatch);
        Assert.Equal("ArchiveExtractionBatch", eventBatch.BatchType);
        Assert.Equal(84, eventBatch.FileCount);

        Assert.NotNull(fileRecord);
        Assert.Equal(".xlsx", fileRecord.Extension);
        Assert.True(fileRecord.IsMeaningful);
        Assert.Equal(0.93, fileRecord.TriageConfidence);
        Assert.Equal(fileRecord.Id, (await store.GetFileRecordByCurrentPathAsync(fileRecord.CurrentPath))!.Id);

        Assert.NotNull(folderRecord);
        Assert.Equal("Repository", folderRecord.FolderType);

        Assert.NotNull(fileEvent);
        Assert.Equal("Created", fileEvent.EventType);
        Assert.True(fileEvent.Ignored);

        Assert.NotNull(metadataEntry);
        Assert.Equal("Revenue Model", metadataEntry.Project);
        Assert.Equal(ids.FileRecordId, metadataEntry.FileRecordId);

        Assert.NotNull(action);
        Assert.Equal("Move", action.ActionType);
        Assert.Equal("Pending", action.Status);

        Assert.NotNull(undoAction);
        Assert.Equal(ids.ActionId, undoAction.ActionId);
        Assert.Equal("MoveBack", undoAction.UndoType);

        Assert.NotNull(transcriptionJob);
        Assert.Equal("ManualText", transcriptionJob.Provider);
        Assert.Equal("Completed", transcriptionJob.Status);

        Assert.NotNull(voiceCommand);
        Assert.Equal("open the last five Excel files I saved", voiceCommand.RawText);
        Assert.Equal(5, voiceCommand.ResultCount);

        Assert.NotNull(searchQuery);
        Assert.Equal("SQLite", searchQuery.Provider);
        Assert.Equal(5, searchQuery.ResultCount);
    }

    [Fact]
    public async Task DataModel_StoreUpdatesMutableRows()
    {
        var store = await CreateStoreAsync();
        var ids = await InsertCompleteRecordGraphAsync(store);

        await store.UpsertAppSettingAsync(new AppSetting("intake.defaultFolder", """{"path":"changed"}""", FixedNow.AddMinutes(1)));

        var intakeFolder = await store.GetIntakeFolderAsync(ids.IntakeFolderId);
        Assert.NotNull(intakeFolder);
        await store.UpdateIntakeFolderAsync(intakeFolder with
        {
            DisplayName = "Disabled Test Folder",
            Enabled = false,
            UpdatedAt = FixedNow.AddMinutes(2)
        });

        var metadataEntry = await store.GetMetadataEntryAsync(ids.MetadataEntryId);
        Assert.NotNull(metadataEntry);
        await store.UpdateMetadataEntryAsync(metadataEntry with
        {
            Project = "Updated Project",
            UpdatedAt = FixedNow.AddMinutes(3)
        });

        var fileRecord = await store.GetFileRecordAsync(ids.FileRecordId);
        Assert.NotNull(fileRecord);
        var updatedPath = Path.Combine(_testRoot, "filed", "Budget Filed.xlsx");
        await store.UpdateFileRecordAsync(fileRecord with
        {
            CurrentFilename = "Budget Filed.xlsx",
            CurrentPath = updatedPath,
            Status = "Filed",
            LastSeenAt = FixedNow.AddMinutes(3)
        });

        var action = await store.GetActionAsync(ids.ActionId);
        Assert.NotNull(action);
        await store.UpdateActionAsync(action with
        {
            Status = "Completed",
            CompletedAt = FixedNow.AddMinutes(4)
        });

        var undoAction = await store.GetUndoActionAsync(ids.UndoActionId);
        Assert.NotNull(undoAction);
        await store.UpdateUndoActionAsync(undoAction with
        {
            Status = "Performed",
            PerformedAt = FixedNow.AddMinutes(5)
        });

        var transcriptionJob = await store.GetTranscriptionJobAsync(ids.TranscriptionJobId);
        Assert.NotNull(transcriptionJob);
        await store.UpdateTranscriptionJobAsync(transcriptionJob with
        {
            TranscriptText = "Updated transcript.",
            Status = "Completed",
            CompletedAt = FixedNow.AddMinutes(6)
        });

        Assert.Equal("""{"path":"changed"}""", (await store.GetAppSettingAsync("intake.defaultFolder"))!.ValueJson);
        Assert.False((await store.GetIntakeFolderAsync(ids.IntakeFolderId))!.Enabled);
        Assert.Equal(updatedPath, (await store.GetFileRecordAsync(ids.FileRecordId))!.CurrentPath);
        Assert.Equal("Updated Project", (await store.GetMetadataEntryAsync(ids.MetadataEntryId))!.Project);
        Assert.Equal("Completed", (await store.GetActionAsync(ids.ActionId))!.Status);
        Assert.Equal("Performed", (await store.GetUndoActionAsync(ids.UndoActionId))!.Status);
        Assert.Equal("Updated transcript.", (await store.GetTranscriptionJobAsync(ids.TranscriptionJobId))!.TranscriptText);
    }

    [Fact]
    public async Task DataModel_ConstraintsRejectInvalidRecords()
    {
        var store = await CreateStoreAsync();

        var invalidFileRecord = SampleFileRecord(
            sourceIntakeFolderId: null,
            triageConfidence: 1.5);

        await Assert.ThrowsAsync<SqliteException>(() => store.AddFileRecordAsync(invalidFileRecord));

        var invalidMetadata = SampleMetadataEntry(
            fileRecordId: null,
            folderRecordId: null);

        await Assert.ThrowsAsync<SqliteException>(() => store.AddMetadataEntryAsync(invalidMetadata));

        var invalidBatch = new EventBatch(
            Id: null,
            RootPath: Path.Combine(_testRoot, "downloads"),
            BatchType: "ArchiveExtractionBatch",
            StartedAt: FixedNow,
            EndedAt: null,
            FileCount: -1,
            Decision: "SuppressIndividualPrompts",
            DetailsJson: null);

        await Assert.ThrowsAsync<SqliteException>(() => store.AddEventBatchAsync(invalidBatch));
    }

    private async Task<IFileIntakeStore> CreateStoreAsync()
    {
        var migrationRunner = new SqliteMigrationRunner();
        await migrationRunner.ApplyMigrationsAsync(DatabasePath);
        return new SqliteFileIntakeStore(DatabasePath);
    }

    private async Task<RecordIds> InsertCompleteRecordGraphAsync(IFileIntakeStore store)
    {
        await store.UpsertAppSettingAsync(new AppSetting(
            Key: "intake.defaultFolder",
            ValueJson: """{"path":"temp-downloads"}""",
            UpdatedAt: FixedNow));

        var intakeFolderId = await store.AddIntakeFolderAsync(new IntakeFolder(
            Id: null,
            Path: Path.Combine(_testRoot, "downloads"),
            DisplayName: "Downloads Test",
            Enabled: true,
            FolderType: "Downloads",
            Recursive: false,
            CreatedAt: FixedNow,
            UpdatedAt: FixedNow));

        var eventBatchId = await store.AddEventBatchAsync(new EventBatch(
            Id: null,
            RootPath: Path.Combine(_testRoot, "downloads", "Extracted"),
            BatchType: "ArchiveExtractionBatch",
            StartedAt: FixedNow,
            EndedAt: FixedNow.AddSeconds(60),
            FileCount: 84,
            Decision: "SuppressIndividualPrompts",
            DetailsJson: """{"threshold":50}"""));

        var fileRecordId = await store.AddFileRecordAsync(SampleFileRecord(
            sourceIntakeFolderId: intakeFolderId,
            triageConfidence: 0.93));

        var folderRecordId = await store.AddFolderRecordAsync(new FolderRecord(
            Id: null,
            Path: Path.Combine(_testRoot, "downloads", "Repo"),
            DisplayName: "Repo",
            FolderType: "Repository",
            SourceIntakeFolderId: intakeFolderId,
            CreatedAt: FixedNow,
            UpdatedAt: FixedNow,
            NotesJson: """{"context":"folder-level"}"""));

        var fileEventId = await store.AddFileEventAsync(new FileEventRecord(
            Id: null,
            FileRecordId: fileRecordId,
            EventType: "Created",
            RawPath: Path.Combine(_testRoot, "downloads", "Budget.xlsx"),
            OldPath: null,
            NewPath: null,
            ObservedAt: FixedNow,
            NormalizedAt: FixedNow.AddMilliseconds(10),
            TriageCategory: "ArchiveExtractionBatch",
            TriageReason: "Suppressed as part of extraction batch.",
            BatchId: eventBatchId,
            Ignored: true,
            DetailsJson: """{"source":"test"}"""));

        var metadataEntryId = await store.AddMetadataEntryAsync(SampleMetadataEntry(
            fileRecordId,
            folderRecordId: null));

        var actionId = await store.AddActionAsync(new FileActionRecord(
            Id: null,
            ActionType: "Move",
            TargetFileRecordId: fileRecordId,
            OldPath: Path.Combine(_testRoot, "downloads", "Budget.xlsx"),
            NewPath: Path.Combine(_testRoot, "filed", "Budget.xlsx"),
            Status: "Pending",
            CreatedAt: FixedNow,
            CompletedAt: null,
            DetailsJson: """{"confirmed":true}"""));

        var undoActionId = await store.AddUndoActionAsync(new UndoActionRecord(
            Id: null,
            ActionId: actionId,
            TargetFileRecordId: fileRecordId,
            UndoType: "MoveBack",
            OriginalPath: Path.Combine(_testRoot, "downloads", "Budget.xlsx"),
            ResultingPath: Path.Combine(_testRoot, "filed", "Budget.xlsx"),
            FileIdentityJson: """{"size":24576}""",
            Status: "Pending",
            CreatedAt: FixedNow,
            PerformedAt: null));

        var transcriptionJobId = await store.AddTranscriptionJobAsync(new TranscriptionJobRecord(
            Id: null,
            Provider: "ManualText",
            AudioPath: null,
            DurationMs: null,
            TranscriptText: "Downloaded for the revenue model.",
            Status: "Completed",
            ErrorMessage: null,
            CreatedAt: FixedNow,
            CompletedAt: FixedNow.AddSeconds(2),
            ProviderMetadataJson: """{"mode":"manual"}"""));

        var voiceCommandId = await store.AddVoiceCommandAsync(new VoiceCommandRecord(
            Id: null,
            RawText: "open the last five Excel files I saved",
            ParsedIntentJson: """{"action":"open","count":5,"type":"excel"}""",
            Status: "Parsed",
            ResultCount: 5,
            ExecutedAction: "ShowConfirmation",
            CreatedAt: FixedNow,
            DetailsJson: """{"requiresConfirmation":true}"""));

        var searchQueryId = await store.AddSearchQueryAsync(new SearchQueryRecord(
            Id: null,
            QueryText: "open the last five Excel files I saved",
            ParsedIntentJson: """{"action":"open","count":5,"type":"excel"}""",
            Provider: "SQLite",
            ResultCount: 5,
            CreatedAt: FixedNow));

        return new RecordIds(
            intakeFolderId,
            fileRecordId,
            fileEventId,
            eventBatchId,
            folderRecordId,
            metadataEntryId,
            actionId,
            undoActionId,
            transcriptionJobId,
            voiceCommandId,
            searchQueryId);
    }

    private FileRecord SampleFileRecord(long? sourceIntakeFolderId, double triageConfidence)
    {
        return new FileRecord(
            Id: null,
            Sha256: "0123456789abcdef",
            OriginalFilename: "Budget.xlsx",
            CurrentFilename: "Budget.xlsx",
            OriginalPath: Path.Combine(_testRoot, "downloads", "Budget.xlsx"),
            CurrentPath: Path.Combine(_testRoot, "downloads", "Budget.xlsx"),
            Extension: ".xlsx",
            SizeBytes: 24_576,
            MimeType: "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            SourceIntakeFolderId: sourceIntakeFolderId,
            FirstSeenAt: FixedNow,
            LastSeenAt: FixedNow,
            StableAt: FixedNow.AddSeconds(2),
            Status: "Candidate",
            TriageCategory: "MeaningfulOneOff",
            TriageConfidence: triageConfidence,
            IsMeaningful: true,
            NotesJson: """{"source":"test"}""");
    }

    private static MetadataEntry SampleMetadataEntry(long? fileRecordId, long? folderRecordId)
    {
        return new MetadataEntry(
            Id: null,
            FileRecordId: fileRecordId,
            FolderRecordId: folderRecordId,
            UserNote: "Downloaded for the transport revenue model assumptions.",
            TranscriptText: "This is for the Revenue Model project.",
            Relevance: "high",
            Project: "Revenue Model",
            Topic: "Transport assumptions",
            TagsJson: """["finance","transport","model-inputs"]""",
            SourceUrl: "https://example.test/budget",
            ReferrerUrl: "https://example.test",
            AgentSummary: "Model input file.",
            ClassifierConfidence: 0.87,
            CreatedAt: FixedNow,
            UpdatedAt: FixedNow);
    }

    private async Task<HashSet<string>> ReadSqliteObjectNamesAsync(string objectType)
    {
        await using var connection = await OpenConnectionAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT name FROM sqlite_master WHERE type = $type;";
        command.Parameters.AddWithValue("$type", objectType);

        var names = new HashSet<string>(StringComparer.Ordinal);
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            names.Add(reader.GetString(0));
        }

        return names;
    }

    private async Task<string[]> ReadAppliedMigrationIdsAsync()
    {
        await using var connection = await OpenConnectionAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT migration_id FROM schema_migrations ORDER BY migration_id;";

        var ids = new List<string>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            ids.Add(reader.GetString(0));
        }

        return ids.ToArray();
    }

    private async Task<SqliteConnection> OpenConnectionAsync()
    {
        var connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = DatabasePath,
            Mode = SqliteOpenMode.ReadWrite
        }.ToString();

        var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync();
        return connection;
    }

    private sealed record RecordIds(
        long IntakeFolderId,
        long FileRecordId,
        long FileEventId,
        long EventBatchId,
        long FolderRecordId,
        long MetadataEntryId,
        long ActionId,
        long UndoActionId,
        long TranscriptionJobId,
        long VoiceCommandId,
        long SearchQueryId);
}
