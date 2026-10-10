# Copyright (c) Microsoft Corporation
# The Microsoft Corporation licenses this file to you under the MIT license.
# See the LICENSE file in the project root for more information.

#Requires -Version 7.0
$path = Join-Path $PSScriptRoot '..\..\src\modules\MouseWithoutBorders\Tests\SandboxExperiment\Invoke-AutonomousLocalVm.ps1'
$tokens = $null
$errors = $null
$ast = [Management.Automation.Language.Parser]::ParseFile($path, [ref]$tokens, [ref]$errors)
if ($errors.Count) { throw 'The local MWB controller contains parse errors.' }
foreach ($name in @('ConvertTo-MwbHostPowerEvent', 'Get-MwbHostPowerEvidence')) {
    $definition = $ast.Find({
        param($node)
        $node -is [Management.Automation.Language.FunctionDefinitionAst] -and $node.Name -eq $name
    }, $true)
    . ([scriptblock]::Create($definition.Extent.Text))
}

function New-MwbPowerEventFixture {
    param([string]$Sleep = '2026-10-08T05:28:26Z', [string]$Wake = '2026-10-08T14:24:19Z')
    $value = [pscustomobject]@{
        RecordId = 446766
        Xml = "<Event><EventData><Data Name=`"SleepTime`">$Sleep</Data><Data Name=`"WakeTime`">$Wake</Data><Data Name=`"WakeSourceText`">private-device</Data></EventData></Event>"
    }
    $value | Add-Member -MemberType ScriptMethod -Name ToXml -Value { $this.Xml }
    $value
}

Describe 'MWB exact host suspend evidence' {
    It 'records the verified long pause and its overlap without wake-source or user details' {
        $result = ConvertTo-MwbHostPowerEvent (New-MwbPowerEventFixture) `
            ([DateTime]::Parse('2026-10-08T05:27:24Z')) ([DateTime]::Parse('2026-10-08T14:25:31Z'))
        $result.RunOverlapMilliseconds | Should BeGreaterThan 32000000
        $result.SuspensionMilliseconds | Should Be $result.RunOverlapMilliseconds
        $result.RecordId | Should Be 446766
        ($result | ConvertTo-Json) | Should Not Match 'private-device|WakeSource|Xml'
    }

    It 'does not attribute a suspension outside the actual run to the failure' {
        $result = ConvertTo-MwbHostPowerEvent (New-MwbPowerEventFixture) `
            ([DateTime]::Parse('2026-10-08T15:00:00Z')) ([DateTime]::Parse('2026-10-08T15:01:00Z'))
        $result.RunOverlapMilliseconds | Should Be 0
    }

    It 'rejects invalid or reversed event timing instead of making an absence claim' -TestCases @(
        @{ Sleep = 'invalid'; Wake = '2026-10-08T14:24:19Z' }
        @{ Sleep = '2026-10-08T14:24:19Z'; Wake = '2026-10-08T05:28:26Z' }
    ) {
        param($Sleep, $Wake)
        { ConvertTo-MwbHostPowerEvent (New-MwbPowerEventFixture $Sleep $Wake) `
            ([DateTime]::Parse('2026-10-08T05:00:00Z')) ([DateTime]::Parse('2026-10-08T15:00:00Z')) } |
            Should Throw 'sleep/wake timing'
    }

    It 'distinguishes a query failure from a confirmed empty event inventory' {
        Mock Get-WinEvent { throw [UnauthorizedAccessException]::new('private diagnostic text') }
        Mock Write-Warning {}
        $result = Get-MwbHostPowerEvidence ([DateTime]::Parse('2026-10-08T05:00:00Z')) ([DateTime]::Parse('2026-10-08T15:00:00Z'))
        $result.QueryStatus | Should Be 'Failed'
        $result.OverlappedSuspension | Should BeNullOrEmpty
        $result.QueryErrorHResult | Should Match '^0x[0-9A-F]{8}$'
        ($result | ConvertTo-Json -Depth 5) | Should Not Match 'private diagnostic'
    }

    It 'records an actual successful empty query and bounded successful suspend inventory' {
        Mock Get-WinEvent { @() }
        $start = [DateTime]::Parse('2026-10-08T05:00:00Z')
        $end = [DateTime]::Parse('2026-10-08T15:00:00Z')
        $empty = Get-MwbHostPowerEvidence $start $end
        $empty.QueryStatus | Should Be 'Succeeded'
        $empty.OverlappedSuspension | Should Be $false
        Mock Get-WinEvent { New-MwbPowerEventFixture }
        $result = Get-MwbHostPowerEvidence $start $end
        $result.QueryStatus | Should Be 'Succeeded'
        $result.OverlappedSuspension | Should Be $true
        $result.Events.Count | Should Be 1
    }

    It 'keeps PlanOnly free of host power queries and writes clock evidence from final cleanup' {
        $source = $ast.Extent.Text
        ($source.IndexOf('if ($PlanOnly)') -lt $source.IndexOf('$controllerStartedUtc =')) | Should Be $true
        $source | Should Match 'ProvisioningResults\\\$runId-clock.json'
        $source | Should Match 'StopwatchElapsedMilliseconds = \$controllerTimer.Elapsed'
        $source | Should Match 'HostPower = Get-MwbHostPowerEvidence'
    }
}
