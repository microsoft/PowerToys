# Copyright (c) Microsoft Corporation
# The Microsoft Corporation licenses this file to you under the MIT license.
# See the LICENSE file in the project root for more information.

$payload = Join-Path $PSScriptRoot '..\..\src\modules\MouseWithoutBorders\MouseWithoutBorders.UITests\Payload'
. (Join-Path $payload 'ModernSandboxRecovery.ps1')

Describe 'Host-only modern Sandbox recovery inventory' {
    BeforeEach {
        $journal = [pscustomobject]@{ SandboxProcesses = @(); ViewerHwnd = 0 }
        $provision = [pscustomobject]@{ SandboxBackend = 'WinApp' }
        Mock Get-MwbTrustedSandboxCli { 'C:\Trusted\wsb.exe' }
        Mock Invoke-MwbSandboxControl { [pscustomobject]@{ WindowsSandboxEnvironments = @() } }
    }

    It 'accepts only a verified empty inventory without stopping any instance' {
        Assert-MwbHostOnlyModernSandboxIdle $journal $provision

        Assert-MockCalled Get-MwbTrustedSandboxCli -Times 1 -Exactly -Scope It
        Assert-MockCalled Invoke-MwbSandboxControl -Times 1 -Exactly -Scope It -ParameterFilter {
            $Action -eq 'List' -and $InstanceId -eq [guid]::Empty
        }
        Assert-MockCalled Invoke-MwbSandboxControl -Times 0 -Exactly -Scope It -ParameterFilter { $Action -eq 'Stop' }
    }

    It 'refuses an active unowned instance without stopping it' {
        Mock Invoke-MwbSandboxControl {
            [pscustomobject]@{ WindowsSandboxEnvironments = @([pscustomobject]@{ Id = '2aa6fc58-ed96-470b-b4eb-ec05a8504e47' }) }
        }

        { Assert-MwbHostOnlyModernSandboxIdle $journal $provision } | Should Throw 'Modern Sandbox instances remain'
        Assert-MockCalled Invoke-MwbSandboxControl -Times 0 -Exactly -Scope It -ParameterFilter { $Action -eq 'Stop' }
    }

    It 'refuses malformed inventory rather than assuming no instances exist' {
        Mock Invoke-MwbSandboxControl { [pscustomobject]@{} }

        { Assert-MwbHostOnlyModernSandboxIdle $journal $provision } | Should Throw 'expected instance array'
    }

    It 'propagates an unavailable provider without assuming the Sandbox is idle' {
        Mock Invoke-MwbSandboxControl { throw 'Provider unavailable.' }

        { Assert-MwbHostOnlyModernSandboxIdle $journal $provision } | Should Throw 'Provider unavailable.'
    }

    It 'does not apply the modern rule to a legacy backend' {
        $provision.SandboxBackend = 'Legacy'

        { Assert-MwbHostOnlyModernSandboxIdle $journal $provision } | Should Throw 'legacy Sandbox ownership records'
        Assert-MockCalled Invoke-MwbSandboxControl -Times 0 -Exactly -Scope It
    }

    It 'refuses recorded legacy Sandbox processes' {
        $journal.SandboxProcesses = @([pscustomobject]@{ Id = 1234 })

        { Assert-MwbHostOnlyModernSandboxIdle $journal $provision } | Should Throw 'legacy Sandbox ownership records'
        Assert-MockCalled Invoke-MwbSandboxControl -Times 0 -Exactly -Scope It
    }

    It 'refuses a legacy viewer handle' {
        $journal.ViewerHwnd = 1234

        { Assert-MwbHostOnlyModernSandboxIdle $journal $provision } | Should Throw 'legacy Sandbox ownership records'
        Assert-MockCalled Invoke-MwbSandboxControl -Times 0 -Exactly -Scope It
    }
}
