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
