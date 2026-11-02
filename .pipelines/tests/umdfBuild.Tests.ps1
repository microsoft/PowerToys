# Copyright (c) Microsoft Corporation
# The Microsoft Corporation licenses this file to you under the MIT license.
# See the LICENSE file in the project root for more information.

. (Join-Path $PSScriptRoot '..\umdf\UmdfBuild.Common.ps1')

Describe 'Shared UMDF build infrastructure' {
    BeforeEach {
        $ErrorActionPreference = 'Stop'
        $originalExit = $global:LASTEXITCODE
        $script:signingCertificate = $null
        $work = Join-Path $TestDrive 'display'
        $cache = [IO.Path]::GetFullPath((Join-Path $TestDrive 'umdf-dependencies\test-version'))
        New-Item $cache -ItemType Directory -Force | Out-Null
        'not-executable' | Set-Content (Join-Path $cache 'nuget.exe')
        Mock Invoke-WebRequest { throw 'Unexpected network request.' }
    }

    AfterEach {
        $global:LASTEXITCODE = $originalExit
        if ($script:signingCertificate) { $script:signingCertificate.Dispose() }
    }

    It 'uses the same versioned dependency cache for either fixture without trusting unsigned tools' {
        Mock Get-AuthenticodeSignature { [pscustomobject]@{ Status = 'NotSigned' } }
        foreach ($fixture in @('hid', 'display')) {
            { Initialize-UmdfBuildTools -WorkRoot (Join-Path $TestDrive $fixture) -WdkVersion test-version -Platforms x64 } |
                Should Throw 'not signed by Microsoft'
        }
        Assert-MockCalled Get-AuthenticodeSignature -Times 2 -Exactly -Scope It -ParameterFilter {
            $FilePath -eq (Join-Path $cache 'nuget.exe')
        }
        Assert-MockCalled Invoke-WebRequest -Times 0 -Exactly -Scope It
    }

    It 'fails closed for an explicitly incomplete package cache instead of downloading a fallback' {
        Mock Get-AuthenticodeSignature { [pscustomobject]@{ Status = 'Valid'; SignerCertificate = @{ Subject = 'CN=Microsoft' } } }
        { Initialize-UmdfBuildTools -WorkRoot $work -WdkVersion test-version -Platforms ARM64 -PackageCache (Join-Path $TestDrive 'incomplete') } |
            Should Throw 'cache is incomplete'
        Assert-MockCalled Invoke-WebRequest -Times 0 -Exactly -Scope It
    }

    It 'removes the private signing key after <Case> failure' -TestCases @(
        @{ Case = 'binary signing'; SignExit = 5; CatalogExit = 0 }
        @{ Case = 'catalog generation'; SignExit = 0; CatalogExit = 5 }
    ) {
        param($Case, $SignExit, $CatalogExit)
        $signTool = Join-Path $TestDrive 'sign.ps1'
        $catalogTool = Join-Path $TestDrive 'catalog.ps1'
        'exit 0' | Set-Content $signTool
        'exit 0' | Set-Content $catalogTool
        # The PowerShell host rejects signtool/inf2cat arguments with a real native nonzero exit.
        if ($SignExit) { $signTool = (Get-Process -Id $PID).Path }
        if ($CatalogExit) { $catalogTool = (Get-Process -Id $PID).Path }
        $rsa = [Security.Cryptography.RSA]::Create(2048)
        try {
            $request = [Security.Cryptography.X509Certificates.CertificateRequest]::new(
                'CN=Test', $rsa, [Security.Cryptography.HashAlgorithmName]::SHA256,
                [Security.Cryptography.RSASignaturePadding]::Pkcs1)
            $script:signingCertificate = $request.CreateSelfSigned([DateTimeOffset]::UtcNow.AddMinutes(-1), [DateTimeOffset]::UtcNow.AddDays(1))
        }
        finally { $rsa.Dispose() }
        Mock New-SelfSignedCertificate { $script:signingCertificate }
        Mock Export-Certificate {}
        Mock Remove-Item {}
        $caught = $null
        try {
            Complete-UmdfDriverPackage -DriverDirectory $TestDrive -Platform ARM64 -Subject 'CN=Test' `
                -CertificateName fixture.cer -Binaries fixture.dll -CatalogName fixture.cat `
                -SignTool $signTool -Inf2Cat $catalogTool
        }
        catch { $caught = $_ }
        Assert-MockCalled New-SelfSignedCertificate -Times 1 -Exactly -Scope It
        Assert-MockCalled Export-Certificate -Times 1 -Exactly -Scope It
        Assert-MockCalled Remove-Item -Times 1 -Exactly -Scope It -ParameterFilter {
            $Path -eq "Cert:\CurrentUser\My\$($script:signingCertificate.Thumbprint)" -and $DeleteKey
        }
        $caught | Should Not BeNullOrEmpty
    }

    It 'hashes nested managed probe files relative to the bundle root' {
        $root = Join-Path $TestDrive 'production'
        New-Item (Join-Path $root 'nested') -ItemType Directory -Force | Out-Null
        'probe' | Set-Content (Join-Path $root 'probe.exe')
        'dependency' | Set-Content (Join-Path $root 'nested\dependency.dll')
        $manifest = Join-Path $TestDrive 'manifest.json'
        Write-UmdfFileHashes -Directory $root -ManifestPath $manifest -Relative
        $entries = @(Get-Content $manifest -Raw | ConvertFrom-Json)
        $entries.Count | Should Be 2
        foreach ($entry in $entries) {
            [IO.Path]::IsPathRooted($entry.Path) | Should Be $false
            $entry.Hash | Should Be (Get-FileHash (Join-Path $root $entry.Path) -Algorithm SHA256).Hash
        }
    }
}
