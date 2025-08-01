<#
.SYNOPSIS
    PowerToys CmdPal AOT Trimming Analysis - Generates assembly comparison reports
    
.DESCRIPTION
    This script builds CmdPal UI with and without AOT optimization, then uses TrimmingAnalyzer
    to analyze the differences and generate reports showing which types are removed by AOT.
    
    ANALYSIS PROCESS:
    1. Build Debug version (no AOT optimization)
    2. Build Release version (with AOT optimization) 
    3. Compare assemblies to identify removed types
    4. Generate reports: TrimmedTypes.md, TrimmedTypes.rd.xml
    
    REQUIREMENTS:
    • Visual Studio 2022 with C++ workload
    • Windows SDK
    • Use Developer Command Prompt for VS 2022
    
    OUTPUT REPORTS:
    • TrimmedTypes.md - Human-readable Markdown report
    • TrimmedTypes.rd.xml - Runtime directives to preserve types
    • Analysis JSON data for further processing
    
.PARAMETER Configuration
    Build configuration (Debug/Release). Defaults to Release
    
.PARAMETER EnableAOT
    Whether to enable AOT compilation. Defaults to true
    
.EXAMPLE
    .\Generate-CmdPalTrimmingReport.ps1
    
    Runs the complete AOT trimming analysis with default settings
    
.NOTES
    Author: PowerToys CmdPal AOT Analysis Tool
    Purpose: Show types removed when enabling AOT compilation
#>

param(
    [string]$Configuration = "Release",
    [bool]$EnableAOT = $true
)

$ErrorActionPreference = "Stop"

Write-Host "======================================" -ForegroundColor Cyan
Write-Host "PowerToys CmdPal AOT Trimming Analysis" -ForegroundColor Cyan  
Write-Host "======================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "PURPOSE: Generate reports showing types removed by AOT optimization" -ForegroundColor Yellow
Write-Host "OUTPUT: TrimmedTypes.md, TrimmedTypes.rd.xml, analysis data" -ForegroundColor Yellow
Write-Host ""

# Get paths
$rootDir = Resolve-Path (Join-Path $PSScriptRoot "..\..")
$cmdPalProject = Join-Path $rootDir "src\modules\cmdpal\Microsoft.CmdPal.UI\Microsoft.CmdPal.UI.csproj"
$cmdPalDir = Split-Path -Parent $cmdPalProject
$analyzerProject = Join-Path $rootDir "tools\TrimmingAnalyzer\TrimmingAnalyzer.csproj"

# Build paths
$tempDir = Join-Path $env:TEMP "CmdPalTrimAnalysis_$(Get-Date -Format 'yyyyMMdd_HHmmss')"
$untrimmedDir = Join-Path $tempDir "untrimmed"
$trimmedDir = Join-Path $tempDir "trimmed"

# Ensure all NuGet packages are restored
Write-Host "Restoring NuGet packages..." -ForegroundColor Yellow
& dotnet restore $cmdPalProject
if ($LASTEXITCODE -ne 0) {
    Write-Warning "Package restore had some issues, but continuing..."
}

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

    # Build Debug mode without AOT (baseline for comparison)
    Write-Host "Building CmdPal in Debug mode without AOT (baseline)..." -ForegroundColor Yellow
    & dotnet publish $cmdPalProject `
        --configuration Debug `
        --runtime win-x64 `
        --self-contained true `
        --property:PublishTrimmed=false `
        --property:EnableCmdPalAOT=false `
        --property:PublishAot=false `
        --verbosity minimal

    if ($LASTEXITCODE -ne 0) {
        throw "Failed to build Debug baseline version"
    }

    # Copy baseline (Debug without AOT) output to analysis directory
    $baselineOutput = Join-Path $cmdPalDir "bin\Debug\net9.0-windows10.0.26100.0\win-x64\publish"
    Copy-Item "$baselineOutput\Microsoft.CmdPal.UI.dll" $untrimmedDir -Force
    # Copy all dependencies to help with assembly resolution
    Get-ChildItem "$baselineOutput\*.dll" | ForEach-Object { 
        if ($_.Name -ne "Microsoft.CmdPal.UI.dll") {
            Copy-Item $_.FullName $untrimmedDir -Force -ErrorAction SilentlyContinue
        }
    }
    Write-Host "Copied Debug baseline DLLs from: $baselineOutput"

    # Build Release mode with AOT enabled
    Write-Host "Building CmdPal in Release mode with AOT enabled..." -ForegroundColor Yellow
    & dotnet publish $cmdPalProject `
        --configuration Release `
        --runtime win-x64 `
        --self-contained true `
        --property:PublishTrimmed=false `
        --property:EnableCmdPalAOT=true `
        --property:PublishAot=true `
        --verbosity minimal

    if ($LASTEXITCODE -ne 0) {
        throw "Failed to build AOT+trimmed version"
    }

    # Copy AOT+trimmed output to analysis directory
    $trimmedOutput = Join-Path $cmdPalDir "bin\$Configuration\net9.0-windows10.0.26100.0\win-x64\publish"
    Copy-Item "$trimmedOutput\Microsoft.CmdPal.UI.dll" $trimmedDir -Force
    # Copy all dependencies to help with assembly resolution
    Get-ChildItem "$trimmedOutput\*.dll" | ForEach-Object { 
        if ($_.Name -ne "Microsoft.CmdPal.UI.dll") {
            Copy-Item $_.FullName $trimmedDir -Force -ErrorAction SilentlyContinue
        }
    }
    Write-Host "Copied Release AOT DLLs from: $trimmedOutput"

    # Use new directory comparison method to compare all types
    Write-Host "Analyzing differences (Debug baseline vs Release AOT)..." -ForegroundColor Yellow
    Write-Host "Using advanced directory comparison to detect AOT-optimized types..." -ForegroundColor Cyan
    
    # Use the new directory comparison feature
    & $analyzerPath --compare-directories "$untrimmedDir" "$trimmedDir" "$cmdPalDir" "rdxml,markdown,json" "TrimmedTypes"
    
    if ($LASTEXITCODE -eq 0) {
        Write-Host "Analysis completed successfully!" -ForegroundColor Green
    } else {
        throw "Analysis failed with exit code $LASTEXITCODE"
    }

    Write-Host "`n===== Analysis Complete =====" -ForegroundColor Cyan
    Write-Host "Reports generated in: $cmdPalDir" -ForegroundColor Green
    
    # List the main combined reports
    $mainReports = @("TrimmedTypes.md", "TrimmedTypes.rd.xml")
    foreach ($report in $mainReports) {
        $reportPath = Join-Path $cmdPalDir $report
        if (Test-Path $reportPath) {
            Write-Host "  - $report" -ForegroundColor Green
        }
    }

} finally {
    # Cleanup
    if (Test-Path $tempDir) {
        Remove-Item -Path $tempDir -Recurse -Force -ErrorAction SilentlyContinue
    }
}