# Codex Guidance

Always start by reading the repository root `AGENTS.md`.

Follow `docs/EXECUTION.md` for implementation work.

## Rules
- Do not implement beyond the current milestone in `docs/STATUS.md`.
- Keep changes scoped and reviewable.
- Use high reasoning for planning, safety-critical logic, privacy decisions, and file operation code.
- Prefer tests over assumptions.
- Never perform real file operations outside temporary test directories during automated tests.
- Never delete user files.
- Never overwrite user files.
- Never move or rename user files without explicit confirmation.
- Do not require OpenAI, Everything, microphone hardware, or real user folders for tests.
- Do not fake test results.
- Do not claim a feature works without automated tests or documented manual smoke tests.
- Update `docs/STATUS.md` after each milestone.
- Update `docs/DECISIONS.md` for meaningful design or dependency choices.
- Update `docs/RISK_REGISTER.md` for new or changed risks.

## Current Phase
Milestone 0 creates documentation and config scaffolding only. App implementation begins in Milestone 1 after user approval.
