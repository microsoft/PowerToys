# Copyright (c) Microsoft Corporation
# The Microsoft Corporation licenses this file to you under the MIT license.
# See the LICENSE file in the project root for more information.

# Invoked by Invoke-ExperimentProbe.ps1 or the guest worker, never by preparation/startup.
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [ValidateSet('Navigate', 'GenerateKey', 'Connect', 'Status', 'Refresh')][string]$Action,
    [Parameter(Mandatory = $true)][int]$SettingsProcessId,
    [Parameter(Mandatory = $true)][string]$WinApp,
    [string]$Key,
    [string]$PeerName,
    [string]$GeneratedKeyPath
)

$ErrorActionPreference = 'Stop'
. "$PSScriptRoot\Experiment.Common.ps1"
$null = Assert-ExperimentDesktop -AllowNonConsole
$settingsPath = Join-Path $env:LOCALAPPDATA 'Microsoft\PowerToys\MouseWithoutBorders\settings.json'

function Read-MwbSettings {
    $stream = [IO.File]::Open($settingsPath, [IO.FileMode]::Open, [IO.FileAccess]::Read,
        ([IO.FileShare]::ReadWrite -bor [IO.FileShare]::Delete))
    $reader = New-Object IO.StreamReader($stream)
    try { $reader.ReadToEnd() | ConvertFrom-Json } finally { $reader.Dispose() }
}

function Invoke-Ui {
    param([string[]]$Arguments)
    $raw = & $WinApp ui @Arguments -a $SettingsProcessId --json
    $exitCode = $LASTEXITCODE
    $data = $raw | Out-String | ConvertFrom-Json
    $emptySearch = $Arguments[0] -eq 'search' -and $exitCode -eq 1 -and $data.matchCount -eq 0
    if ($exitCode -ne 0 -and -not $emptySearch) { throw "winapp $($Arguments[0]) failed with exit $exitCode." }
    $data
}

function Find-ExactButton {
    param([string]$Name, [switch]$AllowMissing)
    $search = Invoke-Ui @('search', $Name)
    $buttons = @($search.matches | Where-Object {
        $_.type -eq 'Button' -and $_.name -eq $Name -and $_.isEnabled -and -not $_.isOffscreen
    })
    if ($buttons.Count -eq 0 -and $AllowMissing) { return }
    if ($buttons.Count -ne 1) { throw "Expected one enabled visible '$Name' button; found $($buttons.Count)." }
    $buttons[0].selector
}

$result = [ordered]@{ Action = $Action; TimestampUtc = [DateTime]::UtcNow.ToString('o') }
switch ($Action) {
    'Navigate' {
        $deadline = [DateTime]::UtcNow.AddSeconds(15)
        do {
            $windows = @(Invoke-Ui @('list-windows'))
            if ($windows.Count) { break }
            Start-Sleep -Milliseconds 250
        } while ([DateTime]::UtcNow -lt $deadline)
        if (-not $windows.Count) { throw 'The Settings window did not appear.' }
        $nav = Invoke-Ui @('search', 'MouseWithoutBordersNavItem')
        if ($nav.matchCount -eq 0) { $null = Invoke-Ui @('invoke', 'InputOutputNavItem') }
        $null = Invoke-Ui @('invoke', 'MouseWithoutBordersNavItem')
        $deadline = [DateTime]::UtcNow.AddSeconds(10)
        do {
            $button = Find-ExactButton 'New key' -AllowMissing
            if ($button) { break }
            Start-Sleep -Milliseconds 250
        } while ([DateTime]::UtcNow -lt $deadline)
        if (-not $button) { throw 'The MWB settings page did not expose New key.' }
        $result.Navigated = $true
    }
    'GenerateKey' {
        if (-not $GeneratedKeyPath) { throw 'A private generated-key output path is required.' }
        $before = (Read-MwbSettings).properties.SecurityKey.value
        $null = Invoke-Ui @('invoke', (Find-ExactButton 'New key'))
        $deadline = [DateTime]::UtcNow.AddSeconds(20)
        do {
            $after = (Read-MwbSettings).properties.SecurityKey.value
            if ($after -and $after -ne $before) { break }
            Start-Sleep -Milliseconds 250
        } while ([DateTime]::UtcNow -lt $deadline)
        if (-not $after -or $after -eq $before) { throw 'New key did not produce a different nonempty key.' }
        [IO.File]::WriteAllText($GeneratedKeyPath, $after, (New-Object Text.UTF8Encoding($false)))
        $result.KeyChanged = $true
        $result.KeyLength = $after.Length
    }
    'Connect' {
        if (-not $Key -or -not $PeerName) { throw 'Connect requires the generated key and discovered peer name.' }
        $fields = Invoke-Ui @('search', 'ConnectPCNameTextBox')
        if ($fields.matchCount -eq 0) { $null = Invoke-Ui @('invoke', (Find-ExactButton 'Security key')) }
        $null = Invoke-Ui @('set-value', 'ConnectSecurityKeyTextBox', $Key)
        $null = Invoke-Ui @('set-value', 'ConnectPCNameTextBox', $PeerName)
        $controls = Invoke-Ui @('search', 'Connect')
        $field = @($controls.matches | Where-Object {
            $_.PSObject.Properties['automationId'] -and $_.automationId -eq 'ConnectPCNameTextBox'
        })
        if ($field.Count -ne 1 -or $field[0].isOffscreen) { throw 'The device-name field must be uniquely visible.' }
        # The header has another Connect button; submit through the input row.
        $buttons = @($controls.matches | Where-Object {
            $_.type -eq 'Button' -and $_.name -eq 'Connect' -and $_.isEnabled -and
            -not $_.isOffscreen -and [Math]::Abs($_.y - $field[0].y) -le 2
        })
        if ($buttons.Count -ne 1) { throw 'Could not uniquely identify the input-row Connect button.' }
        $null = Invoke-Ui @('invoke', $buttons[0].selector)
        $result.RequestSubmitted = $true
    }
    'Refresh' {
        $null = Invoke-Ui @('invoke', (Find-ExactButton 'Refresh connections'))
        $result.RequestSubmitted = $true
    }
    'Status' {
        $settings = Read-MwbSettings
        $result.KeyPresent = -not [string]::IsNullOrWhiteSpace($settings.properties.SecurityKey.value)
        $result.Machines = $settings.properties.MachineMatrixString
        $result.UseService = $settings.properties.UseService
        $result.UI = Invoke-Ui @('search', 'DevicesItemsControl')
    }
}
$result
