# Data Model

## Database Location
Production database:

```text
%LOCALAPPDATA%\File Intake Assistant\data\file-intake.db
```

Test databases:

```text
<temporary test root>\File Intake Assistant\data\file-intake-test.db
```

Tests must never use the production path.

## Migration Strategy
- Use explicit numbered migrations.
- Record applied migrations in a `schema_migrations` table.
- Migrations must be idempotent when safely re-run.
- Tests must create a new temp database and apply all migrations.
- Destructive migrations require a decision note and backup/export path.

## Tables
### schema_migrations
Tracks applied schema migrations.

Columns:

- `id` integer primary key.
- `migration_id` text not null unique.
- `applied_at` text not null.

### app_settings
Stores non-secret app settings.

Columns:

- `key` text primary key.
- `value_json` text not null.
- `updated_at` text not null.

Notes:

- Do not store plaintext API keys here if avoidable.
- Store secret source references rather than secret values.

### intake_folders
Configured watched folders.

Columns:

- `id` integer primary key.
- `path` text not null unique.
- `display_name` text not null.
- `enabled` integer not null default 1.
- `folder_type` text not null.
- `recursive` integer not null default 0.
- `created_at` text not null.
- `updated_at` text not null.

Indexes:

- Unique index on `path`.
- Index on `enabled`.

### file_records
One row per meaningful file or manually captured file.

Columns:

- `id` integer primary key.
- `sha256` text nullable.
- `original_filename` text not null.
- `current_filename` text not null.
- `original_path` text not null.
- `current_path` text not null.
- `extension` text not null.
- `size_bytes` integer not null.
- `mime_type` text nullable.
- `source_intake_folder_id` integer nullable references `intake_folders(id)`.
- `first_seen_at` text not null.
- `last_seen_at` text not null.
- `stable_at` text nullable.
- `status` text not null.
- `triage_category` text not null.
- `triage_confidence` real not null.
- `is_meaningful` integer not null default 0.
- `notes_json` text nullable.

Indexes:

- Index on `current_path`.
- Index on `extension`.
- Index on `first_seen_at`.
- Index on `stable_at`.
- Index on `triage_category`.
- Index on `is_meaningful`.
- Index on `sha256` where not null.

Constraints:

- `size_bytes >= 0`.
- `triage_confidence` between 0 and 1.

### file_events
Normalized watcher or manual file events.

Columns:

- `id` integer primary key.
- `file_record_id` integer nullable references `file_records(id)`.
- `event_type` text not null.
- `raw_path` text not null.
- `old_path` text nullable.
- `new_path` text nullable.
- `observed_at` text not null.
- `normalized_at` text not null.
- `triage_category` text not null.
- `triage_reason` text not null.
- `batch_id` integer nullable references `event_batches(id)`.
- `ignored` integer not null default 0.
- `details_json` text nullable.

Indexes:

- Index on `raw_path`.
- Index on `observed_at`.
- Index on `triage_category`.
- Index on `batch_id`.
- Index on `ignored`.

### event_batches
Groups bursts and suppresses prompt floods.

Columns:

- `id` integer primary key.
- `root_path` text not null.
- `batch_type` text not null.
- `started_at` text not null.
- `ended_at` text nullable.
- `file_count` integer not null.
- `decision` text not null.
- `details_json` text nullable.

Indexes:

- Index on `root_path`.
- Index on `started_at`.
- Index on `batch_type`.
- Index on `decision`.

Constraints:

- `file_count >= 0`.

### folder_records
Optional folder-level context for repos, extracted folders, project folders, and batch roots.

Columns:

- `id` integer primary key.
- `path` text not null unique.
- `display_name` text not null.
- `folder_type` text not null.
- `source_intake_folder_id` integer nullable references `intake_folders(id)`.
- `created_at` text not null.
- `updated_at` text not null.
- `notes_json` text nullable.

Indexes:

- Unique index on `path`.
- Index on `folder_type`.

### metadata_entries
Private metadata attached to files or folders.

Columns:

- `id` integer primary key.
- `file_record_id` integer nullable references `file_records(id)`.
- `folder_record_id` integer nullable references `folder_records(id)`.
- `user_note` text nullable.
- `transcript_text` text nullable.
- `relevance` text nullable.
- `project` text nullable.
- `topic` text nullable.
- `tags_json` text nullable.
- `source_url` text nullable.
- `referrer_url` text nullable.
- `agent_summary` text nullable.
- `classifier_confidence` real nullable.
- `created_at` text not null.
- `updated_at` text not null.

Indexes:

- Index on `file_record_id`.
- Index on `folder_record_id`.
- Index on `relevance`.
- Index on `project`.
- Index on `topic`.
- Index on `created_at`.

Constraints:

- At least one of `file_record_id` or `folder_record_id` must be present.
- `classifier_confidence` is null or between 0 and 1.

### actions
Audit log for user-visible and filesystem actions.

Columns:

- `id` integer primary key.
- `action_type` text not null.
- `target_file_record_id` integer nullable references `file_records(id)`.
- `old_path` text nullable.
- `new_path` text nullable.
- `status` text not null.
- `created_at` text not null.
- `completed_at` text nullable.
- `details_json` text nullable.

Indexes:

- Index on `action_type`.
- Index on `target_file_record_id`.
- Index on `status`.
- Index on `created_at`.

### undo_actions
Reversible file operation records.

Columns:

- `id` integer primary key.
- `action_id` integer not null references `actions(id)`.
- `target_file_record_id` integer not null references `file_records(id)`.
- `undo_type` text not null.
- `original_path` text not null.
- `resulting_path` text not null.
- `file_identity_json` text not null.
- `status` text not null.
- `created_at` text not null.
- `performed_at` text nullable.

Indexes:

- Index on `action_id`.
- Index on `target_file_record_id`.
- Index on `status`.

### transcription_jobs
Audio transcription lifecycle records.

Columns:

- `id` integer primary key.
- `provider` text not null.
- `audio_path` text nullable.
- `duration_ms` integer nullable.
- `transcript_text` text nullable.
- `status` text not null.
- `error_message` text nullable.
- `created_at` text not null.
- `completed_at` text nullable.
- `provider_metadata_json` text nullable.

Indexes:

- Index on `provider`.
- Index on `status`.
- Index on `created_at`.

### voice_commands
Voice or typed command records.

Columns:

- `id` integer primary key.
- `raw_text` text not null.
- `parsed_intent_json` text nullable.
- `status` text not null.
- `result_count` integer not null default 0.
- `executed_action` text nullable.
- `created_at` text not null.
- `details_json` text nullable.

Indexes:

- Index on `status`.
- Index on `created_at`.

### search_queries
Search request audit records.

Columns:

- `id` integer primary key.
- `query_text` text not null.
- `parsed_intent_json` text nullable.
- `provider` text not null.
- `result_count` integer not null default 0.
- `created_at` text not null.

Indexes:

- Index on `provider`.
- Index on `created_at`.

## Relationships
- `file_records.source_intake_folder_id` references `intake_folders`.
- `file_events.file_record_id` references `file_records`.
- `file_events.batch_id` references `event_batches`.
- `metadata_entries.file_record_id` references `file_records`.
- `metadata_entries.folder_record_id` references `folder_records`.
- `actions.target_file_record_id` references `file_records`.
- `undo_actions.action_id` references `actions`.
- `undo_actions.target_file_record_id` references `file_records`.

## Example Records
Example `file_records`:

```json
{
  "original_filename": "Budget.xlsx",
  "current_filename": "Budget.xlsx",
  "extension": ".xlsx",
  "size_bytes": 24576,
  "status": "Candidate",
  "triage_category": "MeaningfulOneOff",
  "triage_confidence": 0.93,
  "is_meaningful": true
}
```

Example `metadata_entries`:

```json
{
  "user_note": "Downloaded for the transport revenue model assumptions.",
  "relevance": "high",
  "project": "Revenue model",
  "topic": "Transport assumptions",
  "tags_json": ["finance", "transport", "model-inputs"]
}
```

Example `event_batches`:

```json
{
  "root_path": "C:\\Users\\User\\Downloads\\Extracted Package",
  "batch_type": "ArchiveExtractionBatch",
  "file_count": 84,
  "decision": "SuppressIndividualPrompts"
}
```

## Backup And Export Strategy
V1 planning requirements:

- Support a future explicit export of metadata to JSON or SQLite backup.
- Do not auto-sync the database to cloud storage.
- Before destructive schema migrations, create a local backup.
- Document export/delete tools before implementation.

## Data Retention Policy
Initial defaults:

- Keep file records, metadata, actions, undo records, and search history until user deletes/export-clears metadata.
- Keep local logs with rolling retention configured later.
- Delete temp audio after successful transcription by default.
- Allow a future setting to retain audio for troubleshooting only if explicitly enabled.

## No Private Metadata In Files
Private context must not be written into:

- Document metadata.
- PDF metadata.
- Office custom properties.
- Alternate data streams.
- xattrs.
- Sidecar files used as source of truth.
