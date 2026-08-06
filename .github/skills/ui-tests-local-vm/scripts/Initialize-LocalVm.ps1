# Copyright (c) Microsoft Corporation
# The Microsoft Corporation licenses this file to you under the MIT license.
# See the LICENSE file in the project root for more information.

<#
.SYNOPSIS
Scaffolds a local Hyper-V UI-test VM directory for persistent PowerToys UI-test execution.

.DESCRIPTION
Copies the Hyper-V VM lifecycle scripts, the unattend template, and the OEM provisioning payload
into a working directory. Everything runs on the platform hypervisor, so no nested virtualization is
needed and the scaffold works on x64 and on Windows on ARM alike.

.EXAMPLE
pwsh ./Initialize-LocalVm.ps1 -DestinationRoot X:\PowerToysUiTestVm
#>

[CmdletBinding(SupportsShouldProcess)]
param(
    [Parameter(Mandatory)]
    [string]$DestinationRoot,
    [switch]$Force
)

$ErrorActionPreference = 'Stop'

if ($PSVersionTable.PSVersion.Major -lt 7) {
    throw 'Run this script with PowerShell 7 (pwsh).'
}

$templateRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\templates\vm'))
$oemTemplateRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\templates\oem'))
$destination = [IO.Path]::GetFullPath($DestinationRoot)
if (-not (Test-Path $templateRoot -PathType Container)) {
    throw "VM templates were not found: $templateRoot"
}
if (-not (Test-Path $oemTemplateRoot -PathType Container)) {
    throw "OEM templates were not found: $oemTemplateRoot"
}

if (Test-Path $destination -PathType Container) {
    $existingItems = @(Get-ChildItem $destination -Force)
    if ($existingItems.Count -gt 0 -and -not $Force) {
        throw "Destination is not empty: $destination. Pass -Force to merge and overwrite template files."
    }
}

if ($PSCmdlet.ShouldProcess($destination, 'Scaffold the local Hyper-V UI-test VM')) {
    New-Item $destination -ItemType Directory -Force | Out-Null
    Copy-Item (Join-Path $templateRoot '*') $destination -Recurse -Force
    New-Item (Join-Path $destination 'oem') -ItemType Directory -Force | Out-Null
    Copy-Item (Join-Path $oemTemplateRoot '*') (Join-Path $destination 'oem') -Recurse -Force
    New-Item (Join-Path $destination 'shared') -ItemType Directory -Force | Out-Null
}

[pscustomobject]@{
    VmRoot = $destination
    ConfigurationTemplate = (Join-Path $destination 'vm.config.example.psd1')
    NextSteps = @(
        "Copy vm.config.example.psd1 to vm.config.psd1 and set the VM name, paths, and architecture.",
        "Obtain media: pwsh $(Join-Path $PSScriptRoot 'Get-WindowsMedia.ps1') -Source Fido -Windows 11 -Architecture x64 -DestinationRoot $(Join-Path $destination 'media')",
        "HUMAN-ONLY, elevated, once: pwsh $(Join-Path $PSScriptRoot 'Initialize-LocalVmHost.ps1') -VmRoot $destination -InstallMedia <windows.iso>",
        "It joins Hyper-V Administrators, saves the DPAPI guest credential, and creates the guest - an agent cannot do any of these.",
        "Agents: verify with -CheckOnly and stop until it reports IsReady=true."
    )
} | ConvertTo-Json -Depth 4
