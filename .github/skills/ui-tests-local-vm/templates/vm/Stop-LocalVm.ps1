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
    [string]$CredentialPath = (Join-Path $env:LOCALAPPDATA 'PowerToysUiTestVm\admin.credential.xml'),
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
    if (-not $Save -and -not $TurnOff) {
        if (-not (Test-Path $CredentialPath -PathType Leaf)) {
            throw "DPAPI credential file was not found: $CredentialPath"
        }

        $credential = Import-Clixml $CredentialPath
        $session = New-PSSession -VMName $vm.Name -Credential $credential
        try {
            # Validate the protected guest-local credential; repair it only when it no longer authenticates.
            $autoLogonScript = Join-Path $PSScriptRoot 'oem\Set-UiTestAutoLogon.ps1'
            if (-not (Test-Path $autoLogonScript -PathType Leaf)) {
                $autoLogonScript = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\oem\Set-UiTestAutoLogon.ps1'))
            }
            if (-not (Test-Path $autoLogonScript -PathType Leaf)) {
                throw "Auto-logon persistence helper was not found: $autoLogonScript"
            }
            Invoke-Command `
                -Session $session `
                -FilePath $autoLogonScript `
                -ArgumentList ([string]$configuration.StandardUser) | Out-Null
        }
        finally {
            Remove-PSSession $session -ErrorAction SilentlyContinue
        }
    }

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
