# Completion Audit

Date: 2026-05-23

## Purpose
This audit compares the implemented milestone sequence against the long-running goal in `docs/GOAL.md`, the product requirements in `docs/SPEC.md`, and the manual validation surface in `docs/MANUAL_SMOKE_TESTS.md`.

The milestone sequence through Milestone 12 has build and automated test coverage, but the full product goal is not yet complete. Several user-facing workflow integrations remain missing or unproven by manual smoke tests.

## Summary Verdict
Status: Not complete.

Reason:

- The app builds and automated tests pass.
- Core and Infrastructure safety, persistence, triage, stability, batch, safe-operation, transcription-provider, search, and Everything-provider boundaries exist.
- The WPF app shell exists with tray and hotkey boundaries.
- The integrated tray app now has watcher-driven popup metadata capture, safe filing preview/confirmation UI, undo UI, and confirmed retrieval actions, but manual smoke proof is still missing.
- Real microphone recording is not implemented.
- Manual smoke tests have not been run; Milestone 16 records them as deferred pending user-approved interactive Windows smoke testing.
- Sandbox-safe no-RID framework-dependent publish passes. Runtime-specific `win-x64` publish also passed after approved NuGet access restored the required runtime packs.
- The no-RID framework-dependent publish artifact and runtime-specific publish artifact pass the local privacy/safety artifact check.
- Manual-smoke fixture planning is reproducible through `tools/new-smoke-fixtures.ps1 -PlanOnly`, but no interactive smoke pass has been run.
- Release-readiness documentation consistency is checked by `tools/check-release-readiness.ps1`, but this does not replace manual smoke.
- Structured local JSON-lines logging now mirrors persisted workflow/audit events and app-shell lifecycle events without raw notes, transcripts, provider metadata, audio paths, search/voice command text, or secrets.

The goal must remain active until the remaining workflow gaps are implemented, manually validated, or explicitly accepted as deferred by the user.

## Requirement Audit
| Requirement | Current Evidence | Status |
| --- | --- | --- |
| Windows-native .NET app | WPF app targets `net8.0-windows`; build passes; tray shell files exist. | Partially complete: interactive smoke not run. |
| Tray app architecture | `TrayIconController` exists and app hides to tray on close. | Partially complete: not manually smoke-tested. |
| Global hotkeys | `GlobalHotkeyController` registers a nonfatal command hotkey boundary. | Partially complete: not configurable and not manually smoke-tested. |
| Watch selected intake folders only | Core/Infrastructure watcher tests cover enabled explicit folders and broad-root rejection. Milestone 13 adds SQLite-backed settings UI and app startup/restart watcher wiring. Milestone 14 routes queued candidates into a popup. | Partially complete: not manually smoke-tested. |
| Downloads default suggestion | Disabled Downloads suggestion provider exists. Milestone 13 settings UI can explicitly enable the suggestion. | Partially complete: manual smoke not run. |
| Meaningful one-off triage | Core triage, stability, and batch tests cover major noise classes. | Core complete; app integration incomplete. |
| Suppress compiler/build/repo/package/cache noise | Core triage tests cover dev/build/package noise. | Core complete; app integration incomplete. |
| Batch/extraction/sync suppression | Core batch detector tests cover thresholds and burst classes. Milestone 13 persists processed event audit rows. | Partially complete: batch-level review UI remains incomplete. |
| Manual metadata capture | WPF manual form and SQLite workflow exist; tests prove no file modification/sidecar. | Partially complete: manual UI smoke not run. |
| Download-intake popup | Milestone 14 adds a watcher-driven `IntakePopupWindow` and popup view model for candidate metadata capture and skip/dismiss audit. Milestone 15 adds separate safe filing and undo tabs for confirmed filing. | Partially complete: not manually smoke-tested. |
| Microphone recording | Provider boundary and manual transcript fallback exist. | Incomplete: real microphone recording not implemented. |
| OpenAI STT optional | Disabled-by-default `HttpClient` adapter with fake tests exists. | Complete for provider boundary; UI configuration still incomplete. |
| Local transcription provider | Not-configured placeholder exists. | Complete for placeholder scope. |
| SQLite source of truth | Schema, migrations, repositories, tests, and app DB composition exist. | Complete for implemented workflows. |
| No private metadata in files | Manual metadata tests prove selected file is unchanged and no sidecar is created. | Complete for implemented workflow; must remain invariant. |
| Safe move/rename | Planner/executor/undo tests exist. Milestone 15 adds WPF preview/confirmation and undo UI tests. | Partially complete: automated coverage exists; interactive smoke not run. |
| Never delete/overwrite | Safe-operation tests cover no-overwrite behavior; no delete operation is implemented. | Complete for implemented file-operation path. |
| Voice/text retrieval parser | Deterministic parser and SQLite search tests exist. | Complete for parser/search; UI action execution incomplete. |
| Open single confirmed file/folder | Confirmation outcome model exists. Milestone 15 adds WPF search-result open actions behind confirmation and fake-launcher tests. | Partially complete: automated coverage exists; interactive OS launch smoke not run. |
| Confirm before opening multiple files | Core confirmation policy exists. Milestone 15 adds bulk-open confirmation and cancellation tests. | Partially complete: automated coverage exists; interactive OS launch smoke not run. |
| Everything optional | Disabled-by-default optional CLI provider with fake tests exists. | Complete for provider boundary; UI configuration incomplete. |
| Logging every user-visible/skipped/failed/provider/filesystem operation | SQLite action/search/transcription rows exist for implemented workflows. Milestone 13 adds watcher event audit rows. Milestone 14 adds popup save and skip/dismiss action rows. The app now wraps SQLite persistence with structured local JSON-lines logging for persisted workflow/audit rows, and App shell lifecycle hooks log startup, exit, tray commands, hotkey registration/activation, and watcher restart/dispose events. Logging tests prove raw transcripts, provider metadata, audio paths, private details, and secret-looking values are not mirrored. | Partially complete: automated structured logging coverage exists; full interactive smoke proof remains incomplete. |
| Manual deterministic mode without API key | Manual metadata, manual transcription fallback, deterministic parser, SQLite search, watcher-popup manual transcript capture, safe filing confirmation, undo UI, and confirmed retrieval actions exist. | Partially complete: manual smoke remains incomplete. |
| Manual smoke tests | `docs/MANUAL_SMOKE_TESTS.md` records all current tests as not run and deferred pending user-approved interactive Windows smoke testing. `tools/new-smoke-fixtures.ps1 -PlanOnly` validates a temp-only placeholder fixture layout without creating files. | Incomplete: fixture planning is ready, but the smoke pass itself has not run. |
| Local publish readiness | `tools/publish-local.ps1` produces a no-RID framework-dependent publish under ignored `artifacts/`; `tools/publish-local.ps1 -RuntimeSpecific` produces a runtime-specific `win-x64` publish after approved NuGet access; `tools/check-publish-artifact.ps1` inspected 33 files in the no-RID artifact and 14 files in the runtime-specific artifact with no forbidden private runtime artifacts or secrets; `tools/check-release-readiness.ps1` verifies the release-readiness docs and blocker state. | Complete for local publish readiness; installer, signing, and manual smoke remain separate. |

## Critical Gaps
- Intake folder settings and watcher startup wiring are implemented, but not manually smoke-tested.
- Watcher candidates are connected to candidate popup display, but the popup is not manually smoke-tested.
- Candidate queue is in-memory and backed by persistent event audit rows, but durable candidate state and deduplication are not implemented.
- Safe move/rename preview, confirmation, undo UI, and confirmed retrieval actions are implemented by automated validation but still need interactive Windows smoke testing.
- Real microphone recording is not implemented.
- Provider configuration UI for OpenAI and Everything is not implemented.
- Manual smoke tests are not run and are deferred pending user approval or a user-run smoke pass; a temp-only fixture helper now exists for that approved pass.
- Structured file logging now mirrors persisted workflow/audit rows and App shell lifecycle events without raw private payloads; interactive smoke proof remains incomplete.
- Runtime-specific `win-x64` publish now passes after approved NuGet access; no-RID framework-dependent publish, artifact safety checking, fixture/report planning, and release-readiness consistency checking succeed.

## Safety And Privacy Review
No safety or privacy invariant violation was found in the implemented automated-test surface:

- No tests require real Downloads, Desktop, OneDrive, OpenAI, Everything, or microphone hardware.
- Safe file operation tests use temp directories.
- SQLite tests use temp database paths.
- OpenAI tests use fake HTTP only.
- Everything tests use fake process runners only.
- The app does not delete user files.
- Metadata is not written into user files in the manual metadata workflow.
- The current no-RID publish artifact passed `tools/check-publish-artifact.ps1`; 33 files were inspected and no forbidden app data, databases, logs, temp audio, local settings, repo-local caches, or raw OpenAI-style secrets were found.
- The current runtime-specific `win-x64` publish artifact passed `tools/check-publish-artifact.ps1 -Path .\artifacts\publish\FileIntakeAssistant-win-x64`; 14 files were inspected and no forbidden private runtime artifacts or secrets were found.
- Manual-smoke fixture planning passed through `tools/new-smoke-fixtures.ps1 -PlanOnly` without creating files. The helper itself is constrained to system temp roots, refuses existing roots, and never deletes or overwrites files.
- Release-readiness consistency checking passed through `tools/check-release-readiness.ps1` without launching the app, touching real user files, making provider calls, or requiring network access.
- Structured local logging tests use temp log paths and prove raw private transcript text, provider metadata, audio path names, action detail payloads, lifecycle secret-looking values, and API-key-shaped fields are not mirrored to the local JSON-lines audit log.

Remaining risk is workflow incompleteness rather than a known unsafe implementation.

## Required Next Work
The milestone plan is extended with post-12 remediation milestones:

1. Milestone 13: App Settings, Intake Folder Wiring, And Candidate Queue UI. Status: complete by automated validation; manual smoke still required.
2. Milestone 14: Watcher-Driven Intake Popup And Manual Transcript Workflow. Status: complete by automated validation; manual smoke still required.
3. Milestone 15: Safe Filing Confirmation, Undo UI, And Confirmed Retrieval Actions. Status: complete by automated validation; manual smoke still required.
4. Milestone 16: Interactive Smoke Pass And Release Readiness. Status: automated validation, no-RID publish, runtime-specific publish, publish artifact safety checks, smoke-fixture/report plan validation, and release-readiness consistency checking complete; manual smoke remains externally blocked/deferred.

The active milestone remains Milestone 16 until the user either approves/runs the interactive smoke tests or explicitly accepts them as deferred.

## Completion Rule
Do not mark the long-running goal complete until:

- Post-12 remediation milestones are implemented or explicitly deferred by the user.
- `tools/validate.ps1` passes after the final implementation state.
- Manual smoke tests are run and recorded, or each unrun smoke test is explicitly accepted as deferred by the user.
- `docs/STATUS.md`, `docs/DECISIONS.md`, and `docs/RISK_REGISTER.md` reflect the final state.
