# Goal Contract

## Objective
Build File Intake Assistant as a Windows-native, private productivity application for meaningful file intake, spoken context capture, external metadata management, safe filing suggestions, and voice/text retrieval.

The app must help the user capture why a file was downloaded or saved, classify relevance/project/topic, safely suggest filing or renaming, and later retrieve files through deterministic natural-language commands such as "open the last five Excel files I saved."

The app must not become a generic whole-computer watcher or a noisy tag-every-event system.

## Completion Condition
The long-running goal is complete when:

- The milestones in `docs/PLAN.md` through polish/smoke-test readiness are implemented or explicitly superseded by documented decisions.
- The app builds and tests from the command line with `dotnet restore`, `dotnet build`, and `dotnet test`.
- The app runs as a Windows-native tray app with selected-folder intake, global hotkeys, intake popup workflow, metadata persistence, safe file operations, undo, and SQLite-first retrieval.
- Metadata remains external to user files and stored in SQLite under `%LOCALAPPDATA%\File Intake Assistant\`.
- Manual/deterministic workflows work without OpenAI, Everything, microphone hardware, or external services.
- Optional provider paths cannot bypass safety, confirmation, logging, or source-of-truth rules.
- Required automated tests pass and manual smoke tests are either passed or explicitly documented as not run with a reason.
- `docs/STATUS.md`, `docs/DECISIONS.md`, and `docs/RISK_REGISTER.md` are current.

## Milestone Loop
Implementation agents must work one milestone at a time.

1. Read `AGENTS.md`, this file, `docs/STATUS.md`, `docs/PLAN.md`, `docs/EXECUTION.md`, and `docs/TESTING.md`.
2. Read milestone-specific docs before touching code, such as `docs/TRIAGE_RULES.md`, `docs/DATA_MODEL.md`, `docs/VOICE_WORKFLOWS.md`, `docs/SEARCH_RETRIEVAL.md`, or `docs/SECURITY_PRIVACY.md`.
3. Confirm the active milestone from `docs/STATUS.md`.
4. Implement only the active milestone's deliverables from `docs/PLAN.md`.
5. Keep diffs scoped and reviewable.
6. Run the milestone validation commands.
7. Fix validation failures before moving on.
8. Update `docs/STATUS.md` with commands run, results, blockers, known issues, and next action.
9. Update `docs/DECISIONS.md` for architecture, dependency, schema, provider, privacy, or safety choices.
10. Update `docs/RISK_REGISTER.md` for new or changed risks.
11. Summarize changed files and state the next milestone.

Do not implement later milestone behavior early unless it is required to make the current milestone coherent and a decision note records the scope change.

## Validation Surface
Baseline validation commands:

```powershell
dotnet --info
dotnet restore
dotnet build
dotnet test
git status --short
```

Milestone-specific validation commands from `docs/PLAN.md` are mandatory when applicable, including filtered tests such as:

```powershell
dotnet test --filter DataModel
dotnet test --filter Triage
dotnet test --filter Stability
dotnet test --filter Batch
dotnet test --filter SafeFileOperations
dotnet test --filter Undo
dotnet test --filter Transcription
dotnet test --filter Search
dotnet test --filter VoiceCommand
dotnet test --filter Everything
```

No feature may be claimed as working without either automated tests or a documented manual smoke test. Do not fake test results. If a command cannot be run, record the exact reason in `docs/STATUS.md`.

## Hard Safety And Privacy Constraints
- Never delete user files.
- Never overwrite user files.
- Never move or rename user files without explicit confirmation.
- Support undo for every move or rename performed by the app.
- Store private metadata externally in SQLite.
- Never write private context into user files.
- Never use embedded metadata, alternate data streams, xattrs, or sidecar files as the source of truth.
- Never require OpenAI, Everything, microphone hardware, or external services for core/manual operation.
- Never log API keys, full secrets, or raw audio content.
- Never watch the whole computer.
- Never broaden watch scope silently.
- Tests must not mutate real Downloads, Desktop, OneDrive, user profile, repo, or project folders.

## Architecture Constraints
- Use .NET 8 or newer.
- Use a Windows-native desktop application; WPF is acceptable for v1.
- Use a tray app architecture with global hotkeys.
- Use SQLite via `Microsoft.Data.Sqlite` as the external metadata source of truth.
- Use structured local logging, currently planned through Serilog or equivalent.
- Use central package management in `Directory.Packages.props`.
- Keep NuGet lock files enabled and commit `packages.lock.json` when projects generate it.
- Do not add production dependencies without documenting the decision in `docs/DECISIONS.md`.

Project boundaries:

- `FileIntakeAssistant.App`: WPF tray UI, popups, hotkeys, view models, user confirmation boundaries, and workflow coordination.
- `FileIntakeAssistant.Core`: domain models, triage, stability, batch decisions, planning, parsing, search intent, metadata extraction, and safety validation.
- `FileIntakeAssistant.Infrastructure`: SQLite, filesystem adapter, logging, configuration, file watcher, Windows integration, audio capture, transcription providers, search providers, and Everything/OpenAI adapters.

Dependency rules:

- App may reference Core and Infrastructure.
- Infrastructure may reference Core.
- Core must not reference App or Infrastructure.
- Core must not depend on WPF, SQLite, OpenAI, Everything, concrete filesystem mutation, or Windows UI APIs.

## File-Operation Constraints
- All move and rename operations must go through one safe file operation service.
- Validate destination paths and path length strategy.
- Sanitize filenames and prevent illegal Windows characters.
- Preserve extensions unless the user explicitly changes them.
- Never overwrite existing files.
- Resolve conflicts with non-overwriting candidates such as `Name (2).ext`.
- Create destination folders only after confirmation.
- Record action before and after operation.
- Record undo action for every successful move or rename.
- Undo only if the current file identity still matches and the original path is free.
- If undo has a conflict or identity mismatch, fail safely and ask the user.
- The watcher must suppress the app's own move/rename events without hiding unrelated user actions.

## Provider, Search, And Triage Constraints
Provider constraints:

- OpenAI speech-to-text is optional behind `ITranscriptionProvider`.
- OpenAI LLM parsing, if added, is optional behind a parser/provider boundary.
- Local transcription provider may start as a not-configured placeholder.
- Manual text capture must always work.
- Providers must be disabled unless configured.
- Provider failures must fall back to manual workflows.
- Tests must use fakes/mocks and must not require live OpenAI or microphone hardware.

Search constraints:

- SQLite search is first and remains the metadata source of truth.
- Everything CLI integration is optional and later behind `IFileSearchProvider`.
- The app must work when Everything is disabled or unavailable.
- Everything results may enrich retrieval but cannot mutate metadata without user action.
- Ambiguous search results must be displayed, not blindly acted on.
- Opening multiple files requires confirmation.
- Destructive commands are unsupported.

Triage constraints:

- Watch only explicit intake folders.
- Downloads is an initial default suggestion, not a hidden broad scan.
- Every event must pass stability, triage, and batch checks before prompting.
- Suppress temp files, partial downloads, archive extractions, OneDrive sync bursts, installers, compilers, package folders, repos, build outputs, caches, and app-generated operations.
- Do not individually tag every child file in repos, build folders, package folders, extracted archives, or sync bursts.
- Prefer folder-level context for repos/project folders/extraction roots.
- Use configurable batch thresholds from `docs/TRIAGE_RULES.md` as initial defaults.
- File stability must account for size changes, timestamp changes, file locks, partial extensions, and deferred hashing for large files.

## Progress Reporting Requirements
After each milestone or blocked attempt, update `docs/STATUS.md` with:

- Current milestone.
- Completed milestones.
- Validation commands run.
- Exact command results or failure summaries.
- Test status.
- Known issues.
- Blockers.
- Next action.
- Manual smoke tests still required.

Also:

- Update `docs/DECISIONS.md` for meaningful choices.
- Update `docs/RISK_REGISTER.md` for new or changed risks.
- Do not claim manual smoke tests were run unless they were actually run.
- Do not claim implementation completion if any acceptance criteria remain unmet.
- Keep summaries concrete: changed files, commands run, current readiness, and next milestone.

## Stop Conditions
Stop and ask the user before proceeding if:

- A choice would change the safety model, privacy model, source-of-truth model, or watched-folder scope.
- A change could move, rename, overwrite, or delete real user files.
- A provider would send audio, transcript, metadata, filenames, or file content to an external API by default.
- OpenAI, Everything, microphone hardware, or another external service would become required for core operation or tests.
- Tests would need to touch real Downloads, Desktop, OneDrive, user profile, repo, or project folders.
- A destructive or privacy-sensitive ambiguity cannot be resolved from the docs or repo.
- A validation failure cannot be fixed without expanding scope.
- A new production dependency is needed but not yet justified in `docs/DECISIONS.md`.
- Manual smoke tests are required but cannot be run or documented honestly.
