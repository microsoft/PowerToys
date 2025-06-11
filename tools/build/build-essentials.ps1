# PowerToys Build Script
# This script checks for MSBuild and then builds the solution.

param(
    [string]$Configuration = "Debug",
    [string]$Platform      = "x64",
    [switch]$Clean,
    [switch]$Rebuild
)

Write-Host "PowerToys Build Script" -ForegroundColor Green
Write-Host "=======================" -ForegroundColor Green

try {
    Write-Host "Checking for MSBuild availability..." -ForegroundColor Yellow
    $msbuildExists = Get-Command msbuild -ErrorAction SilentlyContinue
    if (-not $msbuildExists) {
        Write-Host "MSBuild not found in the current environment." -ForegroundColor Red
        Write-Host "Please ensure you are running this script from a Visual Studio Developer Command Prompt," -ForegroundColor Red
        Write-Host "or that your VS Code terminal is configured to use the Developer PowerShell profile." -ForegroundColor Red
        Write-Host "Alternatively, ensure MSBuild.exe is in your system's PATH." -ForegroundColor Red
        Write-Host "You can find MSBuild typically in: C:\Program Files\Microsoft Visual Studio\2022\<Edition>\MSBuild\Current\Bin\MSBuild.exe" -ForegroundColor Red
        throw "MSBuild is required to build the solution. Please configure your environment."
    } else {
        Write-Host "MSBuild found at: $($msbuildExists.Source)" -ForegroundColor Green
    }

    # Section 2: Determine build targets
    Write-Host "Determining build targets..." -ForegroundColor Yellow
    $targets = [System.Collections.Generic.List[string]]::new()

    if ($Clean.IsPresent) {
        $targets.Add("Clean")
        Write-Host "Will clean the solution" -ForegroundColor Cyan
    }

    if ($Rebuild.IsPresent) {
        $targets.Add("Rebuild")
        Write-Host "Will rebuild the solution" -ForegroundColor Cyan
    } else {
        $targets.Add("Restore")
        $targets.Add("Build")
        Write-Host "Will restore packages and build the solution" -ForegroundColor Cyan
    }

    $targetString = ($targets | Select-Object -Unique) -join ";"
    Write-Host "Starting build process..." -ForegroundColor Yellow
    Write-Host "Configuration: $Configuration" -ForegroundColor Cyan
    Write-Host "Platform:      $Platform"      -ForegroundColor Cyan
    Write-Host "Targets:       $targetString"  -ForegroundColor Cyan
    Write-Host ""

    $scriptDir    = Split-Path -Parent $MyInvocation.MyCommand.Definition
    $solutionPath = Join-Path $scriptDir "..\..\PowerToys.sln"
    $solutionPath = [System.IO.Path]::GetFullPath($solutionPath)

    # ---------- buildArgs ----------
    $buildArgs = @(
        $solutionPath
        "/t:$targetString"
        "/p:Configuration=$Configuration"
        "/p:Platform=$Platform"
        "/m"
        "/verbosity:normal"
    )

    # Add RestorePackagesConfig flag when restoring NuGet packages
    if ($targets -contains "Restore") {
        $buildArgs += "/p:RestorePackagesConfig=true"
    }
    # --------------------------------

    Write-Host "Executing: msbuild $($buildArgs -join ' ')" -ForegroundColor White
    & msbuild @buildArgs

    if ($LASTEXITCODE -eq 0) {
        Write-Host "Build completed successfully!" -ForegroundColor Green
    } else {
        Write-Host "Build failed with exit code: $LASTEXITCODE" -ForegroundColor Red
        throw "Build process failed."
    }
} catch {
    Write-Host "Script Error: $($_.Exception.Message)" -ForegroundColor Red
    if ($_.Exception.InnerException) {
        Write-Host "  Inner Exception: $($_.Exception.InnerException.Message)" -ForegroundColor Red
    }
    exit 1
}

Write-Host "Build script finished." -ForegroundColor Green
