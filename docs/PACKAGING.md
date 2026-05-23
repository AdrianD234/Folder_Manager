# Packaging Notes

## Current Packaging State
The app is a WPF desktop executable targeting `net8.0-windows`. Milestone 12 adds a tray shell and global hotkey boundary, but this repository does not yet include an installer, code signing, auto-start registration, or update channel.

SQLite remains the metadata source of truth under:

```text
%LOCALAPPDATA%\File Intake Assistant\data\file-intake.db
```

Do not package databases, logs, temp audio, API keys, local settings, or user metadata.

## Local Publish Commands
Run validation before publishing:

```powershell
.\tools\validate.ps1
```

Preferred sandbox-safe local publish check in restricted Codex environments:

```powershell
.\tools\publish-local.ps1
```

The script redirects .NET and NuGet home/cache paths into ignored repo-local
folders, restores with the repository `NuGet.config`, publishes with
`--no-restore`, and prints ignored-file status so `artifacts/` output remains
untracked. By default it creates a no-RID framework-dependent publish under:

```text
artifacts\publish\FileIntakeAssistant-framework-dependent\
```

Runtime-specific publish check:

```powershell
.\tools\publish-local.ps1 -RuntimeSpecific
```

The runtime-specific path matches the `win-x64` publish shape, but it may require
Microsoft runtime-pack packages from NuGet. In restricted network environments,
record the exact blocker and use the default no-RID publish check as the local
release-readiness fallback.

Current Milestone 16 status: `.\tools\publish-local.ps1 -RuntimeSpecific`
passed after approved NuGet access restored the required runtime packs.

Framework-dependent local publish:

```powershell
dotnet publish .\src\FileIntakeAssistant.App\FileIntakeAssistant.App.csproj -c Release -r win-x64 --self-contained false -o .\artifacts\publish\FileIntakeAssistant-win-x64
```

Self-contained local publish for machines without a .NET runtime:

```powershell
dotnet publish .\src\FileIntakeAssistant.App\FileIntakeAssistant.App.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o .\artifacts\publish\FileIntakeAssistant-win-x64-self-contained
```

Do not commit `artifacts/` output.

## Artifact Safety Check
Run the artifact safety checker after publishing:

```powershell
.\tools\check-publish-artifact.ps1
```

By default it inspects:

```text
artifacts\publish\FileIntakeAssistant-framework-dependent\
```

Pass `-Path` to inspect another publish output. The checker fails if the
artifact includes databases, logs, temp audio, local settings, secrets,
repo-local cache folders, or obvious plaintext OpenAI-style secrets.

## Release Gate
Before sharing a build:

- Run `.\tools\validate.ps1`.
- Run `.\tools\publish-local.ps1` or the equivalent framework-dependent publish command.
- Run `.\tools\check-publish-artifact.ps1`.
- For a runtime-specific publish, run `.\tools\check-publish-artifact.ps1 -Path .\artifacts\publish\FileIntakeAssistant-win-x64`.
- Run `.\tools\check-release-readiness.ps1` to verify release-readiness docs and explicit blocker status are internally consistent.
- Generate a manual smoke report template with `.\tools\new-smoke-run-report.ps1` if the smoke pass will be user-run or recorded outside the current session.
- Run or explicitly defer the manual smoke tests in `docs/MANUAL_SMOKE_TESTS.md`.
- Confirm no real user files were moved, renamed, overwritten, deleted, or used in automated tests.
- Confirm OpenAI and Everything providers are disabled unless explicitly configured.
- Confirm logs do not contain secrets.
- Confirm the app starts, shows a tray icon, and exits from the tray menu in a manual Windows session.

## Installer Work Not Yet Implemented
Future packaging work should decide:

- Installer technology, such as MSIX, WiX, or a simple zipped portable build.
- Code-signing certificate and signing workflow.
- Auto-start registration with explicit user opt-in.
- Upgrade and rollback behavior.
- Database backup before schema migrations.
- Location of user-editable settings.

These choices require decision notes before implementation.

## Known Packaging Limitations
- No installer project exists.
- No code signing exists.
- No automatic startup registration exists.
- No update channel exists.
- No release artifact naming convention is enforced by CI.
- Manual smoke testing is still required for tray, hotkey, and OS integration behavior.
- `artifacts/` publish output is local-only and must not be committed.
- Runtime-specific `win-x64` publish may need NuGet runtime packs that are not available in restricted network sandboxes unless already cached or network access is approved.
