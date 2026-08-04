# Copyright (c) Microsoft Corporation
# The Microsoft Corporation licenses this file to you under the MIT license.
# See the LICENSE file in the project root for more information.

<#
.SYNOPSIS
Runs a scriptblock inside the dockur/windows UI-test guest over WinRM, handling the credential
import and PSSession lifecycle. Token-efficient replacement for the repeated
Import-Clixml / New-PSSession / Invoke-Command / Remove-PSSession boilerplate when inspecting or
mutating guest state (package registration, staged runtime files, registry, processes).

.PARAMETER ScriptBlock
The scriptblock to run in the guest. Its output is returned to the host.

.PARAMETER WinRmPort
Host loopback WinRM port mapped to the guest (Win11 scaffold default 15987 HTTPS; Win10 15985 HTTP).

.PARAMETER UseHttp
Use http:// (unencrypted Basic) instead of https:// — the older Win10 manual scheme. The guest's
WSMan client must already allow Basic/unencrypted (the Win10 controller path configures this).

.EXAMPLE
./Invoke-GuestScript.ps1 -WinRmPort 15987 -CredentialPath "$env:LOCALAPPDATA\PowerToysUiTestVm-Win11\admin.credential.xml" -ScriptBlock {
    Get-AppxPackage *ImageResizerContextMenu* | Select-Object -Expand Name
}

.EXAMPLE
# Neutralize a sparse package to reproduce CI's unsigned/classic scenario.
./Invoke-GuestScript.ps1 -WinRmPort 15987 -CredentialPath $cred -ScriptBlock {
    Get-AppxPackage -AllUsers *ImageResizerContextMenu* | ForEach-Object { Remove-AppxPackage -Package $_.PackageFullName -AllUsers }
    Rename-Item C:\PowerToysUiTestRun\PowerToys\WinUI3Apps\ImageResizerContextMenuPackage.msix -NewName ImageResizerContextMenuPackage.msix.disabled
}
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)][scriptblock]$ScriptBlock,
    [object[]]$ArgumentList = @(),
    [int]$WinRmPort = 15987,
    [switch]$UseHttp,
    [string]$CredentialPath = (Join-Path $env:LOCALAPPDATA 'PowerToysUiTestVm-Win11\admin.credential.xml')
)

$ErrorActionPreference = 'Stop'
if (-not (Test-Path $CredentialPath)) {
    throw "Credential file not found: $CredentialPath. Point -CredentialPath at the VM's admin.credential.xml."
}
$credential = Import-Clixml $CredentialPath

if ($UseHttp) {
    $session = New-PSSession -ConnectionUri "http://127.0.0.1:$WinRmPort/wsman" -Authentication Basic -Credential $credential
}
else {
    $sessionOption = New-PSSessionOption -SkipCACheck -SkipCNCheck -SkipRevocationCheck
    $session = New-PSSession -ConnectionUri "https://127.0.0.1:$WinRmPort/wsman" -Authentication Basic -Credential $credential -SessionOption $sessionOption
}

try {
    Invoke-Command -Session $session -ScriptBlock $ScriptBlock -ArgumentList $ArgumentList
}
finally {
    Remove-PSSession $session -ErrorAction SilentlyContinue
}
