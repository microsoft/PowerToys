<#
.SYNOPSIS
    Builds and registers the PowerScripts Windows 11 modern context-menu handler as an
    unsigned sparse (loose-file) MSIX package. Requires Developer Mode.

.DESCRIPTION
    1. Builds the native handler DLL (build.cmd).
    2. Publishes PowerScripts.Host.exe (framework-dependent) next to the DLL.
    3. Copies the manifest + logo assets into a deploy folder.
    4. Registers the package in place via Add-AppxPackage -Register.

    Run register.ps1 -Unregister to remove it.
#>
[CmdletBinding()]
param(
    [switch]$Unregister,
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Debug'
)

$ErrorActionPreference = 'Stop'
$PackageName = 'Microsoft.PowerToys.PowerScriptsContextMenu'
$here = Split-Path -Parent $MyInvocation.MyCommand.Definition
$deployDir = Join-Path $env:LOCALAPPDATA 'Microsoft\PowerToys\PowerScriptsContextMenu'

if ($Unregister)
{
    $pkg = Get-AppxPackage -Name $PackageName -ErrorAction SilentlyContinue
    if ($pkg)
    {
        Remove-AppxPackage -Package $pkg.PackageFullName
        Write-Host "Unregistered $($pkg.PackageFullName)"
    }
    else
    {
        Write-Host "Package $PackageName is not registered."
    }
    return
}

Write-Host '== Building handler DLL =='
& cmd /c "`"$here\build.cmd`""
if ($LASTEXITCODE -ne 0) { throw 'DLL build failed.' }

Write-Host '== Publishing PowerScripts.Host =='
$hostProj = Join-Path $here '..\PowerScripts.Host\PowerScripts.Host.csproj'
$hostPublish = Join-Path $here 'hostpublish'
& dotnet publish $hostProj -c $Configuration -o $hostPublish --nologo | Out-Null
if ($LASTEXITCODE -ne 0) { throw 'Host publish failed.' }

Write-Host '== Staging deploy folder =='
# Re-register cleanly: remove any prior registration before overwriting files.
$existing = Get-AppxPackage -Name $PackageName -ErrorAction SilentlyContinue
if ($existing) { Remove-AppxPackage -Package $existing.PackageFullName }

if (Test-Path $deployDir) { Remove-Item $deployDir -Recurse -Force }
New-Item -ItemType Directory -Force -Path $deployDir | Out-Null
New-Item -ItemType Directory -Force -Path (Join-Path $deployDir 'Assets') | Out-Null

Copy-Item (Join-Path $here 'PowerToys.PowerScriptsContextMenu.dll') $deployDir -Force
Copy-Item (Join-Path $here 'AppxManifest.xml') $deployDir -Force
Copy-Item (Join-Path $hostPublish '*') $deployDir -Recurse -Force

# Reuse the ImageResizer context-menu logo assets for the required tile slots.
$assetSrc = Join-Path $here '..\..\..\modules\imageresizer\ImageResizerContextMenu\Assets\ImageResizer'
foreach ($asset in 'storelogo.png', 'Square150x150Logo.png', 'Square44x44Logo.png', 'Wide310x150Logo.png', 'LargeTile.png', 'SmallTile.png', 'SplashScreen.png')
{
    Copy-Item (Join-Path $assetSrc $asset) (Join-Path $deployDir 'Assets') -Force
}

Write-Host '== Registering package =='
Add-AppxPackage -Register (Join-Path $deployDir 'AppxManifest.xml')

Write-Host "Registered. Deploy folder: $deployDir"
Write-Host 'Right-click a matching file (e.g. a .md) to see the PowerScript submenu (restart Explorer if needed).'
