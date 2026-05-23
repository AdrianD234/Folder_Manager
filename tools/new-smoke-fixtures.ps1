[CmdletBinding()]
param(
    [string] $Root,

    [switch] $PlanOnly
)

$ErrorActionPreference = "Stop"

if ([string]::IsNullOrWhiteSpace($Root)) {
    $timestamp = Get-Date -Format "yyyyMMdd-HHmmss"
    $Root = Join-Path ([System.IO.Path]::GetTempPath()) "FileIntakeAssistant-Smoke\$timestamp"
}

$tempRoot = [System.IO.Path]::GetFullPath([System.IO.Path]::GetTempPath())
$rootFullPath = [System.IO.Path]::GetFullPath($Root)

if (-not $rootFullPath.StartsWith($tempRoot, [System.StringComparison]::OrdinalIgnoreCase)) {
    throw "Smoke fixtures must be created under the system temp directory. Refusing root: $rootFullPath"
}

if (Test-Path -LiteralPath $rootFullPath) {
    throw "Refusing to use an existing smoke fixture root because files must never be overwritten: $rootFullPath"
}

$directories = @(
    "Intake",
    "Intake\BatchExtraction",
    "Intake\node_modules\package",
    "Intake\build",
    "FileOperations\Original",
    "FileOperations\Destination",
    "FileOperations\ConflictDestination",
    "SearchSeed"
)

$files = [System.Collections.Generic.List[object]]::new()
$files.Add([pscustomobject]@{
        RelativePath = "Intake\Example Report.pdf"
        Content      = "Placeholder PDF-like smoke-test content. This is not a private file."
    })
$files.Add([pscustomobject]@{
        RelativePath = "Intake\Finance Model.xlsx"
        Content      = "Placeholder spreadsheet-like smoke-test content. This is not a private file."
    })
$files.Add([pscustomobject]@{
        RelativePath = "Intake\Example.pdf.crdownload"
        Content      = "Partial download placeholder. The app should not prompt for this file."
    })
$files.Add([pscustomobject]@{
        RelativePath = "Intake\node_modules\package\index.js"
        Content      = "module.exports = 'development noise';"
    })
$files.Add([pscustomobject]@{
        RelativePath = "Intake\build\output.dll"
        Content      = "Build output placeholder. The app should suppress this file."
    })
$files.Add([pscustomobject]@{
        RelativePath = "FileOperations\Original\Board Pack.pdf"
        Content      = "Placeholder filing source. Use only for confirmed smoke-test move or rename."
    })
$files.Add([pscustomobject]@{
        RelativePath = "FileOperations\ConflictDestination\Board Pack.pdf"
        Content      = "Existing destination placeholder. Conflict resolution must not overwrite this file."
    })
$files.Add([pscustomobject]@{
        RelativePath = "SearchSeed\AI Infrastructure Report.pdf"
        Content      = "Placeholder search seed. Add metadata manually through the app if needed."
    })

for ($i = 1; $i -le 55; $i++) {
    $files.Add([pscustomobject]@{
            RelativePath = "Intake\BatchExtraction\Extracted-$($i.ToString("000")).txt"
            Content      = "Batch extraction placeholder $i. The app should suppress individual prompts."
        })
}

function Resolve-FixturePath {
    param(
        [Parameter(Mandatory = $true)]
        [string] $RelativePath
    )

    $combined = Join-Path $rootFullPath $RelativePath
    $fullPath = [System.IO.Path]::GetFullPath($combined)
    if (-not $fullPath.StartsWith($rootFullPath, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "Fixture path escapes the smoke root: $RelativePath"
    }

    return $fullPath
}

function New-SmokeDirectory {
    param(
        [Parameter(Mandatory = $true)]
        [string] $RelativePath
    )

    $path = Resolve-FixturePath $RelativePath
    New-Item -ItemType Directory -Path $path -Force | Out-Null
}

function New-SmokeFile {
    param(
        [Parameter(Mandatory = $true)]
        [string] $RelativePath,

        [Parameter(Mandatory = $true)]
        [string] $Content
    )

    $path = Resolve-FixturePath $RelativePath
    $parent = Split-Path -Parent $path
    New-Item -ItemType Directory -Path $parent -Force | Out-Null

    $encoding = [System.Text.UTF8Encoding]::new($false)
    $stream = [System.IO.File]::Open($path, [System.IO.FileMode]::CreateNew, [System.IO.FileAccess]::Write, [System.IO.FileShare]::None)
    try {
        $writer = [System.IO.StreamWriter]::new($stream, $encoding)
        try {
            $writer.Write($Content)
        }
        finally {
            $writer.Dispose()
        }
    }
    finally {
        $stream.Dispose()
    }
}

$manifest = [ordered]@{
    created_at                  = (Get-Date).ToString("o")
    root                        = $rootFullPath
    intake_folder               = (Resolve-FixturePath "Intake")
    file_operations_source      = (Resolve-FixturePath "FileOperations\Original")
    file_operations_destination = (Resolve-FixturePath "FileOperations\Destination")
    conflict_destination        = (Resolve-FixturePath "FileOperations\ConflictDestination")
    search_seed_folder          = (Resolve-FixturePath "SearchSeed")
    notes                       = @(
        "Fixtures are placeholder files only.",
        "No files are deleted or overwritten by this script.",
        "Use the Intake folder as the explicit watched folder for smoke tests.",
        "Use FileOperations folders only after confirming move or rename actions in the app."
    )
    files                       = @($files | ForEach-Object { $_.RelativePath })
}

if ($PlanOnly) {
    Write-Host "Smoke fixture plan only. No files were created."
    Write-Host "Root: $rootFullPath"
    Write-Host "Directories planned: $($directories.Count)"
    Write-Host "Files planned: $($files.Count + 1)"
    Write-Host "Intake folder: $($manifest.intake_folder)"
    Write-Host "File operation source: $($manifest.file_operations_source)"
    Write-Host "File operation destination: $($manifest.file_operations_destination)"
    return
}

New-Item -ItemType Directory -Path $rootFullPath -Force | Out-Null

foreach ($directory in $directories) {
    New-SmokeDirectory $directory
}

foreach ($file in $files) {
    New-SmokeFile -RelativePath $file.RelativePath -Content $file.Content
}

$manifestJson = $manifest | ConvertTo-Json -Depth 5
New-SmokeFile -RelativePath "SMOKE_FIXTURES.json" -Content $manifestJson

Write-Host "Smoke fixtures created without deleting or overwriting files."
Write-Host "Root: $rootFullPath"
Write-Host "Intake folder: $($manifest.intake_folder)"
Write-Host "File operation source: $($manifest.file_operations_source)"
Write-Host "File operation destination: $($manifest.file_operations_destination)"
Write-Host "Manifest: $(Resolve-FixturePath "SMOKE_FIXTURES.json")"
