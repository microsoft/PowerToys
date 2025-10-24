param (
    [string]$Platform = '',
    [string]$Configuration = 'Release',
    [string]$PerUser = 'true',
    [string]$InstallerSuffix = 'wix5'
)

# Set NUGET_PACKAGES environment variable
$env:NUGET_PACKAGES = "$env:USERPROFILE\.nuget\packages"

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
while ($repoRoot -and -not (Test-Path (Join-Path $repoRoot "PowerToys.sln"))) {
    $parentDir = Split-Path -Parent $repoRoot
    if ($parentDir -eq $repoRoot) {
        # Reached the root of the drive, PowerToys.sln not found
        Write-Error "Could not find PowerToys repository root. Make sure this script is in the PowerToys repository."
        exit 1
    }
    $repoRoot = $parentDir
}

if (-not $repoRoot -or -not (Test-Path (Join-Path $repoRoot "PowerToys.sln"))) {
    Write-Error "Could not locate PowerToys.sln. Please ensure this script is run from within the PowerToys repository."
    exit 1
}

Write-Host "PowerToys repository root detected: $repoRoot"
# WiX v5 projects use WixToolset.Sdk via NuGet/MSBuild; a separate WiX 3 installation is not required here.
Write-Host ("[PIPELINE] Start | Platform={0} Configuration={1} PerUser={2}" -f $Platform, $Configuration, $PerUser)
Write-Host ''


$commonArgs = '/p:CIBuild=true'
# No local projects found (or continuing) - build full solution and tools

RestoreThenBuild 'PowerToys.sln' $commonArgs $Platform $Configuration

RestoreThenBuild 'tools\BugReportTool\BugReportTool.sln' $commonArgs $Platform $Configuration
RestoreThenBuild 'tools\StylesReportTool\StylesReportTool.sln' $commonArgs $Platform $Configuration

Write-Host '[CLEAN] installer (keep *.exe)'
Push-Location $repoRoot
try {
    git clean -xfd -e '*.exe' -- .\installer\ | Out-Null
} finally {
    Pop-Location
}

RunMSBuild 'installer\PowerToysSetup.sln' "$commonArgs /t:restore /p:RestorePackagesConfig=true" $Platform $Configuration

RunMSBuild 'installer\PowerToysSetup.sln' "$commonArgs /m /t:PowerToysInstallerVNext /p:PerUser=$PerUser /p:InstallerSuffix=$InstallerSuffix" $Platform $Configuration

RunMSBuild 'installer\PowerToysSetup.sln' "$commonArgs /m /t:PowerToysBootstrapperVNext /p:PerUser=$PerUser /p:InstallerSuffix=$InstallerSuffix" $Platform $Configuration

Write-Host '[PIPELINE] Completed'
