# Copyright (c) Microsoft Corporation.
# Licensed under the MIT license.

function Get-CarrierVersion {
    param([Parameter(Mandatory)][string]$Version)
    if ($Version -notmatch '^(0|[1-9]\d{0,2})\.(0|[1-9]\d{0,2})\.(0|[1-9]\d{0,4})(?:\.0)?$' -or
        [int]$Matches[1] -gt 255 -or [int]$Matches[2] -gt 255 -or [int]$Matches[3] -gt 65535) {
        throw 'Version must be MSI major.minor.build, optionally followed by .0; nonzero revisions are not supported.'
    }
    $msiVersion = "$($Matches[1]).$($Matches[2]).$($Matches[3])"
    [pscustomobject]@{ Msi = $msiVersion; PE = "$msiVersion.0" }
}

function Get-ReleaseHash {
    param([Parameter(Mandatory)][string]$Path)
    (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash.ToLowerInvariant()
}

function Get-CertificatePin {
    param([Parameter(Mandatory)]$Certificate)
    $hash = [Security.Cryptography.SHA256]::Create()
    try {
        ([BitConverter]::ToString($hash.ComputeHash($Certificate.RawData))).Replace('-', '').ToLowerInvariant()
    } finally {
        $hash.Dispose()
    }
}

function Assert-ReleaseSignature {
    param(
        [Parameter(Mandatory)][string]$Path,
        [Parameter(Mandatory)][ValidatePattern('^[a-fA-F0-9]{64}$')][string]$ExpectedSignerSha256
    )
    $signature = Get-AuthenticodeSignature -LiteralPath $Path
    if ($signature.Status -ne 'Valid' -or !$signature.SignerCertificate) {
        throw "Protected-storage release is not validly signed: $Path"
    }
    if ((Get-CertificatePin $signature.SignerCertificate) -ne $ExpectedSignerSha256.ToLowerInvariant()) {
        throw "Protected-storage release signer mismatch: $Path"
    }
}

function Assert-DetachedSignature {
    param(
        [Parameter(Mandatory)][string]$Path,
        [Parameter(Mandatory)][string]$SignaturePath,
        [Parameter(Mandatory)][ValidatePattern('^[a-fA-F0-9]{64}$')][string]$ExpectedSignerSha256
    )
    Add-Type -AssemblyName System.Security.Cryptography.Pkcs
    $bytes = [IO.File]::ReadAllBytes($Path)
    $signatureBytes = [IO.File]::ReadAllBytes($SignaturePath)
    if (!$bytes.Length -or $bytes.Length -gt 65536 -or !$signatureBytes.Length -or $signatureBytes.Length -gt 1048576) {
        throw 'Detached release document/signature exceeds the runtime bounds.'
    }
    $content = [Security.Cryptography.Pkcs.ContentInfo]::new($bytes)
    $cms = [Security.Cryptography.Pkcs.SignedCms]::new($content, $true)
    $cms.Decode($signatureBytes)
    if ($cms.SignerInfos.Count -ne 1 -or $cms.SignerInfos[0].DigestAlgorithm.Value -ne '2.16.840.1.101.3.4.2.1' -or
        (Get-CertificatePin $cms.SignerInfos[0].Certificate) -ne $ExpectedSignerSha256.ToLowerInvariant()) {
        throw "Detached release signature must have exactly one SHA256 signer matching the policy pin: $Path"
    }
    $cms.CheckSignature($true)
    if (!('PowerToys.ProtectedStorage.Build.SignatureVerification' -as [type])) {
        Add-Type -Path "$PSScriptRoot\SignatureVerification.cs"
    }
    [PowerToys.ProtectedStorage.Build.SignatureVerification]::Verify($bytes, $signatureBytes)
}

function Assert-ReleaseDocuments {
    param(
        [Parameter(Mandatory)][string]$PackageRoot,
        [Parameter(Mandatory)][string]$Version,
        [Parameter(Mandatory)][ValidatePattern('^[a-fA-F0-9]{64}$')][string]$ExpectedSignerSha256
    )
    $peVersion = (Get-CarrierVersion $Version).PE
    Assert-DetachedSignature -Path "$PackageRoot\manifest.txt" -SignaturePath "$PackageRoot\manifest.p7s" -ExpectedSignerSha256 $ExpectedSignerSha256
    Assert-DetachedSignature -Path "$PackageRoot\ClientCatalog.json" -SignaturePath "$PackageRoot\ClientCatalog.p7s" -ExpectedSignerSha256 $ExpectedSignerSha256
    $expectedManifest = "format=1`napp=PowerToysProtectedStorage`nversion=$peVersion`nbootstrap_sha256=$(Get-ReleaseHash "$PackageRoot\Bootstrap.exe")`nruntime_sha256=$(Get-ReleaseHash "$PackageRoot\Runtime.exe")`ncatalog_sha256=$(Get-ReleaseHash "$PackageRoot\ClientCatalog.json")`n"
    if ([IO.File]::ReadAllText("$PackageRoot\manifest.txt") -cne $expectedManifest) {
        throw 'Signed manifest does not describe the current release payloads.'
    }
    $expectedPolicy = "format=1`napp=PowerToysProtectedStorage`nsigner_sha256=$($ExpectedSignerSha256.ToLowerInvariant())`nminimum_version=$peVersion`n"
    if ([IO.File]::ReadAllText("$PackageRoot\policy.txt") -cne $expectedPolicy) {
        throw 'Seed policy does not match the explicit release signing identity.'
    }
    $catalog = Get-Content -LiteralPath "$PackageRoot\ClientCatalog.json" -Raw | ConvertFrom-Json
    if ($catalog.format -ne 1 -or $catalog.app -cne 'PowerToysProtectedStorage' -or $catalog.version -cne $peVersion) {
        throw 'Signed catalog release identity mismatch.'
    }
    $maintenance = @($catalog.clients | Where-Object { $_.role -eq 'maintenance' })
    if ($maintenance.Count -ne 1 -or $maintenance[0].image -cne 'PowerToys.ProtectedStorageMsiAction.exe' -or
        $maintenance[0].sha256 -cne (Get-ReleaseHash "$PackageRoot\PowerToys.ProtectedStorageMsiAction.exe")) {
        throw 'Signed catalog does not bind the current MSI action.'
    }
}
