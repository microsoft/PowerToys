# Copyright (c) Microsoft Corporation
# The Microsoft Corporation licenses this file to you under the MIT license.
# See the LICENSE file in the project root for more information.

. (Join-Path $PSScriptRoot '..\umdf\UmdfCi.Common.ps1')

Describe 'Shared UMDF fixture lifecycle orchestration' {
    BeforeEach {
        Mock Write-Host {}
        $previousProbe = $env:POWERTOYS_POWERDISPLAY_IDD_PROBE
        $env:POWERTOYS_POWERDISPLAY_IDD_PROBE = 'preserved-parent-value'
        $script:actions = [Collections.Generic.List[string]]::new()
        Mock Invoke-UmdfFixtureManager { $script:actions.Add("$($Context.Fixture.Kind):$Action") }
    }
    AfterEach { $env:POWERTOYS_POWERDISPLAY_IDD_PROBE = $previousProbe }

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

    It 'restores the parent probe environment even if cleanup fails' {
        $context = Get-UmdfFixtureContext -Suite PowerDisplay.UITests -RepositoryRoot $TestDrive -ArtifactRoot $TestDrive `
            -Platform x64 -StateDirectory $TestDrive -ResultsDirectory $TestDrive
        '{}' | Set-Content $context.Arguments.StatePath
        $env:POWERTOYS_POWERDISPLAY_IDD_PROBE = 'temporary-fixture-value'
        Mock Invoke-UmdfFixtureManager { throw 'cleanup failed' }
        { Stop-UmdfFixture $context } | Should Throw 'cleanup failed'
        $env:POWERTOYS_POWERDISPLAY_IDD_PROBE | Should Be 'preserved-parent-value'
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
