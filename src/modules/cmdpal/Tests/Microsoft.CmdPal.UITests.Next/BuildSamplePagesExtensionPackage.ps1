# Copyright (c) Microsoft Corporation
# The Microsoft Corporation licenses this file to you under the MIT license.
# See the LICENSE file in the project root for more information.

#Requires -Version 5.1
[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string] $LayoutDirectory,
    [Parameter(Mandatory)]
    [string] $PackagePath
)

$ErrorActionPreference = 'Stop'
$LayoutDirectory = [IO.Path]::GetFullPath($LayoutDirectory).TrimEnd('\')
$PackagePath = [IO.Path]::GetFullPath($PackagePath)
foreach ($file in 'AppxManifest.xml', 'SamplePagesExtension.exe') {
    if (-not (Test-Path -LiteralPath (Join-Path $LayoutDirectory $file) -PathType Leaf)) {
        throw "SamplePagesExtension build output is missing: $file"
    }
}
if ($PackagePath.StartsWith($LayoutDirectory + '\', [StringComparison]::OrdinalIgnoreCase)) {
    throw 'The test package must be outside the extension layout.'
}

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
    throw 'Windows SDK makeappx.exe is required to package SamplePagesExtension for UI tests.'
}

New-Item (Split-Path $PackagePath -Parent) -ItemType Directory -Force | Out-Null
$mappingPath = $PackagePath + '.mapping.txt'
try {
    $mapping = @('[Files]') + @(Get-ChildItem -LiteralPath $LayoutDirectory -File -Recurse |
        ForEach-Object {
            $relativePath = $_.FullName.Substring($LayoutDirectory.Length + 1)
            if ($relativePath -notlike 'AppPackages\*' -and $_.Extension -notin '.pdb', '.msix', '.appx') {
                '"{0}" "{1}"' -f $_.FullName, $relativePath
            }
        })
    Set-Content -LiteralPath $mappingPath -Value $mapping -Encoding Unicode
    & $makeAppx pack /f $mappingPath /p $PackagePath /o
    if ($LASTEXITCODE -ne 0 -or -not (Test-Path -LiteralPath $PackagePath -PathType Leaf)) {
        throw "makeappx failed to package SamplePagesExtension (exit $LASTEXITCODE)."
    }
}
finally {
    Remove-Item -LiteralPath $mappingPath -Force -ErrorAction Stop
}

Write-Host "Created unsigned UI-test package: $PackagePath"
