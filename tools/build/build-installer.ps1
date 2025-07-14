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
    [string]$Platform = 'x64',
    [string]$Configuration = 'Release'
)

$repoRoot = Resolve-Path "$PSScriptRoot\..\.."
Set-Location $repoRoot

function RunMSBuild {
    param (
        [string]$Solution, 
        [string]$ExtraArgs  
    )

    $base = @(
        $Solution
        "/p:Platform=`"$Platform`""
        "/p:Configuration=$Configuration"
        '/verbosity:normal'
        '/clp:Summary;PerformanceSummary;ErrorsOnly;WarningsOnly'
        '/nologo'
    )

    $cmd = $base + ($ExtraArgs -split ' ')
    Write-Host ("[MSBUILD] {0} {1}" -f $Solution, ($cmd -join ' '))
    & msbuild.exe @cmd

    if ($LASTEXITCODE -ne 0) {
        Write-Error ("Build failed: {0}  {1}" -f $Solution, $ExtraArgs)
        exit $LASTEXITCODE
    }

}

function RestoreThenBuild {
    param ([string]$Solution)

    # 1) restore
    RunMSBuild $Solution '/t:restore /p:RestorePackagesConfig=true'
    # 2) build  -------------------------------------------------
    RunMSBuild $Solution '/m'
}

Write-Host ("Make sure wix is installed and available")
& "$PSScriptRoot\ensure-wix.ps1"

Write-Host ("[PIPELINE] Start | Platform={0} Configuration={1}" -f $Platform, $Configuration)
Write-Host ''

$cmdpalOutputPath = Join-Path $repoRoot "$Platform\$Configuration\WinUI3Apps\CmdPal"

if (Test-Path $cmdpalOutputPath) {
    Write-Host "[CLEAN] Removing previous output: $cmdpalOutputPath"
    Remove-Item $cmdpalOutputPath -Recurse -Force -ErrorAction Ignore
}

RestoreThenBuild '.\PowerToys.sln'

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

RestoreThenBuild '.\tools\BugReportTool\BugReportTool.sln'
RestoreThenBuild '.\tools\StylesReportTool\StylesReportTool.sln'

Write-Host '[CLEAN] installer (keep *.exe)'
git clean -xfd -e '*.exe' -- .\installer\ | Out-Null

RunMSBuild  '.\installer\PowerToysSetup.sln' '/t:restore /p:RestorePackagesConfig=true'

RunMSBuild '.\installer\PowerToysSetup.sln' '/m /t:PowerToysInstaller /p:PerUser=true'

RunMSBuild '.\installer\PowerToysSetup.sln' '/m /t:PowerToysBootstrapper /p:PerUser=true'

Write-Host '[PIPELINE] Completed'