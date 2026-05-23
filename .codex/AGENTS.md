# Codex Guidance

Always start by reading the repository root `AGENTS.md`.

Use `docs/STATUS.md` as the source of truth for the active milestone. Use `docs/GOAL.md` as the long-running goal contract. Follow `docs/EXECUTION.md` for the runbook and `docs/PLAN.md` for milestone scope, acceptance criteria, validation commands, risks, stop conditions, and expected file changes.

## Rules
- Do not implement beyond the current milestone in `docs/STATUS.md`.
- Read `docs/GOAL.md` before implementation work.
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

## Active Milestone
Do not hard-code the current phase in this file. Read `docs/STATUS.md` before work and implement only the active milestone listed there.
