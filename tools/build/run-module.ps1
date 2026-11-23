<#
.SYNOPSIS
Builds one or more PowerToys modules and launches them in a locally assembled dev layout.

.DESCRIPTION
Wraps the common inner loop for module development:
1. Builds the projects under src\modules\<Module> for each requested module (test projects
   are skipped unless -IncludeTests is passed).
2. Builds the essentials (runner, Settings UI, Quick Access UI) that are missing for the
   requested configuration.
3. Assembles a runnable layout through copying the runner's runtime files (Windows App SDK dlls/winmds,
   .pri, locale folders, svgs) next to the module outputs in the repo-root
   x64\<Configuration> folder.
4. Launches the runner unless -NoLaunch is specified.

Module outputs accumulate in the layout, so several modules can be built into one runner.
Since the runner is built only once and the layout copy never deletes existing output,
you can pass them all at once, or run the script once per module.

Exit any installed/running PowerToys instance before running, since two runners compete for keyboard
hooks and hotkeys, and dev/installed builds share the same settings file.

.PARAMETER Module
One or more module folder names under src\modules (e.g. 'peek'). Case-insensitive.
To build several, write them as -Module peek,poweraccent

.PARAMETER Configuration
Build configuration. Default: 'Debug'.

.PARAMETER Platform
Target platform (e.g., 'x64', 'arm64'). If omitted the script attempts to detect the host platform.

.PARAMETER IncludeTests
Also build the modules' test projects. Skipped by default because test projects dominate
build time and disk usage during module iteration.

.PARAMETER NoLaunch
Build and assemble the layout, but do not start the runner.

.PARAMETER ExtraArgs
Additional arguments forwarded to MSBuild.

.EXAMPLE
.\tools\build\run-module.ps1 -Module peek
Builds the Peek module (without tests) and launches it.

.EXAMPLE
.\tools\build\run-module.ps1 -Module peek,poweraccent
Builds both modules into the same layout and launches the runner once.

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
    [string[]]$Module,
    [string]$Configuration = 'Debug',
    [string]$Platform = '',
    [switch]$IncludeTests,
    [switch]$NoLaunch,
    [Parameter(ValueFromRemainingArguments = $true)]
    [string[]]$ExtraArgs
)

$Module = @($Module | ForEach-Object { $_ -split ',' } | ForEach-Object { $_.Trim() } | Where-Object { $_ })
if ($Module.Count -eq 0) {
    Write-Error "-Module requires at least one module name under src\modules"
    exit 1
}

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

# Resolve every requested module folder up front, so a typo fails before anything builds
$modulesRoot = Join-Path $repoRoot 'src\modules'
$moduleDirs = @()
$missing = @()
foreach ($name in $Module) {
    $candidate = Join-Path $modulesRoot $name
    if (-not (Test-Path $candidate)) {
        $candidate = Get-ChildItem -Path $modulesRoot -Directory |
            Where-Object { $_.Name -ieq $name } |
            Select-Object -First 1 -ExpandProperty FullName
    }
    if ($candidate -and (Test-Path $candidate)) {
        $moduleDirs += (Resolve-Path $candidate).Path
    } else {
        $missing += $name
    }
}
if ($missing) {
    Write-Error ("Module folder not found under src\modules: {0}" -f ($missing -join ', '))
    exit 1
}
$moduleDirs = @($moduleDirs | Select-Object -Unique)

$extraArgsString = $null
if ($ExtraArgs -and $ExtraArgs.Count -gt 0) { $extraArgsString = ($ExtraArgs -join ' ') }

$index = 0
foreach ($moduleDir in $moduleDirs) {
    $index++
    if ($moduleDirs.Count -gt 1) {
        Write-Host ("[RUN-MODULE] === Module {0}/{1}: {2}" -f $index, $moduleDirs.Count, (Split-Path $moduleDir -Leaf))
    }
    Write-Host ("[RUN-MODULE] Module folder: {0}" -f $moduleDir)

    # Discover the module's projects recursively
    $projects = Get-ChildItem -Path $moduleDir -Recurse -File -Include *.csproj, *.vcxproj |
        Where-Object { $_.FullName -notmatch '\\(obj|bin|packages)\\' }
    if (-not $IncludeTests) {
        # Test projects are matched by name (Foo.UnitTests, Foo.UITests.Next, Foo.FuzzTests,
        # Foo.FuzzingTest, UnitTests.vcxproj) and by location (projects living under a *Tests
        # folder, such as test-only helper projects).
        $tests = $projects | Where-Object {
            $_.BaseName -match '(^|\.)(UnitTests|UITests|FuzzTests|FuzzingTests?)(\.|$)' -or
            $_.BaseName -match 'Tests?$' -or
            $_.DirectoryName -match '\\[^\\]*Tests?\\'
        }
        if ($tests) {
            $projects = @($projects | Where-Object { $tests -notcontains $_ })
            Write-Host ("[RUN-MODULE] Skipping {0} test project(s): {1}" -f $tests.Count, (($tests | ForEach-Object { $_.Name }) -join ', '))
        }
    }
    if (-not $projects -or $projects.Count -eq 0) {
        Write-Error ("No buildable projects found under {0}" -f $moduleDir)
        exit 1
    }
    Write-Host ("[RUN-MODULE] Building {0} project(s): {1}" -f $projects.Count, (($projects | ForEach-Object { $_.Name }) -join ', '))

    foreach ($project in $projects) {
        # Pass the SolutionDir a solution build would give this project, so IntDir/OutDir
        # land in the shared x64\<Config> tree instead of inside the project folder.
        $solutionDir = Get-ProjectSolutionDir $project.FullName
        $projectArgs = "/m"
        if ($solutionDir -and -not $solutionDir.Contains(' ')) {
            $projectArgs = "$projectArgs /p:SolutionDir=$solutionDir"
        }
        if ($extraArgsString) { $projectArgs = "$projectArgs $extraArgsString" }
        Write-Host ("[RUN-MODULE] Building {0}" -f $project.FullName)
        RestoreThenBuild $project.FullName $projectArgs $Platform $Configuration
    }
}

# Build the runner, the Settings UI and the Quick Access UI if they have not been
# built for this configuration yet. Without them the dev runner cannot open its
# own settings window or the quick access flyout.
$essentials = @(
    @{ Exe = "x64\$Configuration\WinUI3Apps\PowerToys.exe"; Project = "src\runner\Runner.vcxproj"; Name = "Runner" }
    @{ Exe = "x64\$Configuration\WinUI3Apps\PowerToys.Settings.exe"; Project = "src\settings-ui\Settings.UI\PowerToys.Settings.csproj"; Name = "Settings UI" }
    @{ Exe = "x64\$Configuration\WinUI3Apps\PowerToys.QuickAccess.exe"; Project = "src\settings-ui\QuickAccess.UI\PowerToys.QuickAccess.csproj"; Name = "Quick Access" }
)
foreach ($e in $essentials) {
    $exePath = Join-Path $repoRoot $e.Exe
    if (Test-Path $exePath) { continue }
    Write-Host ("[RUN-MODULE] {0} not built for $Configuration, building it once." -f $e.Name)
    RestoreThenBuild (Join-Path $repoRoot $e.Project) $extraArgsString $Platform $Configuration
}

# Put runner runtime files next to the module outputs.
# The module outputs already land in <repo>\x64\<Configuration>\WinUI3Apps, but the runner
# needs its own runtime files in <repo>\x64\<Configuration>. Never /MIR so
# freshly built module outputs (including those of other modules) are not deleted.
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
