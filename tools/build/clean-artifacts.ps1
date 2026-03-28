<#
.SYNOPSIS
Cleans PowerToys build artifacts to resolve build errors.

.DESCRIPTION
Use this script when you encounter build errors about missing image files or corrupted
build state. It removes build output folders and optionally runs MSBuild Clean.

.PARAMETER SkipMSBuildClean
Skip running MSBuild Clean target, only delete folders.

.EXAMPLE
.\tools\build\clean-artifacts.ps1

.EXAMPLE
.\tools\build\clean-artifacts.ps1 -SkipMSBuildClean
#>

param (
    [switch]$SkipMSBuildClean
)

$ErrorActionPreference = 'Continue'

$scriptDir = $PSScriptRoot
$repoRoot = (Resolve-Path "$scriptDir\..\..").Path

Write-Host "Cleaning build artifacts..."
Write-Host ""

# Run MSBuild Clean
if (-not $SkipMSBuildClean) {
    $vsWhere = "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe"
    if (Test-Path $vsWhere) {
        $vsPath = & $vsWhere -latest -products * -requires Microsoft.Component.MSBuild -property installationPath
        if ($vsPath) {
            $msbuildPath = Join-Path $vsPath "MSBuild\Current\Bin\MSBuild.exe"
            if (Test-Path $msbuildPath) {
                $solutionFile = Join-Path $repoRoot "PowerToys.sln"
                if (-not (Test-Path $solutionFile)) {
                    $solutionFile = Join-Path $repoRoot "PowerToys.slnx"
                }

                if (Test-Path $solutionFile) {
                    Write-Host "  Running MSBuild Clean..."
                    foreach ($plat in @('x64', 'ARM64')) {
                        foreach ($config in @('Debug', 'Release')) {
                            & $msbuildPath $solutionFile /t:Clean /p:Platform=$plat /p:Configuration=$config /verbosity:quiet 2>&1 | Out-Null
                        }
                    }
                    Write-Host "  Done."
                }
            }
        }
    }
}

# Delete build folders
$folders = @('x64', 'ARM64', 'Debug', 'Release', 'packages')
$deleted = @()

foreach ($folder in $folders) {
    $fullPath = Join-Path $repoRoot $folder
    if (Test-Path $fullPath) {
        Write-Host "  Removing $folder/"
        try {
            Remove-Item -Path $fullPath -Recurse -Force -ErrorAction Stop
            $deleted += $folder
        } catch {
            Write-Host "  Failed to remove $folder/: $_"
        }
    }
}

Write-Host ""
if ($deleted.Count -gt 0) {
    Write-Host "Removed: $($deleted -join ', ')"
} else {
    Write-Host "No build folders found to remove."
}

Write-Host ""
Write-Host "To rebuild, run:"
Write-Host "  msbuild -restore -p:RestorePackagesConfig=true -p:Platform=x64 -m PowerToys.slnx"
