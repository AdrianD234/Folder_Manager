# Status

## Current Milestone
Milestone 16: Interactive Smoke Pass And Release Readiness

Status: In progress. Focused pre-smoke hardening is complete by automated validation. File/folder launch now rejects unsafe shell targets before OS launch, app move/rename/undo operations register own-operation suppressions consumed by watcher intake processing, the in-memory candidate queue has lightweight normalized-path deduplication, structured audit logging has defensive private-payload key redaction, and minimal Windows CI was added for restore/build/test. Sandbox-safe repo-local validation passes through `tools/validate.ps1`; the latest run used workspace-local `.dotnet`, `.appdata`, `.nuget\packages`, and `.nuget\http-cache` paths and passed with 172 tests. No-RID framework-dependent publish remains the current artifact checked by `tools/check-release-readiness.ps1`; previous runtime-specific `win-x64` publish passed after approved NuGet access. Interactive Windows smoke tests are not run and are deferred pending user approval or a user-run smoke pass.

## Completed Milestones
- Milestone 0: Repository Governance And Planning Files.
- Milestone 0.5: Governance And Readiness Review.
- Milestone 0.6: Durable Goal Contract.
- Milestone 1: Solution Skeleton And Build Discipline.
- Milestone 2: Core Domain Models And SQLite Schema.
- Milestone 3: Event Triage Engine.
- Milestone 4: File Stability And Batch Detection.
- Milestone 5: Safe File Operations And Undo.
- Milestone 6: Manual Metadata Capture Workflow.
- Milestone 7: Watcher For Selected Intake Folders.
- Milestone 8: Voice Capture Provider Boundary.
- Milestone 9: Optional OpenAI Transcription Provider.
- Milestone 10: Voice Retrieval Parser And SQLite Search.
- Milestone 11: Optional Everything CLI Integration.
- Milestone 12: Polish, Smoke Tests, And Packaging Notes.
- Post-Milestone Completion Audit: Requirement audit and remediation plan.
- Milestone 13: App Settings, Intake Folder Wiring, And Candidate Queue UI.
- Milestone 14: Watcher-Driven Intake Popup And Manual Transcript Workflow.
- Milestone 15: Safe Filing Confirmation, Undo UI, And Confirmed Retrieval Actions.

## Validation Commands Last Run
Pre-smoke hardening validation from repository root:

```powershell
dotnet build .\FileIntakeAssistant.sln --no-restore
dotnet test .\FileIntakeAssistant.sln --no-build --filter SafeFileOperations --verbosity normal
dotnet test .\FileIntakeAssistant.sln --no-build --filter Watcher --verbosity normal
dotnet test .\FileIntakeAssistant.sln --no-build --filter Logging --verbosity normal
dotnet build .\FileIntakeAssistant.sln --no-restore
dotnet test .\FileIntakeAssistant.sln --no-build --filter SafeFileOperations --verbosity normal
.\tools\validate.ps1
dotnet test .\FileIntakeAssistant.sln --no-build --filter SafeFileOperations --verbosity normal
dotnet test .\FileIntakeAssistant.sln --no-build --filter Watcher --verbosity normal
dotnet test .\FileIntakeAssistant.sln --no-build --filter Search --verbosity normal
dotnet test .\FileIntakeAssistant.sln --no-build --filter Logging --verbosity normal
.\tools\check-release-readiness.ps1
git --no-pager diff --check
git --no-pager status --short --branch
```

Results:

- Required governance docs were read before code changes: `AGENTS.md`, `.codex/AGENTS.md`, `docs/GOAL.md`, `docs/STATUS.md`, `docs/PLAN.md`, `docs/EXECUTION.md`, `docs/TESTING.md`, `docs/SECURITY_PRIVACY.md`, `docs/COMPLETION_AUDIT.md`, `docs/MANUAL_SMOKE_TESTS.md`, `docs/RISK_REGISTER.md`, and `docs/DECISIONS.md`.
- Initial `dotnet build .\FileIntakeAssistant.sln --no-restore` failed with `CS0136` in `JsonLinesLocalAuditLog.cs` after adding private-payload redaction. The variable shadowing was fixed.
- Next `dotnet build .\FileIntakeAssistant.sln --no-restore` failed with missing `System.IO` symbols in `FileLaunchServices.cs`. The missing `using System.IO;` was added.
- Final `dotnet build .\FileIntakeAssistant.sln --no-restore` passed with 0 warnings and 0 errors.
- Initial parallel filtered validation found the new SafeFileOperations own-operation test using a temp path that triage correctly classified as AppData noise for the unrelated-path assertion. The test setup was corrected to use a synthetic intake path for the unrelated candidate. In the same parallel run, `dotnet test .\FileIntakeAssistant.sln --no-build --filter Watcher --verbosity normal` passed with Total tests: 12, Passed: 12, and `dotnet test .\FileIntakeAssistant.sln --no-build --filter Logging --verbosity normal` passed with Total tests: 5, Passed: 5.
- After rebuilding, `dotnet test .\FileIntakeAssistant.sln --no-build --filter SafeFileOperations --verbosity normal` passed with Total tests: 13, Passed: 13.
- After adding the ADS-like target rejection case, `dotnet build .\FileIntakeAssistant.sln --no-restore` passed with 0 warnings and 0 errors.
- Final `.\tools\validate.ps1` passed. SDK `8.0.421` was available, repo `global.json` was detected, restore reported all projects up to date, build passed with 0 warnings and 0 errors, and the full test suite passed with Total tests: 172, Passed: 172. The script used repo-local `.dotnet`, `.appdata`, `.nuget\packages`, and `.nuget\http-cache` paths.
- Required final `dotnet test .\FileIntakeAssistant.sln --no-build --filter SafeFileOperations --verbosity normal` passed with Total tests: 13, Passed: 13.
- Required final `dotnet test .\FileIntakeAssistant.sln --no-build --filter Watcher --verbosity normal` passed with Total tests: 12, Passed: 12.
- Required final `dotnet test .\FileIntakeAssistant.sln --no-build --filter Search --verbosity normal` passed with Total tests: 38, Passed: 38.
- Required final `dotnet test .\FileIntakeAssistant.sln --no-build --filter Logging --verbosity normal` passed with Total tests: 5, Passed: 5.
- `.\tools\check-release-readiness.ps1` passed. It invoked the publish artifact safety checker, which inspected 33 files under `artifacts\publish\FileIntakeAssistant-framework-dependent`, then reported that automated release gates are documented and current blockers remain explicit.
- `git --no-pager diff --check` passed with Git line-ending conversion warnings only.
- `git --no-pager status --short --branch` showed the pre-smoke hardening source, test, CI workflow, and documentation changes pending commit.

Milestone 16 validation from repository root:

```powershell
Get-Content AGENTS.md
Get-Content .codex\AGENTS.md
Get-ChildItem -LiteralPath docs -Filter *.md | Sort-Object Name | ForEach-Object { Get-Content -LiteralPath $_.FullName }
git --no-pager status --short --branch
dotnet --info
dotnet build .\FileIntakeAssistant.sln --no-restore
dotnet test .\FileIntakeAssistant.sln --no-build --filter Logging --verbosity normal
.\tools\validate.ps1
.\tools\publish-local.ps1 -RuntimeSpecific
.\tools\publish-local.ps1
.\tools\check-publish-artifact.ps1
.\tools\check-publish-artifact.ps1 -Path .\artifacts\publish\FileIntakeAssistant-win-x64
.\tools\new-smoke-fixtures.ps1 -PlanOnly
.\tools\new-smoke-run-report.ps1 -PlanOnly
.\tools\check-release-readiness.ps1
.\tools\check-release-readiness.ps1 -ArtifactPath .\artifacts\publish\FileIntakeAssistant-win-x64
rg -n "Milestone 0|Milestone 1|Ready to start|No app implementation|win-x64 build|FileIntakeAssistant-win-x64|framework-dependent|not run|deferred|blocked|RuntimeSpecific|runtime-specific|TODO|NotImplemented|not implemented|not yet" README.md AGENTS.md .codex docs tools
rg -n "delete|overwrite|silent|whole computer|whole user profile|Downloads|OpenAI|Everything|API key|sidecar|metadata" docs\STATUS.md docs\COMPLETION_AUDIT.md docs\MANUAL_SMOKE_TESTS.md docs\PACKAGING.md docs\SECURITY_PRIVACY.md
rg -n "artifacts|\.appdata|\.dotnet|\.nuget|packages.lock.json" .gitignore docs tools README.md
git --no-pager diff --check
git --no-pager status --short --branch
```

Results:

- Repository guidance and all `docs/*.md` files were read before Milestone 16 changes.
- A first attempt to read all docs with `Get-Content -LiteralPath docs\*.md` failed because PowerShell does not expand wildcards for `-LiteralPath`; the docs were then read successfully through `Get-ChildItem -LiteralPath docs -Filter *.md | Sort-Object Name | ForEach-Object { Get-Content -LiteralPath $_.FullName }`.
- `git --no-pager status --short --branch` showed cumulative Milestone 2 through Milestone 15 source, test, lock-file, tooling, and documentation changes pending commit.
- `dotnet --info` passed with SDK `8.0.421`, MSBuild `17.11.48`, Windows Desktop runtime `8.0.27`, and repo `global.json` detected.
- Latest `dotnet build .\FileIntakeAssistant.sln --no-restore` passed with 0 warnings and 0 errors after adding App shell lifecycle audit hooks and tests.
- Latest `.\tools\validate.ps1` rerun for the sandbox-safe NuGet/AppData route passed. The script redirected `DOTNET_CLI_HOME` to `.dotnet`, `APPDATA` to `.appdata`, `NUGET_PACKAGES` to `.nuget\packages`, and `NUGET_HTTP_CACHE_PATH` to `.nuget\http-cache`; `dotnet --info` detected SDK `8.0.421` and the repo `global.json`; restore completed through the repo `NuGet.config`; build passed with 0 warnings and 0 errors; tests passed with Total tests: 156, Passed: 156; and `git status --short` showed only pending source, test, lock-file, tooling, and documentation changes.
- Latest `dotnet test .\FileIntakeAssistant.sln --no-build --filter Logging --verbosity normal` passed. Total tests: 4. Passed: 4.
- Latest `.\tools\publish-local.ps1` passed. It restored with repo-local .NET/NuGet paths, built Release outputs for Core, Infrastructure, and App, published `FileIntakeAssistant.App` to `artifacts\publish\FileIntakeAssistant-framework-dependent\`, and showed repo-local caches, build output, and publish output as ignored.
- Latest unapproved sandbox `.\tools\publish-local.ps1 -RuntimeSpecific` attempt failed with `NU1801`/`NU1101` because NuGet access was blocked and runtime packs were not cached. The command was then rerun with approved NuGet access and passed, publishing `FileIntakeAssistant.App` to `artifacts\publish\FileIntakeAssistant-win-x64\`.
- Latest `.\tools\check-publish-artifact.ps1` passed. It inspected 33 files under `artifacts\publish\FileIntakeAssistant-framework-dependent\` and found no forbidden private runtime artifacts or secrets.
- Latest `.\tools\check-publish-artifact.ps1 -Path .\artifacts\publish\FileIntakeAssistant-win-x64` passed. It inspected 14 files under `artifacts\publish\FileIntakeAssistant-win-x64\` and found no forbidden private runtime artifacts or secrets.
- Latest `.\tools\check-release-readiness.ps1` passed. It invoked the publish artifact safety checker, inspected the same 33-file no-RID artifact, and confirmed automated release gates are documented while current blockers remain explicit.
- Latest `.\tools\check-release-readiness.ps1 -ArtifactPath .\artifacts\publish\FileIntakeAssistant-win-x64` passed. It invoked the publish artifact safety checker against the 14-file runtime-specific artifact and confirmed the same readiness docs.
- Latest `.\tools\new-smoke-fixtures.ps1 -PlanOnly` passed without creating files. It planned root `C:\Users\ADRIAN~1\AppData\Local\Temp\FileIntakeAssistant-Smoke\20260523-182951`, 8 directories, and 64 files.
- Latest `.\tools\new-smoke-run-report.ps1 -PlanOnly` passed without creating files. It planned report path `C:\Users\Adrian Desilvestro\OneDrive\Documents\File Intake Assistant\artifacts\smoke\manual-smoke-report-20260523-183736.md` and fixture root `C:\Users\ADRIAN~1\AppData\Local\Temp\FileIntakeAssistant-Smoke\20260523-183736`.
- Latest `git --no-pager diff --check` passed with Git line-ending conversion warnings only.
- Latest `git --no-pager status --short --branch` showed cumulative implementation, test, package lock, tool, and documentation changes pending commit. No source-control-tracked runtime cache, build output, app-data, or publish artifact appeared in short status.
- `.\tools\validate.ps1` passed using repo-local `.dotnet`, `.appdata`, `.nuget\packages`, and `.nuget\http-cache` paths. SDK `8.0.421` was available, repo `global.json` was detected, restore was up to date, build passed with 0 warnings and 0 errors, and tests passed. Total tests: 152. Passed: 152.
- Initial `.\tools\publish-local.ps1` attempts found two script/readiness issues: PowerShell parsed `-o` as a helper-function parameter, and `--no-restore` publish did not have `net8.0-windows/win-x64` assets. The helper was fixed to pass arguments as arrays and to support an explicit runtime-specific restore path.
- `.\tools\publish-local.ps1 -RuntimeSpecific` failed because runtime-specific restore could not reach `https://api.nuget.org/v3/index.json` and the required runtime-pack packages were not cached. Errors included warning-as-error `NU1801` and `NU1101` for `Microsoft.NETCore.App.Runtime.win-x64`, `Microsoft.WindowsDesktop.App.Runtime.win-x64`, and `Microsoft.AspNetCore.App.Runtime.win-x64`.
- `.\tools\publish-local.ps1` passed as the sandbox-safe no-RID framework-dependent publish fallback. It restored with repo-local .NET/NuGet paths, published `FileIntakeAssistant.App` Release output to `artifacts\publish\FileIntakeAssistant-framework-dependent\`, and showed `artifacts/` as ignored.
- A later attempted parallel `tools/validate.ps1` and `tools/publish-local.ps1` run produced transient MSBuild `MSB4166` child-node failures. The commands were rerun serially; the final serial `.\tools\validate.ps1` run passed with the same 152 passing tests, 0 build warnings, and 0 build errors.
- After README/tooling documentation cleanup, `.\tools\validate.ps1` was rerun serially and passed again. SDK `8.0.421` was available, restore was up to date, build passed with 0 warnings and 0 errors, and tests passed. Total tests: 152. Passed: 152.
- After README/tooling documentation cleanup, `.\tools\publish-local.ps1` was rerun and passed. It restored with repo-local .NET/NuGet paths, published `FileIntakeAssistant.App` Release output to `artifacts\publish\FileIntakeAssistant-framework-dependent\`, and showed `.appdata/`, `.dotnet/`, `.nuget/`, `artifacts/`, `bin/`, and `obj/` as ignored.
- `.\tools\check-publish-artifact.ps1` passed after the no-RID publish. It inspected 33 files under `artifacts\publish\FileIntakeAssistant-framework-dependent\` and found no forbidden private app-data folders, SQLite databases, logs, temp audio files, local settings, repo-local cache folders, or raw OpenAI-style secrets.
- A Milestone 16 documentation drift scan found no safety/privacy rule conflicts. It did find current-agent wording in `AGENTS.md` that still framed `src/` and `tests/` as future Milestone 1 deliverables, and it found that the manual smoke approval boundary was implicit rather than operationally explicit.
- `AGENTS.md` now says those projects were created in Milestone 1 and that `docs/STATUS.md` controls active milestone scope.
- `docs/MANUAL_SMOKE_TESTS.md` now includes an explicit interactive-smoke approval boundary, default `%TEMP%\FileIntakeAssistant-Smoke\...` test roots, recording requirements, and stop conditions.
- `docs/RISK_REGISTER.md` now reflects that the manual-smoke approval boundary mitigates UI-claims risk.
- After those readiness-doc updates, `.\tools\validate.ps1` was rerun and passed. SDK `8.0.421` was available, restore was up to date, build passed with 0 warnings and 0 errors, and tests passed. Total tests: 152. Passed: 152.
- After those readiness-doc updates, `.\tools\publish-local.ps1` was rerun and passed. It published `FileIntakeAssistant.App` Release output to `artifacts\publish\FileIntakeAssistant-framework-dependent\` and showed repo-local caches, publish output, and build output as ignored.
- After adding the artifact safety checker, `.\tools\validate.ps1` was rerun and passed. SDK `8.0.421` was available, restore was up to date, build passed with 0 warnings and 0 errors, and tests passed. Total tests: 152. Passed: 152.
- After adding the artifact safety checker, `.\tools\publish-local.ps1` was rerun and passed. It published `FileIntakeAssistant.App` Release output to `artifacts\publish\FileIntakeAssistant-framework-dependent\`.
- After that publish, `.\tools\check-publish-artifact.ps1` passed. It inspected 33 files under `artifacts\publish\FileIntakeAssistant-framework-dependent\` and found no forbidden private app-data folders, SQLite databases, logs, temp audio files, local settings, repo-local cache folders, or raw OpenAI-style secrets.
- `tools/new-smoke-fixtures.ps1` was added as a manual-smoke fixture helper. It defaults to a timestamped root under the system temp directory, refuses non-temp roots, refuses existing roots, creates placeholder files only, never deletes or overwrites files, and does not launch the app or run the smoke pass.
- `.\tools\new-smoke-fixtures.ps1 -PlanOnly` passed without creating files. It planned root `C:\Users\ADRIAN~1\AppData\Local\Temp\FileIntakeAssistant-Smoke\20260523-181032`, 8 directories, and 64 files, including a temp intake folder and temp file-operation source/destination folders.
- After adding the fixture helper and docs, `.\tools\validate.ps1` was rerun and passed. SDK `8.0.421` was available, restore was up to date, build passed with 0 warnings and 0 errors, and tests passed. Total tests: 152. Passed: 152.
- After adding the fixture helper and docs, `.\tools\publish-local.ps1` was rerun and passed. It published `FileIntakeAssistant.App` Release output to `artifacts\publish\FileIntakeAssistant-framework-dependent\`.
- After that publish, `.\tools\check-publish-artifact.ps1` was rerun and passed. It inspected 33 files under `artifacts\publish\FileIntakeAssistant-framework-dependent\` and found no forbidden private runtime artifacts or secrets.
- `tools/check-release-readiness.ps1` was added as a read-only Milestone 16 consistency gate. It verifies required release-readiness docs and tools exist, manual-smoke deferral remains explicit, the runtime-specific publish blocker remains explicit, local runtime/cache paths remain ignored, risk entries are present, and the publish artifact safety checker passes.
- The first `.\tools\check-release-readiness.ps1` run failed because the checker patterns were too literal around backticked `win-x64` text and because it treated a successful PowerShell helper as failed through stale `$LASTEXITCODE`. The checker was fixed to use less brittle runtime-publish patterns and to rely on PowerShell exception behavior for the artifact checker.
- After that fix, `.\tools\check-release-readiness.ps1` passed. It also invoked `tools/check-publish-artifact.ps1`, which inspected 33 files under `artifacts\publish\FileIntakeAssistant-framework-dependent\` and passed.
- After adding the release-readiness checker and docs, `.\tools\validate.ps1` was rerun and passed. SDK `8.0.421` was available, restore was up to date, build passed with 0 warnings and 0 errors, and tests passed. Total tests: 152. Passed: 152.
- After adding the release-readiness checker and docs, `.\tools\publish-local.ps1` was rerun and passed. It published `FileIntakeAssistant.App` Release output to `artifacts\publish\FileIntakeAssistant-framework-dependent\`.
- After that publish, `.\tools\check-publish-artifact.ps1` was rerun and passed. It inspected 33 files under `artifacts\publish\FileIntakeAssistant-framework-dependent\` and found no forbidden private runtime artifacts or secrets.
- `.\tools\check-release-readiness.ps1` was rerun after the publish and passed. It confirmed automated release gates are documented and current blockers remain explicit.
- `.\tools\new-smoke-fixtures.ps1 -PlanOnly` was rerun and passed without creating files. It planned root `C:\Users\ADRIAN~1\AppData\Local\Temp\FileIntakeAssistant-Smoke\20260523-181518`, 8 directories, and 64 files.
- `git --no-pager diff --check` passed with Git line-ending conversion warnings only.
- Final `git --no-pager status --short --branch` showed cumulative implementation, test, package lock, tool, and documentation changes pending commit, including `tools/validate.ps1`, `tools/publish-local.ps1`, `tools/check-publish-artifact.ps1`, `tools/new-smoke-fixtures.ps1`, and `tools/check-release-readiness.ps1`.
- Interactive manual smoke tests were not run. They require launching the WPF app in the user session and using explicit temporary intake/file-operation folders. Per the goal contract, Codex did not run them without user approval.

Milestone 16 coverage includes:

- Release-readiness docs now distinguish automated validation, no-RID publish, runtime-specific publish, and interactive smoke status.
- `tools/validate.ps1` provides the preferred sandbox-safe validation route and avoids reading or writing the real Windows profile NuGet/AppData paths.
- `tools/publish-local.ps1` provides a repo-local publish helper that avoids real profile NuGet/AppData paths and keeps output under ignored `artifacts/`.
- `tools/check-publish-artifact.ps1` provides a read-only privacy/safety gate for publish output and passed against the current no-RID framework-dependent artifact.
- `tools/new-smoke-fixtures.ps1` provides a temp-only placeholder fixture generator for approved manual smoke passes and passed in `-PlanOnly` mode without creating files.
- `tools/new-smoke-run-report.ps1` provides an ignored manual smoke run-report template helper for approved or user-run smoke passes and passed in `-PlanOnly` mode without creating files.
- `tools/check-release-readiness.ps1` provides a read-only consistency gate for Milestone 16 docs, blocker state, ignored runtime/cache paths, risk entries, manual-smoke fixture/report helpers, runtime-specific publish status, and publish artifact safety.
- `README.md` and `tools/README.md` now point to the repo-local validation/publish helpers and describe the default no-RID publish fallback consistently.
- `AGENTS.md` now points current agents at `docs/STATUS.md` for active milestone scope instead of leaving the historical Milestone 0 implementation restriction ambiguous.
- `docs/MANUAL_SMOKE_TESTS.md` marks every smoke test as not run and explicitly deferred pending user-approved interactive Windows smoke testing.
- `docs/MANUAL_SMOKE_TESTS.md` now documents exactly what approval is needed before an interactive smoke pass, what temp roots to use by default, and which safety/privacy events stop the pass.
- `docs/COMPLETION_AUDIT.md` records that the long-running goal remains incomplete until manual smoke is completed or explicitly user-deferred.
- `docs/PACKAGING.md` documents the no-RID fallback, runtime-specific publish requirement, and artifact ignore boundary.
- `docs/RISK_REGISTER.md` records the resolved runtime-pack publish risk, publish artifact privacy risk, smoke fixture/report risks, and release-readiness documentation drift risk while keeping UI smoke claims constrained to automated evidence.
- App shell lifecycle logging now records startup, exit, tray command, hotkey registration/activation, and watcher restart/dispose events through the same local JSON-lines audit boundary, with tests proving secret redaction and failure tolerance.

Milestone 15 validation from repository root:

```powershell
dotnet build .\FileIntakeAssistant.sln --no-restore
dotnet test .\FileIntakeAssistant.sln --no-build --filter SafeFileOperations --verbosity normal
dotnet test .\FileIntakeAssistant.sln --no-build --filter Undo --verbosity normal
dotnet test .\FileIntakeAssistant.sln --no-build --filter Search --verbosity normal
.\tools\validate.ps1
git --no-pager diff --check
git --no-pager status --short --branch
```

Results:

- `dotnet build .\FileIntakeAssistant.sln --no-restore` passed with 0 warnings and 0 errors after adding the Milestone 15 App view models, confirmation services, SQLite undo listing, and tests.
- `dotnet test .\FileIntakeAssistant.sln --no-build --filter SafeFileOperations --verbosity normal` passed. Total tests: 11. Passed: 11.
- `dotnet test .\FileIntakeAssistant.sln --no-build --filter Undo --verbosity normal` passed. Total tests: 9. Passed: 9.
- `dotnet test .\FileIntakeAssistant.sln --no-build --filter Search --verbosity normal` passed. Total tests: 27. Passed: 27.
- `tools/validate.ps1` passed. SDK `8.0.421` was available, repo `global.json` was detected, restore was up to date, build passed with 0 warnings and 0 errors, and tests passed. Total tests: 152. Passed: 152.
- `git --no-pager diff --check` passed with Git line-ending conversion warnings only.
- `git --no-pager status --short --branch` showed cumulative Milestone 2 through Milestone 15 source, test, project, lock-file, validation-script, packaging documentation, completion-audit documentation, and governance documentation changes pending commit.

Milestone 15 coverage includes:

- WPF Filing tab with deterministic safe move/rename preview showing source path, destination path, sanitized filename, conflict-resolved destination, destination-folder creation state, extension preservation state, and explicit confirmation requirement.
- Move/rename confirmation path uses `SafeFileOperationExecutor`; confirmation refusal logs a cancelled action and performs no filesystem mutation or destination folder creation.
- WPF Undo tab lists pending SQLite undo actions and runs undo through the safe executor after confirmation.
- Undo UI workflow logs performed, cancelled, and failed undo audit actions.
- Search tab now displays selectable structured results and exposes confirmed open-file, open-containing-folder, and bulk-open actions.
- Search open actions require confirmation, log confirmed/cancelled/failed actions, and do not blindly open ambiguous or multi-result commands.
- Automated tests prove preview state, refusal without mutation, confirmed move/undo behavior, undo conflict failure, undo identity mismatch failure, search single open, search bulk cancellation, ambiguous multi-result display, and containing-folder launch through fake services.
- No delete, overwrite, silent move/rename, OpenAI, Everything, microphone, live API call, real Downloads, Desktop, OneDrive, repo, AppData, Program Files, Windows, or user-profile mutation was added or required.

Milestone 14 validation from repository root:

```powershell
dotnet build .\FileIntakeAssistant.sln --no-restore
dotnet test .\FileIntakeAssistant.sln --no-build --filter ManualMetadata --verbosity normal
dotnet test .\FileIntakeAssistant.sln --no-build --filter Transcription --verbosity normal
dotnet test .\FileIntakeAssistant.sln --no-build --filter IntakePopup --verbosity normal
.\tools\validate.ps1
dotnet test .\FileIntakeAssistant.sln --no-build --filter ManualMetadata --verbosity normal
dotnet test .\FileIntakeAssistant.sln --no-build --filter Transcription --verbosity normal
dotnet test .\FileIntakeAssistant.sln --no-build --filter IntakePopup --verbosity normal
git --no-pager diff --check
git --no-pager status --short --branch
```

Results:

- `dotnet build .\FileIntakeAssistant.sln --no-restore` passed with 0 warnings and 0 errors.
- `dotnet test .\FileIntakeAssistant.sln --no-build --filter ManualMetadata --verbosity normal` passed. Total tests: 4. Passed: 4.
- `dotnet test .\FileIntakeAssistant.sln --no-build --filter Transcription --verbosity normal` passed. Total tests: 12. Passed: 12.
- Initial `dotnet test .\FileIntakeAssistant.sln --no-build --filter IntakePopup --verbosity normal` failed because two new popup workflow tests used candidate `source_intake_folder_id` values that did not exist in the temp SQLite database, correctly triggering foreign-key enforcement. The tests were fixed to seed valid `intake_folders` rows rather than weakening the schema path.
- `dotnet build .\FileIntakeAssistant.sln --no-restore` passed after the test fix with 0 warnings and 0 errors.
- `dotnet test .\FileIntakeAssistant.sln --no-build --filter IntakePopup --verbosity normal` then passed. Total tests: 5. Passed: 5.
- `tools/validate.ps1` passed after repo-local restore. SDK `8.0.421` was available, repo `global.json` was detected, restore was up to date, build passed with 0 warnings and 0 errors, and tests passed. Total tests: 142. Passed: 142.
- Final `dotnet test .\FileIntakeAssistant.sln --no-build --filter ManualMetadata --verbosity normal` passed. Total tests: 4. Passed: 4.
- Final `dotnet test .\FileIntakeAssistant.sln --no-build --filter Transcription --verbosity normal` passed. Total tests: 12. Passed: 12.
- Final `dotnet test .\FileIntakeAssistant.sln --no-build --filter IntakePopup --verbosity normal` passed. Total tests: 5. Passed: 5.
- `git --no-pager diff --check` passed with Git line-ending conversion warnings only.
- `git --no-pager status --short --branch` showed cumulative Milestone 2 through Milestone 14 source, test, project, lock-file, validation-script, packaging documentation, completion-audit documentation, and governance documentation changes pending commit.

Milestone 14 coverage includes:

- Core `IntakeCandidateWorkflowService` saves watcher candidate metadata by composing the existing manual file snapshot reader, manual metadata capture service, and manual text transcription workflow.
- Candidate metadata saves create SQLite `file_records`, `metadata_entries`, `transcription_jobs` when reviewed transcript text is present, and completed `actions` rows with the `IntakeCandidateMetadataSaved` action type.
- Candidate skip/dismiss creates a completed `IntakeCandidateSkipped` action row without creating file records or metadata entries.
- WPF `IntakeCandidatePopupViewModel` exposes candidate file name, path, extension, size, triage reason/category/confidence, stability evidence, batch evidence, manual note/transcript fields, relevance/project/topic/tags/source URL fields, and a provider status message that keeps OpenAI/local transcription disabled unless configured.
- WPF `IntakePopupWindow` provides the dismissible candidate popup and logs window-close dismissal through the skip path.
- App startup composition now routes watcher-queued candidates into a modeless intake popup after candidate/audit refresh. It dequeues one popup at a time and refreshes the candidate queue after save/skip.
- Tests prove candidate-to-popup state, save from candidate, skip/dismiss audit logging, no-key manual transcript fallback, and unchanged target temp file content/timestamp with no sidecar file.
- No move, rename, overwrite, delete, OpenAI, Everything, microphone, live API call, real Downloads, Desktop, OneDrive, repo, AppData, Program Files, Windows, or user-profile mutation was added or required.

Milestone 13 validation from repository root:

```powershell
dotnet build .\FileIntakeAssistant.sln --no-restore
dotnet test .\FileIntakeAssistant.sln --no-build --filter Intake --verbosity normal
.\tools\validate.ps1
dotnet test .\FileIntakeAssistant.sln --no-build --filter Watcher --verbosity normal
dotnet test .\FileIntakeAssistant.sln --no-build --filter Intake --verbosity normal
git --no-pager diff --check
git --no-pager status --short --branch
```

Results:

- Initial `dotnet build .\FileIntakeAssistant.sln --no-restore` failed because the test project target changed to `net8.0-windows` and restore had not yet regenerated `project.assets.json` for that target.
- The same build also found missing `System.IO` imports in the new App-layer intake folder settings and watcher coordinator code. The imports were added.
- Initial `dotnet test .\FileIntakeAssistant.sln --no-build --filter Intake --verbosity normal` failed because the new `net8.0-windows` test binary had not been built yet.
- `tools/validate.ps1` passed after repo-local restore. SDK `8.0.421` was available, repo `global.json` was detected, the test project restored for `net8.0-windows`, build passed with 0 warnings and 0 errors, and tests passed. Total tests: 137. Passed: 137.
- `dotnet test .\FileIntakeAssistant.sln --no-build --filter Watcher --verbosity normal` passed. Total tests: 10. Passed: 10.
- `dotnet test .\FileIntakeAssistant.sln --no-build --filter Intake --verbosity normal` passed. Total tests: 137. Passed: 137. The filter currently matches the full assembly name as well as intake-specific tests, so it exercised the full suite.
- `git --no-pager diff --check` passed with Git line-ending conversion warnings only.
- `git --no-pager status --short --branch` showed cumulative Milestone 2 through Milestone 13 source, test, project, lock-file, validation-script, packaging documentation, completion-audit documentation, and governance documentation changes pending commit.

Milestone 13 coverage includes:

- Core `IntakeFolderPathValidator` rejects drive roots, whole user-profile roots, configured protected roots, development/build/package/cache folder segments, and repository roots when a `.git` marker is detected.
- Core `IntakeFolderSettingsService` lists SQLite-backed intake folders with a disabled Downloads suggestion, adds or updates explicit folders, enables/disables folders, and treats remove as "disable from active watch list" rather than deleting a record.
- Core `AuditedIntakeEventProcessor` wraps intake processing and writes `file_events` audit rows for ignored and candidate outcomes.
- Infrastructure `SqliteFileIntakeStore` can list recent file-event audit rows.
- Infrastructure watcher construction now uses the shared path validator for broad-root and repo/build/package rejection while still allowing temp-directory tests.
- WPF App composition now creates intake folder settings and candidate queue view models, starts/restarts a configured watcher from enabled SQLite `intake_folders`, and refreshes the candidate/audit UI after processed watcher events.
- WPF UI now has `Folders` and `Candidates` tabs for explicit intake folder management, disabled Downloads suggestion enablement, candidate queue display, and recent event audit display.
- Tests cover view-model add/enable/disable/remove behavior, disabled Downloads suggestion enablement, broad-root/repository-marker rejection before watcher construction, ignored event audit rows that do not enter the queue, and meaningful candidate audit rows that appear in the UI-facing queue.
- No move, rename, overwrite, delete, OpenAI, Everything, microphone, live API call, real Downloads, Desktop, OneDrive, repo, AppData, Program Files, Windows, or user-profile mutation was added or required.

Post-Milestone completion audit validation from repository root:

```powershell
Get-Content -Path AGENTS.md
Get-Content -Path .codex\AGENTS.md
Get-Content -Path docs\STATUS.md
Get-Content -Path docs\GOAL.md
Get-Content -Path docs\PLAN.md
Get-Content -Path docs\SPEC.md
Get-Content -Path docs\ARCHITECTURE.md
Get-Content -Path docs\EXECUTION.md
Get-Content -Path docs\TESTING.md
Get-Content -Path docs\RISK_REGISTER.md
Get-Content -Path docs\DATA_MODEL.md
Get-Content -Path docs\TRIAGE_RULES.md
Get-Content -Path docs\SECURITY_PRIVACY.md
Get-Content -Path docs\VOICE_WORKFLOWS.md
Get-Content -Path docs\SEARCH_RETRIEVAL.md
Get-Content -Path docs\MANUAL_SMOKE_TESTS.md
Get-Content -Path docs\DECISIONS.md
Get-Content -Path docs\PACKAGING.md
rg -n "ConfiguredIntakeFolderWatcher|CandidateQueue|ManualMetadataCaptureService|SafeFileOperation|Undo|TranscriptionWorkflowService|OpenAiTranscriptionProvider|SearchWorkflowService|EverythingCliSearchProvider|TrayIconController|GlobalHotkeyController" src tests
rg -n "TODO|NotImplemented|not implemented|not yet|NotConfigured|Manual smoke|Status: Not run|not run|does not directly open|not wired" src docs tests
rg --files src tests
git --no-pager diff --stat
.\tools\validate.ps1
git --no-pager diff --check
git --no-pager status --short --branch
```

Results:

- Repo and Codex guidance are consistent: `docs/STATUS.md` is the active milestone source of truth and `docs/GOAL.md` is the long-running goal contract.
- `docs/COMPLETION_AUDIT.md` was added to compare implementation evidence against `docs/GOAL.md`, `docs/SPEC.md`, and `docs/MANUAL_SMOKE_TESTS.md`.
- `docs/PLAN.md` was extended with Milestones 13 through 16 to cover post-12 remediation work.
- The audit found no documented safety or privacy invariant violation in the automated-test surface.
- The audit found the overall product goal is not complete because several integrated user workflows remain incomplete or unproven by manual smoke tests.
- `tools/validate.ps1` passed. SDK `8.0.421` was available, repo `global.json` was detected, restore was up to date, build passed with 0 warnings and 0 errors, and tests passed. Total tests: 132. Passed: 132.
- `git --no-pager diff --check` passed with Git line-ending conversion warnings only.
- `git --no-pager status --short --branch` showed cumulative Milestone 2 through Milestone 12 source/test/project/lock/documentation changes plus the new completion audit and plan-extension documentation pending commit.
- Milestone 13 is ready to start.

Milestone 12 validation from repository root:

```powershell
dotnet build .\FileIntakeAssistant.sln --no-restore
dotnet test .\FileIntakeAssistant.sln --no-build --verbosity normal
.\tools\validate.ps1
git --no-pager diff --check
git --no-pager status --short --branch
```

Results:

- Initial `dotnet build .\FileIntakeAssistant.sln --no-restore` failed after enabling Windows Forms support because `Application`, `MessageBox`, and `OpenFileDialog` became ambiguous between WPF and Windows Forms namespaces.
- The namespace ambiguity was fixed by explicitly using `System.Windows.Application`, `System.Windows.MessageBox`, and `Microsoft.Win32.OpenFileDialog`.
- `dotnet build .\FileIntakeAssistant.sln --no-restore` then passed with 0 warnings and 0 errors.
- `dotnet test .\FileIntakeAssistant.sln --no-build --verbosity normal` passed. Total tests: 132. Passed: 132.
- `tools/validate.ps1` passed. SDK `8.0.421` was available, repo `global.json` was detected, restore updated the WPF app after the project setting change, build passed with 0 warnings and 0 errors, and test passed. Total tests: 132. Passed: 132.
- `git status --short` inside `tools/validate.ps1` showed cumulative Milestone 2 through Milestone 12 source, test, project, lock-file, validation-script, and documentation changes pending commit.
- `git --no-pager diff --check` passed with Git line-ending conversion warnings only.
- `git --no-pager status --short --branch` showed cumulative Milestone 2 through Milestone 12 source, test, project, lock-file, validation-script, packaging documentation, and governance documentation changes pending commit.

Milestone 12 coverage includes:

- WPF app project now enables Windows Forms support for tray integration without adding a NuGet dependency.
- App-layer tray shell added with Open, Search, and Exit tray menu commands.
- Main window now hides to tray on close and exits only through the explicit app exit path.
- App-layer global hotkey boundary added with nonfatal registration behavior; the command hotkey shows the Search tab when registered.
- Architecture tests verify WPF, Windows Forms, and tray/hotkey shell files.
- `docs/PACKAGING.md` documents local publish commands, release gates, installer work not yet implemented, and packaging limitations.
- `docs/MANUAL_SMOKE_TESTS.md` clearly records that Milestone 12 interactive smoke tests were not run in this validation pass and why.
- No real Downloads, Desktop, OneDrive, microphone, OpenAI, Everything, or real user-file operations were used in automated validation.

Milestone 11 validation from repository root:

```powershell
dotnet build .\FileIntakeAssistant.sln --no-restore
dotnet test .\FileIntakeAssistant.sln --no-build --filter Everything --verbosity normal
dotnet test .\FileIntakeAssistant.sln --no-build --verbosity normal
.\tools\validate.ps1
git --no-pager diff --check
git --no-pager status --short --branch
```

Results:

- `dotnet build .\FileIntakeAssistant.sln --no-restore` passed with 0 warnings and 0 errors.
- `dotnet test .\FileIntakeAssistant.sln --no-build --filter Everything --verbosity normal` passed. Total tests: 5. Passed: 5.
- `dotnet test .\FileIntakeAssistant.sln --no-build --verbosity normal` passed. Total tests: 131. Passed: 131.
- `tools/validate.ps1` passed. SDK `8.0.421` was available, repo `global.json` was detected, restore was up to date, build passed with 0 warnings and 0 errors, and test passed. Total tests: 131. Passed: 131.
- `git status --short` inside `tools/validate.ps1` showed cumulative Milestone 2 through Milestone 11 source, test, project, lock-file, validation-script, and documentation changes pending commit.
- `git --no-pager diff --check` passed with Git line-ending conversion warnings only.
- `git --no-pager status --short --branch` showed cumulative Milestone 2 through Milestone 11 source, test, project, lock-file, validation-script, and documentation changes pending commit.

Milestone 11 coverage includes:

- Infrastructure `EverythingCliSearchProvider` implements optional Everything CLI search behind the existing `IFileSearchProvider` interface.
- Everything provider is disabled by default and returns no results without invoking any process.
- Missing `es.exe` returns no results without failure.
- Enabled Everything without configured allowed roots returns no results without invoking any process.
- Configured/discovered path handling is isolated behind an `IEverythingCliPathResolver`.
- CLI execution is isolated behind an `IEverythingCliProcessRunner`, with tests using fakes only.
- Fake `es.exe` line-output parsing covers path results, extension filtering, keyword matching, and allowed-root filtering.
- `CompositeFileSearchProvider` merges optional Everything path hits with SQLite metadata results while preserving SQLite record ids and metadata fields.
- Everything-only results can enrich discovery only when no SQLite-only metadata filters are required.
- The WPF app composes SQLite search with an Everything provider that remains disabled by default.
- No Everything SDK dependency was added.
- No automated test requires real Everything, real `es.exe`, broad filesystem scanning, live provider calls, or real user folders.

Milestone 10 validation from repository root:

```powershell
dotnet build .\FileIntakeAssistant.sln --no-restore
dotnet test .\FileIntakeAssistant.sln --no-build --filter Search --verbosity normal
dotnet test .\FileIntakeAssistant.sln --no-build --filter VoiceCommand --verbosity normal
dotnet test .\FileIntakeAssistant.sln --no-build --verbosity normal
.\tools\validate.ps1
git --no-pager diff --check
git --no-pager status --short --branch
```

Results:

- `dotnet build .\FileIntakeAssistant.sln --no-restore` passed with 0 warnings and 0 errors.
- `dotnet test .\FileIntakeAssistant.sln --no-build --filter Search --verbosity normal` passed. Total tests: 18. Passed: 18.
- `dotnet test .\FileIntakeAssistant.sln --no-build --filter VoiceCommand --verbosity normal` passed. Total tests: 3. Passed: 3.
- `dotnet test .\FileIntakeAssistant.sln --no-build --verbosity normal` passed. Total tests: 126. Passed: 126.
- `tools/validate.ps1` passed. SDK `8.0.421` was available, repo `global.json` was detected, restore was up to date, build passed with 0 warnings and 0 errors, and test passed. Total tests: 126. Passed: 126.
- `git status --short` inside `tools/validate.ps1` showed cumulative Milestone 2 through Milestone 10 source, test, project, lock-file, validation-script, and documentation changes pending commit.
- `git --no-pager diff --check` passed with Git line-ending conversion warnings only.
- `git --no-pager status --short --branch` showed cumulative Milestone 2 through Milestone 10 source, test, project, lock-file, validation-script, and documentation changes pending commit.

Milestone 10 coverage includes:

- Deterministic Core search intent parsing for supported voice/text commands.
- Stable intent JSON for parser outputs.
- Search date parsing for today, yesterday, this week, last week, last N weeks, last month, and last year.
- Destructive commands are classified as unsupported and do not execute search or file operations.
- SQLite-first search across file records and metadata entries for recency, file type, relevance, keyword, and downloaded/source-intake filters.
- Folder-level search across folder records and metadata for open-folder commands.
- Workflow logging to `voice_commands` and `search_queries`.
- Multiple open results require confirmation instead of blind opening.
- The WPF shell now includes a minimal Search tab that displays ranked results and confirmation status; it does not open files or folders automatically.
- No Everything CLI, OpenAI LLM, broad filesystem scan, move, rename, delete, or overwrite path is required.

Milestone 9 validation from repository root:

```powershell
dotnet build .\FileIntakeAssistant.sln --no-restore
dotnet test .\FileIntakeAssistant.sln --no-build --filter OpenAi --verbosity normal
dotnet build .\FileIntakeAssistant.sln --no-restore
dotnet test .\FileIntakeAssistant.sln --no-build --filter OpenAi --verbosity normal
dotnet test .\FileIntakeAssistant.sln --no-build --verbosity normal
.\tools\validate.ps1
git --no-pager diff --check
git --no-pager status --short --branch
```

Results:

- Initial `dotnet build .\FileIntakeAssistant.sln --no-restore` passed with 0 warnings and 0 errors.
- Initial `dotnet test .\FileIntakeAssistant.sln --no-build --filter OpenAi --verbosity normal` passed. Total tests: 5. Passed: 5.
- `dotnet build .\FileIntakeAssistant.sln --no-restore` passed with 0 warnings and 0 errors after a metadata-host refinement.
- `dotnet test .\FileIntakeAssistant.sln --no-build --filter OpenAi --verbosity normal` passed. Total tests: 5. Passed: 5.
- `dotnet test .\FileIntakeAssistant.sln --no-build --verbosity normal` passed. Total tests: 108. Passed: 108.
- `tools/validate.ps1` passed. SDK `8.0.421` was available, repo `global.json` was detected, restore was up to date, build passed with 0 warnings and 0 errors, and test passed. Total tests: 108. Passed: 108.
- `git status --short` inside `tools/validate.ps1` showed cumulative Milestone 2 through Milestone 9 source, test, project, lock-file, validation-script, and documentation changes pending commit.
- `git --no-pager diff --check` passed with Git line-ending conversion warnings only.
- `git --no-pager status --short --branch` showed cumulative Milestone 2 through Milestone 9 source, test, project, lock-file, validation-script, and documentation changes pending commit.

Milestone 9 coverage includes:

- Infrastructure `OpenAiTranscriptionProvider` implements `ITranscriptionProvider` using `HttpClient` without adding an OpenAI SDK dependency.
- OpenAI provider is disabled by default and reports `NotConfigured` without making HTTP calls.
- API key lookup uses an environment-variable provider, defaulting to `OPENAI_API_KEY`.
- Missing API key reports `NotConfigured` and does not call HTTP.
- Fake HTTP success posts multipart audio, model, response format, and optional language fields and returns transcript text.
- Provider metadata stores only safe operational metadata such as provider, model, endpoint host, and HTTP status.
- Provider error responses and exception messages are redacted before they can be persisted in transcription job error fields.
- No automated test calls live OpenAI or requires a real API key.
- OpenAI success can use the existing temp-audio retention policy, which deletes successful temp audio by default inside the configured temp-audio root.

Milestone 7 validation from repository root:

```powershell
dotnet test .\FileIntakeAssistant.sln --no-build --filter Watcher --verbosity normal
dotnet build .\FileIntakeAssistant.sln --no-restore
dotnet test .\FileIntakeAssistant.sln --no-build --filter Watcher --verbosity normal
dotnet test .\FileIntakeAssistant.sln --no-restore --filter Watcher --verbosity normal
dotnet test .\FileIntakeAssistant.sln --no-build --verbosity normal
.\tools\validate.ps1
git --no-pager diff --check
git --no-pager status --short --branch
```

Results:

- The first `dotnet test .\FileIntakeAssistant.sln --no-build --filter Watcher --verbosity normal` ran against the previous test assembly and matched no tests. This was expected after adding new test files before rebuilding.
- `dotnet build .\FileIntakeAssistant.sln --no-restore` passed with 0 warnings and 0 errors.
- `dotnet test .\FileIntakeAssistant.sln --no-build --filter Watcher --verbosity normal` passed. Total tests: 9. Passed: 9.
- `dotnet test .\FileIntakeAssistant.sln --no-restore --filter Watcher --verbosity normal` passed. Total tests: 9. Passed: 9.
- `dotnet test .\FileIntakeAssistant.sln --no-build --verbosity normal` passed. Total tests: 96. Passed: 96.
- `tools/validate.ps1` passed. Restore was up to date, build passed with 0 warnings and 0 errors, and test passed. Total tests: 96. Passed: 96.
- `git --no-pager diff --check` passed with Git line-ending conversion warnings only.
- `git --no-pager status --short --branch` showed cumulative Milestone 2 through Milestone 7 source, test, project, lock-file, validation-script, and documentation changes pending commit.

Milestone 7 coverage includes:

- Infrastructure watcher construction uses only enabled explicit intake folders.
- Disabled intake folders are not watched.
- Drive roots are rejected as too broad for watcher construction.
- Downloads is represented as a disabled suggestion until the user accepts/configures it.
- Events outside configured folders are ignored.
- Meaningful stable files under enabled intake folders enter the candidate queue.
- Partial downloads do not enter the candidate queue.
- Development/package noise under watched roots does not enter the candidate queue.
- Batch-suppressed files do not enter the candidate queue.
- Own-operation events do not enter the candidate queue.
- SQLite persistence can list enabled intake folders for watcher configuration.
- Automated watcher tests use temp folders only for actual filesystem watcher setup and do not touch real Downloads.

Milestone 6 validation from repository root:

```powershell
dotnet build .\FileIntakeAssistant.sln --no-restore
dotnet test .\FileIntakeAssistant.sln --no-build --filter ManualMetadata --verbosity normal
dotnet test .\FileIntakeAssistant.sln --no-build --verbosity normal
.\tools\validate.ps1
git --no-pager diff --check
git --no-pager status --short
```

Results:

- `dotnet build .\FileIntakeAssistant.sln --no-restore` passed with 0 warnings and 0 errors.
- `dotnet test .\FileIntakeAssistant.sln --no-build --filter ManualMetadata --verbosity normal` passed. Total tests: 4. Passed: 4.
- `dotnet test .\FileIntakeAssistant.sln --no-build --verbosity normal` passed. Total tests: 87. Passed: 87.
- `tools/validate.ps1` passed. Restore was up to date, build passed with 0 warnings and 0 errors, and test passed. Total tests: 87. Passed: 87.
- `git --no-pager diff --check` passed with Git line-ending conversion warnings only.
- `git --no-pager status --short` showed cumulative Milestone 2 through Milestone 6 source, test, project, lock-file, validation-script, and documentation changes pending commit.

Milestone 6 coverage includes:

- Core manual metadata workflow creates a file record for a selected file snapshot.
- Existing file records are reused for additional manual metadata.
- Metadata entries store user note, relevance, project, topic, tags, and source URL in SQLite.
- Manual capture records a completed `ManualMetadataCapture` action.
- Empty metadata is rejected without creating a file record.
- Local snapshot reader returns null for missing files.
- Automated temp-file test confirms selected file content and last-write time are unchanged.
- Automated temp-file test confirms no sidecar file is created beside the selected file.
- WPF shell now opens a minimal manual metadata form and composes SQLite persistence under `%LOCALAPPDATA%\File Intake Assistant\data\file-intake.db`.
- No OpenAI, Everything, microphone, API key, sidecar, embedded metadata, move, rename, delete, or overwrite path is required.

Milestone 5 validation from repository root:

```powershell
.\tools\validate.ps1
dotnet build .\FileIntakeAssistant.sln --no-restore
dotnet test .\FileIntakeAssistant.sln --no-build --filter SafeFileOperations --verbosity normal
dotnet test .\FileIntakeAssistant.sln --no-build --filter Undo --verbosity normal
dotnet test .\FileIntakeAssistant.sln --no-build --verbosity normal
git --no-pager diff --check
git --no-pager status --short
```

Results:

- `tools/validate.ps1` passed after seeding the ignored repo-local `.nuget\packages` cache from the existing local NuGet package cache.
- The validation script uses repo-local `.dotnet`, `.appdata`, `.nuget\packages`, and `.nuget\http-cache` directories, all ignored by `.gitignore`.
- The validation script runs `dotnet restore .\FileIntakeAssistant.sln --configfile .\NuGet.config -p:NuGetAudit=false`; this avoids the real user profile NuGet config and avoids network-only `NU1900` audit failures in the sandbox.
- `dotnet --info` passed with SDK `8.0.421` available and repo `global.json` detected.
- `dotnet restore` passed for Core, Infrastructure, App, and Tests.
- `dotnet build` passed with 0 warnings and 0 errors.
- `dotnet test` inside `tools/validate.ps1` passed. Total tests: 83. Passed: 83.
- `dotnet build .\FileIntakeAssistant.sln --no-restore` passed with 0 warnings and 0 errors.
- `dotnet test .\FileIntakeAssistant.sln --no-build --filter SafeFileOperations --verbosity normal` passed. Total tests: 8. Passed: 8.
- `dotnet test .\FileIntakeAssistant.sln --no-build --filter Undo --verbosity normal` passed. Total tests: 5. Passed: 5.
- `dotnet test .\FileIntakeAssistant.sln --no-build --verbosity normal` passed. Total tests: 83. Passed: 83.
- `git --no-pager diff --check` passed with Git line-ending conversion warnings only.
- `git --no-pager status --short` showed cumulative Milestone 2, Milestone 3, Milestone 4, Milestone 5, validation-script, lock-file, project, and documentation changes pending commit.

Milestone 5 coverage includes:

- Filename sanitization for illegal Windows characters and reserved names.
- Extension preservation unless explicitly allowed.
- Numbered non-overwriting conflict candidates such as `Name (2).ext` and `Name (3).ext`.
- Destination validation rejecting no-op source/destination paths.
- Executor refusal when a destination appears after planning, without overwriting the existing file.
- Confirmed move against temp files with completed action and pending undo rows.
- Confirmed rename against temp files with completed action and pending undo rows.
- Unconfirmed move refusal without creating the destination folder.
- Undo success when identity matches and original path is free.
- Undo failure when original path is occupied.
- Undo failure when current file identity no longer matches.
- File record update support for current path, filename, extension, size, hash, last seen time, and status.

Milestone 4 validation from repository root:

```powershell
dotnet --info
dotnet build .\FileIntakeAssistant.sln --no-restore
dotnet test .\FileIntakeAssistant.sln --no-build --filter Stability --verbosity normal
dotnet test .\FileIntakeAssistant.sln --no-build --filter Batch --verbosity normal
dotnet test .\FileIntakeAssistant.sln --no-build --verbosity normal
git status --short
```

Results:

- `dotnet --info` passed with SDK `8.0.421` available and repo `global.json` detected.
- `dotnet build .\FileIntakeAssistant.sln --no-restore` passed with 0 warnings and 0 errors.
- `dotnet test .\FileIntakeAssistant.sln --no-build --filter Stability --verbosity normal` passed. Total tests: 11. Passed: 11. This filter includes one existing triage test with `Stability` in the test name.
- `dotnet test .\FileIntakeAssistant.sln --no-build --filter Batch --verbosity normal` passed. Total tests: 11. Passed: 11.
- `dotnet test .\FileIntakeAssistant.sln --no-build --verbosity normal` passed. Total tests: 72. Passed: 72.
- `git status --short` showed cumulative Milestone 2, Milestone 3, and Milestone 4 source, test, project, lock-file, and documentation changes pending commit.

Passing Milestone 4 coverage includes:

- Stable file after ordinary debounce.
- Changing size delays processing.
- Changing timestamp delays processing.
- Locked file delays processing.
- Zero-byte transient delays processing.
- Partial/temp extension delays processing.
- Extended debounce after partial-download transition.
- Large stable file defers hash when above the configured threshold.
- Infrastructure local snapshot reader detects a locked temp file.
- Infrastructure local snapshot reader supports a temp copied-file simulation.
- Default batch thresholds match `docs/TRIAGE_RULES.md`.
- More than 10 files in 10 seconds creates a possible batch.
- More than 50 files in 60 seconds suppresses individual prompts.
- More than 200 files in 5 minutes creates batch review only.
- Archive extraction, OneDrive sync, installer/unpacker, package-manager, and build-output bursts suppress individual prompts.
- Events outside the batch root are ignored by the batch detector.

Milestone 4 deliverables:

- Core file stability models and `FileStabilityChecker` were added.
- Stability decisions cover existence, directory exclusion, temp/partial extension checks, zero-byte transients, lock state, two-observation size/timestamp stability, ordinary debounce, extended debounce after partial transitions, and large-file hash deferral.
- Core batch detection models and `BatchDetector` were added.
- Batch detection uses documented default thresholds:
  - More than 10 files in 10 seconds: possible batch.
  - More than 50 files in 60 seconds: suppress individual prompts.
  - More than 200 files in 5 minutes: batch review only.
- Batch type classification covers archive extraction, OneDrive sync, installer/unpacker, package install, build output, and unknown bursts.
- Infrastructure `LocalFileStabilitySnapshotReader` was added to provide file existence, size, timestamp, and lock evidence without putting filesystem access in Core.
- Tests use temp directories only for filesystem cases and do not touch real Downloads, Desktop, OneDrive, AppData, Program Files, Windows, repo folders, or user-profile folders.

## Test Status
Milestone 16 automated validation passed locally through `tools/validate.ps1`: 172 tests passed. The validation route redirects .NET and NuGet home/cache paths into ignored repo-local directories and avoids the real Windows profile NuGet config. Focused pre-smoke hardening filters also passed: SafeFileOperations 13/13, Watcher 12/12, Search 38/38, and Logging 5/5. The hardening tests cover launch target rejection without starting OS processes, app move/rename own-operation suppression, unrelated-event non-suppression, candidate queue deduplication, and private-payload audit-log redaction. No-RID framework-dependent publish previously passed through `tools/publish-local.ps1`, and runtime-specific `win-x64` publish previously passed after approved NuGet access through `tools/publish-local.ps1 -RuntimeSpecific`. The current release-readiness check passed and rechecked the no-RID publish artifact: 33 files inspected and no forbidden private runtime artifacts or secrets found. Manual-smoke fixture planning previously passed through `tools/new-smoke-fixtures.ps1 -PlanOnly` without creating files. Manual-smoke report planning previously passed through `tools/new-smoke-run-report.ps1 -PlanOnly` without creating files. Interactive manual smoke remains not run.

## Known Issues
- NuGet can still hit transient network timeouts in restricted or slow environments.
- `tools/validate.ps1` fixes the sandbox profile-access issue by redirecting NuGet and .NET home/cache paths into the repo. It disables online NuGet audit lookup for sandbox validation because blocked `https://api.nuget.org/v3/index.json` access otherwise raises warning-as-error `NU1900`.
- The ignored repo-local `.nuget\packages` cache was seeded from the existing local NuGet package cache to validate without network access. These cache files are not source files and must not be committed.
- Watcher integration now has a Core candidate queue boundary, Infrastructure explicit-folder `FileSystemWatcher` wrapper, SQLite-backed settings UI, app startup/restart wiring, persistent file-event audit rows for processed outcomes, and watcher-candidate popup routing. Interactive popup smoke testing remains required.
- Transcription provider boundaries now exist. OpenAI speech-to-text has an optional disabled-by-default `HttpClient` adapter with fake-HTTP tests; real microphone recording is not implemented yet.
- Everything CLI integration now has an optional disabled-by-default provider and fake-process tests. There is no UI yet to enable or configure Everything.
- The app has a tray shell and nonfatal global hotkey boundary, but tray icon and hotkey behavior have not been manually smoke-tested in an interactive Windows session.
- Safe file operation preview, confirmation, and undo UI now exist and are covered by automated temp-directory tests. Interactive Windows smoke testing remains required.
- The download-intake popup workflow now has a dismissible metadata popup with manual note/transcript fallback, but it has not been manually smoke-tested in an interactive Windows session.
- Search UI now performs confirmed open-file/open-containing-folder/bulk-open actions through an App-layer launch boundary with fake-launcher automated tests. Interactive Windows smoke testing remains required before claiming OS launch behavior works in the user session.
- Structured local file logging through a dependency-free JSON-lines equivalent now mirrors persisted workflow/audit rows and App shell lifecycle events without raw private payloads in automated tests; the pre-smoke hardening pass adds defensive redaction for private payload-shaped keys such as source URLs, transcript text, provider metadata, audio paths, raw search/voice text, and notes. Interactive Windows proof remains incomplete.
- Persistent durable candidate state is not implemented yet. Current tests prove queue gating, settings validation, event audit rows, explicit watched-folder scope, and lightweight in-memory duplicate suppression by normalized path inside the observed/stable window.
- File/folder launch is now hardened to reject unsafe shell targets and wrong target types before OS launch, with fake-process tests only. Interactive Windows proof of actual OS launch behavior remains incomplete.
- Own-operation suppression is now wired from safe file operations into watcher intake processing through an in-memory registry. Suppression is exact-path and short-window only; it is not persisted across restart.
- Installer, code signing, auto-start, and update-channel work are not implemented. Packaging next steps are documented in `docs/PACKAGING.md`.
- Runtime-specific `win-x64` publish requires Microsoft runtime-pack packages when they are not already cached. The unapproved sandbox run failed with `NU1801`/`NU1101` because nuget.org was unavailable, then the approved NuGet-access run restored the required runtime packs and passed.

## Next Action
Ask the user whether to approve an interactive Windows smoke pass using explicit temporary folders, or to explicitly accept the remaining smoke tests as deferred for now. Do not mark the long-running goal complete until that decision is made and recorded.

## Blockers
- Interactive Windows smoke tests require user approval or a user-run smoke pass because they launch the WPF app in the user session and use temporary intake/file-operation folders.

## Manual Tests Still Required
- Milestone 16 manual smoke tests were not run in this validation pass. They are explicitly deferred pending user-approved interactive Windows smoke testing:
  - Tray startup, tray menu, hide-to-tray, and exit.
  - Intake folder configuration and candidate popup against temp folders only.
  - Metadata save, safe move/rename confirmation, undo, search confirmation, and open-file/folder confirmation flows.
  - Batch/noise/partial-download suppression in the running app.
  - Optional provider smoke only if the user supplies and approves local provider configuration.
- Milestone 15 manual smoke tests were not run in this validation pass because they require launching the WPF app in an interactive Windows session and using explicit user-approved temporary file-operation targets:
  - Preview move/rename for a temp file and confirm source, destination, conflict resolution, and extension behavior.
  - Decline a move/rename confirmation and confirm no destination folder or file change occurs.
  - Confirm a move/rename and verify the file changes path only after confirmation.
  - Run undo from the Undo tab and verify identity/conflict checks.
  - Run a retrieval command, open one selected file/folder with confirmation, and confirm action logging.
  - Run a multi-result retrieval command and confirm no bulk open happens before confirmation.
- Automated tests covered preview state, cancellation without mutation, confirmed move through the safe executor, undo success, undo conflict failure, undo identity mismatch failure, selected-file open action logging, bulk-open cancellation, ambiguous multi-result display, and containing-folder launch through fake services.
- Milestone 14 manual smoke tests were not run in this validation pass because they require launching the WPF app in an interactive Windows session and creating an explicit user-approved temporary intake folder:
  - Configure temp intake folder.
  - Create fake completed PDF.
  - Confirm popup appears.
  - Save note and transcript metadata.
  - Confirm SQLite metadata exists.
  - Confirm file content and metadata are unchanged.
  - Dismiss another candidate and confirm no metadata row is created except audit.
- Automated tests covered candidate-to-popup state, candidate metadata save, skip/dismiss audit action, no-key manual transcript fallback, and unchanged temp-file content/timestamp with no sidecar.
- Milestone 13 manual smoke tests were not run in this validation pass because they require launching the WPF app in an interactive Windows session and creating an explicit user-approved temp intake folder:
  - Launch app.
  - Add a temp intake folder.
  - Confirm it appears enabled.
  - Create a fake completed PDF in the temp intake folder.
  - Confirm candidate queue shows the file.
  - Create `.crdownload` and `node_modules` examples and confirm no prompt/candidate.
- Automated tests covered settings view-model behavior, disabled Downloads suggestion enablement, broad-root/repository-marker rejection, event audit rows, and UI-facing candidate queue refresh with temp databases and deterministic paths.
- Milestone 12 manual smoke tests were not run in this validation pass because they require an interactive Windows session and, for some flows, explicit user approval for temp intake folders, file operations, provider settings, or Everything CLI configuration:
  - Start app and confirm tray icon appears.
  - Open tray menu.
  - Show Intake from tray.
  - Show Search from tray.
  - Verify the global search hotkey behavior if registration succeeds on the machine.
  - Exit from tray menu.
- Automated validation covered build, full unit/integration suite, project settings for Windows Forms tray support, and presence of tray/hotkey shell boundaries.
- Milestone 4 manual smoke tests were not run as UI/watch workflows do not exist yet:
  - Simulate a copied file in a temp folder.
  - Simulate a burst of files in a temp folder.
- Automated temp-directory tests covered the local snapshot reader and copied-file simulation; batch-burst behavior was covered by deterministic unit tests.
- Milestone 5 manual smoke test was not run in that validation pass because the move/rename preview UI did not exist at the time; Milestone 15 now adds preview/confirmation and undo UI, but interactive smoke remains not run:
  - Confirm preview text for move and rename in a test folder once UI exists.
- Automated temp-directory tests covered confirmed move, confirmed rename, no-overwrite checks, confirmation refusal, undo success, undo conflict failure, and undo identity mismatch failure.
- Milestone 6 manual smoke test was not run because the WPF UI was not launched interactively in this validation pass:
  - Select temp file.
  - Save metadata.
  - Confirm DB contains metadata.
  - Confirm file content and metadata are unchanged.
- Automated temp-directory tests covered manual metadata persistence, action audit creation, selected-file content preservation, last-write preservation, and absence of sidecar files beside the selected file.
- Milestone 7 manual smoke test was not run in that validation pass because the watcher had not yet been wired into the WPF popup workflow at the time; Milestone 14 now adds popup routing, but the interactive smoke remains not run:
  - Configure temp intake folder.
  - Create fake completed PDF.
  - Confirm candidate appears.
  - Create `.crdownload`.
  - Confirm no prompt.
- Automated tests covered explicit-folder watcher construction with temp folders, no broad drive-root watching, disabled Downloads suggestion, candidate queueing for meaningful stable files, and suppression of unconfigured paths, partial downloads, development/package noise, batch events, and own-operation events.
- Milestone 8 manual smoke test was not run because real microphone capture is not implemented and the WPF UI was not launched interactively in this validation pass:
  - Use manual text fallback to create transcript metadata.
- Automated tests covered manual text transcription capture, fake provider success, provider failure fallback, local no-key fallback, reviewed transcript storage in SQLite metadata, and temp-audio retention policy.
- Milestone 9 manual smoke test was not run because the optional real OpenAI provider requires a user-supplied runtime API key and would make a live external API call:
  - Configure OpenAI provider locally.
  - Transcribe a temp audio file.
  - Confirm transcript review and temp-audio cleanup.
- Automated tests covered disabled OpenAI provider behavior, missing API key behavior, fake HTTP success, redacted provider errors, no live OpenAI calls, and default temp-audio cleanup after provider success.
- Milestone 10 manual smoke test was not run because the WPF UI was not launched interactively in this validation pass:
  - Type `open the last five Excel files I saved`.
  - Confirm ranked results display.
  - Confirm bulk opening requires confirmation and does not open blindly.
  - Type `open the folder for my AI infrastructure reports`.
  - Confirm folder results display before any open action.
- Automated tests covered deterministic parser behavior, relative dates, destructive-command rejection, SQLite search filters, folder-level retrieval, workflow audit rows, and confirmation-required outcomes for multiple open results.
- Milestone 11 manual smoke test was not run because optional local Everything validation requires a configured `es.exe` and user approval:
  - Configure Everything CLI path.
  - Enable Everything integration.
  - Search for a known temp-path file through the UI.
  - Confirm SQLite metadata remains authoritative and no metadata is mutated without user action.
- Automated tests covered disabled provider behavior, missing `es.exe`, fake process output parsing, allowed-root filtering, and merge behavior with SQLite metadata.
