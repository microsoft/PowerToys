<#
.SYNOPSIS
Build and package PowerToys (CmdPal and installer) for a specific platform and configuration LOCALLY.

.DESCRIPTION
This script automates the end-to-end build and packaging process for PowerToys, including:
- Restoring and building all necessary solutions (CmdPal, BugReportTool, StylesReportTool, etc.)
- Cleaning up old output
- Signing generated .msix packages
- Building the WiX-based MSI and bootstrapper installers

It is designed to work in local development.

.PARAMETER Platform
Specifies the target platform for the build (e.g., 'arm64', 'x64'). Default is 'arm64'.

.PARAMETER Configuration
Specifies the build configuration (e.g., 'Debug', 'Release'). Default is 'Release'.

.EXAMPLE
.\build-installer.ps1
Runs the installer build pipeline for ARM64 Release (default).

.EXAMPLE
.\build-installer.ps1 -Platform x64 -Configuration Release
Runs the pipeline for x64 Debug.

.NOTES
- Requires MSBuild, WiX Toolset, and Git to be installed and accessible from your environment.
- Make sure to run this script from a Developer PowerShell (e.g., VS2022 Developer PowerShell).
- Generated MSIX files will be signed using cert-sign-package.ps1.
- This script will clean previous outputs under the build directories and installer directory (except *.exe files).
- First time run need admin permission to trust the certificate.
- The built installer will be placed under: installer/PowerToysSetup/[Platform]/[Configuration]/UserSetup 
  relative to the solution root directory.
- The installer can't be run right after the build, I need to copy it to another file before it can be run.
#>

param (
    [string]$Platform = 'arm64',
    [string]$Configuration = 'Release'
)

# Import shared build utilities
$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Definition
Import-Module (Join-Path $scriptDir "BuildUtils.psm1") -Force

$repoRoot = Resolve-Path "$PSScriptRoot\..\.."
Set-Location $repoRoot

Write-BuildHeader "PowerToys Installer Build Script"

Write-Host ("Make sure wix is installed and available")
& "$PSScriptRoot\ensure-wix.ps1"

Write-Host ("[PIPELINE] Start | Platform={0} Configuration={1}" -f $Platform, $Configuration)
Write-Host ''

$cmdpalOutputPath = Join-Path $repoRoot "$Platform\$Configuration\WinUI3Apps\CmdPal"

if (Test-Path $cmdpalOutputPath) {
    Write-Host "[CLEAN] Removing previous output: $cmdpalOutputPath"
    Remove-Item $cmdpalOutputPath -Recurse -Force -ErrorAction Ignore
}

# Build PowerToys main solution
Invoke-RestoreThenBuild -Solution '.\PowerToys.sln' -Platform $Platform -Configuration $Configuration

$msixSearchRoot = Join-Path $repoRoot "$Platform\$Configuration"
$msixFiles = Get-ChildItem -Path $msixSearchRoot -Recurse -Filter *.msix |
Select-Object -ExpandProperty FullName

if ($msixFiles.Count) {
    Write-Host ("[SIGN] .msix file(s): {0}" -f ($msixFiles -join '; '))
    & "$PSScriptRoot\cert-sign-package.ps1" -TargetPaths $msixFiles
}
else {
    Write-Warning "[SIGN] No .msix files found in $msixSearchRoot"
}

# Build tool solutions
Invoke-RestoreThenBuild -Solution '.\tools\BugReportTool\BugReportTool.sln' -Platform $Platform -Configuration $Configuration
Invoke-RestoreThenBuild -Solution '.\tools\StylesReportTool\StylesReportTool.sln' -Platform $Platform -Configuration $Configuration

Write-Host '[CLEAN] installer (keep *.exe)'
git clean -xfd -e '*.exe' -- .\installer\ | Out-Null

# Build installer components
Invoke-MSBuild -Solution '.\installer\PowerToysSetup.sln' -Platform $Platform -Configuration $Configuration -Target 'restore' -ExtraArgs '/p:RestorePackagesConfig=true'

Invoke-MSBuild -Solution '.\installer\PowerToysSetup.sln' -Platform $Platform -Configuration $Configuration -Target 'PowerToysInstaller' -ExtraArgs '/p:PerUser=true' -UseMultiProcessor

Invoke-MSBuild -Solution '.\installer\PowerToysSetup.sln' -Platform $Platform -Configuration $Configuration -Target 'PowerToysBootstrapper' -ExtraArgs '/p:PerUser=true' -UseMultiProcessor

Write-Host '[PIPELINE] Completed'