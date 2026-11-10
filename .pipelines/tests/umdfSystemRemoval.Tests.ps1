# Copyright (c) Microsoft Corporation
# The Microsoft Corporation licenses this file to you under the MIT license.
# See the LICENSE file in the project root for more information.

. (Join-Path $PSScriptRoot '..\umdf\UmdfDriver.Common.ps1')

Describe 'Protected noninteractive owned display removal' {
    BeforeEach {
        $script:nativeResult = 0
        $script:hangTask = $false
        $script:registeredArguments = $null
        $script:deletedTask = $null
        $script:taskSddl = 'O:BAG:SYD:P(A;;GA;;;SY)(A;;GA;;;BA)'
        $script:runningTask = [pscustomobject]@{ EnginePID = 0 }
        $script:registeredTask = [pscustomobject]@{
            State = 3; LastRunTime = [datetime]::MinValue; LastTaskResult = 267011
            Runs = 0; Stops = 0
        }
        $script:registeredTask | Add-Member ScriptMethod GetSecurityDescriptor {
            param($Flags)
            $script:taskSddl
        }
        $script:registeredTask | Add-Member ScriptMethod Run {
            param($Parameters)
            $this.Runs++
            $this.LastRunTime = [datetime]::Now
            $this.State = if ($script:hangTask) { 4 } else { 3 }
            $this.LastTaskResult = if ($script:hangTask) { 267009 } else { $script:nativeResult }
            $script:runningTask
        }
        $script:registeredTask | Add-Member ScriptMethod Stop {
            param($Flags)
            $this.Stops++
            $this.State = 3
        }
        $script:taskAction = [pscustomobject]@{ Path = ''; Arguments = ''; WorkingDirectory = '' }
        $actions = [pscustomobject]@{}
        $actions | Add-Member ScriptMethod Create { param($Type) $script:taskAction }
        $script:taskDefinition = [pscustomobject]@{
            Principal = [pscustomobject]@{ UserId = ''; LogonType = 0; RunLevel = 0 }
            Settings = [pscustomobject]@{
                Enabled = $false; Hidden = $false; AllowDemandStart = $false
                DisallowStartIfOnBatteries = $true; StopIfGoingOnBatteries = $true
                ExecutionTimeLimit = ''
            }
            Actions = $actions
        }
        $script:taskFolder = [pscustomobject]@{}
        $script:taskFolder | Add-Member ScriptMethod RegisterTaskDefinition {
            param($Name, $Definition, $Flags, $UserId, $Password, $LogonType, $Sddl)
            $script:registeredArguments = @{
                Name = $Name; Definition = $Definition; Flags = $Flags
                UserId = $UserId; Password = $Password; LogonType = $LogonType; Sddl = $Sddl
            }
            $script:registeredTask
        }
        $script:taskFolder | Add-Member ScriptMethod DeleteTask {
            param($Name, $Flags)
            $script:deletedTask = $Name
        }
        $script:scheduler = [pscustomobject]@{}
        $script:scheduler | Add-Member ScriptMethod Connect {}
        $script:scheduler | Add-Member ScriptMethod GetFolder { param($Path) $script:taskFolder }
        $script:scheduler | Add-Member ScriptMethod NewTask { param($Flags) $script:taskDefinition }
        Mock New-Object { $script:scheduler } -ParameterFilter { $ComObject -eq 'Schedule.Service' }
        Mock Test-UmdfAdministrator { $true }
        Mock Get-CimInstance { [pscustomobject]@{ Manufacturer = 'Microsoft Corporation'; Model = 'Virtual Machine' } }
        Mock Get-UmdfOwnedDevices { @([pscustomobject]@{ InstanceId = 'ROOT\DISPLAY\0000' }) }
        Mock Get-AuthenticodeSignature {
            [pscustomobject]@{ Status = 'Valid'; SignerCertificate = [pscustomobject]@{ Subject = 'CN=Microsoft Windows, O=Microsoft Corporation' } }
        }
        $results = Join-Path $TestDrive ([guid]::NewGuid().ToString('N'))
        New-Item -ItemType Directory -Path $results | Out-Null
    }

    It 'executes only the inbox utility for the exact owned root and preserves native exit <Exit>' -TestCases @(
        @{ Exit = 0 }
        @{ Exit = 3010 }
    ) {
        param($Exit)
        $script:nativeResult = $Exit
        $actual = Invoke-UmdfSystemDeviceRemoval -InstanceId 'ROOT\DISPLAY\0000' -ResultsDirectory $results -IsolatedGuest
        $actual | Should Be $Exit
        $script:taskAction.Path | Should Be (Join-Path ([Environment]::SystemDirectory) 'pnputil.exe')
        $script:taskAction.Arguments | Should Be '/remove-device ROOT\DISPLAY\0000'
        $script:taskAction.WorkingDirectory | Should Be ([Environment]::SystemDirectory)
        $script:registeredArguments.UserId | Should Be 'S-1-5-18'
        $script:registeredArguments.LogonType | Should Be 5
        $script:registeredArguments.Password | Should BeNullOrEmpty
        $script:registeredArguments.Flags | Should Be 18
        $script:registeredTask.Runs | Should Be 1
        $script:deletedTask | Should Be $script:registeredArguments.Name
        $evidence = Get-ChildItem -LiteralPath $results -Filter 'system-removal-*.json' | Get-Content -Raw | ConvertFrom-Json
        $evidence.ExitCode | Should Be $Exit
    }

    It 'rejects an unsafe or unrelated target <Instance>' -TestCases @(
        @{ Instance = 'ROOT\DISPLAY\0000 /force' }
        @{ Instance = 'PCI\UNRELATED' }
    ) {
        param($Instance)
        { Invoke-UmdfSystemDeviceRemoval -InstanceId $Instance -ResultsDirectory $results -IsolatedGuest } | Should Throw 'Invalid owned display'
        $script:registeredTask.Runs | Should Be 0
    }

    It 'requires exactly the hardware-matched fixture before registering SYSTEM work' {
        Mock Get-UmdfOwnedDevices { @([pscustomobject]@{ InstanceId = 'ROOT\DISPLAY\0001' }) }
        { Invoke-UmdfSystemDeviceRemoval -InstanceId 'ROOT\DISPLAY\0000' -ResultsDirectory $results -IsolatedGuest } |
            Should Throw 'exactly the known display fixture'
        $script:registeredArguments | Should BeNullOrEmpty
    }

    It 'requires explicit isolated approval before registering privileged work' {
        { Invoke-UmdfSystemDeviceRemoval -InstanceId 'ROOT\DISPLAY\0000' -ResultsDirectory $results -IsolatedGuest:$false } |
            Should Throw 'approved isolated-guest'
        $script:registeredArguments | Should BeNullOrEmpty
    }

    It 'refuses a utility without a valid Microsoft signature' {
        Mock Get-AuthenticodeSignature { [pscustomobject]@{ Status = 'NotSigned' } }
        { Invoke-UmdfSystemDeviceRemoval -InstanceId 'ROOT\DISPLAY\0000' -ResultsDirectory $results -IsolatedGuest } |
            Should Throw 'trusted inbox'
        $script:registeredArguments | Should BeNullOrEmpty
    }

    It 'rejects unsafe task security before running it: <Case>' -TestCases @(
        @{ Case = 'standard-user access'; Sddl = 'O:SYG:SYD:P(A;;GA;;;SY)(A;;GA;;;BA)(A;;GR;;;BU)' }
        @{ Case = 'untrusted owner'; Sddl = 'O:BUG:SYD:P(A;;GA;;;SY)(A;;GA;;;BA)' }
        @{ Case = 'inherited ACL'; Sddl = 'O:SYG:SYD:(A;;GA;;;SY)(A;;GA;;;BA)' }
    ) {
        param($Case, $Sddl)
        $script:taskSddl = $Sddl
        { Invoke-UmdfSystemDeviceRemoval -InstanceId 'ROOT\DISPLAY\0000' -ResultsDirectory $results -IsolatedGuest } |
            Should Throw 'protected SYSTEM'
        $script:registeredTask.Runs | Should Be 0
        $script:deletedTask | Should Be $script:registeredArguments.Name
    }

    It 'stops and deletes only its owned task after a bounded timeout' {
        $script:hangTask = $true
        { Invoke-UmdfSystemDeviceRemoval -InstanceId 'ROOT\DISPLAY\0000' -ResultsDirectory $results -IsolatedGuest -TimeoutSeconds 1 } |
            Should Throw 'deadline'
        $script:registeredTask.Stops | Should Be 1
        $script:deletedTask | Should Be $script:registeredArguments.Name
    }
}
