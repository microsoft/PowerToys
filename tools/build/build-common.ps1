<#
.SYNOPSIS
Shared build helper functions for PowerToys build scripts.

.DESCRIPTION
This file provides reusable helper functions used by the build scripts:
- Get-BuildPaths: returns ScriptDir, OriginalCwd, RepoRoot (repo root detection)
- RunMSBuild: wrapper around msbuild.exe (accepts optional Platform/Configuration)
- RestoreThenBuild: performs restore and optionally builds the solution/project
- BuildProjectsInDirectory: discovers and builds local .sln/.csproj/.vcxproj files
- Ensure-VsDevEnvironment: initializes the Visual Studio developer environment when possible.
  It prefers the DevShell PowerShell module (Microsoft.VisualStudio.DevShell.dll / Enter-VsDevShell),
  falls back to running VsDevCmd.bat and importing its environment into the current PowerShell session,
  and restores the caller's working directory after initialization.

USAGE
Dot-source this file from a script to load helpers:
. "$PSScriptRoot\build-common.ps1"

ERROR DETAILS
When a build fails, check the logs written next to the solution/project folder:
- build.<configuration>.<platform>.all.log — full MSBuild text log
- build.<configuration>.<platform>.errors.log — extracted errors only
- build.<configuration>.<platform>.warnings.log — extracted warnings only
- build.<configuration>.<platform>.trace.binlog — binary log (open with the MSBuild Structured Log Viewer)

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
            Write-Error (("Build failed: {0}  {1}`nSee logs:`n  All: {2}`n  Errors: {3}`n  Binlog: {4}" -f $Solution, $ExtraArgs, $allLog, $errorsLog, $binLog))
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
        $files = Get-ChildItem -Path (Join-Path $DirectoryPath '*') -Include *.sln,*.slnx,*.csproj,*.vcxproj -File -ErrorAction SilentlyContinue
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

function Get-DefaultPlatform {
    <#
    Returns a default target platform string based on the host machine (x64, arm64, x86).
    #>
    try {
        $envArch = $env:PROCESSOR_ARCHITECTURE
        if ($envArch) { $envArch = $envArch.ToLower() }
        if ($envArch -eq 'amd64' -or $envArch -eq 'x86_64') { return 'x64' }
        if ($envArch -match 'arm64') { return 'arm64' }
        if ($envArch -eq 'x86') { return 'x86' }

        if ($env:PROCESSOR_ARCHITEW6432) {
            $envArch2 = $env:PROCESSOR_ARCHITEW6432.ToLower()
            if ($envArch2 -eq 'amd64') { return 'x64' }
            if ($envArch2 -match 'arm64') { return 'arm64' }
        }

        try {
            $osArch = [System.Runtime.InteropServices.RuntimeInformation]::OSArchitecture
            switch ($osArch.ToString().ToLower()) {
                'x64' { return 'x64' }
                'arm64' { return 'arm64' }
                'x86' { return 'x86' }
            }
        } catch {
            # ignore - RuntimeInformation may not be available
        }
    } catch {
        # ignore any errors and fall back
    }

    return 'x64'
}

function Ensure-VsDevEnvironment {
    $OriginalLocationForVsInit = Get-Location
    try {

    if ($env:VSINSTALLDIR -or $env:VCINSTALLDIR -or $env:DevEnvDir -or $env:VCToolsInstallDir) {
        Write-Host "[VS] VS developer environment already present"
        return $true
    }

    # Locate vswhere if available
    $vswhereCandidates = @(
        "$env:ProgramFiles (x86)\Microsoft Visual Studio\Installer\vswhere.exe",
        "$env:ProgramFiles\Microsoft Visual Studio\Installer\vswhere.exe"
    )
    $vswhere = $vswhereCandidates | Where-Object { Test-Path $_ } | Select-Object -First 1
    if ($vswhere) { Write-Host "[VS] vswhere found: $vswhere" } else { Write-Host "[VS] vswhere not found" }

    $instPaths = @()
    if ($vswhere) {
        # First try with the VC tools requirement (preferred)
        try { $p = & $vswhere -latest -products * -requires Microsoft.VisualStudio.Component.VC.Tools.x86.x64 -property installationPath 2>$null; if ($p) { $instPaths += $p } } catch {}
        # Fallback: try without -requires to find any VS installations
        if (-not $instPaths) {
            try { $p2 = & $vswhere -latest -products * -property installationPath 2>$null; if ($p2) { $instPaths += $p2 } } catch {}
        }
    }

    # Add explicit common year-based candidates as a last resort
    if (-not $instPaths) {
        $explicit = @(
            "$env:ProgramFiles (x86)\Microsoft Visual Studio\2022\Community",
            "$env:ProgramFiles (x86)\Microsoft Visual Studio\2022\Professional",
            "$env:ProgramFiles (x86)\Microsoft Visual Studio\2022\Enterprise",
            "$env:ProgramFiles\Microsoft Visual Studio\2022\Community",
            "$env:ProgramFiles\Microsoft Visual Studio\2022\Professional",
            "$env:ProgramFiles\Microsoft Visual Studio\2022\Enterprise"
        )
        foreach ($c in $explicit) { if (Test-Path $c) { $instPaths += $c } }
    }

    if (-not $instPaths -or $instPaths.Count -eq 0) {
        Write-Warning "[VS] Could not locate Visual Studio installation (no candidates found)"
        return $false
    }

    # Try each candidate installation path until one works
    foreach ($inst in $instPaths) {
        if (-not $inst) { continue }
        Write-Host "[VS] Checking candidate: $inst"

        $devDll = Join-Path $inst 'Common7\Tools\Microsoft.VisualStudio.DevShell.dll'
        if (Test-Path $devDll) {
            try {
                Import-Module $devDll -DisableNameChecking -ErrorAction Stop

                # Call Enter-VsDevShell using only the install path to avoid parameter name differences
                try {
                    Enter-VsDevShell -VsInstallPath $inst -ErrorAction Stop
                    Write-Host "[VS] Entered Visual Studio DevShell at $inst"
                    return $true
                } catch {
                    Write-Warning ("[VS] DevShell import/Enter-VsDevShell failed: {0}" -f $_)
                }
            } catch {
                Write-Warning ("[VS] DevShell import failed: {0}" -f $_)
            }
        }

        $vsDevCmd = Join-Path $inst 'Common7\Tools\VsDevCmd.bat'
        if (Test-Path $vsDevCmd) {
            Write-Host "[VS] Running VsDevCmd.bat and importing environment from $vsDevCmd"
            try {
                $cmdOut = cmd.exe /c "`"$vsDevCmd`" && set"
                foreach ($line in $cmdOut) {
                    $parts = $line -split('=',2)
                    if ($parts.Length -eq 2) {
                        try { [Environment]::SetEnvironmentVariable($parts[0], $parts[1], 'Process') } catch {}
                    }
                }
                Write-Host "[VS] Imported environment from VsDevCmd.bat at $inst"
                return $true
            } catch {
                Write-Warning ("[VS] Failed to run/import VsDevCmd.bat at {0}: {1}" -f $inst, $_)
            }
        }
    }

    Write-Warning "[VS] Neither DevShell module nor VsDevCmd.bat found in any candidate paths"
    return $false

    } finally {
        try { Set-Location $OriginalLocationForVsInit } catch {}
    }
}
