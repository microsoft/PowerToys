# Copyright (c) Microsoft Corporation.
# Licensed under the MIT license.
# Signature verification is mocked only in this test's process scope. No
# certificate, trust store, installed package, or production output is modified.
[CmdletBinding()]
param(
    [ValidateSet('x64', 'ARM64')][string]$Platform = 'x64',
    [ValidateSet('Debug', 'Release')][string]$Configuration = 'Release'
)
$ErrorActionPreference = 'Stop'
$repo = (Resolve-Path "$PSScriptRoot\..\..\..").Path
$verify = (Resolve-Path "$PSScriptRoot\..\Verify-Release.ps1").Path
$source = "$repo\$Platform\$Configuration\ProtectedStorage\PowerToys.ProtectedStorageSetup.exe"
$scratch = Join-Path $PSScriptRoot ("Publication-" + [guid]::NewGuid().ToString('N'))
$certificateBytes = [Text.Encoding]::UTF8.GetBytes('mock-only-publication-test-certificate')
$hasher = [Security.Cryptography.SHA256]::Create()
try {
    $pin = ([BitConverter]::ToString($hasher.ComputeHash($certificateBytes))).Replace('-', '').ToLowerInvariant()
} finally {
    $hasher.Dispose()
}
function Get-AuthenticodeSignature {
    param([string]$LiteralPath)
    if (!(Test-Path -LiteralPath $LiteralPath)) { throw 'Missing signature fixture.' }
    [pscustomobject]@{ Status = 'Valid'; SignerCertificate = [pscustomobject]@{ RawData = $certificateBytes } }
}
try {
    $stage = New-Item -ItemType Directory -Path "$scratch\Stage"
    $publishedDirectory = New-Item -ItemType Directory -Path "$scratch\BinDir"
    $setup = Join-Path $stage.FullName 'PowerToys.ProtectedStorageSetup.exe'
    $published = Join-Path $publishedDirectory.FullName 'PowerToys.ProtectedStorageSetup.exe'
    Copy-Item -LiteralPath $source -Destination $setup
    Copy-Item -LiteralPath $source -Destination $published
    $carrier = Join-Path $stage.FullName 'PowerToys.ProtectedStorage.Carrier.msi'
    $lifecycle = Join-Path $stage.FullName 'PowerToys.ProtectedStorageLifecycle.exe'
    [IO.File]::WriteAllText($carrier, 'non-installable-publication-fixture')
    [IO.File]::WriteAllText($lifecycle, 'non-executable-lifecycle-fixture')
    $version = ([Diagnostics.FileVersionInfo]::GetVersionInfo($source).FileVersion.Split('.')[0..2]) -join '.'
    @{
        version = $version
        platform = $Platform
        signerSha256 = $pin
        setupSha256 = (Get-FileHash -LiteralPath $setup -Algorithm SHA256).Hash.ToLowerInvariant()
        carrierSha256 = (Get-FileHash -LiteralPath $carrier -Algorithm SHA256).Hash.ToLowerInvariant()
        lifecycleSha256 = (Get-FileHash -LiteralPath $lifecycle -Algorithm SHA256).Hash.ToLowerInvariant()
    } | ConvertTo-Json | Set-Content -LiteralPath "$stage\release.json" -Encoding UTF8
    $parameters = @{ PackageRoot = $stage.FullName; ExpectedSignerSha256 = $pin; Version = $version; Platform = $Platform; PublishedSetupPath = $published }
    & $verify @parameters
    [IO.File]::AppendAllText($published, 'tamper')
    $rejected = $false
    try {
        & $verify @parameters
    } catch {
        if ($_.Exception.Message -notlike 'The public BinDir Setup*') { throw }
        $rejected = $true
    }
    if (!$rejected) { throw 'A changed public Setup passed the packaging prerequisite.' }
    Write-Host 'PASS mocked release publication: exact signed-source bytes required at BinDir; tampering rejected.'
} finally {
    Remove-Item Function:\Get-AuthenticodeSignature
    if (Test-Path -LiteralPath $scratch) { Remove-Item -LiteralPath $scratch -Recurse -Force }
}
