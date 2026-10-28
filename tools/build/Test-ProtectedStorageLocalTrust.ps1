# Copyright (c) Microsoft Corporation.
# Licensed under the MIT license.
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$Verifier,
    [Parameter(Mandatory)][string]$Document,
    [Parameter(Mandatory)][string]$Signature,
    [switch]$RequireExpiredFixture
)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Security.Cryptography.Pkcs
$content = [IO.File]::ReadAllBytes($Document)
$cms = [Security.Cryptography.Pkcs.SignedCms]::new([Security.Cryptography.Pkcs.ContentInfo]::new($content), $true)
$cms.Decode([IO.File]::ReadAllBytes($Signature))
$expired = $cms.SignerInfos[0].Certificate.NotAfter -lt (Get-Date)
if ($RequireExpiredFixture -and !$expired) { throw 'An actually expired fixture is required; do not change the machine clock.' }
$fixture = Join-Path ([IO.Path]::GetTempPath()) "PowerToysPinnedTrust-$([guid]::NewGuid().ToString('N'))"
New-Item -ItemType Directory -Path $fixture | Out-Null
$rsa = [Security.Cryptography.RSA]::Create(2048)
$certificate = $null
try {
    & $Verifier --verify-test-cms $Document $Signature
    if ($LASTEXITCODE) { throw 'The exact signed local fixture was rejected.' }
    $changed = [byte[]]$content.Clone()
    $changed[0] = $changed[0] -bxor 1
    $changedFile = Join-Path $fixture 'changed.txt'
    [IO.File]::WriteAllBytes($changedFile, $changed)
    & $Verifier --verify-test-cms $changedFile $Signature *> (Join-Path $fixture 'changed.log')
    if ($LASTEXITCODE -ne 1) { throw 'Modified content was not rejected by signature verification.' }
    $request = [Security.Cryptography.X509Certificates.CertificateRequest]::new('CN=Unpinned local fixture', $rsa,
        [Security.Cryptography.HashAlgorithmName]::SHA256, [Security.Cryptography.RSASignaturePadding]::Pkcs1)
    $certificate = $request.CreateSelfSigned([DateTimeOffset]::UtcNow.AddDays(-2), [DateTimeOffset]::UtcNow.AddDays(-1))
    $other = [Security.Cryptography.Pkcs.SignedCms]::new([Security.Cryptography.Pkcs.ContentInfo]::new($content), $true)
    $other.ComputeSignature([Security.Cryptography.Pkcs.CmsSigner]::new($certificate), $true)
    $otherFile = Join-Path $fixture 'other.p7s'
    [IO.File]::WriteAllBytes($otherFile, $other.Encode())
    & $Verifier --verify-test-cms $Document $otherFile *> (Join-Path $fixture 'other.log')
    if ($LASTEXITCODE -ne 1) { throw 'A different signing key was not rejected.' }
    $global:LASTEXITCODE = 0
    Write-Host "PASS build-pinned local trust: expiredFixture=$expired; changed content and different signer rejected."
}
finally {
    if ($certificate) { $certificate.Dispose() }
    $rsa.Dispose()
    Remove-Item -LiteralPath $fixture -Recurse -Force
}
