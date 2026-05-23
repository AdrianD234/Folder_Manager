# Manual Smoke Tests

Manual smoke tests must be run only when the relevant milestone has an app or workflow to test. Do not mark a test as passed unless it was actually run.

Record each run with:

- Date.
- Commit or build.
- Windows version if relevant.
- Steps.
- Result.
- Notes.

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

Status: Not run.

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

Status: Not run.

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

Status: Not run.

## Test 4: Confirm Popup Appears
Purpose: Confirm intake popup presents file info and metadata controls.

Steps:

1. Trigger a meaningful file candidate.
2. Review popup content.
3. Confirm file name, path, type, and triage reason are visible.

Expected result:

- Popup is understandable and dismissible.

Status: Not run.

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

Status: Not run.

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

Status: Not run.

## Test 7: Undo Move
Purpose: Confirm app-performed move can be undone.

Steps:

1. Perform confirmed move in temp folder.
2. Choose undo.
3. Verify file returns to original path.
4. Verify undo action is logged.

Expected result:

- Undo succeeds when original path is free and identity matches.

Status: Not run.

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

Status: Not run.

## Test 9: Ignore `.crdownload`
Purpose: Confirm partial download is not prompted.

Steps:

1. Create `Example.pdf.crdownload` in temp intake folder.
2. Wait through ordinary debounce.
3. Observe prompts.

Expected result:

- No prompt for partial file.

Status: Not run.

## Test 10: Ignore `node_modules` And Build Noise
Purpose: Confirm development noise suppression.

Steps:

1. Create `node_modules\package\index.js` under temp intake folder.
2. Create `build\output.dll`.
3. Observe prompts.

Expected result:

- No individual prompts.
- Triage logs development/build noise.

Status: Not run.

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

Status: Not run.

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

Status: Not run.

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

Status: Not run.
