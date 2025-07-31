<#
.SYNOPSIS
    Generates a trimming report for CmdPal UI project
.DESCRIPTION
    This script builds CmdPal UI with and without trimming/AOT, then uses TrimmingAnalyzer
    to analyze the differences and generate reports.
.PARAMETER Configuration
    Build configuration (Debug/Release). Defaults to Release
.PARAMETER EnableAOT
    Whether to enable AOT compilation. Defaults to true
#>

param(
    [string]$Configuration = "Release",
    [bool]$EnableAOT = $true
)

$ErrorActionPreference = "Stop"

# Get paths
$rootDir = Resolve-Path (Join-Path $PSScriptRoot "..\..")
$cmdPalProject = Join-Path $rootDir "src\modules\cmdpal\Microsoft.CmdPal.UI\Microsoft.CmdPal.UI.csproj"
$cmdPalDir = Split-Path -Parent $cmdPalProject
$analyzerProject = Join-Path $rootDir "tools\TrimmingAnalyzer\TrimmingAnalyzer.csproj"

# Build paths
$tempDir = Join-Path $env:TEMP "CmdPalTrimAnalysis_$(Get-Date -Format 'yyyyMMdd_HHmmss')"
$untrimmedDir = Join-Path $tempDir "untrimmed"
$trimmedDir = Join-Path $tempDir "trimmed"

Write-Host "===== CmdPal Trimming Analysis =====" -ForegroundColor Cyan

try {
    # Create directories
    New-Item -ItemType Directory -Path $untrimmedDir -Force | Out-Null
    New-Item -ItemType Directory -Path $trimmedDir -Force | Out-Null

    # Build TrimmingAnalyzer
    Write-Host "Building TrimmingAnalyzer tool..." -ForegroundColor Yellow
    & dotnet build $analyzerProject -c Release
    if ($LASTEXITCODE -ne 0) {
        throw "Failed to build TrimmingAnalyzer"
    }

    # Build without trimming using MSBuild
    Write-Host "Building CmdPal without trimming..." -ForegroundColor Yellow
    & msbuild $cmdPalProject `
        /p:Configuration=$Configuration `
        /p:Platform=x64 `
        /p:PublishTrimmed=false `
        /p:EnableCmdPalAOT=false `
        /p:PublishAot=false `
        /p:RuntimeIdentifier=win-x64 `
        /p:SelfContained=true `
        /t:Publish `
        /verbosity:minimal

    if ($LASTEXITCODE -ne 0) {
        throw "Failed to build untrimmed version"
    }

    # Copy untrimmed output to analysis directory
    $untrimmedOutput = Join-Path $cmdPalDir "x64\$Configuration\WinUI3Apps\CmdPal"
    Copy-Item "$untrimmedOutput\Microsoft.CmdPal.UI.dll" $untrimmedDir -Force
    Write-Host "Copied untrimmed DLL from: $untrimmedOutput"

    # Build with trimming using MSBuild
    Write-Host "Building CmdPal with trimming (AOT=$EnableAOT)..." -ForegroundColor Yellow
    & msbuild $cmdPalProject `
        /p:Configuration=$Configuration `
        /p:Platform=x64 `
        /p:PublishTrimmed=true `
        /p:EnableCmdPalAOT=$EnableAOT `
        /p:PublishAot=$EnableAOT `
        /p:RuntimeIdentifier=win-x64 `
        /p:SelfContained=true `
        /p:TrimMode=partial `
        /p:TreatWarningsAsErrors=false `
        /p:WarningsAsErrors="" `
        /p:WarningsNotAsErrors="" `
        /p:SuppressTrimAnalysisWarnings=true `
        "/p:NoWarn=IL2104,IL2026,IL2070,IL2072,IL2075,IL2077,IL2080,IL2091,IL2092,IL2093,IL2094,IL2095,IL2096,IL2097,IL2098,IL2099,IL2103,CsWinRT1028" `
        /t:Publish `
        /verbosity:minimal

    if ($LASTEXITCODE -ne 0) {
        throw "Failed to build trimmed version"
    }

    # Copy trimmed output to analysis directory
    $trimmedOutput = Join-Path $cmdPalDir "x64\$Configuration\WinUI3Apps\CmdPal"
    Copy-Item "$trimmedOutput\Microsoft.CmdPal.UI.dll" $trimmedDir -Force
    Write-Host "Copied trimmed DLL from: $trimmedOutput"

    # Run analyzer
    Write-Host "Analyzing trimming differences..." -ForegroundColor Yellow
    & dotnet run --project $analyzerProject -- `
        "$untrimmedDir\Microsoft.CmdPal.UI.dll" `
        "$trimmedDir\Microsoft.CmdPal.UI.dll" `
        $cmdPalDir `
        "rdxml,markdown"

    if ($LASTEXITCODE -ne 0) {
        throw "Failed to analyze trimming"
    }

    Write-Host "`n===== Analysis Complete =====" -ForegroundColor Cyan
    Write-Host "Reports generated in: $cmdPalDir" -ForegroundColor Green
    Write-Host "  - TrimmedTypes.rd.xml" -ForegroundColor Green
    Write-Host "  - TrimmedTypes.md" -ForegroundColor Green

} finally {
    # Cleanup
    if (Test-Path $tempDir) {
        Remove-Item -Path $tempDir -Recurse -Force -ErrorAction SilentlyContinue
    }
}