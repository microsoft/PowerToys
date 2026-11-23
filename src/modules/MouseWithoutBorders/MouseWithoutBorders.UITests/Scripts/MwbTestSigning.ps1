# Copyright (c) Microsoft Corporation
# The Microsoft Corporation licenses this file to you under the MIT license.
# See the LICENSE file in the project root for more information.

function Get-MwbCertificateRawDataSha256 {
    param($Certificate)
    $hash = [Security.Cryptography.SHA256]::Create()
    try { [BitConverter]::ToString($hash.ComputeHash($Certificate.RawData)).Replace('-', '') }
    finally { $hash.Dispose() }
}

function Get-MwbTestSigningCertificate {
    param([string]$Path, [switch]$AllowExpired)
    $ErrorActionPreference = 'Stop'
    $bytes = [IO.File]::ReadAllBytes($Path)
    if ([Security.Cryptography.X509Certificates.X509Certificate2]::GetCertContentType($bytes) -ne
        [Security.Cryptography.X509Certificates.X509ContentType]::Cert) {
        throw 'MWB test trust accepts only a public certificate, never a private-key archive.'
    }
    $certificate = [Security.Cryptography.X509Certificates.X509Certificate2]::new($bytes)
    try {
        $publisher = 'CN=Microsoft Corporation, O=Microsoft Corporation, L=Redmond, S=Washington, C=US'
        $usage = @($certificate.Extensions | Where-Object { $_.Oid.Value -eq '2.5.29.37' })
        if ($certificate.HasPrivateKey -or $certificate.Subject -cne $publisher -or
            $certificate.Issuer -cne $publisher -or $certificate.NotBefore.ToUniversalTime() -gt [DateTime]::UtcNow -or
            (-not $AllowExpired -and $certificate.NotAfter.ToUniversalTime() -le [DateTime]::UtcNow) -or $usage.Count -ne 1 -or
            @($usage[0].EnhancedKeyUsages | Where-Object { $_.Value -eq '1.3.6.1.5.5.7.3.3' }).Count -ne 1) {
            throw 'MWB test trust requires an unexpired self-signed Microsoft-subject code-signing public certificate.'
        }
        [pscustomobject]@{
            Path = [IO.Path]::GetFullPath($Path)
            Sha256 = (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash
            Thumbprint = $certificate.Thumbprint
            RawDataSha256 = Get-MwbCertificateRawDataSha256 $certificate
        }
    }
    finally { $certificate.Dispose() }
}

function Get-MwbTestSigningTrust {
    param([string]$Path)
    $certificate = Get-MwbTestSigningCertificate $Path
    $addedStores = @()
    foreach ($store in @('Cert:\LocalMachine\Root', 'Cert:\LocalMachine\TrustedPeople')) {
        $path = Join-Path $store $certificate.Thumbprint
        if (-not (Test-Path -LiteralPath $path)) {
            $addedStores += $store
        }
        elseif ((Get-MwbCertificateRawDataSha256 (Get-Item -LiteralPath $path)) -cne $certificate.RawDataSha256) {
            throw 'An existing certificate-store entry does not match the MWB public certificate.'
        }
    }
    [pscustomobject]@{
        CertificatePath = $certificate.Path
        Sha256 = $certificate.Sha256
        Thumbprint = $certificate.Thumbprint
        RawDataSha256 = $certificate.RawDataSha256
        AddedStores = $addedStores
    }
}

function Assert-MwbTestSigningTrustIdentity {
    param($Trust, [switch]$AllowExpired)
    $certificate = Get-MwbTestSigningCertificate $Trust.CertificatePath -AllowExpired:$AllowExpired
    if ($certificate.Sha256 -ine $Trust.Sha256 -or $certificate.Thumbprint -cne $Trust.Thumbprint -or
        $certificate.RawDataSha256 -cne $Trust.RawDataSha256 -or
        @($Trust.AddedStores | Select-Object -Unique).Count -ne @($Trust.AddedStores).Count -or
        @($Trust.AddedStores | Where-Object {
            $_ -cnotin @('Cert:\LocalMachine\Root', 'Cert:\LocalMachine\TrustedPeople')
        }).Count) {
        throw 'The owned MWB test certificate or trust-store inventory changed.'
    }
}

function Install-MwbTestSigningTrust {
    param($Trust)
    Assert-MwbTestSigningTrustIdentity $Trust
    foreach ($store in $Trust.AddedStores) {
        $path = Join-Path $store $Trust.Thumbprint
        if (Test-Path -LiteralPath $path) {
            throw 'A planned MWB test trust entry appeared before this run imported it.'
        }
        Import-Certificate -FilePath $Trust.CertificatePath -CertStoreLocation $store -ErrorAction Stop | Out-Null
        if (-not (Test-Path -LiteralPath $path) -or
            (Get-MwbCertificateRawDataSha256 (Get-Item -LiteralPath $path)) -cne $Trust.RawDataSha256) {
            throw 'The owned MWB public test certificate was not imported.'
        }
    }
}

function Remove-MwbTestSigningTrust {
    param($Trust)
    Assert-MwbTestSigningTrustIdentity $Trust -AllowExpired
    foreach ($store in $Trust.AddedStores) {
        $path = Join-Path $store $Trust.Thumbprint
        if (Test-Path -LiteralPath $path) {
            if ((Get-MwbCertificateRawDataSha256 (Get-Item -LiteralPath $path)) -cne $Trust.RawDataSha256) {
                throw 'An owned certificate-store entry was replaced; refusing cleanup.'
            }
            Remove-Item -LiteralPath $path -Force -ErrorAction Stop
        }
        if (Test-Path -LiteralPath $path) {
            throw 'An owned MWB test trust entry remains after cleanup.'
        }
    }
}

function Assert-MwbEndpointSignatures {
    param([string]$ProductRoot, $TestCertificate, [switch]$AllowUntrustedTestCertificate)
    $identities = @()
    foreach ($relative in @('PowerToys.exe', 'PowerToys.MouseWithoutBorders.exe',
        'PowerToys.MouseWithoutBordersHelper.exe', 'WinUI3Apps\PowerToys.Settings.exe',
        'WinUI3Apps\PowerToys.QuickAccess.exe')) {
        $path = Join-Path $ProductRoot $relative
        $signature = Get-AuthenticodeSignature -LiteralPath $path
        $signer = $signature.SignerCertificate
        $rawDataSha256 = if ($null -ne $signer) { Get-MwbCertificateRawDataSha256 $signer } else { $null }
        $pendingTrust = $AllowUntrustedTestCertificate -and $null -ne $TestCertificate -and
            $signature.Status -in @('NotTrusted', 'UnknownError')
        if ($null -eq $signer -or ($signature.Status -ne 'Valid' -and -not $pendingTrust) -or
            $signer.GetNameInfo([Security.Cryptography.X509Certificates.X509NameType]::SimpleName, $false) -cne 'Microsoft Corporation' -or
            $signer.Subject -notmatch '(?:^|,\s*)O=Microsoft Corporation(?:,|$)' -or
            ($null -ne $TestCertificate -and $rawDataSha256 -cne $TestCertificate.RawDataSha256)) {
            throw "MWB Release endpoint signature is not the expected Microsoft identity: $relative ($($signature.Status))."
        }
        $version = [Diagnostics.FileVersionInfo]::GetVersionInfo($path)
        $identities += [pscustomobject]@{
            Path = $relative
            Thumbprint = $signer.Thumbprint
            CertificateRawDataSha256 = $rawDataSha256
            Status = $signature.Status.ToString()
            FileVersion = '{0}.{1}.{2}.{3}' -f $version.FileMajorPart, $version.FileMinorPart, $version.FileBuildPart, $version.FilePrivatePart
        }
    }
    $mwb = @($identities | Where-Object Path -CEQ 'PowerToys.MouseWithoutBorders.exe')[0]
    $settings = @($identities | Where-Object Path -CEQ 'WinUI3Apps\PowerToys.Settings.exe')[0]
    $runner = @($identities | Where-Object Path -CEQ 'PowerToys.exe')[0]
    if ($mwb.CertificateRawDataSha256 -cne $settings.CertificateRawDataSha256 -or
        $runner.FileVersion -cne $settings.FileVersion) {
        throw 'MWB/Settings must share one signer and Release Runner/Settings must share one file version.'
    }
    $identities
}
