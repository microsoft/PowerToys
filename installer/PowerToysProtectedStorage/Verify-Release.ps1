# Copyright (c) Microsoft Corporation.
# Licensed under the MIT license.
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$PackageRoot,
    [Parameter(Mandatory)][ValidatePattern('^[0-9a-fA-F]{64}$')][string]$ExpectedSignerSha256,
    [Parameter(Mandatory)][string]$Version,
    [Parameter(Mandatory)][ValidateSet('x64', 'ARM64')][string]$Platform,
    [string]$PublishedSetupPath
)
$ErrorActionPreference = 'Stop'
. "$PSScriptRoot\ReleaseTools.ps1"
$releaseVersion = Get-CarrierVersion $Version
$Version = $releaseVersion.Msi
$release = Get-Content -LiteralPath (Join-Path $PackageRoot 'release.json') -Raw | ConvertFrom-Json
if ($release.version -ne $Version -or $release.platform -ne $Platform -or
    $release.signerSha256 -ne $ExpectedSignerSha256.ToLowerInvariant()) {
    throw 'Protected-storage release identity does not match the main installer release.'
}
foreach ($entry in @(
    @{ Name = 'PowerToys.ProtectedStorageSetup.exe'; Hash = $release.setupSha256 },
    @{ Name = 'PowerToys.ProtectedStorage.Carrier.msi'; Hash = $release.carrierSha256 },
    @{ Name = 'PowerToys.ProtectedStorageLifecycle.exe'; Hash = $release.lifecycleSha256 }
)) {
    $path = Join-Path $PackageRoot $entry.Name
    if ((Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash.ToLowerInvariant() -ne $entry.Hash) {
        throw "Protected-storage release hash mismatch: $($entry.Name)"
    }
    Assert-ReleaseSignature -Path $path -ExpectedSignerSha256 $ExpectedSignerSha256
    if ($entry.Name -eq 'PowerToys.ProtectedStorageSetup.exe') {
        $identity = [Diagnostics.FileVersionInfo]::GetVersionInfo($path)
        if ($identity.CompanyName -cne 'Microsoft Corporation' -or
            $identity.ProductName -cne 'PowerToys (Preview) Protected Storage' -or
            $identity.OriginalFilename -cne 'PowerToys.ProtectedStorageSetup.exe' -or
            $identity.FileVersion -ne "$Version.0") {
            throw 'Protected-storage Setup has an unexpected version-resource identity.'
        }
    }
}
if ($PublishedSetupPath -and
    (Get-FileHash -LiteralPath $PublishedSetupPath -Algorithm SHA256).Hash.ToLowerInvariant() -ne $release.setupSha256) {
    throw 'The public BinDir Setup does not exactly match the finalized signed release.'
}
Write-Host 'Verified finalized protected-storage Setup, carrier, and Lifecycle for main-installer packaging.'
