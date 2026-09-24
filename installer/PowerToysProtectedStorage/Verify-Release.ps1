# Copyright (c) Microsoft Corporation.
# Licensed under the MIT license.
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$PackageRoot,
    [Parameter(Mandatory)][string]$TrustPolicy,
    [Parameter(Mandatory)][string]$TrustVerifierPath,
    [Parameter(Mandatory)][string]$Version,
    [Parameter(Mandatory)][ValidateSet('x64', 'ARM64')][string]$Platform,
    [string]$PublishedSetupPath
)
$ErrorActionPreference = 'Stop'
. "$PSScriptRoot\ReleaseTools.ps1"
Assert-ProductionTrustPolicy $TrustPolicy
$releaseVersion = Get-CarrierVersion $Version
$Version = $releaseVersion.Msi
$release = Get-Content -LiteralPath (Join-Path $PackageRoot 'release.json') -Raw | ConvertFrom-Json
if ($release.version -ne $Version -or $release.platform -ne $Platform -or
    $release.peVersion -cne $releaseVersion.PE -or
    $release.trustPolicy -cne $TrustPolicy) {
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
    Assert-ReleaseSignature -Path $path -TrustVerifierPath $TrustVerifierPath
    if ($entry.Name -ne 'PowerToys.ProtectedStorageSetup.exe' -and [IO.Path]::GetExtension($path) -eq '.exe' -and
        [Diagnostics.FileVersionInfo]::GetVersionInfo($path).FileVersion -ne $releaseVersion.PE) {
        throw "Protected-storage helper version does not match the release: $($entry.Name)"
    }
    if ($entry.Name -eq 'PowerToys.ProtectedStorageSetup.exe') {
        $identity = [Diagnostics.FileVersionInfo]::GetVersionInfo($path)
        if ($identity.CompanyName -cne 'Microsoft Corporation' -or
            $identity.ProductName -cne 'PowerToys (Preview) Protected Storage' -or
            $identity.OriginalFilename -cne 'PowerToys.ProtectedStorageSetup.exe' -or
            $identity.FileVersion -ne $releaseVersion.PE -or $identity.ProductVersion -ne $releaseVersion.PE) {
            throw 'Protected-storage Setup has an unexpected version-resource identity.'
        }
    }
}
if ($PublishedSetupPath -and
    (Get-FileHash -LiteralPath $PublishedSetupPath -Algorithm SHA256).Hash.ToLowerInvariant() -ne $release.setupSha256) {
    throw 'The public BinDir Setup does not exactly match the finalized signed release.'
}
foreach ($name in @('Bootstrap.exe', 'Runtime.exe', 'PowerToys.ProtectedStorageMsiAction.exe',
    'PowerToys.ProtectedStorageProvisionBroker.exe')) {
    $path = Join-Path $PackageRoot $name
    Assert-ReleaseSignature -Path $path -TrustVerifierPath $TrustVerifierPath
    if ([Diagnostics.FileVersionInfo]::GetVersionInfo($path).FileVersion -ne $releaseVersion.PE) {
        throw "Protected-storage helper version does not match the release: $name"
    }
}
Assert-ReleaseDocuments -PackageRoot $PackageRoot -Version $Version -TrustVerifierPath $TrustVerifierPath
Write-Host 'Verified finalized protected-storage Setup, carrier, and Lifecycle for main-installer packaging.'
