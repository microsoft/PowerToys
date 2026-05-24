<#
.SYNOPSIS
Build essential native PowerToys projects (runner and settings), restoring NuGet packages first.

.DESCRIPTION
Lightweight script to build a small set of essential C++ projects used by PowerToys' runner and native modules. This script first restores NuGet packages for the full solution (`PowerToys.slnx`) and then builds the runner and settings projects. Intended for fast local builds during development.

.PARAMETER Platform
Target platform for the build (for example: 'x64', 'arm64'). If omitted the script will attempt to auto-detect the host platform.

.PARAMETER Configuration
Build configuration (for example: 'Debug' or 'Release'). Default is 'Debug'.

.EXAMPLE
.\tools\build\build-essentials.ps1
Restores packages for the solution and builds the default set of native projects using the auto-detected platform and Debug configuration.

.EXAMPLE
.\tools\build\build-essentials.ps1 -Platform arm64 -Configuration Release
Restores packages and builds the essentials in Release mode for ARM64, even if your machine is running on x64.

.NOTES
- This script dot-sources `build-common.ps1` and uses the shared helper `RunMSBuild`.
- It will call `RestoreThenBuild 'PowerToys.slnx'` before building the essential projects to ensure NuGet packages are restored.
- The script attempts to locate the repository root automatically and can be run from any folder inside the repo.
#>

param (
    [string]$Platform = '',
    [string]$Configuration = 'Debug'
)

# Find repository root starting from the script location
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Definition
$repoRoot = $ScriptDir
while ($repoRoot -and -not (Test-Path (Join-Path $repoRoot "PowerToys.slnx"))) {
    $parent = Split-Path -Parent $repoRoot
    if ($parent -eq $repoRoot) {
        Write-Error "Could not find PowerToys repository root."
        exit 1
    }
    $repoRoot = $parent
}

# Export script-scope variables used by build-common helpers
Set-Variable -Name RepoRoot -Value $repoRoot -Scope Script -Force

# Load shared helpers
. "$PSScriptRoot\build-common.ps1"

# Initialize Visual Studio dev environment
if (-not (Ensure-VsDevEnvironment)) { exit 1 }

# If platform not provided, auto-detect from host
if (-not $Platform -or $Platform -eq '') {
    try {
        $Platform = Get-DefaultPlatform
        Write-Host ("[AUTO-PLATFORM] Detected platform: {0}" -f $Platform)
    } catch {
        Write-Warning "Failed to auto-detect platform; defaulting to 'x64'"
        $Platform = 'x64'
    }
}

# Ensure solution packages are restored
RestoreThenBuild 'PowerToys.slnx' '' $Platform $Configuration $true

# Build both runner and settings
$ProjectsToBuild = @(".\src\runner\runner.vcxproj", ".\src\settings-ui\Settings.UI\PowerToys.Settings.csproj")
$ExtraArgs = "/p:SolutionDir=$repoRoot\"
foreach ($proj in $ProjectsToBuild) {
    Write-Host ("[BUILD-ESSENTIALS] Building {0}" -f $proj)
    RunMSBuild $proj $ExtraArgs $Platform $Configuration
}
