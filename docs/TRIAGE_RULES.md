# Event Triage Rules

## Purpose
Triage decides whether a filesystem event deserves user attention. The app must not prompt for every event. It should prompt only for meaningful one-off files after stability and batch checks.

## Processing States
- `Observed`: raw watcher event received.
- `Normalized`: path and event type normalized.
- `WaitingForStability`: file may still be changing.
- `WaitingForBatchDecision`: event may belong to a burst.
- `Ignored`: event is known noise.
- `BatchSuppressed`: event belongs to a suppressed batch.
- `Candidate`: event may be meaningful.
- `PromptQueued`: user prompt is allowed.
- `Captured`: metadata captured.
- `Filed`: app performed confirmed move or rename.
- `Failed`: processing failed safely.

## Triage Categories
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
- `BrowserCacheNoise`
- `NeedsMoreObservation`
- `UnknownSafeToIgnore`

Every decision must include:

- Category.
- Reason.
- Confidence from 0.0 to 1.0.
- Whether user prompting is allowed.
- Optional batch id.

## Meaningful One-Off File Detection
A file may become `MeaningfulOneOff` only when:

- It is under an enabled intake folder.
- It is not under an ignored directory.
- It is not a known temp or partial file.
- It is not part of a suppressed batch.
- It is stable by size, timestamp, and lock checks.
- It is not produced by the app's own operation.
- Its extension or file characteristics match a user-level file type.

Initial meaningful extensions:

- Documents: `.pdf`, `.doc`, `.docx`, `.rtf`, `.txt`, `.md`.
- Spreadsheets: `.xls`, `.xlsx`, `.csv`, `.tsv`.
- Presentations: `.ppt`, `.pptx`.
- Images: `.png`, `.jpg`, `.jpeg`, `.gif`, `.webp`, `.tif`, `.tiff`.
- Archives as single downloads: `.zip`, `.7z`, `.rar`.
- Other files may be manually captured by the user.

## Temp And Partial Download Detection
Ignore or delay:

- `.crdownload`
- `.part`
- `.tmp`
- `.download`
- `.partial`
- `~$*`
- `*.lock`
- `*.swp`
- Hidden Office lock files.
- Zero-byte files that are newly created and still changing.
- Files with size or last-write time changing inside the debounce window.

If a partial download later becomes a stable final file, process the final file only.

## File Stability Rules
Default checks:

- Minimum debounce: 2 seconds for ordinary files.
- Extended debounce: 10 seconds after a partial/temp extension transition.
- File size must be unchanged across two observations.
- Last-write time must be unchanged across two observations.
- File must be openable for shared read or pass the configured lock check.
- File path must still exist.

Hashing:

- Compute SHA-256 for ordinary stable files below the configured hash threshold.
- Initial hash threshold: 100 MB.
- Defer hash for files above the threshold.
- Never repeatedly hash huge files during active changes.

## Debounce Windows
Initial defaults:

- Ordinary candidate: 2 seconds.
- Partial download finalization: 10 seconds.
- Archive/extraction root: 60 seconds before final batch decision.
- OneDrive sync burst: 60 seconds before prompting.
- Installer/unpacker burst: 60 seconds before prompting.

Debounce behavior must use injectable time in tests.

## Own-Operation Suppression
The safe file operation service must register planned move/rename operations before execution.

Suppress watcher events when:

- Old or new path matches a pending app operation.
- Event occurs within the own-operation suppression window.
- File identity matches the recorded operation.

Default suppression window: 30 seconds.

Own-operation suppression must not hide unrelated user actions outside the recorded paths.

## Batch Thresholds
Defaults:

- More than 10 files in 10 seconds under the same root: possible batch.
- More than 50 files in 60 seconds under the same root: suppress individual prompts.
- More than 200 files in 5 minutes: batch review only.

Batch thresholds must be configurable later, but defaults must exist from the start.

## Archive Extraction Detection
Signals:

- Many files created under a new folder in a short time.
- Common extracted structure such as nested directories, `__MACOSX`, many small files, or archive-named root folder.
- Recently observed `.zip`, `.7z`, or `.rar` in the same intake folder.
- File count exceeds batch thresholds.

Behavior:

- Do not prompt for every extracted child file.
- Record an event batch.
- Optionally show one batch-level review later.
- Allow user to tag the folder root if useful.

## OneDrive Sync Burst Detection
Signals:

- Many create/change events under OneDrive paths.
- Rapid timestamp changes.
- Files appearing with sync-related attributes or placeholder behavior where detectable.
- Repeated duplicate events for the same path.

Behavior:

- Delay prompting until the burst settles.
- Suppress individual prompts above threshold.
- Prefer folder-level or batch audit records.

## Installer And Unpacker Burst Detection
Signals:

- Many files created by an executable or installer-like process when detectable.
- Common directories: `bin`, `lib`, `resources`, `locales`, `runtimes`, `plugins`.
- Common generated file types: `.dll`, `.exe`, `.pdb`, `.cache`, `.json`, `.dat`, `.pak`.
- Large nested structures created quickly.

Behavior:

- Suppress individual prompts.
- Record batch decision.

## Development, Repo, And Build Noise
Ignore paths containing:

- `.git`
- `.svn`
- `.hg`
- `node_modules`
- `.venv`
- `venv`
- `.tox`
- `.mypy_cache`
- `.pytest_cache`
- `.ruff_cache`
- `bin`
- `obj`
- `target`
- `dist`
- `build`
- `.vs`
- `.idea`
- `.gradle`
- `.next`
- `.nuxt`
- `coverage`
- `packages`

Ignore common build/generated extensions unless manually captured:

- `.dll`
- `.exe` when under build/tool folders
- `.pdb`
- `.obj`
- `.o`
- `.class`
- `.cache`
- `.map`
- `.min.js` under build output

Repos and development folders may receive folder-level context, but child files are not individually tagged by default.

## System And App-Data Noise
Never watch these by default. If nested under a watched root, ignore:

- `C:\Windows`
- `C:\Program Files`
- `C:\Program Files (x86)`
- `%APPDATA%`
- `%LOCALAPPDATA%`
- Browser cache folders.
- The app's own `%LOCALAPPDATA%\File Intake Assistant\` directory.

## Folder-Level Tagging Versus File-Level Tagging
For folders that look like repos, extracted archives, package folders, or project directories:

- Prefer folder-level context.
- Do not create individual metadata rows for every child file.
- Child files may inherit context in search/display.
- Allow manual file-level metadata when the user explicitly tags a file.

## Confidence Scoring
Initial guidance:

- 0.90 to 1.00: strong known category.
- 0.70 to 0.89: likely category, safe automated decision if it suppresses prompts.
- 0.40 to 0.69: needs more observation or batch grouping.
- 0.00 to 0.39: unknown; fail conservative and avoid prompting unless user initiated.

Prompting should require high confidence that the file is meaningful and stable.

## Fallback Behavior
- Unknown event under selected intake folder: delay and observe.
- Unknown stable single file with user-level extension: queue candidate with moderate confidence.
- Unknown burst: create batch record and suppress individual prompts.
- Any safety uncertainty: do not move, rename, delete, or overwrite.
