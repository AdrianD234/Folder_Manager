# Testing Strategy

## Principles
- Tests protect safety, privacy, and trust first.
- Tests must be deterministic.
- Tests must not require OpenAI, Everything, microphone hardware, real Downloads, Desktop, OneDrive, project folders, or user profile folders.
- Tests involving files must use temporary directories.
- Tests involving SQLite must use temporary database paths.
- Provider tests must use fakes, mocks, or local stubs.
- Do not claim manual smoke tests were run unless they were actually run.

## Unit Test Strategy
Unit tests cover pure Core behavior:

- Triage classification.
- File stability decisions with injectable clock and fake file state.
- Batch detection.
- Filename planning.
- Safety validation.
- Conflict resolution.
- Search intent parsing.
- Voice command parsing.
- Metadata parsing from typed or transcribed text.
- Relative date parsing.

Unit tests should not touch the real filesystem unless they create and clean up temp directories through test utilities.

## Integration Test Strategy
Integration tests cover Infrastructure behavior using temp resources:

- SQLite migration and repository tests against a temp database.
- File operation tests against temp directories.
- Watcher tests against temp directories only.
- Logging tests with temp log paths only. Logging tests must prove secrets and raw private payloads such as transcripts, provider metadata, and free-form notes are not mirrored to local log files.
- Provider tests using fake HTTP/process runners.
- App view-model tests may target `net8.0-windows` to reference the WPF app project, but they must not launch windows or require an interactive desktop session.

No integration test may use real app data paths.

## Manual Smoke Test Strategy
Manual smoke tests validate UX and OS integration that automated tests cannot fully prove:

- Tray startup.
- Global hotkeys.
- Popup display.
- Microphone or manual text capture.
- User confirmation flows.
- Opening files/folders.
- Undo move/rename.

Manual tests must record:

- Date run.
- Build/version or commit.
- Test environment.
- Steps performed.
- Result.
- Any deviations.

## Test Data Strategy
- Generate files under temporary test roots.
- Use small representative files with safe placeholder content.
- Use fake extensions and names to simulate downloads, build artifacts, and archives.
- Avoid storing real private files in the repo.
- Avoid committing generated databases, logs, audio, or local settings.

## File Operation Tests
Use temporary directories only. Required cases:

- Move file to empty destination.
- Rename file in same folder.
- Conflict generates non-overwriting candidate.
- Illegal characters are sanitized.
- Existing destination is not overwritten.
- Destination folder creation occurs only after confirmation boundary.
- Undo succeeds when original path is free and identity matches.
- Undo fails safely when original path is occupied.
- Undo fails safely when identity does not match.

## SQLite Tests
Use temp app-data paths. Required cases:

- Migration creates all tables.
- `app_settings` insert/update.
- `intake_folders` insert/update.
- `file_records` insert/update.
- `file_events` insert.
- `event_batches` insert.
- `metadata_entries` insert/update.
- `actions` insert/update.
- `undo_actions` insert/update.
- `transcription_jobs` insert/update.
- `voice_commands` insert.
- `search_queries` insert.
- Indexes support expected lookup patterns.
- Constraints reject invalid records where applicable.

## Triage Tests
Required categories:

- Meaningful one-off files.
- Temporary and partial downloads.
- File size still changing.
- File locked by another process.
- Archive extraction bursts.
- OneDrive sync bursts.
- Installer/unpacker bursts.
- Package manager installs.
- Development/repo/build noise.
- App own-operation suppression.
- System and app-data noise.

## File Stability Tests
- Stable size and timestamp after debounce.
- Changing size delays processing.
- Changing timestamp delays processing.
- Locked file delays processing.
- Zero-byte transient is delayed or ignored.
- Hash is computed for ordinary stable files.
- Hash is deferred for large files.

## Batch Detection Tests
- More than 10 files in 10 seconds under same root creates a possible batch.
- More than 50 files in 60 seconds suppresses individual prompts.
- More than 200 files in 5 minutes creates batch review only.
- Archive-like filename patterns and nested output are grouped.
- OneDrive-like sync bursts are grouped.
- Installer and package-manager bursts are grouped.

## Undo Tests
- Undo record is created for every successful move/rename.
- Undo verifies current path.
- Undo verifies file identity.
- Undo fails safely if original path is occupied.
- Undo fails safely if current file changed.
- Undo logs success and failure.

## Search Parser Tests
Required examples:

- "open the last five Excel files I saved"
- "show high relevance PDFs from last week"
- "find the finance report I downloaded two weeks ago"
- "open the folder for my AI infrastructure files"
- "find files about Nvidia capex"

Required filters:

- File type.
- Count.
- Relevance.
- Project.
- Topic.
- Tags.
- Relative dates.
- Open file vs open folder.

## Voice Command Parser Tests
- Spoken transcript with filler words.
- Manual typed command.
- Ambiguous command.
- Unsupported destructive command.
- Multiple results require confirmation.
- Single result can be presented for confirmation.

## Provider Tests
- Fake transcription provider success.
- Fake transcription provider failure.
- Local provider not configured.
- OpenAI provider missing API key.
- OpenAI provider secret redaction.
- Everything provider disabled.
- Everything CLI missing.
- Everything fake process output parsing.

## Required Command Set
Baseline:

```powershell
.\tools\validate.ps1
```

This script redirects .NET and NuGet home/cache paths into ignored repo-local
folders before running:

```powershell
dotnet --info
dotnet restore .\FileIntakeAssistant.sln --configfile .\NuGet.config -p:NuGetAudit=false
dotnet build .\FileIntakeAssistant.sln --no-restore
dotnet test .\FileIntakeAssistant.sln --no-build --verbosity normal
git status --short
```

Milestone-specific filters are listed in `docs/PLAN.md`. If restore is blocked
by network access after the repo-local script is used, record the blocker and
run `--no-restore` validation only when project, package, and lock files are
unchanged.

For logging changes, also run:

```powershell
dotnet test .\FileIntakeAssistant.sln --no-build --filter Logging --verbosity normal
```

The script disables the online NuGet audit lookup for sandbox validation to
avoid network-only `NU1900` failures under `TreatWarningsAsErrors`; package
versions are still pinned centrally and restored from lock files.
