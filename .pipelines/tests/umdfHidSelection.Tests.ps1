# Copyright (c) Microsoft Corporation
# The Microsoft Corporation licenses this file to you under the MIT license.
# See the LICENSE file in the project root for more information.

. (Join-Path $PSScriptRoot '..\umdf-hid-smoke\UmdfHidCi.Common.ps1')

Describe 'Virtual HID suite selection' {
    It 'enables supported x64 input for <Platform>' -TestCases @(
        'x64', 'x64Win10', 'x64Win11' | ForEach-Object { @{ Platform = $_ } }
    ) {
        param($Platform)
        $selection = Get-UmdfHidUiTestSelection -Modules 'GrabAndMove.UITests' -Platform $Platform
        $selection.Run | Should Be $true
        $selection.Exclude | Should Be $false
        $selection.Blocked | Should Be $false
    }

    It 'blocks an explicitly requested unsupported platform' {
        $selection = Get-UmdfHidUiTestSelection -Modules 'GrabAndMove.UITests' -Platform ARM64
        $selection.Run | Should Be $false
        $selection.Exclude | Should Be $true
        $selection.Blocked | Should Be $true
        $selection.Skipped | Should Be $false
    }

    It 'skips only the implicit ARM64 HID suite for <Case>' -TestCases @(
        @{ Case = 'PR selection'; Modules = @('GrabAndMove.UITests'); Auto = $true }
        @{ Case = 'mixed PR selection'; Modules = @('GrabAndMove.UITests', 'ColorPicker.UITests'); Auto = $true }
        @{ Case = 'all suites'; Modules = @(); Auto = $false }
    ) {
        param($Case, $Modules, $Auto)
        $selection = Get-UmdfHidUiTestSelection -Modules $Modules -Platform ARM64 -AutoSelect $Auto
        $selection.Run | Should Be $false
        $selection.Exclude | Should Be $true
        $selection.Blocked | Should Be $false
        $selection.Skipped | Should Be $true
    }

    It 'does not affect unrelated selection <Module>' -TestCases @(
        'Other.UITests', 'GrabAndMove.UITests.Extra' | ForEach-Object { @{ Module = $_ } }
    ) {
        param($Module)
        $selection = Get-UmdfHidUiTestSelection -Modules $Module -Platform ARM64
        $selection.Run | Should Be $false
        $selection.Exclude | Should Be $false
        $selection.Blocked | Should Be $false
        $selection.Skipped | Should Be $false
    }

    It 'preserves case-insensitive project and platform matching' {
        (Get-UmdfHidUiTestSelection -Modules 'grabandmove.uitests' -Platform x64win10).Run | Should Be $true
    }

    It 'runs only GrabAndMove in the dedicated ARM64 job' {
        $selection = Get-UmdfHidUiTestSelection -Modules 'GrabAndMove.UITests' -Platform ARM64 -DedicatedArm64 $true
        $selection.Run | Should Be $true
        $selection.Exclude | Should Be $false
        $selection.Blocked | Should Be $false
        $selection.Skipped | Should Be $false
        $selection.Routed | Should Be $false
    }

    It 'routes ARM64 input away from the ordinary job for <Case>' -TestCases @(
        @{ Case = 'explicit'; Modules = @('GrabAndMove.UITests'); Auto = $false }
        @{ Case = 'PR'; Modules = @('GrabAndMove.UITests'); Auto = $true }
        @{ Case = 'all'; Modules = @(); Auto = $false }
        @{ Case = 'mixed'; Modules = @('GrabAndMove.UITests', 'ColorPicker.UITests'); Auto = $false }
    ) {
        param($Case, $Modules, $Auto)
        $before = $Modules -join ';'
        $selection = Get-UmdfHidUiTestSelection -Modules $Modules -Platform ARM64 -AutoSelect $Auto -RouteArm64 $true
        $selection.Run | Should Be $false
        $selection.Exclude | Should Be $true
        $selection.Routed | Should Be $true
        $selection.Blocked | Should Be $false
        $selection.Skipped | Should Be $false
        ($Modules -join ';') | Should Be $before
    }

    It 'does not route unrelated suites or change x64 behavior' {
        (Get-UmdfHidUiTestSelection -Modules 'Other.UITests' -Platform ARM64 -RouteArm64 $true).Routed | Should Be $false
        $selection = Get-UmdfHidUiTestSelection -Modules 'GrabAndMove.UITests' -Platform x64 -RouteArm64 $true
        $selection.Run | Should Be $true
        $selection.Exclude | Should Be $false
        $selection.Routed | Should Be $false
    }

    It 'rejects unsafe dedicated-job selection <Case>' -TestCases @(
        @{ Case = 'wrong architecture'; Platform = 'x64'; Modules = @('GrabAndMove.UITests'); Auto = $false }
        @{ Case = 'all suites'; Platform = 'ARM64'; Modules = @(); Auto = $false }
        @{ Case = 'another suite'; Platform = 'ARM64'; Modules = @('Other.UITests'); Auto = $false }
        @{ Case = 'mixed suites'; Platform = 'ARM64'; Modules = @('GrabAndMove.UITests', 'Other.UITests'); Auto = $false }
        @{ Case = 'PR override'; Platform = 'ARM64'; Modules = @('GrabAndMove.UITests'); Auto = $true }
    ) {
        param($Case, $Platform, $Modules, $Auto)
        { Get-UmdfHidUiTestSelection -Modules $Modules -Platform $Platform -AutoSelect $Auto -DedicatedArm64 $true } |
            Should Throw 'must select exactly GrabAndMove.UITests'
    }

    It 'accepts the preconfigured dedicated English-language ARM64 image' {
        Assert-UmdfHidArm64TestEnvironment -OsArchitecture ARM64 -BootConfiguration "Windows Boot Loader`r`ntestsigning             Yes`r`n"
    }

    It 'rejects an unprepared dedicated image <Case>' -TestCases @(
        @{ Case = 'x64'; Architecture = 'X64'; Boot = 'testsigning Yes' }
        @{ Case = 'off'; Architecture = 'Arm64'; Boot = 'testsigning No' }
        @{ Case = 'default-off'; Architecture = 'Arm64'; Boot = 'Windows Boot Loader' }
        @{ Case = 'empty'; Architecture = 'Arm64'; Boot = '' }
    ) {
        param($Case, $Architecture, $Boot)
        $failure = $null
        try { Assert-UmdfHidArm64TestEnvironment -OsArchitecture $Architecture -BootConfiguration $Boot }
        catch { $failure = $_ }
        $failure | Should Not BeNullOrEmpty
        $failure.Exception.Message | Should Match 'BLOCKED_INFRASTRUCTURE'
    }

    It 'acquires only host tools and requested target packages for <Platform>' -TestCases @(
        @{ Platform = 'x64'; Expected = 3 }
        @{ Platform = 'ARM64'; Expected = 5 }
    ) {
        param($Platform, $Expected)
        $tokens = $null
        $errors = $null
        $build = [Management.Automation.Language.Parser]::ParseFile(
            (Join-Path $PSScriptRoot '..\umdf-hid-smoke\Build-Smoke.ps1'), [ref]$tokens, [ref]$errors)
        $statements = @($build.EndBlock.Statements | Where-Object {
            $_.Extent.Text.StartsWith('$ids = ') -or
            ($_.Extent.Text.StartsWith('if ($Platforms -contains ''ARM64'')'))
        })
        $statements.Count | Should Be 2
        $Platforms = @($Platform)
        . ([scriptblock]::Create(($statements.Extent.Text -join "`n")))
        $ids.Count | Should Be $Expected
        $ids -contains 'Microsoft.Windows.WDK.x64' | Should Be $true
        ($ids -contains 'Microsoft.Windows.WDK.arm64') | Should Be ($Platform -eq 'ARM64')
    }

    It 'builds the x64 PR prerequisite only after Resolve selects the HID suite' {
        $templates = Join-Path $PSScriptRoot '..\v2\templates'
        $resolve = Get-Content (Join-Path $templates 'job-resolve-ui-tests.yml') -Raw
        $resolve | Should Match '\$hasUmdfHidUiTests = if \(\$modules -contains ''GrabAndMove.UITests''\)'
        $resolve | Should Match 'hasUmdfHidUiTests;isOutput=true'
        $ci = Get-Content (Join-Path $templates 'pipeline-ci-build.yml') -Raw
        $ci | Should Match "prepareUmdfHid:.*parameters.runAffectedUiTests, or\(eq\(platform, 'x64'\), and\(eq\(platform, 'arm64'\), ne\(parameters.umdfHidArm64Image, 'disabled'\)\)\)"
        $ci | Should Match "UmdfHidSelected:.*dependencies.Resolve.outputs\['resolve.hasUmdfHidUiTests'\]"
        $ci | Should Match "umdfHidCondition:.*eq\(variables\['UmdfHidSelected'\], 'true'\)"
        $build = Get-Content (Join-Path $templates 'job-build-project.yml') -Raw
        $build | Should Match 'condition: \$\{\{ parameters.umdfHidCondition \}\}'
        (Get-Content (Join-Path $templates 'steps-build-umdf-hid.yml') -Raw) |
            Should Match 'condition: \$\{\{ parameters.condition \}\}'
    }

    It 'filters only the unsupported HID runner after preserving original matches' {
        $template = Get-Content (Join-Path $PSScriptRoot '..\v2\templates\job-test-project.yml') -Raw
        $filter = [regex]::Match($template, '(?ms)^ {6}\$matchedEntries =.*?^ {6}\}').Value
        $filter | Should Not BeNullOrEmpty
        $original = $env:EXCLUDE_UMDF_HID_SUITE
        try {
            $env:EXCLUDE_UMDF_HID_SUITE = 'true'
            $entries = @(
                [pscustomobject]@{ Name = 'GrabAndMove.UITests.runtimeconfig.json' }
                [pscustomobject]@{ Name = 'ColorPicker.UITests.runtimeconfig.json' }
            )
            . ([scriptblock]::Create($filter))
            $matchedEntries | Should Be 2
            @($entries).Count | Should Be 1
            $entries[0].Name | Should Be 'ColorPicker.UITests.runtimeconfig.json'
        }
        finally { $env:EXCLUDE_UMDF_HID_SUITE = $original }
    }
}
