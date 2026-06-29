<#
.SYNOPSIS
  PTSettingsSvc - per-user lazy hardening (Design-v6-Final.md section 11 "Lazy per-user install").

  Run ELEVATED (the one-time UAC) the first time a per-user install needs protection
  (first save/launch of a workspace with a launch-as-admin entry).  It:
    1. registers the machine-wide PTSettingsSvc service if it isn't already present
       (another user on the box may have installed it earlier),
    2. ensures the protected %ProgramData% data root, and
    3. migrates THIS user's legacy file into a protected, owner=SYSTEM blob.

  Unlike the per-machine installer (which seeds every profile as SYSTEM), this runs
  in one user's elevation and seeds only the invoking user.

.PARAMETER UserSid        SID of the user being hardened (defaults to the caller).
.PARAMETER ServiceBinary  Full path to PowerToys.PTSettingsSvc.exe (required if the
                          service is not yet registered).
#>
[CmdletBinding()]
param(
    [string]$UserSid       = ([Security.Principal.WindowsIdentity]::GetCurrent().User.Value),
    [string]$ServiceName   = 'PTSettingsSvc',
    [string]$ServiceBinary = '',
    [string]$NamespaceId   = 'Workspaces',
    [string]$FileName      = 'workspaces.json',
    [string]$LegacyRelative= 'AppData\Local\Microsoft\PowerToys\Workspaces\workspaces.json',
    [string]$ServiceAccount= 'NT SERVICE\PTSettingsSvc'
)

$ErrorActionPreference = 'Stop'
$programData = [Environment]::GetFolderPath('CommonApplicationData')
# SID-first layout: <storeRoot>\<sid>\<namespace>\<file>
$storeRoot = Join-Path $programData 'Microsoft\PowerToys\Settings'
$userRoot  = Join-Path $storeRoot $UserSid
$nsFolder  = Join-Path $userRoot $NamespaceId
$file      = Join-Path $nsFolder $FileName

# Store root: SYSTEM/Admins/service Full, Authenticated Users RX (so each user
# can traverse to their own <sid> node), owner SYSTEM, PROTECTED.
function Set-RootAcl([string]$path)
{
    $acl = New-Object System.Security.AccessControl.DirectorySecurity
    $acl.SetAccessRuleProtection($true, $false)
    $acl.SetOwner([System.Security.Principal.SecurityIdentifier]'S-1-5-18')   # SYSTEM
    foreach ($p in @('NT AUTHORITY\SYSTEM','BUILTIN\Administrators',$ServiceAccount))
    {
        $acl.AddAccessRule((New-Object Security.AccessControl.FileSystemAccessRule($p,'FullControl','ContainerInherit,ObjectInherit','None','Allow')))
    }
    $acl.AddAccessRule((New-Object Security.AccessControl.FileSystemAccessRule(
        'NT AUTHORITY\Authenticated Users','ReadAndExecute','ContainerInherit,ObjectInherit','None','Allow')))
    Set-Acl -Path $path -AclObject $acl
}

# Per-user node: svc/SYSTEM/Admins Full, THIS user RX only, owner SYSTEM,
# PROTECTED (drops the root's blanket AuthUsers RX so user A can't read user B).
# Applied once at <sid>; the namespace folder and file inherit it.
function Set-ProtectedAcl([string]$path, [string]$sid)
{
    $acl = New-Object System.Security.AccessControl.DirectorySecurity
    $acl.SetAccessRuleProtection($true, $false)
    $acl.SetOwner([System.Security.Principal.SecurityIdentifier]'S-1-5-18')   # SYSTEM
    foreach ($p in @('NT AUTHORITY\SYSTEM','BUILTIN\Administrators',$ServiceAccount))
    {
        $acl.AddAccessRule((New-Object Security.AccessControl.FileSystemAccessRule($p,'FullControl','ContainerInherit,ObjectInherit','None','Allow')))
    }
    $acl.AddAccessRule((New-Object Security.AccessControl.FileSystemAccessRule(
        (New-Object Security.Principal.SecurityIdentifier($sid)),'ReadAndExecute,Synchronize','ContainerInherit,ObjectInherit','None','Allow')))
    Set-Acl -Path $path -AclObject $acl
}

# 1) Register the service if absent.
if (-not (Get-Service $ServiceName -ErrorAction SilentlyContinue))
{
    if ([string]::IsNullOrEmpty($ServiceBinary) -or -not (Test-Path $ServiceBinary))
    {
        throw "Service '$ServiceName' is not registered and -ServiceBinary was not provided/found."
    }

    # The service runs as a machine-wide virtual account (NT SERVICE\PTSettingsSvc)
    # and cannot read a binary staged under a user profile (%LocalAppData% is
    # ACL'd to that user only) - SCM start fails 0x5 ACCESS_DENIED.  Copy the
    # payload to a machine-readable location and register the service from there.
    $machineBin = Join-Path $storeRoot 'bin'
    if (-not (Test-Path $machineBin)) { New-Item -ItemType Directory -Force $machineBin | Out-Null }
    $machineExe = Join-Path $machineBin (Split-Path $ServiceBinary -Leaf)
    Copy-Item $ServiceBinary $machineExe -Force
    # Service account + Authenticated Users need RX to load/execute the binary.
    & icacls.exe $machineBin /grant "$($ServiceAccount):(OI)(CI)RX" | Out-Null
    & icacls.exe $machineBin /grant '*S-1-5-11:(OI)(CI)RX'           | Out-Null

    sc.exe create $ServiceName binPath= "`"$machineExe`"" start= auto obj= $ServiceAccount DisplayName= "PowerToys Settings Service" | Out-Null
    sc.exe start  $ServiceName | Out-Null
    Write-Output "service '$ServiceName' registered + started (binary staged to $machineExe)."
}
else { Write-Output "service '$ServiceName' already present." }

# 2) Ensure the store root + this user's protected node (namespace inherits).
if (-not (Test-Path $storeRoot)) { New-Item -ItemType Directory -Force $storeRoot | Out-Null }
Set-RootAcl $storeRoot
if (-not (Test-Path $userRoot)) { New-Item -ItemType Directory -Force $userRoot | Out-Null }
Set-ProtectedAcl -path $userRoot -sid $UserSid
if (-not (Test-Path $nsFolder)) { New-Item -ItemType Directory -Force $nsFolder | Out-Null }

# 3) Migrate this user's legacy file into the protected store (idempotent).
#    The file inherits the protected DACL from the <sid> node above.
if (Test-Path $file)
{
    Write-Output "settings file already present for $UserSid - nothing to migrate."
}
else
{
    $profilePath = (Get-ItemProperty "HKLM:\SOFTWARE\Microsoft\Windows NT\CurrentVersion\ProfileList\$UserSid" -Name ProfileImagePath -ErrorAction SilentlyContinue).ProfileImagePath
    $legacy = if ($profilePath) { Join-Path $profilePath $LegacyRelative } else { $null }
    if ($legacy -and (Test-Path $legacy))
    {
        [System.IO.File]::WriteAllBytes($file, [System.IO.File]::ReadAllBytes($legacy))
        $fileSize = ([System.IO.FileInfo]::new($file)).Length
        Write-Output "migrated legacy file for $UserSid ($fileSize bytes)."
    }
    else
    {
        [System.IO.File]::WriteAllBytes($file, [byte[]]@())   # empty protected file
        Write-Output "no legacy file; created empty protected settings file for $UserSid."
    }
}

Write-Output "per-user hardening complete for $UserSid."
exit 0
