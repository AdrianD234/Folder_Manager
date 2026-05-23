# Status

## Current Milestone
Milestone 1: Solution Skeleton And Build Discipline

Status: Partially implemented. Do not advance to Milestone 2 until full solution restore/build/test validation passes.

## Completed Milestones
- Milestone 0: Repository Governance And Planning Files.
- Milestone 0.5: Governance And Readiness Review.
- Milestone 0.6: Durable Goal Contract.

## Validation Commands Last Run
Milestone 1 skeleton validation:

```powershell
git status --short --branch
dotnet --list-sdks
dotnet --info
dotnet restore
dotnet build
dotnet test
dotnet build src\FileIntakeAssistant.App\FileIntakeAssistant.App.csproj --no-restore
git status --short
```

Results:

- Branch is `master` tracking `origin/master`.
- `dotnet --list-sdks` reported `8.0.421 [C:\Program Files\dotnet\sdk]`.
- `dotnet --info` reported SDK version `8.0.421`, MSBuild version `17.11.48+02bf66295`, RID `win-x64`, and base path `C:\Program Files\dotnet\sdk\8.0.421\`.
- Installed runtimes reported by `dotnet --info`: `Microsoft.AspNetCore.App 8.0.27`, `Microsoft.NETCore.App 8.0.27`, and `Microsoft.WindowsDesktop.App 8.0.27`.
- `global.json` is present and requests SDK `8.0.100` with `rollForward` set to `latestFeature`; the installed `8.0.421` SDK satisfies the intended .NET 8 requirement.
- `FileIntakeAssistant.sln` was created.
- `src\FileIntakeAssistant.App`, `src\FileIntakeAssistant.Core`, `src\FileIntakeAssistant.Infrastructure`, and `tests\FileIntakeAssistant.Tests` were created.
- Project references were wired as App -> Core + Infrastructure and Infrastructure -> Core. Core has no project references.
- The WPF app project targets `net8.0-windows` and sets `UseWPF=true`.
- An architecture smoke test was added under `tests\FileIntakeAssistant.Tests\Architecture`.
- `packages.lock.json` files were generated, but the test project lock file could not be fully resolved because restore could not reach NuGet.
- `dotnet restore` failed for `tests\FileIntakeAssistant.Tests\FileIntakeAssistant.Tests.csproj` with `NU1301: Unable to load the service index for source https://api.nuget.org/v3/index.json`.
- `dotnet build` failed for the same test-project restore error.
- `dotnet test` failed for the same test-project restore error.
- `dotnet build src\FileIntakeAssistant.App\FileIntakeAssistant.App.csproj --no-restore` succeeded with 0 warnings and 0 errors, proving App/Core/Infrastructure compile after the successful partial restore.
- Dotnet commands were run with workspace-local `DOTNET_CLI_HOME`, `APPDATA`, `NUGET_PACKAGES`, and `NUGET_HTTP_CACHE_PATH` values to avoid sandboxed user-profile writes and reads.

## Test Status
Architecture smoke tests exist but have not run because the xUnit test project cannot restore packages without NuGet access or cached packages.

## Known Issues
- Full Milestone 1 validation is blocked by NuGet package restore failure for the test project.
- The test project `packages.lock.json` exists but is not considered validated until restore succeeds.
- No app implementation exists yet beyond the WPF template skeleton.

## Next Action
Restore NuGet package access or provide the required packages in a local cache, then re-run `dotnet restore`, `dotnet build`, and `dotnet test`. Fix any resulting validation failures before advancing to Milestone 2.

## Blockers
- `dotnet restore` cannot load `https://api.nuget.org/v3/index.json` from the current sandbox, and the required test packages are not present in the checked user or repo package caches.

## Manual Tests Still Required
- None for Milestone 1. Manual smoke tests begin in later UI/workflow milestones.
