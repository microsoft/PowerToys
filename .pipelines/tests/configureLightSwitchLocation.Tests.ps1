# Copyright (c) Microsoft Corporation
# The Microsoft Corporation licenses this file to you under the MIT license.
# See the LICENSE file in the project root for more information.

. (Join-Path $PSScriptRoot '..\configureLightSwitchLocation.ps1')
$pipelinePath = Join-Path $PSScriptRoot '..\v2\templates\job-test-project.yml'

Describe 'Light Switch location prerequisites without host mutation' {
    BeforeEach {
        $statePath = Join-Path $TestDrive "$([guid]::NewGuid().ToString('N')).json"
        $targets = @(Get-LightSwitchLocationTargets)
        $registry = @{}
        $kinds = @{}
        foreach ($target in $targets) {
            $registry[$target.Path] = @{ LastSetTime = 123456789L }
            $kinds[$target.Path] = @{ LastSetTime = 'QWord'; $target.Name = $target.Kind }
        }
        $registry[$targets[0].Path].Value = 'Deny'
        $registry[$targets[2].Path].Status = 0
        $fixture = @{ Status = 'Stopped'; StartType = 'Manual'; Writes = 0; FailWrite = $false; FailRestore = $false }
        Mock Get-LightSwitchLocationIdentity { 'S-1-5-21-100-200-300-1001' }
        Mock Test-Path { $registry.ContainsKey([string]$LiteralPath) } -ParameterFilter { $LiteralPath -match '^HK(LM|CU):' }
        Mock Get-Item {
            param($LiteralPath)
            $key = [pscustomobject]@{ Data = $registry[[string]$LiteralPath]; Kinds = $kinds[[string]$LiteralPath]; SubKeyCount = 0 }
            $key | Add-Member ScriptMethod GetValueNames { @($this.Data.Keys) }
            $key | Add-Member ScriptMethod GetValue { param($name) $this.Data[$name] }
            $key | Add-Member ScriptMethod GetValueKind { param($name) [Microsoft.Win32.RegistryValueKind]$this.Kinds[$name] }
            $key | Add-Member ScriptMethod Dispose {}
            $key | Add-Member ScriptProperty ValueCount { $this.Data.Count }
            $key
        } -ParameterFilter { $LiteralPath -match '^HK(LM|CU):' }
        Mock New-Item {
            param($Path, $Force)
            if ($Force -or $registry.ContainsKey([string]$Path)) { throw 'Never recreate an existing registry key.' }
            $registry[[string]$Path] = @{}
            $kinds[[string]$Path] = @{}
        } -ParameterFilter { $Path -match '^HK(LM|CU):' }
        Mock New-ItemProperty {
            param($LiteralPath, $Name, $Value, $PropertyType)
            if (-not (Test-Path -LiteralPath $statePath)) { throw 'Snapshot must precede all mutations.' }
            $fixture.Writes++
            if ($fixture.FailWrite -and $fixture.Writes -eq 2) { throw 'Consent write failed.' }
            if ($fixture.FailRestore -and $Value -eq 'Deny') { throw 'Restore failed.' }
            $registry[[string]$LiteralPath][[string]$Name] = $Value
            $kinds[[string]$LiteralPath][[string]$Name] = $PropertyType
        } -ParameterFilter { $LiteralPath -match '^HK(LM|CU):' }
        Mock Remove-ItemProperty { $registry[[string]$LiteralPath].Remove([string]$Name) } -ParameterFilter { $LiteralPath -match '^HK(LM|CU):' }
        Mock Remove-Item {
            if ($registry[[string]$LiteralPath].Count -ne 0) { throw 'Never delete registry metadata.' }
            $registry.Remove([string]$LiteralPath)
        } -ParameterFilter { $LiteralPath -match '^HK(LM|CU):' }
        Mock Get-Service {
            $service = [pscustomobject]@{ Status = $fixture.Status; StartType = $fixture.StartType }
            $service | Add-Member ScriptMethod WaitForStatus {
                param($expected, $timeout)
                if ($this.Status -ne $expected.ToString()) { throw 'Service did not reach requested state.' }
            }
            $service
        }
        Mock Restart-Service { $fixture.Status = 'Running' }
        Mock Stop-Service { $fixture.Status = 'Stopped' }
    }

    It 'changes only the three named values and restores original values, absence, types, and stopped service' {
        Enable-LightSwitchLocation -StatePath $statePath
        $registry[$targets[0].Path].Value | Should Be 'Allow'
        $registry[$targets[1].Path].Value | Should Be 'Allow'
        $registry[$targets[2].Path].Status | Should Be 1
        $fixture.Status | Should Be 'Running'
        Restore-LightSwitchLocation -StatePath $statePath
        $registry[$targets[0].Path].Value | Should Be 'Deny'
        $registry[$targets[1].Path].ContainsKey('Value') | Should Be $false
        $registry[$targets[2].Path].Status | Should Be 0
        $fixture.Status | Should Be 'Stopped'
        foreach ($target in $targets) {
            $registry[$target.Path].LastSetTime | Should Be 123456789L
            $kinds[$target.Path][$target.Name] | Should Be $target.Kind
        }
        Assert-MockCalled New-Item -Times 0 -Exactly -Scope It -ParameterFilter { $Path -match '^HK(LM|CU):' }
        Test-Path $statePath | Should Be $false
    }

    It 'creates only missing keys and removes only empty test-created keys' {
        $registry.Remove($targets[1].Path)
        Enable-LightSwitchLocation -StatePath $statePath
        $registry.ContainsKey($targets[1].Path) | Should Be $true
        Restore-LightSwitchLocation -StatePath $statePath
        $registry.ContainsKey($targets[1].Path) | Should Be $false
        Assert-MockCalled New-Item -Times 1 -Exactly -Scope It -ParameterFilter { $Path -match '^HK(LM|CU):' -and -not $Force }
    }

    It 'keeps OS metadata added to a test-created key' {
        $registry.Remove($targets[1].Path)
        Enable-LightSwitchLocation -StatePath $statePath
        $registry[$targets[1].Path].LastSetTime = 456L
        Restore-LightSwitchLocation -StatePath $statePath
        $registry[$targets[1].Path].LastSetTime | Should Be 456L
        $registry[$targets[1].Path].ContainsKey('Value') | Should Be $false
    }

    It 'restores an interrupted setup before taking another snapshot and preserves a running service' {
        $fixture.Status = 'Running'
        Enable-LightSwitchLocation -StatePath $statePath
        Enable-LightSwitchLocation -StatePath $statePath
        Restore-LightSwitchLocation -StatePath $statePath
        $registry[$targets[0].Path].Value | Should Be 'Deny'
        $fixture.Status | Should Be 'Running'
        Assert-MockCalled Stop-Service -Times 0 -Exactly -Scope It
    }

    It 'restores a partial setup failure through the caller finally without swallowing errors' {
        $fixture.FailWrite = $true
        {
            try { Enable-LightSwitchLocation -StatePath $statePath }
            finally { Restore-LightSwitchLocation -StatePath $statePath }
        } | Should Throw 'Consent write failed.'
        $registry[$targets[0].Path].Value | Should Be 'Deny'
        $registry[$targets[1].Path].ContainsKey('Value') | Should Be $false
        $fixture.Status | Should Be 'Stopped'
        Test-Path $statePath | Should Be $false
    }

    It 'retains a retryable snapshot on restoration failure' {
        Enable-LightSwitchLocation -StatePath $statePath
        $fixture.FailRestore = $true
        { Restore-LightSwitchLocation -StatePath $statePath } | Should Throw 'Restore failed.'
        Test-Path $statePath | Should Be $true
        $fixture.FailRestore = $false
        Restore-LightSwitchLocation -StatePath $statePath
        Test-Path $statePath | Should Be $false
    }

    It 'does nothing for an unrelated job without a snapshot' {
        Restore-LightSwitchLocation -StatePath $statePath
        Assert-MockCalled Get-LightSwitchLocationIdentity -Times 0 -Exactly -Scope It
        Assert-MockCalled Get-Service -Times 0 -Exactly -Scope It
        Assert-MockCalled New-ItemProperty -Times 0 -Exactly -Scope It
    }

    It 'rejects a disabled service without changing startup type or consent' {
        $fixture.StartType = 'Disabled'
        { Enable-LightSwitchLocation -StatePath $statePath } | Should Throw 'does not change policies or service startup type'
        Assert-MockCalled New-ItemProperty -Times 0 -Exactly -Scope It
        Test-Path $statePath | Should Be $false
    }

    It 'rejects a mismatched cleanup user before touching HKCU' {
        Enable-LightSwitchLocation -StatePath $statePath
        Mock Get-LightSwitchLocationIdentity { 'S-1-5-21-100-200-300-1002' }
        { Restore-LightSwitchLocation -StatePath $statePath } | Should Throw 'different UI-test account'
        $fixture.Writes | Should Be 3
        Test-Path $statePath | Should Be $true
    }

    It 'rejects malformed snapshot values before any restoration' {
        Enable-LightSwitchLocation -StatePath $statePath
        $snapshot = Get-Content $statePath -Raw | ConvertFrom-Json
        $snapshot.Values[2].Value = 'not-a-dword'
        $snapshot | ConvertTo-Json -Depth 4 | Set-Content $statePath
        { Restore-LightSwitchLocation -StatePath $statePath } | Should Throw 'Invalid Light Switch location snapshot value'
        $fixture.Writes | Should Be 3
    }

    It 'rejects unexpected registry types before snapshot or mutation' {
        $kinds[$targets[0].Path].Value = 'DWord'
        { Enable-LightSwitchLocation -StatePath $statePath } | Should Throw 'Unexpected registry type'
        Assert-MockCalled New-ItemProperty -Times 0 -Exactly -Scope It
        Test-Path $statePath | Should Be $false
    }

    It 'fails when the service reverts consent during setup and still restores through finally' {
        Mock Restart-Service {
            $fixture.Status = 'Running'
            $registry[(Get-LightSwitchLocationTargets)[0].Path].Value = 'Deny'
        }
        {
            try { Enable-LightSwitchLocation -StatePath $statePath }
            finally { Restore-LightSwitchLocation -StatePath $statePath }
        } | Should Throw 'did not remain enabled after lfsvc restarted'
        $registry[$targets[0].Path].Value | Should Be 'Deny'
        $fixture.Status | Should Be 'Stopped'
        Test-Path $statePath | Should Be $false
    }

    It 'retains the marker if service restoration changes an original value' {
        $fixture.Status = 'Running'
        Enable-LightSwitchLocation -StatePath $statePath
        Mock Restart-Service {
            $fixture.Status = 'Running'
            $registry[(Get-LightSwitchLocationTargets)[0].Path].Value = 'Allow'
        }
        { Restore-LightSwitchLocation -StatePath $statePath } | Should Throw 'changed during service restoration'
        Test-Path $statePath | Should Be $true
    }

    It 'fails before mutation when the pipeline account is not elevated' {
        Mock Get-LightSwitchLocationIdentity { throw 'Not elevated.' }
        { Enable-LightSwitchLocation -StatePath $statePath } | Should Throw 'Not elevated.'
        Assert-MockCalled New-ItemProperty -Times 0 -Exactly -Scope It
        Test-Path $statePath | Should Be $false
    }
}

Describe 'Light Switch pipeline prerequisite gating' {
    BeforeEach {
        Mock Enable-LightSwitchLocation {}
        $yaml = Get-Content -LiteralPath $pipelinePath -Raw
        $locationStatePath = Join-Path $TestDrive 'unused.json'
    }

    It 'enables only the exact runner <Module>' -TestCases @(
        @{ Module = 'LightSwitch.UITests.Next'; Count = 1 }
        @{ Module = 'lightswitch.uitests.next'; Count = 1 }
        @{ Module = 'LightSwitch.UITests'; Count = 0 }
        @{ Module = 'LightSwitch.UITests.Next.Extra'; Count = 0 }
        @{ Module = 'OtherLightSwitch.UITests.Next'; Count = 0 }
        @{ Module = 'ColorPicker.UITests'; Count = 0 }
    ) {
        param($Module, $Count)
        $start = $yaml.IndexOf("          if (`$base -eq 'LightSwitch.UITests.Next') {")
        $end = $yaml.IndexOf('          if ($nonElevatedSuites', $start)
        $start | Should BeGreaterThan 0
        $base = $Module
        & ([scriptblock]::Create($yaml.Substring($start, $end - $start)))
        Assert-MockCalled Enable-LightSwitchLocation -Times $Count -Exactly -Scope It
    }

    It 'retains direct same-user dispatch and immediate, always, and interrupted cleanup' {
        $line = ($yaml -split "`n" | Where-Object { $_ -match '^\s+\$nonElevatedSuites =' }).Trim()
        $suites = & ([scriptblock]::Create($line + '; $nonElevatedSuites'))
        ($suites -contains 'LightSwitch.UITests.Next') | Should Be $false
        $yaml | Should Match "(?s)finally \{\s+try \{\s+if \(\`$base -eq 'LightSwitch.UITests.Next'\) \{\s+Restore-LightSwitchLocation"
        $yaml | Should Match 'displayName: Restore interrupted Light Switch location setup'
        $yaml | Should Match '(?s)displayName: Restore Light Switch location prerequisites\s+condition: always\(\)'
    }
}
