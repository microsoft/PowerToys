# Copyright (c) Microsoft Corporation
# The Microsoft Corporation licenses this file to you under the MIT license.
# See the LICENSE file in the project root for more information.

Describe 'UI test module selection in pipeline previews' {
    BeforeAll {
        . (Join-Path $PSScriptRoot '..\scripts\AzureDevOps.ps1')
        $probeModule = 'FancyZones.UITests.Next'
        $previewTemplate = @'
stages:
- stage: Build_x64
  jobs: []
- stage: Test_x64Win10_FullBuild
  jobs:
  - job: Testx64Win10Release
    steps:
{0}
- stage: Test_x64Win11_FullBuild
  jobs:
  - job: Testx64Win11Release
    steps:
{1}
'@
        $selectedStep = @'
    - pwsh: $modules = $env:SELECTED_UI_TEST_MODULES
      env:
        SELECTED_UI_TEST_MODULES: {0}
'@
    }

    It 'accepts the exact module in both expanded environment mappings' {
        $yaml = $previewTemplate -f ($selectedStep -f $probeModule), ($selectedStep -f "'$probeModule'")
        $stages = @(Get-AzDevOpsUiTestPreviewStages -FinalYaml $yaml -ProbeModule $probeModule)
        $stages -join ';' | Should Be 'Build_x64;Test_x64Win10_FullBuild;Test_x64Win11_FullBuild'
    }

    It 'rejects an empty or extra module selection in either OS stage' {
        foreach ($wrongValue in @("''", "$probeModule;Other.UITests")) {
            $yaml = $previewTemplate -f ($selectedStep -f $probeModule), ($selectedStep -f $wrongValue)
            { Get-AzDevOpsUiTestPreviewStages -FinalYaml $yaml -ProbeModule $probeModule } |
                Should Throw 'did not resolve only module'
        }
    }

    It 'rejects a runtime reference that has no literal selection in one OS stage' {
        $yaml = $previewTemplate -f ($selectedStep -f $probeModule), '    - pwsh: $modules = $env:SELECTED_UI_TEST_MODULES'
        { Get-AzDevOpsUiTestPreviewStages -FinalYaml $yaml -ProbeModule $probeModule } |
            Should Throw "no literal UI test module selection in 'Test_x64Win11_FullBuild'"
    }

    It 'accepts the literal PowerShell assignments from older probe branches' {
        $legacyStep = @'
    - pwsh: |
        $modulesRaw = '{0}'
'@ -f $probeModule
        $yaml = $previewTemplate -f $legacyStep, $legacyStep
        $stages = @(Get-AzDevOpsUiTestPreviewStages -FinalYaml $yaml -ProbeModule $probeModule)
        $stages.Count | Should Be 3
    }
}
