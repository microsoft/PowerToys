# Copyright (c) Microsoft Corporation
# The Microsoft Corporation licenses this file to you under the MIT license.
# See the LICENSE file in the project root for more information.

#Requires -Version 5.1
[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string] $BinaryDirectory,
    [Parameter(Mandatory)]
    [string] $StagingDirectory,
    [Parameter(Mandatory)]
    [ValidateSet('x64', 'ARM64')]
    [string] $Platform
)

$ErrorActionPreference = 'Stop'
$BinaryDirectory = [IO.Path]::GetFullPath($BinaryDirectory)
$StagingDirectory = [IO.Path]::GetFullPath($StagingDirectory)
$packagePath = Join-Path $BinaryDirectory 'CropAndLock.TestApp.msix'

$makeAppx = Get-Command makeappx.exe -ErrorAction SilentlyContinue | Select-Object -First 1 -ExpandProperty Source
if (-not $makeAppx) {
    $hostArchitecture = if ($env:PROCESSOR_ARCHITECTURE -eq 'ARM64') { 'arm64' } else { 'x64' }
    $sdkTools = @(
        "${env:ProgramFiles(x86)}\Windows Kits\10\bin\*\$hostArchitecture\makeappx.exe",
        "$env:ProgramFiles\Windows Kits\10\bin\*\$hostArchitecture\makeappx.exe"
    )
    $makeAppx = Get-ChildItem -Path $sdkTools -ErrorAction SilentlyContinue |
        Where-Object { $version = $null; [version]::TryParse($_.Directory.Parent.Name, [ref]$version) } |
        Sort-Object { [version]$_.Directory.Parent.Name } -Descending |
        Select-Object -First 1 -ExpandProperty FullName
}
if (-not $makeAppx) {
    throw 'Windows SDK makeappx.exe is required to build the packaged Crop And Lock fixture.'
}

foreach ($file in @(
    'CropAndLock.TestApp.exe',
    'CropAndLock.TestApp.dll',
    'CropAndLock.TestApp.deps.json',
    'CropAndLock.TestApp.runtimeconfig.json',
    'hostfxr.dll',
    'hostpolicy.dll',
    'coreclr.dll',
    'System.Private.CoreLib.dll',
    'System.Windows.Forms.dll'
)) {
    if (-not (Test-Path (Join-Path $BinaryDirectory $file))) {
        throw "Missing self-contained fixture build output: $file"
    }
}

$runtimeConfig = Get-Content (Join-Path $BinaryDirectory 'CropAndLock.TestApp.runtimeconfig.json') -Raw | ConvertFrom-Json
if ($runtimeConfig.runtimeOptions.framework -or $runtimeConfig.runtimeOptions.frameworks) {
    throw 'The packaged fixture must not depend on an ambient .NET installation.'
}
if ($StagingDirectory.StartsWith($BinaryDirectory.TrimEnd('\') + '\', [StringComparison]::OrdinalIgnoreCase)) {
    throw 'Package staging must be outside the binary directory.'
}

if (Test-Path $StagingDirectory) {
    Remove-Item $StagingDirectory -Recurse -Force
}
New-Item $StagingDirectory -ItemType Directory -Force | Out-Null
try {
    # Preserve the complete self-contained runtime, including native files and culture subfolders.
    # This output directory belongs only to the fixture app; exclude symbols and earlier packages.
    foreach ($file in Get-ChildItem $BinaryDirectory -File -Recurse | Where-Object { $_.Extension -notin '.pdb', '.msix' }) {
        $relativePath = $file.FullName.Substring($BinaryDirectory.TrimEnd('\').Length).TrimStart('\')
        $destination = Join-Path $StagingDirectory $relativePath
        New-Item (Split-Path $destination -Parent) -ItemType Directory -Force | Out-Null
        Copy-Item $file.FullName $destination
    }
    [xml]$manifest = Get-Content (Join-Path $PSScriptRoot 'AppxManifest.xml') -Raw
    $manifest.Package.Identity.SetAttribute('ProcessorArchitecture', $Platform.ToLowerInvariant())
    $manifest.Save((Join-Path $StagingDirectory 'AppxManifest.xml'))

    & $makeAppx pack /d $StagingDirectory /p $packagePath /o
    if ($LASTEXITCODE -ne 0 -or -not (Test-Path $packagePath)) {
        throw "makeappx failed to produce the Crop And Lock fixture (exit $LASTEXITCODE)."
    }
}
finally {
    Remove-Item $StagingDirectory -Recurse -Force
}

Write-Host "Created unsigned fixture package: $packagePath"
