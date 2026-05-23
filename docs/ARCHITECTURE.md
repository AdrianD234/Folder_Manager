# Architecture Specification

## Architecture Overview
File Intake Assistant uses a layered .NET architecture with strict dependency direction:

```text
FileIntakeAssistant.App
  -> FileIntakeAssistant.Core
  -> FileIntakeAssistant.Infrastructure

FileIntakeAssistant.Infrastructure
  -> FileIntakeAssistant.Core
```

`App` composes services and owns user interaction. `Core` owns decisions. `Infrastructure` owns persistence, filesystem, logging, providers, and Windows integration. Core must not depend on WPF, SQLite, OpenAI, Everything, or concrete filesystem mutation.

## Project Boundaries
### FileIntakeAssistant.App
- WPF tray application.
- Tray menu, popup windows, command windows, and view models.
- Global hotkey registration.
- User confirmation boundaries.
- Starts workflows and displays results.
- Calls application services through interfaces.
- No direct SQL.
- No direct provider calls except through configured services.
- No business decisions beyond UI coordination.

### FileIntakeAssistant.Core
- Domain models and value objects.
- File event triage rules.
- File stability decisions.
- Batch detection decisions.
- Folder/repo context rules.
- Search intent parser.
- Voice command parser.
- Metadata extraction from transcript for deterministic/manual fields.
- Suggested folder/name planner.
- Safety validation for file operations.
- No direct SQLite.
- No direct filesystem mutation.
- No direct OpenAI or Everything calls.
- No WPF references.

### FileIntakeAssistant.Infrastructure
- SQLite database connection, migrations, and repositories.
- Filesystem adapter and safe file operation executor.
- Logging adapter.
- Configuration storage.
- Windows known-folder lookup.
- File watcher implementation.
- Microphone/audio capture implementation.
- Transcription providers.
- Search providers.
- Everything CLI adapter placeholder.
- OpenAI provider implementation or stub behind interfaces.

## Provider Interfaces
Initial provider boundaries:

- `ITranscriptionProvider`
  - Input: audio file, options, cancellation token.
  - Output: transcript text, optional confidence, provider metadata, error state.
  - Implementations: manual/fake for tests, local placeholder, optional OpenAI.

- `IMetadataParser`
  - Input: transcript or typed note.
  - Output: relevance, project, topic, tags, source URL, summary, confidence.
  - Implementations: deterministic parser first, optional LLM parser later.

- `IFileSearchProvider`
  - Input: parsed search intent.
  - Output: ranked file results.
  - Implementations: SQLite first, optional Everything CLI later.

- `IFileSystem`
  - Input/output abstraction for testable file checks and operations.
  - Real implementation in Infrastructure; fake or temp-directory implementation in tests.

- `IClock`
  - Testable time source for triage, stability, debounce, and relative date parsing.

## Database Source Of Truth
SQLite under `%LOCALAPPDATA%\File Intake Assistant\data\file-intake.db` is the source of truth for:

- Intake folder configuration.
- File records and event history.
- Metadata entries.
- Action logs.
- Undo records.
- Transcription jobs.
- Voice commands.
- Search queries.
- App settings.

User files must not contain private metadata. Sidecars may not be used as source of truth.

## File Event Triage Engine
The triage engine receives normalized filesystem events and classifies them before user prompting.

Primary outputs:

- `MeaningfulOneOff`
- `TemporaryOrPartial`
- `DevelopmentNoise`
- `BuildOrCompilerNoise`
- `PackageInstallNoise`
- `ArchiveExtractionBatch`
- `OneDriveSyncBurst`
- `InstallerOrUnpackerBurst`
- `OwnOperation`
- `SystemOrAppDataNoise`
- `NeedsMoreObservation`
- `UnknownSafeToIgnore`

The triage engine must produce a reason, confidence, and processing state. Low-confidence events should be logged and either delayed or grouped, not immediately prompted.

## File Stability Engine
The stability engine prevents partial files from being processed.

Checks include:

- File exists.
- File is not a known partial/temp extension.
- Size is nonzero unless the file type can legitimately be zero length.
- Size and last-write time remain stable across a debounce window.
- File is not locked for exclusive write by another process.
- The same path is not part of a current batch suppression window.
- Optional hash-on-stability is safe for file size.

Large files may defer hashing and use path, size, timestamps, and event history until hashing is safe.

## Batch Detection Engine
Batch detection groups bursts under the same root before prompting.

Default thresholds:

- More than 10 files in 10 seconds under the same root: possible batch.
- More than 50 files in 60 seconds under the same root: suppress individual prompts.
- More than 200 files in 5 minutes: batch review only.

Batch types include archive extraction, OneDrive sync, installer/unpacker, package install, build output, and unknown burst. Batch decisions must be logged.

## Voice Capture And Transcription Pipeline
1. App opens microphone popup or command window.
2. User records audio or chooses manual text.
3. Audio is written to `%LOCALAPPDATA%\File Intake Assistant\temp-audio\`.
4. Configured `ITranscriptionProvider` runs.
5. Transcript is shown for review.
6. User can correct transcript.
7. Metadata or search parsing proceeds from reviewed text.
8. Audio is deleted by default after successful transcription.
9. Transcription job and provider call are logged without secrets.

## Metadata Parsing Pipeline
1. Input is typed note or reviewed transcript.
2. Deterministic parser extracts obvious fields first.
3. Optional LLM parser may enrich fields only if configured.
4. User reviews structured metadata.
5. Metadata is saved to SQLite.
6. Parser confidence is stored.

No parser may trigger file moves or renames directly.

## Search And Retrieval Pipeline
1. Command text or transcript is parsed into a search intent.
2. SQLite search provider runs first.
3. Optional Everything provider may enrich file discovery if configured.
4. Results are ranked by recency, file type, relevance, metadata match, project/topic match, and action history.
5. Ambiguous results are displayed.
6. Bulk open requires confirmation.
7. File/folder open actions are logged.

## File Operation And Undo Pipeline
1. User reviews suggested move/rename.
2. App validates destination, filename, path length, extension preservation, and conflict handling.
3. User confirms.
4. Action row is created with pending status.
5. Filesystem operation is executed by the safe operation service.
6. File record is updated.
7. Undo action is recorded with file identity.
8. Action row is completed or failed.
9. Watcher suppresses own-operation events.

Undo verifies identity before moving a file back. If the original path is occupied, undo fails safely and asks the user through the UI.

## Logging And Audit Pipeline
Use structured local logs under `%LOCALAPPDATA%\File Intake Assistant\logs\`.

Log:

- User-visible actions.
- Skipped actions.
- Failed actions.
- Provider calls.
- File watcher starts/stops.
- Triage decisions.
- Batch decisions.
- File operations.
- Undo attempts.

Never log API keys, full secrets, or unnecessary audio content. Logs may include paths because path-level auditability is needed, but this must be documented as local private data.

## Configuration Model
Config lives under `%LOCALAPPDATA%\File Intake Assistant\config\settings.json` and/or SQLite `app_settings`.

Configuration includes:

- Intake folders.
- Recursive watch settings.
- Hotkeys.
- Provider enablement.
- Provider model names.
- API key source reference.
- Audio retention setting.
- Batch thresholds.
- Hashing thresholds.
- Filename planner preferences.

Secrets should come from environment variables or Windows credential storage when implemented. Do not store secrets in repo files.

## Error Handling Model
- Fail closed for file operations.
- Fail silent-to-log for noise events.
- Fail visible for user-initiated actions.
- Provider failures fall back to manual text entry.
- Database failures show a clear local error and avoid file mutation.
- Watcher failures disable the affected folder and log the reason.
- Ambiguous retrieval never opens files blindly.

## Dependency Direction Rules
- App may reference Core and Infrastructure.
- Infrastructure may reference Core.
- Core must not reference App or Infrastructure.
- Tests may reference all projects.
- Provider implementations live in Infrastructure.
- Interfaces used by Core decisions should live in Core unless they are purely technical infrastructure boundaries.
