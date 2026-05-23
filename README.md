# File Intake Assistant

File Intake Assistant is a private Windows productivity application for meaningful file intake, spoken context capture, external metadata management, and safe file retrieval.

The app is designed to help a user answer questions such as:

- Why did I download this file?
- Which project or topic does this file belong to?
- What did I say about this file when I saved it?
- Can I open the last five Excel files I saved?
- Can I find the finance report I downloaded two weeks ago?

## Core Principles
- Watch only explicit intake folders, with Downloads as the initial default.
- Triage filesystem events before prompting the user.
- Ignore build output, package installs, archive extractions, temporary downloads, OneDrive sync bursts, repo noise, and the app's own operations.
- Store metadata externally in SQLite under `%LOCALAPPDATA%\File Intake Assistant\`.
- Never write private context into files.
- Never delete files.
- Never overwrite files.
- Never move or rename files without confirmation.
- Support undo for every move or rename performed by the app.
- Keep OpenAI and Everything integrations optional.

## Current State
This repository is in Milestone 0: governance, architecture, and planning. No app implementation exists yet.

Read these first:

- [AGENTS.md](AGENTS.md)
- [docs/SPEC.md](docs/SPEC.md)
- [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md)
- [docs/PLAN.md](docs/PLAN.md)
- [docs/EXECUTION.md](docs/EXECUTION.md)
- [docs/STATUS.md](docs/STATUS.md)

## Planned Stack
- .NET 8 or newer
- Windows-native desktop app
- WPF tray app
- Global hotkeys
- SQLite via `Microsoft.Data.Sqlite`
- Structured local logging via Serilog or equivalent
- MVVM support via CommunityToolkit.Mvvm or equivalent
- xUnit or NUnit tests

## Planned App Data Paths
```text
%LOCALAPPDATA%\File Intake Assistant\
  data\file-intake.db
  logs\
  temp-audio\
  config\settings.json
```

Automated tests must not use these real locations. Tests use temporary directories and temporary databases only.

## Command Line Validation
After Milestone 1 creates the solution and projects, the repository must validate with:

```powershell
dotnet --info
dotnet restore
dotnet build
dotnet test
git status --short
```

Do not claim validation passed unless these commands were actually run.
