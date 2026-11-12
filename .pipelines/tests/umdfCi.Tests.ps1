# Copyright (c) Microsoft Corporation
# The Microsoft Corporation licenses this file to you under the MIT license.
# See the LICENSE file in the project root for more information.

. (Join-Path $PSScriptRoot '..\umdf\UmdfCi.Common.ps1')

Describe 'UMDF manager artifact forwarding' {
    It 'passes the known artifact for <Kind> <Action> without changing context arguments' -TestCases @(
        @{ Suite = 'PowerDisplay.UITests'; Kind = 'Display'; Action = 'Cleanup'; HasArtifact = $true }
        @{ Suite = 'PowerDisplay.UITests'; Kind = 'Display'; Action = 'Install'; HasArtifact = $true }
        @{ Suite = 'GrabAndMove.UITests'; Kind = 'Hid'; Action = 'Cleanup'; HasArtifact = $false }
        @{ Suite = 'GrabAndMove.UITests'; Kind = 'Hid'; Action = 'Install'; HasArtifact = $true }
    ) {
        param($Suite, $Kind, $Action, $HasArtifact)
        $context = Get-UmdfFixtureContext -Suite $Suite -RepositoryRoot $TestDrive -ArtifactRoot $TestDrive `
            -Platform x64 -StateDirectory $TestDrive -ResultsDirectory $TestDrive
        $context.Manager = {
            param($Action, $StatePath, $ResultsDirectory, $ArtifactDirectory, [switch]$IsolatedGuest)
            $script:managerArguments = $PSBoundParameters
        }
        Invoke-UmdfFixtureManager -Context $context -Action $Action
        $script:managerArguments.Action | Should Be $Action
        $script:managerArguments.StatePath | Should Be $context.Arguments.StatePath
        $script:managerArguments.ContainsKey('IsolatedGuest') | Should Be ($Kind -eq 'Display')
        $script:managerArguments.ContainsKey('ArtifactDirectory') | Should Be $HasArtifact
        if ($HasArtifact) { $script:managerArguments.ArtifactDirectory | Should Be $context.Artifact }
        $context.Arguments.ContainsKey('ArtifactDirectory') | Should Be $false
    }
}

Describe 'Shared UMDF fixture lifecycle orchestration' {
    BeforeEach {
        Mock Write-Host {}
        $previousProbe = $env:POWERTOYS_POWERDISPLAY_IDD_PROBE
        $previousCi = $env:TF_BUILD
        $previousJob = $env:SYSTEM_JOBID
        $env:POWERTOYS_POWERDISPLAY_IDD_PROBE = 'preserved-parent-value'
        $script:actions = [Collections.Generic.List[string]]::new()
        Mock Invoke-UmdfFixtureManager { $script:actions.Add("$($Context.Fixture.Kind):$Action") }
    }
    AfterEach {
        $env:POWERTOYS_POWERDISPLAY_IDD_PROBE = $previousProbe
        $env:TF_BUILD = $previousCi
        $env:SYSTEM_JOBID = $previousJob
    }

    It 'uses the existing fixture-specific manager, artifact and approval for <Suite>' -TestCases @(
        @{ Suite = 'GrabAndMove.UITests'; Kind = 'Hid'; Artifact = 'umdf-hid'; Isolated = $false }
        @{ Suite = 'PowerDisplay.UITests'; Kind = 'Display'; Artifact = 'powerdisplay-idd'; Isolated = $true }
    ) {
        param($Suite, $Kind, $Artifact, $Isolated)
        $context = Get-UmdfFixtureContext -Suite $Suite -RepositoryRoot $TestDrive -ArtifactRoot $TestDrive `
            -Platform ARM64 -StateDirectory $TestDrive -ResultsDirectory $TestDrive
        $context.Artifact | Should Be (Join-Path $TestDrive "$Artifact\ARM64")
        $context.Arguments.ContainsKey('IsolatedGuest') | Should Be $Isolated
        Start-UmdfFixture $context
        $script:actions.ToArray() | Should Be @("${Kind}:Install")
    }

    It 'cleans an existing journal before installing and exposes the probe only after successful installation' {
        $context = Get-UmdfFixtureContext -Suite PowerDisplay.UITests -RepositoryRoot $TestDrive -ArtifactRoot $TestDrive `
            -Platform x64 -StateDirectory $TestDrive -ResultsDirectory $TestDrive
        '{}' | Set-Content $context.Arguments.StatePath
        Start-UmdfFixture $context
        $script:actions.ToArray() | Should Be @('Display:Cleanup', 'Display:Install')
        $env:POWERTOYS_POWERDISPLAY_IDD_PROBE | Should Be (Join-Path $context.Artifact 'driver\IddProbe.exe')
    }

    It 'does not expose a failed installation as a usable probe' {
        $context = Get-UmdfFixtureContext -Suite PowerDisplay.UITests -RepositoryRoot $TestDrive -ArtifactRoot $TestDrive `
            -Platform x64 -StateDirectory $TestDrive -ResultsDirectory $TestDrive
        Mock Invoke-UmdfFixtureManager { throw 'installation failed' } -ParameterFilter { $Action -eq 'Install' }
        { Start-UmdfFixture $context } | Should Throw 'installation failed'
        $env:POWERTOYS_POWERDISPLAY_IDD_PROBE | Should Be 'preserved-parent-value'
    }

    It 'rejects a stale disposable guest without attempting native cleanup or installation' {
        $context = Get-UmdfFixtureContext -Suite PowerDisplay.UITests -RepositoryRoot $TestDrive -ArtifactRoot $TestDrive `
            -Platform x64 -StateDirectory $TestDrive -ResultsDirectory $TestDrive
        '{}' | Set-Content $context.Arguments.StatePath
        { Start-UmdfFixture -Context $context -RequireFreshGuest } | Should Throw 'refusing to reuse or clean it'
        $script:actions.Count | Should Be 0
        $env:POWERTOYS_POWERDISPLAY_IDD_PROBE | Should Be 'preserved-parent-value'
        Test-Path $context.Arguments.StatePath | Should Be $true
    }

    It 'restores the parent probe environment even if cleanup fails' {
        $context = Get-UmdfFixtureContext -Suite PowerDisplay.UITests -RepositoryRoot $TestDrive -ArtifactRoot $TestDrive `
            -Platform x64 -StateDirectory $TestDrive -ResultsDirectory $TestDrive
        '{}' | Set-Content $context.Arguments.StatePath
        $env:POWERTOYS_POWERDISPLAY_IDD_PROBE = 'temporary-fixture-value'
        Mock Invoke-UmdfFixtureManager { throw 'cleanup failed' }
        { Stop-UmdfFixture $context } | Should Throw 'cleanup failed'
        $env:POWERTOYS_POWERDISPLAY_IDD_PROBE | Should Be 'preserved-parent-value'
    }

    It 'retains <Suite> ownership without native removal only for explicit disposable CI' -TestCases @(
        @{ Suite = 'GrabAndMove.UITests' }
        @{ Suite = 'PowerDisplay.UITests' }
    ) {
        param($Suite)
        $context = Get-UmdfFixtureContext -Suite $Suite -RepositoryRoot $TestDrive -ArtifactRoot $TestDrive `
            -Platform x64 -StateDirectory $TestDrive -ResultsDirectory $TestDrive
        $ownership = '{"Thumbprint":"retained-public-test-identity","ImportedStores":["Root"]}'
        $ownership | Set-Content $context.Arguments.StatePath
        $env:TF_BUILD = 'True'
        $env:SYSTEM_JOBID = [guid]::NewGuid().ToString()
        $env:POWERTOYS_POWERDISPLAY_IDD_PROBE = 'temporary-fixture-value'
        Stop-UmdfFixture -Context $context -DeferRemovalToVmDisposal
        $script:actions.Count | Should Be 0
        Get-Content $context.Arguments.StatePath -Raw | Should Be "$ownership$([Environment]::NewLine)"
        $disposal = Get-Content (Join-Path $context.Arguments.ResultsDirectory 'disposal.json') -Raw | ConvertFrom-Json
        $disposal.Disposition | Should Be 'DeferredToVmDisposal'
        $disposal.NativeRemovalAttempted | Should Be $false
        $disposal.OwnershipJournal | Should Be $context.Arguments.StatePath
        $disposal.Ownership.ImportedStores | Should Be @('Root')
        $env:POWERTOYS_POWERDISPLAY_IDD_PROBE | Should Be $(if ($Suite -eq 'PowerDisplay.UITests') { 'preserved-parent-value' } else { 'temporary-fixture-value' })
    }

    It 'refuses a deferred-removal request outside a disposable CI job: <Case>' -TestCases @(
        @{ Case = 'local process'; Ci = ''; Job = 'a6a296ae-4044-427e-a3eb-8e1686f6b77c' }
        @{ Case = 'missing job'; Ci = 'True'; Job = '' }
    ) {
        param($Case, $Ci, $Job)
        $context = Get-UmdfFixtureContext -Suite PowerDisplay.UITests -RepositoryRoot $TestDrive -ArtifactRoot $TestDrive `
            -Platform x64 -StateDirectory $TestDrive -ResultsDirectory $TestDrive
        '{}' | Set-Content $context.Arguments.StatePath
        $env:TF_BUILD = $Ci
        $env:SYSTEM_JOBID = $Job
        $env:POWERTOYS_POWERDISPLAY_IDD_PROBE = 'temporary-fixture-value'
        { Stop-UmdfFixture -Context $context -DeferRemovalToVmDisposal } | Should Throw 'disposable Azure Pipelines job'
        $script:actions.Count | Should Be 0
        Test-Path $context.Arguments.StatePath | Should Be $true
        $env:POWERTOYS_POWERDISPLAY_IDD_PROBE | Should Be 'preserved-parent-value'
    }

    It 'still performs strict cleanup in CI unless deferral is explicitly requested' {
        $context = Get-UmdfFixtureContext -Suite PowerDisplay.UITests -RepositoryRoot $TestDrive -ArtifactRoot $TestDrive `
            -Platform x64 -StateDirectory $TestDrive -ResultsDirectory $TestDrive
        '{}' | Set-Content $context.Arguments.StatePath
        $env:TF_BUILD = 'True'
        $env:SYSTEM_JOBID = [guid]::NewGuid().ToString()
        Stop-UmdfFixture -Context $context
        $script:actions.ToArray() | Should Be @('Display:Cleanup')
    }

    It 'preserves both fixture journals during final disposable-job prerequisite restoration' {
        foreach ($fixture in Get-UmdfFixtures) { '{}' | Set-Content (Join-Path $TestDrive $fixture.StateFile) }
        $env:TF_BUILD = 'True'
        $env:SYSTEM_JOBID = [guid]::NewGuid().ToString()
        Restore-UmdfFixtures -RepositoryRoot $TestDrive -ArtifactRoot $TestDrive -Platform x64 `
            -StateDirectory $TestDrive -ResultsDirectory $TestDrive -DeferRemovalToVmDisposal
        $script:actions.Count | Should Be 0
        foreach ($fixture in Get-UmdfFixtures) {
            Test-Path (Join-Path $TestDrive $fixture.StateFile) | Should Be $true
            (Get-Content (Join-Path $TestDrive "$($fixture.Evidence)\disposal.json") -Raw | ConvertFrom-Json).Disposition |
                Should Be 'DeferredToVmDisposal'
        }
    }

    It 'attempts both journal recoveries before surfacing a cleanup failure' {
        foreach ($fixture in Get-UmdfFixtures) { '{}' | Set-Content (Join-Path $TestDrive $fixture.StateFile) }
        Mock Invoke-UmdfFixtureManager {
            $script:actions.Add("$($Context.Fixture.Kind):$Action")
            if ($Context.Fixture.Kind -eq 'Hid') { throw 'HID removal failed' }
        }
        {
            Restore-UmdfFixtures -RepositoryRoot $TestDrive -ArtifactRoot $TestDrive -Platform x64 `
                -StateDirectory $TestDrive -ResultsDirectory $TestDrive
        } | Should Throw 'HID removal failed'
        $script:actions.ToArray() | Should Be @('Hid:Cleanup', 'Display:Cleanup')
    }
}
