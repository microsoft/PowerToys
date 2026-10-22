# Copyright (c) Microsoft Corporation
# The Microsoft Corporation licenses this file to you under the MIT license.
# See the LICENSE file in the project root for more information.

#Requires -Version 7.0
. "$PSScriptRoot\..\mwb\MwbSandboxCi.Common.ps1"
. "$PSScriptRoot\..\mwb\MwbSandboxClientSetup.Common.ps1"

function Get-UiTestFlowStep {
    param([string] $Name)

    $yaml = Get-Content "$PSScriptRoot\..\v2\templates\job-test-project.yml" -Raw
    $steps = [regex]::Matches($yaml, '(?ms)^  - pwsh: \|\r?\n(?<script>.*?)(?=^    displayName:)    displayName: "?(?<name>[^"\r\n]+)"?')
    $step = @($steps | Where-Object { $_.Groups['name'].Value -eq $Name })
    if ($step.Count -ne 1) { throw "Expected one pipeline step named $Name." }
    [regex]::Replace($step[0].Groups['script'].Value, '(?m)^      ', '')
}

Describe 'Bounded isolated MWB pipeline phases' {
    It 'isolates environment changes and propagates nonzero exit codes' {
        $path = Join-Path $TestDrive 'phase.ps1'
        @'
param([string] $Value)
if ($Value -cne 'value with spaces & punctuation') { exit 29 }
$env:POWERTOYS_INSTALL_DIR = 'must-not-leak'
exit 23
'@ | Set-Content $path
        $original = $env:POWERTOYS_INSTALL_DIR
        { Invoke-MwbCiPhase $path @('-Value', 'value with spaces & punctuation') 20 } | Should Throw 'exit 23'
        $env:POWERTOYS_INSTALL_DIR | Should Be $original
    }

    It 'terminates only the owned phase process tree on timeout' {
        $path = Join-Path $TestDrive 'timeout.ps1'
        $pidPath = Join-Path $TestDrive 'child-pid.txt'
        @'
param([string] $PidPath)
$start = [Diagnostics.ProcessStartInfo]::new((Get-Process -Id $PID).Path)
$start.UseShellExecute = $false
foreach ($value in @('-NoProfile', '-Command', 'Start-Sleep -Seconds 60')) { $start.ArgumentList.Add($value) }
$child = [Diagnostics.Process]::Start($start)
$child.Id | Set-Content $PidPath
Start-Sleep -Seconds 60
'@ | Set-Content $path
        try {
            { Invoke-MwbCiPhase $path @('-PidPath', $pidPath) 8 } | Should Throw 'exceeded 8 seconds'
            Test-Path $pidPath | Should Be $true
            $childId = [int](Get-Content $pidPath)
            Get-Process -Id $childId -ErrorAction SilentlyContinue | Should BeNullOrEmpty
        }
        finally {
            if (Test-Path $pidPath) {
                $child = Get-Process -Id ([int](Get-Content $pidPath)) -ErrorAction SilentlyContinue
                if ($child) { Stop-Process -Id $child.Id -Force }
            }
        }
    }

    It 'preserves setup and suite deadlines on <Platform>' -TestCases @(
        @{ Platform = 'x64Win10'; Expected = 'Prepare:900,Run:6180' }
        @{ Platform = 'x64Win11'; Expected = 'Install:720,Prepare:900,Run:6180' }
    ) {
        param($Platform, $Expected)
        $script:phases = @()
        Mock Invoke-MwbCiPhase {
            param($ScriptPath, $Arguments, $TimeoutSeconds)
            $name = if ($Arguments[0] -eq '-Mode') { $Arguments[1] } else { 'Install' }
            $script:phases += "${name}:$TimeoutSeconds"
        }
        Invoke-MwbUiTestSuite -RunId ([guid]::NewGuid()) -ArtifactRoot $TestDrive -ResultsDirectory $TestDrive `
            -Platform $Platform -SourceRevision 'revision' -CertificateMarkerPath (Join-Path $TestDrive 'marker')
        ($script:phases -join ',') | Should Be $Expected
    }

    It 'retains independent recovery, cleanup and export deadlines after a recovery error' {
        $script:phases = @()
        Mock Invoke-MwbCiPhase {
            param($ScriptPath, $Arguments, $TimeoutSeconds)
            $name = if ($Arguments[0] -eq '-Mode') { $Arguments[1] } else { 'Diagnostics' }
            $script:phases += "${name}:$TimeoutSeconds"
            if ($name -eq 'Recover') { throw 'Injected recovery failure.' }
        }
        Mock Write-Host {}
        { Restore-MwbUiTestPrerequisites -RunId ([guid]::NewGuid()) -ResultsDirectory $TestDrive } |
            Should Throw 'Injected recovery failure'
        ($script:phases -join ',') | Should Be 'Recover:360,Cleanup:300,Diagnostics:120'
    }
}

Describe 'Shared UI pipeline dispatch without host mutation' {
    BeforeEach {
        $fixture = (New-Item -ItemType Directory (Join-Path $TestDrive ([guid]::NewGuid().ToString('N')))).FullName
        $mwb = (New-Item -ItemType Directory "$fixture\.pipelines\mwb" -Force).FullName
        Copy-Item "$PSScriptRoot\..\mwb\MwbSandboxCi.Common.ps1" $mwb
        $phase = @'
param($Mode, $RunId, $ArtifactRoot, $ResultsDirectory, $Platform, $SourceRevision, $Configuration, $CertificateMarkerPath)
if (-not $Mode) {
    $Mode = if ($PSCommandPath -like '*Install-*') { 'Install' } else { 'Diagnostics' }
}
if ($env:POWERTOYS_INSTALL_DIR -cne 'original-product' -or $env:useInstallerForTest -cne 'true') {
    throw 'Another phase leaked its environment.'
}
$Mode | Add-Content $env:MWB_FLOW_LOG
if ($env:MWB_FLOW_FAILURE -eq $Mode) { exit 23 }
$env:POWERTOYS_INSTALL_DIR = 'private-mwb-product'
$env:useInstallerForTest = 'false'
exit 0
'@
        foreach ($name in @('Install-MwbSandboxClient.ps1', 'mwbSandboxExperiment.ps1', 'Export-MwbSandboxPrerequisites.ps1')) {
            $phase | Set-Content (Join-Path $mwb $name)
        }
        @'
function Enable-LightSwitchLocation { param($StatePath) }
function Restore-LightSwitchLocation {
    param($StatePath)
    'LightSwitchCleanup' | Add-Content $env:MWB_FLOW_LOG
    if ($env:MWB_FLOW_FAILURE -eq 'LightSwitchCleanup') { throw 'Injected location cleanup failure.' }
}
'@ | Set-Content "$fixture\.pipelines\configureLightSwitchLocation.ps1"
        $runner = New-Item -ItemType Directory "$fixture\artifact\tests\ColorPicker.UITests" -Force
        '{}' | Set-Content "$runner\ColorPicker.UITests.runtimeconfig.json"
        '' | Set-Content "$runner\ColorPicker.UITests.dll"
        $environment = @{}
        foreach ($name in @('POWERTOYS_INSTALL_DIR', 'useInstallerForTest', 'MWB_FLOW_LOG', 'MWB_FLOW_FAILURE',
            'RUN_GENERIC_UI_TESTS', 'RUN_MWB_SANDBOX', 'MWB_SELECTION_BLOCKED', 'AUTO_UI_TEST_MODULES')) {
            $environment[$name] = [Environment]::GetEnvironmentVariable($name)
        }
        $env:POWERTOYS_INSTALL_DIR = 'original-product'
        $env:useInstallerForTest = 'true'
        $env:MWB_FLOW_LOG = Join-Path $fixture 'phases.log'
        $env:MWB_FLOW_FAILURE = ''
        $env:AUTO_UI_TEST_MODULES = ''
        $env:MWB_SELECTION_BLOCKED = 'false'
        $env:RUN_MWB_SANDBOX = 'true'
        $env:RUN_GENERIC_UI_TESTS = 'true'
    }

    AfterEach {
        foreach ($name in $environment.Keys) {
            [Environment]::SetEnvironmentVariable($name, $environment[$name])
        }
    }

    It 'executes the actual shared step for <Scenario>' -TestCases @(
        @{ Scenario = 'mixed Win10'; Platform = 'x64Win10'; Generic = $true; Mwb = $true; Failure = ''; Blocked = $false; Expected = 'Generic,Prepare,Run'; ExitCode = 0 }
        @{ Scenario = 'mixed Win11'; Platform = 'x64Win11'; Generic = $true; Mwb = $true; Failure = ''; Blocked = $false; Expected = 'Generic,Install,Prepare,Run'; ExitCode = 0 }
        @{ Scenario = 'MWB only'; Platform = 'x64Win10'; Generic = $false; Mwb = $true; Failure = ''; Blocked = $false; Expected = 'Prepare,Run'; ExitCode = 0 }
        @{ Scenario = 'unrelated selection'; Platform = 'x64Win10'; Generic = $true; Mwb = $false; Failure = ''; Blocked = $false; Expected = 'Generic'; ExitCode = 0 }
        @{ Scenario = 'ARM64'; Platform = 'arm64'; Generic = $true; Mwb = $false; Failure = ''; Blocked = $false; Expected = 'Generic'; ExitCode = 0 }
        @{ Scenario = 'generic failure'; Platform = 'x64Win10'; Generic = $true; Mwb = $true; Failure = 'Generic'; Blocked = $false; Expected = 'Generic,Prepare,Run'; ExitCode = 1 }
        @{ Scenario = 'client setup failure'; Platform = 'x64Win11'; Generic = $true; Mwb = $true; Failure = 'Install'; Blocked = $false; Expected = 'Generic,Install'; ExitCode = 1 }
        @{ Scenario = 'preparation failure'; Platform = 'x64Win10'; Generic = $true; Mwb = $true; Failure = 'Prepare'; Blocked = $false; Expected = 'Generic,Prepare'; ExitCode = 1 }
        @{ Scenario = 'test failure'; Platform = 'x64Win10'; Generic = $true; Mwb = $true; Failure = 'Run'; Blocked = $false; Expected = 'Generic,Prepare,Run'; ExitCode = 1 }
        @{ Scenario = 'unsupported mixed source'; Platform = 'x64Win10'; Generic = $true; Mwb = $false; Failure = ''; Blocked = $true; Expected = 'Generic'; ExitCode = 1 }
        @{ Scenario = 'unsupported MWB-only source'; Platform = 'x64Win10'; Generic = $false; Mwb = $false; Failure = ''; Blocked = $true; Expected = ''; ExitCode = 1 }
    ) {
        param($Scenario, $Platform, $Generic, $Mwb, $Failure, $Blocked, $Expected, $ExitCode)
        $env:RUN_GENERIC_UI_TESTS = $Generic.ToString().ToLowerInvariant()
        $env:RUN_MWB_SANDBOX = $Mwb.ToString().ToLowerInvariant()
        $env:MWB_SELECTION_BLOCKED = $Blocked.ToString().ToLowerInvariant()
        $env:MWB_FLOW_FAILURE = $Failure
        $step = Get-UiTestFlowStep 'Run UI Tests'
        $step = $step.Replace('$(Pipeline.Workspace)\$(TestArtifactsName)', "$fixture\artifact").
            Replace('$(Build.SourcesDirectory)', $fixture).Replace('$(build.sourcesdirectory)', $fixture).
            Replace('$(Common.TestResultsDirectory)', "$fixture\results").Replace('$(Agent.WorkFolder)', $fixture).
            Replace('$(System.JobId)', [guid]::NewGuid().ToString()).Replace('$(TestPlatform)', $Platform).
            Replace('$(Build.SourceVersion)', 'revision').Replace('$(BuildConfiguration)', 'Release').
            Replace('${{ join('';'', parameters.uiTestModules) }}', 'ColorPicker.UITests;MouseWithoutBorders.UITests')
        $prefix = @'
function dotnet {
    'Generic' | Add-Content $env:MWB_FLOW_LOG
    $global:LASTEXITCODE = if ($env:MWB_FLOW_FAILURE -eq 'Generic') { 23 } else { 0 }
}
'@
        $path = Join-Path $fixture 'dispatch.ps1'
        ($prefix + [Environment]::NewLine + $step) | Set-Content $path
        $result = Invoke-MwbClientCommand (Get-Process -Id $PID).Path @('-NoProfile', '-File', $path) 30
        $result.TimedOut | Should Be $false
        $result.ExitCode | Should Be $ExitCode
        $actual = if (Test-Path $env:MWB_FLOW_LOG) { (Get-Content $env:MWB_FLOW_LOG) -join ',' } else { '' }
        $actual | Should Be $Expected
        if ($Mwb -and $Failure -notin @('Install', 'Prepare', 'Run')) {
            $result.Output | Should Match 'variable=MwbUiTestsPassed\]true'
        } else {
            $result.Output | Should Not Match 'variable=MwbUiTestsPassed\]true'
        }
        if ($ExitCode) { $result.Output | Should Match 'task.logissue type=error' }
    }

    It 'always attempts all cleanup actions after <Failure>' -TestCases @(
        @{ Failure = ''; ExitCode = 0 }
        @{ Failure = 'Recover'; ExitCode = 1 }
        @{ Failure = 'Cleanup'; ExitCode = 1 }
        @{ Failure = 'Diagnostics'; ExitCode = 1 }
        @{ Failure = 'LightSwitchCleanup'; ExitCode = 1 }
    ) {
        param($Failure, $ExitCode)
        $env:MWB_FLOW_FAILURE = $Failure
        $step = (Get-UiTestFlowStep 'Restore UI-test prerequisites').
            Replace('$(Build.SourcesDirectory)', $fixture).Replace('$(build.sourcesdirectory)', $fixture).
            Replace('$(Common.TestResultsDirectory)', "$fixture\results").Replace('$(Agent.WorkFolder)', $fixture).
            Replace('$(System.JobId)', [guid]::NewGuid().ToString())
        $path = Join-Path $fixture 'cleanup.ps1'
        $step | Set-Content $path
        $result = Invoke-MwbClientCommand (Get-Process -Id $PID).Path @('-NoProfile', '-File', $path) 30
        $result.TimedOut | Should Be $false
        $result.ExitCode | Should Be $ExitCode
        ((Get-Content $env:MWB_FLOW_LOG) -join ',') | Should Be 'Recover,Cleanup,Diagnostics,LightSwitchCleanup'
        if ($ExitCode) {
            $result.Output | Should Not Match 'variable=UiTestCleanupComplete\]true'
            $result.Output | Should Match 'task.logissue type=error'
        } else {
            $result.Output | Should Match 'variable=UiTestCleanupComplete\]true'
        }
    }
}

Describe 'Failure-only pipeline diagnostic publication' {
    It 'evaluates the actual artifact condition for <Scenario>' -TestCases @(
        @{ Scenario = 'green'; Selected = 'true'; Passed = 'true'; Cleaned = 'true'; Failed = $false; Canceled = $false; Publish = $false }
        @{ Scenario = 'unselected failure'; Selected = 'false'; Passed = ''; Cleaned = ''; Failed = $true; Canceled = $false; Publish = $false }
        @{ Scenario = 'missing TRX'; Selected = 'true'; Passed = ''; Cleaned = 'true'; Failed = $false; Canceled = $false; Publish = $true }
        @{ Scenario = 'interrupted cleanup'; Selected = 'true'; Passed = 'true'; Cleaned = ''; Failed = $false; Canceled = $false; Publish = $true }
        @{ Scenario = 'test or setup failure'; Selected = 'true'; Passed = ''; Cleaned = 'true'; Failed = $true; Canceled = $false; Publish = $true }
        @{ Scenario = 'another suite failure'; Selected = 'true'; Passed = 'true'; Cleaned = 'true'; Failed = $true; Canceled = $false; Publish = $true }
        @{ Scenario = 'cancellation after success'; Selected = 'true'; Passed = 'true'; Cleaned = 'true'; Failed = $false; Canceled = $true; Publish = $true }
        @{ Scenario = 'cancellation before execution'; Selected = 'true'; Passed = ''; Cleaned = ''; Failed = $false; Canceled = $true; Publish = $true }
    ) {
        param($Scenario, $Selected, $Passed, $Cleaned, $Failed, $Canceled, $Publish)
        $yaml = Get-Content "$PSScriptRoot\..\v2\templates\job-test-project.yml" -Raw
        $condition = [regex]::Match($yaml, 'displayName: Publish UI-test diagnostics\r?\n\s*condition: ([^\r\n]+)').Groups[1].Value
        $variables = @{ RunMwbSandbox = $Selected; MwbUiTestsPassed = $Passed; UiTestCleanupComplete = $Cleaned }
        $condition = [regex]::Replace($condition, "(eq|ne)\(variables\['([^']+)'\], 'true'\)", {
            param($match)
            $equal = $variables[$match.Groups[2].Value] -eq 'true'
            $(if ($match.Groups[1].Value -eq 'eq') { $equal } else { -not $equal }).ToString().ToLowerInvariant()
        })
        $condition = $condition.Replace('always()', 'true').Replace('failed()', $Failed.ToString().ToLowerInvariant()).
            Replace('canceled()', $Canceled.ToString().ToLowerInvariant())
        $flat = '(and|or)\(((?:true|false)(?:, (?:true|false))*)\)'
        while ([regex]::IsMatch($condition, $flat)) {
            $condition = [regex]::Replace($condition, $flat, {
                param($match)
                $values = $match.Groups[2].Value -split ', '
                $(if ($match.Groups[1].Value -eq 'and') { $values -notcontains 'false' } else { $values -contains 'true' }).ToString().ToLowerInvariant()
            })
        }
        $condition | Should Be $Publish.ToString().ToLowerInvariant()
    }

    It 'exports cancellation evidence before potentially lengthy test attachment publication' {
        $yaml = Get-Content "$PSScriptRoot\..\v2\templates\job-test-project.yml" -Raw
        $diagnostics = $yaml.IndexOf('displayName: Publish UI-test diagnostics')
        $diagnostics | Should BeGreaterThan $yaml.IndexOf('displayName: Restore UI-test prerequisites')
        $diagnostics | Should BeLessThan $yaml.IndexOf('displayName: "Publish UI Test Results"')
    }
}
