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
    [string]$VcRedistUrl,
    [string]$PowerShellVersion = '7.6.4',
    [string]$PowerShellUrl,
    [string]$PowerShellSha256,

    [switch]$CheckOnly,
    [switch]$SkipScaffoldRefresh,
    [switch]$SkipVcRedist,
    [switch]$SkipPowerShell,
    [switch]$SkipWindowsUpdate,
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

# Existing scaffolds are copies, so they do not receive skill fixes automatically. Refresh before
# every mutating setup run; vm.config.psd1, media, VHDX files, and extra OEM installers are preserved.
if (-not $CheckOnly -and -not $SkipScaffoldRefresh) {
    Write-Host 'Refreshing the VM scaffold from the current skill templates...'
    & (Join-Path $PSScriptRoot 'Initialize-LocalVm.ps1') -DestinationRoot $vmRootPath -Force | Out-Null
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

$vcArchitecture = switch ([string]$configuration.ProcessorArchitecture) {
    'arm64' { 'arm64' }
    default { 'x64' }
}
$vcRedistPath = Join-Path $vmRootPath "oem\vc_redist.$vcArchitecture.exe"
$vcRedistReady = if (Test-Path $vcRedistPath -PathType Leaf) {
    $existingVcSignature = Get-AuthenticodeSignature $vcRedistPath
    $existingVcSignature.Status -eq 'Valid' -and
        $null -ne $existingVcSignature.SignerCertificate -and
        $existingVcSignature.SignerCertificate.Subject -like 'CN=Microsoft Corporation*'
}
else {
    $false
}
$powerShellPath = Join-Path $vmRootPath "oem\PowerShell-$PowerShellVersion-win-$vcArchitecture.msi"
$knownPowerShellHashes = @{
    'x64' = 'D11942DF52FD12470169797ABFA4781D9480EFDC81000BA4FA55A5B921ED8DD0'
    'arm64' = '9B441D52176BEFD22B3AADF34F2F43F3A6F692C8D0181815169A397236B33D1F'
}
$expectedPowerShellHash = if ([string]::IsNullOrWhiteSpace($PowerShellSha256)) {
    if ($PowerShellVersion -ne '7.6.4') {
        throw '-PowerShellSha256 is required when overriding the pinned PowerShell version 7.6.4.'
    }
    $knownPowerShellHashes[$vcArchitecture]
}
else {
    $PowerShellSha256.ToUpperInvariant()
}
$powerShellPayloadValid = if (Test-Path $powerShellPath -PathType Leaf) {
    $existingHash = (Get-FileHash $powerShellPath -Algorithm SHA256).Hash
    $existingSignature = Get-AuthenticodeSignature $powerShellPath
    $existingSignerIsMicrosoft = $null -ne $existingSignature.SignerCertificate -and
        $existingSignature.SignerCertificate.Subject -like 'CN=Microsoft Corporation*'
    $existingHash -eq $expectedPowerShellHash -and
        $existingSignature.Status -eq 'Valid' -and
        $existingSignerIsMicrosoft
}
else {
    $false
}
$powerShellMetadataPath = "$powerShellPath.sha256"
$powerShellMetadataValid = (Test-Path $powerShellMetadataPath -PathType Leaf) -and
    ((Get-Content $powerShellMetadataPath -Raw).Trim() -eq $expectedPowerShellHash)
$powerShellReady = $powerShellPayloadValid -and $powerShellMetadataValid

function Install-PowerShellPayload {
    if ($powerShellReady) {
        return
    }
    if ($SkipPowerShell) {
        return
    }
    if ($powerShellPayloadValid) {
        Set-Content $powerShellMetadataPath $expectedPowerShellHash -Encoding ascii
        $script:powerShellMetadataValid = $true
        $script:powerShellReady = $true
        Write-Host "Repaired PowerShell payload trust metadata: $powerShellMetadataPath"
        return
    }

    $url = if ([string]::IsNullOrWhiteSpace($PowerShellUrl)) {
        "https://github.com/PowerShell/PowerShell/releases/download/v$PowerShellVersion/PowerShell-$PowerShellVersion-win-$vcArchitecture.msi"
    }
    else {
        $PowerShellUrl
    }

    New-Item (Split-Path $powerShellPath -Parent) -ItemType Directory -Force | Out-Null
    $temporaryPath = "$powerShellPath.download"
    Write-Host "Downloading PowerShell $PowerShellVersion for guest-side test orchestration..."
    Invoke-WebRequest -Uri $url -OutFile $temporaryPath

    $actualHash = (Get-FileHash $temporaryPath -Algorithm SHA256).Hash
    $signature = Get-AuthenticodeSignature $temporaryPath
    $isMicrosoft = $null -ne $signature.SignerCertificate -and
        $signature.SignerCertificate.Subject -like 'CN=Microsoft Corporation*'
    if ($actualHash -ne $expectedPowerShellHash -or $signature.Status -ne 'Valid' -or -not $isMicrosoft) {
        Remove-Item $temporaryPath -Force -ErrorAction SilentlyContinue
        throw "Refusing PowerShell payload '$url': sha256=$actualHash, expected=$expectedPowerShellHash, signature=$($signature.Status), signer='$($signature.SignerCertificate.Subject)'."
    }

    Move-Item $temporaryPath $powerShellPath -Force
    Set-Content $powerShellMetadataPath $expectedPowerShellHash -Encoding ascii
    $script:powerShellPayloadValid = $true
    $script:powerShellMetadataValid = $true
    $script:powerShellReady = $true
    Write-Host "Staged verified PowerShell ${PowerShellVersion}: $powerShellPath"
}

function Install-VcRedistPayload {
    if ($vcRedistReady -or $SkipVcRedist) {
        return
    }

    $url = if ([string]::IsNullOrWhiteSpace($VcRedistUrl)) {
        "https://aka.ms/vs/17/release/vc_redist.$vcArchitecture.exe"
    }
    else {
        $VcRedistUrl
    }

    New-Item (Split-Path $vcRedistPath -Parent) -ItemType Directory -Force | Out-Null
    $temporaryPath = "$vcRedistPath.download"
    Write-Host "Downloading the Visual C++ redistributable required for MP4 capture..."
    Invoke-WebRequest -Uri $url -OutFile $temporaryPath

    $signature = Get-AuthenticodeSignature $temporaryPath
    $isMicrosoft = $null -ne $signature.SignerCertificate -and
        $signature.SignerCertificate.Subject -like 'CN=Microsoft Corporation*'
    if ($signature.Status -ne 'Valid' -or -not $isMicrosoft) {
        Remove-Item $temporaryPath -Force -ErrorAction SilentlyContinue
        throw "Refusing VC++ redistributable from '$url': signature=$($signature.Status), signer='$($signature.SignerCertificate.Subject)'."
    }

    Move-Item $temporaryPath $vcRedistPath -Force
    $script:vcRedistReady = $true
    Write-Host "Staged the signed Microsoft redistributable: $vcRedistPath"
}

function Test-Elevation {
    return ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole(
        [Security.Principal.WindowsBuiltInRole]::Administrator)
}

function Test-HyperVAdministratorsMembership {
    param([Parameter(Mandatory)][string]$Member)

    try {
        $group = Get-LocalGroup -SID 'S-1-5-32-578' -ErrorAction Stop
        $targetSid = ([Security.Principal.NTAccount]$Member).Translate(
            [Security.Principal.SecurityIdentifier]).Value
        return @(Get-LocalGroupMember -Group $group.Name -ErrorAction Stop |
            Where-Object { $_.SID.Value -eq $targetSid }).Count -gt 0
    }
    catch {
        return $false
    }
}

$accountHasHyperVAccess = $SkipGroupMembership -or (Test-HyperVAdministratorsMembership -Member $Account)

function Write-Status {
    param([Parameter(Mandatory)]$Status)

    Write-Host ''
    Write-Host "Local UI-test VM host setup - $vmName"
    foreach ($row in @(
            @{ Name = 'Hyper-V access'; Ok = $Status.HyperVAccess; Detail = $Status.HyperVAccessDetail },
            @{ Name = 'Agent Hyper-V group'; Ok = $accountHasHyperVAccess; Detail = $(if ($accountHasHyperVAccess) { "ok ($Account)" } else { "missing: $Account must join Hyper-V Administrators and sign out/in" }) },
            @{ Name = 'Guest credential'; Ok = $Status.Credential; Detail = $Status.CredentialDetail },
            @{ Name = 'Video prerequisite'; Ok = $vcRedistReady; Detail = $(if ($vcRedistReady) { "ok ($vcArchitecture)" } else { "missing: $vcRedistPath" }) },
            @{ Name = 'PowerShell 7'; Ok = $powerShellReady; Detail = $(if ($powerShellReady) { "ok ($PowerShellVersion)" } else { "missing: $powerShellPath" }) },
            @{ Name = 'Guest'; Ok = $Status.Guest; Detail = $Status.GuestDetail })) {
        $mark = if ($row.Ok) { '[ok]     ' } else { '[missing]' }
        Write-Host ("  {0} {1,-17} {2}" -f $mark, $row.Name, $row.Detail)
    }
    Write-Host ''
}

$status = Test-LocalVmHostSetup -VmName $vmName -CredentialPath $CredentialPath -AdminUserName $AdminUserName
Write-Status -Status $status

if ($CheckOnly) {
    $allMissing = @($status.Missing)
    if (-not $accountHasHyperVAccess) {
        $allMissing += 'HyperVAdministratorsMembership'
    }
    if (-not $vcRedistReady) {
        $allMissing += 'VideoPrerequisite'
    }
    if (-not $powerShellReady) {
        $allMissing += 'PowerShell7'
    }
    $allReady = $status.IsReady -and $accountHasHyperVAccess -and $vcRedistReady -and $powerShellReady

    if (-not $allReady) {
        $media = if ([string]::IsNullOrWhiteSpace($InstallMedia)) { '<windows.iso>' } else { $InstallMedia }
        Write-Host (Get-LocalVmSetupMessage `
            -Status $status `
            -VmRoot $vmRootPath `
            -InstallMedia $media `
            -ConfigPath $ConfigPath `
            -CredentialPath $CredentialPath)
    }
    [pscustomobject]@{
        VmName = $vmName
        IsReady = $allReady
        Missing = $allMissing
        CredentialPath = $status.CredentialPath
        HyperVAdministratorsMembership = $accountHasHyperVAccess
        VcRedistReady = $vcRedistReady
        PowerShellReady = $powerShellReady
    } | ConvertTo-Json -Depth 3
    exit ($(if ($allReady) { 0 } else { 1 }))
}

$elevated = Test-Elevation
$needsElevation = (-not $accountHasHyperVAccess -and -not $SkipGroupMembership) -or
                  ((-not $status.Guest -or $Force) -and -not $SkipGuestCreation)
if ($needsElevation -and -not $elevated) {
    throw @"
BLOCKED: this run needs an elevated PowerShell 7 terminal.
Missing: $($status.Missing -join ', ')

Start an elevated pwsh and re-run:
    pwsh -File "$PSCommandPath" -VmRoot "$vmRootPath" -ConfigPath "$ConfigPath" -CredentialPath "$CredentialPath"$(if ($InstallMedia) { " -InstallMedia `"$InstallMedia`"" })
"@
}

# 1. Hyper-V access -------------------------------------------------------------------------------
if (-not $accountHasHyperVAccess -and -not $SkipGroupMembership) {
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

# 3. Recording prerequisite -----------------------------------------------------------------------
Install-VcRedistPayload
Install-PowerShellPayload

# 4. The guest ------------------------------------------------------------------------------------
if ((-not $status.Guest -or $Force) -and -not $SkipGuestCreation) {
    if ([string]::IsNullOrWhiteSpace($InstallMedia)) {
        throw @"
BLOCKED: -InstallMedia is required to create '$vmName'.
Obtain media first, for example:
  pwsh "$(Join-Path $PSScriptRoot 'Get-WindowsMedia.ps1')" -Source Fido -Windows 11 -Architecture x64 -DestinationRoot "$(Join-Path $vmRootPath 'media')"
"@
    }

    # Execute the source template, not the scaffold copy. A copied script can be stale after the
    # skill is updated (the PowerShell 5.1 readiness-query fix exposed exactly this failure mode).
    $newVmScript = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\templates\vm\New-UiTestVm.ps1'))
    if (-not (Test-Path $newVmScript -PathType Leaf)) {
        throw "New-UiTestVm.ps1 template was not found: $newVmScript"
    }

    Write-Host "Creating '$vmName' from $InstallMedia. Windows Setup runs inside the guest; this takes a while and needs no interaction."
    $arguments = @{
        ConfigPath = $ConfigPath
        InstallMedia = $InstallMedia
        ImageName = $ImageName
        CredentialPath = $CredentialPath
        OemPath = (Join-Path $vmRootPath 'oem')
    }
    if ($AllowReFsVolume) { $arguments.AllowReFsVolume = $true }
    if ($Force) { $arguments.Force = $true }
    & $newVmScript @arguments
}

# Windows Setup Dynamic Update is the primary Win10 servicing path. Verify its result against the
# .NET 10 CET floor (1904x.5007); only then use online Windows Update as a fallback and replace the
# pre-update checkpoint. Windows 11 media does not need this compatibility step.
if (-not $SkipWindowsUpdate -and (Get-VM -Name $vmName -ErrorAction SilentlyContinue)) {
    & ([IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\templates\vm\Start-LocalVm.ps1'))) `
        -ConfigPath $ConfigPath `
        -CredentialPath $CredentialPath `
        -Wait | Out-Null
    $credential = Import-Clixml $CredentialPath
    $session = New-PSSession -VMName $vmName -Credential $credential
    try {
        $guestVersion = Invoke-Command -Session $session -ScriptBlock {
            $currentVersion = Get-ItemProperty 'HKLM:\SOFTWARE\Microsoft\Windows NT\CurrentVersion'
            [pscustomobject]@{
                Build = [int]$currentVersion.CurrentBuild
                Ubr = [int]$currentVersion.UBR
                Display = "$($currentVersion.CurrentBuild).$($currentVersion.UBR)"
            }
        }
    }
    finally {
        Remove-PSSession $session -ErrorAction SilentlyContinue
    }

    if ($guestVersion.Build -lt 22000 -and
        ($guestVersion.Build -lt 19041 -or $guestVersion.Build -gt 19045 -or $guestVersion.Ubr -lt 5007)) {
        Write-Warning "Windows Setup Dynamic Update left '$vmName' at $($guestVersion.Display); .NET 10 needs 1904x.5007 or newer. Falling back to online Windows Update."
        & (Join-Path $PSScriptRoot 'Update-LocalVmGuest.ps1') `
            -VmName $vmName `
            -CredentialPath $CredentialPath

        & ([IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\templates\vm\Reset-LocalVm.ps1'))) `
            -ConfigPath $ConfigPath `
            -CreateBaseline | Out-Null
        Write-Host "Recreated '$($configuration.BaselineCheckpointName)' after Windows Update."
    }
}

$final = Test-LocalVmHostSetup -VmName $vmName -CredentialPath $CredentialPath -AdminUserName $AdminUserName
$accountHasHyperVAccess = $SkipGroupMembership -or (Test-HyperVAdministratorsMembership -Member $Account)
Write-Status -Status $final
$finalMissing = @($final.Missing)
if (-not $accountHasHyperVAccess) { $finalMissing += 'HyperVAdministratorsMembership' }
if (-not $vcRedistReady) { $finalMissing += 'VideoPrerequisite' }
if (-not $powerShellReady) { $finalMissing += 'PowerShell7' }
$allReady = $final.IsReady -and $accountHasHyperVAccess -and $vcRedistReady -and $powerShellReady
if ($allReady) {
    Write-Host 'Host setup is complete. The agent can now drive Invoke-LocalVmUiTest.ps1 unattended.'
}
else {
    Write-Warning "Still missing: $($finalMissing -join ', '). Re-run this script."
}

[pscustomobject]@{
    VmName = $vmName
    IsReady = $allReady
    Missing = $finalMissing
    CredentialPath = $final.CredentialPath
    HyperVAdministratorsMembership = $accountHasHyperVAccess
    VcRedistReady = $vcRedistReady
    PowerShellReady = $powerShellReady
} | ConvertTo-Json -Depth 3
