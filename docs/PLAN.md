# Milestone Plan

Each milestone must be small enough for one implementation loop. Work one milestone at a time. Do not proceed if acceptance criteria fail, validation commands fail, or a stop condition is met.

After every milestone:

1. Run the listed validation commands.
2. Fix failures before moving on.
3. Update `docs/STATUS.md`.
4. Update `docs/DECISIONS.md` for meaningful choices.
5. Update `docs/RISK_REGISTER.md` for new or changed risks.
6. Summarize changed files.
7. State the next milestone.

## Milestone 0: Repository Governance And Planning Files
Objective: Create the durable documentation and configuration layer before implementation.

Deliverables:

- Root governance and readme files.
- Documentation set under `docs/`.
- Codex guidance under `.codex/`.
- Shared .NET config scaffolding.
- .NET/WPF/privacy-aware `.gitignore`.

Acceptance criteria:

- All required governance files exist.
- Documents are internally consistent on safety rules, app data paths, provider optionality, and milestones.
- No app logic has been implemented.
- OpenAI and Everything are described as optional.

Automated tests required: none.

Manual smoke tests required: document review only.

Validation commands:

```powershell
dotnet --info
git status --short
```

Risks:

- Over-specification that blocks implementation.
- Inconsistent terminology across documents.

Stop conditions:

- Documents conflict on safety, privacy, or source-of-truth rules.
- The user changes product direction.

Expected files changed:

- `AGENTS.md`, `README.md`, `global.json`, `Directory.Build.props`, `Directory.Packages.props`, `NuGet.config`, `.gitignore`, `.codex/*`, `docs/*`, `tools/README.md`.

## Milestone 1: Solution Skeleton And Build Discipline
Objective: Create the .NET solution and projects without business implementation.

Deliverables:

- `FileIntakeAssistant.sln`.
- `src/FileIntakeAssistant.App`.
- `src/FileIntakeAssistant.Core`.
- `src/FileIntakeAssistant.Infrastructure`.
- `tests/FileIntakeAssistant.Tests`.
- Project references matching architecture rules.
- Package lock files generated.

Acceptance criteria:

- `dotnet restore`, `dotnet build`, and `dotnet test` pass.
- Core has no WPF, SQLite, OpenAI, or Everything dependencies.
- App is Windows/WPF capable.
- Tests project can run one placeholder architecture test.

Automated tests required:

- Architecture reference smoke test.

Manual smoke tests required: none.

Validation commands:

```powershell
dotnet --info
dotnet restore
dotnet build
dotnet test
git status --short
```

Risks:

- Local SDK missing or mismatched.
- WPF project settings accidentally applied to Core.

Stop conditions:

- SDK cannot build a WPF-capable solution.
- Project references violate dependency direction.

Expected files changed:

- Solution file, project files, initial test file, lock files, `docs/STATUS.md`.

## Milestone 2: Core Domain Models And SQLite Schema
Objective: Implement the initial domain model and SQLite schema using temp databases in tests.

Deliverables:

- Domain models for intake folders, files, events, batches, metadata, actions, undo, transcription jobs, voice commands, and search queries.
- SQLite migration runner.
- Repository interfaces and infrastructure implementations.
- Temp database test helpers.

Acceptance criteria:

- Schema matches `docs/DATA_MODEL.md` unless a decision note explains a change.
- Tests insert and query each core table.
- Tests verify constraints and indexes used by common queries.
- Tests use temp app-data paths only.

Automated tests required:

- Settings insert/update.
- Intake folder insert/update.
- File record insert.
- Metadata insert.
- Action and undo insert.
- Transcription job insert.
- Voice command and search query insert.

Manual smoke tests required: none.

Validation commands:

```powershell
dotnet build
dotnet test --filter DataModel
dotnet test
git status --short
```

Risks:

- Schema churn.
- Storing too much unstructured JSON too early.

Stop conditions:

- Any test touches real app data.
- SQLite schema diverges from docs without a decision.

Expected files changed:

- Core models, infrastructure persistence, test helpers, `docs/STATUS.md`, possibly `docs/DECISIONS.md`.

## Milestone 3: Event Triage Engine
Objective: Implement deterministic triage before prompting.

Deliverables:

- Triage inputs, outputs, categories, reasons, and confidence scoring.
- Ignore directory and extension rules.
- Own-operation suppression contract.
- Folder-level context behavior for repos and development folders.

Acceptance criteria:

- Meaningful one-off files are detected after basic checks.
- Temp files and partial downloads are ignored or delayed.
- Development, build, package, cache, and repo noise is suppressed.
- Own-operation events are suppressed.
- All triage decisions return reason and confidence.

Automated tests required:

- `.crdownload`, `.part`, `.tmp`, `.download`, `~$*`.
- `.git`, `node_modules`, `.venv`, `venv`, `bin`, `obj`, `target`, `dist`, `build`, `.vs`, `.idea`.
- App data, Program Files, Windows, browser cache path examples.
- Meaningful PDF, XLSX, DOCX, PPTX, image, ZIP, and text examples.
- Own-operation suppression examples.

Manual smoke tests required: none.

Validation commands:

```powershell
dotnet test --filter Triage
dotnet test
git status --short
```

Risks:

- Prompt fatigue if rules are too permissive.
- Missed meaningful files if rules are too aggressive.

Stop conditions:

- Triage suggests prompts for known build or package folders.
- Rules require watching whole-computer paths.

Expected files changed:

- Core triage classes, tests, `docs/TRIAGE_RULES.md` if refined, `docs/STATUS.md`.

## Milestone 4: File Stability And Batch Detection
Objective: Prevent partial files and bursts from producing prompts.

Deliverables:

- Stability checker with debounce, size/timestamp checks, lock checks, and deferred hash policy.
- Batch detector with configurable thresholds.
- Batch event records and suppression decisions.

Acceptance criteria:

- File is not processed while locked or changing.
- Batch thresholds match documented defaults.
- Archive extraction, OneDrive sync, installer, and package bursts suppress individual prompts.
- Large file hashing can be deferred.

Automated tests required:

- Stable file after debounce.
- Changing size delays processing.
- Locked file delays processing.
- More than 10 files in 10 seconds creates possible batch.
- More than 50 files in 60 seconds suppresses individual prompts.
- More than 200 files in 5 minutes is batch review only.

Manual smoke tests required:

- Simulate a copied file in a temp folder.
- Simulate a burst of files in a temp folder.

Validation commands:

```powershell
dotnet test --filter Stability
dotnet test --filter Batch
dotnet test
git status --short
```

Risks:

- Race conditions with filesystem events.
- Slow tests if debounce is not injectable.

Stop conditions:

- Tests require sleeping for real long windows instead of using injectable time.
- Tests touch real Downloads or OneDrive folders.

Expected files changed:

- Core stability and batch classes, infrastructure lock checks, tests, status/risk docs.

## Milestone 5: Safe File Operations And Undo
Objective: Implement safe move/rename planning and reversible execution.

Deliverables:

- Filename sanitizer.
- Destination validator.
- Conflict resolver.
- Move/rename planner.
- Safe operation executor with confirmation boundary.
- Undo validator and executor.

Acceptance criteria:

- Illegal Windows characters are sanitized.
- Extension is preserved unless user explicitly changes it.
- Existing destination is never overwritten.
- Conflict candidates use ` (2)`, ` (3)`, etc.
- Move/rename records action and undo rows.
- Undo verifies identity and fails safely on conflict.

Automated tests required:

- Filename sanitization.
- Conflict resolution.
- Destination validation.
- Move with temp files.
- Rename with temp files.
- Undo success.
- Undo conflict failure.
- Identity mismatch failure.

Manual smoke tests required:

- Confirm preview text for move and rename in a test folder once UI exists.

Validation commands:

```powershell
dotnet test --filter SafeFileOperations
dotnet test --filter Undo
dotnet test
git status --short
```

Risks:

- Accidental overwrite.
- Incorrect undo.
- Path length edge cases.

Stop conditions:

- Any operation can overwrite a file.
- Any test mutates a non-temp folder.

Expected files changed:

- Core planner/validator, infrastructure file executor, tests, status/risk docs.

## Milestone 6: Manual Metadata Capture Workflow
Objective: Implement a minimal user-driven metadata capture path.

Deliverables:

- Minimal WPF tray app or command-driven interim UI if tray UI is too large for the loop.
- File selection or recent candidate selection.
- Metadata form for note, relevance, project, topic, tags, source URL.
- SQLite save path.

Acceptance criteria:

- User can save metadata for a selected file.
- Metadata is stored only in SQLite.
- No metadata is written to the file.
- App works without API keys.

Automated tests required:

- View model or workflow tests.
- Repository persistence tests for metadata.

Manual smoke tests required:

- Select temp file.
- Save metadata.
- Confirm DB contains metadata.
- Confirm file content and metadata are unchanged.

Validation commands:

```powershell
dotnet build
dotnet test
git status --short
```

Risks:

- UI scope expands too quickly.
- Metadata accidentally leaks to files.

Stop conditions:

- Workflow requires OpenAI.
- Workflow writes sidecar or embedded metadata.

Expected files changed:

- App UI/view models, Core workflow, Infrastructure repositories, tests, manual smoke docs, status.

## Milestone 7: Watcher For Selected Intake Folders
Objective: Watch configured folders only and queue meaningful candidates.

Deliverables:

- Intake folder configuration.
- File watcher service.
- Candidate queue.
- Integration with triage, stability, and batch detection.
- Downloads default suggestion.

Acceptance criteria:

- Only configured folders are watched.
- Tests use temp folders only.
- Noise and batches do not prompt.
- Meaningful stable files enter the intake queue.
- Own-operation events are suppressed.

Automated tests required:

- Watch configured temp folder.
- Ignore unconfigured temp folder.
- Queue meaningful stable file.
- Suppress partial, build, batch, and own-operation examples.

Manual smoke tests required:

- Configure temp intake folder.
- Create fake completed PDF.
- Confirm candidate appears.
- Create `.crdownload`.
- Confirm no prompt.

Validation commands:

```powershell
dotnet test --filter Watcher
dotnet test
git status --short
```

Risks:

- Event duplication.
- Prompt fatigue.
- OneDrive event behavior.

Stop conditions:

- Any code watches the whole user profile or drive.
- Automated tests touch real Downloads.

Expected files changed:

- Infrastructure watcher, Core queue/workflow, tests, status/risk docs.

## Milestone 8: Voice Capture Provider Boundary
Objective: Add microphone/manual text workflow boundaries without requiring a real provider.

Deliverables:

- `ITranscriptionProvider`.
- Manual text capture path.
- Fake provider for tests.
- Local provider placeholder returning not configured.
- Audio temp path service.

Acceptance criteria:

- Manual text fallback works.
- Provider failures fall back to manual text.
- Tests require no microphone or API key.
- Temp audio retention policy is represented.

Automated tests required:

- Fake provider success.
- Provider failure.
- No-key/manual mode.
- Temp audio cleanup policy.

Manual smoke tests required:

- Use manual text fallback to create transcript metadata.

Validation commands:

```powershell
dotnet test --filter Transcription
dotnet test
git status --short
```

Risks:

- OS audio integration complexity.
- Accidentally requiring provider config.

Stop conditions:

- Tests require microphone hardware.
- Tests require OpenAI API key.

Expected files changed:

- Core provider contracts, Infrastructure provider placeholders, App workflow, tests, voice docs/status.

## Milestone 9: Optional OpenAI Transcription Provider
Objective: Implement OpenAI speech-to-text as an optional provider.

Deliverables:

- OpenAI provider implementation or minimal adapter.
- API key source from environment or secure config reference.
- Secret redaction in logs.
- Provider-disabled behavior.

Acceptance criteria:

- App works without API key.
- Provider reports not configured when key is absent.
- No tests require real OpenAI.
- Secrets are not logged.
- Audio temp files are deleted by default after successful transcription.

Automated tests required:

- Missing API key.
- Fake HTTP/provider success if adapter is implemented.
- Secret redaction.
- Audio cleanup.

Manual smoke tests required:

- Optional only if user supplies API key in local environment.

Validation commands:

```powershell
dotnet test --filter OpenAi
dotnet test
git status --short
```

Risks:

- API cost surprise.
- Secret leakage.
- Network-dependent tests.

Stop conditions:

- Provider cannot be disabled.
- Tests call live OpenAI.

Expected files changed:

- Infrastructure OpenAI provider, config, tests, decisions, security docs/status.

## Milestone 10: Voice Retrieval Parser And SQLite Search
Objective: Parse initial retrieval commands and search SQLite first.

Deliverables:

- Deterministic command parser.
- Search intent model.
- SQLite search provider.
- Result ranking.
- Confirmation rules for bulk actions.

Acceptance criteria:

- Commands parse into stable intent JSON.
- SQLite search handles recency, file type, relevance, project, topic, tags, and folder open.
- Multiple open results require confirmation.
- Ambiguity displays results instead of blind opening.

Automated tests required:

- "open the last five Excel files I saved".
- "show high relevance PDFs from last week".
- "find files about Nvidia capex".
- "open the folder for my AI infrastructure reports".
- Relative dates: today, yesterday, this week, last week, last N weeks, last month, last year.

Manual smoke tests required:

- Run typed retrieval command against temp DB records.
- Confirm single file open preview.
- Confirm multi-file confirmation.

Validation commands:

```powershell
dotnet test --filter Search
dotnet test --filter VoiceCommand
dotnet test
git status --short
```

Risks:

- Ambiguous commands opening too much.
- Search ranking feels surprising.

Stop conditions:

- Bulk open lacks confirmation.
- Parser requires LLM.

Expected files changed:

- Core parser/search intent, Infrastructure SQLite search, App command UI, tests, search docs/status.

## Milestone 11: Optional Everything CLI Integration
Objective: Add Everything CLI search provider only as optional enrichment.

Deliverables:

- `EverythingCliSearchProvider`.
- `es.exe` configured/discovered path handling.
- Disabled-by-default configuration.
- Result merge with SQLite metadata.

Acceptance criteria:

- App works with Everything disabled or unavailable.
- SQLite remains metadata source of truth.
- Everything results cannot mutate metadata without user action.
- Tests use fake process runner, not real Everything.

Automated tests required:

- Provider disabled.
- Missing `es.exe`.
- Fake `es.exe` result parsing.
- Merge with SQLite metadata.

Manual smoke tests required:

- Optional local smoke only if `es.exe` is configured.

Validation commands:

```powershell
dotnet test --filter Everything
dotnet test
git status --short
```

Risks:

- CLI path discovery fragility.
- Search result identity ambiguity.

Stop conditions:

- Everything becomes required.
- Tests require real Everything.

Expected files changed:

- Infrastructure search provider, config, tests, decisions/status.

## Milestone 12: Polish, Smoke Tests, And Packaging Notes
Objective: Improve usability, document manual validation, and prepare for packaging work.

Deliverables:

- UX polish for tray, popup, and command windows.
- Manual smoke test pass.
- Packaging notes.
- Known limitations.
- Updated risk register.

Acceptance criteria:

- Manual smoke tests in `docs/MANUAL_SMOKE_TESTS.md` are run or clearly marked not run.
- No known safety invariant violations.
- Build and tests pass.
- Packaging next steps are documented.

Automated tests required:

- Full suite.

Manual smoke tests required:

- Start app.
- Configure temp intake folder or Downloads with user approval.
- Process fake file.
- Save metadata.
- Move/rename with confirmation.
- Undo.
- Suppress batch.
- Ignore partial download.
- Run search command.

Validation commands:

```powershell
dotnet build
dotnet test
git status --short
```

Risks:

- Manual tests are skipped.
- UI claims exceed proven behavior.

Stop conditions:

- Any required manual smoke test cannot be run and lacks documentation.
- Safety or privacy issue remains unresolved.

Expected files changed:

- App UI, docs, tests as needed, status/risk docs.
