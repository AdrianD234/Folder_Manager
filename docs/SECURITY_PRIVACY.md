# Security And Privacy

## Privacy Model
File Intake Assistant stores private context locally. Metadata such as notes, transcripts, relevance, project, topic, tags, source URLs, and summaries must stay in the app database unless the user explicitly exports it.

The app must not write private metadata into user files.

## Source Of Truth
SQLite under `%LOCALAPPDATA%\File Intake Assistant\data\file-intake.db` is the source of truth.

Not source of truth:

- Embedded document metadata.
- Office custom properties.
- PDF metadata.
- Alternate data streams.
- xattrs.
- Sidecar files.

## API Providers
OpenAI and other API providers are optional.

Rules:

- Disabled by default.
- No background API calls without explicit opt-in.
- App works without API key.
- Tests do not require API keys.
- Settings must clearly show provider status.
- Runtime OpenAI API usage is separate from ChatGPT/Codex credits.

Current OpenAI transcription implementation:

- Uses an optional Infrastructure `HttpClient` adapter behind `ITranscriptionProvider`.
- Is disabled unless configured.
- Reads the API key from an environment-variable source, defaulting to `OPENAI_API_KEY`.
- Returns `NotConfigured` without making an HTTP call when disabled or missing a key.
- Redacts the configured API key from provider error messages before they can be persisted.
- Does not add an OpenAI SDK dependency.
- Has only fake-HTTP automated tests; no test calls live OpenAI.

## Secrets
- API keys must not be committed.
- API keys must not be logged.
- Prefer environment variables or Windows credential storage.
- If a local config reference is needed, store a reference rather than the secret itself when possible.
- `.gitignore` must exclude secret and local settings files.

## Logs
Logs are local private data and may contain file paths for auditability.

Logs must not contain:

- API keys.
- Full secrets.
- Raw audio content.
- Unnecessary provider request payloads.

Logs should contain:

- Action ids.
- Timestamps.
- Operation status.
- Triage decisions.
- Provider names.

The structured local JSON-lines audit log mirrors persisted workflow and audit
events, but it must log only summaries for private payloads. It may log paths
for local auditability, but it must not mirror raw user notes, transcript text,
provider metadata JSON, provider error bodies, raw search/voice command text,
or API keys.
- Error classes and safe messages.

## Audio
Audio temp files live under:

```text
%LOCALAPPDATA%\File Intake Assistant\temp-audio\
```

Rules:

- Delete after successful transcription by default.
- Retain only if explicitly configured.
- Do not commit audio files.
- Do not log audio content.
- Tests must not require real microphone audio.

## Database
Database lives under:

```text
%LOCALAPPDATA%\File Intake Assistant\data\file-intake.db
```

Rules:

- Do not commit databases.
- Tests use temp databases.
- Support future export/delete metadata tools.
- Support backup before destructive migrations.
- Do not assume database is cloud-synced.

## File Operations
Security and trust rules:

- Never delete.
- Never overwrite.
- Confirm before move/rename.
- Validate destination paths.
- Prevent illegal Windows filename characters.
- Handle path length safely.
- Record undo information.
- Verify identity before undo.

## Watched Folders
- Watch selected intake folders only.
- Downloads is an initial default suggestion, not a hidden broad scan.
- Do not watch the whole user profile, drive, AppData, Program Files, Windows, or browser caches.

## User Export And Delete
Future tools should support:

- Export metadata to a user-selected file.
- Delete selected metadata records.
- Clear search history.
- Clear transcription history.

These tools must not delete user files.

## Threats And Mitigations
- Private metadata leakage: store only in SQLite, redact logs, keep providers optional.
- Secret leakage: never log keys, ignore local secret files.
- Unsafe automation: require confirmation and undo.
- Prompt fatigue: triage and batch suppression.
- API cost surprise: no background API calls without opt-in.
- Database corruption: migrations, backups, and export path.
