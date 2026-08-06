# Copyright (c) Microsoft Corporation
# The Microsoft Corporation licenses this file to you under the MIT license.
# See the LICENSE file in the project root for more information.

<#
.SYNOPSIS
Completes the local UI-test VM host setup - the steps an agent cannot perform itself.

.DESCRIPTION
Three prerequisites gate every agent-driven run, and all three need a human:

  1. Hyper-V access      Membership in the local Hyper-V Administrators group. Needs elevation, and
                         only takes effect after signing out and back in.
  2. Guest credential    A DPAPI-protected PSCredential for the guest administrator. The password is
                         typed straight into the prompt so it never reaches a command line, a
                         configuration file, source control, or a model.
  3. The guest itself    New-UiTestVm.ps1 reads the installation media and creates the virtual disk,
                         which needs an elevated shell.

Run this once per host from an elevated PowerShell 7 terminal. It reports what is already in place,
performs only what is missing, and is safe to re-run.

Agents: run it with -CheckOnly (no elevation, changes nothing) and, when it reports NotReady, stop
and ask the user to run the command it prints. Do not attempt to work around it.

.EXAMPLE
# Human, elevated - the whole setup in one command.
pwsh ./Initialize-LocalVmHost.ps1 -VmRoot C:\PowerToysUiTestVm -InstallMedia C:\media\Win11.iso

.EXAMPLE
# Agent - observe only.
pwsh ./Initialize-LocalVmHost.ps1 -VmRoot C:\PowerToysUiTestVm -CheckOnly
#>

[CmdletBinding(SupportsShouldProcess)]
param(
    [Parameter(Mandatory)]
    [string]$VmRoot,

    [string]$ConfigPath,
    [string]$InstallMedia,
    [string]$ImageName = 'Windows 11 Pro',

    # Account that will run the tests. Defaults to whoever runs this script.
    [string]$Account = "$env:USERDOMAIN\$env:USERNAME",
    [string]$AdminUserName,
    [string]$CredentialPath = (Join-Path $env:LOCALAPPDATA 'PowerToysUiTestVm\admin.credential.xml'),

    [switch]$CheckOnly,
    [switch]$SkipGroupMembership,
    [switch]$SkipCredential,
    [switch]$SkipGuestCreation,
    [switch]$AllowReFsVolume,
    [switch]$Force
)

$ErrorActionPreference = 'Stop'

if ($PSVersionTable.PSVersion.Major -lt 7) {
    throw 'Run this script with PowerShell 7 (pwsh).'
}

Import-Module (Join-Path $PSScriptRoot 'LocalVmGuest.psm1') -Force

$vmRootPath = [IO.Path]::GetFullPath($VmRoot)
if (-not (Test-Path $vmRootPath -PathType Container)) {
    throw "VM root was not found: $vmRootPath. Run Initialize-LocalVm.ps1 -DestinationRoot $vmRootPath first."
}
if ([string]::IsNullOrWhiteSpace($ConfigPath)) {
    $ConfigPath = Join-Path $vmRootPath 'vm.config.psd1'
}
if (-not (Test-Path $ConfigPath -PathType Leaf)) {
    throw "Configuration was not found: $ConfigPath. Copy vm.config.example.psd1 to vm.config.psd1 and edit it."
}

$configuration = Import-PowerShellDataFile $ConfigPath
$vmName = [string]$configuration.VmName
if ([string]::IsNullOrWhiteSpace($AdminUserName)) {
    $AdminUserName = [string]$configuration.AdminUserName
}

function Test-Elevation {
    return ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole(
        [Security.Principal.WindowsBuiltInRole]::Administrator)
}

function Write-Status {
    param([Parameter(Mandatory)]$Status)

    Write-Host ''
    Write-Host "Local UI-test VM host setup - $vmName"
    foreach ($row in @(
            @{ Name = 'Hyper-V access'; Ok = $Status.HyperVAccess; Detail = $Status.HyperVAccessDetail },
            @{ Name = 'Guest credential'; Ok = $Status.Credential; Detail = $Status.CredentialDetail },
            @{ Name = 'Guest'; Ok = $Status.Guest; Detail = $Status.GuestDetail })) {
        $mark = if ($row.Ok) { '[ok]     ' } else { '[missing]' }
        Write-Host ("  {0} {1,-17} {2}" -f $mark, $row.Name, $row.Detail)
    }
    Write-Host ''
}

$status = Test-LocalVmHostSetup -VmName $vmName -CredentialPath $CredentialPath -AdminUserName $AdminUserName
Write-Status -Status $status

if ($CheckOnly) {
    if (-not $status.IsReady) {
        $media = if ([string]::IsNullOrWhiteSpace($InstallMedia)) { '<windows.iso>' } else { $InstallMedia }
        Write-Host (Get-LocalVmSetupMessage -Status $status -VmRoot $vmRootPath -InstallMedia $media)
    }
    [pscustomobject]@{
        VmName = $vmName
        IsReady = $status.IsReady
        Missing = $status.Missing
        CredentialPath = $status.CredentialPath
    } | ConvertTo-Json -Depth 3
    exit ($(if ($status.IsReady) { 0 } else { 1 }))
}

$elevated = Test-Elevation
$needsElevation = (-not $status.HyperVAccess -and -not $SkipGroupMembership) -or
                  (-not $status.Guest -and -not $SkipGuestCreation)
if ($needsElevation -and -not $elevated) {
    throw @"
BLOCKED: this run needs an elevated PowerShell 7 terminal.
Missing: $($status.Missing -join ', ')

Start an elevated pwsh and re-run:
  pwsh -File "$PSCommandPath" -VmRoot "$vmRootPath"$(if ($InstallMedia) { " -InstallMedia `"$InstallMedia`"" })
"@
}

# 1. Hyper-V access -------------------------------------------------------------------------------
if (-not $status.HyperVAccess -and -not $SkipGroupMembership) {
    $member = $Account
    Write-Host "Adding '$member' to the local Hyper-V Administrators group..."
    if ($PSCmdlet.ShouldProcess($member, 'Add to Hyper-V Administrators')) {
        try {
            Add-LocalGroupMember -Group 'Hyper-V Administrators' -Member $member -ErrorAction Stop
            Write-Host "Added '$member'."
        }
        catch [Microsoft.PowerShell.Commands.MemberExistsException] {
            Write-Host "'$member' is already a member."
        }
    }

    Write-Warning @'
Group membership is baked into the logon token, so this session cannot use it yet.
SIGN OUT AND BACK IN, then re-run this script to finish the remaining steps.
'@
    return
}

# 2. Guest credential -----------------------------------------------------------------------------
if (-not $status.Credential -and -not $SkipCredential) {
    Write-Host "Saving the guest administrator credential for '$AdminUserName'."
    Write-Host 'Type the password directly into the prompt. Any password works: it only ever exists'
    Write-Host 'inside the disposable guest. Never reuse a real account password here.'

    if ($PSCmdlet.ShouldProcess($CredentialPath, 'Save the DPAPI guest credential')) {
        New-Item (Split-Path $CredentialPath -Parent) -ItemType Directory -Force | Out-Null
        $credential = Get-Credential -UserName $AdminUserName -Message "Local UI-test VM administrator ($vmName)"
        if ($null -eq $credential) {
            throw 'No credential was entered.'
        }
        if (($credential.UserName -replace '^.*\\', '') -ne $AdminUserName) {
            throw "The credential must be for '$AdminUserName' to match $ConfigPath."
        }
        $credential | Export-Clixml $CredentialPath
        Write-Host "Saved: $CredentialPath (decryptable only by $env:USERNAME on this host)."
    }
}

# 3. The guest ------------------------------------------------------------------------------------
if (-not $status.Guest -and -not $SkipGuestCreation) {
    if ([string]::IsNullOrWhiteSpace($InstallMedia)) {
        throw @"
BLOCKED: -InstallMedia is required to create '$vmName'.
Obtain media first, for example:
  pwsh "$(Join-Path $PSScriptRoot 'Get-WindowsMedia.ps1')" -Source Fido -Windows 11 -Architecture x64 -DestinationRoot "$(Join-Path $vmRootPath 'media')"
"@
    }

    $newVmScript = Join-Path $vmRootPath 'New-UiTestVm.ps1'
    if (-not (Test-Path $newVmScript -PathType Leaf)) {
        throw "New-UiTestVm.ps1 was not found in $vmRootPath. Re-run Initialize-LocalVm.ps1."
    }

    Write-Host "Creating '$vmName' from $InstallMedia. Windows Setup runs inside the guest; this takes a while and needs no interaction."
    $arguments = @{
        ConfigPath = $ConfigPath
        InstallMedia = $InstallMedia
        ImageName = $ImageName
        CredentialPath = $CredentialPath
    }
    if ($AllowReFsVolume) { $arguments.AllowReFsVolume = $true }
    if ($Force) { $arguments.Force = $true }
    & $newVmScript @arguments
}

$final = Test-LocalVmHostSetup -VmName $vmName -CredentialPath $CredentialPath -AdminUserName $AdminUserName
Write-Status -Status $final
if ($final.IsReady) {
    Write-Host 'Host setup is complete. The agent can now drive Invoke-LocalVmUiTest.ps1 unattended.'
}
else {
    Write-Warning "Still missing: $($final.Missing -join ', '). Re-run this script."
}

[pscustomobject]@{
    VmName = $vmName
    IsReady = $final.IsReady
    Missing = $final.Missing
    CredentialPath = $final.CredentialPath
} | ConvertTo-Json -Depth 3
