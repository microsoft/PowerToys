# Copyright (c) Microsoft Corporation
# The Microsoft Corporation licenses this file to you under the MIT license.
# See the LICENSE file in the project root for more information.

<#
.SYNOPSIS
Stages a Debug MWB experiment without launching anything or changing host settings.
.PARAMETER Destination
New, private directory outside the source checkout. Existing directories are refused.
#>
[CmdletBinding()]
param([Parameter(Mandatory = $true)][string]$Destination)

$ErrorActionPreference = 'Stop'
. "$PSScriptRoot\Experiment.Common.ps1"
$sourceRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..\..\..\..'))
$destinationRoot = [IO.Path]::GetFullPath($Destination).TrimEnd('\')
if ($destinationRoot -ieq $sourceRoot -or
    $destinationRoot.StartsWith($sourceRoot.TrimEnd('\') + '\', [StringComparison]::OrdinalIgnoreCase)) {
    throw 'Destination must be outside the source checkout.'
}
if ($destinationRoot -notmatch '^[A-Za-z]:\\' -or (Test-Path -LiteralPath $destinationRoot)) {
    throw 'Use a new local-drive destination, not an existing directory or network share.'
}
# Avoid a junction redirecting a seemingly external destination back into the checkout.
$ancestor = Split-Path $destinationRoot
while ($ancestor) {
    if (Test-Path -LiteralPath $ancestor) {
        if ((Get-Item -LiteralPath $ancestor).Attributes -band [IO.FileAttributes]::ReparsePoint) {
            throw "Destination ancestors cannot be reparse points: $ancestor"
        }
    }
    $ancestor = Split-Path $ancestor
}
$productRoot = Join-Path $sourceRoot 'x64\Debug'
$productFiles = @(
    'PowerToys.exe',
    'PowerToys.MouseWithoutBordersModuleInterface.dll',
    'PowerToys.MouseWithoutBorders.exe',
    'PowerToys.MouseWithoutBorders.dll',
    'PowerToys.MouseWithoutBorders.deps.json',
    'PowerToys.MouseWithoutBorders.runtimeconfig.json',
    'PowerToys.MouseWithoutBordersHelper.exe',
    'PowerToys.MouseWithoutBordersHelper.dll',
    'PowerToys.MouseWithoutBordersHelper.deps.json',
    'PowerToys.MouseWithoutBordersHelper.runtimeconfig.json',
    'PowerToys.GPOWrapper.dll',
    'PowerToys.Interop.dll',
    'PowerToys.Settings.UI.Lib.dll',
    'coreclr.dll', 'hostfxr.dll', 'hostpolicy.dll',
    'WinUI3Apps\PowerToys.Settings.exe',
    'WinUI3Apps\PowerToys.Settings.dll',
    'WinUI3Apps\PowerToys.Settings.UI.Lib.dll',
    'WinUI3Apps\PowerToys.Settings.deps.json',
    'WinUI3Apps\PowerToys.Settings.runtimeconfig.json'
)
$files = @()
foreach ($relative in $productFiles) {
    $path = Join-Path $productRoot $relative
    $item = Get-Item -LiteralPath $path
    $files += [ordered]@{
        Kind = 'Product'; RelativePath = $relative; OriginalPath = $path
        Length = $item.Length; LastWriteTimeUtc = $item.LastWriteTimeUtc.ToString('o')
        Sha256 = (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash
    }
}
$libraryPath = Join-Path $productRoot 'PowerToys.Settings.UI.Lib.dll'
$settingsLibraryPath = Join-Path $productRoot 'WinUI3Apps\PowerToys.Settings.UI.Lib.dll'
if ((Get-FileHash -LiteralPath $libraryPath -Algorithm SHA256).Hash -ne
    (Get-FileHash -LiteralPath $settingsLibraryPath -Algorithm SHA256).Hash) {
    throw 'MWB and Settings have different settings-library builds. Rebuild Settings in Debug before preparing the experiment.'
}
$assembly = [IO.File]::ReadAllBytes((Join-Path $productRoot 'PowerToys.MouseWithoutBorders.dll'))
$hasFlag = [Text.Encoding]::Unicode.GetString($assembly).Contains('POWERTOYS_MWB_ALLOW_NONCONSOLE') -or
    [Text.Encoding]::Unicode.GetString($assembly, 1, $assembly.Length - 1).Contains('POWERTOYS_MWB_ALLOW_NONCONSOLE')
if (-not $hasFlag) {
    throw 'The x64\Debug MWB payload must be rebuilt with the experimental flag before preparation.'
}
$packages = @(Get-AppxPackage -Name winapp | Where-Object { $_.Architecture -eq 'X64' })
if ($packages.Count -ne 1) { throw 'One installed x64 winapp package is required. No tools will be installed automatically.' }
$package = $packages[0]
foreach ($name in @('winapp.exe', 'libSkiaSharp.dll')) {
    if (-not (Test-Path -LiteralPath (Join-Path $package.InstallLocation $name) -PathType Leaf)) {
        throw "Installed winapp package is missing $name."
    }
}
$moduleSource = Join-Path $sourceRoot 'src\settings-ui\Settings.UI.Library\EnabledModules.cs'
$moduleNames = @([regex]::Matches([IO.File]::ReadAllText($moduleSource),
    '(?:\[JsonPropertyName\("(?<json>[^"]+)"\)\]\s*)?public bool (?<property>\w+)') | ForEach-Object {
        if ($_.Groups['json'].Success) { $_.Groups['json'].Value } else { $_.Groups['property'].Value }
    })
if ($moduleNames.Count -lt 30 -or 'MouseWithoutBorders' -notin $moduleNames) {
    throw 'Could not derive the complete enabled-module configuration from source.'
}
$inputRoot = Join-Path $destinationRoot 'input'
$null = New-Item -ItemType Directory -Path "$inputRoot\winapp", "$destinationRoot\runs" -Force
foreach ($name in @('winapp.exe', 'libSkiaSharp.dll')) {
    Copy-Item -LiteralPath (Join-Path $package.InstallLocation $name) -Destination "$inputRoot\winapp\$name"
}
Get-ChildItem -LiteralPath $PSScriptRoot -File | Where-Object { $_.Extension -in '.ps1', '.md', '.json' } |
    Copy-Item -Destination $inputRoot

$productXml = [Security.SecurityElement]::Escape($productRoot)
$inputXml = [Security.SecurityElement]::Escape($inputRoot)
$template = @"
<Configuration>
  <Networking>Enable</Networking>
  <ClipboardRedirection>Disable</ClipboardRedirection>
  <AudioInput>Disable</AudioInput>
  <VideoInput>Disable</VideoInput>
  <PrinterRedirection>Disable</PrinterRedirection>
  <MappedFolders>
    <MappedFolder><HostFolder>$productXml</HostFolder><SandboxFolder>C:\PowerToysBuild</SandboxFolder><ReadOnly>true</ReadOnly></MappedFolder>
    <MappedFolder><HostFolder>$inputXml</HostFolder><SandboxFolder>C:\MwbProbe</SandboxFolder><ReadOnly>true</ReadOnly></MappedFolder>
    <MappedFolder><HostFolder>__RUN_INPUT__</HostFolder><SandboxFolder>C:\MwbRun</SandboxFolder><ReadOnly>true</ReadOnly></MappedFolder>
    <MappedFolder><HostFolder>__RUN_OUTPUT__</HostFolder><SandboxFolder>C:\MwbEvidence</SandboxFolder><ReadOnly>false</ReadOnly></MappedFolder>
  </MappedFolders>
  <LogonCommand><Command>powershell.exe -NoProfile -ExecutionPolicy Bypass -File C:\MwbProbe\Initialize-Sandbox.ps1</Command></LogonCommand>
</Configuration>
"@
[IO.File]::WriteAllText("$inputRoot\Sandbox.wsb.template", $template, (New-Object Text.UTF8Encoding($false)))
foreach ($item in Get-ChildItem -LiteralPath $inputRoot -File -Recurse) {
    $files += [ordered]@{
        Kind = 'Input'; RelativePath = $item.FullName.Substring($inputRoot.Length + 1)
        OriginalPath = $item.FullName; Length = $item.Length
        LastWriteTimeUtc = $item.LastWriteTimeUtc.ToString('o')
        Sha256 = (Get-FileHash -LiteralPath $item.FullName -Algorithm SHA256).Hash
    }
}
$manifest = [ordered]@{
    FormatVersion = 1; PreparedUtc = [DateTime]::UtcNow.ToString('o')
    SourceRoot = $sourceRoot; ProductRoot = $productRoot; Destination = $destinationRoot
    Configuration = 'Debug'; Platform = 'x64'
    WinAppPackage = $package.PackageFullName
    EnabledModulesSourceSha256 = (Get-FileHash -LiteralPath $moduleSource -Algorithm SHA256).Hash
    ModuleNames = $moduleNames; Files = $files
}
Write-ExperimentJson "$destinationRoot\prepared.json" $manifest
Assert-ExperimentPayload $manifest $inputRoot $productRoot
Write-Host "Prepared only: $destinationRoot"
Write-Host "Later, from an active unlocked non-elevated host desktop:"
Write-Host "& '$inputRoot\Start-Experiment.ps1' -Destination '$destinationRoot' -AllowNonConsole"
