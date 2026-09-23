# Copyright (c) Microsoft Corporation.
# Licensed under the MIT license.
[CmdletBinding()]
param(
    [ValidateSet('x64', 'ARM64')][string]$Platform = 'x64',
    [ValidateSet('Debug', 'Release')][string]$Configuration = 'Release',
    [Parameter(Mandatory)][string]$OutputPath,
    [Parameter(Mandatory)][string]$ModuleServicesHostPath
)
$ErrorActionPreference = 'Stop'
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$bin = Join-Path $repoRoot "$Platform\$Configuration"
$hostPath = (Resolve-Path -LiteralPath $ModuleServicesHostPath).Path
if ([IO.Path]::GetFileName($hostPath) -cne 'Microsoft.CmdPal.Ext.PowerToys.exe') {
    throw 'The approved ModuleServices host is Microsoft.CmdPal.Ext.PowerToys.exe; do not authorize arbitrary hosts.'
}
$clients = @(
    @{ path = Join-Path $bin 'PowerToys.WorkspacesEditor.exe'; role = 'workspaces.writer' }
    @{ path = Join-Path $bin 'PowerToys.WorkspacesEditor.exe'; role = 'workspaces.preview' }
    @{ path = Join-Path $bin 'PowerToys.WorkspacesLauncher.exe'; role = 'workspaces.launcher' }
    @{ path = Join-Path $bin 'PowerToys.WorkspacesLauncher.exe'; role = 'workspaces.preview' }
    @{ path = Join-Path $bin 'PowerToys.WorkspacesSnapshotTool.exe'; role = 'workspaces.preview' }
    @{ path = Join-Path $bin 'PowerToys.WorkspacesWindowArranger.exe'; role = 'workspaces.arranger' }
    @{ path = $hostPath; role = 'workspaces.reader' }
)
foreach ($client in $clients) {
    $client.path = (Resolve-Path -LiteralPath $client.path).Path
    if ((Get-AuthenticodeSignature -LiteralPath $client.path).Status -ne 'Valid') {
        throw "Sign the final consumer binary before catalog generation: $($client.path)"
    }
}
$destination = [IO.Path]::GetFullPath($OutputPath)
if (Test-Path -LiteralPath $destination) { throw 'Refusing to overwrite an existing release inventory.' }
$directory = [IO.Path]::GetDirectoryName($destination)
New-Item -ItemType Directory -Path $directory -Force | Out-Null
[IO.File]::WriteAllText($destination, (@{ clients = $clients } | ConvertTo-Json -Depth 4), [Text.UTF8Encoding]::new($false))
Write-Host "Unsigned build inventory created at $destination; Build-Carrier must still validate signer policy and sign the resulting catalog."
