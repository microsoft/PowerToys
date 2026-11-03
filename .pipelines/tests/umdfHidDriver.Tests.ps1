# Copyright (c) Microsoft Corporation
# The Microsoft Corporation licenses this file to you under the MIT license.
# See the LICENSE file in the project root for more information.

$sourcePath = Join-Path $PSScriptRoot '..\umdf-hid-smoke\Manage-SmokeDriver.ps1'
$tokens = $null
$parseErrors = $null
$ast = [Management.Automation.Language.Parser]::ParseFile($sourcePath, [ref]$tokens, [ref]$parseErrors)
if ($parseErrors) { throw 'Virtual HID manager did not parse.' }
# Execute the real action bodies with mocked OS commands, never the administrator/store bootstrap.
foreach ($function in $ast.FindAll({ param($node) $node -is [Management.Automation.Language.FunctionDefinitionAst] }, $false)) {
    . ([scriptblock]::Create($function.Extent.Text))
}
$cleanupStatement = $ast.EndBlock.Statements | Where-Object {
    $_ -is [Management.Automation.Language.IfStatementAst] -and $_.Clauses[0].Item1.Extent.Text -eq '$Action -eq ''Cleanup'''
}
if (-not $cleanupStatement) { throw 'Virtual HID cleanup action was not found.' }
$cleanupText = $cleanupStatement.Clauses[0].Item2.Extent.Text
$cleanupAction = [scriptblock]::Create($cleanupText.Substring(1, $cleanupText.Length - 2))
$source = Get-Content $sourcePath -Raw
$start = $source.IndexOf('if ([string]::IsNullOrWhiteSpace($ArtifactDirectory))')
$end = $source.IndexOf('$state =', $start)
if ($start -lt 0 -or $end -le $start) { throw 'Virtual HID install preflight was not found.' }
$installPreflight = [scriptblock]::Create($source.Substring($start, $end - $start))

Describe 'Virtual HID driver ownership safety' {
    BeforeEach {
        $hardwareId = 'ROOT\PtUmdfHidPrototype'
        $subject = 'CN=PowerToys Isolated UMDF CI Smoke'
        $originalWindows = $env:WINDIR
        $originalExitCode = $global:LASTEXITCODE
        $env:WINDIR = Join-Path $TestDrive 'Windows'
        New-Item (Join-Path $env:WINDIR 'INF') -ItemType Directory -Force | Out-Null
        $StatePath = Join-Path $TestDrive "$([guid]::NewGuid().ToString('N')).json"
        $ResultsDirectory = Join-Path $TestDrive ([guid]::NewGuid().ToString('N'))
        New-Item $ResultsDirectory -ItemType Directory -Force | Out-Null
        $journal = @{
            HardwareId = $hardwareId
            Subject = $subject
            Thumbprint = 'A' * 40
            InfNames = @()
            ImportedStores = @()
        }
        $script:devices = @()
        $script:leaveDevice = $false
        $script:nativeExit = 0
        Mock Get-PnpDevice { $script:devices } -ParameterFilter { $PresentOnly }
        Mock Get-PnpDeviceProperty { [pscustomobject]@{ Data = @($hardwareId) } }
        Mock pnputil.exe {
            $global:LASTEXITCODE = $script:nativeExit
            if ($args[0] -eq '/delete-driver' -and $script:nativeExit -eq 0) {
                Remove-Item -LiteralPath (Join-Path $env:WINDIR "INF\$($args[1])")
            }
            if ($args[0] -eq '/remove-device' -and -not $script:leaveDevice -and $script:nativeExit -eq 0) {
                $removed = $args[1]
                $script:devices = @($script:devices | Where-Object InstanceId -ne $removed)
            }
        }
        Mock Test-Path { $true } -ParameterFilter { $Path -like 'Cert:*' }
        Mock Get-Item { [pscustomobject]@{ Subject = $subject } } -ParameterFilter { $Path -like 'Cert:*' }
        Mock Remove-Item {} -ParameterFilter { $Path -like 'Cert:*' }
    }

    AfterEach {
        $env:WINDIR = $originalWindows
        $global:LASTEXITCODE = $originalExitCode
    }

    It 'does not touch devices or trust without an ownership journal' {
        $Action = 'Cleanup'
        & $cleanupAction
        Assert-MockCalled Get-PnpDevice -Times 0 -Exactly -Scope It
        Assert-MockCalled pnputil.exe -Times 0 -Exactly -Scope It
        Assert-MockCalled Remove-Item -Times 0 -Exactly -Scope It -ParameterFilter { $Path -like 'Cert:*' }
    }

    It 'rejects malformed ownership <Field> before any native mutation' -TestCases @(
        @{ Field = 'HardwareId'; Value = 'ROOT\Unrelated' }
        @{ Field = 'Subject'; Value = 'CN=Unrelated' }
        @{ Field = 'Thumbprint'; Value = '..\not-a-thumbprint' }
        @{ Field = 'InfNames'; Value = @('unrelated.inf') }
        @{ Field = 'ImportedStores'; Value = @('My') }
    ) {
        param($Field, $Value)
        $Action = 'Cleanup'
        $journal[$Field] = $Value
        $journal | ConvertTo-Json | Set-Content $StatePath
        $Action | Should Be 'Cleanup'
        Test-Path -LiteralPath $StatePath | Should Be $true
        $caught = $null
        try { & $cleanupAction } catch { $caught = $_ }
        $caught | Should Not BeNullOrEmpty
        Assert-MockCalled pnputil.exe -Times 0 -Exactly -Scope It
        Assert-MockCalled Remove-Item -Times 0 -Exactly -Scope It -ParameterFilter { $Path -like 'Cert:*' }
    }

    It 'refuses a published INF which does not contain the owned hardware ID' {
        $Action = 'Cleanup'
        $journal.InfNames = @('oem7.inf')
        $journal | ConvertTo-Json | Set-Content $StatePath
        'ROOT\Unrelated' | Set-Content (Join-Path $env:WINDIR 'INF\oem7.inf')
        { & $cleanupAction } | Should Throw 'Refusing to remove unrelated driver package'
        Assert-MockCalled pnputil.exe -Times 0 -Exactly -Scope It
    }

    It 'removes only its package, present root, and journaled certificate stores' {
        $Action = 'Cleanup'
        $journal.InfNames = @('oem7.inf')
        $journal.ImportedStores = @('TrustedPublisher')
        $journal | ConvertTo-Json | Set-Content $StatePath
        $hardwareId | Set-Content (Join-Path $env:WINDIR 'INF\oem7.inf')
        $script:devices = @(
            [pscustomobject]@{ InstanceId = 'ROOT\HIDCLASS\0001'; Status = 'OK' }
            [pscustomobject]@{ InstanceId = 'ROOT\HIDCLASS\0002'; Status = 'OK' }
        )
        Mock Get-PnpDeviceProperty { [pscustomobject]@{ Data = @('ROOT\Unrelated') } } -ParameterFilter {
            $InstanceId -eq 'ROOT\HIDCLASS\0002'
        }
        & $cleanupAction
        Test-Path -LiteralPath $StatePath | Should Be $false
        (Get-Content (Join-Path $ResultsDirectory 'driver-cleanup.json') -Raw | ConvertFrom-Json).Removed | Should Be $true
        Assert-MockCalled pnputil.exe -Times 2 -Exactly -Scope It
        Assert-MockCalled Get-PnpDevice -Scope It -ParameterFilter { $PresentOnly }
        $script:devices.Count | Should Be 1
        $script:devices[0].InstanceId | Should Be 'ROOT\HIDCLASS\0002'
        Assert-MockCalled Remove-Item -Times 1 -Exactly -Scope It -ParameterFilter {
            $Path -eq "Cert:\LocalMachine\TrustedPublisher\$('A' * 40)"
        }
    }

    It 'retains the journal and fails if a present unknown root remains' {
        $Action = 'Cleanup'
        $journal | ConvertTo-Json | Set-Content $StatePath
        $script:devices = @([pscustomobject]@{ InstanceId = 'ROOT\HIDCLASS\0001'; Status = 'Unknown' })
        $script:leaveDevice = $true
        { & $cleanupAction } | Should Throw 'remains after cleanup'
        Test-Path -LiteralPath $StatePath | Should Be $true
    }

    It 'retains the journal and records native removal failure' {
        $Action = 'Cleanup'
        $journal | ConvertTo-Json | Set-Content $StatePath
        $script:devices = @([pscustomobject]@{ InstanceId = 'ROOT\HIDCLASS\0001'; Status = 'OK' })
        $script:nativeExit = 5
        { & $cleanupAction } | Should Throw 'Device removal failed'
        Test-Path -LiteralPath $StatePath | Should Be $true
        (Get-Content (Join-Path $ResultsDirectory 'driver-cleanup.json') -Raw | ConvertFrom-Json).Removed | Should Be $false
    }

    It 'does not remove a certificate whose subject no longer matches the journal' {
        $Action = 'Cleanup'
        $journal.ImportedStores = @('Root')
        $journal | ConvertTo-Json | Set-Content $StatePath
        Mock Get-Item { [pscustomobject]@{ Subject = 'CN=Unrelated' } } -ParameterFilter { $Path -like 'Cert:*' }
        { & $cleanupAction } | Should Throw 'Refusing to remove an unrelated certificate'
        Assert-MockCalled Remove-Item -Times 0 -Exactly -Scope It -ParameterFilter { $Path -like 'Cert:*' }
    }

    It 'requires an explicit staged artifact for install' {
        $ArtifactDirectory = ''
        { & $installPreflight } | Should Throw 'Install requires'
        Assert-MockCalled pnputil.exe -Times 0 -Exactly -Scope It
    }

    It 'refuses install while an earlier journal still needs cleanup' {
        $ArtifactDirectory = $TestDrive
        $journal | ConvertTo-Json | Set-Content $StatePath
        { & $installPreflight } | Should Throw 'requires cleanup'
    }

    It 'refuses to overwrite a present owned device' {
        $ArtifactDirectory = $TestDrive
        $script:devices = @([pscustomobject]@{ InstanceId = 'ROOT\HIDCLASS\0001'; Status = 'Unknown' })
        { & $installPreflight } | Should Throw 'pre-existing prototype device'
    }

    It 'rejects an artifact hash mismatch before importing any trust' {
        $ArtifactDirectory = Join-Path $TestDrive 'artifact'
        New-Item (Join-Path $ArtifactDirectory 'driver') -ItemType Directory -Force | Out-Null
        'modified' | Set-Content (Join-Path $ArtifactDirectory 'driver\VhidminiUm.dll')
        @(@{ Path = 'VhidminiUm.dll'; Hash = 'not-the-file-hash' }) |
            ConvertTo-Json | Set-Content (Join-Path $ArtifactDirectory 'driver-hashes.json')
        { & $installPreflight } | Should Throw 'Staged driver hash mismatch'
        Assert-MockCalled pnputil.exe -Times 0 -Exactly -Scope It
    }

    It 'rejects an unexpected certificate subject before saving ownership or importing trust' {
        $ArtifactDirectory = Join-Path $TestDrive 'wrong-certificate'
        $driverPath = Join-Path $ArtifactDirectory 'driver'
        New-Item $driverPath -ItemType Directory -Force | Out-Null
        $rsa = [Security.Cryptography.RSA]::Create(2048)
        try {
            $request = [Security.Cryptography.X509Certificates.CertificateRequest]::new(
                'CN=Unrelated', $rsa, [Security.Cryptography.HashAlgorithmName]::SHA256,
                [Security.Cryptography.RSASignaturePadding]::Pkcs1)
            $cert = $request.CreateSelfSigned([DateTimeOffset]::UtcNow.AddMinutes(-1), [DateTimeOffset]::UtcNow.AddDays(1))
            try {
                [IO.File]::WriteAllBytes((Join-Path $driverPath 'PtHidProbe.cer'),
                    $cert.Export([Security.Cryptography.X509Certificates.X509ContentType]::Cert))
            }
            finally { $cert.Dispose() }
        }
        finally { $rsa.Dispose() }
        $hash = Get-FileHash (Join-Path $driverPath 'PtHidProbe.cer') -Algorithm SHA256
        @(@{ Path = $hash.Path; Hash = $hash.Hash }) |
            ConvertTo-Json | Set-Content (Join-Path $ArtifactDirectory 'driver-hashes.json')
        { & $installPreflight } | Should Throw 'Unexpected test certificate subject'
        Test-Path -LiteralPath $StatePath | Should Be $false
        Assert-MockCalled pnputil.exe -Times 0 -Exactly -Scope It
    }
}
