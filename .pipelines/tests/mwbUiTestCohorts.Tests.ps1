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
        $repository = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
        $yaml = Get-Content (Join-Path $repository '.pipelines\v2\templates\job-test-project.yml') -Raw
        $block = [regex]::Match($yaml, '(?ms)^      # Filter execution only:.*?(?=^      \$resultsDir =)').Value
        if (-not $block) { throw 'Diagnostic execution block was not found.' }
        $entries = @(
            [pscustomobject]@{ Name = 'AdvancedPaste.UITests.Next.runtimeconfig.json' }
            [pscustomobject]@{ Name = 'PreviewPane.UITests.runtimeconfig.json' }
        )
        $modulesRaw = 'AdvancedPaste.UITests.Next;PreviewPane.UITests;MouseWithoutBorders.UITests'
        $runMwbSandbox = $true
        $shardCount = 1
    }

    It 'keeps desktop execution separate from Sandbox execution' {
        $block = $block.Replace('${{ join('';'', parameters.uiTestExecutionModules) }}',
            'AdvancedPaste.UITests.Next;PreviewPane.UITests').Replace('${{ parameters.uiTestDiagnosticCohort }}', 'True')
        $block = $block.Replace('$(Build.SourcesDirectory)', $repository)
        . ([scriptblock]::Create($block))

        $entries.Count | Should Be 2
        $runMwbSandbox | Should Be $false
        $modulesRaw | Should Be 'AdvancedPaste.UITests.Next;PreviewPane.UITests;MouseWithoutBorders.UITests'
    }

    It 'runs only Sandbox while preserving the original installer and signing selection' {
        $block = $block.Replace('${{ join('';'', parameters.uiTestExecutionModules) }}',
            'MouseWithoutBorders.UITests').Replace('${{ parameters.uiTestDiagnosticCohort }}', 'True')
        $block = $block.Replace('$(Build.SourcesDirectory)', $repository)
        . ([scriptblock]::Create($block))

        $entries.Count | Should Be 0
        $runMwbSandbox | Should Be $true
        $modulesRaw | Should Be 'AdvancedPaste.UITests.Next;PreviewPane.UITests;MouseWithoutBorders.UITests'
    }

    It 'rejects an empty diagnostic request instead of running the entire selection' {
        $block = $block.Replace('${{ join('';'', parameters.uiTestExecutionModules) }}',
            '').Replace('${{ parameters.uiTestDiagnosticCohort }}', 'True')
        $block = $block.Replace('$(Build.SourcesDirectory)', $repository)
        $emptyCohortScript = [scriptblock]::Create($block)

        $refusal = $null
        try {
            . $emptyCohortScript
        }
        catch {
            $refusal = $_.Exception.Message
        }
        $refusal | Should Match 'Diagnostic cohort suite is unavailable|at least one suite'
    }

    It 'does not alter normal execution when diagnostics are disabled' {
        $block = $block.Replace('${{ join('';'', parameters.uiTestExecutionModules) }}',
            '').Replace('${{ parameters.uiTestDiagnosticCohort }}', 'False')
        . ([scriptblock]::Create($block))

        $entries.Count | Should Be 2
        $runMwbSandbox | Should Be $true
    }
}
