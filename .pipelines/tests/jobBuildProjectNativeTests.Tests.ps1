# Copyright (c) Microsoft Corporation
# The Microsoft Corporation licenses this file to you under the MIT license.
# See the LICENSE file in the project root for more information.

$template = Get-Content -LiteralPath (Join-Path $PSScriptRoot '..\v2\templates\job-build-project.yml') -Raw
$nativeTask = [regex]::Match($template, '(?ms)^    - task: VSTest@2\r?\n.*?(?=^  - pwsh:|\z)').Value
if ([string]::IsNullOrWhiteSpace($nativeTask)) {
    throw 'The native test task could not be located.'
}

Describe 'Native test task diagnostic inputs' {
    It 'keeps test assemblies while excluding generated resource assemblies' {
        $nativeTask.Contains('**\KeyboardManagerEngineTest.dll') | Should Be $true
        $nativeTask.Contains('**\KeyboardManagerEditorTest.dll') | Should Be $true
        $nativeTask.Contains('**\*UnitTest*.dll') | Should Be $true
        $nativeTask.Contains('!**\obj\**') | Should Be $true
        $nativeTask.Contains('!**\*.resources.dll') | Should Be $true
    }

    It 'writes informational diagnostics into the existing log artifact directory' {
        $nativeTask.Contains('$(Build.ArtifactStagingDirectory)\logs\native-tests.diag.log') | Should Be $true
        $nativeTask.Contains('tracelevel=info') | Should Be $true
        $nativeTask.Contains('CollectDump') | Should Be $false
        $nativeTask.Contains('TestSessionTimeout') | Should Be $false
    }

    It 'scopes temporary stack collection to this PR and always stops the watcher' {
        $template.Contains("-Action Start") | Should Be $true
        $template.Contains("-Action Stop") | Should Be $true
        $template.Contains("condition: and(always(), ne(variables['BuildPlatform'], 'arm64'), eq(variables['System.PullRequest.PullRequestNumber'], '50508'))") | Should Be $true
        $template.Contains('-BinariesDirectory "$(Build.SourcesDirectory)\$(BuildPlatform)\$(BuildConfiguration)"') | Should Be $true
    }
}
