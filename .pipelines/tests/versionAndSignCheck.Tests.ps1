# Copyright (c) Microsoft Corporation
# The Microsoft Corporation licenses this file to you under the MIT license.

$scriptPath = Join-Path $PSScriptRoot '..\versionAndSignCheck.ps1'

function New-VersionedPayload {
    param(
        [string]$Name,
        [AllowNull()][object]$FileVersion = '2.3.4.5'
    )

    [pscustomobject]@{
        Name = $Name
        FullName = Join-Path $TestDrive $Name
        Extension = [System.IO.Path]::GetExtension($Name)
        VersionInfo = [pscustomobject]@{ FileVersion = $FileVersion }
    }
}

Describe 'versionAndSignCheck metadata discovery and signatures' {
    BeforeEach {
        $targetDir = Join-Path $TestDrive ([guid]::NewGuid().ToString('N'))
        New-Item -ItemType Directory -Path $targetDir | Out-Null
        Mock Write-Host {}
        Mock Write-Error {}
        Mock Get-AuthenticodeSignature {
            [pscustomobject]@{
                Status = 'Valid'
                SignerCertificate = [pscustomobject]@{ Subject = 'CN=Third-party publisher' }
            }
        }
    }

    It 'accepts signed metadata without a FileVersion: <Name>' -TestCases @(
        @{ Name = 'PowerToys.Interop.winmd' }
        @{ Name = 'WinUI3ApplicationsFiles_File_SkiaSharp.Views.WinUI.Native.winmd' }
        @{ Name = 'Microsoft.CommandPalette.Extensions.WINMD' }
    ) {
        param($Name)
        $file = New-Item -ItemType File -Path (Join-Path $targetDir $Name)
        $file.VersionInfo.FileVersion | Should BeNullOrEmpty

        # Use the call operator: exit returns from the audited script and sets LASTEXITCODE.
        & $scriptPath -targetDir $targetDir

        $LASTEXITCODE | Should Be 0
        Assert-MockCalled Get-AuthenticodeSignature -Times 1 -Exactly -Scope It -ParameterFilter { $LiteralPath -eq $file.FullName }
    }

    It 'rejects metadata with signature status <Status> and certificate present <HasCertificate>' -TestCases @(
        @{ Status = 'NotSigned'; HasCertificate = $false }
        @{ Status = 'HashMismatch'; HasCertificate = $true }
        @{ Status = 'NotTrusted'; HasCertificate = $true }
        @{ Status = 'UnknownError'; HasCertificate = $true }
        @{ Status = 'NotSupportedFileFormat'; HasCertificate = $false }
        @{ Status = 'Valid'; HasCertificate = $false }
    ) {
        param($Status, $HasCertificate)
        New-Item -ItemType File -Path (Join-Path $targetDir 'PowerToys.Interop.winmd') | Out-Null
        $signature = [pscustomobject]@{
            Status = $Status
            SignerCertificate = if ($HasCertificate) { [pscustomobject]@{ Subject = 'CN=Publisher' } } else { $null }
        }
        Mock Get-AuthenticodeSignature { $signature }

        & $scriptPath -targetDir $targetDir

        $LASTEXITCODE | Should Be 1
        Assert-MockCalled Get-AuthenticodeSignature -Times 1 -Exactly -Scope It
        Assert-MockCalled Write-Host -Scope It -ParameterFilter { $Object -like 'Invalid signature*' }
    }

    It 'fails an empty payload instead of passing without checking anything' {
        & $scriptPath -targetDir $targetDir

        $LASTEXITCODE | Should Be 1
        Assert-MockCalled Get-AuthenticodeSignature -Times 0 -Exactly -Scope It
    }

    It 'checks nested metadata in WinmdOnly mode while ignoring an unsigned unversioned DLL' {
        $nestedDir = Join-Path $targetDir 'Extension'
        New-Item -ItemType Directory -Path $nestedDir | Out-Null
        $file = New-Item -ItemType File -Path (Join-Path $nestedDir 'Microsoft.CommandPalette.Extensions.winmd')
        $dll = New-Item -ItemType File -Path (Join-Path $targetDir 'Unversioned.dll')
        Mock Get-AuthenticodeSignature {
            [pscustomobject]@{ Status = 'NotSigned'; SignerCertificate = $null }
        } -ParameterFilter { $LiteralPath -eq $dll.FullName }

        & $scriptPath -targetDir $targetDir -WinmdOnly

        $LASTEXITCODE | Should Be 0
        Assert-MockCalled Get-AuthenticodeSignature -Times 1 -Exactly -Scope It -ParameterFilter { $LiteralPath -eq $file.FullName }
        Assert-MockCalled Get-AuthenticodeSignature -Times 0 -Exactly -Scope It -ParameterFilter { $LiteralPath -eq $dll.FullName }
    }

    It 'rejects unsigned nested metadata in WinmdOnly mode' {
        $nestedDir = Join-Path $targetDir 'Extension'
        New-Item -ItemType Directory -Path $nestedDir | Out-Null
        New-Item -ItemType File -Path (Join-Path $nestedDir 'Microsoft.CommandPalette.Extensions.winmd') | Out-Null
        Mock Get-AuthenticodeSignature {
            [pscustomobject]@{ Status = 'NotSigned'; SignerCertificate = $null }
        }

        & $scriptPath -targetDir $targetDir -WinmdOnly

        $LASTEXITCODE | Should Be 1
        Assert-MockCalled Get-AuthenticodeSignature -Times 1 -Exactly -Scope It
    }

    It 'fails WinmdOnly mode when only a DLL is present' {
        New-Item -ItemType File -Path (Join-Path $targetDir 'Unversioned.dll') | Out-Null

        & $scriptPath -targetDir $targetDir -WinmdOnly

        $LASTEXITCODE | Should Be 1
        Assert-MockCalled Get-AuthenticodeSignature -Times 0 -Exactly -Scope It
    }
}

Describe 'versionAndSignCheck existing binary version policy' {
    BeforeEach {
        $fixture = @{ Files = @(); TargetDir = $TestDrive }
        Mock Get-ChildItem { $fixture.Files } -ParameterFilter { $Path -eq $fixture.TargetDir }
        Mock Write-Host {}
        Mock Write-Error {}
        Mock Get-AuthenticodeSignature {
            [pscustomobject]@{
                Status = 'Valid'
                SignerCertificate = [pscustomobject]@{ Subject = 'CN=Third-party publisher' }
            }
        }
    }

    It 'accepts a valid signed and versioned <Name>' -TestCases @(
        @{ Name = 'PowerToys.exe' }
        @{ Name = 'OpenAI.dll' }
        @{ Name = 'PTCustomActions' }
    ) {
        param($Name)
        $fixture.Files = @(New-VersionedPayload -Name $Name)

        & $scriptPath -targetDir $fixture.TargetDir

        $LASTEXITCODE | Should Be 0
        Assert-MockCalled Get-AuthenticodeSignature -Times 1 -Exactly -Scope It
    }

    It 'rejects missing FileVersion for <Name> and still validates its signature' -TestCases @(
        @{ Name = 'PowerToys.exe' }
        @{ Name = 'PowerToys.Interop.dll' }
    ) {
        param($Name)
        $fixture.Files = @(New-VersionedPayload -Name $Name -FileVersion $null)
        Mock Get-AuthenticodeSignature {
            [pscustomobject]@{ Status = 'HashMismatch'; SignerCertificate = [pscustomobject]@{ Subject = 'CN=Publisher' } }
        }

        & $scriptPath -targetDir $fixture.TargetDir

        $LASTEXITCODE | Should Be 1
        Assert-MockCalled Get-AuthenticodeSignature -Times 1 -Exactly -Scope It
        Assert-MockCalled Write-Host -Scope It -ParameterFilter { $Object -contains 'Version not set: ' }
        Assert-MockCalled Write-Host -Scope It -ParameterFilter { $Object -like 'Invalid signature*' }
    }

    It 'rejects reserved FileVersion <Version> and still validates the signature' -TestCases @(
        @{ Version = '0.0.0.0' }
        @{ Version = '1.0.0.0' }
    ) {
        param($Version)
        $fixture.Files = @(New-VersionedPayload -Name 'PowerToys.exe' -FileVersion $Version)

        & $scriptPath -targetDir $fixture.TargetDir

        $LASTEXITCODE | Should Be 1
        Assert-MockCalled Get-AuthenticodeSignature -Times 1 -Exactly -Scope It
    }

    It 'preserves the version exception for <Name> at <Version>' -TestCases @(
        @{ Name = 'AdaptiveCards.Templating.dll'; Version = '0.0.0.0' }
        @{ Name = 'AdaptiveCards.Templating.dll'; Version = '1.0.0.0' }
        @{ Name = 'BaseApplicationsFiles_File_SkiaSharp.Views.WinUI.Native.dll'; Version = $null }
        @{ Name = 'codicon.ttf'; Version = $null }
    ) {
        param($Name, $Version)
        $fixture.Files = @(New-VersionedPayload -Name $Name -FileVersion $Version)

        & $scriptPath -targetDir $fixture.TargetDir

        $LASTEXITCODE | Should Be 0
        Assert-MockCalled Get-AuthenticodeSignature -Times 1 -Exactly -Scope It
    }

    It 'does not let a version exception bypass the signature requirement' {
        $fixture.Files = @(New-VersionedPayload -Name 'libSkiaSharp.dll' -FileVersion $null)
        Mock Get-AuthenticodeSignature {
            [pscustomobject]@{ Status = 'NotSigned'; SignerCertificate = $null }
        }

        & $scriptPath -targetDir $fixture.TargetDir

        $LASTEXITCODE | Should Be 1
        Assert-MockCalled Get-AuthenticodeSignature -Times 1 -Exactly -Scope It
    }
}
