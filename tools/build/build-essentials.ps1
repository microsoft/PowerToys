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

# Detect vcpkg using the Windows Terminal pattern: prefer VS-shipped vcpkg
# (Microsoft.VisualStudio.Component.Vcpkg), fall back to a runtime clone at
# deps/vcpkg if VS doesn't have it. Either way, set VCPKG_ROOT so MSBuild
# picks it up via the three-tier VcpkgRoot fallback in Cpp.Build.props
# (env var > VS-shipped > local clone). Idempotent.
#
# The fallback clone is pinned to vcpkg.json's `builtin-baseline` so local
# builds match what CI uses and don't drift as vcpkg HEAD changes. If an
# existing deps/vcpkg clone is at a different commit, it's checked out to
# the baseline before bootstrap (the manifest baseline is the source of
# truth; the clone is just a delivery mechanism).
if (-not $env:VCPKG_ROOT) {
    $vswhere = 'C:\Program Files (x86)\Microsoft Visual Studio\Installer\vswhere.exe'
    $vsInstallRoot = $null
    if (Test-Path $vswhere) {
        $vsInstallRoot = & $vswhere -latest -prerelease -requires Microsoft.VisualStudio.Component.Vcpkg -property installationPath 2>$null
    }
    if ($vsInstallRoot) {
        $env:VCPKG_ROOT = Join-Path $vsInstallRoot 'VC\vcpkg'
        Write-Host "[BUILD-ESSENTIALS] Using vcpkg from Visual Studio installation ($env:VCPKG_ROOT)"
    } else {
        $manifest = Get-Content (Join-Path $repoRoot 'vcpkg.json') -Raw | ConvertFrom-Json
        $baseline = $manifest.'builtin-baseline'
        if (-not $baseline) { Write-Error "vcpkg.json is missing 'builtin-baseline'."; exit 1 }
        $localVcpkg = Join-Path $repoRoot 'deps\vcpkg'
        $needClone = -not (Test-Path (Join-Path $localVcpkg '.git'))
        if (-not $needClone) {
            $currentHead = (& git -C $localVcpkg rev-parse HEAD 2>$null).Trim()
            if ($currentHead -ne $baseline) {
                Write-Host "[BUILD-ESSENTIALS] Existing deps/vcpkg at $currentHead; checking out baseline $baseline"
                & git -C $localVcpkg fetch --quiet origin $baseline
                if ($LASTEXITCODE -ne 0) { Write-Warning "[BUILD-ESSENTIALS] fetch failed, re-cloning"; $needClone = $true } else {
                    & git -C $localVcpkg checkout --quiet $baseline
                    if ($LASTEXITCODE -ne 0) { Write-Error "git checkout $baseline in vcpkg failed (exit $LASTEXITCODE)"; exit 1 }
                }
            }
        }
        if ($needClone) {
            Write-Host "[BUILD-ESSENTIALS] Cloning microsoft/vcpkg to $localVcpkg pinned to baseline $baseline"
            if (Test-Path $localVcpkg) { Remove-Item -Recurse -Force $localVcpkg }
            & git clone https://github.com/microsoft/vcpkg $localVcpkg
            if ($LASTEXITCODE -ne 0) { Write-Error "git clone vcpkg failed (exit $LASTEXITCODE)"; exit 1 }
            & git -C $localVcpkg checkout --quiet $baseline
            if ($LASTEXITCODE -ne 0) { Write-Error "git checkout $baseline in vcpkg failed (exit $LASTEXITCODE)"; exit 1 }
        }
        if (-not (Test-Path (Join-Path $localVcpkg 'vcpkg.exe'))) {
            Push-Location $localVcpkg
            try {
                & .\bootstrap-vcpkg.bat -disableMetrics
                if ($LASTEXITCODE -ne 0) { Write-Error "bootstrap-vcpkg failed (exit $LASTEXITCODE)"; exit 1 }
            } finally { Pop-Location }
        }
        $env:VCPKG_ROOT = $localVcpkg
        Write-Host "[BUILD-ESSENTIALS] Using vcpkg from local checkout ($env:VCPKG_ROOT) at $baseline"
    }
}

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
