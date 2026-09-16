# Copyright (c) Microsoft Corporation
# The Microsoft Corporation licenses this file to you under the MIT license.
# See the LICENSE file in the project root for more information.

<#
.SYNOPSIS
Removes only the firewall rule recorded by the privileged experiment provisioner.
#>
[CmdletBinding()]
param([string]$StateRoot = 'C:\ProgramData\PowerToysMwbExperiment')

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version 2.0
$identity = [Security.Principal.WindowsIdentity]::GetCurrent()
try {
    if (-not (New-Object Security.Principal.WindowsPrincipal($identity)).IsInRole(
        [Security.Principal.WindowsBuiltInRole]::Administrator)) {
        throw 'Cleanup requires the privileged setup context.'
    }
}
finally { $identity.Dispose() }
$marker = Join-Path $StateRoot 'host-provisioning.json'
if (-not (Test-Path -LiteralPath $marker -PathType Leaf)) {
    throw 'No provisioning ownership marker exists; refusing to guess which rule to remove.'
}
$state = [IO.File]::ReadAllText($marker) | ConvertFrom-Json
$runId = [Guid]::Parse($state.RunId)
if ($state.RuleName -cne "PowerToys.Mwb.UITest.$runId") { throw 'Invalid firewall ownership marker.' }
$provisioner = Get-Process -Id $state.ProvisionerProcessId -ErrorAction SilentlyContinue
if ($provisioner -and $provisioner.Id -ne $PID -and
    $provisioner.Path -ieq $state.ProvisionerExecutable -and
    $provisioner.StartTime.ToUniversalTime().Ticks -eq
        ([DateTime]$state.ProvisionerStartTimeUtc).ToUniversalTime().Ticks) {
    Stop-Process -Id $provisioner.Id -Force -ErrorAction Stop
    if (-not $provisioner.WaitForExit(10000)) { throw 'The owned provisioning process did not stop.' }
}
$rule = Get-NetFirewallRule -Name $state.RuleName -ErrorAction SilentlyContinue
if ($rule) {
    $application = $rule | Get-NetFirewallApplicationFilter
    if ($application.Program -ine $state.Executable -or $rule.Direction -ne 'Inbound') {
        throw 'Firewall rule identity no longer matches this experiment.'
    }
    Remove-NetFirewallRule -Name $state.RuleName -ErrorAction Stop
}
if (Get-NetFirewallRule -Name $state.RuleName -ErrorAction SilentlyContinue) { throw 'The owned rule still exists.' }
$state.Status = 'Removed'
[IO.File]::WriteAllText($marker, ($state | ConvertTo-Json -Depth 5), [Text.UTF8Encoding]::new($false))
[pscustomobject]@{ Status = 'Removed'; RuleName = $state.RuleName }
