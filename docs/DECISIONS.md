# Architecture Decision Log

Record durable decisions here. Update this file whenever architecture, dependencies, provider behavior, schema, safety, or privacy choices change.

## Decision 0001: Windows-Native .NET Application
Status: Accepted
Date: 2026-05-23

Context:
The app is a private Windows productivity tool requiring tray integration, global hotkeys, microphone workflows, and safe local file operations.

Decision:
Use .NET 8 or newer and a Windows-native desktop architecture. WPF is acceptable for the first version.

Consequences:
The app can use native Windows capabilities and `dotnet` command-line builds. Cross-platform support is not a v1 goal.

## Decision 0002: SQLite As External Metadata Source Of Truth
Status: Accepted
Date: 2026-05-23

Context:
Private context must stay outside user files and remain queryable for retrieval.

Decision:
Use SQLite under `%LOCALAPPDATA%\File Intake Assistant\data\file-intake.db` as the source of truth.

Consequences:
The app must own schema migrations, backup/export guidance, and temp database tests.

## Decision 0003: Metadata Is Not Written Into Files
Status: Accepted
Date: 2026-05-23

Context:
User metadata may include private spoken context, project relevance, and summaries.

Decision:
Do not write private context into embedded metadata, alternate data streams, xattrs, or sidecar files.

Consequences:
Sharing a file does not share private app metadata. Retrieval depends on SQLite and app state.

## Decision 0004: Everything Integration Is Optional
Status: Accepted
Date: 2026-05-23

Context:
Everything is installed on the user's system, but v1 must not require Everything CLI or SDK.

Decision:
Implement SQLite search first. Add Everything CLI later only through optional `IFileSearchProvider`.

Consequences:
The app remains functional without Everything. Everything can enrich retrieval later but cannot become metadata source of truth.

## Decision 0005: OpenAI Speech-To-Text Is Optional
Status: Accepted
Date: 2026-05-23

Context:
Runtime OpenAI API use is separate from ChatGPT/Codex credits and may not be configured.

Decision:
Expose OpenAI STT behind `ITranscriptionProvider`. Manual text and fake providers must work without an API key.

Consequences:
The app must have deterministic/manual workflows and tests must not require OpenAI.

## Decision 0006: Deterministic And Manual Mode Exists
Status: Accepted
Date: 2026-05-23

Context:
The app must be useful without API keys or external services.

Decision:
Implement deterministic parsing and manual metadata capture as first-class paths.

Consequences:
Provider-backed enrichment can improve UX but cannot be required for core workflows.

## Decision 0007: Watch Selected Intake Folders Only
Status: Accepted
Date: 2026-05-23

Context:
Watching the whole computer would create noise, privacy risk, and trust issues.

Decision:
Watch only explicit intake folders chosen by the user, with Downloads as the initial default suggestion.

Consequences:
The app must provide folder configuration and must not silently broaden watch scope.

## Decision 0008: Triage Before Prompting
Status: Accepted
Date: 2026-05-23

Context:
Prompt fatigue is a major product risk.

Decision:
Every filesystem event must pass stability, triage, and batch checks before user prompting.

Consequences:
Triage is core product behavior and must have strong tests.

## Decision 0009: Confirm Before Move Or Rename
Status: Accepted
Date: 2026-05-23

Context:
Automated file movement can damage user trust.

Decision:
Every move or rename requires explicit user confirmation and preview.

Consequences:
No provider, workflow, or parser can bypass the confirmation boundary.

## Decision 0010: Never Delete
Status: Accepted
Date: 2026-05-23

Context:
Deletion is not necessary for v1 and creates high risk.

Decision:
The app never deletes user files.

Consequences:
Cleanup features must be out of scope or explicitly redesigned later.

## Decision 0011: Pinned NuGet Dependencies And Lock Files
Status: Accepted
Date: 2026-05-23

Context:
The app should build reproducibly and avoid unreviewed dependency drift.

Decision:
Use central package management and NuGet lock files.

Consequences:
Dependency additions must be deliberate and documented.

## Decision 0012: Initial Dependency Allowlist
Status: Accepted
Date: 2026-05-23

Context:
The preferred stack calls for SQLite, structured logging, MVVM support, and tests, but large dependencies should not enter the repository without review.

Decision:
Pin an initial allowlist in `Directory.Packages.props`: `Microsoft.Data.Sqlite`, `Serilog`, `Serilog.Sinks.File`, `CommunityToolkit.Mvvm`, `Microsoft.Extensions.DependencyInjection`, `Microsoft.Extensions.Hosting`, `xunit`, `xunit.runner.visualstudio`, and `Microsoft.NET.Test.Sdk`.

Consequences:
Milestone 1 can create projects against known versions. Future production dependencies require a new decision note or an update to this decision.

## Decision 0013: Explicit SQLite Migrations And Typed Store
Status: Accepted
Date: 2026-05-23

Context:
Milestone 2 needs the initial SQLite schema, migration discipline, and repository implementation without pulling in a large ORM or mixing SQL into the WPF app.

Decision:
Use an explicit `SqliteMigrationRunner` with numbered migrations recorded in `schema_migrations`, plus a typed `IFileIntakeStore` contract in Core and a `SqliteFileIntakeStore` implementation in Infrastructure.

Consequences:
The schema remains visible and reviewable against `docs/DATA_MODEL.md`. App code can consume repository contracts without direct SQL. Future schema changes must add numbered migrations and update tests rather than editing behavior implicitly through an ORM convention.

## Decision 0014: Pure Deterministic Core Triage
Status: Accepted
Date: 2026-05-23

Context:
Milestone 3 needs event triage before watcher, stability, batch, and UI workflows exist. The triage engine must suppress obvious noise without touching real files, scanning broad folders, or depending on Infrastructure.

Decision:
Implement triage as a pure Core service that classifies supplied event context, path strings, stability flags, selected-folder scope, and own-operation suppression records. Full file stability checks and batch threshold decisioning remain separate Milestone 4 responsibilities.

Consequences:
Triage rules are deterministic, fast, and easy to unit test without filesystem mutation. The watcher can later compose stability and batch evidence into the triage request without moving SQL, WPF, or filesystem code into Core.

## Decision 0015: Evidence-Based Stability And Batch Decisions
Status: Accepted
Date: 2026-05-23

Context:
Milestone 4 needs stability and batch decisions before the watcher exists. Core must not perform filesystem IO, but it must own the rules that decide whether a file is stable, changing, locked, part of a burst, or safe to prompt later.

Decision:
Implement file stability and batch detection as pure Core decision services that consume supplied evidence. Infrastructure provides a local file snapshot reader for existence, size, timestamp, and lock state. Use documented batch thresholds and set the initial large-file hash threshold to 100 MB, deferring hashes above that threshold.

Consequences:
Tests can exercise debounce and burst logic without long sleeps or real user folders. Watcher integration can later compose Infrastructure snapshots, Core stability decisions, Core batch decisions, and Core triage decisions without changing the safety model.

## Decision 0016: Central Safe File Operation Boundary
Status: Accepted
Date: 2026-05-23

Context:
Milestone 5 needs move/rename planning, confirmation, audit records, and undo without allowing any workflow or future provider to bypass safety validation.

Decision:
Keep filename sanitization, extension preservation, destination validation, conflict resolution, and planning in Core. Keep actual filesystem mutation in a single Infrastructure executor that requires explicit confirmation, refuses existing destination paths, records action and undo rows, updates file records, and verifies file identity before undo.

Consequences:
Core remains pure and testable while Infrastructure owns the only file mutation path. Future UI, watcher, parser, or provider workflows must call this boundary for move/rename and cannot overwrite or silently rename files.

## Decision 0017: Repo-Local Sandbox Validation Route
Status: Accepted
Date: 2026-05-23

Context:
Sandboxed `dotnet restore` attempted to read the real user profile NuGet config and failed with access denied. Network access to nuget.org may also be unavailable during Codex runs.

Decision:
Use `tools/validate.ps1` for sandbox-safe validation. The script redirects `DOTNET_CLI_HOME`, `APPDATA`, `NUGET_PACKAGES`, and `NUGET_HTTP_CACHE_PATH` into ignored repo-local folders, restores with the repo `NuGet.config`, and disables the online NuGet audit lookup only for this validation route so blocked network access does not become warning-as-error `NU1900`.

Consequences:
Validation no longer needs the real Windows profile config. Package versions remain pinned by central package management and lock files. Online audit can be run separately outside the restricted sandbox when network access is available.

## Decision 0018: Manual Metadata Capture Uses SQLite-Only Workflow
Status: Accepted
Date: 2026-05-23

Context:
Milestone 6 needs a minimal manual capture path before watcher, microphone, tray, and retrieval workflows are implemented. The path must save private context without modifying the selected file or requiring OpenAI.

Decision:
Implement a Core `ManualMetadataCaptureService` that receives a file snapshot and manual metadata fields, creates or reuses a `file_records` row, writes a `metadata_entries` row, and records a completed `ManualMetadataCapture` action. Infrastructure provides a local file snapshot reader. The WPF app composes these services and presents a minimal manual metadata form backed by the SQLite database under local app data.

Consequences:
The first usable workflow stores manual notes, relevance, project, topic, tags, and source URL externally in SQLite. No provider, microphone, sidecar file, embedded metadata, move, rename, overwrite, or delete path is involved.

## Decision 0019: Explicit-Folder Watcher Boundary
Status: Accepted
Date: 2026-05-23

Context:
Milestone 7 needs watcher infrastructure without broad filesystem scanning or prompting from every filesystem event. The app must watch only configured intake folders, suppress own-operation noise, and queue only meaningful stable candidates after triage, stability, and batch evidence.

Decision:
Add a Core intake processor and in-memory candidate queue that compose existing triage, stability, and batch decisions. Add an Infrastructure `ConfiguredIntakeFolderWatcher` that is constructed only from enabled `intake_folders` entries, rejects drive roots and whole-user-profile roots as too broad, and exposes watcher events without performing file mutation. Add a disabled Downloads suggestion provider so Downloads is explicit user configuration, not a hidden watcher.

Consequences:
The watcher boundary can be tested without real Downloads or profile mutation. Candidate queue behavior stays deterministic and Core-owned, while the `FileSystemWatcher` wrapper remains Infrastructure-owned. Later UI/tray work must wire this boundary into persistence, logging, and popup workflows without widening watched scope.

## Decision 0020: Transcription Boundary Starts With Manual/Fake/Local-No-Key Paths
Status: Accepted
Date: 2026-05-23

Context:
Milestone 8 needs voice capture provider boundaries without requiring microphone hardware, OpenAI, API keys, or live provider calls. The app must support manual text fallback and represent temp-audio retention before any real speech provider is added.

Decision:
Define `ITranscriptionProvider` and transcription workflow models in Core. Implement a Core `TranscriptionWorkflowService` that records `transcription_jobs`, supports manual text capture, records provider success/failure/not-configured results, and falls back to manual text when available. Implement an Infrastructure `LocalTranscriptionProvider` that returns `NotConfigured` and an `AudioTempFileService` that creates opaque temp-audio paths and applies a retention policy only inside the configured temp-audio root. Keep fake providers in tests and defer OpenAI and microphone capture to later milestones.

Consequences:
Manual/no-key transcription workflows are testable and deterministic. Provider failures cannot force external calls. Temp-audio cleanup has a safe path boundary, but UI microphone recording and OpenAI STT remain later milestone work.

## Decision 0021: OpenAI Transcription Uses Optional HttpClient Adapter
Status: Accepted
Date: 2026-05-23

Context:
Milestone 9 needs OpenAI speech-to-text support without making OpenAI a required dependency, without live API calls in tests, and without leaking API keys into persisted job fields or logs. Adding a production OpenAI SDK dependency would expand the dependency surface for a narrow adapter.

Decision:
Implement `OpenAiTranscriptionProvider` in Infrastructure behind Core `ITranscriptionProvider` using `HttpClient` and the OpenAI transcription HTTP endpoint. Keep the provider disabled by default. Resolve the API key through an environment-variable key provider, defaulting to `OPENAI_API_KEY`. Use fake `HttpMessageHandler` tests for success and failure paths. Redact configured secrets from provider error messages before they can be persisted.

Consequences:
The app still works without an API key and does not require OpenAI for tests or deterministic/manual operation. No OpenAI SDK package is added. Live runtime OpenAI calls are possible only after explicit configuration, and later UI/settings work must surface that runtime API usage is separate from ChatGPT/Codex credits.

## Decision 0022: Deterministic SQLite-First Search And Confirmation Outcomes
Status: Accepted
Date: 2026-05-23

Context:
Milestone 10 needs voice/text retrieval before optional Everything integration or any LLM-backed command parser. Retrieval must be useful in deterministic/manual mode and must not open files blindly when results are ambiguous or numerous.

Decision:
Implement a Core deterministic `SearchIntentParser`, stable intent JSON, `IFileSearchProvider`, confirmation policy, and search workflow service. Implement the first search provider as `SqliteSearchProvider` in Infrastructure against the app database. Use SQLite metadata and folder context as the source of truth, treat Everything as later optional enrichment, and return confirmation-required outcomes instead of directly opening files or folders.

Consequences:
The app can parse and search commands such as `open the last five Excel files I saved`, `show high relevance PDFs from last week`, `find files about Nvidia capex`, and `open the folder for my AI infrastructure reports` without OpenAI or Everything. Multiple open results require confirmation and destructive commands are rejected. Later Everything or LLM providers must conform to the same provider and confirmation boundaries.

## Decision 0023: Everything CLI Is Optional Discovery Enrichment Only
Status: Accepted
Date: 2026-05-23

Context:
Milestone 11 adds optional Everything CLI integration. The app must still work when Everything is disabled, missing, or not configured, and tests must not require the real Everything CLI or SDK.

Decision:
Implement `EverythingCliSearchProvider` behind `IFileSearchProvider`, keep it disabled by default, require configured allowed roots before invoking `es.exe`, isolate executable resolution behind `IEverythingCliPathResolver`, and isolate process execution behind `IEverythingCliProcessRunner`. Compose it with SQLite through `CompositeFileSearchProvider`, where SQLite metadata results remain authoritative and Everything path hits only enrich or discover results under configured provider rules. Do not add an Everything SDK dependency.

Consequences:
The app can later use `es.exe` when explicitly configured or discovered, but core operation remains SQLite-first and deterministic. Tests use fake process output. Everything results do not write metadata, do not mutate file records, and do not bypass confirmation rules.

## Decision 0024: Tray Shell Uses Windows Desktop APIs Without New NuGet Dependencies
Status: Accepted
Date: 2026-05-23

Context:
Milestone 12 needs UX polish for tray and command-window access. The project should remain Windows-native without adding a production dependency for basic shell integration.

Decision:
Enable Windows Forms support in the WPF app project and use `System.Windows.Forms.NotifyIcon` for the tray icon. Add a small WPF App-layer global hotkey controller using Win32 `RegisterHotKey`, currently registering a command-window hotkey boundary that shows the Search tab. Hotkey registration failure is nonfatal.

Consequences:
The app now has a tray shell with Open, Search, and Exit commands and can hide to tray instead of exiting on window close. No new NuGet dependency is added. Tray and hotkey behavior still require manual Windows smoke testing because they depend on the interactive desktop session.

## Decision 0025: Extend The Plan After Completion Audit
Status: Accepted
Date: 2026-05-23

Context:
Milestones 0 through 12 delivered the repository governance layer, .NET solution, Core and Infrastructure services, provider boundaries, deterministic search, optional Everything integration, and a WPF tray shell. The post-milestone completion audit found that the long-running goal is still not complete because several integrated user workflows are missing or unproven: watcher-to-popup intake, intake folder settings, safe filing confirmation UI, undo UI, confirmed file/folder open actions, real microphone capture, structured workflow logging, and interactive smoke tests.

Decision:
Add `docs/COMPLETION_AUDIT.md` and extend `docs/PLAN.md` with Milestones 13 through 16. These milestones cover app settings and intake folder wiring, watcher-driven intake popup and manual transcript workflow, safe filing/undo/retrieval confirmation UI, and final interactive smoke/release readiness.

Consequences:
The implementation goal remains active. Agents must not treat Milestone 12 as product completion. `docs/STATUS.md` moves to Milestone 13 as the active milestone, and completion requires either implementing the remediation milestones or explicitly documenting user-approved deferrals.

## Decision 0026: SQLite-Backed Intake Settings And Audited Candidate Queue
Status: Accepted
Date: 2026-05-23

Context:
Milestone 13 needs the WPF app to configure explicit intake folders and show watcher candidates without broadening watch scope or adding file mutation behavior. It also needs persistent audit rows for observed, ignored, and candidate outcomes.

Decision:
Add Core intake-folder validation and settings services that use SQLite `intake_folders` through `IFileIntakeStore`. Keep Downloads as a disabled suggestion until explicitly enabled. Treat "remove" from the settings UI as disabling the intake folder rather than deleting the record, preserving configuration history. Add an audited intake event processor that writes `file_events` rows for processed outcomes. Add WPF settings and candidate queue view models, plus an App-layer watcher coordinator that starts/restarts `ConfiguredIntakeFolderWatcher` from enabled SQLite folders.

Consequences:
Milestone 13 wires selected-folder watching into the app without adding move/rename/delete behavior. The candidate queue remains in-memory for display and is backed by persistent event audit rows, but persistent deduplication is still future work. The test project now targets `net8.0-windows` and references the WPF app project so App view models can be tested without launching the UI.

## Decision 0027: Watcher Candidates Use Manual Popup Workflow By Default
Status: Accepted
Date: 2026-05-23

Context:
Milestone 14 needs watcher candidates to open a dismissible intake popup and save reviewed notes/transcripts without requiring microphone hardware, OpenAI, Everything, or real user folders in tests. The workflow must preserve the SQLite source-of-truth model and must not write metadata into user files.

Decision:
Add a Core `IntakeCandidateWorkflowService` that composes the existing manual file snapshot reader contract, `ManualMetadataCaptureService`, and `TranscriptionWorkflowService`. Candidate popup saves write candidate-specific file records, metadata entries, manual transcription jobs when transcript text is reviewed, and completed `IntakeCandidateMetadataSaved` action rows. Candidate skip/dismiss writes a completed `IntakeCandidateSkipped` action row without creating metadata. Add a WPF `IntakePopupWindow` and `IntakeCandidatePopupViewModel`; route watcher-queued candidates into one modeless popup at a time. Keep provider status display in manual/no-key mode and do not start recording audio or call providers automatically.

Consequences:
Watcher-driven intake can now capture notes and reviewed transcript text into SQLite without modifying the target file, creating sidecars, requiring external providers, or adding move/rename/delete behavior. Real microphone recording, provider configuration UI, safe filing confirmation UI, persistent candidate deduplication, and interactive smoke testing remain later work.

## Decision 0028: User-Facing File And Retrieval Actions Use Confirmation View Models
Status: Accepted
Date: 2026-05-23

Context:
Milestone 15 needs user-facing move/rename preview, undo UI, and confirmed retrieval actions without weakening the central safe file operation boundary or adding real file/folder launching to automated tests.

Decision:
Add WPF App-layer view models for safe filing, undo actions, and search-result open actions. Keep move/rename execution in the existing Infrastructure `SafeFileOperationExecutor`. Add a small App-layer confirmation service and file-launch service boundary so tests can fake user confirmation and OS launching. List pending undo actions from SQLite through `IFileIntakeStore`. Log confirmed, cancelled, failed, and undo-related UI actions to SQLite `actions`.

Consequences:
The UI can preview conflict-resolved destinations and require confirmation before move/rename, run undo with identity/conflict checks, and open selected search results only after confirmation. Automated tests remain temp-directory and fake-launcher only. Interactive smoke testing is still required before claiming tray/UI/OS launching behavior works in the user session.

## Decision 0029: Release Publish Checks Use Repo-Local No-RID Fallback Under Sandbox Restrictions
Status: Accepted
Date: 2026-05-23

Context:
Milestone 16 requires a local publish check. The documented runtime-specific `win-x64` publish shape needs runtime-pack assets for `net8.0-windows/win-x64`; in restricted Codex environments, those Microsoft runtime-pack packages may be unavailable until NuGet access is approved or the packs are cached.

Decision:
Keep `artifacts/` ignored and add `tools/publish-local.ps1` as the preferred repo-local publish helper. By default it redirects .NET and NuGet home/cache paths into ignored repo-local folders and performs a no-RID framework-dependent publish that does not require downloading runtime packs. It also supports `-RuntimeSpecific` for the `win-x64` publish check when network access or cached runtime packs are available.

Consequences:
Release-readiness validation can prove the app publishes to ignored local artifact folders without requesting broad machine access. The no-RID publish remains the restricted-environment fallback. Runtime-specific publish remains the preferred release-shape check and passed after approved NuGet access restored the required runtime packs.

## Decision 0030: Publish Artifacts Must Pass A Local Privacy Safety Check
Status: Accepted
Date: 2026-05-23

Context:
Milestone 16 release readiness requires proving that local publish output does not include the app database, logs, temp audio, secrets, local settings, user metadata, or repo-local validation caches. Manual inspection is easy to skip and hard to reproduce.

Decision:
Add `tools/check-publish-artifact.ps1` as a repo-local, read-only publish artifact checker. The checker inspects a publish output directory, defaulting to `artifacts\publish\FileIntakeAssistant-framework-dependent\`, and fails if forbidden private/runtime artifacts or obvious plaintext OpenAI-style secrets are present.

Consequences:
The release gate now has an executable privacy check for publish output. The script does not replace interactive smoke testing, code signing, installer review, or runtime-specific publish validation, and it must be rerun for any alternate publish or installer output before distribution.

## Decision 0031: Manual Smoke Fixtures Must Be Generated Under Temp Only
Status: Accepted
Date: 2026-05-23

Context:
Milestone 16 requires interactive Windows smoke testing, but those tests must use placeholder files and explicit temporary folders rather than real Downloads, Desktop, OneDrive, repositories, or private files. Preparing the same fixture structure manually is error-prone and could lead to accidental use of a real user folder.

Decision:
Add `tools/new-smoke-fixtures.ps1` as a local fixture helper for approved smoke passes. The script defaults to a timestamped root under the system temp directory, refuses non-temp roots, refuses existing roots, creates placeholder files only, never deletes or overwrites files, and supports `-PlanOnly` so agents can validate the intended fixture layout without creating files.

Consequences:
Manual smoke testing becomes more reproducible without weakening the approval boundary. The script does not launch the app, configure watched folders, run microphone capture, call providers, move files, rename files, or perform the smoke pass.

## Decision 0032: Release Readiness Needs A Read-Only Consistency Gate
Status: Accepted
Date: 2026-05-23

Context:
Milestone 16 has multiple release-readiness facts that must stay aligned: automated validation passes, no-RID publish passes, runtime-specific publish status is explicit, artifact safety passes, and manual smoke remains unrun/deferred. These facts appear across several docs and are easy to drift during follow-up edits.

Decision:
Add `tools/check-release-readiness.ps1` as a read-only consistency gate. The script verifies required Milestone 16 docs and tools exist, release-readiness docs mention the current blocker state, ignored runtime/cache paths are still documented in `.gitignore`, and the publish artifact safety checker passes for the selected publish output.

Consequences:
Future Milestone 16 edits can be validated against an executable release-readiness gate without launching the app, touching real user files, making provider calls, or requiring network/runtime-pack access. The checker does not replace interactive smoke testing or runtime-specific publish validation.

## Decision 0033: Structured Local Logging Uses A Dependency-Free JSON-Lines Adapter
Status: Accepted
Date: 2026-05-23

Context:
The architecture calls for structured local logging, but adding a production logging package during release-readiness would add dependency and restore risk. The implemented workflows already persist actions, file events, transcription jobs, voice commands, and search queries to SQLite; those persisted workflow/audit events need a local structured file mirror without leaking raw private payloads.

Decision:
Add a dependency-free Infrastructure logging adapter using JSON lines under the app logs directory. The WPF app wraps the SQLite store with `StructuredLoggingFileIntakeStore`, which mirrors persisted workflow/audit rows to local logs. The logger redacts secret-like values and records only summaries for sensitive private content such as notes, transcript text, provider metadata, audio paths, raw search/voice text, and action details.

Consequences:
Milestone 16 improves the structured logging surface without adding NuGet dependencies or changing the SQLite source-of-truth model. Tests use temp databases and temp log paths only. This does not replace future interactive smoke proof.

## Decision 0034: App Shell Lifecycle Logging Uses The Same Local Audit Boundary
Status: Accepted
Date: 2026-05-23

Context:
Milestone 16 still needs audit coverage for App shell lifecycle paths such as startup, exit, tray commands, hotkey registration/activation, and watcher restart/dispose events. These events are user-visible or operationally important, but they must not introduce a new logging dependency or mirror private metadata.

Decision:
Add an App-layer `AppLifecycleAudit` wrapper around the existing Infrastructure `ILocalAuditLog`. Tray, hotkey, watcher, startup, and exit paths log minimal lifecycle fields such as component names, command names, counts, booleans, and exception types. The wrapper swallows audit-log failures so logging cannot break shutdown, tray, hotkey, or watcher paths.

Consequences:
The app now has structured local lifecycle audit hooks without adding packages or changing the SQLite source-of-truth model. Automated tests cover successful lifecycle logging, secret redaction, and logging failure tolerance. Interactive smoke testing is still required before claiming tray, hotkey, watcher, or shutdown behavior works in the user desktop session.

## Decision 0035: Manual Smoke Evidence Uses Ignored Report Templates
Status: Accepted
Date: 2026-05-23

Context:
Milestone 16 requires manual smoke tests to be run or explicitly deferred, and any run needs durable evidence. The repo already has safe fixture generation, but it did not have a structured way to capture a user-run smoke pass without accidentally committing private notes or overwriting evidence.

Decision:
Add `tools/new-smoke-run-report.ps1` to create a manual smoke run-report template under ignored `artifacts/`. The helper refuses output paths outside `artifacts/`, refuses to overwrite existing reports, supports `-PlanOnly`, and starts every test as `Not run` so the template cannot be mistaken for passed evidence.

Consequences:
User-approved or user-run smoke passes now have a repeatable reporting surface. The report helper does not launch the app, create fixtures, mutate user folders, run providers, or prove smoke results by itself.

## Decision 0036: File And Folder Launch Requires Existing Local Filesystem Paths
Status: Accepted
Date: 2026-05-23

Context:
Pre-smoke hardening found that the App-layer launch service delegated arbitrary strings to the Windows shell after confirmation. Retrieval confirmation must not become a way to launch URLs, `shell:` targets, relative paths, file URI strings, or nonexistent paths.

Decision:
Harden `WindowsFileLaunchService` so `OpenFileAsync` opens only existing fully qualified file paths and `OpenFolderAsync` opens only existing fully qualified directory paths. Reject blank strings, relative paths, protocol-like targets, `http`/`https`/`file` URI strings, `shell:` targets, and other non-local shell targets before calling `Process.Start`. Keep OS process launching behind an injectable boundary so tests never launch real processes.

Consequences:
Confirmed retrieval actions remain constrained to local filesystem paths already present on disk. Future support for URLs or special shell targets requires a separate documented decision and safety review.

## Decision 0037: App File Operations Register Own-Operation Suppressions
Status: Accepted
Date: 2026-05-23

Context:
Core triage already supported own-operation suppression records, but pre-smoke hardening found that App-performed move/rename operations were not centrally registered into the watcher processing path.

Decision:
Add a Core `OwnOperationSuppressionRegistry` that records old path, new path, registration time, and suppression window for app-performed moves, renames, and undo moves. `SafeFileOperationExecutor` registers operations around the actual filesystem move. `IntakeWatcherCoordinator` reads active suppressions when building `IntakeProcessingRequest`.

Consequences:
Watcher events caused by app-performed file operations are suppressed by exact old/new path within a short window without hiding unrelated user-created files. The registry is in-memory and intentionally narrow; persistent suppression history is not required for restart recovery.

## Decision 0038: Candidate Queue Uses Lightweight In-Memory Deduplication
Status: Accepted
Date: 2026-05-23

Context:
The candidate queue is intentionally in-memory for the current UI workflow, but duplicate watcher events for the same path can cause repeated prompts before manual smoke testing.

Decision:
Keep the candidate queue in-memory, backed by persistent file-event audit rows, and add lightweight deduplication by normalized path within a short observed/stable window. Do not add a durable candidate queue in this hardening pass.

Consequences:
Duplicate watcher events for the same candidate are suppressed during normal debounce windows, while unrelated paths are unaffected. Restart still loses pending UI candidate state; durable candidate persistence remains a documented follow-up.

## Decision 0039: Minimal Windows CI Validates Build And Tests Only
Status: Accepted
Date: 2026-05-23

Context:
The repository has command-line build/test discipline and package locks. A minimal CI check can catch drift without launching the app, publishing artifacts, or touching user files.

Decision:
Add `.github/workflows/dotnet.yml` using `windows-latest`, `actions/setup-dotnet` for .NET 8, restore through the repo `NuGet.config`, build with `--no-restore`, and run the test suite with `--no-build`. Do not publish artifacts in CI yet.

Consequences:
GitHub pushes and pull requests get the same basic restore/build/test gate expected locally. Packaging, artifact safety checks, and manual smoke evidence remain local/user-approved release gates.
