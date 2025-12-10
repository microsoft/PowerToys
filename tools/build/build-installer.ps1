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
    [string]$Version,
    [switch]$EnableCmdPalAOT,
    [switch]$Clean,
    [switch]$SkipBuild,
    [switch]$Help
)

if ($Help) {
    Write-Host "Usage: .\build-installer.ps1 [-Platform <x64|arm64>] [-Configuration <Release|Debug>] [-PerUser <true|false>] [-Version <0.0.1>] [-EnableCmdPalAOT] [-Clean] [-SkipBuild]"
    Write-Host "  -Platform       Target platform (default: auto-detect or x64)"
    Write-Host "  -Configuration  Build configuration (default: Release)"
    Write-Host "  -PerUser        Build per-user installer (default: true)"
    Write-Host "  -Version        Overwrites the PowerToys version (default: from src\Version.props)"
    Write-Host "  -EnableCmdPalAOT Enable AOT compilation for CmdPal (slower build)"
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

$cmdpalOutputPath = Join-Path $repoRoot "$Platform\$Configuration\WinUI3Apps\CmdPal"
$buildOutputPath = Join-Path $repoRoot "$Platform\$Configuration"

# Clean should be done first before any other steps
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

# Set up versioning using versionSetting.ps1 if Version is provided
# These files will be modified by versionSetting.ps1 and need to be restored after build
$versionFilesToRestore = @(
    "src\Version.props",
    "src\CmdPalVersion.props",
    "src\modules\powerrename\PowerRenameContextMenu\AppxManifest.xml",
    "src\modules\imageresizer\ImageResizerContextMenu\AppxManifest.xml",
    "src\modules\FileLocksmith\FileLocksmithContextMenu\AppxManifest.xml",
    "src\modules\NewPlus\NewShellExtensionContextMenu\AppxManifest.xml"
)
$versionFileBackups = @{}

# These files are generated by Terminal versioning and need to be cleaned up after build
$cmdpalGeneratedFiles = @(
    "src\modules\cmdpal\Directory.Build.props",
    "src\modules\cmdpal\Directory.Build.targets",
    "src\modules\cmdpal\Microsoft.CmdPal.UI\BuildInfo.xml",
    "src\modules\cmdpal\Microsoft.CmdPal.UI\GeneratedPackage.appxmanifest",
    "src\modules\cmdpal\ext\ProcessMonitorExtension\BuildInfo.xml",
    "src\modules\cmdpal\ext\ProcessMonitorExtension\GeneratedPackage.appxmanifest",
    "src\modules\cmdpal\ext\SamplePagesExtension\BuildInfo.xml",
    "src\modules\cmdpal\ext\SamplePagesExtension\GeneratedPackage.appxmanifest"
)

# Backup all version files before any versioning scripts run
Write-Host "[VERSION] Backing up version files before modification..."
foreach ($relPath in $versionFilesToRestore) {
    $fullPath = Join-Path $repoRoot $relPath
    if (Test-Path $fullPath) {
        $versionFileBackups[$relPath] = Get-Content $fullPath -Raw
        Write-Host "  Backed up: $relPath"
    }
}

if ($Version) {
    Write-Host "[VERSION] Setting PowerToys version to $Version using versionSetting.ps1..."
    $versionScript = Join-Path $repoRoot ".pipelines\versionSetting.ps1"
    if (Test-Path $versionScript) {
        & $versionScript -versionNumber $Version -DevEnvironment 'Local'
        if (-not $?) {
            Write-Error "versionSetting.ps1 failed"
            exit 1
        }
    } else {
        Write-Error "Could not find versionSetting.ps1 at: $versionScript"
        exit 1
    }
}

Write-Host "[VERSION] Setting up versioning using Microsoft.Windows.Terminal.Versioning..."

# Check for nuget.exe - download to AppData if not available
$nugetDownloaded = $false
$nugetPath = $null
if (-not (Get-Command nuget -ErrorAction SilentlyContinue)) {
    Write-Warning "nuget.exe not found in PATH. Attempting to download..."
    $nugetUrl = "https://dist.nuget.org/win-x86-commandline/latest/nuget.exe"
    $nugetDir = Join-Path $env:LOCALAPPDATA "PowerToys\BuildTools"
    if (-not (Test-Path $nugetDir)) { New-Item -ItemType Directory -Path $nugetDir -Force | Out-Null }
    $nugetPath = Join-Path $nugetDir "nuget.exe"
    if (-not (Test-Path $nugetPath)) {
        try {
            Invoke-WebRequest $nugetUrl -OutFile $nugetPath
            $nugetDownloaded = $true
        } catch {
            Write-Error "Failed to download nuget.exe. Please install it manually and add to PATH."
            exit 1
        }
    }
    $env:Path += ";$nugetDir"
}

# Install Terminal versioning package to AppData
$versioningDir = Join-Path $env:LOCALAPPDATA "PowerToys\BuildTools\.versioning"
if (-not (Test-Path $versioningDir)) { New-Item -ItemType Directory -Path $versioningDir -Force | Out-Null }

$configFile = Join-Path $repoRoot ".pipelines\release-nuget.config"

# Install the package
# Use -ExcludeVersion to make the path predictable
nuget install Microsoft.Windows.Terminal.Versioning -ConfigFile $configFile -OutputDirectory $versioningDir -ExcludeVersion -NonInteractive

$versionRoot = Join-Path $versioningDir "Microsoft.Windows.Terminal.Versioning"
$setupScript = Join-Path $versionRoot "build\Setup.ps1"

if (Test-Path $setupScript) {
    & $setupScript -ProjectDirectory (Join-Path $repoRoot "src\modules\cmdpal") -Verbose
} else {
    Write-Error "Could not find Setup.ps1 in $versionRoot"
}

# WiX v5 projects use WixToolset.Sdk via NuGet/MSBuild; no separate WiX installation is required.
Write-Host ("[PIPELINE] Start | Platform={0} Configuration={1} PerUser={2}" -f $Platform, $Configuration, $PerUser)
Write-Host ''

$commonArgs = '/p:CIBuild=true /p:IsPipeline=true'

if ($EnableCmdPalAOT) {
    $commonArgs += " /p:EnableCmdPalAOT=true"
}

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

# Restore version files if they were backed up
if ($versionFileBackups.Count -gt 0) {
    Write-Host "[VERSION] Restoring version files to original state..."
    foreach ($relPath in $versionFileBackups.Keys) {
        $fullPath = Join-Path $repoRoot $relPath
        Set-Content -Path $fullPath -Value $versionFileBackups[$relPath] -NoNewline
        Write-Host "  Restored: $relPath"
    }
}

# Clean up cmdpal generated files from Terminal versioning
Write-Host "[CLEANUP] Removing cmdpal generated files..."
foreach ($relPath in $cmdpalGeneratedFiles) {
    $fullPath = Join-Path $repoRoot $relPath
    if (Test-Path $fullPath) {
        Remove-Item $fullPath -Force
        Write-Host "  Removed: $relPath"
    }
}

Write-Host '[PIPELINE] Completed'
