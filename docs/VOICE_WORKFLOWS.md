# Voice Workflows

## Goals
- Capture spoken context during intake.
- Support typed fallback at every step.
- Support command-mode retrieval by voice or text.
- Keep transcription providers optional.
- Avoid API usage unless explicitly configured.

## Download-Intake Microphone Popup
When a meaningful one-off file is detected:

1. Show the intake popup with file name, path, type, size, and triage reason.
2. Show fields for note, relevance, project, topic, tags, and source URL.
3. If microphone capture is enabled, offer record/stop controls.
4. If no transcription provider is configured, show manual text entry.
5. After transcript generation, show transcript for review.
6. User can edit transcript and extracted metadata.
7. Save metadata externally to SQLite.
8. Show safe folder/name suggestions if available.
9. Require confirmation before move or rename.

Microphone capture must not begin silently. User action is required.

## Hotkey-Triggered Command Microphone
When the command hotkey is pressed:

1. Open a small command window.
2. User can type or record a command.
3. If recording, show clear recording state.
4. Transcribe through configured provider or fall back to manual text.
5. Show recognized command before action.
6. Parse deterministically first.
7. Show results and required confirmations.

## Manual Text Fallback
Manual text is first-class:

- User can type context instead of recording.
- User can paste a transcript.
- User can correct provider output before saving.
- Tests should use manual/fake providers rather than real audio.

## Audio Capture Lifecycle
Default lifecycle:

1. Create temp audio file under `%LOCALAPPDATA%\File Intake Assistant\temp-audio\`.
2. Record until user stops or configured max duration is reached.
3. Create `transcription_jobs` row.
4. Call provider.
5. Save transcript and provider metadata.
6. Delete temp audio after successful transcription by default.
7. On failure, follow retention setting and show manual fallback.

Logs must not include audio content.

## Temporary Audio File Handling
- Temp audio directory is app-local.
- Files are named with opaque ids, not private transcript text.
- Audio is deleted by default after successful transcription.
- Retaining audio must be explicit user configuration.
- Tests must not require real audio capture.

## Transcription Provider Interface
Conceptual contract:

```text
TranscribeAsync(audioFile, options, cancellationToken)
  -> transcript text
  -> optional confidence
  -> provider metadata
  -> status or error
```

Providers must not know about WPF controls or SQLite schema details.

## OpenAI Transcription Provider
OpenAI STT is optional.

Requirements:

- Disabled until configured.
- API key from environment variable or secure config source.
- No tests require real API key.
- No logs expose secrets.
- Provider returns not-configured state when missing key.
- Runtime API usage is separate from ChatGPT/Codex credits and must be communicated in settings.

## Local Transcription Provider Placeholder
The local provider can initially return `NotConfigured`.

Requirements:

- Same interface as other providers.
- No UI-specific logic.
- Easy to replace with a real local model later.

## Fake Transcription Provider
Tests use a fake provider that can return:

- Success transcript.
- Failure.
- Delay/cancellation.
- Low confidence.

## Transcript Review
Before transcript-derived metadata is saved:

- Show transcript to user.
- Allow edit.
- Preserve original provider transcript in job record if useful.
- Save user-reviewed text as metadata.

## Transcript-To-Metadata Parsing
Initial parser is deterministic:

- Extract simple project/topic/tag patterns if obvious.
- Allow relevance selection independent of transcript.
- Do not invent metadata silently.
- Optional LLM parser may be added later behind `IMetadataParser`.

## Confidence Handling
- Store provider confidence if available.
- Store classifier confidence if metadata parsing occurred.
- Low confidence should require review.
- Confidence cannot bypass confirmation for move/rename or bulk open.

## User Correction Flow
User can:

- Edit transcript text.
- Edit extracted metadata.
- Clear fields.
- Save note only.
- Dismiss without saving metadata.

Dismissal should be logged as a skipped action when appropriate.

## No-Key Mode
When no API key is configured:

- Manual text fallback works.
- Fake provider is used only in tests.
- OpenAI controls appear disabled or not configured.
- App does not attempt background API calls.
