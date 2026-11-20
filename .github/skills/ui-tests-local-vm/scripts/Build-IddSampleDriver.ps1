# Copyright (c) Microsoft Corporation
# The Microsoft Corporation licenses this file to you under the MIT license.
# See the LICENSE file in the project root for more information.

<#
.SYNOPSIS
Builds the Windows-driver-samples IndirectDisplay driver for the multi-monitor guest, unsigned.

.DESCRIPTION
Fetches only the IndirectDisplay sample from microsoft/Windows-driver-samples at a pinned commit
(sparse checkout), restores the Windows Driver Kit as NuGet packages, and builds IddSampleDriver with
signing off. The guest signs the catalog with a certificate that exists only inside it, so the build
creates no certificate on the host.

The repository's packages.config sits at its root, where the NuGet MSBuild restore never looks: that
restore reads a packages.config next to each project. This script writes one next to
IddSampleDriver.vcxproj, holding the root file's exact pins for the target platform only, then runs
the Visual Studio MSBuild restore with nuget.org as the only source. No nuget.exe is needed. The
packages land in the folder the repository's Directory.Build.props imports them from.

Requires Visual Studio with the Desktop development with C++ workload plus two individual
components, as the WDK NuGet installation page lists them: Windows Driver Kit, which supplies the
WindowsUserModeDriver10.0 platform toolset the sample uses, and C++ Spectre-mitigated libraries for
the target platform, because the sample builds with Spectre mitigation. The script checks both
before it fetches anything.

.PARAMETER VmRoot
Scaffold root holding vm.config.psd1. The sample is fetched under multimon\Windows-driver-samples.

.PARAMETER ConfigPath
VM configuration. Defaults to vm.config.psd1 under VmRoot. Its ProcessorArchitecture picks the
platform. The driver is copied to Multimon.DriverPath, the folder Initialize-LocalVmMultimon.ps1
reads it from, which defaults to multimon\IddSampleDriver under VmRoot.

.PARAMETER Commit
Windows-driver-samples commit to build. Raise it deliberately, after reviewing the sample's changes.

.PARAMETER PackageSource
The only NuGet source the restore uses.

.EXAMPLE
pwsh .github\skills\ui-tests-local-vm\scripts\Build-IddSampleDriver.ps1 -VmRoot X:\PowerToysUiTestVm
#>

[CmdletBinding(SupportsShouldProcess)]
param(
    [Parameter(Mandatory)]
    [string]$VmRoot,
    [string]$ConfigPath,
    [ValidatePattern('^[0-9a-f]{40}$')]
    [string]$Commit = '2dc3fd3a0cc84a2933f2194e7ec0871584979071',
    [ValidateNotNullOrEmpty()]
    [string]$PackageSource = 'https://api.nuget.org/v3/index.json'
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$PSNativeCommandUseErrorActionPreference = $true

$vmRootPath = [System.IO.Path]::GetFullPath($VmRoot)
if ([string]::IsNullOrWhiteSpace($ConfigPath)) {
    $ConfigPath = Join-Path $vmRootPath 'vm.config.psd1'
}
$configuration = Import-PowerShellDataFile -LiteralPath $ConfigPath

Import-Module -Name (Join-Path $PSScriptRoot 'LocalVmMultimon.psm1') -Force
$platform = Get-MultimonPlatform -Configuration $configuration
$driverPackage = Get-MultimonDriverPackage -Configuration $configuration -VmRoot $vmRootPath

$sourceRoot = Join-Path $vmRootPath 'multimon\Windows-driver-samples'
$driverOutput = $driverPackage.Path
$projectRoot = Join-Path $sourceRoot "video\IndirectDisplay\$($driverPackage.Name)"
$project = Join-Path $projectRoot "$($driverPackage.Name).vcxproj"

# Check the toolchain before fetching anything, so a missing component costs no download.
$vswhere = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\Installer\vswhere.exe'
if (-not (Test-Path -LiteralPath $vswhere -PathType Leaf)) {
    throw "Visual Studio was not found ($vswhere is missing)."
}
$msbuild = & $vswhere -latest -products * -requires Microsoft.Component.MSBuild -find 'MSBuild\**\Bin\amd64\MSBuild.exe' |
    Select-Object -First 1
if ([string]::IsNullOrWhiteSpace($msbuild)) {
    throw 'No Visual Studio installation with MSBuild was found.'
}
if ($null -eq (Get-Command -Name git -ErrorAction SilentlyContinue)) {
    throw 'git was not found on PATH.'
}
$installPath = & $vswhere -latest -products * -requires Microsoft.Component.MSBuild -property installationPath |
    Select-Object -First 1
$toolsetPattern = Join-Path $installPath "MSBuild\Microsoft\VC\*\Platforms\$platform\PlatformToolsets\WindowsUserModeDriver10.0"
$spectrePattern = Join-Path $installPath "VC\Tools\MSVC\*\lib\spectre\$($platform.ToLowerInvariant())"
$missing = @(
    if (-not (Test-Path -Path $toolsetPattern)) { 'Windows Driver Kit (WindowsUserModeDriver10.0 toolset)' }
    if (-not (Test-Path -Path $spectrePattern)) { "C++ Spectre-mitigated libraries for $platform (Latest MSVC)" }
)
if ($missing.Count -gt 0) {
    throw "Visual Studio at $installPath lacks: $($missing -join '; '). Add them in the Visual Studio Installer under Individual components."
}

if ($PSCmdlet.ShouldProcess($sourceRoot, "Sparse-checkout Windows-driver-samples at $Commit")) {
    if (-not (Test-Path -LiteralPath (Join-Path $sourceRoot '.git'))) {
        git clone --filter=blob:none --no-checkout --quiet https://github.com/microsoft/Windows-driver-samples.git $sourceRoot
    }
    # Set on every run, so a checkout whose first run stopped early still gets the pattern.
    git -C $sourceRoot sparse-checkout set --no-cone '/Directory.Build.props' '/packages.config' '/video/IndirectDisplay/'
    git -C $sourceRoot fetch --quiet origin $Commit
    git -C $sourceRoot checkout --quiet --detach $Commit
    $head = [string](git -C $sourceRoot rev-parse HEAD)
    if ($head.Trim() -ne $Commit) {
        throw "Windows-driver-samples is at $head, not $Commit."
    }
}

# The root packages.config carries the pins Directory.Build.props imports by exact version.
$rootPackagesPath = Join-Path $sourceRoot 'packages.config'
if (-not (Test-Path -LiteralPath $rootPackagesPath -PathType Leaf)) {
    if ($WhatIfPreference) {
        Write-Host "What if: restore the $platform driver-kit pins from $rootPackagesPath and build $project with signing off."
        return
    }
    throw "The sparse checkout has no $rootPackagesPath."
}
$rootPackages = [xml](Get-Content -LiteralPath $rootPackagesPath -Raw)
$platformPackages = @($rootPackages.packages.package | Where-Object {
        $_.id -in 'Microsoft.Windows.SDK.CPP', "Microsoft.Windows.SDK.CPP.$platform", "Microsoft.Windows.WDK.$platform"
    })
if ($platformPackages.Count -ne 3) {
    throw "Expected 3 $platform driver-kit pins in the root packages.config; found $($platformPackages.Count)."
}
$projectPackages = Join-Path $projectRoot 'packages.config'
$content = [System.Text.StringBuilder]::new()
[void]$content.AppendLine('<?xml version="1.0" encoding="utf-8"?>').AppendLine('<packages>')
foreach ($package in $platformPackages) {
    [void]$content.AppendLine("  <package id=`"$($package.id)`" version=`"$($package.version)`" targetFramework=`"native`" />")
}
[void]$content.AppendLine('</packages>')

if ($PSCmdlet.ShouldProcess($projectPackages, 'Write the project packages.config and restore it from nuget.org only')) {
    Set-Content -LiteralPath $projectPackages -Value $content.ToString() -Encoding utf8
    # RestoreSources replaces every configured source, so nothing outside PackageSource is consulted.
    & $msbuild $project -nologo -verbosity:minimal -t:restore `
        -p:RestorePackagesConfig=true `
        "-p:RestoreRepositoryPath=$(Join-Path $sourceRoot 'packages')" `
        "-p:RestoreSources=$PackageSource" `
        "-p:Platform=$platform" -p:Configuration=Release
    $wdkVersion = ($platformPackages | Where-Object { $_.id -like 'Microsoft.Windows.WDK.*' }).version
    $wdkPropsPath = 'packages\Microsoft.Windows.WDK.{0}.{1}\build\native\Microsoft.Windows.WDK.{0}.props' -f $platform, $wdkVersion
    $wdkProps = Join-Path $sourceRoot $wdkPropsPath
    if (-not (Test-Path -LiteralPath $wdkProps -PathType Leaf)) {
        throw "The restore finished without $wdkProps, which Directory.Build.props imports."
    }
}

if ($PSCmdlet.ShouldProcess($project, "Build Release|$platform with signing off")) {
    & $msbuild $project -nologo -verbosity:minimal -m `
        "-p:Platform=$platform" -p:Configuration=Release -p:SignMode=Off

    # The package step writes the driver package to <platform>\Release\<project> under the project.
    $packageRoot = Join-Path $projectRoot "$platform\Release\$($driverPackage.Name)"
    $catalogName = "$($driverPackage.Name).cat"
    if (-not (Test-Path -LiteralPath (Join-Path $packageRoot $catalogName) -PathType Leaf)) {
        throw "The build produced no $catalogName in $packageRoot. The guest signs that catalog, so the package step must create it unsigned."
    }
    New-Item -ItemType Directory -Path $driverOutput -Force | Out-Null
    foreach ($name in $driverPackage.Files) {
        $built = Join-Path $packageRoot $name
        if (-not (Test-Path -LiteralPath $built -PathType Leaf)) {
            throw "The driver package folder $packageRoot has no $name."
        }
        Copy-Item -LiteralPath $built -Destination (Join-Path $driverOutput $name) -Force
    }
}

[pscustomobject]@{
    Commit     = $Commit
    Platform   = $platform
    Packages   = @($platformPackages | ForEach-Object { "$($_.id) $($_.version)" })
    DriverPath = $driverOutput
}
