[CmdletBinding()]
param(
    [string] $ArtifactPath,

    [switch] $SkipArtifactCheck
)

$ErrorActionPreference = "Stop"

$repo = Split-Path -Parent $PSScriptRoot

if ([string]::IsNullOrWhiteSpace($ArtifactPath)) {
    $ArtifactPath = Join-Path $repo "artifacts\publish\FileIntakeAssistant-framework-dependent"
}

$violations = [System.Collections.Generic.List[string]]::new()

function Add-Violation {
    param(
        [Parameter(Mandatory = $true)]
        [string] $Message
    )

    $violations.Add($Message)
}

function Read-RequiredText {
    param(
        [Parameter(Mandatory = $true)]
        [string] $RelativePath
    )

    $path = Join-Path $repo $RelativePath
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
        Add-Violation "Missing required file: $RelativePath"
        return ""
    }

    return Get-Content -LiteralPath $path -Raw -ErrorAction Stop
}

function Assert-Contains {
    param(
        [Parameter(Mandatory = $true)]
        [string] $Name,

        [Parameter(Mandatory = $true)]
        [string] $Content,

        [Parameter(Mandatory = $true)]
        [string] $Pattern
    )

    if ($Content -notmatch $Pattern) {
        Add-Violation "$Name does not contain required pattern: $Pattern"
    }
}

function Assert-FileExists {
    param(
        [Parameter(Mandatory = $true)]
        [string] $RelativePath
    )

    $path = Join-Path $repo $RelativePath
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
        Add-Violation "Missing required file: $RelativePath"
    }
}

$requiredFiles = @(
    "AGENTS.md",
    ".codex\AGENTS.md",
    "docs\GOAL.md",
    "docs\PLAN.md",
    "docs\STATUS.md",
    "docs\COMPLETION_AUDIT.md",
    "docs\MANUAL_SMOKE_TESTS.md",
    "docs\PACKAGING.md",
    "docs\SECURITY_PRIVACY.md",
    "docs\RISK_REGISTER.md",
    "tools\validate.ps1",
    "tools\publish-local.ps1",
    "tools\check-publish-artifact.ps1",
    "tools\new-smoke-fixtures.ps1",
    "tools\new-smoke-run-report.ps1"
)

foreach ($file in $requiredFiles) {
    Assert-FileExists $file
}

$agents = Read-RequiredText "AGENTS.md"
$codexAgents = Read-RequiredText ".codex\AGENTS.md"
$status = Read-RequiredText "docs\STATUS.md"
$audit = Read-RequiredText "docs\COMPLETION_AUDIT.md"
$manualSmoke = Read-RequiredText "docs\MANUAL_SMOKE_TESTS.md"
$packaging = Read-RequiredText "docs\PACKAGING.md"
$risk = Read-RequiredText "docs\RISK_REGISTER.md"
$gitignore = Read-RequiredText ".gitignore"

Assert-Contains "AGENTS.md" $agents "docs/STATUS\.md"
Assert-Contains ".codex/AGENTS.md" $codexAgents "docs/STATUS\.md"
Assert-Contains ".codex/AGENTS.md" $codexAgents "docs/GOAL\.md"

Assert-Contains "docs/STATUS.md" $status "Milestone 16: Interactive Smoke Pass And Release Readiness"
Assert-Contains "docs/STATUS.md" $status "tools/validate\.ps1"
Assert-Contains "docs/STATUS.md" $status "tools/publish-local\.ps1"
Assert-Contains "docs/STATUS.md" $status "tools/check-publish-artifact\.ps1"
Assert-Contains "docs/STATUS.md" $status "tools/new-smoke-fixtures\.ps1"
Assert-Contains "docs/STATUS.md" $status "tools/new-smoke-run-report\.ps1"
Assert-Contains "docs/STATUS.md" $status "Interactive Windows smoke tests are not run"
Assert-Contains "docs/STATUS.md" $status "Runtime-specific.*win-x64.*publish.*passed"

Assert-Contains "docs/COMPLETION_AUDIT.md" $audit "Status: Not complete"
Assert-Contains "docs/COMPLETION_AUDIT.md" $audit "Manual smoke tests have not been run"
Assert-Contains "docs/COMPLETION_AUDIT.md" $audit "runtime-specific.*win-x64.*publish.*passed"

Assert-Contains "docs/MANUAL_SMOKE_TESTS.md" $manualSmoke "Interactive Smoke Approval Boundary"
Assert-Contains "docs/MANUAL_SMOKE_TESTS.md" $manualSmoke "Status: Not run; deferred"
Assert-Contains "docs/MANUAL_SMOKE_TESTS.md" $manualSmoke "tools\\new-smoke-fixtures\.ps1"
Assert-Contains "docs/MANUAL_SMOKE_TESTS.md" $manualSmoke "tools\\new-smoke-run-report\.ps1"

Assert-Contains "docs/PACKAGING.md" $packaging "Release Gate"
Assert-Contains "docs/PACKAGING.md" $packaging "tools\\validate\.ps1"
Assert-Contains "docs/PACKAGING.md" $packaging "tools\\publish-local\.ps1"
Assert-Contains "docs/PACKAGING.md" $packaging "tools\\check-publish-artifact\.ps1"
Assert-Contains "docs/PACKAGING.md" $packaging "tools\\new-smoke-run-report\.ps1"

Assert-Contains "docs/RISK_REGISTER.md" $risk "R028"
Assert-Contains "docs/RISK_REGISTER.md" $risk "R029"
Assert-Contains "docs/RISK_REGISTER.md" $risk "R030"

Assert-Contains ".gitignore" $gitignore "(?m)^\.appdata/$"
Assert-Contains ".gitignore" $gitignore "(?m)^\.dotnet/$"
Assert-Contains ".gitignore" $gitignore "(?m)^\.nuget/$"
Assert-Contains ".gitignore" $gitignore "(?m)^artifacts/$"
Assert-Contains ".gitignore" $gitignore "(?m)^!packages\.lock\.json$"

if (-not $SkipArtifactCheck) {
    $checker = Join-Path $repo "tools\check-publish-artifact.ps1"
    if (Test-Path -LiteralPath $checker -PathType Leaf) {
        & $checker -Path $ArtifactPath
    }
}

if ($violations.Count -gt 0) {
    $message = "Release readiness check failed:`n" + ($violations -join "`n")
    throw $message
}

Write-Host "Release readiness check passed. Automated release gates are documented and current blockers remain explicit."
