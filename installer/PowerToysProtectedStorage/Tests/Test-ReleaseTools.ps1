# Copyright (c) Microsoft Corporation.
# Licensed under the MIT license.
# In-memory test keys only: no certificate stores, services, or installers.
#Requires -Version 7.2
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

Add-Type -AssemblyName System.Security.Cryptography.Pkcs
$scratch = Join-Path $PSScriptRoot ("ReleaseTools-" + [guid]::NewGuid().ToString('N'))
$key = [Security.Cryptography.RSA]::Create(2048)
$request = [Security.Cryptography.X509Certificates.CertificateRequest]::new('CN=InMemoryReleaseTest', $key,
    [Security.Cryptography.HashAlgorithmName]::SHA256, [Security.Cryptography.RSASignaturePadding]::Pkcs1)
$certificate = $request.CreateSelfSigned([DateTimeOffset]::UtcNow.AddMinutes(-1), [DateTimeOffset]::UtcNow.AddMinutes(5))
try {
    New-Item -ItemType Directory -Path $scratch | Out-Null
    $contentPath = Join-Path $scratch 'document.json'
    $signaturePath = Join-Path $scratch 'document.p7s'
    $original = [Text.Encoding]::UTF8.GetBytes('{"format":1,"value":"signed test data"}')
    [IO.File]::WriteAllBytes($contentPath, $original)
    $pin = Get-CertificatePin $certificate
    # Mirror ESRP's replacement contract: submit a copy, preserve the original.
    Copy-Item -LiteralPath $contentPath -Destination $signaturePath
    $content = [Security.Cryptography.Pkcs.ContentInfo]::new([IO.File]::ReadAllBytes($signaturePath))
    $cms = [Security.Cryptography.Pkcs.SignedCms]::new($content, $true)
    $signer = [Security.Cryptography.Pkcs.CmsSigner]::new($certificate)
    $signer.DigestAlgorithm = [Security.Cryptography.Oid]::new('2.16.840.1.101.3.4.2.1')
    $cms.ComputeSignature($signer)
    [IO.File]::WriteAllBytes($signaturePath, $cms.Encode())
    $parameters = @{ Path = $contentPath; SignaturePath = $signaturePath; ExpectedSignerSha256 = $pin }
    Assert-DetachedSignature @parameters
    Expect-Rejection { Assert-DetachedSignature -Path $contentPath -SignaturePath $signaturePath -ExpectedSignerSha256 ('0' * 64) }
    [IO.File]::AppendAllText($contentPath, 'tamper')
    Expect-Rejection { Assert-DetachedSignature @parameters }
    [IO.File]::WriteAllBytes($contentPath, $original)
    $cms.ComputeSignature($signer)
    [IO.File]::WriteAllBytes($signaturePath, $cms.Encode())
    Expect-Rejection { Assert-DetachedSignature @parameters }
    $cms = [Security.Cryptography.Pkcs.SignedCms]::new($content, $true)
    $signer.DigestAlgorithm = [Security.Cryptography.Oid]::new('1.3.14.3.2.26')
    $cms.ComputeSignature($signer)
    [IO.File]::WriteAllBytes($signaturePath, $cms.Encode())
    Expect-Rejection { Assert-DetachedSignature @parameters }
    [IO.File]::WriteAllText($signaturePath, 'not a signature')
    Expect-Rejection { Assert-DetachedSignature @parameters }
    Write-Host 'PASS release versions and real CMS verification: valid signature, wrong pin, changed bytes, multiple signers, SHA1, malformed signature.'
} finally {
    $certificate.Dispose()
    $key.Dispose()
    if (Test-Path -LiteralPath $scratch) { Remove-Item -LiteralPath $scratch -Recurse -Force }
}
