# Copyright (c) Microsoft Corporation
# The Microsoft Corporation licenses this file to you under the MIT license.
# See the LICENSE file in the project root for more information.

. (Join-Path $PSScriptRoot '..\UiTestShards.Common.ps1')

Describe 'Controlled UI execution cohorts' {
    It 'selects a desktop cohort without executing the Sandbox suite' {
        $result = @(Get-UiTestExecutionCohort `
            -AvailableSuites @('AdvancedPaste.UITests.Next', 'PreviewPane.UITests', 'MouseWithoutBorders.UITests') `
            -ExecutionSuites @('AdvancedPaste.UITests.Next', 'PreviewPane.UITests') -ShardCount 1)

        $result.Count | Should Be 2
        $result[0] | Should Be 'AdvancedPaste.UITests.Next'
        $result[1] | Should Be 'PreviewPane.UITests'
    }

    It 'allows the Sandbox alone from the same prerequisite selection' {
        $result = @(Get-UiTestExecutionCohort `
            -AvailableSuites @('AdvancedPaste.UITests.Next', 'MouseWithoutBorders.UITests') `
            -ExecutionSuites @('MouseWithoutBorders.UITests') -ShardCount 1)

        $result.Count | Should Be 1
        $result[0] | Should Be 'MouseWithoutBorders.UITests'
    }

    It 'refuses a cohort that sharding could silently truncate' {
        { Get-UiTestExecutionCohort -AvailableSuites @('A') -ExecutionSuites @('A') -ShardCount 2 } |
            Should Throw 'one unsharded job'
    }

    It 'refuses an unavailable suite' {
        { Get-UiTestExecutionCohort -AvailableSuites @('A') -ExecutionSuites @('B') -ShardCount 1 } |
            Should Throw 'unavailable'
    }

    It 'refuses an empty execution cohort' {
        { Get-UiTestExecutionCohort -AvailableSuites @('A') -ExecutionSuites @() -ShardCount 1 } |
            Should Throw 'at least one suite'
    }

    It 'refuses a blank suite rather than dropping it' {
        { Get-UiTestExecutionCohort -AvailableSuites @('A') -ExecutionSuites @(' ') -ShardCount 1 } |
            Should Throw 'unavailable'
    }

    It 'refuses duplicate suite names regardless of case' {
        { Get-UiTestExecutionCohort -AvailableSuites @('A') -ExecutionSuites @('A', 'a') -ShardCount 1 } |
            Should Throw 'Duplicate'
    }
}

Describe 'Diagnostic execution leaves prerequisite selection unchanged' {
    BeforeEach {
        $entries = @(
            [pscustomobject]@{ Name = 'AdvancedPaste.UITests.Next.runtimeconfig.json' }
            [pscustomobject]@{ Name = 'PreviewPane.UITests.runtimeconfig.json' }
        )
    }

    It 'keeps desktop execution separate from Sandbox execution' {
        $plan = Select-UiTestExecutionPlan $entries $true -DiagnosticCohort -ExecutionModules 'AdvancedPaste.UITests.Next','PreviewPane.UITests'
        $plan.Entries.Count | Should Be 2
        $plan.RunMwbSandbox | Should Be $false
    }

    It 'runs only Sandbox while preserving the original installer and signing selection' {
        $plan = Select-UiTestExecutionPlan $entries $true -DiagnosticCohort -ExecutionModules 'MouseWithoutBorders.UITests'
        $plan.Entries.Count | Should Be 0
        $plan.RunMwbSandbox | Should Be $true
        $entries.Count | Should Be 2
    }

    It 'rejects an empty diagnostic request instead of running the entire selection' {
        { Select-UiTestExecutionPlan $entries $true -DiagnosticCohort } | Should Throw 'at least one suite'
    }

    It 'does not alter normal execution when diagnostics are disabled' {
        $plan = Select-UiTestExecutionPlan $entries $true
        $plan.Entries.Count | Should Be 2
        $plan.RunMwbSandbox | Should Be $true
    }

    It 'assigns each runtime entry and Sandbox to exactly one executing worker' {
        $jobs = @(1..3 | ForEach-Object { Select-UiTestExecutionPlan $entries $true -ShardCount 3 -ShardIndex $_ })
        @($jobs | Where-Object RunMwbSandbox).Count | Should Be 1
        ($jobs.Entries.Name | Sort-Object) -join ',' | Should Be (($entries.Name | Sort-Object) -join ',')
    }
}

Describe 'Deterministic UI suite sharding' {
    BeforeEach {
        $durations = Join-Path $TestDrive 'durations.json'
        '{"suites":{"A":8,"B":6,"C":3,"D":3},"defaultMinutes":2,"platformMultipliers":{"arm64":1.5}}' |
            Set-Content -LiteralPath $durations
    }

    It 'makes a complete disjoint stable partition for <Count> shards' -TestCases @(1..6 | ForEach-Object { @{ Count = $_ } }) {
        param($Count)
        $names = @('D','A','E','B','F','C')
        $plan = @(Get-UiTestShardPlan $names $Count -DurationsPath $durations)
        $plan.Count | Should Be $Count
        (@($plan | ForEach-Object { $_.Suites } | Sort-Object) -join ',') | Should Be 'A,B,C,D,E,F'
        $expected = ($plan | ForEach-Object { $_.Suites -join ',' }) -join ';'
        [array]::Reverse($names)
        foreach ($platform in @('x64Win11','arm64')) {
            $again = @(Get-UiTestShardPlan $names $Count -Platform $platform -DurationsPath $durations)
            (($again | ForEach-Object { $_.Suites -join ',' }) -join ';') | Should Be $expected
        }
    }

    It 'packs heaviest first with a default weight for unknown suites and scales the displayed estimate' {
        $plan = @(Get-UiTestShardPlan @('E','B','A','F','D','C') 2 -Platform arm64 -DurationsPath $durations)
        ($plan[0].Suites -join ',') | Should Be 'A,D,F'
        ($plan[1].Suites -join ',') | Should Be 'B,C,E'
        $plan[0].Minutes | Should Be 19.5
        $plan[1].Minutes | Should Be 16.5
    }

    It 'rejects a shard index outside the plan' {
        { Get-UiTestShardSuites @('A') -ShardIndex 3 -ShardCount 2 -DurationsPath $durations } | Should Throw 'outside'
    }
}
