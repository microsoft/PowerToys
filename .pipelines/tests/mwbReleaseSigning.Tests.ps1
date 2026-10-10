# Copyright (c) Microsoft Corporation
# The Microsoft Corporation licenses this file to you under the MIT license.
# See the LICENSE file in the project root for more information.

#Requires -Version 7.0
. "$PSScriptRoot\..\MwbSandboxCi.Common.ps1"
. "$PSScriptRoot\..\..\src\modules\MouseWithoutBorders\Tests\SandboxExperiment\MwbTestSigning.ps1"

function New-MwbPublicCertificateFixture {
    param([string]$Subject = 'CN=Microsoft Corporation, O=Microsoft Corporation, L=Redmond, S=Washington, C=US')
    $key = [Security.Cryptography.RSA]::Create(2048)
    try {
        $request = [Security.Cryptography.X509Certificates.CertificateRequest]::new(
            $Subject, $key, [Security.Cryptography.HashAlgorithmName]::SHA256, [Security.Cryptography.RSASignaturePadding]::Pkcs1)
        $usage = [Security.Cryptography.OidCollection]::new()
        $null = $usage.Add([Security.Cryptography.Oid]::new('1.3.6.1.5.5.7.3.3'))
        $request.CertificateExtensions.Add([Security.Cryptography.X509Certificates.X509EnhancedKeyUsageExtension]::new($usage, $false))
        $private = $request.CreateSelfSigned([DateTimeOffset]::UtcNow.AddMinutes(-1), [DateTimeOffset]::UtcNow.AddHours(2))
        try {
            [Security.Cryptography.X509Certificates.X509Certificate2]::new(
                $private.Export([Security.Cryptography.X509Certificates.X509ContentType]::Cert))
        }
        finally { $private.Dispose() }
    }
    finally { $key.Dispose() }
}

Describe 'MWB public test certificate and exact trust ownership' {
    BeforeAll { $script:certificate = New-MwbPublicCertificateFixture }
    AfterAll { $script:certificate.Dispose() }
    BeforeEach {
        $certificatePath = Join-Path $TestDrive ([guid]::NewGuid().ToString('N') + '.cer')
        [IO.File]::WriteAllBytes($certificatePath, $script:certificate.RawData)
        $script:stores = @{}
        Mock Test-Path {
            param([string]$LiteralPath)
            if ($LiteralPath -like 'Cert:*') { return $script:stores.ContainsKey($LiteralPath) }
            Microsoft.PowerShell.Management\Test-Path -LiteralPath $LiteralPath
        }
        Mock Get-Item {
            param([string]$LiteralPath)
            if ($LiteralPath -like 'Cert:*') { return $script:stores[$LiteralPath] }
            Microsoft.PowerShell.Management\Get-Item -LiteralPath $LiteralPath
        }
        Mock Import-Certificate {
            param($CertStoreLocation)
            $script:stores[(Join-Path $CertStoreLocation $script:certificate.Thumbprint)] = $script:certificate
        }
        Mock Remove-Item {
            param([string]$LiteralPath)
            if ($LiteralPath -notlike 'Cert:*') { throw 'Unexpected deletion outside a mocked certificate store.' }
            $script:stores.Remove($LiteralPath)
        }
    }

    It 'accepts only an approved public Microsoft-subject code-signing certificate' {
        $result = Get-MwbTestSigningCertificate $certificatePath
        $result.Thumbprint | Should Be $script:certificate.Thumbprint
        $result.RawDataSha256 | Should Be (Get-MwbCertificateRawDataSha256 $script:certificate)
        $result.Sha256 | Should Be (Get-FileHash -LiteralPath $certificatePath).Hash
        $script:certificate.HasPrivateKey | Should Be $false
    }

    It 'rejects a PKCS12 container even when it contains no private key' {
        [IO.File]::WriteAllBytes($certificatePath,
            $script:certificate.Export([Security.Cryptography.X509Certificates.X509ContentType]::Pkcs12))
        { Get-MwbTestSigningCertificate $certificatePath } | Should Throw 'only a public certificate'
        Assert-MockCalled Import-Certificate -Times 0 -Exactly -Scope It
    }

    It 'does not trust an arbitrary publisher' {
        $other = New-MwbPublicCertificateFixture 'CN=Not the approved publisher'
        try {
            [IO.File]::WriteAllBytes($certificatePath, $other.RawData)
            { Get-MwbTestSigningCertificate $certificatePath } | Should Throw 'Microsoft-subject'
        }
        finally { $other.Dispose() }
    }

    It 'journals only absent stores, then imports and removes those exact entries' {
        $existing = Join-Path 'Cert:\LocalMachine\Root' $script:certificate.Thumbprint
        $script:stores[$existing] = $script:certificate
        $trust = Get-MwbTestSigningTrust $certificatePath
        @($trust.AddedStores).Count | Should Be 1
        $trust.AddedStores[0] | Should Be 'Cert:\LocalMachine\TrustedPeople'
        Install-MwbTestSigningTrust $trust
        Remove-MwbTestSigningTrust $trust
        $script:stores.ContainsKey($existing) | Should Be $true
        $script:stores.Count | Should Be 1
        Assert-MockCalled Import-Certificate -Times 1 -Exactly -Scope It
        Assert-MockCalled Remove-Item -Times 1 -Exactly -Scope It
    }

    It 'rejects changed public certificate bytes before importing or removing trust' {
        $trust = Get-MwbTestSigningTrust $certificatePath
        $trust.Sha256 = '0' * 64
        { Install-MwbTestSigningTrust $trust } | Should Throw 'changed'
        { Remove-MwbTestSigningTrust $trust } | Should Throw 'changed'
        Assert-MockCalled Import-Certificate -Times 0 -Exactly -Scope It
        Assert-MockCalled Remove-Item -Times 0 -Exactly -Scope It
    }

    It 'rejects an injected, duplicate or private-key store' -TestCases @(
        @{ Stores = @('Cert:\CurrentUser\My') }
        @{ Stores = @('Cert:\LocalMachine\Root', 'Cert:\LocalMachine\Root') }
        @{ Stores = @('C:\unrelated') }
    ) {
        param($Stores)
        $trust = Get-MwbTestSigningTrust $certificatePath
        $trust.AddedStores = $Stores
        { Install-MwbTestSigningTrust $trust } | Should Throw 'inventory changed'
        { Remove-MwbTestSigningTrust $trust } | Should Throw 'inventory changed'
        Assert-MockCalled Import-Certificate -Times 0 -Exactly -Scope It
        Assert-MockCalled Remove-Item -Times 0 -Exactly -Scope It
    }
}

Describe 'MWB immutable Release runtime signing lineage' {
    BeforeAll { $script:signer = New-MwbPublicCertificateFixture }
    AfterAll { $script:signer.Dispose() }
    BeforeEach {
        $root = Join-Path $TestDrive ([guid]::NewGuid().ToString('N'))
        $bundleRoot = Join-Path $root 'bundle'
        $original = Join-Path $root 'original'
        $product = Join-Path $root 'signed'
        $archive = Join-Path $root 'signed.zip'
        $null = New-Item -ItemType Directory -Path $bundleRoot
        $names = @('PowerToys.exe', 'PowerToys.MouseWithoutBorders.exe', 'PowerToys.MouseWithoutBorders.dll',
            'PowerToys.MouseWithoutBordersHelper.exe', 'WinUI3Apps\PowerToys.Settings.exe', 'WinUI3Apps\PowerToys.QuickAccess.exe')
        $files = @(
            foreach ($name in $names) {
                $path = Join-Path $original $name
                $null = New-Item -ItemType Directory -Path (Split-Path $path) -Force
                Set-Content -LiteralPath $path -Value "Nonexecutable original fixture $name"
                @{ Path = $name; Sha256 = (Get-FileHash -LiteralPath $path).Hash }
            }
        )
        $inputArchive = Join-Path $bundleRoot 'runtime.zip'
        [IO.Compression.ZipFile]::CreateFromDirectory($original, $inputArchive)
        $originalArchiveHash = (Get-FileHash -LiteralPath $inputArchive).Hash
        $bundle = [pscustomobject]@{
            Root = $bundleRoot; ManifestSha256 = 'a' * 64
            Manifest = @{ Runtime = @{ Files = $files; Sha256 = $originalArchiveHash } }
        }
        @{
            ArchivePath = $inputArchive; ProductRoot = $original; Sha256 = $originalArchiveHash
            Length = (Get-Item $inputArchive).Length; FileCount = $files.Count; Files = $names; Configuration = 'Release'
            ReadyToRun = @{ ManagedILAndAttributesUnchanged = $true; Configuration = 'Release' }
        } | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath "$inputArchive.manifest.json"
        Mock Get-AuthenticodeSignature { [pscustomobject]@{ Status = 'Valid'; SignerCertificate = $script:signer } }
        Mock Invoke-MwbCiEndpointSigning {
            param($ProductRoot)
            foreach ($file in Get-ChildItem -LiteralPath $ProductRoot -Filter '*.exe' -File -Recurse) {
                Add-Content -LiteralPath $file.FullName -Value 'Test signature fixture'
            }
            [IO.File]::WriteAllBytes((Join-Path $ProductRoot 'mwb-test-signer.cer'), $script:signer.RawData)
        }
    }

    It 'changes only executable signatures, leaves managed files and the input archive untouched, and stages one cohort' {
        $result = New-MwbCiSignedRuntime $bundle $product $archive (Join-Path $root 'marker.txt') ([guid]::NewGuid())
        $result.Files.Count | Should Be 7
        $result.RuntimeManifest.Configuration | Should Be 'Release'
        $result.RuntimeManifest.TestSigning.ExecutableChanges.Count | Should Be 5
        $result.RuntimeManifest.TestSigning.ManagedFilesUnchanged | Should Be $true
        $result.RuntimeManifest.TestSigning.InputRuntimeSha256 | Should Be $originalArchiveHash
        (Get-FileHash -LiteralPath $inputArchive).Hash | Should Be $originalArchiveHash
        (Get-FileHash -LiteralPath (Join-Path $product 'PowerToys.MouseWithoutBorders.dll')).Hash |
            Should Be (Get-FileHash -LiteralPath (Join-Path $original 'PowerToys.MouseWithoutBorders.dll')).Hash
        $guest = Join-Path $root 'guest'
        Expand-MwbCiRuntime $archive $guest $result.Files
        foreach ($entry in $result.Files) {
            (Get-FileHash -LiteralPath (Join-Path $guest $entry.Path)).Hash |
                Should Be (Get-FileHash -LiteralPath (Join-Path $product $entry.Path)).Hash
        }
    }

    It 'rejects signing that changes any managed dependency instead of manufacturing matching provenance' {
        Mock Invoke-MwbCiEndpointSigning {
            param($ProductRoot)
            Add-Content -LiteralPath (Join-Path $ProductRoot 'PowerToys.MouseWithoutBorders.dll') -Value 'Changed IL'
        }
        { New-MwbCiSignedRuntime $bundle $product $archive (Join-Path $root 'marker.txt') ([guid]::NewGuid()) } |
            Should Throw 'non-executable runtime dependency'
        Test-Path -LiteralPath $archive | Should Be $false
    }

    It 'rejects unmanifested signer outputs' {
        Mock Invoke-MwbCiEndpointSigning {
            param($ProductRoot)
            Set-Content -LiteralPath (Join-Path $ProductRoot 'unexpected.json') -Value '{}'
        }
        { New-MwbCiSignedRuntime $bundle $product $archive (Join-Path $root 'marker.txt') ([guid]::NewGuid()) } |
            Should Throw 'unmanifested runtime files'
        Test-Path -LiteralPath $archive | Should Be $false
    }

    It 'requires trusted signatures before endpoint startup and rejects a hash mismatch even with pending test trust' {
        $certificatePath = Join-Path $root 'approved.cer'
        [IO.File]::WriteAllBytes($certificatePath, $script:signer.RawData)
        $metadata = Get-MwbTestSigningCertificate $certificatePath
        Mock Get-AuthenticodeSignature { [pscustomobject]@{ Status = 'NotTrusted'; SignerCertificate = $script:signer } }
        { Assert-MwbEndpointSignatures $original $metadata } | Should Throw 'expected Microsoft identity'
        @(Assert-MwbEndpointSignatures $original $metadata -AllowUntrustedTestCertificate).Count | Should Be 5
        Mock Get-AuthenticodeSignature { [pscustomobject]@{ Status = 'HashMismatch'; SignerCertificate = $script:signer } }
        { Assert-MwbEndpointSignatures $original $metadata -AllowUntrustedTestCertificate } |
            Should Throw 'expected Microsoft identity'
    }

    It 'rejects another signer even when its publisher subject is identical' {
        $certificatePath = Join-Path $root 'approved.cer'
        [IO.File]::WriteAllBytes($certificatePath, $script:signer.RawData)
        $metadata = Get-MwbTestSigningCertificate $certificatePath
        $other = New-MwbPublicCertificateFixture
        try {
            Mock Get-AuthenticodeSignature { [pscustomobject]@{ Status = 'Valid'; SignerCertificate = $other } }
            { Assert-MwbEndpointSignatures $original $metadata } | Should Throw 'expected Microsoft identity'
        }
        finally { $other.Dispose() }
    }
}
