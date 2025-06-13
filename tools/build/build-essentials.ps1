# PowerToys Build Script
# This script checks for MSBuild and then builds the solution.
# 
# Parameters:
#   -Configuration: Build configuration (Debug/Release, default: Debug)
#   -Platform: Target platform (x64/arm64, default: x64)
#   -Clean: Clean the solution before building
#   -Rebuild: Rebuild the solution (clean + build)
#   -Component: Specify which component to build (powertoys, bugreporttool, stylesreporttool)

param(
    [string]$Configuration = "Debug",
    [string]$Platform      = "x64",
    [switch]$Clean,
    [switch]$Rebuild,
    [ValidateSet("powertoys", "bugreporttool", "stylesreporttool")]
    [string]$Component = "powertoys"
)

# Import shared build utilities
$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Definition
Import-Module (Join-Path $scriptDir "BuildUtils.psm1") -Force

Write-BuildHeader

try {
    Test-MSBuildAvailability

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
    Write-Host "Component:     $Component"      -ForegroundColor Cyan
    Write-Host "Targets:       $targetString"  -ForegroundColor Cyan
    Write-Host ""

    # Get solution path
    $solutionPath = Get-SolutionPath -Component $Component -ScriptDir $scriptDir
    Write-Host "Building $Component solution..." -ForegroundColor Green

    # Execute build based on targets
    if ($targets -contains "Restore" -and $targets -contains "Build") {
        # Use separate restore and build steps to avoid parameter conflicts
        Invoke-RestoreThenBuild -Solution $solutionPath -Platform $Platform -Configuration $Configuration
    } else {
        # For single-target builds (Clean or Rebuild only)
        $extraArgs = ""
        if ($targets -contains "Restore") {
            $extraArgs = "/p:RestorePackagesConfig=true"
        }
        
        Invoke-MSBuild -Solution $solutionPath -Platform $Platform -Configuration $Configuration -Target $targetString -ExtraArgs $extraArgs -UseMultiProcessor
    }

    Write-Host "$Component build completed successfully!" -ForegroundColor Green
} catch {
    Write-Host "Script Error: $($_.Exception.Message)" -ForegroundColor Red
    if ($_.Exception.InnerException) {
        Write-Host "  Inner Exception: $($_.Exception.InnerException.Message)" -ForegroundColor Red
    }
    exit 1
}

Write-Host "Build script finished." -ForegroundColor Green
