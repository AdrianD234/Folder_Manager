# Risk Register

## Risk Levels
- High: Could violate safety, privacy, or user trust.
- Medium: Could cause incorrect behavior or poor usability.
- Low: Manageable implementation or maintenance risk.

## Risks
| ID | Risk | Level | Mitigation | Status |
| --- | --- | --- | --- | --- |
| R001 | Prompt fatigue from too many file events | High | Triage before prompting, batch suppression, selected folders only, confidence thresholds | Open |
| R002 | Incorrectly processing archive extractions | High | Batch detection, extraction pattern rules, no individual prompts above thresholds | Open |
| R003 | Incorrectly processing OneDrive sync bursts | High | Burst detection, sync path heuristics, batch audit instead of prompts | Open |
| R004 | Incorrectly processing compiler/build noise | High | Ignore directories and extensions, development folder tests | Open |
| R005 | Incorrectly processing Codex-generated repo files | High | Repo/build ignore rules, folder-level context, no child-file tagging by default | Open |
| R006 | File locks and partial downloads | High | Stability engine, debounce, lock checks, partial extension rules | Open |
| R007 | Accidental overwrite | High | Central safe operation service, destination validation, conflict resolver, tests | Open |
| R008 | Incorrect undo | High | Identity verification, original-path conflict checks, action/undo records | Open |
| R009 | Private metadata leakage | High | SQLite source of truth, no embedded metadata, secret redaction, local logs only | Open |
| R010 | API cost surprise | Medium | Providers disabled by default, visible settings, no background API calls without opt-in | Open |
| R011 | Search result ambiguity | Medium | Rank results, show ambiguous results, confirm bulk open | Open |
| R012 | Hotkey conflicts | Medium | Configurable hotkeys, visible failure when registration fails | Open |
| R013 | Database corruption | High | Migrations, backups/exports, SQLite tests, clear recovery guidance | Open |
| R014 | Large file hashing performance | Medium | Hash ordinary stable files, defer large hashes, configurable thresholds | Open |
| R015 | User trust erosion from unsafe automation | High | Explicit confirmation, logs, undo, conservative defaults, manual mode | Open |
| R016 | SDK/toolchain mismatch | Medium | Pin SDK in `global.json`, verify `dotnet --info`, document blocker in status | Open |
| R017 | External dependency drift | Medium | Central package versions, lock files, decision notes for dependencies | Open |

## Review Cadence
Review this register after every milestone. Add new risks immediately when discovered.
