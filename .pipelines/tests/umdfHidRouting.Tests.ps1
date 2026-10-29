# Copyright (c) Microsoft Corporation
# The Microsoft Corporation licenses this file to you under the MIT license.
# See the LICENSE file in the project root for more information.

$templates = Join-Path $PSScriptRoot '..\v2\templates'

Describe 'Dedicated ARM64 virtual HID routing' {
    It 'pins the TESTSIGNING image in the existing ARM64 pools' {
        foreach ($root in @('pipeline-ci-build.yml', 'pipeline-ui-tests-automation.yml')) {
            $source = Get-Content (Join-Path $templates $root) -Raw
            $source | Should Match 'name: umdfHidArm64Image\r?\n\s+type: string\r?\n\s+default: SHINE-W11-Preview-Testing-arm64-TestSigning'
            $source | Should Not Match 'variables-umdf-hid.yml'
        }
    }

    It 'passes image configuration explicitly through every template boundary' {
        foreach ($name in @('pipeline-ci-build.yml', 'pipeline-ui-tests-automation.yml',
            'pipeline-ui-tests-full-build.yml', 'pipeline-ui-tests-official-build.yml', 'job-test-arm64.yml')) {
            $source = Get-Content (Join-Path $templates $name) -Raw
            $source | Should Match 'umdfHidArm64Image: \$\{\{ parameters.umdfHidArm64Image \}\}'
            $source | Should Not Match 'variables.UmdfHidArm64Image'
        }
    }

    It 'isolates a literal one-suite selection and never inherits all/mixed/PR modules' {
        $wrapper = Get-Content (Join-Path $templates 'job-test-arm64.yml') -Raw
        $dedicated = $wrapper.Substring($wrapper.IndexOf('  - ${{ if and(ne(parameters.umdfHidArm64Image'))
        $dedicated | Should Match 'uiTestModules: \[GrabAndMove.UITests\]'
        $dedicated | Should Match 'umdfHidArm64Dedicated: true'
        $dedicated | Should Not Match '(?m)^\s+autoSelectUiTests:'
        $dedicated | Should Match "resolve.hasUmdfHidUiTests'\], 'true'"
        $dedicated | Should Match 'jobSuffix: \$\{\{ parameters.jobSuffix \}\}_UmdfHid'
        $wrapper | Should Match "resolve.hasNonUmdfHidUiTests'\], 'true'"
        $wrapper | Should Match 'routeUmdfHidArm64: \$\{\{ ne\(parameters.umdfHidArm64Image'
        # Preserve the producer's artifact-name casing, rather than silently forcing ARM64.
        ([regex]::Matches($wrapper, 'platform: \$\{\{ parameters.platform \}\}')).Count | Should Be 2
    }

    It 'separates all full/reused-product and PR ARM64 entry points' {
        $full = Get-Content (Join-Path $templates 'pipeline-ui-tests-full-build.yml') -Raw
        $official = Get-Content (Join-Path $templates 'pipeline-ui-tests-official-build.yml') -Raw
        $ci = Get-Content (Join-Path $templates 'pipeline-ci-build.yml') -Raw
        ([regex]::Matches($full, 'template: job-test-arm64.yml')).Count | Should Be 1
        ([regex]::Matches($official, 'template: job-test-arm64.yml')).Count | Should Be 2
        $official | Should Match "installMode: 'peruser'\r?\n\s+jobSuffix: '_PerUser'"
        ([regex]::Matches($ci, 'template: job-test-arm64.yml')).Count | Should Be 1
        ([regex]::Matches($ci, 'template: job-test-project.yml')).Count | Should Be 2
        ([regex]::Matches($full, 'template: job-test-project.yml')).Count | Should Be 2
        ([regex]::Matches($official, 'template: job-test-project.yml')).Count | Should Be 4
    }

    It 'builds native ARM64 artifacts only when the separate image is configured' {
        $full = Get-Content (Join-Path $templates 'pipeline-ui-tests-full-build.yml') -Raw
        $ci = Get-Content (Join-Path $templates 'pipeline-ci-build.yml') -Raw
        foreach ($source in @($full, $ci)) {
            $source | Should Match "prepareUmdfHid:.*eq\((parameters.platform|platform), 'arm64'\), ne\(parameters.umdfHidArm64Image, ''\)"
        }
        $testsOnly = Get-Content (Join-Path $templates 'job-build-ui-tests.yml') -Raw
        $testsOnly | Should Match "eq\(variables\['BuildPlatform'\], 'arm64'\), ne\('\$\{\{ parameters.umdfHidArm64Image \}\}', ''\)"
    }

    It 'selects the dedicated image without changing the existing default pool branches' {
        $job = Get-Content (Join-Path $templates 'job-test-project.yml') -Raw
        $job | Should Match "if eq\(parameters.umdfHidArm64Dedicated, true\)"
        $job | Should Match 'demands: ImageOverride -equals \$\{\{ parameters.umdfHidArm64Image \}\}'
        $job | Should Match 'name: SHINE-INT-Testing-arm64\r?\n'
        $job | Should Match 'name: SHINE-OSS-Testing-arm64\r?\n'
        $job | Should Match 'Assert-UmdfHidArm64TestEnvironment'
    }

    It 'resolves empty, HID-only and mixed PR selections without expanding empty selections' -TestCases @(
        @{ Modules = @(); Hid = 'false'; Other = 'false' }
        @{ Modules = @('GrabAndMove.UITests'); Hid = 'true'; Other = 'false' }
        @{ Modules = @('Other.UITests'); Hid = 'false'; Other = 'true' }
        @{ Modules = @('GrabAndMove.UITests', 'Other.UITests'); Hid = 'true'; Other = 'true' }
    ) {
        param($Modules, $Hid, $Other)
        $resolve = Get-Content (Join-Path $templates 'job-resolve-ui-tests.yml') -Raw
        $assignments = [regex]::Matches($resolve, '(?m)^\s+\$has(?:Non)?UmdfHidUiTests = .+$').Value
        @($assignments).Count | Should Be 2
        . ([scriptblock]::Create(($assignments -join "`n")))
        $hasUmdfHidUiTests | Should Be $Hid
        $hasNonUmdfHidUiTests | Should Be $Other
        $resolve | Should Match 'hasNonUmdfHidUiTests;isOutput=true'
    }

    It 'never changes boot/signing policy and routes the ARM64 transport probe separately too' {
        $job = Get-Content (Join-Path $templates 'job-test-project.yml') -Raw
        $smoke = Get-Content (Join-Path $templates 'pipeline-umdf-hid-smoke.yml') -Raw
        foreach ($source in @($job, $smoke)) {
            $source | Should Match "bcdedit.exe /enum '\{current\}'"
            $source | Should Not Match '(?i)bcdedit(\.exe)?\s+/(set|deletevalue)'
        }
        $smoke | Should Match 'name: SHINE-INT-Testing-arm64\r?\n'
        $smoke | Should Match 'name: SHINE-OSS-Testing-arm64\r?\n'
        $smoke | Should Match 'configure the dedicated ARM64 UMDF image'
    }
}
