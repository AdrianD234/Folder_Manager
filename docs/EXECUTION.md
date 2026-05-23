# Execution Runbook

Use this runbook for all implementation work.

## Startup Checklist
Before implementing a milestone, read:

1. `AGENTS.md`
2. `docs/SPEC.md`
3. `docs/ARCHITECTURE.md`
4. `docs/PLAN.md`
5. `docs/TESTING.md`
6. `docs/STATUS.md`
7. `docs/COMPLETION_AUDIT.md` when the repo is past Milestone 12 or when claiming product completion.
8. Any milestone-specific docs such as `docs/TRIAGE_RULES.md`, `docs/DATA_MODEL.md`, `docs/VOICE_WORKFLOWS.md`, or `docs/SEARCH_RETRIEVAL.md`.

Then inspect the current repo state:

```powershell
git status --short --branch
dotnet --info
```

When running inside Codex or another restricted environment, prefer the
repo-local validation script so NuGet and .NET do not read or write the real
Windows profile:

```powershell
.\tools\validate.ps1
```

The script sets `DOTNET_CLI_HOME`, `APPDATA`, `NUGET_PACKAGES`, and
`NUGET_HTTP_CACHE_PATH` to ignored workspace-local folders before running
`dotnet --info`, restore, build, test, and `git status --short`. It disables
the online NuGet audit lookup for this sandbox-safe restore route because
blocked network access otherwise becomes warning-as-error `NU1900`; lock files
remain enabled and authoritative for package versions.

## Work One Milestone At A Time
- Select the current milestone from `docs/STATUS.md`.
- Confirm the milestone exists in `docs/PLAN.md`.
- Implement only the deliverables for that milestone.
- Keep diffs scoped and reviewable.
- Do not pull later milestone work forward unless a decision note explains why it is necessary.
- Do not skip tests.

## Validation Loop
After implementation:

1. Run the milestone validation commands.
2. If validation fails, fix the failure before moving on.
3. Re-run the failed command and any relevant broader command.
4. Update `docs/STATUS.md` with commands run and results.
5. Update `docs/DECISIONS.md` for meaningful design, dependency, provider, privacy, or safety choices.
6. Update `docs/RISK_REGISTER.md` for new risks or changed mitigations.
7. Summarize changed files.
8. State the next milestone.

Do not claim success unless commands were actually run. If a command could not be run, say why and record it in `docs/STATUS.md`.

If restore fails because network access is blocked after using
`tools/validate.ps1`, record the exact blocker in `docs/STATUS.md`. Continue
with `--no-restore` build/test commands only when project, package, and lock
files have not changed.

## Failure Handling
If validation fails:

- Treat the failure as part of the active milestone.
- Fix it before moving to the next milestone.
- If the fix would materially change scope, safety, privacy, provider behavior, or architecture, stop and ask the user.
- Record unresolved failures in `docs/STATUS.md`.

## Scope Control
Do not expand scope without a decision note. Examples that require a decision note:

- Adding a production dependency.
- Changing the database schema beyond `docs/DATA_MODEL.md`.
- Changing provider boundaries.
- Changing how file operation confirmation works.
- Changing where private data is stored.
- Enabling cloud/API behavior by default.
- Making Everything required.
- Watching new folders by default.

## Destructive Ambiguity
Stop and ask the user if a choice could:

- Move, rename, overwrite, or delete real files.
- Change the source-of-truth model.
- Send private metadata, audio, or file content to an external provider.
- Store secrets differently.
- Watch a broader folder scope.
- Reduce confirmation or undo guarantees.

## File Operation Rules During Development
- Automated tests must use temporary directories.
- Manual smoke tests may use a real folder only when the user explicitly chooses it.
- Do not test against real Downloads, Desktop, OneDrive, or repo folders unless the user explicitly authorizes a manual smoke test.
- Never delete files.
- Never overwrite files.

## Status Updates
Update `docs/STATUS.md` after every milestone or blocked attempt with:

- Current milestone.
- Completed milestones.
- Commands run.
- Test status.
- Known issues.
- Next action.
- Blockers.
- Manual tests still required.

## Decision Updates
Update `docs/DECISIONS.md` when:

- A dependency is added.
- A provider behavior changes.
- A schema shape changes.
- A safety/privacy tradeoff is made.
- A default folder, threshold, or retention rule changes.

Use short decision entries with status, context, decision, consequences, and date.

## Risk Updates
Update `docs/RISK_REGISTER.md` when:

- A new risk appears.
- A mitigation is implemented.
- A risk level changes.
- A risk is accepted for a milestone.

## Completion Standard
Do not claim completion until:

- Active milestone acceptance criteria are met.
- Required commands pass or failures are documented as blockers.
- Tests are run honestly.
- Manual smoke tests are run or documented as still required.
- Status, decisions, and risks are updated.
- A self-review confirms no safety, privacy, or scope violation.
