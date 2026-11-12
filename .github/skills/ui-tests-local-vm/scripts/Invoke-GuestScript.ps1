# Copyright (c) Microsoft Corporation
# The Microsoft Corporation licenses this file to you under the MIT license.
# See the LICENSE file in the project root for more information.

<#
.SYNOPSIS
Runs a scriptblock inside the local UI-test guest over PowerShell Direct, handling the credential
import and PSSession lifecycle. Token-efficient replacement for the repeated
Import-Clixml / New-PSSession / Invoke-Command / Remove-PSSession boilerplate when inspecting or
mutating guest state (package registration, staged runtime files, registry, processes).

.PARAMETER ScriptBlock
The scriptblock to run in the guest. Its output is returned to the host.

.PARAMETER VmName
Name of the Hyper-V virtual machine. Hyper-V access is required: either an elevated shell or
membership in the local Hyper-V Administrators group.

.EXAMPLE
./Invoke-GuestScript.ps1 -VmName PowerToysUiTest-Win11 -ScriptBlock {
    Get-AppxPackage *ImageResizerContextMenu* | Select-Object -Expand Name
}

.EXAMPLE
./Invoke-GuestScript.ps1 -VmName PowerToysUiTest-Win11 -ScriptBlock {
    Get-Process explorer | Select-Object Id, SessionId
}

.EXAMPLE
# Neutralize a sparse package to reproduce CI's unsigned/classic scenario.
./Invoke-GuestScript.ps1 -VmName PowerToysUiTest-Win11 -ScriptBlock {
    Get-AppxPackage -AllUsers *ImageResizerContextMenu* | ForEach-Object { Remove-AppxPackage -Package $_.PackageFullName -AllUsers }
    Rename-Item C:\PowerToysUiTestRun\PowerToys\WinUI3Apps\ImageResizerContextMenuPackage.msix -NewName ImageResizerContextMenuPackage.msix.disabled
}
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)][scriptblock]$ScriptBlock,
    [Parameter(Mandatory)][string]$VmName,
    [object[]]$ArgumentList = @(),
    [string]$CredentialPath = (Join-Path $env:LOCALAPPDATA 'PowerToysUiTestVm\admin.credential.xml')
)

$ErrorActionPreference = 'Stop'
if (-not (Test-Path $CredentialPath)) {
    throw "Credential file not found: $CredentialPath. Point -CredentialPath at the VM's admin.credential.xml."
}
$credential = Import-Clixml $CredentialPath

try {
    Import-Module Hyper-V -ErrorAction Stop
    Get-VM -ErrorAction Stop | Out-Null
}
catch {
    throw 'BLOCKED: Hyper-V is not accessible from this shell. Run from an elevated PowerShell 7 terminal, or add this account to the local "Hyper-V Administrators" group.'
}
$session = New-PSSession -VMName $VmName -Credential $credential

try {
    Invoke-Command -Session $session -ScriptBlock $ScriptBlock -ArgumentList $ArgumentList
}
finally {
    Remove-PSSession $session -ErrorAction SilentlyContinue
}
