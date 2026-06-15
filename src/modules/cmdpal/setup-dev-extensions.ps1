# setup-dev-extensions.ps1
# Sets up the JavaScript extension development environment for local testing.
# Run from: src/modules/cmdpal/
#
# What it does:
# 1. Builds the TypeScript SDK (ts-sdk)
# 2. Builds the sample JS extension
# 3. Installs the sample extension (with cmdpal.json) into the JSExtensions directory

param(
    [string]$Configuration = "Debug",
    [string]$Platform = "ARM64"
)

$ErrorActionPreference = "Stop"
$scriptDir = $PSScriptRoot

# Paths
$sdkDir = Join-Path $scriptDir "ts-sdk"
$sampleExtDir = Join-Path $scriptDir "ext\SampleJSExtension"
$extensionsDir = Join-Path $env:LOCALAPPDATA "Microsoft\PowerToys\CommandPalette\JSExtensions"
$sampleInstallDir = Join-Path $extensionsDir "sample-js-extension"

Write-Host "=== Step 1: Building TypeScript SDK ===" -ForegroundColor Cyan
Push-Location $sdkDir
npm install --quiet 2>&1 | Out-Null
npm run build
if ($LASTEXITCODE -ne 0) { Pop-Location; throw "SDK build failed" }
Pop-Location
Write-Host "  SDK built successfully." -ForegroundColor Green

Write-Host ""
Write-Host "=== Step 2: Building Sample Extension ===" -ForegroundColor Cyan
Push-Location $sampleExtDir
npm install --quiet 2>&1 | Out-Null
npm run build
if ($LASTEXITCODE -ne 0) { Pop-Location; throw "Sample extension build failed" }
Pop-Location
Write-Host "  Sample extension built successfully." -ForegroundColor Green

Write-Host ""
Write-Host "=== Step 3: Installing Sample Extension ===" -ForegroundColor Cyan
# Create the extensions directory structure
New-Item -ItemType Directory -Path $sampleInstallDir -Force | Out-Null

# Copy the built sample extension (dist/, node_modules/, cmdpal.json, package.json)
Copy-Item -Path "$sampleExtDir\cmdpal.json" -Destination $sampleInstallDir -Force
Copy-Item -Path "$sampleExtDir\package.json" -Destination $sampleInstallDir -Force
if (Test-Path "$sampleExtDir\dist") {
    Copy-Item -Path "$sampleExtDir\dist" -Destination $sampleInstallDir -Recurse -Force
}
if (Test-Path "$sampleExtDir\node_modules") {
    Copy-Item -Path "$sampleExtDir\node_modules" -Destination $sampleInstallDir -Recurse -Force
}

Write-Host "  Installed to: $sampleInstallDir" -ForegroundColor Green

Write-Host ""
Write-Host "=== Done! ===" -ForegroundColor Green
Write-Host ""
Write-Host "Extension installed at: $sampleInstallDir"
Write-Host "The JavaScriptExtensionService will discover it on next CmdPal launch."
Write-Host ""
Write-Host "Requirements:"
Write-Host "  - Node.js v22+ must be on your PATH"
Write-Host "  - Each JS extension runs as its own 'node dist/index.js' process"
Write-Host ""
Write-Host "Hot-reload: Edit .js files in the extension directory and CmdPal will"
Write-Host "automatically restart the extension process (500ms debounce)."
