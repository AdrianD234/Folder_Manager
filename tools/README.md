# Tools

This directory is reserved for repository-local helper tools.

Rules:

- Tools must not mutate real user folders unless explicitly designed, reviewed, and confirmed.
- Tools used by tests must operate only on temporary directories.
- Any tool that changes files must document its inputs, outputs, and safety boundaries.
- Do not add large tools or generated binaries to the repository.

## Validation
Use `tools/validate.ps1` for sandbox-safe repository validation. It redirects
`DOTNET_CLI_HOME`, `APPDATA`, `NUGET_PACKAGES`, and `NUGET_HTTP_CACHE_PATH` into
ignored workspace-local folders before running restore, build, test, and status.

## Manual Smoke Fixtures
Use `tools/new-smoke-fixtures.ps1` to prepare placeholder files for an approved
manual Windows smoke pass. The script creates a timestamped root under the
system temp directory, refuses non-temp roots, refuses existing roots, and never
deletes or overwrites files.

Preview the plan without creating files:

```powershell
tools/new-smoke-fixtures.ps1 -PlanOnly
```

Create fixtures for a user-approved smoke pass:

```powershell
tools/new-smoke-fixtures.ps1
```

The script does not launch the app, configure watched folders, run microphone
capture, call providers, move files, rename files, or perform the smoke pass.

## Manual Smoke Run Report
Use `tools/new-smoke-run-report.ps1` to create a run-report template for an
approved manual smoke pass. By default, it writes under ignored `artifacts/`,
refuses output paths outside `artifacts/`, refuses to overwrite an existing
report, and uses a temp-only fixture root.

Preview the report path and fixture root without creating files:

```powershell
tools/new-smoke-run-report.ps1 -PlanOnly
```

Create a report template for a user-approved or user-run smoke pass:

```powershell
tools/new-smoke-run-report.ps1
```

The generated report starts every test as `Not run`. It must be filled from an
actual interactive smoke pass before it can be used as evidence.

## Publishing
Use `tools/publish-local.ps1` for sandbox-safe local publish checks. It uses the
same repo-local .NET and NuGet cache paths as validation, restores with the repo
`NuGet.config`, publishes with `--no-restore`, and prints ignored-file status so
publish output does not accidentally enter source control.

By default, the script creates a no-RID framework-dependent publish at:

```text
artifacts\publish\FileIntakeAssistant-framework-dependent\
```

Use `tools/publish-local.ps1 -RuntimeSpecific` for the runtime-specific
`win-x64` publish shape when NuGet access or cached Microsoft runtime packs are
available. In restricted environments, record the exact blocker and use the
default no-RID publish as the local readiness fallback.

After publishing, run:

```powershell
tools/check-publish-artifact.ps1
```

The checker inspects the publish output for forbidden local/private artifacts
such as databases, logs, temp audio, secrets, local settings, repo-local cache
folders, and obvious plaintext OpenAI-style secrets.

## Release Readiness
Use `tools/check-release-readiness.ps1` after validation and publish checks. It
is a read-only gate that verifies the Milestone 16 release-readiness docs,
manual-smoke deferral state, runtime-specific publish status, risk entries,
ignored runtime/cache paths, and publish artifact safety check are internally
consistent.

```powershell
tools/check-release-readiness.ps1
```
