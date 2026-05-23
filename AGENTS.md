# File Intake Assistant Agent Guide

## Project Purpose
File Intake Assistant is a private Windows productivity application for meaningful file intake, spoken context capture, external metadata management, and safe file retrieval. It watches only explicit user-selected intake folders, captures why a file was downloaded or saved, stores all metadata outside the file in SQLite, and supports later retrieval through text or voice commands.

This is a safety-critical personal productivity layer. It must be durable, testable, conservative, and clear about what it did and did not do.

## Non-Negotiable Safety Rules
- Never delete user files.
- Never overwrite user files.
- Never move or rename user files without explicit user confirmation.
- Always support undo for every move or rename performed by the app.
- Never write private context into user files.
- Never use embedded metadata, alternate data streams, xattrs, or sidecar files as the source of truth.
- Store the source of truth in SQLite under `%LOCALAPPDATA%\File Intake Assistant\`.
- Watch only explicit intake folders selected by the user. Downloads may be the initial default.
- Do not watch the whole computer.
- Do not individually tag every file created inside repos, builds, package folders, archive extractions, compiler outputs, caches, OneDrive sync bursts, or app-generated operations.
- Log every user-visible action, skipped action, failed action, provider call, and filesystem operation.
- Runtime OpenAI API usage must remain optional. The app must work in deterministic/manual mode without an API key.
- Everything CLI or SDK must not be required for v1.

## Repository Layout
```text
AGENTS.md
README.md
Directory.Build.props
Directory.Packages.props
NuGet.config
global.json
.gitignore
docs/
  SPEC.md
  ARCHITECTURE.md
  PLAN.md
  EXECUTION.md
  TESTING.md
  DECISIONS.md
  STATUS.md
  RISK_REGISTER.md
  TRIAGE_RULES.md
  DATA_MODEL.md
  VOICE_WORKFLOWS.md
  SEARCH_RETRIEVAL.md
  SECURITY_PRIVACY.md
  MANUAL_SMOKE_TESTS.md
.codex/
  AGENTS.md
  config.example.toml
src/
  FileIntakeAssistant.App/
  FileIntakeAssistant.Core/
  FileIntakeAssistant.Infrastructure/
tests/
  FileIntakeAssistant.Tests/
tools/
  README.md
```

The `src/` and `tests/` projects are created in Milestone 1. Do not add implementation code during Milestone 0.

## Build And Test Commands
Run these from the repository root:

```powershell
dotnet --info
dotnet restore
dotnet build
dotnet test
git status --short
```

Use filtered test commands when a milestone defines them, such as:

```powershell
dotnet test --filter Triage
dotnet test --filter Stability
dotnet test --filter SafeFileOperations
```

Do not report tests as passing unless the commands were actually run and completed successfully.

## Coding Standards
- Target .NET 8 or newer.
- Use nullable reference types.
- Treat warnings as errors for project code once projects exist.
- Keep domain logic in `FileIntakeAssistant.Core`.
- Keep SQLite, filesystem, logging, provider, and Windows integration code in `FileIntakeAssistant.Infrastructure`.
- Keep WPF tray UI, hotkeys, windows, and view models in `FileIntakeAssistant.App`.
- Use async APIs for IO and provider calls.
- Prefer deterministic rules over opaque behavior.
- Keep external dependencies small and justified in `docs/DECISIONS.md`.
- Use explicit interfaces at provider boundaries.
- Keep tests close to the behavior they protect.

## Dependency Rules
- Use centrally pinned NuGet versions in `Directory.Packages.props`.
- Keep NuGet package lock files enabled.
- Do not add a production dependency without documenting the decision in `docs/DECISIONS.md`.
- Do not add dependencies that require external services for tests.
- OpenAI providers must be optional and disabled unless configured.
- Everything integration must be optional and disabled unless configured.

## File Operation Rules
- All move and rename operations must go through one safe file operation service.
- The service must validate destination paths, sanitize filenames, prevent illegal Windows characters, preserve extensions, and avoid overwrites.
- Conflict handling must generate non-overwriting candidates such as `Name (2).ext`.
- Create destination folders only after confirmation.
- Record an action before and after each operation.
- Record an undo action for every successful move or rename.
- Undo must verify file identity and fail safely if the original path is occupied or the current file no longer matches.
- Automated tests must use temporary directories only. Tests must never mutate real Downloads, Desktop, OneDrive, repo, or project folders.

## Planning And Execution Documents
- `docs/PLAN.md` is the milestone source of truth.
- `docs/EXECUTION.md` is the runbook for how agents work through milestones.
- `docs/STATUS.md` must be updated after each milestone or blocked attempt.
- `docs/DECISIONS.md` must be updated for architecture, dependency, privacy, provider, or safety choices.
- `docs/RISK_REGISTER.md` must be updated when a new risk appears or a mitigation changes.

## Done Means
A milestone is done only when:
- All deliverables in `docs/PLAN.md` are complete.
- Acceptance criteria are met.
- Required automated tests pass.
- Required validation commands were run.
- Required manual smoke tests are either run and documented or explicitly listed as still required.
- `docs/STATUS.md` is updated.
- Any new decisions are recorded in `docs/DECISIONS.md`.
- Any new risks are recorded in `docs/RISK_REGISTER.md`.
- A self-review found no obvious safety, privacy, or scope violations.

## Requires User Confirmation
- Moving a file.
- Renaming a file.
- Creating a destination folder as part of a move.
- Opening multiple files at once.
- Enabling a provider that sends audio, transcript, metadata, or filenames to an external API.
- Changing the privacy model, safety model, watched folder model, or source-of-truth model.
- Adding a large production dependency.
- Running any operation that could affect real user files outside temporary test directories.

## Must Never Be Done
- Do not delete files.
- Do not overwrite files.
- Do not silently move or rename files.
- Do not scan or watch the whole computer.
- Do not store private metadata inside user files.
- Do not use sidecar files as the source of truth.
- Do not require OpenAI or Everything for core app operation.
- Do not log API keys or full secrets.
- Do not fake test results.
- Do not claim a feature works without automated tests or documented manual smoke tests.
- Do not expand scope beyond the active milestone without a decision note.

## Self-Review Before Completion
Before declaring a milestone complete, review:
- Safety invariants.
- Privacy invariants.
- Test coverage for the changed behavior.
- Whether all file operations are temporary or confirmed.
- Whether logs avoid secrets.
- Whether no app logic leaked into the wrong layer.
- Whether the working tree contains only intended changes.
