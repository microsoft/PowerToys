# Copyright (c) Microsoft Corporation
# The Microsoft Corporation licenses this file to you under the MIT license.
# See the LICENSE file in the project root for more information.

<#
.SYNOPSIS
Manages clean-baseline checkpoints for the Hyper-V UI-test guest.

.DESCRIPTION
Restoring a standard checkpoint returns the guest to the exact state it was captured in, including
the logged-on desktop, which makes it the fast equivalent of recreating the VM. Use it for every
clean-profile claim instead of trusting a long-lived, mutated guest.

.EXAMPLE
pwsh ./Reset-LocalVm.ps1 -List

.EXAMPLE
pwsh ./Reset-LocalVm.ps1 -Restore

.EXAMPLE
pwsh ./Reset-LocalVm.ps1 -CreateBaseline -CheckpointName 'webview2-installed'
#>

[CmdletBinding(SupportsShouldProcess, DefaultParameterSetName = 'List')]
param(
    [string]$ConfigPath = (Join-Path $PSScriptRoot 'vm.config.psd1'),

    [Parameter(ParameterSetName = 'List')]
    [switch]$List,

    [Parameter(Mandatory, ParameterSetName = 'Restore')]
    [switch]$Restore,

    [Parameter(Mandatory, ParameterSetName = 'Create')]
    [switch]$CreateBaseline,

    [Parameter(ParameterSetName = 'Restore')]
    [Parameter(ParameterSetName = 'Create')]
    [string]$CheckpointName,

    [Parameter(ParameterSetName = 'Restore')]
    [switch]$StartAfterRestore
)

$ErrorActionPreference = 'Stop'

if (-not (Test-Path $ConfigPath -PathType Leaf)) {
    throw "Configuration was not found: $ConfigPath"
}
$configuration = Import-PowerShellDataFile $ConfigPath
$effectiveCheckpoint = if ([string]::IsNullOrWhiteSpace($CheckpointName)) {
    $configuration.BaselineCheckpointName
}
else {
    $CheckpointName
}

try {
    Import-Module Hyper-V -ErrorAction Stop
    Get-VMHost -ErrorAction Stop | Out-Null
}
catch {
    throw 'BLOCKED: Hyper-V is not accessible from this shell. Run from an elevated PowerShell 7 terminal, or add this account to the local "Hyper-V Administrators" group.'
}

$vm = Get-VM -Name $configuration.VmName -ErrorAction SilentlyContinue
if ($null -eq $vm) {
    throw "Virtual machine '$($configuration.VmName)' does not exist."
}

if ($CreateBaseline) {
    if ($PSCmdlet.ShouldProcess($vm.Name, "Create checkpoint '$effectiveCheckpoint'")) {
        Get-VMCheckpoint -VMName $vm.Name -Name $effectiveCheckpoint -ErrorAction SilentlyContinue |
            Remove-VMCheckpoint -Confirm:$false
        Checkpoint-VM -Name $vm.Name -SnapshotName $effectiveCheckpoint
    }
}
elseif ($Restore) {
    $checkpoint = Get-VMCheckpoint -VMName $vm.Name -Name $effectiveCheckpoint -ErrorAction SilentlyContinue
    if ($null -eq $checkpoint) {
        throw "Checkpoint '$effectiveCheckpoint' does not exist for '$($vm.Name)'."
    }
    if ($PSCmdlet.ShouldProcess($vm.Name, "Restore checkpoint '$effectiveCheckpoint'")) {
        if ($vm.State -eq 'Running') {
            Stop-VM -Name $vm.Name -TurnOff -Force
        }
        Restore-VMCheckpoint -VMName $vm.Name -Name $effectiveCheckpoint -Confirm:$false
        if ($StartAfterRestore) {
            Start-VM -Name $vm.Name
        }
    }
}

$vm = Get-VM -Name $configuration.VmName
[pscustomobject]@{
    VmName = $vm.Name
    State = [string]$vm.State
    BaselineCheckpointName = $configuration.BaselineCheckpointName
    Checkpoints = @(Get-VMCheckpoint -VMName $vm.Name -ErrorAction SilentlyContinue | ForEach-Object {
        [pscustomobject]@{
            Name = $_.Name
            CreationTime = $_.CreationTime
            ParentCheckpointName = $_.ParentCheckpointName
        }
    })
} | ConvertTo-Json -Depth 4
