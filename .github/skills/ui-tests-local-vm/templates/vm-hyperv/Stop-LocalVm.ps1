# Copyright (c) Microsoft Corporation
# The Microsoft Corporation licenses this file to you under the MIT license.
# See the LICENSE file in the project root for more information.

<#
.SYNOPSIS
Stops the persistent Hyper-V UI-test guest while preserving its disk and checkpoints.

.EXAMPLE
pwsh ./Stop-LocalVm.ps1
#>

[CmdletBinding()]
param(
    [string]$ConfigPath = (Join-Path $PSScriptRoot 'vm.config.psd1'),
    # Saves the running state instead of shutting the guest down, so the next start resumes instantly.
    [switch]$Save,
    [switch]$TurnOff
)

$ErrorActionPreference = 'Stop'

if (-not (Test-Path $ConfigPath -PathType Leaf)) {
    throw "Configuration was not found: $ConfigPath"
}
$configuration = Import-PowerShellDataFile $ConfigPath

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

if ($vm.State -eq 'Running') {
    if ($Save) {
        Save-VM -Name $vm.Name
    }
    elseif ($TurnOff) {
        Stop-VM -Name $vm.Name -TurnOff -Force
    }
    else {
        Stop-VM -Name $vm.Name -Force
    }
}

$vm = Get-VM -Name $configuration.VmName
[pscustomobject]@{
    VmName = $vm.Name
    State = [string]$vm.State
} | ConvertTo-Json
