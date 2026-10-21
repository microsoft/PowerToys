# Copyright (c) Microsoft Corporation
# The Microsoft Corporation licenses this file to you under the MIT license.
# See the LICENSE file in the project root for more information.

. "$PSScriptRoot\..\MwbSandboxCi.Common.ps1"

Describe 'Shared Release MWB UI-test selection' {
    It 'defaults to the current full build and describes unsupported official sources in the UI' {
        $source = Get-Content -LiteralPath (Join-Path $PSScriptRoot '..\v2\templates\pipeline-ui-tests-automation.yml') -Raw
        $source | Should Match '(?m)^  - name: buildSource\r?\n    type: string\r?\n    default: "buildNow"'
        $source | Should Match 'displayName: "Build Source \(latestMainOfficialBuild and specificBuildId do not support MWB\)"'
    }

    It 'has no independent MWB enable switch in the selection API or pipeline templates' {
        (Get-Command Get-MwbUiTestSelection).Parameters.ContainsKey('Enabled') | Should Be $false
        foreach ($name in @('pipeline-ui-tests-automation.yml', 'pipeline-ui-tests-full-build.yml',
            'pipeline-ui-tests-official-build.yml', 'pipeline-ci-build.yml', 'job-build-project.yml', 'job-test-project.yml')) {
            $source = Get-Content -LiteralPath (Join-Path $PSScriptRoot "..\v2\templates\$name") -Raw
            $source | Should Not Match 'mwbSandboxExperiment'
        }
    }

    It 'derives manual x64 runtime preparation directly from the selected modules' {
        $source = Get-Content -LiteralPath (Join-Path $PSScriptRoot '..\v2\templates\pipeline-ui-tests-full-build.yml') -Raw
        $source | Should Match "prepareMwbRuntime:.*and\(eq\(parameters.platform, 'x64'\), or\(eq\(length\(parameters.uiTestModules\), 0\), containsValue\(parameters.uiTestModules, 'MouseWithoutBorders.UITests'\)\)\)"
    }

    It 'always wires the runtime-gated MWB steps and requires selected-suite results' {
        $source = Get-Content -LiteralPath (Join-Path $PSScriptRoot '..\v2\templates\job-test-project.yml') -Raw
        $source | Should Match '(?m)^  - template: steps-mwb-sandbox-experiment.yml'
        $source | Should Match 'failTaskOnFailedTests: true'
        $source | Should Match 'failTaskOnMissingResultsFile: \$\(RunMwbSandbox\)'
        $source | Should Match 'cancelTimeoutInMinutes: 15'
        $source | Should Not Match '-Enabled'
    }

    It 'runs MWB without dropping all, mixed or affected suites on <Platform> from <BuildSource>' -TestCases @(
        foreach ($platform in @('x64Win10', 'x64Win11')) {
            foreach ($source in @('buildNow', 'buildNowSlim')) {
                @{ Platform = $platform; BuildSource = $source }
            }
        }
    ) {
        param($Platform, $BuildSource)
        foreach ($modules in @(
            @{ Modules = @(); Auto = $false; Generic = $true }
            @{ Modules = @('MouseWithoutBorders.UITests'); Auto = $false; Generic = $false }
            @{ Modules = @('MouseWithoutBorders.UITests', 'ColorPicker.UITests'); Auto = $false; Generic = $true }
            @{ Modules = @('MouseWithoutBorders.UITests', 'ColorPicker.UITests'); Auto = $true; Generic = $true }
        )) {
            $result = Get-MwbUiTestSelection -Modules $modules.Modules -AutoSelect $modules.Auto `
                -Platform $Platform -BuildSource $BuildSource
            $result.Run | Should Be $true
            $result.RunGeneric | Should Be $modules.Generic
            $result.Blocked | Should Be $false
            $result.Reason | Should Be 'Selected'
        }
    }

    It 'skips only MWB on ARM64 for all, mixed and affected selections' {
        foreach ($modules in @(
            @{ Modules = @(); Auto = $false; Generic = $true }
            @{ Modules = @('MouseWithoutBorders.UITests'); Auto = $false; Generic = $false }
            @{ Modules = @('MouseWithoutBorders.UITests', 'ColorPicker.UITests'); Auto = $false; Generic = $true }
            @{ Modules = @('MouseWithoutBorders.UITests', 'ColorPicker.UITests'); Auto = $true; Generic = $true }
        )) {
            $result = Get-MwbUiTestSelection -Modules $modules.Modules -AutoSelect $modules.Auto `
                -Platform arm64 -BuildSource buildNowSlim
            $result.Run | Should Be $false
            $result.RunGeneric | Should Be $modules.Generic
            $result.Blocked | Should Be $false
            $result.Reason | Should Be 'Arm64SandboxImageUnavailable'
        }
    }

    It 'does not turn an empty affected selection into all modules' {
        $result = Get-MwbUiTestSelection -AutoSelect $true -Platform x64Win10 -BuildSource buildNowSlim
        $result.Selected | Should Be $false
        $result.Run | Should Be $false
        $result.RunGeneric | Should Be $false
        $result.Blocked | Should Be $false
    }

    It 'skips implicit MWB without blocking official all-module runs on <Platform> from <Source>' -TestCases @(
        foreach ($platform in @('x64Win10', 'x64Win11')) {
            foreach ($source in @('latestMainOfficialBuild', 'specificBuildId')) {
                @{ Platform = $platform; Source = $source }
            }
        }
    ) {
        param($Platform, $Source)
        $result = Get-MwbUiTestSelection -Modules @() -Platform $Platform -BuildSource $Source
        $result.Run | Should Be $false
        $result.RunGeneric | Should Be $true
        $result.Blocked | Should Be $false
        $result.Reason | Should Be 'SkippedRequiresCurrentBuildBundle'
        $emptyAffected = Get-MwbUiTestSelection -Modules @() -AutoSelect $true -Platform $Platform -BuildSource $Source
        $emptyAffected.Selected | Should Be $false
        $emptyAffected.RunGeneric | Should Be $false
        $emptyAffected.Blocked | Should Be $false
    }

    It 'gates PR bundle preparation on exact resolved MWB selection while allowing non-PR builds' {
        $resolver = Get-Content -LiteralPath (Join-Path $PSScriptRoot '..\v2\templates\job-resolve-ui-tests.yml') -Raw
        $resolver | Should Match '\$modules -contains ''MouseWithoutBorders.UITests'''
        $resolver | Should Match 'variable=hasMwbUiTests;isOutput=true'
        $pipeline = Get-Content -LiteralPath (Join-Path $PSScriptRoot '..\v2\templates\pipeline-ci-build.yml') -Raw
        $pipeline | Should Match 'dependsOn:\s+- Resolve'
        $pipeline | Should Match 'condition: and\(not\(failed\(\)\), not\(canceled\(\)\)\)'
        $pipeline | Should Match 'MwbSandboxSelected: \$\[ dependencies.Resolve.outputs\[''resolve.hasMwbUiTests''\] \]'
        $pipeline | Should Match 'prepareMwbRuntime: \$\{\{ and\(parameters.runAffectedUiTests, eq\(platform, ''x64''\)\) \}\}'
        $pipeline | Should Match 'mwbRuntimeCondition: and\(succeeded\(\), eq\(variables\[''MwbSandboxSelected''\], ''true''\)\)'
        $job = Get-Content -LiteralPath (Join-Path $PSScriptRoot '..\v2\templates\job-build-project.yml') -Raw
        ([regex]::Matches($job, 'condition: \$\{\{ parameters.mwbRuntimeCondition \}\}')).Count | Should Be 3
    }

    It 'does not select MWB for unrelated or similarly named modules' -TestCases @(
        @{ Module = 'ColorPicker.UITests' }
        @{ Module = 'OtherMouseWithoutBorders.UITests' }
        @{ Module = 'MouseWithoutBorders.UITests.Extra' }
    ) {
        param($Module)
        $result = Get-MwbUiTestSelection -Modules $Module -Platform x64Win11 -BuildSource buildNow
        $result.Selected | Should Be $false
        $result.Run | Should Be $false
        $result.RunGeneric | Should Be $true
    }

    It 'preserves ordinary case-insensitive project selection' {
        $result = Get-MwbUiTestSelection -Modules 'mousewithoutborders.uitests' -Platform x64Win10 -BuildSource buildNow
        $result.Run | Should Be $true
        $result.RunGeneric | Should Be $false
    }

    It 'blocks unsupported provenance without silently dropping other selected suites' -TestCases @(
        @{ Platform = 'x64Win10'; Configuration = 'Debug'; Source = 'buildNow'; Reason = 'RequiresReleaseArtifact' }
        @{ Platform = 'x86'; Configuration = 'Release'; Source = 'buildNow'; Reason = 'UnsupportedPlatform' }
        @{ Platform = 'x64Win11'; Configuration = 'Release'; Source = 'latestMainOfficialBuild'; Reason = 'RequiresCurrentBuildBundle' }
        @{ Platform = 'x64Win11'; Configuration = 'Release'; Source = 'specificBuildId'; Reason = 'RequiresCurrentBuildBundle' }
    ) {
        param($Platform, $Configuration, $Source, $Reason)
        $result = Get-MwbUiTestSelection -Modules @('MouseWithoutBorders.UITests', 'ColorPicker.UITests') `
            -Platform $Platform -Configuration $Configuration -BuildSource $Source
        $result.Run | Should Be $false
        $result.RunGeneric | Should Be $true
        $result.Blocked | Should Be $true
        $result.Reason | Should Be $Reason
    }
}
