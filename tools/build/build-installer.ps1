<#
.SYNOPSIS
Build and package PowerToys (CmdPal and installer) for a specific platform and configuration LOCALLY.

.DESCRIPTION
Builds and packages PowerToys (CmdPal and installer) locally. Handles solution build, signing, and WiX installer generation.

.PARAMETER Platform
Specifies the target platform for the build (e.g., 'arm64', 'x64'). Default is 'x64'.

.PARAMETER Configuration
Specifies the build configuration (e.g., 'Debug', 'Release'). Default is 'Release'.

.PARAMETER PerUser
Specifies whether to build a per-user installer (true) or machine-wide installer (false). Default is true (per-user).

.EXAMPLE
.\build-installer.ps1
Runs the installer build pipeline for x64 Release.

.EXAMPLE
.\build-installer.ps1 -Platform x64 -Configuration Release
Runs the pipeline for x64 Release.

.EXAMPLE
.\build-installer.ps1 -Platform x64 -Configuration Release -PerUser false
Runs the pipeline for x64 Release with machine-wide installer.

.NOTES
- Make sure to run this script from a Developer PowerShell (e.g., VS2022 Developer PowerShell).
- Generated MSIX files will be signed using cert-sign-package.ps1.
- This script will clean previous outputs under the build directories and installer directory (except *.exe files).
- First time run need admin permission to trust the certificate.
- The built installer will be placed under: installer/PowerToysSetupVNext/[Platform]/[Configuration]/User[Machine]Setup 
  relative to the solution root directory.
- To run the full installation in other machines, call "./cert-management.ps1" to export the cert used to sign the packages.
  And trust the cert in the target machine.
#>

param (
    [string]$Platform = 'x64',
    [string]$Configuration = 'Release',
    [string]$PerUser = 'true',
    [switch]$Clean,
    [switch]$SkipBuild,
    [switch]$Help
)

if ($Help) {
    Write-Host "Usage: .\build-installer.ps1 [-Platform <x64|arm64>] [-Configuration <Release|Debug>] [-PerUser <true|false>] [-Clean] [-SkipBuild]"
    Write-Host "  -Platform       Target platform (default: auto-detect or x64)"
    Write-Host "  -Configuration  Build configuration (default: Release)"
    Write-Host "  -PerUser        Build per-user installer (default: true)"
    Write-Host "  -Clean          Clean output directories before building"
    Write-Host "  -SkipBuild      Skip building the main solution and tools (assumes they are already built)"
    Write-Host "  -Help           Show this help message"
    exit 0
}

# Ensure helpers are available
. "$PSScriptRoot\build-common.ps1"

# Initialize Visual Studio dev environment
if (-not (Ensure-VsDevEnvironment)) { exit 1 }

# Auto-detect platform when not provided
if (-not $Platform -or $Platform -eq '') {
    try {
        $Platform = Get-DefaultPlatform
        Write-Host ("[AUTO-PLATFORM] Detected platform: {0}" -f $Platform)
    } catch {
        Write-Warning "Failed to auto-detect platform; defaulting to x64"
        $Platform = 'x64'
    }
}

# Find the PowerToys repository root automatically
$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Definition
$repoRoot = $scriptDir

# Navigate up from the script location to find the repo root
# Script is typically in tools\build, so go up two levels
while ($repoRoot -and -not (Test-Path (Join-Path $repoRoot "PowerToys.slnx"))) {
    $parentDir = Split-Path -Parent $repoRoot
    if ($parentDir -eq $repoRoot) {
        # Reached the root of the drive, PowerToys.slnx not found
        Write-Error "Could not find PowerToys repository root. Make sure this script is in the PowerToys repository."
        exit 1
    }
    $repoRoot = $parentDir
}

if (-not $repoRoot -or -not (Test-Path (Join-Path $repoRoot "PowerToys.slnx"))) {
    Write-Error "Could not locate PowerToys.slnx. Please ensure this script is run from within the PowerToys repository."
    exit 1
}

Write-Host "PowerToys repository root detected: $repoRoot"
# WiX v5 projects use WixToolset.Sdk via NuGet/MSBuild; no separate WiX installation is required.
Write-Host ("[PIPELINE] Start | Platform={0} Configuration={1} PerUser={2}" -f $Platform, $Configuration, $PerUser)
Write-Host ''

$cmdpalOutputPath = Join-Path $repoRoot "$Platform\$Configuration\WinUI3Apps\CmdPal"
$buildOutputPath = Join-Path $repoRoot "$Platform\$Configuration"

if ($Clean) {
    if (Test-Path $cmdpalOutputPath) {
        Write-Host "[CLEAN] Removing previous output: $cmdpalOutputPath"
        Remove-Item $cmdpalOutputPath -Recurse -Force -ErrorAction Ignore
    }
    if (Test-Path $buildOutputPath) {
        Write-Host "[CLEAN] Removing previous build output: $buildOutputPath"
        Remove-Item $buildOutputPath -Recurse -Force -ErrorAction Ignore
    }
    
    Write-Host "[CLEAN] Cleaning all build artifacts (git clean -Xfd)..."
    Push-Location $repoRoot
    try {
        git clean -Xfd | Out-Null
    } catch {
        Write-Warning "[CLEAN] git clean failed: $_"
    } finally {
        Pop-Location
    }

    Write-Host "[CLEAN] Cleaning solution (msbuild /t:Clean)..."
    RunMSBuild 'PowerToys.slnx' '/t:Clean' $Platform $Configuration
}

$commonArgs = '/p:CIBuild=true /p:IsPipeline=true'
# No local projects found (or continuing) - build full solution and tools
if (-not $SkipBuild) {
    RestoreThenBuild 'PowerToys.slnx' $commonArgs $Platform $Configuration
}

$msixSearchRoot = Join-Path $repoRoot "$Platform\$Configuration"
$msixFiles = Get-ChildItem -Path $msixSearchRoot -Recurse -Filter *.msix |
Select-Object -ExpandProperty FullName

if ($msixFiles.Count) {
    Write-Host ("[SIGN] .msix file(s): {0}" -f ($msixFiles -join '; '))
    & (Join-Path $PSScriptRoot "cert-sign-package.ps1") -TargetPaths $msixFiles
}
else {
    Write-Warning "[SIGN] No .msix files found in $msixSearchRoot"
}

# Generate DSC manifest files
Write-Host '[DSC] Generating DSC manifest files...'
$dscScriptPath = Join-Path $repoRoot '.\tools\build\generate-dsc-manifests.ps1'
if (Test-Path $dscScriptPath) {
    & $dscScriptPath -BuildPlatform $Platform -BuildConfiguration $Configuration -RepoRoot $repoRoot
    if ($LASTEXITCODE -ne 0) {
        Write-Error "DSC manifest generation failed with exit code $LASTEXITCODE"
        exit 1
    }
    Write-Host '[DSC] DSC manifest files generated successfully'
} else {
    Write-Warning "[DSC] DSC manifest generator script not found at: $dscScriptPath"
}

if (-not $SkipBuild) {
    RestoreThenBuild 'tools\BugReportTool\BugReportTool.sln' $commonArgs $Platform $Configuration
    RestoreThenBuild 'tools\StylesReportTool\StylesReportTool.sln' $commonArgs $Platform $Configuration
}

if ($Clean) {
    Write-Host '[CLEAN] installer (keep *.exe)'
    Push-Location $repoRoot
    try {
        git clean -xfd -e '*.exe' -- .\installer\ | Out-Null
    } finally {
        Pop-Location
    }
}

RunMSBuild 'installer\PowerToysSetup.slnx' "$commonArgs /t:restore /p:RestorePackagesConfig=true" $Platform $Configuration

RunMSBuild 'installer\PowerToysSetup.slnx' "$commonArgs /m /t:PowerToysInstallerVNext /p:PerUser=$PerUser" $Platform $Configuration

# Fix: WiX v5 locally puts the MSI in an 'en-us' subfolder, but the Bootstrapper expects it in the root of UserSetup/MachineSetup.
# We move it up one level to match expectations.
$setupType = if ($PerUser -eq 'true') { 'UserSetup' } else { 'MachineSetup' }
$msiParentDir = Join-Path $repoRoot "installer\PowerToysSetupVNext\$Platform\$Configuration\$setupType"
$msiEnUsDir = Join-Path $msiParentDir "en-us"

if (Test-Path $msiEnUsDir) {
    Write-Host "[FIX] Moving MSI files from $msiEnUsDir to $msiParentDir"
    Get-ChildItem -Path $msiEnUsDir -Filter *.msi | Move-Item -Destination $msiParentDir -Force
}

RunMSBuild 'installer\PowerToysSetup.slnx' "$commonArgs /m /t:PowerToysBootstrapperVNext /p:PerUser=$PerUser" $Platform $Configuration

Write-Host '[PIPELINE] Completed'
