# Copyright (c) Microsoft Corporation
# The Microsoft Corporation licenses this file to you under the MIT license.
# See the LICENSE file in the project root for more information.

. "$PSScriptRoot\..\MwbSandboxCi.Common.ps1"

Describe 'Shared Release MWB UI-test selection' {
    It 'defaults automation to one current Release build rather than an incompatible older official source' {
        $source = Get-Content -LiteralPath (Join-Path $PSScriptRoot '..\v2\templates\pipeline-ui-tests-automation.yml') -Raw
        $source | Should Match '(?m)^  - name: buildSource\r?\n    type: string\r?\n    default: "buildNowSlim"'
        $source | Should Match 'name: mwbSandboxExperiment\r?\n    type: boolean\r?\n    default: true'
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

    It 'preserves other suites when explicitly disabling MWB' {
        $result = Get-MwbUiTestSelection -Modules @('MouseWithoutBorders.UITests', 'ColorPicker.UITests') `
            -Enabled $false -Platform x64Win10 -BuildSource buildNow
        $result.Selected | Should Be $true
        $result.Run | Should Be $false
        $result.RunGeneric | Should Be $true
        $result.Blocked | Should Be $false
        $result.Reason | Should Be 'ExplicitlyDisabled'
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
