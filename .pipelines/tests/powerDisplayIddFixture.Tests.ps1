# Copyright (c) Microsoft Corporation
# The Microsoft Corporation licenses this file to you under the MIT license.
# See the LICENSE file in the project root for more information.

$sourcePath = Join-Path $PSScriptRoot '..\powerdisplay-idd-poc\Manage-Poc.ps1'
. (Join-Path $PSScriptRoot '..\umdf\UmdfDriver.Common.ps1')
$tokens = $null
$parseErrors = $null
$ast = [Management.Automation.Language.Parser]::ParseFile($sourcePath, [ref]$tokens, [ref]$parseErrors)
if ($parseErrors) { throw 'Virtual display manager did not parse.' }
foreach ($function in $ast.FindAll({ param($node) $node -is [Management.Automation.Language.FunctionDefinitionAst] }, $false)) {
    . ([scriptblock]::Create($function.Extent.Text))
}
$cleanupStatement = $ast.EndBlock.Statements | Where-Object {
    $_ -is [Management.Automation.Language.IfStatementAst] -and $_.Clauses[0].Item1.Extent.Text -eq '$Action -eq ''Cleanup'''
}
$cleanupText = $cleanupStatement.Clauses[0].Item2.Extent.Text
$cleanupAction = [scriptblock]::Create($cleanupText.Substring(1, $cleanupText.Length - 2))
$source = Get-Content $sourcePath -Raw
$start = $source.IndexOf('if (-not $IsolatedGuest)')
$end = $source.IndexOf('$hardwareId =', $start)
if ($start -lt 0 -or $end -le $start) { throw 'Virtual display approval gate was not found.' }
$approvalGate = [scriptblock]::Create($source.Substring($start, $end - $start))
$start = $source.IndexOf('if (-not $ArtifactDirectory)')
$end = $source.IndexOf('$state =', $start)
if ($start -lt 0 -or $end -le $start) { throw 'Virtual display artifact preflight was not found.' }
$installPreflight = [scriptblock]::Create($source.Substring($start, $end - $start))

function Invoke-TestDisplayProbe { throw 'Unmocked fixture probe invocation.' }

function New-DisplayArtifact {
    param([string]$Subject = 'CN=PowerToys Isolated IDD POC', [switch]$Expired, [switch]$PrivateKey)
    $script:ArtifactDirectory = Join-Path $TestDrive ([guid]::NewGuid().ToString('N'))
    $driverPath = Join-Path $ArtifactDirectory 'driver'
    $productionPath = Join-Path $ArtifactDirectory 'production'
    New-Item $driverPath, $productionPath -ItemType Directory -Force | Out-Null
    foreach ($name in @('PtDisplayIdd.dll', 'PtDisplayIdd.inf', 'PtDisplayIdd.cat', 'IddProbe.exe')) {
        'not-executable-test-data' | Set-Content (Join-Path $driverPath $name)
    }
    $rsa = [Security.Cryptography.RSA]::Create(2048)
    try {
        $request = [Security.Cryptography.X509Certificates.CertificateRequest]::new(
            $Subject, $rsa, [Security.Cryptography.HashAlgorithmName]::SHA256,
            [Security.Cryptography.RSASignaturePadding]::Pkcs1)
        $notAfter = if ($Expired) { [DateTimeOffset]::UtcNow.AddMinutes(-1) } else { [DateTimeOffset]::UtcNow.AddDays(1) }
        $cert = $request.CreateSelfSigned([DateTimeOffset]::UtcNow.AddHours(-1), $notAfter)
        try {
            $script:artifactThumbprint = $cert.Thumbprint
            $type = if ($PrivateKey) { [Security.Cryptography.X509Certificates.X509ContentType]::Pfx } else {
                [Security.Cryptography.X509Certificates.X509ContentType]::Cert
            }
            [IO.File]::WriteAllBytes((Join-Path $driverPath 'PtDisplayIdd.cer'), $cert.Export($type))
        }
        finally { $cert.Dispose() }
    }
    finally { $rsa.Dispose() }
    Get-ChildItem $driverPath -File | Get-FileHash -Algorithm SHA256 | Select-Object Path, Hash |
        ConvertTo-Json | Set-Content (Join-Path $ArtifactDirectory 'driver-hashes.json')
    foreach ($name in @('PowerDisplay.IddPoc.ProductionProbe.exe', 'PowerDisplay.Lib.dll')) {
        'not-executable-test-data' | Set-Content (Join-Path $productionPath $name)
    }
    Get-ChildItem $productionPath -File | ForEach-Object {
        @{ Path = $_.Name; Hash = (Get-FileHash $_.FullName -Algorithm SHA256).Hash }
    } | ConvertTo-Json | Set-Content (Join-Path $ArtifactDirectory 'production-hashes.json')
    @{ Platform = [Runtime.InteropServices.RuntimeInformation]::OSArchitecture.ToString(); DriverVersion = '0.0.1.0' } |
        ConvertTo-Json | Set-Content (Join-Path $ArtifactDirectory 'build.json')
    # Redirect only this fake artifact's native entry point, leaving the real cleanup body intact.
    $probePath = Join-Path $driverPath 'IddProbe.exe'
    Set-Alias -Name $probePath -Value Invoke-TestDisplayProbe -Scope Script
    $script:probeAliases.Add($probePath)
}

Describe 'Virtual display fixture ownership and approval' {
    BeforeEach {
        $hardwareId = 'ROOT\PtDisplayIddPoc'
        $subject = 'CN=PowerToys Isolated IDD POC'
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
            ImportedStores = @()
            BeforeInfs = @()
        }
        $script:devices = @()
        $script:unrelatedDeviceIds = @()
        $script:nativeExit = 0
        $script:leaveDevice = $false
        $script:removeOnReboot = $false
        $script:nativeCalls = [Collections.Generic.List[string]]::new()
        $script:ArtifactDirectory = ''
        $script:probeAliases = [Collections.Generic.List[string]]::new()
        $script:probeFailure = ''
        $script:probeVerified = $false
        $script:invalidProbeSignature = $false
        $script:cleanupSnapshotJson = '{"Version":2,"Monitors":[{"Connected":false},{"Connected":false}]}'
        $script:bootTime = [datetime]::new(2026, 11, 4, 0, 0, 0, [DateTimeKind]::Utc)
        $IsolatedGuest = $true
        Mock Test-Administrator { $true }
        Mock Get-CimInstance {
            if ($ClassName -eq 'Win32_OperatingSystem') { [pscustomobject]@{ LastBootUpTime = $script:bootTime } }
            else { [pscustomobject]@{ Manufacturer = 'Microsoft Corporation'; Model = 'Virtual Machine' } }
        }
        Mock Get-PnpDevice { $script:devices }
        Mock Get-PnpDeviceProperty {
            [pscustomobject]@{ Data = @($(if ($InstanceId -in $script:unrelatedDeviceIds) { 'ROOT\Unrelated' } else { $hardwareId })) }
        }
        Mock Get-AuthenticodeSignature {
            if ($script:invalidProbeSignature -and (Split-Path $FilePath -Leaf) -eq 'IddProbe.exe') {
                return [pscustomobject]@{ Status = 'HashMismatch'; SignerCertificate = $null }
            }
            if ((Split-Path $FilePath -Leaf) -eq 'IddProbe.exe') { $script:probeVerified = $true }
            [pscustomobject]@{ Status = 'Valid'; SignerCertificate = [pscustomobject]@{ Thumbprint = $script:artifactThumbprint } }
        }
        Mock Invoke-TestDisplayProbe {
            if (-not $script:probeVerified) { throw 'Probe invoked before signature verification.' }
            $command = $args -join ' '
            $script:nativeCalls.Add($command)
            $global:LASTEXITCODE = if ($command -eq $script:probeFailure) { 5 } else { 0 }
            $script:cleanupSnapshotJson
        }
        Mock pnputil.exe {
            $script:nativeCalls.Add($args[0])
            $global:LASTEXITCODE = $script:nativeExit
            $removed = $script:nativeExit -eq 0 -or ($script:nativeExit -eq 3010 -and $script:removeOnReboot)
            if ($args[0] -eq '/delete-driver' -and $removed) {
                Remove-Item -LiteralPath (Join-Path $env:WINDIR "INF\$($args[1])")
            }
            if ($args[0] -eq '/remove-device' -and -not $script:leaveDevice -and $removed) {
                $removed = $args[1]
                $script:devices = @($script:devices | Where-Object InstanceId -NE $removed)
            }
        }
        Mock certutil.exe { throw 'Trust mutation is forbidden in these preflight tests.' }
        Mock Export-UmdfDriverDiagnostics {
            'diagnostic setup log' | Set-Content (Join-Path $ResultsDirectory 'setupapi-tail.log')
        }
        Mock Test-Path { $true } -ParameterFilter { $Path -like 'Cert:*' }
        Mock Get-Item { [pscustomobject]@{ Subject = $subject } } -ParameterFilter { $Path -like 'Cert:*' }
        Mock Remove-Item {} -ParameterFilter { $Path -like 'Cert:*' }
    }

    AfterEach {
        foreach ($alias in $script:probeAliases) { Remove-Alias -Name $alias -Scope Script }
        $env:WINDIR = $originalWindows
        $global:LASTEXITCODE = $originalExitCode
    }

    It 'requires explicit isolated-guest approval before OS discovery' {
        $IsolatedGuest = $false
        { & $approvalGate } | Should Throw 'Explicit isolated-guest approval'
        Assert-MockCalled Get-CimInstance -Times 0 -Exactly -Scope It
    }

    It 'rejects a host which is not the approved guest type' {
        Mock Get-CimInstance { [pscustomobject]@{ Manufacturer = 'Other'; Model = 'Physical workstation' } }
        { & $approvalGate } | Should Throw 'approved Hyper-V guest'
        Assert-MockCalled Test-Administrator -Times 0 -Exactly -Scope It
    }

    It 'requires the separate elevated provisioning channel' {
        Mock Test-Administrator { $false }
        { & $approvalGate } | Should Throw 'already elevated'
        Assert-MockCalled pnputil.exe -Times 0 -Exactly -Scope It
    }

    It 'does not mutate devices or trust without an ownership journal' {
        { & $cleanupAction } | Should Throw 'No ownership journal'
        Assert-MockCalled pnputil.exe -Times 0 -Exactly -Scope It
        Assert-MockCalled Remove-Item -Times 0 -Exactly -Scope It -ParameterFilter { $Path -like 'Cert:*' }
    }

    It 'rejects malformed ownership <Field>' -TestCases @(
        @{ Field = 'HardwareId'; Value = 'ROOT\Unrelated' }
        @{ Field = 'Subject'; Value = 'CN=Unrelated' }
        @{ Field = 'Thumbprint'; Value = '..\invalid' }
        @{ Field = 'ImportedStores'; Value = @('My') }
        @{ Field = 'BeforeInfs'; Value = @('unrelated.inf') }
        @{ Field = 'BeforeInfs'; Value = 'oem1.inf' }
        @{ Field = 'ImportedStores'; Value = 'Root' }
    ) {
        param($Field, $Value)
        $journal[$Field] = $Value
        $journal | ConvertTo-Json | Set-Content $StatePath
        { & $cleanupAction } | Should Throw 'Invalid ownership journal'
        Assert-MockCalled pnputil.exe -Times 0 -Exactly -Scope It
    }

    It 'rejects missing ownership collection <Field>' -TestCases @(
        @{ Field = 'BeforeInfs' }
        @{ Field = 'ImportedStores' }
    ) {
        param($Field)
        $journal.Remove($Field)
        $journal | ConvertTo-Json | Set-Content $StatePath
        { & $cleanupAction } | Should Throw 'Invalid ownership journal'
        Assert-MockCalled pnputil.exe -Times 0 -Exactly -Scope It
    }

    It 'removes only newly owned matching packages, devices and imported stores' {
        New-DisplayArtifact
        $journal.Thumbprint = $artifactThumbprint
        $journal.BeforeInfs = @('oem1.inf')
        $journal.ImportedStores = @('TrustedPublisher')
        $journal | ConvertTo-Json | Set-Content $StatePath
        $hardwareId | Set-Content (Join-Path $env:WINDIR 'INF\oem1.inf')
        $hardwareId | Set-Content (Join-Path $env:WINDIR 'INF\oem7.inf')
        'ROOT\Unrelated' | Set-Content (Join-Path $env:WINDIR 'INF\oem8.inf')
        $script:devices = @(
            [pscustomobject]@{ InstanceId = 'ROOT\DISPLAY\0001'; Status = 'OK' }
            [pscustomobject]@{ InstanceId = 'ROOT\DISPLAY\0002'; Status = 'OK' }
        )
        $script:unrelatedDeviceIds = @('ROOT\DISPLAY\0002')
        & $cleanupAction
        Test-Path -LiteralPath $StatePath | Should Be $false
        Test-Path (Join-Path $env:WINDIR 'INF\oem1.inf') | Should Be $true
        Test-Path (Join-Path $env:WINDIR 'INF\oem8.inf') | Should Be $true
        $script:devices.Count | Should Be 1
        $script:nativeCalls.ToArray() | Should Be @('--connected 0 0', '--connected 1 0', '--snapshot', '/remove-device', '/delete-driver')
        (Get-Content (Join-Path $ResultsDirectory 'cleanup-snapshot.json') -Raw | ConvertFrom-Json).Version | Should Be 2
        (Get-Content (Join-Path $ResultsDirectory 'cleanup.json') -Raw | ConvertFrom-Json).Removed | Should Be $true
        Assert-MockCalled pnputil.exe -Times 2 -Exactly -Scope It
        Assert-MockCalled Remove-Item -Times 1 -Exactly -Scope It -ParameterFilter { $Path -eq "Cert:\LocalMachine\TrustedPublisher\$artifactThumbprint" }
    }

    It 'retains ownership and trust when device removal fails' {
        New-DisplayArtifact
        $journal.Thumbprint = $artifactThumbprint
        $journal.ImportedStores = @('Root')
        $journal | ConvertTo-Json | Set-Content $StatePath
        $script:devices = @([pscustomobject]@{ InstanceId = 'ROOT\DISPLAY\0001'; Status = 'Unknown' })
        $script:nativeExit = 5
        { & $cleanupAction } | Should Throw 'Device removal failed'
        Test-Path -LiteralPath $StatePath | Should Be $true
        (Get-Content (Join-Path $ResultsDirectory 'cleanup.json') -Raw | ConvertFrom-Json).Removed | Should Be $false
        Assert-MockCalled Remove-Item -Times 0 -Exactly -Scope It -ParameterFilter { $Path -like 'Cert:*' }
    }

    It 'retains ownership when a present fixture remains after removal' {
        New-DisplayArtifact
        $journal.Thumbprint = $artifactThumbprint
        $journal | ConvertTo-Json | Set-Content $StatePath
        $script:devices = @([pscustomobject]@{ InstanceId = 'ROOT\DISPLAY\0001'; Status = 'Unknown' })
        $script:leaveDevice = $true
        { & $cleanupAction } | Should Throw 'Fixture device remains'
        Test-Path -LiteralPath $StatePath | Should Be $true
    }

    It 'preserves native removal evidence when a later same-boot retry is refused' {
        New-DisplayArtifact
        $journal.Thumbprint = $artifactThumbprint
        $journal | ConvertTo-Json | Set-Content $StatePath
        $script:devices = @([pscustomobject]@{ InstanceId = 'ROOT\DISPLAY\0001'; Status = 'OK' })
        $script:nativeExit = 3010
        $script:removeOnReboot = $true
        { & $cleanupAction } | Should Throw 'Device removal requires a reboot'
        { & $cleanupAction } | Should Throw 'requires an actual reboot'
        $failures = @(Get-ChildItem -LiteralPath $ResultsDirectory -Directory -Filter 'cleanup-failure-*')
        $failures.Count | Should Be 2
        $messages = @($failures | ForEach-Object {
            Test-Path (Join-Path $_.FullName 'setupapi-tail.log') | Should Be $true
            Get-Content (Join-Path $_.FullName 'display-devices.json') -Raw | Should Match '^\s*\[\s*\]\s*$'
            (Get-Content (Join-Path $_.FullName 'failure.json') -Raw | ConvertFrom-Json).Errors
        }) -join '; '
        $messages | Should Match 'Device removal requires a reboot'
        $messages | Should Match 'requires an actual reboot'
        Assert-MockCalled Export-UmdfDriverDiagnostics -Times 2 -Exactly -Scope It
        Assert-MockCalled pnputil.exe -Times 1 -Exactly -Scope It
        Test-Path -LiteralPath $StatePath | Should Be $true
    }

    It 'retains the original cleanup failure when diagnostic collection fails' {
        New-DisplayArtifact
        $journal.Thumbprint = $artifactThumbprint
        $journal | ConvertTo-Json | Set-Content $StatePath
        $script:devices = @([pscustomobject]@{ InstanceId = 'ROOT\DISPLAY\0001'; Status = 'OK' })
        $script:nativeExit = 5
        Mock Export-UmdfDriverDiagnostics { throw 'Diagnostic source unavailable.' }
        { & $cleanupAction } | Should Throw 'Device removal failed'
        $failure = Get-ChildItem -LiteralPath $ResultsDirectory -Directory -Filter 'cleanup-failure-*'
        (Get-Content (Join-Path $failure.FullName 'failure.json') -Raw | ConvertFrom-Json).Errors[0] |
            Should Match 'Device removal failed'
        Test-Path -LiteralPath $StatePath | Should Be $true
        Assert-MockCalled Remove-Item -Times 0 -Exactly -Scope It -ParameterFilter { $Path -like 'Cert:*' }
    }

    It 'refuses <Case> cleanup artifacts before any native mutation' -TestCases @(
        @{ Case = 'missing'; Expected = 'requires its original artifact directory' }
        @{ Case = 'mutated-probe'; Expected = 'artifact hash' }
        @{ Case = 'wrong-subject'; Expected = 'Unexpected, expired, or non-public' }
        @{ Case = 'different-signing-identity'; Expected = 'does not match the ownership journal' }
        @{ Case = 'invalid-probe-signature'; Expected = 'Driver signature validation failed: IddProbe.exe' }
    ) {
        param($Case, $Expected)
        $certificateSubject = if ($Case -eq 'wrong-subject') { 'CN=Unrelated' } else { $subject }
        New-DisplayArtifact -Subject $certificateSubject
        if ($Case -ne 'different-signing-identity') { $journal.Thumbprint = $artifactThumbprint }
        $journal.ImportedStores = @('Root', 'TrustedPublisher')
        $journal | ConvertTo-Json | Set-Content $StatePath
        $originalJournal = Get-Content $StatePath -Raw
        $script:devices = @([pscustomobject]@{ InstanceId = 'ROOT\DISPLAY\0001'; Status = 'OK' })
        if ($Case -eq 'missing') { $script:ArtifactDirectory = '' }
        if ($Case -eq 'mutated-probe') { 'mutated' | Set-Content (Join-Path $ArtifactDirectory 'driver\IddProbe.exe') }
        $script:invalidProbeSignature = $Case -eq 'invalid-probe-signature'
        { & $cleanupAction } | Should Throw $Expected
        $script:nativeCalls.Count | Should Be 0
        Get-Content $StatePath -Raw | Should Be $originalJournal
        (Get-Content (Join-Path $ResultsDirectory 'cleanup.json') -Raw | ConvertFrom-Json).Removed | Should Be $false
        Assert-MockCalled Remove-Item -Times 0 -Exactly -Scope It -ParameterFilter { $Path -like 'Cert:*' }
    }

    It 'refuses to control multiple matching roots through the first-interface probe' {
        $journal | ConvertTo-Json | Set-Content $StatePath
        $script:devices = @(
            [pscustomobject]@{ InstanceId = 'ROOT\DISPLAY\0001'; Status = 'OK' }
            [pscustomobject]@{ InstanceId = 'ROOT\DISPLAY\0002'; Status = 'OK' }
        )
        { & $cleanupAction } | Should Throw 'exactly one owned display fixture root'
        $script:nativeCalls.Count | Should Be 0
        Test-Path -LiteralPath $StatePath | Should Be $true
    }

    It 'preserves ownership, package and trust when probe command <Command> fails' -TestCases @(
        @{ Command = '--connected 0 0'; Expected = 'Fixture monitor departure failed' }
        @{ Command = '--connected 1 0'; Expected = 'Fixture monitor departure failed' }
        @{ Command = '--snapshot'; Expected = 'Fixture departure snapshot failed' }
    ) {
        param($Command, $Expected)
        New-DisplayArtifact
        $journal.Thumbprint = $artifactThumbprint
        $journal.ImportedStores = @('Root')
        $journal | ConvertTo-Json | Set-Content $StatePath
        $originalJournal = Get-Content $StatePath -Raw
        $hardwareId | Set-Content (Join-Path $env:WINDIR 'INF\oem7.inf')
        $script:devices = @([pscustomobject]@{ InstanceId = 'ROOT\DISPLAY\0001'; Status = 'OK' })
        $script:probeFailure = $Command
        { & $cleanupAction } | Should Throw $Expected
        Get-Content $StatePath -Raw | Should Be $originalJournal
        Test-Path (Join-Path $env:WINDIR 'INF\oem7.inf') | Should Be $true
        (Get-Content (Join-Path $ResultsDirectory 'cleanup.json') -Raw | ConvertFrom-Json).Removed | Should Be $false
        $script:nativeCalls[$script:nativeCalls.Count - 1] | Should Be $Command
        Assert-MockCalled pnputil.exe -Times 0 -Exactly -Scope It
        Assert-MockCalled Remove-Item -Times 0 -Exactly -Scope It -ParameterFilter { $Path -like 'Cert:*' }
    }

    It 'rejects an unconfirmed departure snapshot: <Case>' -TestCases @(
        @{ Case = 'still-connected'; Json = '{"Version":2,"Monitors":[{"Connected":false},{"Connected":true}]}' }
        @{ Case = 'missing-connected'; Json = '{"Version":2,"Monitors":[{"Connected":false},{}]}' }
        @{ Case = 'non-boolean'; Json = '{"Version":2,"Monitors":[{"Connected":false},{"Connected":0}]}' }
        @{ Case = 'wrong-count'; Json = '{"Version":2,"Monitors":[{"Connected":false}]}' }
        @{ Case = 'wrong-version'; Json = '{"Version":1,"Monitors":[{"Connected":false},{"Connected":false}]}' }
    ) {
        param($Case, $Json)
        New-DisplayArtifact
        $journal.Thumbprint = $artifactThumbprint
        $journal.ImportedStores = @('Root')
        $journal | ConvertTo-Json | Set-Content $StatePath
        $originalJournal = Get-Content $StatePath -Raw
        $script:devices = @([pscustomobject]@{ InstanceId = 'ROOT\DISPLAY\0001'; Status = 'OK' })
        $script:cleanupSnapshotJson = $Json
        { & $cleanupAction } | Should Throw 'must confirm both monitors are disconnected'
        Get-Content $StatePath -Raw | Should Be $originalJournal
        (Get-Content (Join-Path $ResultsDirectory 'cleanup.json') -Raw | ConvertFrom-Json).Removed | Should Be $false
        Assert-MockCalled pnputil.exe -Times 0 -Exactly -Scope It
        Assert-MockCalled Remove-Item -Times 0 -Exactly -Scope It -ParameterFilter { $Path -like 'Cert:*' }
    }

    It 'does not claim immediate cleanup when <Operation> removal requires a reboot even if it disappears' -TestCases @(
        @{ Operation = 'device'; Expected = 'Device removal requires a reboot' }
        @{ Operation = 'package'; Expected = 'Package removal requires a reboot' }
    ) {
        param($Operation, $Expected)
        if ($Operation -eq 'device') {
            New-DisplayArtifact
            $journal.Thumbprint = $artifactThumbprint
            $script:devices = @([pscustomobject]@{ InstanceId = 'ROOT\DISPLAY\0001'; Status = 'OK' })
        }
        $journal.ImportedStores = @('Root')
        $journal | ConvertTo-Json | Set-Content $StatePath
        $originalJournal = Get-Content $StatePath -Raw
        $hardwareId | Set-Content (Join-Path $env:WINDIR 'INF\oem7.inf')
        $script:nativeExit = 3010
        $script:removeOnReboot = $true
        { & $cleanupAction } | Should Throw $Expected
        $retained = Get-Content $StatePath -Raw | ConvertFrom-Json
        $retained.RemovalPendingBootTicks | Should Be $script:bootTime.Ticks
        $retained.PSObject.Properties.Remove('RemovalPendingBootTicks')
        ($retained | ConvertTo-Json) | Should Be $originalJournal.TrimEnd()
        (Get-Content (Join-Path $ResultsDirectory 'cleanup.json') -Raw | ConvertFrom-Json).Removed | Should Be $false
        $script:devices.Count | Should Be 0
        if ($Operation -eq 'device') {
            Test-Path (Join-Path $env:WINDIR 'INF\oem7.inf') | Should Be $true
            Assert-MockCalled pnputil.exe -Times 0 -Exactly -Scope It -ParameterFilter { $args[0] -eq '/delete-driver' }
        }
        else { Test-Path (Join-Path $env:WINDIR 'INF\oem7.inf') | Should Be $false }
        Assert-MockCalled Remove-Item -Times 0 -Exactly -Scope It -ParameterFilter { $Path -like 'Cert:*' }
    }

    It 'blocks same-boot retries after <Operation> exit 3010 despite empty device and package enumeration' -TestCases @(
        @{ Operation = 'device'; Expected = 'Device removal requires a reboot' }
        @{ Operation = 'package'; Expected = 'Package removal requires a reboot' }
    ) {
        param($Operation, $Expected)
        if ($Operation -eq 'device') {
            New-DisplayArtifact
            $journal.Thumbprint = $artifactThumbprint
            $script:devices = @([pscustomobject]@{ InstanceId = 'ROOT\DISPLAY\0001'; Status = 'OK' })
        }
        $journal.ImportedStores = @('Root')
        $journal | ConvertTo-Json | Set-Content $StatePath
        $infPath = Join-Path $env:WINDIR 'INF\oem7.inf'
        $hardwareId | Set-Content $infPath
        $script:nativeExit = 3010
        $script:removeOnReboot = $true
        { & $cleanupAction } | Should Throw $Expected
        if (Test-Path $infPath) { Remove-Item $infPath }
        $script:devices.Count | Should Be 0
        $retainedJournal = Get-Content $StatePath -Raw
        $script:nativeExit = 0
        $script:ArtifactDirectory = ''
        { & $cleanupAction } | Should Throw 'requires an actual reboot'
        Get-Content $StatePath -Raw | Should Be $retainedJournal
        (Get-Content (Join-Path $ResultsDirectory 'cleanup.json') -Raw | ConvertFrom-Json).Removed | Should Be $false
        Assert-MockCalled pnputil.exe -Times 1 -Exactly -Scope It
        Assert-MockCalled Remove-Item -Times 0 -Exactly -Scope It -ParameterFilter { $Path -like 'Cert:*' }
        $script:bootTime = $script:bootTime.AddHours(1)
        & $cleanupAction
        Test-Path -LiteralPath $StatePath | Should Be $false
        (Get-Content (Join-Path $ResultsDirectory 'cleanup.json') -Raw | ConvertFrom-Json).Removed | Should Be $true
        Assert-MockCalled pnputil.exe -Times 1 -Exactly -Scope It
        Assert-MockCalled Remove-Item -Times 1 -Exactly -Scope It -ParameterFilter { $Path -like 'Cert:*' }
    }

    It 'retains ownership and trust when package removal fails' {
        $journal.ImportedStores = @('Root')
        $journal | ConvertTo-Json | Set-Content $StatePath
        $hardwareId | Set-Content (Join-Path $env:WINDIR 'INF\oem7.inf')
        $script:nativeExit = 5
        { & $cleanupAction } | Should Throw 'Package remains'
        Test-Path -LiteralPath $StatePath | Should Be $true
        Test-Path (Join-Path $env:WINDIR 'INF\oem7.inf') | Should Be $true
        Assert-MockCalled Remove-Item -Times 0 -Exactly -Scope It -ParameterFilter { $Path -like 'Cert:*' }
    }

    It 'does not delete unrelated certificate subjects' {
        $journal.ImportedStores = @('Root')
        $journal | ConvertTo-Json | Set-Content $StatePath
        Mock Get-Item { [pscustomobject]@{ Subject = 'CN=Unrelated' } } -ParameterFilter { $Path -like 'Cert:*' }
        { & $cleanupAction } | Should Throw 'Refusing to remove unrelated trust'
        Assert-MockCalled Remove-Item -Times 0 -Exactly -Scope It -ParameterFilter { $Path -like 'Cert:*' }
    }

    It 'requires a staged artifact and refuses existing ownership or devices' {
        $ArtifactDirectory = ''
        { & $installPreflight } | Should Throw 'Install requires'
        $ArtifactDirectory = $TestDrive
        $journal | ConvertTo-Json | Set-Content $StatePath
        { & $installPreflight } | Should Throw 'requires cleanup'
        Remove-Item $StatePath
        $script:devices = @([pscustomobject]@{ InstanceId = 'ROOT\DISPLAY\0001'; Status = 'OK' })
        { & $installPreflight } | Should Throw 'requires cleanup'
    }

    It 'rejects mismatched driver bytes before trust import' {
        New-DisplayArtifact
        'modified' | Set-Content (Join-Path $ArtifactDirectory 'driver\PtDisplayIdd.dll')
        { & $installPreflight } | Should Throw 'artifact hash'
        Assert-MockCalled certutil.exe -Times 0 -Exactly -Scope It
        Test-Path -LiteralPath $StatePath | Should Be $false
    }

    It 'rejects unsafe production manifest path <Path>' -TestCases @(
        @{ Path = '..\outside.dll' }
        @{ Path = 'C:\outside.dll' }
    ) {
        param($Path)
        New-DisplayArtifact
        $manifest = @(Get-Content (Join-Path $ArtifactDirectory 'production-hashes.json') -Raw | ConvertFrom-Json)
        $manifest += @{ Path = $Path; Hash = 'not-a-hash' }
        $manifest | ConvertTo-Json | Set-Content (Join-Path $ArtifactDirectory 'production-hashes.json')
        { & $installPreflight } | Should Throw 'production probe'
        Assert-MockCalled certutil.exe -Times 0 -Exactly -Scope It
    }

    It 'rejects a <Case> certificate before importing trust' -TestCases @(
        @{ Case = 'wrong-subject'; CertificateSubject = 'CN=Unrelated'; Expired = $false; Private = $false }
        @{ Case = 'expired'; CertificateSubject = 'CN=PowerToys Isolated IDD POC'; Expired = $true; Private = $false }
        @{ Case = 'private-key'; CertificateSubject = 'CN=PowerToys Isolated IDD POC'; Expired = $false; Private = $true }
    ) {
        param($Case, $CertificateSubject, $Expired, $Private)
        New-DisplayArtifact -Subject $CertificateSubject -Expired:$Expired -PrivateKey:$Private
        { & $installPreflight } | Should Throw 'Unexpected, expired, or non-public'
        Assert-MockCalled certutil.exe -Times 0 -Exactly -Scope It
        Test-Path -LiteralPath $StatePath | Should Be $false
    }

    It 'rejects architecture mismatch before creating ownership or trust' {
        New-DisplayArtifact
        @{ Platform = 'not-the-guest-architecture' } | ConvertTo-Json | Set-Content (Join-Path $ArtifactDirectory 'build.json')
        { & $installPreflight } | Should Throw 'architecture does not match'
        Assert-MockCalled certutil.exe -Times 0 -Exactly -Scope It
        Test-Path -LiteralPath $StatePath | Should Be $false
    }

    It 'accepts a complete public-certificate artifact without creating ownership or importing trust' {
        New-DisplayArtifact
        & $installPreflight
        Test-Path -LiteralPath $StatePath | Should Be $false
        Assert-MockCalled certutil.exe -Times 0 -Exactly -Scope It
        Assert-MockCalled pnputil.exe -Times 0 -Exactly -Scope It
    }
}
