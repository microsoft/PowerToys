# Copyright (c) Microsoft Corporation.
# Licensed under the MIT license.
# In-memory test keys only: no certificate stores, services, or installers.
#Requires -Version 7.2
[CmdletBinding()]
param(
    [switch]$LiveTimestamp,
    [string]$TrustVerifierPath
)
$ErrorActionPreference = 'Stop'
. "$PSScriptRoot\..\ReleaseTools.ps1"

function Expect-Rejection([scriptblock]$Action) {
    $rejected = $false
    try { & $Action } catch { $rejected = $true }
    if (!$rejected) { throw 'Invalid release input was accepted.' }
}

foreach ($version in @('0.101.3000', '0.101.3000.0')) {
    $result = Get-CarrierVersion $version
    if ($result.Msi -cne '0.101.3000' -or $result.PE -cne '0.101.3000.0') {
        throw 'MSI/PE version normalization failed.'
    }
}
foreach ($version in @('0.101.3000.1', '0.256.0', '256.0.0', '0.101.65536', '00.101.3000', '0.101', '0.101.3000.0.0')) {
    Expect-Rejection { Get-CarrierVersion $version }
}
Assert-ProductionTrustPolicy microsoft-production-v1
foreach ($policy in @('MICROSOFT-PRODUCTION-V1', 'development', ('a' * 64), '')) {
    Expect-Rejection { Assert-ProductionTrustPolicy $policy }
}
Expect-Rejection { Assert-ReleaseClientCatalog @(@{ image = 'ProtectedStorage.TrustVerifier.exe'; role = 'workspaces.reader'; sha256 = 'a' * 64 }) }
Expect-Rejection { Assert-ReleaseClientCatalog @(@{ image = 'PowerToys.ProtectedStorageSetup.exe'; role = 'workspaces.writer'; sha256 = 'a' * 64 }) }
Expect-Rejection { Assert-ReleaseSignature -Path $PSCommandPath -TrustVerifierPath "$PSScriptRoot\missing-verifier.exe" }

Add-Type -Path "$PSScriptRoot\..\Timestamping.cs", "$PSScriptRoot\TimestampTests.cs" `
    -ReferencedAssemblies (@(Get-ReleaseTimestampReferences) + 'System.Formats.Asn1', 'System.Runtime.Numerics')
Write-Host ([PowerToys.ProtectedStorage.Build.Tests.TimestampTests]::Run())

if ($LiveTimestamp -or $TrustVerifierPath) {
    $scratch = Join-Path $PSScriptRoot ("ReleaseTools-" + [guid]::NewGuid().ToString('N'))
    $key = [Security.Cryptography.RSA]::Create(2048)
    $request = [Security.Cryptography.X509Certificates.CertificateRequest]::new('CN=Microsoft Corporation, O=Microsoft Corporation', $key,
        [Security.Cryptography.HashAlgorithmName]::SHA256, [Security.Cryptography.RSASignaturePadding]::Pkcs1)
    $usage = [Security.Cryptography.OidCollection]::new()
    [void]$usage.Add([Security.Cryptography.Oid]::new('1.3.6.1.5.5.7.3.3'))
    $request.CertificateExtensions.Add([Security.Cryptography.X509Certificates.X509EnhancedKeyUsageExtension]::new($usage, $false))
    $certificate = $request.CreateSelfSigned([DateTimeOffset]::UtcNow.AddMinutes(-1), [DateTimeOffset]::UtcNow.AddMinutes(10))
    try {
        New-Item -ItemType Directory -Path $scratch | Out-Null
        $contentPath = Join-Path $scratch 'document.json'
        $signaturePath = Join-Path $scratch 'document.p7s'
        $original = [Text.Encoding]::UTF8.GetBytes('{"format":1,"value":"in-memory timestamp mechanics only"}')
        [IO.File]::WriteAllBytes($contentPath, $original)
        # ESRP receives a copy and returns detached DER without changing the original.
        Copy-Item -LiteralPath $contentPath -Destination $signaturePath
        $cms = [Security.Cryptography.Pkcs.SignedCms]::new(
            [Security.Cryptography.Pkcs.ContentInfo]::new([IO.File]::ReadAllBytes($signaturePath)), $true)
        $signer = [Security.Cryptography.Pkcs.CmsSigner]::new($certificate)
        $signer.DigestAlgorithm = [Security.Cryptography.Oid]::new('2.16.840.1.101.3.4.2.1')
        $cms.ComputeSignature($signer)
        $signature = $cms.Encode()
        if ($LiveTimestamp) {
            $signature = [PowerToys.ProtectedStorage.Build.Timestamping]::AddTimestamp(
                $original, $signature, 'http://timestamp.acs.microsoft.com')
            $decoded = [PowerToys.ProtectedStorage.Build.Timestamping]::ReadDetached($original, $signature)
            if (![PowerToys.ProtectedStorage.Build.Timestamping]::HasValidTimestamp($decoded.SignerInfos[0])) {
                throw 'Microsoft endpoint did not return a bound timestamp.'
            }
            $again = [PowerToys.ProtectedStorage.Build.Timestamping]::AddTimestamp(
                $original, $signature, 'http://timestamp.acs.microsoft.com')
            if ([Convert]::ToBase64String($again) -cne [Convert]::ToBase64String($signature)) {
                throw 'Already-timestamped CMS was changed instead of preserving its one timestamp.'
            }
            Write-Host 'PASS live Microsoft RFC3161 endpoint: bound timestamp, original bytes unchanged, one timestamp preserved. Only a random signature digest was sent.'
        }
        [IO.File]::WriteAllBytes($signaturePath, $signature)
        if (([IO.File]::ReadAllText($contentPath)) -cne [Text.Encoding]::UTF8.GetString($original)) {
            throw 'Timestamp operation altered the release document.'
        }
        if ($TrustVerifierPath) {
            if (!(Test-Path -LiteralPath $TrustVerifierPath -PathType Leaf)) { throw 'Requested native verifier is missing.' }
            Expect-Rejection { Assert-DetachedSignature -Path $contentPath -SignaturePath $signaturePath -TrustVerifierPath $TrustVerifierPath }
            Write-Host 'PASS native production policy rejected the self-signed Microsoft-looking code-signing certificate.'
            if ($LiveTimestamp) {
                Write-Host 'A verified Microsoft RFC3161 timestamp did not grant publisher authority to the self-signed certificate.'
            }
        }
    } finally {
        $certificate.Dispose()
        $key.Dispose()
        if (Test-Path -LiteralPath $scratch) { Remove-Item -LiteralPath $scratch -Recurse -Force }
    }
}
Write-Host 'PASS release version normalization and fail-closed fixed policy, helper exclusion, and missing verifier.'
