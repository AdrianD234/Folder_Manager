# Product Specification

## Problem Statement
Users often download, save, and receive files for a reason that is obvious in the moment and hard to recover later. Filenames, browser history, and folder locations are not enough to answer questions such as why a file was saved, which project it belongs to, whether it was important, or how to find it again by memory.

File Intake Assistant captures meaningful user-level file intake events, lets the user add spoken or typed context, stores that context externally, and later retrieves files through deterministic search and voice commands.

The product must not become a noisy filesystem watcher. Its value depends on ignoring low-value events and asking for context only when a file looks meaningful.

## Goals
- Watch only explicit intake folders selected by the user.
- Default the first watched folder to Downloads when the user accepts it.
- Detect meaningful one-off files after stability and triage.
- Suppress prompts for temp files, partial downloads, archive extractions, OneDrive sync bursts, installers, compilers, package folders, repos, build output, caches, and app-generated operations.
- Capture context through a popup with typed notes, optional microphone recording, relevance, project, topic, tags, and source URL.
- Store all private metadata externally in SQLite under `%LOCALAPPDATA%\File Intake Assistant\`.
- Support deterministic/manual mode without OpenAI, Everything, or other external services.
- Provide optional provider boundaries for OpenAI speech-to-text, OpenAI parsing, local transcription, and Everything search.
- Suggest filing and renaming safely, with preview and explicit confirmation.
- Never delete, overwrite, move, or rename files silently.
- Log actions, skipped actions, failed actions, provider calls, and filesystem operations.
- Support undo for every move or rename the app performs.
- Retrieve files with typed or spoken commands such as "open the last five Excel files I saved."

## Non-Goals
- Do not watch the whole computer.
- Do not build a general endpoint indexing service.
- Do not replace Everything, Windows Search, or full document management systems.
- Do not write tags, notes, transcripts, or private context into user files.
- Do not use sidecar files as the primary metadata source.
- Do not require OpenAI or Everything for v1.
- Do not support destructive file commands.
- Do not auto-classify every file in development folders or extracted archives.
- Do not sync the app database to cloud storage by default.
- Do not implement packaging, installer signing, or enterprise deployment in the first implementation milestones.

## User Workflows
### Download Intake
1. A file appears in a configured intake folder.
2. The watcher waits until the file is stable.
3. The triage engine classifies it.
4. If it is a meaningful one-off file, the app shows an intake popup.
5. The user adds typed or spoken context.
6. The app saves metadata to SQLite.
7. The app may suggest a destination folder or filename.
8. Any move or rename requires explicit confirmation and creates an undo record.

### Manual Intake
1. The user presses a global hotkey or tray command.
2. The app shows recent meaningful files or allows file selection.
3. The user enters metadata manually or records context.
4. The app stores metadata externally in SQLite.
5. Optional safe filing suggestions can be previewed and confirmed.

### Voice Retrieval
1. The user presses a command hotkey.
2. The user types or speaks a retrieval command.
3. The app transcribes if needed, then parses deterministically first.
4. Search runs against SQLite first.
5. Ambiguous or multi-file results are displayed for confirmation.
6. The app opens selected files or containing folders and logs the action.

### Folder Or Repo Context
1. The user marks a folder or repo root as belonging to a project or topic.
2. The app does not tag every child file.
3. Search and display may inherit folder context for child files.
4. Individual files can still be manually tagged when important.

## Supported V1 Scenarios
- Configure Downloads or a temporary test intake folder.
- Detect stable one-off files such as PDFs, Word documents, Excel files, PowerPoint files, images, ZIP files, and text documents.
- Ignore common partial downloads and temp files.
- Suppress prompts for known development and build folders.
- Capture manual metadata.
- Save metadata and action records in SQLite.
- Search by file type, recency, relevance, project, topic, tags, and source URL when present.
- Parse a small deterministic set of retrieval commands.
- Open a single confirmed file.
- Confirm before opening multiple files.
- Confirm before move or rename.
- Undo app-performed move or rename.

## Unsupported V1 Scenarios
- Watching entire drives or user profiles.
- Silent background cloud transcription.
- Silent LLM classification.
- File deletion.
- Overwrite operations.
- Embedded metadata tagging.
- Sidecar metadata as source of truth.
- Required Everything SDK or CLI.
- Required OpenAI API key.
- Enterprise policy management.
- Mobile support.
- Cross-platform support.

## Safety Invariants
- A file operation that could alter path or name must have a confirmation boundary.
- An operation that could lose data must not exist in v1.
- User private context must stay in SQLite and logs must avoid secret values.
- Tests must not mutate real user folders.
- The app must be useful in manual mode without API keys.
- No provider can bypass safe file operation validation.
- Own-operation suppression must prevent the watcher from reprocessing app-generated moves and renames.

## UX Requirements
- The app runs as a tray application.
- Popups must be short, actionable, and dismissible.
- The app must avoid prompt fatigue by suppressing batch/noise events.
- The user must see what file is being processed and why it was shown.
- Move/rename suggestions must be previewed before confirmation.
- Ambiguous retrieval results must be shown rather than blindly acted on.
- Provider/API usage must be visible and configurable.
- Manual text fallback must always be available.

## Data Privacy Requirements
- SQLite is the source of truth.
- Database path: `%LOCALAPPDATA%\File Intake Assistant\data\file-intake.db`.
- Logs path: `%LOCALAPPDATA%\File Intake Assistant\logs\`.
- Temp audio path: `%LOCALAPPDATA%\File Intake Assistant\temp-audio\`.
- Config path: `%LOCALAPPDATA%\File Intake Assistant\config\settings.json`.
- Audio temp files are deleted by default after successful transcription.
- API keys must not be committed or logged.
- External providers are disabled until configured.
- No assumption that the database is cloud-synced.

## Initial Watched Folder Assumptions
- Downloads is the default suggestion, not a hidden requirement.
- The user can disable Downloads and add other intake folders.
- Recursive watching is configurable per intake folder.
- System folders, app data folders, development dependency folders, and build folders are ignored even if nested under watched roots.

## Future Roadmap
- Optional Everything CLI search provider.
- Optional OpenAI transcription provider.
- Optional OpenAI metadata parser behind an interface.
- Local transcription provider implementation.
- Better folder/repo context inheritance.
- Packaging and auto-start configuration.
- Export/delete metadata tools.
- Richer retrieval ranking and command grammar.
- UI polish after safety and correctness are proven.
