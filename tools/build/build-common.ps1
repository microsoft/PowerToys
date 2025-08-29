<#
.SYNOPSIS
Shared build helper functions for PowerToys build scripts.

.DESCRIPTION
This file provides reusable helper functions used by the build scripts:
- Get-BuildPaths: returns ScriptDir, OriginalCwd, RepoRoot (repo root detection)
- RunMSBuild: wrapper around msbuild.exe (accepts optional Platform/Configuration)
- RestoreThenBuild: performs restore and optionally builds the solution/project
- BuildProjectsInDirectory: discovers and builds local .sln/.csproj/.vcxproj files

USAGE
Dot-source this file from a script to load helpers:
. "$PSScriptRoot\build-common.ps1"

.NOTES
Do not execute this file directly; dot-source it from `build.ps1` or `build-installer.ps1` so helpers are available in your script scope.
#>

function RunMSBuild {
    param (
        [string]$Solution,
        [string]$ExtraArgs,
        [string]$Platform,
        [string]$Configuration
    )

    # Prefer the solution's folder for logs; fall back to current directory
    $logRoot = Split-Path -Path $Solution
    if (-not $logRoot) { $logRoot = '.' }

    $cfg = $null
    if ($Configuration) { $cfg = $Configuration.ToLower() } else { $cfg = 'unknown' }
    $plat = $null
    if ($Platform) { $plat = $Platform.ToLower() } else { $plat = 'unknown' }

    $allLog = Join-Path $logRoot ("build.{0}.{1}.all.log" -f $cfg, $plat)
    $warningLog = Join-Path $logRoot ("build.{0}.{1}.warnings.log" -f $cfg, $plat)
    $errorsLog = Join-Path $logRoot ("build.{0}.{1}.errors.log" -f $cfg, $plat)
    $binLog = Join-Path $logRoot ("build.{0}.{1}.trace.binlog" -f $cfg, $plat)

    $base = @(
        $Solution
        "/p:Platform=$Platform"
        "/p:Configuration=$Configuration"
        "/verbosity:normal"
        '/clp:Summary;PerformanceSummary;ErrorsOnly;WarningsOnly'
        "/fileLoggerParameters:LogFile=$allLog;Verbosity=detailed"
        "/fileLoggerParameters1:LogFile=$warningLog;WarningsOnly"
        "/fileLoggerParameters2:LogFile=$errorsLog;ErrorsOnly"
        "/bl:$binLog"
        '/nologo'
    )

    $cmd = $base + ($ExtraArgs -split ' ')
    Write-Host (("[MSBUILD] {0}" -f ($cmd -join ' ')))

    Push-Location $script:RepoRoot
    try {
        & msbuild.exe @cmd
        if ($LASTEXITCODE -ne 0) {
            Write-Error (("Build failed: {0}  {1}" -f $Solution, $ExtraArgs))
            exit $LASTEXITCODE
        }
    } finally {
        Pop-Location
    }
}

function RestoreThenBuild {
    param (
        [string]$Solution,
        [string]$ExtraArgs,
        [string]$Platform,
        [string]$Configuration,
        [bool]$RestoreOnly=$false
    )

    $restoreArgs = '/t:restore /p:RestorePackagesConfig=true'
    if ($ExtraArgs) { $restoreArgs = "$restoreArgs $ExtraArgs" }
    RunMSBuild $Solution $restoreArgs $Platform $Configuration

    if (-not $RestoreOnly) {
        $buildArgs = '/m'
        if ($ExtraArgs) { $buildArgs = "$buildArgs $ExtraArgs" }
        RunMSBuild $Solution $buildArgs $Platform $Configuration
    }
}

function BuildProjectsInDirectory {
    param(
        [string]$DirectoryPath,
        [string]$ExtraArgs,
        [string]$Platform,
        [string]$Configuration,
        [switch]$RestoreOnly
    )

    if (-not (Test-Path $DirectoryPath)) {
        return $false
    }

    $files = @()
    try {
        $files = Get-ChildItem -Path (Join-Path $DirectoryPath '*') -Include *.sln,*.csproj,*.vcxproj -File -ErrorAction SilentlyContinue
    } catch {
        $files = @()
    }

    if (-not $files -or $files.Count -eq 0) {
        return $false
    }

    $names = ($files | ForEach-Object { $_.Name }) -join ', '
    Write-Host ("[LOCAL BUILD] Found {0} project(s) in {1}: {2}" -f $files.Count, $DirectoryPath, $names)

    $preferredOrder = @('.sln', '.csproj', '.vcxproj')
    $files = $files | Sort-Object @{Expression = { [array]::IndexOf($preferredOrder, $_.Extension.ToLower()) }}

    foreach ($f in $files) {
        Write-Host ("[LOCAL BUILD] Building {0}" -f $f.FullName)
        if ($f.Extension -eq '.sln') {
            RestoreThenBuild $f.FullName $ExtraArgs $Platform $Configuration $RestoreOnly
        } else {
            $buildArgs = '/m'
            if ($ExtraArgs) { $buildArgs = "$buildArgs $ExtraArgs" }
            RunMSBuild $f.FullName $buildArgs $Platform $Configuration
        }
    }

    return $true
}
