# Copyright (c) Microsoft Corporation
# The Microsoft Corporation licenses this file to you under the MIT license.
# See the LICENSE file in the project root for more information.

#Requires -Version 7.0
. "$PSScriptRoot\..\MwbSandboxCi.Common.ps1"
. "$PSScriptRoot\..\MwbPilotResults.Common.ps1"

Describe 'MWB full-suite results gate' {
    BeforeEach {
        $root = (New-Item -ItemType Directory -Path (Join-Path $TestDrive ([guid]::NewGuid().ToString('N')))).FullName
        $id = [guid]::NewGuid()
        $directory = (New-Item -ItemType Directory -Path (Join-Path $root "ui-$($id.ToString('N').Substring(0, 12))")).FullName
        $path = Join-Path $directory 'pilot.trx'
        $names = @('AutonomousSandboxSmoke', 'Infrastructure')
    }

    It 'requires the complete current-job suite with all outcomes passing' {
        @'
<TestRun xmlns="http://microsoft.com/schemas/VisualStudio/TeamTest/2010">
<Results><UnitTestResult testName="AutonomousSandboxSmoke" outcome="Passed"/><UnitTestResult testName="Infrastructure" outcome="Passed"/></Results>
<ResultSummary><Counters total="2" executed="2" passed="2" failed="0" notExecuted="0"/></ResultSummary>
</TestRun>
'@ | Set-Content -LiteralPath $path
        $counts = Assert-MwbPilotTestResults $root $id -RequiredMethods $names
        $counts.Total | Should Be 2
        $counts.Passed | Should Be 2
    }

    It 'rejects a smoke-only success even when its process exited zero' {
        '<TestRun><Results><UnitTestResult testName="AutonomousSandboxSmoke" outcome="Passed"/></Results><ResultSummary><Counters total="1" executed="1" passed="1" failed="0" notExecuted="0"/></ResultSummary></TestRun>' |
            Set-Content -LiteralPath $path
        { Assert-MwbPilotTestResults $root $id -RequiredMethods $names } | Should Throw 'every test to execute'
    }

    It 'rejects skipped, failed, inconclusive, or mismatched execution evidence' -TestCases @(
        @{ Outcome = 'NotExecuted'; Executed = 1; Passed = 1; Failed = 0; NotExecuted = 1 }
        @{ Outcome = 'Failed'; Executed = 2; Passed = 1; Failed = 1; NotExecuted = 0 }
        @{ Outcome = 'Inconclusive'; Executed = 2; Passed = 1; Failed = 0; NotExecuted = 0 }
        @{ Outcome = 'Passed'; Executed = 2; Passed = 1; Failed = 0; NotExecuted = 0 }
    ) {
        param($Outcome, $Executed, $Passed, $Failed, $NotExecuted)
        "<TestRun><Results><UnitTestResult testName=`"AutonomousSandboxSmoke`" outcome=`"Passed`"/><UnitTestResult testName=`"Infrastructure`" outcome=`"$Outcome`"/></Results><ResultSummary><Counters total=`"2`" executed=`"$Executed`" passed=`"$Passed`" failed=`"$Failed`" notExecuted=`"$NotExecuted`"/></ResultSummary></TestRun>" |
            Set-Content -LiteralPath $path
        { Assert-MwbPilotTestResults $root $id -RequiredMethods $names } | Should Throw 'every test to execute'
    }

    It 'rejects missing required methods despite enough passing tests' {
        '<TestRun><Results><UnitTestResult testName="Other" outcome="Passed"/><UnitTestResult testName="Infrastructure" outcome="Passed"/></Results><ResultSummary><Counters total="2" executed="2" passed="2" failed="0" notExecuted="0"/></ResultSummary></TestRun>' |
            Set-Content -LiteralPath $path
        { Assert-MwbPilotTestResults $root $id -RequiredMethods $names } | Should Throw 'missing required test'
    }

    It 'does not reuse a successful report from another job' {
        '<TestRun/>' | Set-Content -LiteralPath (Join-Path $root 'old-success.trx')
        { Assert-MwbPilotTestResults $root $id -RequiredMethods $names } | Should Throw 'exactly one current-job TRX'
    }

    It 'rejects multiple reports instead of choosing a convenient passing one' {
        '<TestRun/>' | Set-Content -LiteralPath $path
        '<TestRun/>' | Set-Content -LiteralPath (Join-Path $directory 'duplicate.trx')
        { Assert-MwbPilotTestResults $root $id -RequiredMethods $names } | Should Throw 'exactly one current-job TRX'
    }

    It 'rejects missing or invalid execution counters' -TestCases @(
        @{ Summary = '' }
        @{ Summary = '<ResultSummary><Counters total="bad" executed="2" passed="2" failed="0" notExecuted="0"/></ResultSummary>' }
        @{ Summary = '<ResultSummary><Counters total="2" executed="2" passed="2" failed="-1" notExecuted="0"/></ResultSummary>' }
    ) {
        param($Summary)
        "<TestRun>$Summary</TestRun>" | Set-Content -LiteralPath $path
        { Assert-MwbPilotTestResults $root $id -RequiredMethods $names } | Should Throw 'execution counters'
    }

    It 'does not resolve external entities while loading test evidence' {
        '<!DOCTYPE TestRun [<!ENTITY outside SYSTEM "file:///does-not-exist">]><TestRun>&outside;</TestRun>' |
            Set-Content -LiteralPath $path
        { Assert-MwbPilotTestResults $root $id -RequiredMethods $names } | Should Throw 'DTD'
    }

    It 'has no smoke filter in the dispatch and checks results after the child completes' {
        $source = Get-Content -LiteralPath (Join-Path $PSScriptRoot '..\mwbSandboxExperiment.ps1') -Raw
        $source | Should Not Match "-Filter 'FullyQualifiedName~AutonomousSandboxSmoke'"
        $source | Should Match 'Assert-MwbPilotTestResults -ResultsDirectory'
        ($source.IndexOf('$testExitCode = $LASTEXITCODE') -lt $source.IndexOf('$counts = Assert-MwbPilotTestResults')) | Should Be $true
    }

    It 'enables MWB by default without restricting callers to an explicit standalone suite' {
        $source = Get-Content -LiteralPath (Join-Path $PSScriptRoot '..\v2\templates\pipeline-ui-tests-automation.yml') -Raw
        $source | Should Match 'name: mwbSandboxExperiment\r?\n    type: boolean\r?\n    default: true'
        $source | Should Match 'mwbSandboxExperiment: \$\{\{ parameters.mwbSandboxExperiment \}\}'
        $source | Should Match "eq\(parameters.buildSource, 'buildNow'\)"
        $source | Should Not Match 'Debug|exactly uiTestModules|InvalidMwbSandboxSelection'
    }

    It 'records ARM64 as skipped without skipping its other module builds or suites' {
        $selection = Get-MwbUiTestSelection -Platform arm64 -BuildSource buildNow -Modules @(
            'MouseWithoutBorders.UITests', 'ColorPicker.UITests')
        $selection.Run | Should Be $false
        $selection.RunGeneric | Should Be $true
        $selection.Reason | Should Be 'Arm64SandboxImageUnavailable'
        $source = Get-Content -LiteralPath (Join-Path $PSScriptRoot '..\v2\templates\pipeline-ui-tests-full-build.yml') -Raw
        $source | Should Match 'stage: Build_\$\{\{ parameters.platform \}\}'
        $source | Should Match 'stage: Test_\$\{\{ parameters.platform \}\}_FullBuild'
        $source | Should Not Match 'SkipMwbSandbox_arm64'
    }

    It 'keeps an explicit pilot opt-out skipped instead of failing selection' {
        $selection = Get-MwbUiTestSelection -Enabled $false -Platform x64Win11 -BuildSource buildNow `
            -Modules @('MouseWithoutBorders.UITests', 'ColorPicker.UITests')
        $selection.Run | Should Be $false
        $selection.RunGeneric | Should Be $true
        $selection.Blocked | Should Be $false
        $selection.Reason | Should Be 'ExplicitlyDisabled'
    }

    It 'uses one Release artifact for mixed-platform and mixed-module selections' {
        $source = Get-Content -LiteralPath (Join-Path $PSScriptRoot '..\v2\templates\pipeline-ui-tests-automation.yml') -Raw
        $source | Should Match 'each platform in parameters.buildPlatforms'
        $source | Should Match 'uiTestModules: \$\{\{ parameters.uiTestModules \}\}'
        $fullBuild = Get-Content -LiteralPath (Join-Path $PSScriptRoot '..\v2\templates\pipeline-ui-tests-full-build.yml') -Raw
        $fullBuild | Should Match 'buildConfigurations: \[Release\]'
        $fullBuild | Should Not Match 'Debug|buildInstallers: false'
    }

    It 'requires all recovery methods and the stale-journal guard as well as the ordered smoke' {
        $source = Get-Content -LiteralPath (Join-Path $PSScriptRoot '..\MwbPilotResults.Common.ps1') -Raw
        $function = [scriptblock]::Create($source).Ast.Find({
            param($node)
            $node -is [Management.Automation.Language.FunctionDefinitionAst] -and
                $node.Name -eq 'Assert-MwbPilotTestResults'
        }, $true)
        $names = $function.Body.ParamBlock.Parameters |
            Where-Object { $_.Name.VariablePath.UserPath -eq 'RequiredMethods' } |
            ForEach-Object { $_.DefaultValue.SafeGetValue() }
        $names.Count | Should Be 11
        ($names -ccontains 'AutonomousSandboxSmoke') | Should Be $true
        ($names -ccontains 'ControllerExitBeforePairingRestoresOriginalSettings') | Should Be $true
        ($names -ccontains 'KilledClipboardOwnerRequiresBaselineReset') | Should Be $true
        ($names -ccontains 'AbortedSandboxStartupRefusesUnownedInstance') | Should Be $true
        ($names -ccontains 'StaleRunDirectoryRefusalPreservesRecoveryJournals') | Should Be $true
    }
}
