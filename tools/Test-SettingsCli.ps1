# Copyright (c) Microsoft Corporation
# The Microsoft Corporation licenses this file to you under the MIT license.
# See the LICENSE file in the project root for more information.

[CmdletBinding()]
param(
    [string]$CliPath = (Join-Path $PSScriptRoot '..\x64\Debug\WinUI3Apps\PowerToys.Settings.Cli.exe'),
    [string]$Module = 'LightSwitch'
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$CliPath = [IO.Path]::GetFullPath($CliPath)
if (-not (Test-Path -LiteralPath $CliPath -PathType Leaf)) {
    throw "Settings CLI executable was not found: $CliPath"
}

function Invoke-SettingsCli {
    param([string[]]$Arguments)

    $output = (& $CliPath @Arguments 2>&1 | Out-String).Trim()
    [pscustomobject]@{
        Output = $output
        ExitCode = $LASTEXITCODE
    }
}

function Assert-ExitCode {
    param(
        [pscustomobject]$Result,
        [int]$Expected,
        [string]$Description
    )

    if ($Result.ExitCode -ne $Expected) {
        throw "$Description failed with exit code $($Result.ExitCode). Output: $($Result.Output)"
    }
}

function Assert-True {
    param(
        [bool]$Condition,
        [string]$Description
    )

    if (-not $Condition) {
        throw $Description
    }
}

Write-Host "Testing Settings CLI: $CliPath"

$help = Invoke-SettingsCli @('--help')
Assert-ExitCode $help 0 'help'
Assert-True ($help.Output -match 'list' -and $help.Output -match 'status' -and $help.Output -match 'enable' -and $help.Output -match 'disable') 'help does not list the expected commands'

$list = Invoke-SettingsCli @('list')
Assert-ExitCode $list 0 'list'
Assert-True ($list.Output -match $Module) "list did not include module '$Module'"

$listJsonResult = Invoke-SettingsCli @('list', '--json')
Assert-ExitCode $listJsonResult 0 'list --json'
$listJson = $listJsonResult.Output | ConvertFrom-Json
Assert-True ($null -ne $listJson.PSObject.Properties[$Module]) "list --json did not include module '$Module'"

$status = Invoke-SettingsCli @('status', $Module)
Assert-ExitCode $status 0 "status $Module"
Assert-True ($status.Output -match "Module '$Module' is (Enabled|Disabled)\.") "status output was unexpected: $($status.Output)"

$statusJsonResult = Invoke-SettingsCli @('status', $Module, '--json')
Assert-ExitCode $statusJsonResult 0 "status $Module --json"
$before = $statusJsonResult.Output | ConvertFrom-Json
Assert-True ($null -ne $before.Enabled) "status $Module --json did not contain Enabled"
if ($null -ne $before.GroupPolicy) {
    throw "Module '$Module' is controlled by Group Policy and cannot be used for enable/disable testing."
}

try {
    $disable = Invoke-SettingsCli @('disable', $Module)
    Assert-ExitCode $disable 0 "disable $Module"
    $disabled = (Invoke-SettingsCli @('status', $Module, '--json')).Output | ConvertFrom-Json
    Assert-True (-not $disabled.Enabled) "disable $Module did not persist Disabled"

    $enable = Invoke-SettingsCli @('enable', $Module)
    Assert-ExitCode $enable 0 "enable $Module"
    $enabled = (Invoke-SettingsCli @('status', $Module, '--json')).Output | ConvertFrom-Json
    Assert-True $enabled.Enabled "enable $Module did not persist Enabled"
}
finally {
    $restore = if ($before.Enabled) { 'enable' } else { 'disable' }
    $restoreResult = Invoke-SettingsCli @($restore, $Module)
    Assert-ExitCode $restoreResult 0 "restore $Module to $restore"
}

$missing = Invoke-SettingsCli @('status', '__SettingsCliMissingModule__')
Assert-ExitCode $missing 1 'status for an unknown module'

$removedToggle = Invoke-SettingsCli @('toggle', $Module)
Assert-ExitCode $removedToggle 1 'removed toggle command'

Write-Host 'All Settings CLI E2E checks passed.'
Write-Host 'Restart PowerToys separately to validate live module lifecycle changes.'
