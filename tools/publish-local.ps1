param(
    [switch] $RuntimeSpecific
)

$ErrorActionPreference = "Stop"

$repo = Split-Path -Parent $PSScriptRoot
$publishRoot = Join-Path $repo "artifacts\publish"
$outputName = if ($RuntimeSpecific) {
    "FileIntakeAssistant-win-x64"
}
else {
    "FileIntakeAssistant-framework-dependent"
}
$outputPath = Join-Path $publishRoot $outputName

$env:DOTNET_CLI_HOME = Join-Path $repo ".dotnet"
$env:APPDATA = Join-Path $repo ".appdata"
$env:NUGET_PACKAGES = Join-Path $repo ".nuget\packages"
$env:NUGET_HTTP_CACHE_PATH = Join-Path $repo ".nuget\http-cache"

New-Item -ItemType Directory -Force -Path `
    $env:DOTNET_CLI_HOME, `
    $env:APPDATA, `
    $env:NUGET_PACKAGES, `
    $env:NUGET_HTTP_CACHE_PATH, `
    $publishRoot | Out-Null

function Invoke-Checked {
    param(
        [Parameter(Mandatory = $true)]
        [string] $FilePath,

        [Parameter(Mandatory = $true)]
        [string[]] $Arguments
    )

    & $FilePath @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "Command failed with exit code ${LASTEXITCODE}: $FilePath $($Arguments -join ' ')"
    }
}

Push-Location $repo
try {
    Invoke-Checked dotnet @(
        "restore",
        "$repo\FileIntakeAssistant.sln",
        "--configfile",
        "$repo\NuGet.config",
        "--ignore-failed-sources",
        "-p:NuGetAudit=false")
    if ($RuntimeSpecific) {
        Invoke-Checked dotnet @(
            "restore",
            "$repo\src\FileIntakeAssistant.App\FileIntakeAssistant.App.csproj",
            "-r",
            "win-x64",
            "--configfile",
            "$repo\NuGet.config",
            "--ignore-failed-sources",
            "-p:NuGetAudit=false")
    }

    $publishArguments = @(
        "publish",
        "$repo\src\FileIntakeAssistant.App\FileIntakeAssistant.App.csproj",
        "-c",
        "Release",
        "--self-contained",
        "false",
        "--no-restore",
        "--output",
        $outputPath)

    if ($RuntimeSpecific) {
        $publishArguments = @(
            "publish",
            "$repo\src\FileIntakeAssistant.App\FileIntakeAssistant.App.csproj",
            "-c",
            "Release",
            "-r",
            "win-x64",
            "--self-contained",
            "false",
            "--no-restore",
            "--output",
            $outputPath)
    }

    Invoke-Checked dotnet $publishArguments
    Invoke-Checked git @("status", "--short", "--ignored")
}
finally {
    Pop-Location
}
