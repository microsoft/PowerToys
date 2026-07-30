# Copyright (c) Microsoft Corporation
# The Microsoft Corporation licenses this file to you under the MIT license.
# See the LICENSE file in the project root for more information.

<#
.SYNOPSIS
Scaffolds a dockur/windows VM directory for persistent PowerToys UI-test execution.

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
$destination = [IO.Path]::GetFullPath($DestinationRoot)
if (-not (Test-Path $templateRoot -PathType Container)) {
    throw "VM templates were not found: $templateRoot"
}

if (Test-Path $destination -PathType Container) {
    $existingItems = @(Get-ChildItem $destination -Force)
    if ($existingItems.Count -gt 0 -and -not $Force) {
        throw "Destination is not empty: $destination. Pass -Force to merge and overwrite template files."
    }
}

if ($PSCmdlet.ShouldProcess($destination, 'Scaffold local UI-test VM')) {
    New-Item $destination -ItemType Directory -Force | Out-Null
    Copy-Item (Join-Path $templateRoot '*') $destination -Recurse -Force
    New-Item (Join-Path $destination 'shared') -ItemType Directory -Force | Out-Null
}

[pscustomobject]@{
    VmRoot = $destination
    ComposeFile = Join-Path $destination 'compose.yml'
    EnvironmentTemplate = Join-Path $destination '.env.example'
    NextSteps = @(
        'Copy .env.example to .env and set a unique administrator password.',
        'Run Start-LocalVm.ps1 -WaitForWinRM from an elevated PowerShell 7 terminal.',
        'Save the administrator PSCredential with Export-Clixml as documented in references/setup.md.'
    )
} | ConvertTo-Json -Depth 4
