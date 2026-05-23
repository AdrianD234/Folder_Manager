[CmdletBinding()]
param(
    [string] $OutputPath,

    [string] $FixtureRoot,

    [switch] $PlanOnly
)

$ErrorActionPreference = "Stop"

$repo = Split-Path -Parent $PSScriptRoot
$timestamp = Get-Date -Format "yyyyMMdd-HHmmss"

if ([string]::IsNullOrWhiteSpace($OutputPath)) {
    $OutputPath = Join-Path $repo "artifacts\smoke\manual-smoke-report-$timestamp.md"
}

if ([string]::IsNullOrWhiteSpace($FixtureRoot)) {
    $FixtureRoot = Join-Path ([System.IO.Path]::GetTempPath()) "FileIntakeAssistant-Smoke\$timestamp"
}

$artifactsRoot = [System.IO.Path]::GetFullPath((Join-Path $repo "artifacts"))
$outputFullPath = [System.IO.Path]::GetFullPath($OutputPath)
$fixtureFullPath = [System.IO.Path]::GetFullPath($FixtureRoot)

if (-not $outputFullPath.StartsWith($artifactsRoot, [System.StringComparison]::OrdinalIgnoreCase)) {
    throw "Smoke run reports must be written under the ignored artifacts directory. Refusing output path: $outputFullPath"
}

if (Test-Path -LiteralPath $outputFullPath) {
    throw "Refusing to overwrite an existing smoke run report: $outputFullPath"
}

if (-not $fixtureFullPath.StartsWith([System.IO.Path]::GetFullPath([System.IO.Path]::GetTempPath()), [System.StringComparison]::OrdinalIgnoreCase)) {
    throw "Smoke fixture roots must be under the system temp directory. Refusing fixture root: $fixtureFullPath"
}

function Get-GitValue {
    param(
        [Parameter(Mandatory = $true)]
        [string[]] $Arguments
    )

    try {
        $value = & git @Arguments 2>$null
        if ($LASTEXITCODE -eq 0 -and -not [string]::IsNullOrWhiteSpace($value)) {
            return ($value | Select-Object -First 1)
        }
    }
    catch {
    }

    return "Unavailable"
}

$commit = Get-GitValue @("rev-parse", "--short", "HEAD")
$branch = Get-GitValue @("branch", "--show-current")
$status = Get-GitValue @("status", "--short")
if ($status -is [array]) {
    $status = $status -join "`n"
}

if ([string]::IsNullOrWhiteSpace($status)) {
    $status = "Clean or unavailable"
}

$intakeFolder = Join-Path $fixtureFullPath "Intake"
$fileOperationSource = Join-Path $fixtureFullPath "FileOperations\Original"
$fileOperationDestination = Join-Path $fixtureFullPath "FileOperations\Destination"
$manifestPath = Join-Path $fixtureFullPath "SMOKE_FIXTURES.json"

$report = @"
# Manual Smoke Run Report

Generated: $(Get-Date -Format "o")

This report is a template for a user-approved interactive Windows smoke pass.
It is not proof that smoke tests passed until each test result is filled in from
an actual run.

## Environment
- Branch: $branch
- Commit: $commit
- Working tree status at template generation:

```text
$status
```

- Windows version: $([System.Environment]::OSVersion.VersionString)
- App build or publish path:
- Provider/API state: OpenAI disabled unless explicitly configured; Everything disabled unless explicitly configured.
- Smoke fixture root: $fixtureFullPath
- Intake folder: $intakeFolder
- File operation source: $fileOperationSource
- File operation destination: $fileOperationDestination
- Fixture manifest: $manifestPath

## Safe Setup Commands

Preview fixtures without creating files:

```powershell
.\tools\new-smoke-fixtures.ps1 -Root "$fixtureFullPath" -PlanOnly
```

Create fixtures only after approval:

```powershell
.\tools\new-smoke-fixtures.ps1 -Root "$fixtureFullPath"
```

Build/publish checks:

```powershell
.\tools\validate.ps1
.\tools\publish-local.ps1
.\tools\check-publish-artifact.ps1
```

## Stop Conditions
Stop the smoke pass immediately if the app attempts to delete, overwrite,
silently move or rename, broaden watch scope, call an external provider without
opt-in, or write private metadata into a user file or sidecar.

## Results Summary
- Overall result: Not run
- Safety/privacy issues found:
- Deferred tests:
- Notes:

## Test Results

| Test | Result | Evidence/Notes |
| --- | --- | --- |
| 1. Start app and tray menu | Not run | |
| 2. Configure temp intake folder | Not run | |
| 3. Create fake completed PDF | Not run | |
| 4. Confirm popup appears | Not run | |
| 5. Save metadata only | Not run | |
| 6. Move/rename with confirmation | Not run | |
| 7. Undo move | Not run | |
| 8. Skip batch extraction | Not run | |
| 9. Ignore `.crdownload` | Not run | |
| 10. Ignore `node_modules` and build noise | Not run | |
| 11. Manual transcript fallback | Not run | |
| 12. Run search command | Not run | |
| 13. Open file or folder with confirmation | Not run | |

## Detailed Notes

### Test 1: Start App And Tray Menu
- Result: Not run
- Evidence:
- Notes:

### Test 2: Configure Temp Intake Folder
- Result: Not run
- Evidence:
- Notes:

### Test 3: Create Fake Completed PDF
- Result: Not run
- Evidence:
- Notes:

### Test 4: Confirm Popup Appears
- Result: Not run
- Evidence:
- Notes:

### Test 5: Save Metadata Only
- Result: Not run
- Evidence:
- Notes:

### Test 6: Move/Rename With Confirmation
- Result: Not run
- Evidence:
- Notes:

### Test 7: Undo Move
- Result: Not run
- Evidence:
- Notes:

### Test 8: Skip Batch Extraction
- Result: Not run
- Evidence:
- Notes:

### Test 9: Ignore `.crdownload`
- Result: Not run
- Evidence:
- Notes:

### Test 10: Ignore `node_modules` And Build Noise
- Result: Not run
- Evidence:
- Notes:

### Test 11: Manual Transcript Fallback
- Result: Not run
- Evidence:
- Notes:

### Test 12: Run Search Command
- Result: Not run
- Evidence:
- Notes:

### Test 13: Open File Or Folder With Confirmation
- Result: Not run
- Evidence:
- Notes:
"@

if ($PlanOnly) {
    Write-Host "Smoke run report plan only. No files were created."
    Write-Host "Output path: $outputFullPath"
    Write-Host "Fixture root: $fixtureFullPath"
    return
}

$outputDirectory = Split-Path -Parent $outputFullPath
New-Item -ItemType Directory -Path $outputDirectory -Force | Out-Null

$encoding = [System.Text.UTF8Encoding]::new($false)
$stream = [System.IO.File]::Open($outputFullPath, [System.IO.FileMode]::CreateNew, [System.IO.FileAccess]::Write, [System.IO.FileShare]::None)
try {
    $writer = [System.IO.StreamWriter]::new($stream, $encoding)
    try {
        $writer.Write($report)
    }
    finally {
        $writer.Dispose()
    }
}
finally {
    $stream.Dispose()
}

Write-Host "Smoke run report template created without overwriting files."
Write-Host "Output path: $outputFullPath"
Write-Host "Fixture root: $fixtureFullPath"
