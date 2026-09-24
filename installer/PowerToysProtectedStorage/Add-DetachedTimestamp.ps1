# Copyright (c) Microsoft Corporation.
# Licensed under the MIT license.
#Requires -Version 7.2
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$Path,
    [Parameter(Mandatory)][string]$SignaturePath,
    [Parameter(Mandatory)][string]$TrustVerifierPath,
    [string]$TimestampServer = 'http://timestamp.acs.microsoft.com'
)
$ErrorActionPreference = 'Stop'
. "$PSScriptRoot\ReleaseTools.ps1"
Import-ReleaseTimestamping
$Path = (Resolve-Path -LiteralPath $Path).Path
$SignaturePath = (Resolve-Path -LiteralPath $SignaturePath).Path
$pending = "$SignaturePath.$([guid]::NewGuid().ToString('N')).pending"
try {
    $timestamped = [PowerToys.ProtectedStorage.Build.Timestamping]::AddTimestamp(
        [IO.File]::ReadAllBytes($Path), [IO.File]::ReadAllBytes($SignaturePath), $TimestampServer)
    [IO.File]::WriteAllBytes($pending, $timestamped)
    Assert-DetachedSignature -Path $Path -SignaturePath $pending -TrustVerifierPath $TrustVerifierPath
    [IO.File]::Move($pending, $SignaturePath, $true)
} finally {
    Remove-Item -LiteralPath $pending -ErrorAction SilentlyContinue
}
