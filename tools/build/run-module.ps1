<#
.SYNOPSIS
Builds a single PowerToys module and launches it in a locally assembled dev layout.

.DESCRIPTION
Wraps the common inner loop for module development:
1. Builds the projects under src\modules\<Module> (test projects are skipped, pass
   -IncludeTests to build them).
2. Builds the runner if it has not been built for the requested configuration yet.
3. Assembles a runnable layout through copying the runner's runtime files (Windows App SDK dlls/winmds,
   .pri, locale folders, svgs) next to the module outputs in the repo-root
   x64\<Configuration> folder.
4. Launches the runner unless -NoLaunch is specified.

Exit any installed/running PowerToys instance before running, since two runners compete for keyboard
hooks and hotkeys, and dev/installed builds share the same settings file.

.PARAMETER Module
Module folder name under src\modules (e.g. 'peek'). Case-insensitive.

.PARAMETER Configuration
Build configuration. Default: 'Debug'.

.PARAMETER Platform
Target platform (e.g., 'x64', 'arm64'). If omitted the script attempts to detect the host platform.

.PARAMETER IncludeTests
Also build the module's test projects. Skipped by default because test projects dominate
build time and disk usage during module iteration.

.PARAMETER NoLaunch
Build and assemble the layout, but do not start the runner.

.PARAMETER ExtraArgs
Additional arguments forwarded to MSBuild.

.EXAMPLE
.\tools\build\run-module.ps1 -Module peek
Builds the Peek module (without tests) and launches it.

.EXAMPLE
.\tools\build\run-module.ps1 -Module peek -Configuration Release -NoLaunch
Builds Peek for Release and assembles x64\Release without launching.

.NOTES
- Requires build-common.ps1 in the same folder.
- The first run needs a restore of the C++ NuGet packages.
  This script restores each project before building, so no extra step is needed.
#>

param (
    [Parameter(Mandatory = $true)]
    [string]$Module,
    [string]$Configuration = 'Debug',
    [string]$Platform = '',
    [switch]$IncludeTests,
    [switch]$NoLaunch,
    [Parameter(ValueFromRemainingArguments = $true)]
    [string[]]$ExtraArgs
)

$ErrorActionPreference = 'Stop'

. "$PSScriptRoot\build-common.ps1"

# Initialize Visual Studio dev environment
if (-not (Ensure-VsDevEnvironment)) { exit 1 }

# Repo that contains this script, everything is discovered relative to it
$repoRoot = Get-ProjectRepoRoot "$PSScriptRoot\"

# Auto-detect platform when not provided
if (-not $Platform -or $Platform -eq '') {
    try {
        $Platform = Get-DefaultPlatform
        Write-Host ("[RUN-MODULE] Detected platform: {0}" -f $Platform)
    } catch {
        Write-Warning "Failed to auto-detect platform, defaulting to x64"
        $Platform = 'x64'
    }
}

# Locate the module folder
$moduleDir = Join-Path $repoRoot "src\modules\$Module"
if (-not (Test-Path $moduleDir)) {
    $candidate = Get-ChildItem -Path (Join-Path $repoRoot 'src\modules') -Directory |
        Where-Object { $_.Name -ieq $Module } |
        Select-Object -First 1
    if ($candidate) { $moduleDir = $candidate.FullName }
}
if (-not (Test-Path $moduleDir)) {
    Write-Error ("Module folder not found: no directory matching '{0}' under src\modules" -f $Module)
    exit 1
}
Write-Host ("[RUN-MODULE] Module folder: {0}" -f $moduleDir)

$extraArgsString = $null
if ($ExtraArgs -and $ExtraArgs.Count -gt 0) { $extraArgsString = ($ExtraArgs -join ' ') }

# Build the module's projects (tests skipped unless -IncludeTests)
$built = BuildProjectsInDirectory -DirectoryPath $moduleDir -ExtraArgs $extraArgsString `
    -Platform $Platform -Configuration $Configuration -SkipTests:(-not $IncludeTests)
if (-not $built) {
    Write-Error ("No buildable projects found under {0}" -f $moduleDir)
    exit 1
}

# Build the runner if it has not been built for this configuration yet
$runnerExe = Join-Path $repoRoot "src\runner\x64\$Configuration\PowerToys.exe"
if (-not (Test-Path $runnerExe)) {
    Write-Host "[RUN-MODULE] Runner not built for $Configuration, building it once."
    RestoreThenBuild (Join-Path $repoRoot 'src\runner\Runner.vcxproj') $extraArgsString $Platform $Configuration
}

# Put runner runtime files next to the module outputs.
# The module outputs already land in <repo>\x64\<Configuration>\WinUI3Apps, but the runner
# needs its own runtime files in <repo>\x64\<Configuration>.
# Never /MIR so freshly built module outputs are not deleted.
$runnerDir = Join-Path $repoRoot "src\runner\x64\$Configuration"
$outDir = Join-Path $repoRoot "x64\$Configuration"
Write-Host "[RUN-MODULE] Assembling layout: $runnerDir -> $outDir"
robocopy $runnerDir $outDir /E /XD WinUI3Apps /NFL /NDL /NJH /NJS | Out-Null
if ($LASTEXITCODE -ge 8) {
    Write-Error "robocopy failed with exit code $LASTEXITCODE while assembling the layout."
    exit 1
}
$LASTEXITCODE = 0

if ($NoLaunch) {
    Write-Host "[RUN-MODULE] Done. Launch $($outDir -replace '\\$','')\PowerToys.exe when ready."
    exit 0
}

# Warn about (and stop) dev runners
# Refuse to fight with an installed PowerToys quietly
$running = Get-Process -Name 'PowerToys' -ErrorAction SilentlyContinue
if ($running) {
    $installed = $running | Where-Object { $_.Path -and ($_.Path -notlike "$repoRoot*") }
    if ($installed) {
        Write-Warning "An installed PowerToys is running ($($installed[0].Path)). Exit it first; two runners compete for keyboard hooks and hotkeys."
        exit 1
    }
    Write-Host "[RUN-MODULE] Stopping previously launched dev runner(s)."
    $running | Stop-Process -Force
    Start-Sleep -Seconds 2
}

Write-Host "[RUN-MODULE] Launching $outDir\PowerToys.exe"
Start-Process -FilePath (Join-Path $outDir 'PowerToys.exe') -WorkingDirectory $outDir
