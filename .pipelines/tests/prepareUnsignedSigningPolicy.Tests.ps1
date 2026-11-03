# Copyright (c) Microsoft Corporation
# The Microsoft Corporation licenses this file to you under the MIT license.

$prepareSigningPolicyScript = Join-Path $PSScriptRoot '..\prepareUnsignedSigningPolicy.ps1'

function New-SigningPolicyTestPe {
    param(
        [string] $Path,
        [switch] $Pe32Plus,
        [switch] $HasCertificate,
        [switch] $HasStrongName
    )

    # A small PE fixture with a section and optional certificate/CLR directories.
    # Its certificate deliberately contains invalid data: preserving an existing
    # signature must not depend on WinTrust accepting its contents or publisher.
    $bytes = New-Object byte[] 1040
    [System.Text.Encoding]::ASCII.GetBytes('MZ').CopyTo($bytes, 0)
    [BitConverter]::GetBytes([uint32] 128).CopyTo($bytes, 60)
    [System.Text.Encoding]::ASCII.GetBytes("PE`0`0").CopyTo($bytes, 128)
    $optionalHeaderSize = 224
    $machine = 0x14c
    $magic = 0x10b
    $directoryOffset = 248
    if ($Pe32Plus) {
        $optionalHeaderSize = 240
        $machine = 0x8664
        $magic = 0x20b
        $directoryOffset = 264
    }

    [BitConverter]::GetBytes([uint16] $machine).CopyTo($bytes, 132)
    [BitConverter]::GetBytes([uint16] 1).CopyTo($bytes, 134)
    [BitConverter]::GetBytes([uint16] $optionalHeaderSize).CopyTo($bytes, 148)
    [BitConverter]::GetBytes([uint16] 0x2022).CopyTo($bytes, 150)
    [BitConverter]::GetBytes([uint16] $magic).CopyTo($bytes, 152)
    [BitConverter]::GetBytes([uint32] 4096).CopyTo($bytes, 184)
    [BitConverter]::GetBytes([uint32] 512).CopyTo($bytes, 188)
    [BitConverter]::GetBytes([uint32] 8192).CopyTo($bytes, 208)
    [BitConverter]::GetBytes([uint32] 512).CopyTo($bytes, 212)
    [BitConverter]::GetBytes([uint16] 3).CopyTo($bytes, 220)
    [BitConverter]::GetBytes([uint32] 16).CopyTo($bytes, $directoryOffset - 4)

    $sectionOffset = 152 + $optionalHeaderSize
    [System.Text.Encoding]::ASCII.GetBytes('.text').CopyTo($bytes, $sectionOffset)
    [BitConverter]::GetBytes([uint32] 512).CopyTo($bytes, $sectionOffset + 8)
    [BitConverter]::GetBytes([uint32] 4096).CopyTo($bytes, $sectionOffset + 12)
    [BitConverter]::GetBytes([uint32] 512).CopyTo($bytes, $sectionOffset + 16)
    [BitConverter]::GetBytes([uint32] 512).CopyTo($bytes, $sectionOffset + 20)
    [BitConverter]::GetBytes([uint32] 0x60000020).CopyTo($bytes, $sectionOffset + 36)

    if ($HasCertificate) {
        [BitConverter]::GetBytes([uint32] 1024).CopyTo($bytes, $directoryOffset + 32)
        [BitConverter]::GetBytes([uint32] 16).CopyTo($bytes, $directoryOffset + 36)
        [BitConverter]::GetBytes([uint32] 16).CopyTo($bytes, 1024)
        [BitConverter]::GetBytes([uint16] 0x200).CopyTo($bytes, 1028)
        [BitConverter]::GetBytes([uint16] 2).CopyTo($bytes, 1030)
        [System.Text.Encoding]::ASCII.GetBytes('not-a-p7').CopyTo($bytes, 1032)
    }

    if ($HasStrongName) {
        [BitConverter]::GetBytes([uint32] 4096).CopyTo($bytes, $directoryOffset + 112)
        [BitConverter]::GetBytes([uint32] 72).CopyTo($bytes, $directoryOffset + 116)
        [BitConverter]::GetBytes([uint32] 72).CopyTo($bytes, 512)
        [BitConverter]::GetBytes([uint32] 9).CopyTo($bytes, 528)
        [BitConverter]::GetBytes([uint32] 4352).CopyTo($bytes, 544)
        [BitConverter]::GetBytes([uint32] 128).CopyTo($bytes, 548)
        $bytes[768] = 1
    }

    $null = New-Item -ItemType Directory -Path (Split-Path -Parent $Path) -Force
    [System.IO.File]::WriteAllBytes($Path, $bytes)
}

function New-SigningPolicyTestBatch {
    param(
        [string[]] $Patterns,
        [string] $KeyCode = 'CP-231522',
        [switch] $StrongName
    )

    $operations = @()
    if ($StrongName) {
        foreach ($operation in @('StrongNameSign', 'StrongNameVerify')) {
            $operations += @{
                KeyCode = 'CP-233904-SN'
                OperationSetCode = $operation
                Parameters = @()
                ToolName = 'sign'
                ToolVersion = '1.0'
            }
        }
    }

    foreach ($operation in @('SigntoolSign', 'SigntoolVerify')) {
        $operations += @{
            KeyCode = $KeyCode
            OperationSetCode = $operation
            Parameters = @()
            ToolName = 'sign'
            ToolVersion = '1.0'
        }
    }

    @{
        MatchedPath = @($Patterns)
        SigningInfo = @{ Operations = $operations }
    }
}

function Write-SigningPolicyTestInput {
    param(
        [string] $Path,
        [object[]] $Batches
    )

    @{
        Version = '1.0.0'
        UseMinimatch = $false
        SignBatches = @($Batches)
    } | ConvertTo-Json -Depth 20 | Set-Content -LiteralPath $Path -Encoding UTF8
}

Describe 'Unsigned release signing selection' {
    BeforeEach {
        $caseRoot = Join-Path $TestDrive ([guid]::NewGuid().ToString())
        $payloadRoot = Join-Path $caseRoot 'payload'
        $null = New-Item -ItemType Directory -Path $payloadRoot -Force
        $inputPolicyPath = Join-Path $caseRoot 'input.json'
        $outputPolicyPath = Join-Path $caseRoot 'output.json'

        Mock Write-Host { }
        Mock Get-AuthenticodeSignature {
            [pscustomobject]@{ SignerCertificate = $null; Status = 'NotSigned'; SignatureType = 'None' }
        }
    }

    It 'preserves an existing embedded signature even when its certificate cannot be validated (<Format>)' -TestCases @(
        @{ Format = 'PE32'; Pe32Plus = $false },
        @{ Format = 'PE32+'; Pe32Plus = $true }
    ) {
        param($Format, $Pe32Plus)

        New-SigningPolicyTestPe -Path (Join-Path $payloadRoot 'upstream.dll') -Pe32Plus:$Pe32Plus -HasCertificate
        New-SigningPolicyTestPe -Path (Join-Path $payloadRoot 'product.winmd') -Pe32Plus:$Pe32Plus
        Write-SigningPolicyTestInput -Path $inputPolicyPath -Batches @(
            New-SigningPolicyTestBatch -Patterns @('*.dll', '*.winmd')
        )

        & $prepareSigningPolicyScript -FolderPath $payloadRoot -SigningPolicyPath $inputPolicyPath -OutputPath $outputPolicyPath
        $policy = Get-Content -LiteralPath $outputPolicyPath -Raw | ConvertFrom-Json

        @($policy.SignBatches).Count | Should Be 1
        @($policy.SignBatches[0].MatchedPath).Count | Should Be 1
        $policy.SignBatches[0].MatchedPath[0] | Should Be 'product.winmd'
        Assert-MockCalled Write-Host -Times 1 -Exactly -Scope It -ParameterFilter {
            $Object -eq '##vso[task.setvariable variable=EsrpHasUnsignedSigningFiles]true'
        }
    }

    It 'excludes the signed copy of a recursively matched filename and keeps its unsigned copy' {
        New-SigningPolicyTestPe -Path (Join-Path $payloadRoot 'signed\Shared.dll') -HasCertificate
        New-SigningPolicyTestPe -Path (Join-Path $payloadRoot 'unsigned\Shared.dll')
        Write-SigningPolicyTestInput -Path $inputPolicyPath -Batches @(
            New-SigningPolicyTestBatch -Patterns @('Shared.dll', 'Shared.dll')
        )

        & $prepareSigningPolicyScript -FolderPath $payloadRoot -SigningPolicyPath $inputPolicyPath -OutputPath $outputPolicyPath
        $policy = Get-Content -LiteralPath $outputPolicyPath -Raw | ConvertFrom-Json

        @($policy.SignBatches[0].MatchedPath).Count | Should Be 1
        $policy.SignBatches[0].MatchedPath[0] | Should Be 'unsigned/Shared.dll'
        $policy.UseMinimatch | Should Be $true
    }

    It 'does not let a selected root filename match a signed nested copy again' {
        New-SigningPolicyTestPe -Path (Join-Path $payloadRoot 'Shared.dll')
        New-SigningPolicyTestPe -Path (Join-Path $payloadRoot 'nested\Shared.dll') -HasCertificate
        Write-SigningPolicyTestInput -Path $inputPolicyPath -Batches @(
            New-SigningPolicyTestBatch -Patterns @('Shared.dll')
        )

        & $prepareSigningPolicyScript -FolderPath $payloadRoot -SigningPolicyPath $inputPolicyPath -OutputPath $outputPolicyPath
        $policy = Get-Content -LiteralPath $outputPolicyPath -Raw | ConvertFrom-Json

        @($policy.SignBatches[0].MatchedPath).Count | Should Be 1
        $policy.SignBatches[0].MatchedPath[0] | Should Be 'Shared.dll'
        $policy.UseMinimatch | Should Be $true
    }

    It 'keeps relative path candidates within their original directory (<Separator>)' -TestCases @(
        @{ Separator = '\' },
        @{ Separator = '/' }
    ) {
        param($Separator)

        New-SigningPolicyTestPe -Path (Join-Path $payloadRoot 'selected\Dependency.dll')
        New-SigningPolicyTestPe -Path (Join-Path $payloadRoot 'other\Dependency.dll')
        New-SigningPolicyTestPe -Path (Join-Path $payloadRoot 'unlisted.winmd')
        Write-SigningPolicyTestInput -Path $inputPolicyPath -Batches @(
            New-SigningPolicyTestBatch -Patterns @("selected${Separator}*.dll")
        )

        & $prepareSigningPolicyScript -FolderPath $payloadRoot -SigningPolicyPath $inputPolicyPath -OutputPath $outputPolicyPath
        $policy = Get-Content -LiteralPath $outputPolicyPath -Raw | ConvertFrom-Json

        @($policy.SignBatches[0].MatchedPath).Count | Should Be 1
        $policy.SignBatches[0].MatchedPath[0] | Should Be 'selected/Dependency.dll'
    }

    It 'preserves an untrusted upstream script signature without applying publisher rules' {
        Set-Content -LiteralPath (Join-Path $payloadRoot 'upstream.ps1') -Value '# Existing upstream script'
        Set-Content -LiteralPath (Join-Path $payloadRoot 'unsigned.ps1') -Value '# Unsigned product script'
        Mock Get-AuthenticodeSignature {
            [pscustomobject]@{
                SignerCertificate = [pscustomobject]@{ Subject = 'CN=Untrusted upstream publisher' }
                Status = 'NotTrusted'
                SignatureType = 'Authenticode'
            }
        } -ParameterFilter { ($FilePath -like '*upstream.ps1') -or ($LiteralPath -like '*upstream.ps1') }
        Write-SigningPolicyTestInput -Path $inputPolicyPath -Batches @(
            New-SigningPolicyTestBatch -Patterns @('*.ps1')
        )

        & $prepareSigningPolicyScript -FolderPath $payloadRoot -SigningPolicyPath $inputPolicyPath -OutputPath $outputPolicyPath
        $policy = Get-Content -LiteralPath $outputPolicyPath -Raw | ConvertFrom-Json

        @($policy.SignBatches[0].MatchedPath).Count | Should Be 1
        $policy.SignBatches[0].MatchedPath[0] | Should Be 'unsigned.ps1'
    }

    It 'retains SDK strong-name signing and Authenticode operations for a strong-name-only assembly' {
        New-SigningPolicyTestPe -Path (Join-Path $payloadRoot 'Toolkit.dll') -HasStrongName
        Write-SigningPolicyTestInput -Path $inputPolicyPath -Batches @(
            New-SigningPolicyTestBatch -Patterns @('Toolkit.dll') -KeyCode 'CP-230012' -StrongName
        )

        & $prepareSigningPolicyScript -FolderPath $payloadRoot -SigningPolicyPath $inputPolicyPath -OutputPath $outputPolicyPath
        $policy = Get-Content -LiteralPath $outputPolicyPath -Raw | ConvertFrom-Json

        $policy.SignBatches[0].MatchedPath[0] | Should Be 'Toolkit.dll'
        ($policy.SignBatches[0].SigningInfo.Operations.OperationSetCode -join ',') | Should Be 'StrongNameSign,StrongNameVerify,SigntoolSign,SigntoolVerify'
        $policy.SignBatches[0].SigningInfo.Operations[0].KeyCode | Should Be 'CP-233904-SN'
        $policy.SignBatches[0].SigningInfo.Operations[2].KeyCode | Should Be 'CP-230012'
    }

    It 'outputs an empty policy and disables signing when all candidates already have signatures' {
        New-SigningPolicyTestPe -Path (Join-Path $payloadRoot 'upstream.dll') -HasCertificate
        Write-SigningPolicyTestInput -Path $inputPolicyPath -Batches @(
            New-SigningPolicyTestBatch -Patterns @('*.dll')
        )

        & $prepareSigningPolicyScript -FolderPath $payloadRoot -SigningPolicyPath $inputPolicyPath -OutputPath $outputPolicyPath
        $policy = Get-Content -LiteralPath $outputPolicyPath -Raw | ConvertFrom-Json

        @($policy.SignBatches).Count | Should Be 0
        Assert-MockCalled Write-Host -Times 1 -Exactly -Scope It -ParameterFilter {
            $Object -eq '##vso[task.setvariable variable=EsrpHasUnsignedSigningFiles]false'
        }
    }

    It 'does not schedule the same unsigned file through two signing batches' {
        New-SigningPolicyTestPe -Path (Join-Path $payloadRoot 'Shared.dll')
        Write-SigningPolicyTestInput -Path $inputPolicyPath -Batches @(
            New-SigningPolicyTestBatch -Patterns @('*.dll') -KeyCode 'CP-230012'
            New-SigningPolicyTestBatch -Patterns @('Shared.dll') -KeyCode 'CP-231522'
        )

        { & $prepareSigningPolicyScript -FolderPath $payloadRoot -SigningPolicyPath $inputPolicyPath -OutputPath $outputPolicyPath } | Should Throw 'File matches more than one signing batch'
    }

    It 'leaves both signed and unsigned payload bytes unchanged while preparing the policy' {
        $signedPath = Join-Path $payloadRoot 'upstream.dll'
        $unsignedPath = Join-Path $payloadRoot 'product.dll'
        New-SigningPolicyTestPe -Path $signedPath -HasCertificate
        New-SigningPolicyTestPe -Path $unsignedPath
        $signedHash = (Get-FileHash -LiteralPath $signedPath -Algorithm SHA256).Hash
        $unsignedHash = (Get-FileHash -LiteralPath $unsignedPath -Algorithm SHA256).Hash
        Write-SigningPolicyTestInput -Path $inputPolicyPath -Batches @(
            New-SigningPolicyTestBatch -Patterns @('*.dll')
        )

        & $prepareSigningPolicyScript -FolderPath $payloadRoot -SigningPolicyPath $inputPolicyPath -OutputPath $outputPolicyPath

        (Get-FileHash -LiteralPath $signedPath -Algorithm SHA256).Hash | Should Be $signedHash
        (Get-FileHash -LiteralPath $unsignedPath -Algorithm SHA256).Hash | Should Be $unsignedHash
    }
}
