#!/usr/bin/env pwsh
# Option 2: register the debug build as a proper Windows app package (Developer Mode required).
# After registration the dev CmdPal appears in the Start menu and is activatable like a normal app.
# Re-run this script whenever you rebuild the full CmdPal (build-cmdpal.cmd).
$ErrorActionPreference = "Stop"
$Root = $PSScriptRoot

$DebugDir    = Join-Path $Root "x64\Debug\WinUI3Apps\CmdPal"
$Manifest    = Join-Path $DebugDir "AppxManifest.xml"
$CmdPalExe   = Join-Path $DebugDir "Microsoft.CmdPal.UI.exe"

if (-not (Test-Path $Manifest)) {
    Write-Error "AppxManifest.xml not found.`nRun build-cmdpal.cmd first to build the full CmdPal."
    exit 1
}
if (-not (Test-Path $CmdPalExe)) {
    Write-Error "Microsoft.CmdPal.UI.exe not found.`nRun build-cmdpal.cmd first."
    exit 1
}

Write-Host "=== Building MonitorPowerExtension (x64 Debug) ==="
dotnet build "$Root\src\modules\cmdpal\ext\MonitorPowerExtension\MonitorPowerExtension.csproj" `
    -c Debug -p:Platform=x64 -p:SolutionDir="$Root\" `
    -p:AppendTargetFrameworkToOutputPath=false `
    -p:AppendRuntimeIdentifierToOutputPath=false --nologo -v:m
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

$extensionOutput = Join-Path $Root "x64\Debug\WinUI3Apps\CmdPalExtensions\MonitorPowerExtension"
$extensionManifest = Join-Path $extensionOutput "AppxManifest.xml"
if (-not (Test-Path (Join-Path $extensionOutput "MonitorPowerExtension.dll"))) {
    Write-Error "MonitorPowerExtension.dll still missing after build."
    exit 1
}
if (-not (Test-Path $extensionManifest)) {
    Write-Error "MonitorPowerExtension AppxManifest.xml still missing after build."
    exit 1
}

Write-Host "=== Stopping any running CmdPal ==="
Get-Process -Name "Microsoft.CmdPal.UI" -ErrorAction SilentlyContinue | Stop-Process -Force
Start-Sleep -Milliseconds 500

Write-Host "=== Registering dev packages ==="
Add-AppxPackage -Register $Manifest -ForceUpdateFromAnyVersion -ForceApplicationShutdown
Add-AppxPackage -Register $extensionManifest -ForceUpdateFromAnyVersion -ForceApplicationShutdown
Write-Host ""
Write-Host "Done! The dev CmdPal (Microsoft.CommandPalette.Dev) is now registered."
Write-Host "To use it as your daily CmdPal:"
Write-Host "  1. In PowerToys tray -> Command Palette -> disable the module (avoids hotkey conflict)"
Write-Host "  2. Launch the dev CmdPal: Start-Process '$CmdPalExe'"
Write-Host "     or search 'Command Palette' in Start menu"
Write-Host ""
Write-Host "Optional - add to Windows startup:"
Write-Host "  $(`$startup = [Environment]::GetFolderPath('Startup'); `"Copy-Item '$CmdPalExe' '`$startup\CmdPal-dev.lnk' (shortcut)`")"
