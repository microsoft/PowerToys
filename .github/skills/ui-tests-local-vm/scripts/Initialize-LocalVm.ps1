# Copyright (c) Microsoft Corporation
# The Microsoft Corporation licenses this file to you under the MIT license.
# See the LICENSE file in the project root for more information.

<#
.SYNOPSIS
Scaffolds a local UI-test VM directory for persistent PowerToys UI-test execution.

.DESCRIPTION
The Docker backend scaffolds a dockur/windows compose stack. The HyperV backend scaffolds a native
Hyper-V stack that needs no Docker Desktop, WSL2, or nested virtualization, but does require an
elevated host shell. Both scaffolds receive the same OEM provisioning payload.

.EXAMPLE
pwsh ./Initialize-LocalVm.ps1 -DestinationRoot X:\PowerToysUiTestVm

.EXAMPLE
pwsh ./Initialize-LocalVm.ps1 -Backend HyperV -DestinationRoot X:\PowerToysUiTestVm-HyperV
#>

[CmdletBinding(SupportsShouldProcess)]
param(
    [Parameter(Mandatory)]
    [string]$DestinationRoot,
    [ValidateSet('Docker', 'HyperV')]
    [string]$Backend = 'Docker',
    [switch]$Force
)

$ErrorActionPreference = 'Stop'

if ($PSVersionTable.PSVersion.Major -lt 7) {
    throw 'Run this script with PowerShell 7 (pwsh).'
}

$templateFolder = if ($Backend -eq 'HyperV') { 'vm-hyperv' } else { 'vm' }
$templateRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot "..\templates\$templateFolder"))
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

if ($PSCmdlet.ShouldProcess($destination, "Scaffold the $Backend local UI-test VM")) {
    New-Item $destination -ItemType Directory -Force | Out-Null
    Copy-Item (Join-Path $templateRoot '*') $destination -Recurse -Force
    New-Item (Join-Path $destination 'oem') -ItemType Directory -Force | Out-Null
    Copy-Item (Join-Path $oemTemplateRoot '*') (Join-Path $destination 'oem') -Recurse -Force
    New-Item (Join-Path $destination 'shared') -ItemType Directory -Force | Out-Null
}

$nextSteps = if ($Backend -eq 'HyperV') {
    @(
        'Copy vm.config.example.psd1 to vm.config.psd1 and set the VM name, paths, and architecture.',
        'Save the administrator PSCredential with Get-Credential | Export-Clixml as documented in references/setup-hyperv.md.',
        'From an elevated PowerShell 7 terminal, run New-UiTestVm.ps1 with -InstallMedia or -BaseVhdx.'
    )
}
else {
    @(
        'Copy .env.example to .env and set a unique administrator password.',
        'Run Start-LocalVm.ps1 -WaitForWinRM from an elevated PowerShell 7 terminal.',
        'Save the administrator PSCredential with Export-Clixml as documented in references/setup.md.'
    )
}

[pscustomobject]@{
    Backend = $Backend
    VmRoot = $destination
    ConfigurationTemplate = if ($Backend -eq 'HyperV') {
        Join-Path $destination 'vm.config.example.psd1'
    }
    else {
        Join-Path $destination '.env.example'
    }
    RequiresElevation = ($Backend -eq 'HyperV')
    NextSteps = $nextSteps
} | ConvertTo-Json -Depth 4
