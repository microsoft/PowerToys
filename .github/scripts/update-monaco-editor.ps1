<#
.SYNOPSIS
    Updates the Monaco Editor in PowerToys to the latest (or specified) version.

.DESCRIPTION
    This script automates the Monaco Editor update process described in
    doc/devdocs/common/FilePreviewCommon.md:
      1. Downloads Monaco editor via npm
      2. Copies the min folder into src/Monaco/monacoSRC/
      3. Generates the monaco_languages.json file using Node.js (headless)

.PARAMETER Version
    The Monaco Editor npm version to install. Defaults to "latest".

.PARAMETER RepoRoot
    The root of the PowerToys repository. Defaults to the repo root relative to this script.

.EXAMPLE
    ./update-monaco-editor.ps1
    ./update-monaco-editor.ps1 -Version "0.50.0"
#>
[CmdletBinding()]
param(
    [Parameter()]
    [string]$Version = "latest",

    [Parameter()]
    [string]$RepoRoot
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

if (-not $RepoRoot) {
    $RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot ".." "..")).Path
}

$monacoDir = Join-Path $RepoRoot "src" "Monaco"
$monacoSrcDir = Join-Path $monacoDir "monacoSRC"
$languagesJsonPath = Join-Path $monacoDir "monaco_languages.json"
$tempDir = Join-Path ([System.IO.Path]::GetTempPath()) "monaco-update-$([System.Guid]::NewGuid().ToString('N').Substring(0, 8))"

Write-Host "=== Monaco Editor Update Script ==="
Write-Host "Repository root: $RepoRoot"
Write-Host "Target version: $Version"
Write-Host "Temp directory: $tempDir"

# Verify prerequisites
$npmPath = Get-Command npm -ErrorAction SilentlyContinue
if (-not $npmPath) {
    throw "npm is required but not found in PATH. Please install Node.js."
}

$nodePath = Get-Command node -ErrorAction SilentlyContinue
if (-not $nodePath) {
    throw "node is required but not found in PATH. Please install Node.js."
}

# Verify repo structure
if (-not (Test-Path $monacoDir)) {
    throw "Monaco directory not found at: $monacoDir"
}

try {
    # Step 1: Download Monaco via npm
    Write-Host "`n--- Step 1: Downloading Monaco Editor ($Version) via npm ---"
    New-Item -ItemType Directory -Path $tempDir -Force | Out-Null

    Push-Location $tempDir
    try {
        $versionSpec = if ($Version -eq "latest") { "monaco-editor@latest" } else { "monaco-editor@$Version" }
        npm init -y 2>&1 | Out-Null
        npm install $versionSpec 2>&1

        if ($LASTEXITCODE -ne 0) {
            throw "npm install failed with exit code $LASTEXITCODE"
        }
    }
    finally {
        Pop-Location
    }

    $downloadedMinDir = Join-Path $tempDir "node_modules" "monaco-editor" "min"
    if (-not (Test-Path $downloadedMinDir)) {
        throw "Downloaded Monaco min directory not found at: $downloadedMinDir"
    }

    # Detect the downloaded version from loader.js
    $loaderJsPath = Join-Path $downloadedMinDir "vs" "loader.js"
    if (-not (Test-Path $loaderJsPath)) {
        throw "loader.js not found in downloaded Monaco package"
    }

    $loaderContent = Get-Content $loaderJsPath -Raw
    if ($loaderContent -match 'Version:\s*(\d+\.\d+\.\d+)') {
        $newVersion = $Matches[1]
        Write-Host "Downloaded Monaco version: $newVersion"
    }
    else {
        Write-Warning "Could not detect version from loader.js"
        $newVersion = $Version
    }

    # Step 2: Replace monacoSRC/min folder
    Write-Host "`n--- Step 2: Replacing monacoSRC with new version ---"
    $targetMinDir = Join-Path $monacoSrcDir "min"

    if (Test-Path $targetMinDir) {
        Write-Host "Removing existing min directory..."
        Remove-Item -Recurse -Force $targetMinDir
    }

    Write-Host "Copying new min directory..."
    Copy-Item -Recurse -Force $downloadedMinDir $targetMinDir

    # Step 3: Generate monaco_languages.json
    Write-Host "`n--- Step 3: Generating monaco_languages.json ---"
    $generateScript = Join-Path $PSScriptRoot "generate-monaco-languages.js"

    node $generateScript $monacoDir

    if ($LASTEXITCODE -ne 0) {
        throw "Failed to generate monaco_languages.json (exit code: $LASTEXITCODE)"
    }

    if (-not (Test-Path $languagesJsonPath)) {
        throw "monaco_languages.json was not generated at: $languagesJsonPath"
    }

    Write-Host "`n=== Monaco Editor update complete ==="
    Write-Host "Updated to version: $newVersion"
    Write-Host "Languages JSON: $languagesJsonPath"

    # Output the new version for the workflow to use
    Write-Output "MONACO_VERSION=$newVersion"
}
finally {
    # Clean up temp directory
    if (Test-Path $tempDir) {
        Remove-Item -Recurse -Force $tempDir -ErrorAction SilentlyContinue
    }
}
