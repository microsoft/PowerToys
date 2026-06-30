# Copyright (c) Microsoft Corporation
# The Microsoft Corporation licenses this file to you under the MIT license.
# See the LICENSE file in the project root for more information.

<#
.SYNOPSIS
    Publishes the Native AOT CLI shim and stages one exe per command name.

.DESCRIPTION
    Publishes tools/CliShim as a single Native AOT binary, then copies it to one exe per
    command name into -OutDir (the installer's "cli" staging folder), and removes the source
    launcher so the folder holds exactly what the installer harvests and ESRP signs.

    Shared by both entry points so the logic lives in one place:
      - the local installer build (tools/build/build-installer.ps1)
      - the CI build (.pipelines/v2/templates/job-build-project.yml)

    The command names come from a single source of truth: the keys of the Targets dictionary
    in tools/CliShim/Program.cs. This script also verifies that CliShims.wxs and
    ESRPSigning_core.json reference exactly that set, failing the build on any drift (which
    would otherwise ship a shim that always exits 9009, or silently omit one).

.NOTES
    Native AOT linking needs the Desktop C++ workload; the .NET ILCompiler targets locate it
    automatically via findvcvarsall.bat (no separate vcvars activation required).
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string] $Platform,       # x64 | ARM64 (any case)
    [Parameter(Mandatory = $true)][string] $Configuration,  # Debug | Release
    [Parameter(Mandatory = $true)][string] $OutDir          # staging "cli" folder
)

$ErrorActionPreference = 'Stop'

# tools/build/<this>.ps1 -> repo root is two levels up.
$repoRoot  = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$shimProj  = Join-Path $repoRoot 'tools\CliShim\CliShim.csproj'
$programCs = Join-Path $repoRoot 'tools\CliShim\Program.cs'
$shimsWxs  = Join-Path $repoRoot 'installer\PowerToysSetupVNext\CliShims.wxs'
$esrpJson  = Join-Path $repoRoot '.pipelines\ESRPSigning_core.json'

function Get-CapturedValues([string] $Path, [string] $Pattern) {
    if (-not (Test-Path $Path)) { throw "publish-cli-shims: file not found: $Path" }
    $found = Select-String -Path $Path -Pattern $Pattern -AllMatches
    if (-not $found) { return @() }
    @($found.Matches | ForEach-Object { $_.Groups[1].Value } | Sort-Object -Unique)
}

# Single source of truth: the command names are the keys of the Targets dictionary in Program.cs.
$commandNames = Get-CapturedValues $programCs '\["([^"]+)"\]\s*=\s*@"'
if (-not $commandNames) { throw "publish-cli-shims: could not parse any command names from $programCs" }

# Drift guard: the installer harvest and the signing list must reference exactly these names.
$consumers = @(
    @{ Name = 'CliShims.wxs';          Actual = (Get-CapturedValues $shimsWxs 'cli\\([^"\\]+)\.exe') }
    @{ Name = 'ESRPSigning_core.json'; Actual = (Get-CapturedValues $esrpJson 'cli\\\\([^"\\]+)\.exe') }
)
foreach ($consumer in $consumers) {
    if (Compare-Object -ReferenceObject $commandNames -DifferenceObject $consumer.Actual) {
        throw ("publish-cli-shims: command set mismatch. Program.cs has [$($commandNames -join ', ')] " +
               "but $($consumer.Name) has [$($consumer.Actual -join ', ')]. " +
               'Add/rename the shim in Program.cs, CliShims.wxs, and ESRPSigning_core.json together.')
    }
}

# Native AOT linking shells out to vswhere.exe to locate the MSVC toolchain (which then sets
# LIB/INCLUDE via vcvars). In a plain, non-developer shell -- e.g. the CI 'pwsh' step, which
# unlike the local installer build does not activate a VS Dev environment -- vswhere may not be
# on PATH, and the link step fails with "'vswhere.exe' is not recognized". Add the fixed VS
# Installer location so the publish works without requiring callers to pre-activate vcvars.
if (-not (Get-Command 'vswhere.exe' -ErrorAction SilentlyContinue)) {
    $vsInstaller = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\Installer'
    if (Test-Path (Join-Path $vsInstaller 'vswhere.exe')) {
        $env:PATH = "$vsInstaller;$env:PATH"
        Write-Host "[CLI-SHIM] Added VS Installer to PATH so AOT linking can find vswhere.exe."
    }
}

# RID from platform (project config is x64;ARM64).
$cliPlatform = if ($Platform -ieq 'arm64') { 'ARM64' } else { 'x64' }
$rid = "win-$($cliPlatform.ToLower())"

Write-Host "[CLI-SHIM] Publishing Native AOT shim ($rid) -> $OutDir"
dotnet publish $shimProj -c $Configuration -r $rid -p:Platform=$cliPlatform -o $OutDir --nologo
if ($LASTEXITCODE -ne 0) { throw "publish-cli-shims: dotnet publish failed with exit code $LASTEXITCODE" }

# Copy the single published binary to one exe per command name, then drop the source launcher
# (and any sidecar pdb) so the staging dir holds exactly what the installer harvests and signs.
$srcExe = Join-Path $OutDir 'PowerToys.CliShim.exe'
if (-not (Test-Path $srcExe)) { throw "publish-cli-shims: expected '$srcExe' was not produced by dotnet publish." }
foreach ($name in $commandNames) {
    Copy-Item $srcExe (Join-Path $OutDir "$name.exe") -Force
}
Remove-Item $srcExe -Force
Remove-Item (Join-Path $OutDir 'PowerToys.CliShim.pdb') -Force -ErrorAction SilentlyContinue

Write-Host "[CLI-SHIM] Staged: $($commandNames -join ', ')"
