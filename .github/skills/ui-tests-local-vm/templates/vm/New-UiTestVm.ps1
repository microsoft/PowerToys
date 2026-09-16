# Copyright (c) Microsoft Corporation
# The Microsoft Corporation licenses this file to you under the MIT license.
# See the LICENSE file in the project root for more information.

<#
.SYNOPSIS
Builds a persistent Hyper-V guest for PowerToys UI tests by running Windows Setup inside the guest.

.DESCRIPTION
Creates an empty virtual disk, attaches the Windows installation media plus a generated answer-file
ISO, and lets Windows Setup partition and install from inside the virtual machine. Nothing about the
guest disk is ever prepared from the host, so every boot reference Setup writes is correct by
construction.

An earlier design applied the image with DISM and ran bcdboot on the host against the mounted VHDX.
That is the technique Convert-WindowsImage uses, but its goal is native-VHD boot, so bcdboot records
'vhd=[X:]\path\to.vhdx' device references. Inside a virtual machine that file does not exist - the
VHDX is the disk - and the guest fails with 0xc000000e. Repairing it from the host is not possible
either, because bcdedit resolves drive letters through the host's view and rewrites them straight
back into vhd= references.

The administrator password is never accepted as a parameter or stored in the configuration file. It
is read from a DPAPI-protected credential file and written to the answer file using the base64
obfuscation Windows expects, so no plaintext password reaches the media.

.EXAMPLE
pwsh ./New-UiTestVm.ps1 -InstallMedia D:\media\Win11_25H2_English_Arm64_v2.iso -ListImages

.EXAMPLE
pwsh ./New-UiTestVm.ps1 -InstallMedia D:\media\Win11_25H2_English_Arm64_v2.iso -ImageName 'Windows 11 Pro'
#>

[CmdletBinding(SupportsShouldProcess)]
param(
    [string]$ConfigPath = (Join-Path $PSScriptRoot 'vm.config.psd1'),
    [string]$InstallMedia,
    [string]$ImageName = 'Windows 11 Pro',
    [string]$CredentialPath = (Join-Path $env:LOCALAPPDATA 'PowerToysUiTestVm\admin.credential.xml'),
    [string]$OemPath = (Join-Path $PSScriptRoot 'oem'),
    [ValidateRange(5, 720)]
    [int]$TimeoutMinutes = 90,
    [switch]$ListImages,
    [switch]$PlanOnly,
    [switch]$AllowReFsVolume,
    [switch]$Force
)

$ErrorActionPreference = 'Stop'

if ($PSVersionTable.PSVersion.Major -lt 7) {
    throw 'Run this script with PowerShell 7 (pwsh).'
}

function Test-Elevation {
    $identity = [Security.Principal.WindowsIdentity]::GetCurrent()
    return ([Security.Principal.WindowsPrincipal]$identity).IsInRole(
        [Security.Principal.WindowsBuiltInRole]::Administrator)
}

function ConvertTo-UnattendPassword {
    <#
    .SYNOPSIS
    Applies the base64 obfuscation Windows expects for answer-file passwords.

    .DESCRIPTION
    Windows appends the name of the containing element to the password before base64-encoding the
    UTF-16LE bytes. This is obfuscation, not encryption, but it keeps the plaintext off the media.
    #>
    param(
        [Parameter(Mandatory)][AllowEmptyString()][string]$Password,
        [Parameter(Mandatory)][ValidateSet('Password', 'AdministratorPassword')][string]$Element
    )

    return [Convert]::ToBase64String([Text.Encoding]::Unicode.GetBytes($Password + $Element))
}

function Assert-SupportedVmVolume {
    <#
    .SYNOPSIS
    Refuses to store the guest on a ReFS volume such as a Dev Drive.

    .DESCRIPTION
    Keeping the VHDX on a Dev Drive has been observed to wedge the Hyper-V management service:
    subsequent management calls, including read-only ones, never return, and recovery needs a vmms
    restart or a reboot. Hyper-V on plain ReFS is supported, so this refusal is deliberately
    conservative - ReFS is a cheap proxy for "Dev Drive", which cannot be detected without elevation.
    Use -AllowReFsVolume on a known-good ReFS volume. Only VhdPath and VmPath are checked; the
    exchange is ordinary file I/O and needs no such restriction.
    #>
    param(
        [Parameter(Mandatory)][string]$Path,
        [switch]$Allow
    )

    $root = [IO.Path]::GetPathRoot([IO.Path]::GetFullPath($Path))
    if ($root -notmatch '^(?<Letter>[A-Za-z]):\\$') {
        return
    }
    $volume = Get-Volume -DriveLetter $Matches.Letter -ErrorAction SilentlyContinue
    if ($null -eq $volume -or [string]$volume.FileSystemType -ne 'ReFS') {
        return
    }

    $message = "Guest storage '$Path' is on a $($volume.FileSystemType) volume ($($Matches.Letter):). Prefer NTFS: hosting a VHDX on a Dev Drive has been observed to hang the Hyper-V management service."
    if ($Allow) {
        Write-Warning $message
        return
    }
    throw "BLOCKED: $message Pass -AllowReFsVolume to override."
}

function Get-MountedImageRoot {
    <#
    .SYNOPSIS
    Returns the drive root of mounted installation media that carries a Windows image.

    .DESCRIPTION
    Uses System.IO.DriveInfo rather than Get-Volume: PowerShell 7 reaches the Storage cmdlets through
    the Windows PowerShell compatibility layer, where CIM enum properties do not compare reliably.
    #>
    param([Parameter(Mandatory)][string]$ImagePath)

    foreach ($drive in [IO.DriveInfo]::GetDrives() | Where-Object { $_.DriveType -eq 'CDRom' -and $_.IsReady }) {
        $root = $drive.Name.TrimEnd('\')
        foreach ($name in 'install.wim', 'install.esd') {
            if (Test-Path (Join-Path $root "sources\$name") -PathType Leaf) {
                return $root
            }
        }
    }

    throw "No Windows image was found on the mounted media: $ImagePath"
}

function Get-WindowsImageList {
    param([Parameter(Mandatory)][string]$MediaRoot)

    foreach ($name in 'install.wim', 'install.esd') {
        $candidate = Join-Path $MediaRoot "sources\$name"
        if (Test-Path $candidate -PathType Leaf) {
            return @(Get-WindowsImage -ImagePath $candidate)
        }
    }

    throw "No install.wim or install.esd was found under $MediaRoot\sources."
}

function New-DataIso {
    <#
    .SYNOPSIS
    Builds a small ISO from a folder using the in-box IMAPI2 file system image COM API.

    .DESCRIPTION
    Windows Setup only reads autounattend.xml from removable media, and generation 2 virtual
    machines have no floppy controller, so the answer file has to be delivered as a second optical
    disc. This avoids a dependency on oscdimg from the ADK.
    #>
    param(
        [Parameter(Mandatory)][string]$SourceFolder,
        [Parameter(Mandatory)][string]$Destination,
        [string]$VolumeName = 'PTUNATTEND'
    )

    if (-not ('PowerToysUiTestVm.IsoWriter' -as [type])) {
        Add-Type -Namespace PowerToysUiTestVm -Name IsoWriter -MemberDefinition @'
[System.Runtime.InteropServices.DllImport("shlwapi.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode, ExactSpelling = true, PreserveSig = false)]
private static extern void SHCreateStreamOnFileEx(string fileName, uint grfMode, uint dwAttributes,
    bool fCreate, System.Runtime.InteropServices.ComTypes.IStream reserved,
    out System.Runtime.InteropServices.ComTypes.IStream ppstm);

public static void Write(object imageStream, string path)
{
    System.Runtime.InteropServices.ComTypes.IStream source =
        (System.Runtime.InteropServices.ComTypes.IStream)imageStream;
    System.Runtime.InteropServices.ComTypes.IStream target = null;
    try
    {
        // STGM_CREATE | STGM_WRITE, FILE_ATTRIBUTE_NORMAL
        SHCreateStreamOnFileEx(path, 0x00001001, 0x80, true, null, out target);
        source.CopyTo(target, long.MaxValue, System.IntPtr.Zero, System.IntPtr.Zero);
        target.Commit(0);
    }
    finally
    {
        if (target != null && System.Runtime.InteropServices.Marshal.IsComObject(target))
        {
            System.Runtime.InteropServices.Marshal.FinalReleaseComObject(target);
        }
    }
}
'@
    }

    Remove-Item $Destination -Force -ErrorAction SilentlyContinue
    $fileSystemImage = New-Object -ComObject IMAPI2FS.MsftFileSystemImage
    $resultImage = $null
    $imageStream = $null
    try {
        $fileSystemImage.FileSystemsToCreate = 3 # ISO9660 | Joliet
        $fileSystemImage.VolumeName = $VolumeName
        $fileSystemImage.Root.AddTree($SourceFolder, $false)
        $resultImage = $fileSystemImage.CreateResultImage()
        $imageStream = $resultImage.ImageStream
        [PowerToysUiTestVm.IsoWriter]::Write($imageStream, $Destination)
    }
    finally {
        if ($null -ne $imageStream) {
            [Runtime.InteropServices.Marshal]::FinalReleaseComObject($imageStream) | Out-Null
        }
        if ($null -ne $resultImage) {
            [Runtime.InteropServices.Marshal]::FinalReleaseComObject($resultImage) | Out-Null
        }
        [Runtime.InteropServices.Marshal]::ReleaseComObject($fileSystemImage) | Out-Null
    }

    if (-not (Test-Path $Destination -PathType Leaf)) {
        throw "The answer-file ISO was not created: $Destination"
    }
}

function New-UnattendContent {
    param(
        [Parameter(Mandatory)][hashtable]$Configuration,
        [Parameter(Mandatory)][AllowEmptyString()][string]$ObfuscatedPassword,
        [Parameter(Mandatory)][string]$SelectedImageName,
        [Parameter(Mandatory)][string]$ProvisionArguments,
        [Parameter(Mandatory)][string]$TemplatePath
    )

    if (-not (Test-Path $TemplatePath -PathType Leaf)) {
        throw "Answer-file template was not found: $TemplatePath"
    }

    $tokens = @{
        '{{ARCH}}' = $Configuration.ProcessorArchitecture
        '{{COMPUTERNAME}}' = $Configuration.ComputerName
        '{{LOCALE}}' = $Configuration.Locale
        '{{TIMEZONE}}' = $Configuration.TimeZone
        '{{ADMINUSER}}' = $Configuration.AdminUserName
        '{{ADMINPASSWORD}}' = $ObfuscatedPassword
        '{{IMAGENAME}}' = [Security.SecurityElement]::Escape($SelectedImageName)
        '{{PROVISIONARGUMENTS}}' = $ProvisionArguments
    }
    $content = Get-Content $TemplatePath -Raw
    foreach ($token in $tokens.GetEnumerator()) {
        $content = $content.Replace($token.Key, [string]$token.Value)
    }
    if ($content -match '\{\{[A-Z]+\}\}') {
        throw "The answer-file template still contains unresolved placeholders: $($Matches[0])"
    }
    [xml]$content | Out-Null
    return $content
}

function Get-GuestScreenLevel {
    <#
    .SYNOPSIS
    Returns the mean intensity of the guest framebuffer, used to tell a text-mode boot prompt from a
    graphical Setup screen.

    .DESCRIPTION
    The firmware and the "Press any key to boot from CD or DVD" prompt are near-black; Setup's UI is
    a saturated blue. Averaging the raw RGB565 bytes separates the two without decoding the image.
    #>
    param([Parameter(Mandatory)][string]$VmName)

    $namespace = 'root\virtualization\v2'
    $service = Get-CimInstance -Namespace $namespace -ClassName Msvm_VirtualSystemManagementService
    $system = Get-CimInstance -Namespace $namespace -ClassName Msvm_ComputerSystem -Filter "ElementName='$VmName'"
    $settings = Get-CimAssociatedInstance -InputObject $system -ResultClassName Msvm_VirtualSystemSettingData |
        Where-Object { $_.VirtualSystemType -eq 'Microsoft:Hyper-V:System:Realized' } |
        Select-Object -First 1
    $result = Invoke-CimMethod -InputObject $service -MethodName GetVirtualSystemThumbnailImage -Arguments @{
        TargetSystem = [ciminstance]$settings
        WidthPixels  = [uint16]160
        HeightPixels = [uint16]120
    }
    if ($result.ReturnValue -ne 0 -or $null -eq $result.ImageData -or $result.ImageData.Length -eq 0) {
        return 0
    }

    $total = 0
    foreach ($byte in $result.ImageData) {
        $total += $byte
    }
    return [math]::Round($total / $result.ImageData.Length, 2)
}

function Send-GuestKey {
    <#
    .SYNOPSIS
    Presses a key on the guest's virtual keyboard.

    .DESCRIPTION
    Windows installation media prompts "Press any key to boot from CD or DVD". Nothing types that key
    in an automated virtual machine, so the firmware falls through to an empty disk and the install
    never starts.
    #>
    param(
        [Parameter(Mandatory)][string]$VmName,
        [uint32]$KeyCode = 0x0D,
        [int]$Count = 1
    )

    $system = Get-CimInstance -Namespace root\virtualization\v2 -ClassName Msvm_ComputerSystem `
        -Filter "ElementName='$VmName'"
    $keyboard = Get-CimAssociatedInstance -InputObject $system -ResultClassName Msvm_Keyboard
    for ($index = 0; $index -lt $Count; $index++) {
        Invoke-CimMethod -InputObject $keyboard -MethodName TypeKey -Arguments @{ keyCode = $KeyCode } | Out-Null
        Start-Sleep -Milliseconds 400
    }
}

if (-not (Test-Path $ConfigPath -PathType Leaf)) {
    throw "Configuration was not found: $ConfigPath. Copy vm.config.example.psd1 to vm.config.psd1 first."
}
$configuration = Import-PowerShellDataFile $ConfigPath
foreach ($key in 'VmName', 'ComputerName', 'VmPath', 'VhdPath', 'DiskSizeGB', 'MemoryStartupGB',
    'ProcessorCount', 'AdminUserName', 'StandardUser', 'ProcessorArchitecture', 'Locale', 'TimeZone',
    'BaselineCheckpointName') {
    if (-not $configuration.ContainsKey($key) -or [string]::IsNullOrWhiteSpace([string]$configuration[$key])) {
        throw "Configuration value '$key' is missing from $ConfigPath."
    }
}
if ($configuration.ProcessorArchitecture -notin @('amd64', 'arm64')) {
    throw "ProcessorArchitecture must be amd64 or arm64, not '$($configuration.ProcessorArchitecture)'."
}
$hostArchitecture = switch ($env:PROCESSOR_ARCHITECTURE) {
    'AMD64' { 'amd64' }
    'ARM64' { 'arm64' }
    default { $env:PROCESSOR_ARCHITECTURE.ToLowerInvariant() }
}
if ($configuration.ProcessorArchitecture -ne $hostArchitecture) {
    throw "Hyper-V cannot run a $($configuration.ProcessorArchitecture) guest on a $hostArchitecture host."
}

$answerIsoPath = Join-Path $configuration.VmPath 'answer-file.iso'
$answerTemplatePath = Join-Path $PSScriptRoot 'unattend\autounattend.xml.template'
Assert-SupportedVmVolume -Path $configuration.VhdPath -Allow:$AllowReFsVolume
Assert-SupportedVmVolume -Path $configuration.VmPath -Allow:$AllowReFsVolume
$plan = [ordered]@{
    VmName = $configuration.VmName
    ComputerName = $configuration.ComputerName
    VmPath = $configuration.VmPath
    VhdPath = $configuration.VhdPath
    DiskSizeGB = $configuration.DiskSizeGB
    MemoryStartupGB = $configuration.MemoryStartupGB
    ProcessorCount = $configuration.ProcessorCount
    SwitchName = [string]$configuration.SwitchName
    ProcessorArchitecture = $configuration.ProcessorArchitecture
    InstallMedia = $InstallMedia
    ImageName = $ImageName
    AnswerIso = $answerIsoPath
    OemPath = $OemPath
    CredentialPath = $CredentialPath
    BaselineCheckpointName = $configuration.BaselineCheckpointName
}

if ($PlanOnly) {
    $preview = New-UnattendContent -Configuration $configuration `
        -ObfuscatedPassword (ConvertTo-UnattendPassword -Password 'preview' -Element 'Password') `
        -SelectedImageName $ImageName `
        -ProvisionArguments "-StandardUser $($configuration.StandardUser)" `
        -TemplatePath $answerTemplatePath
    $plan.AnswerFileBytes = $preview.Length
    $plan.AnswerFileIsWellFormed = $true
    [pscustomobject]$plan | ConvertTo-Json -Depth 4
    return
}

if (-not (Test-Elevation)) {
    throw 'BLOCKED: creating a Hyper-V guest requires an elevated host shell.'
}
Import-Module Hyper-V -ErrorAction Stop

if ([string]::IsNullOrWhiteSpace($InstallMedia) -or -not (Test-Path $InstallMedia -PathType Leaf)) {
    throw "Installation media was not found: $InstallMedia"
}

if ($ListImages) {
    Mount-DiskImage -ImagePath $InstallMedia -Access ReadOnly -StorageType ISO | Out-Null
    try {
        Get-WindowsImageList -MediaRoot (Get-MountedImageRoot -ImagePath $InstallMedia) |
            Select-Object ImageIndex, ImageName, ImageDescription
    }
    finally {
        Dismount-DiskImage -ImagePath $InstallMedia | Out-Null
    }
    return
}

if (-not (Test-Path $OemPath -PathType Container)) {
    throw "OEM payload folder was not found: $OemPath"
}
if (-not (Test-Path (Join-Path $OemPath 'Provision-UiTestVm.ps1') -PathType Leaf)) {
    throw "Provision-UiTestVm.ps1 was not found under $OemPath."
}
if (-not (Test-Path $CredentialPath -PathType Leaf)) {
    throw "DPAPI credential file was not found: $CredentialPath. Create it with Get-Credential | Export-Clixml."
}
$credential = Import-Clixml $CredentialPath
if ($credential -isnot [pscredential]) {
    throw "Credential file does not contain a PSCredential: $CredentialPath"
}
$credentialUser = $credential.UserName -replace '^.*\\', ''
if ($credentialUser -ne $configuration.AdminUserName) {
    throw "The credential file is for '$credentialUser', but the configuration expects '$($configuration.AdminUserName)'."
}

$existingVm = Get-VM -Name $configuration.VmName -ErrorAction SilentlyContinue
if (($null -ne $existingVm -or (Test-Path $configuration.VhdPath -PathType Leaf)) -and -not $Force) {
    throw "Virtual machine '$($configuration.VmName)' or its disk already exists. Pass -Force to replace it."
}
if (-not $PSCmdlet.ShouldProcess($configuration.VmName, 'Create the local UI-test Hyper-V guest')) {
    return
}

if ($null -ne $existingVm) {
    if ($existingVm.State -ne 'Off') {
        Stop-VM -Name $configuration.VmName -TurnOff -Force
    }
    Get-VMSnapshot -VMName $configuration.VmName -ErrorAction SilentlyContinue | Remove-VMSnapshot -Confirm:$false
    Remove-VM -Name $configuration.VmName -Force
}
Remove-Item $configuration.VhdPath -Force -ErrorAction SilentlyContinue
New-Item $configuration.VmPath -ItemType Directory -Force | Out-Null
New-Item (Split-Path $configuration.VhdPath -Parent) -ItemType Directory -Force | Out-Null

Write-Host 'Validating the requested edition on the installation media...'
Mount-DiskImage -ImagePath $InstallMedia -Access ReadOnly -StorageType ISO | Out-Null
try {
    $images = Get-WindowsImageList -MediaRoot (Get-MountedImageRoot -ImagePath $InstallMedia)
    $selected = $images | Where-Object { $_.ImageName -eq $ImageName } | Select-Object -First 1
    if ($null -eq $selected) {
        throw "Edition '$ImageName' is not on this media. Available: $(($images | ForEach-Object { $_.ImageName }) -join ' | ')"
    }
    Write-Host "Selected image $($selected.ImageIndex): $($selected.ImageName)"
}
finally {
    Dismount-DiskImage -ImagePath $InstallMedia | Out-Null
}

Write-Host 'Building the answer-file ISO...'
$stagingRoot = Join-Path ([IO.Path]::GetTempPath()) ("ptvm-answer-" + [guid]::NewGuid().ToString('N'))
try {
    New-Item $stagingRoot -ItemType Directory -Force | Out-Null
    $unattend = New-UnattendContent -Configuration $configuration `
        -ObfuscatedPassword (ConvertTo-UnattendPassword -Password $credential.GetNetworkCredential().Password -Element 'Password') `
        -SelectedImageName $selected.ImageName `
        -ProvisionArguments "-StandardUser $($configuration.StandardUser)" `
        -TemplatePath $answerTemplatePath
    Set-Content (Join-Path $stagingRoot 'autounattend.xml') -Value $unattend -Encoding utf8
    $unattend = $null
    Copy-Item $OemPath (Join-Path $stagingRoot 'OEM') -Recurse -Force
    New-DataIso -SourceFolder $stagingRoot -Destination $answerIsoPath
}
finally {
    Remove-Item $stagingRoot -Recurse -Force -ErrorAction SilentlyContinue
}

Write-Host "Creating virtual machine '$($configuration.VmName)'..."
New-VHD -Path $configuration.VhdPath -SizeBytes ($configuration.DiskSizeGB * 1GB) -Dynamic | Out-Null
$newVmParameters = @{
    Name = $configuration.VmName
    Generation = 2
    MemoryStartupBytes = ($configuration.MemoryStartupGB * 1GB)
    VHDPath = $configuration.VhdPath
    Path = $configuration.VmPath
}
if (-not [string]::IsNullOrWhiteSpace([string]$configuration.SwitchName)) {
    $newVmParameters.SwitchName = $configuration.SwitchName
}
New-VM @newVmParameters | Out-Null

Set-VMProcessor -VMName $configuration.VmName -Count $configuration.ProcessorCount
Set-VMMemory -VMName $configuration.VmName -DynamicMemoryEnabled $false
Set-VM -Name $configuration.VmName `
    -AutomaticCheckpointsEnabled $false -CheckpointType Standard `
    -AutomaticStartAction Nothing -AutomaticStopAction ShutDown
Set-VMFirmware -VMName $configuration.VmName -EnableSecureBoot On -SecureBootTemplate MicrosoftWindows
try {
    Set-VMKeyProtector -VMName $configuration.VmName -NewLocalKeyProtector -ErrorAction Stop
    Enable-VMTPM -VMName $configuration.VmName -ErrorAction Stop
}
catch {
    Write-Warning "The virtual TPM could not be enabled: $($_.Exception.Message)"
}
Enable-VMIntegrationService -VMName $configuration.VmName -Name 'Guest Service Interface'

Add-VMDvdDrive -VMName $configuration.VmName -Path $InstallMedia
Add-VMDvdDrive -VMName $configuration.VmName -Path $answerIsoPath
$installDvd = Get-VMDvdDrive -VMName $configuration.VmName | Where-Object { $_.Path -eq $InstallMedia }
Set-VMFirmware -VMName $configuration.VmName -FirstBootDevice $installDvd

Write-Host 'Starting Windows Setup in the guest...'
Start-VM -Name $configuration.VmName
# The media waits for "Press any key to boot from CD or DVD" and gives up without it. Stop as soon as
# Setup's own UI is up: Enter there activates Cancel and aborts the installation.
for ($attempt = 0; $attempt -lt 10; $attempt++) {
    Start-Sleep -Seconds 2
    $screenLevel = try { Get-GuestScreenLevel -VmName $configuration.VmName } catch { 0 }
    if ($screenLevel -gt 12) {
        Write-Host "  Setup is on screen after $($attempt * 2)s; no further key presses."
        break
    }
    try {
        Send-GuestKey -VmName $configuration.VmName -Count 1
    }
    catch {
        Write-Verbose "Key press failed: $($_.Exception.Message)"
    }
}

Write-Host 'Waiting for Setup and provisioning to finish...'
$deadline = [DateTime]::UtcNow.AddMinutes($TimeoutMinutes)
$provisioning = $null
$desktopUser = $null
$lastConnectionError = $null
$lastFailureStage = 'connect'
$attempt = 0
do {
    Start-Sleep -Seconds 20
    $attempt++
    try {
        $lastFailureStage = 'connect'
        $session = New-PSSession -VMName $configuration.VmName -Credential $credential -ErrorAction Stop
        $lastFailureStage = 'query'
        try {
            $guestState = Invoke-Command -Session $session -ScriptBlock {
                param($InteractiveUser)
                # PowerShell Direct lands on the guest's in-box PowerShell 5.1, whose parser rejects a
                # multi-line statement as a hashtable value ("The hash literal was incomplete"). Build
                # the values first so this scriptblock parses on 5.1 as well as 7.
                $provisioningJson = $null
                if (Test-Path C:\OEM\ProvisioningReady.json -PathType Leaf) {
                    $provisioningJson = Get-Content C:\OEM\ProvisioningReady.json -Raw
                }
                $interactiveExplorer = @(Get-Process explorer -IncludeUserName -ErrorAction SilentlyContinue |
                    Where-Object { $_.UserName -like "*\$InteractiveUser" } |
                    Select-Object -First 1 -ExpandProperty UserName)
                [pscustomobject]@{
                    Provisioning = $provisioningJson
                    DesktopUser = $interactiveExplorer
                }
            } -ArgumentList $configuration.StandardUser
            $provisioning = $guestState.Provisioning
            $desktopUser = @($guestState.DesktopUser) | Select-Object -First 1
            $lastConnectionError = $null
        }
        finally {
            Remove-PSSession $session -ErrorAction SilentlyContinue
        }
    }
    catch {
        $lastConnectionError = $_.Exception.Message
    }
    # Surface why the guest is still not answering; silence here hides real failures for the whole timeout.
    if (($attempt % 6) -eq 0) {
        if ($null -ne $lastConnectionError) {
            # A guest-side failure looks nothing like an unreachable guest: reporting both as
            # "not reachable" hid a scriptblock parse error until the timeout expired.
            $stagePrefix = if ($lastFailureStage -eq 'connect') { 'not reachable' } else { 'reachable, but the guest query failed' }
            $state = "${stagePrefix}: $lastConnectionError"
        }
        else {
            $state = "reachable; provisioned=$(-not [string]::IsNullOrWhiteSpace($provisioning)) desktopUser='$desktopUser'"
        }
        Write-Host "  still waiting after $([int]($attempt * 20 / 60)) minute(s) - $state"
    }
    # Provisioning ends with a reboot into the standard-user desktop, so both signals are required.
    if (-not [string]::IsNullOrWhiteSpace($provisioning) -and -not [string]::IsNullOrWhiteSpace($desktopUser)) {
        break
    }
    if ([DateTime]::UtcNow -ge $deadline) {
        $reason = if ($null -ne $lastConnectionError) {
            if ($lastFailureStage -eq 'connect') {
                "the guest never answered PowerShell Direct: $lastConnectionError"
            }
            else {
                "PowerShell Direct connected but the readiness query failed: $lastConnectionError"
            }
        }
        elseif ([string]::IsNullOrWhiteSpace($provisioning)) {
            'C:\OEM\ProvisioningReady.json was never written'
        }
        else {
            "no interactive Explorer session for $($configuration.StandardUser) appeared"
        }
        throw "The guest did not finish provisioning within $TimeoutMinutes minute(s): $reason. The virtual machine and its disk are preserved; inspect the console with Get-VmConsoleImage.ps1 or vmconnect.exe."
    }
} while ($true)

Write-Host "Interactive desktop user: $desktopUser"
Write-Host 'Detaching installation media...'
Get-VMDvdDrive -VMName $configuration.VmName | Remove-VMDvdDrive
Remove-Item $answerIsoPath -Force -ErrorAction SilentlyContinue

Write-Host "Creating baseline checkpoint '$($configuration.BaselineCheckpointName)'..."
Checkpoint-VM -Name $configuration.VmName -SnapshotName $configuration.BaselineCheckpointName

$plan.Provisioning = $provisioning | ConvertFrom-Json
$plan.DesktopUser = $desktopUser
$plan.Checkpoint = $configuration.BaselineCheckpointName
[pscustomobject]$plan | ConvertTo-Json -Depth 5
