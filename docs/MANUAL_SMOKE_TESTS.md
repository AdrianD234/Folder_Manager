# Manual Smoke Tests

Manual smoke tests must be run only when the relevant milestone has an app or workflow to test. Do not mark a test as passed unless it was actually run.

Record each run with:

- Date.
- Commit or build.
- Windows version if relevant.
- Steps.
- Result.
- Notes.

## Milestone 12 Smoke Status
Date: 2026-05-23.

Result: Not run in this Codex validation pass.

Reason: These are interactive Windows UX and OS-integration checks. They require launching the WPF app in the user session and, for some cases, explicit user-approved temp intake folders, file operations, microphone/provider settings, or Everything CLI configuration. Automated validation remains limited to temp directories, temp databases, fake providers, and command-line build/test runs.

Current implementation notes:

- The app has a WPF tray shell with tray menu commands for Open, Search, and Exit.
- The app registers a nonfatal command hotkey boundary for showing Search.
- Manual metadata capture and deterministic search can be exercised through the main window.
- Watcher candidate processing is wired into a user-facing Folders tab, a Candidate queue tab, persistent file-event audit rows, and a watcher-driven intake popup.
- Move/rename/undo services and WPF confirmation surfaces are implemented and tested with temp files, but interactive Windows smoke testing has not been run.
- Real microphone capture is not implemented; manual transcript fallback and provider boundaries are implemented.

All tests below remain `Not run` until a manual Windows smoke pass is performed and recorded.

## Milestone 13 Smoke Status
Date: 2026-05-23.

Result: Not run in this Codex validation pass.

Reason: These checks require launching the WPF app in an interactive Windows session and creating an explicit user-approved temporary intake folder. Automated validation covered the settings view model, disabled Downloads suggestion, broad-root/repository-marker rejection, persistent event audit rows, and UI-facing candidate queue refresh using temp databases and deterministic paths.

## Milestone 14 Smoke Status
Date: 2026-05-23.

Result: Not run in this Codex validation pass.

Reason: These checks require launching the WPF app in an interactive Windows session, configuring an explicit temporary intake folder, and observing the watcher-driven popup. Automated validation covered the candidate popup view model, save-to-SQLite workflow, skip/dismiss audit action, no-key manual transcript fallback, and unchanged temp-file content/timestamp with no sidecar.

## Milestone 15 Smoke Status
Date: 2026-05-23.

Result: Not run in this Codex validation pass.

Reason: These checks require launching the WPF app in an interactive Windows session and using explicit user-approved temporary file-operation targets. Automated validation covered safe filing preview state, confirmation refusal without mutation, confirmed move through the safe executor, undo success, undo conflict failure, undo identity mismatch failure, selected search-result opening through fake launch services, bulk-open cancellation, and ambiguous-result display.

## Milestone 16 Smoke Status
Date: 2026-05-23.

Result: Not run in this Codex validation pass; deferred pending user-approved interactive Windows smoke testing.

Reason: The active goal explicitly requires stopping before manual smoke tests that require the user's machine, microphone, Downloads, API keys, or real-user-file mutation. The remaining smoke tests require launching the WPF app in the user session and using explicit temporary intake and filing folders. No interactive app launch, microphone capture, OpenAI call, Everything call, real Downloads usage, or real user-file move/rename was performed by Codex.

Deferral status: Pending user approval or user-run smoke pass. This is not a passed smoke result and must not be treated as proof of interactive Windows behavior.

## Interactive Smoke Approval Boundary
Codex may run the interactive smoke pass only after explicit user approval for the current session.

Default safe test roots for an approved run:

```text
%TEMP%\FileIntakeAssistant-Smoke\<timestamp>\Intake\
%TEMP%\FileIntakeAssistant-Smoke\<timestamp>\FileOperations\
```

Before an approved run, prepare placeholder files with:

```powershell
.\tools\new-smoke-fixtures.ps1
```

To preview the fixture plan without creating files, run:

```powershell
.\tools\new-smoke-fixtures.ps1 -PlanOnly
```

The fixture script creates only placeholder files under the system temp
directory, refuses existing roots, refuses non-temp roots, and does not delete
or overwrite files. The script does not launch the app and does not perform the
manual smoke pass by itself.

Create a smoke run report template with:

```powershell
.\tools\new-smoke-run-report.ps1
```

Preview the report path and fixture root without creating files:

```powershell
.\tools\new-smoke-run-report.ps1 -PlanOnly
```

The report helper writes only under ignored `artifacts/`, refuses output paths
outside `artifacts/`, refuses to overwrite an existing report, and starts every
test as `Not run`. The report is not evidence of a passed smoke test until it
is completed from an actual approved interactive run.

Approval must identify whether Codex or the user will run the pass. Unless the
user explicitly approves otherwise, the smoke pass must not use real Downloads,
Desktop, OneDrive, project/repo folders, API keys, microphone hardware,
Everything CLI, or real private files.

Record for each approved run:

- Date and local time.
- Commit or working-tree state.
- Windows version.
- App build command or publish path.
- Test roots used.
- Provider/API state.
- Each test result as Passed, Failed, Skipped, or Deferred.
- Any safety/privacy issue and the exact stop point.

Stop immediately if the app attempts to delete, overwrite, silently move or
rename, broaden watch scope, call an external provider without opt-in, or write
private metadata into a user file or sidecar.

## Test 1: Start App
Purpose: Confirm app starts and tray icon appears.

Steps:

1. Build app.
2. Start app.
3. Confirm tray icon appears.
4. Open tray menu.
5. Exit app from tray menu.

Expected result:

- App starts without crash.
- Tray menu opens.
- Exit works.

Status: Not run; deferred pending user-approved interactive Windows smoke testing.

## Test 2: Configure Downloads As Intake Folder
Purpose: Confirm user can configure Downloads or a test substitute.

Steps:

1. Open settings.
2. Add Downloads or a temporary test intake folder.
3. Confirm folder appears as enabled.
4. Confirm recursive setting is visible.

Expected result:

- Folder is explicitly configured.
- App does not watch unrelated folders.

Status: Not run; deferred pending user-approved interactive Windows smoke testing.

## Test 3: Create Fake Completed PDF In Temp Intake Folder
Purpose: Confirm meaningful stable file produces intake candidate.

Steps:

1. Configure a temp intake folder.
2. Create `Example Report.pdf` with small placeholder content.
3. Wait for stability window.
4. Observe intake popup or candidate queue.

Expected result:

- Candidate appears.
- Triage reason is meaningful one-off file.

Status: Not run; deferred pending user-approved interactive Windows smoke testing.

## Test 4: Confirm Popup Appears
Purpose: Confirm intake popup presents file info and metadata controls.

Steps:

1. Trigger a meaningful file candidate.
2. Review popup content.
3. Confirm file name, path, type, and triage reason are visible.

Expected result:

- Popup is understandable and dismissible.

Status: Not run; deferred pending user-approved interactive Windows smoke testing.

## Test 5: Save Metadata Only
Purpose: Confirm metadata saves externally without modifying file.

Steps:

1. Open popup for temp file.
2. Enter note, relevance, project, topic, tags, and source URL.
3. Save metadata.
4. Inspect database through app/test tool.
5. Confirm file content and file metadata are unchanged.

Expected result:

- Metadata exists in SQLite.
- User file is unchanged.

Status: Not run; deferred pending user-approved interactive Windows smoke testing.

## Test 6: Move/Rename With Confirmation
Purpose: Confirm safe operation preview and confirmation.

Steps:

1. Select temp file candidate.
2. Request move or rename suggestion.
3. Review preview.
4. Confirm operation.
5. Verify file path changed.
6. Verify action and undo records exist.

Expected result:

- No overwrite occurs.
- Move/rename happens only after confirmation.

Status: Not run; deferred pending user-approved interactive Windows smoke testing.

Automated coverage:

- Milestone 15 tests cover preview generation, destination conflict resolution display, extension preservation display, confirmation refusal, confirmed temp-file move, action rows, and undo rows.

## Test 7: Undo Move
Purpose: Confirm app-performed move can be undone.

Steps:

1. Perform confirmed move in temp folder.
2. Choose undo.
3. Verify file returns to original path.
4. Verify undo action is logged.

Expected result:

- Undo succeeds when original path is free and identity matches.

Status: Not run; deferred pending user-approved interactive Windows smoke testing.

Automated coverage:

- Milestone 15 tests cover undo success, original-path conflict failure, identity mismatch failure, and undo audit action logging through temp directories only.

## Test 8: Skip Batch Extraction
Purpose: Confirm batch suppression.

Steps:

1. Configure temp intake folder.
2. Create more than 50 files within 60 seconds under one root.
3. Observe prompts.
4. Check batch audit.

Expected result:

- Individual prompts are suppressed.
- Batch event is recorded.

Status: Not run; deferred pending user-approved interactive Windows smoke testing.

## Test 9: Ignore `.crdownload`
Purpose: Confirm partial download is not prompted.

Steps:

1. Create `Example.pdf.crdownload` in temp intake folder.
2. Wait through ordinary debounce.
3. Observe prompts.

Expected result:

- No prompt for partial file.

Status: Not run; deferred pending user-approved interactive Windows smoke testing.

## Test 10: Ignore `node_modules` And Build Noise
Purpose: Confirm development noise suppression.

Steps:

1. Create `node_modules\package\index.js` under temp intake folder.
2. Create `build\output.dll`.
3. Observe prompts.

Expected result:

- No individual prompts.
- Triage logs development/build noise.

Status: Not run; deferred pending user-approved interactive Windows smoke testing.

## Test 11: Record Or Manual Transcript Fallback
Purpose: Confirm no-key voice workflow still works.

Steps:

1. Open intake popup.
2. Leave OpenAI unconfigured.
3. Enter manual transcript text.
4. Save metadata.

Expected result:

- Manual transcript saves.
- No API call occurs.

Status: Not run; deferred pending user-approved interactive Windows smoke testing.

## Test 12: Run Search Command
Purpose: Confirm retrieval works from SQLite metadata.

Steps:

1. Seed or create metadata records.
2. Open command window.
3. Type `show high relevance PDFs from last week`.
4. Review results.

Expected result:

- Results are shown from SQLite.
- No external provider is required.

Status: Not run; deferred pending user-approved interactive Windows smoke testing.

## Test 13: Open File Or Folder With Confirmation
Purpose: Confirm safe retrieval action behavior.

Steps:

1. Run a command that returns multiple files.
2. Choose open.
3. Confirm the bulk-open prompt appears.
4. Open selected file or containing folder.

Expected result:

- Multiple files do not open without confirmation.
- Action is logged.

Status: Not run; deferred pending user-approved interactive Windows smoke testing.

Automated coverage:

- Milestone 15 tests cover selected file open, selected containing-folder open, bulk-open cancellation, ambiguous multi-result display, and action logging through fake launch services.
