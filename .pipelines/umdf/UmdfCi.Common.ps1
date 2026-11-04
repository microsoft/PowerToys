# Copyright (c) Microsoft Corporation
# The Microsoft Corporation licenses this file to you under the MIT license.
# See the LICENSE file in the project root for more information.

function Get-UmdfUiTestSelection {
    param(
        [string[]]$Modules = @(),
        [string]$Platform,
        [bool]$AutoSelect = $false,
        [bool]$RouteArm64 = $false,
        [bool]$DedicatedArm64 = $false,
        [ValidateSet('GrabAndMove.UITests', 'PowerDisplay.UITests')]
        [string]$FixtureModule = 'GrabAndMove.UITests'
    )
    if ($DedicatedArm64 -and ($Platform -ne 'ARM64' -or $AutoSelect -or
        $Modules.Count -ne 1 -or $Modules[0] -notin @('GrabAndMove.UITests', 'PowerDisplay.UITests'))) {
        throw 'The dedicated ARM64 UMDF job must select exactly one supported fixture suite without PR auto-selection.'
    }

    $explicit = $FixtureModule -in $Modules
    $selected = $Modules.Count -eq 0 -or $explicit
    $supported = $Platform -in @('x64', 'x64Win10', 'x64Win11') -or $DedicatedArm64
    $routed = $selected -and $Platform -eq 'ARM64' -and $RouteArm64 -and -not $DedicatedArm64
    $nonFixture = $selected -and $FixtureModule -eq 'PowerDisplay.UITests' -and
        $Platform -eq 'ARM64' -and -not $DedicatedArm64 -and -not $routed
    $blocked = $selected -and -not $supported -and $explicit -and -not $AutoSelect -and -not $routed -and -not $nonFixture
    [pscustomobject]@{
        Run = $selected -and ($supported -or $nonFixture)
        Exclude = $selected -and -not $supported -and -not $nonFixture
        Blocked = $blocked
        Skipped = $selected -and -not $supported -and -not $blocked -and -not $routed -and -not $nonFixture
        Routed = $routed
        Filter = if ($nonFixture) { 'TestCategory!=PowerDisplayVirtualMonitors' } else { '' }
    }
}

function Assert-UmdfArm64TestEnvironment {
    param([string]$OsArchitecture, [string]$BootConfiguration)
    if ($OsArchitecture -ne 'ARM64') {
        throw 'BLOCKED_INFRASTRUCTURE: the dedicated UMDF test image must run on ARM64.'
    }
    if ($BootConfiguration -notmatch '(?im)^\s*testsigning\s+Yes\s*$') {
        throw 'BLOCKED_INFRASTRUCTURE: the dedicated ARM64 image must already have TESTSIGNING enabled and be rebooted. The pipeline does not change boot policy.'
    }
}

function Get-UmdfFixtures {
    [pscustomobject]@{
        Kind = 'Hid'; Suite = 'GrabAndMove.UITests'
        Manager = 'umdf-hid-smoke\Manage-SmokeDriver.ps1'
        Artifact = 'umdf-hid'; StateFile = 'UmdfHidSmoke.json'; Evidence = 'umdf-hid'
        IsolatedGuest = $false
    }
    [pscustomobject]@{
        Kind = 'Display'; Suite = 'PowerDisplay.UITests'
        Manager = 'powerdisplay-idd-poc\Manage-Poc.ps1'
        Artifact = 'powerdisplay-idd'; StateFile = 'UmdfDisplay.json'; Evidence = 'umdf-display'
        IsolatedGuest = $true
    }
}

function Get-UmdfFixtureContext {
    param(
        [Parameter(Mandatory)][ValidateSet('GrabAndMove.UITests', 'PowerDisplay.UITests')][string]$Suite,
        [Parameter(Mandatory)][string]$RepositoryRoot,
        [Parameter(Mandatory)][string]$ArtifactRoot,
        [Parameter(Mandatory)][ValidateSet('x64', 'ARM64')][string]$Platform,
        [Parameter(Mandatory)][string]$StateDirectory,
        [Parameter(Mandatory)][string]$ResultsDirectory
    )
    $fixture = Get-UmdfFixtures | Where-Object Suite -EQ $Suite
    $arguments = @{
        StatePath = Join-Path $StateDirectory $fixture.StateFile
        ResultsDirectory = Join-Path $ResultsDirectory $fixture.Evidence
    }
    if ($fixture.IsolatedGuest) { $arguments.IsolatedGuest = $true }
    $artifact = Join-Path $ArtifactRoot "$($fixture.Artifact)\$Platform"
    $environment = @{}
    if ($fixture.Kind -eq 'Display') {
        $environment.POWERTOYS_POWERDISPLAY_IDD_PROBE = @{
            Value = Join-Path $artifact 'driver\IddProbe.exe'
            Previous = [Environment]::GetEnvironmentVariable('POWERTOYS_POWERDISPLAY_IDD_PROBE', 'Process')
        }
    }
    [pscustomobject]@{
        Fixture = $fixture
        Manager = Join-Path $RepositoryRoot ".pipelines\$($fixture.Manager)"
        Artifact = $artifact
        Arguments = $arguments
        Environment = $environment
    }
}

function Invoke-UmdfFixtureManager {
    param([Parameter(Mandatory)]$Context, [Parameter(Mandatory)][ValidateSet('Install', 'Cleanup')][string]$Action)
    $arguments = $Context.Arguments.Clone()
    if ($Action -eq 'Install') { $arguments.ArtifactDirectory = $Context.Artifact }
    & $Context.Manager -Action $Action @arguments | Out-Host
}

function Start-UmdfFixture {
    param([Parameter(Mandatory)]$Context)
    Write-Host "##vso[task.setvariable variable=Umdf$($Context.Fixture.Kind)Requested]true"
    if (Test-Path -LiteralPath $Context.Arguments.StatePath) {
        Invoke-UmdfFixtureManager -Context $Context -Action Cleanup
    }
    Invoke-UmdfFixtureManager -Context $Context -Action Install
    foreach ($name in $Context.Environment.Keys) {
        [Environment]::SetEnvironmentVariable($name, $Context.Environment[$name].Value, 'Process')
    }
}

function Stop-UmdfFixture {
    param([Parameter(Mandatory)]$Context)
    try {
        if (Test-Path -LiteralPath $Context.Arguments.StatePath) {
            Invoke-UmdfFixtureManager -Context $Context -Action Cleanup
        }
    }
    finally {
        foreach ($name in $Context.Environment.Keys) {
            [Environment]::SetEnvironmentVariable($name, $Context.Environment[$name].Previous, 'Process')
        }
    }
}

function Restore-UmdfFixtures {
    param(
        [Parameter(Mandatory)][string]$RepositoryRoot,
        [Parameter(Mandatory)][string]$ArtifactRoot,
        [Parameter(Mandatory)][ValidateSet('x64', 'ARM64')][string]$Platform,
        [Parameter(Mandatory)][string]$StateDirectory,
        [Parameter(Mandatory)][string]$ResultsDirectory
    )
    $errors = [Collections.Generic.List[string]]::new()
    foreach ($fixture in Get-UmdfFixtures) {
        $context = Get-UmdfFixtureContext -Suite $fixture.Suite -RepositoryRoot $RepositoryRoot -ArtifactRoot $ArtifactRoot `
            -Platform $Platform -StateDirectory $StateDirectory -ResultsDirectory $ResultsDirectory
        try { Stop-UmdfFixture -Context $context }
        catch {
            $message = "UMDF $($fixture.Kind) prerequisite cleanup failed: $($_.Exception.Message)"
            Write-Host "##vso[task.logissue type=error]$message"
            $errors.Add($message)
        }
    }
    if ($errors.Count) { throw ($errors -join '; ') }
}
