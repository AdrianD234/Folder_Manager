$ErrorActionPreference = "Stop"

$repo = Split-Path -Parent $PSScriptRoot

$env:DOTNET_CLI_HOME = Join-Path $repo ".dotnet"
$env:APPDATA = Join-Path $repo ".appdata"
$env:NUGET_PACKAGES = Join-Path $repo ".nuget\packages"
$env:NUGET_HTTP_CACHE_PATH = Join-Path $repo ".nuget\http-cache"

New-Item -ItemType Directory -Force -Path `
    $env:DOTNET_CLI_HOME, `
    $env:APPDATA, `
    $env:NUGET_PACKAGES, `
    $env:NUGET_HTTP_CACHE_PATH | Out-Null

function Invoke-Checked {
    param(
        [Parameter(Mandatory = $true)]
        [string] $FilePath,

        [Parameter(ValueFromRemainingArguments = $true)]
        [string[]] $Arguments
    )

    & $FilePath @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "Command failed with exit code ${LASTEXITCODE}: $FilePath $($Arguments -join ' ')"
    }
}

Push-Location $repo
try {
    Invoke-Checked dotnet --info
    Invoke-Checked dotnet restore "$repo\FileIntakeAssistant.sln" --configfile "$repo\NuGet.config" "-p:NuGetAudit=false"
    Invoke-Checked dotnet build "$repo\FileIntakeAssistant.sln" --no-restore
    Invoke-Checked dotnet test "$repo\FileIntakeAssistant.sln" --no-build --verbosity normal
    Invoke-Checked git status --short
}
finally {
    Pop-Location
}
