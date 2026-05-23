param(
    [string] $Path
)

$ErrorActionPreference = "Stop"

$repo = Split-Path -Parent $PSScriptRoot

if ([string]::IsNullOrWhiteSpace($Path)) {
    $Path = Join-Path $repo "artifacts\publish\FileIntakeAssistant-framework-dependent"
}

$resolvedPath = Resolve-Path -LiteralPath $Path -ErrorAction Stop
$artifactRoot = $resolvedPath.ProviderPath

if (-not (Test-Path -LiteralPath $artifactRoot -PathType Container)) {
    throw "Publish artifact path is not a directory: $artifactRoot"
}

$forbiddenDirectories = @(
    ".appdata",
    ".dotnet",
    ".nuget",
    "data",
    "logs",
    "temp-audio"
)

$forbiddenFileNames = @(
    ".env",
    "appsettings.local.json",
    "file-intake.db",
    "secrets.json",
    "settings.json",
    "settings.local.json"
)

$forbiddenExtensions = @(
    ".db",
    ".log",
    ".m4a",
    ".mp3",
    ".sqlite",
    ".wav",
    ".webm"
)

$forbiddenSuffixes = @(
    ".db-shm",
    ".db-wal",
    ".env.local",
    ".secret.json",
    ".sqlite-shm",
    ".sqlite-wal",
    ".transcript.json"
)

$textExtensionsToScan = @(
    ".bat",
    ".cmd",
    ".config",
    ".json",
    ".md",
    ".ps1",
    ".txt",
    ".xml"
)

$violations = [System.Collections.Generic.List[string]]::new()

Get-ChildItem -LiteralPath $artifactRoot -Force -Recurse -Directory | ForEach-Object {
    $directoryName = $_.Name.ToLowerInvariant()
    if ($forbiddenDirectories -contains $directoryName) {
        $violations.Add("Forbidden directory in publish artifact: $($_.FullName)")
    }
}

$files = @(Get-ChildItem -LiteralPath $artifactRoot -Force -Recurse -File)

foreach ($file in $files) {
    $fileName = $file.Name.ToLowerInvariant()
    $extension = $file.Extension.ToLowerInvariant()

    if ($forbiddenFileNames -contains $fileName) {
        $violations.Add("Forbidden file name in publish artifact: $($file.FullName)")
    }

    if ($forbiddenExtensions -contains $extension) {
        $violations.Add("Forbidden file extension in publish artifact: $($file.FullName)")
    }

    foreach ($suffix in $forbiddenSuffixes) {
        if ($fileName.EndsWith($suffix, [StringComparison]::OrdinalIgnoreCase)) {
            $violations.Add("Forbidden file suffix in publish artifact: $($file.FullName)")
            break
        }
    }

    if (($textExtensionsToScan -contains $extension) -and $file.Length -le 5MB) {
        $content = Get-Content -LiteralPath $file.FullName -Raw -ErrorAction Stop
        if ($content -match "sk-[A-Za-z0-9_-]{20,}") {
            $violations.Add("Possible raw OpenAI-style secret in publish artifact: $($file.FullName)")
        }

        if ($content -match "OPENAI_API_KEY\s*=") {
            $violations.Add("Possible plaintext OpenAI API key assignment in publish artifact: $($file.FullName)")
        }
    }
}

if ($violations.Count -gt 0) {
    $message = "Publish artifact safety check failed:`n" + ($violations -join "`n")
    throw $message
}

Write-Host "Publish artifact safety check passed: $($files.Count) files inspected under $artifactRoot"
