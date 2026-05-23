# Search And Retrieval

## Principles
- SQLite search is first.
- Everything integration is optional and later.
- Search results must be explainable.
- Ambiguous results are displayed.
- Bulk opening requires confirmation.
- No destructive commands are supported.

## Current Implementation
Milestones 10, 11, and 15 implement the first deterministic retrieval path, optional Everything enrichment, and confirmed user-facing open actions:

- Core `SearchIntentParser` parses supported text/voice commands without OpenAI.
- Core search workflow records `voice_commands` and `search_queries`.
- Infrastructure `SqliteSearchProvider` searches file records, metadata entries, and folder context.
- Infrastructure `EverythingCliSearchProvider` can parse optional `es.exe` path output when explicitly enabled and available.
- Infrastructure `CompositeFileSearchProvider` merges Everything path hits into SQLite results while preserving SQLite metadata as authoritative.
- The WPF shell displays selectable ranked results in a Search tab.
- Single-file, containing-folder, and bulk-open actions are routed through an App-layer confirmation and file-launch boundary.
- Multiple open results require confirmation and cancellation is logged without launching anything.
- Destructive commands are rejected before provider search.
- Everything CLI remains disabled by default and is not required for app operation or tests.

## SQLite-First Search
SQLite is queried for:

- File name.
- Extension/file type.
- First seen and last seen dates.
- Stable date.
- Metadata notes.
- Transcript text.
- Relevance.
- Project.
- Topic.
- Tags.
- Source/referrer URL.
- Folder-level inherited context.
- Action history.

SQLite remains the source of truth for private metadata.

## Optional Everything Search Provider
Everything CLI integration is available through `IFileSearchProvider` as optional enrichment.

Rules:

- Disabled by default.
- Requires configured or discovered `es.exe`.
- Requires configured allowed roots before invoking `es.exe`.
- Tests use fake process runner.
- App works if Everything is unavailable.
- Everything results may enrich file discovery.
- Everything must not become metadata source of truth.
- No Everything SDK dependency in v1.
- Missing or failing `es.exe` must fail closed to SQLite-only results.
- Missing allowed roots must fail closed to SQLite-only results.
- Everything-only results cannot satisfy SQLite-only metadata filters such as relevance, project, topic, date, or downloaded source unless they merge with a SQLite metadata result.

## File System Fallback
File system fallback can verify:

- Whether a stored path still exists.
- Whether a containing folder can be opened.
- Whether file identity appears to match before an action.

Fallback must not scan the whole computer.

## Result Ranking
Initial ranking signals:

- Exact file type match.
- Recency match.
- Relevance match.
- Project/topic/tag match.
- Filename match.
- Transcript/note match.
- Source URL match.
- Folder context match.
- Whether path still exists.
- Recent user action history.

Ranking must be deterministic for tests.

## Voice Command Intents
Supported initial intents:

- Search files.
- Show recent files.
- Open a single confirmed file.
- Open containing folder.
- Open last N files matching file type.
- Show high, medium, or low relevance files.
- Filter by project.
- Filter by topic.
- Filter by tags.
- Filter by relative dates.

Unsupported:

- Delete files.
- Overwrite files.
- Move/rename without confirmation.
- Send files externally.
- Watch new folders silently.

## Relative Dates
Initial parser supports:

- Today.
- Yesterday.
- This week.
- Last week.
- Last N weeks.
- This month.
- Last month.
- This year.
- Last year.
- N days ago.
- N weeks ago.

Tests must use injectable current time.

## Examples
### "open the last five Excel files I saved"
Intent:

- Action: open files.
- File type: Excel.
- Count: 5.
- Sort: most recent saved/seen.
- Confirmation: required because multiple files.

### "show high relevance PDFs from last week"
Intent:

- Action: show results.
- File type: PDF.
- Relevance: high.
- Date range: last week.
- Confirmation: not needed for display.

### "find the finance report I downloaded two weeks ago"
Intent:

- Action: show results.
- Keywords: finance report.
- Date range: around two weeks ago.
- Source: downloaded/intake.
- Ambiguity: show ranked results.

### "open the folder for my AI infrastructure files"
Intent:

- Action: open containing folder.
- Project/topic keywords: AI infrastructure.
- Target: folder.
- Confirmation: required if multiple folders match.

## Confirmation Rules
Always require confirmation for:

- Opening multiple files.
- Opening a large result set.
- Opening files that no longer match expected identity.
- Move/rename suggestions.
- Provider/API actions that send data externally.

Single file open can be one-click confirmed from a result display.

## Ambiguity Handling
When results are ambiguous:

- Show ranked list.
- Explain why top results matched.
- Let user choose file or folder.
- Do not open blindly.
- Log the command and outcome.

## Safety Rules For Actions
- No destructive commands.
- No delete intent.
- No overwrite intent.
- No silent move/rename intent.
- No broad filesystem search beyond configured providers and scopes.
- If parser is unsure, show results instead of acting.
