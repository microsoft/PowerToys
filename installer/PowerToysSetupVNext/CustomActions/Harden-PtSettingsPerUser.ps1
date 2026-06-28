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
    [string]$LegacyRelative= 'AppData\Local\Microsoft\PowerToys\Workspaces\workspaces.json',
    [string]$ServiceAccount= 'NT SERVICE\PTSettingsSvc'
)

$ErrorActionPreference = 'Stop'
$programData = [Environment]::GetFolderPath('CommonApplicationData')
$nsRoot      = Join-Path $programData "Microsoft\PowerToys\SettingsSvc\$NamespaceId"

function Set-ProtectedAcl([string]$path, [bool]$isDir, [string]$sid)
{
    $acl = if ($isDir) { New-Object System.Security.AccessControl.DirectorySecurity } `
           else        { New-Object System.Security.AccessControl.FileSecurity }
    $acl.SetAccessRuleProtection($true, $false)
    $acl.SetOwner([System.Security.Principal.SecurityIdentifier]'S-1-5-18')   # SYSTEM
    $inh = if ($isDir) { 'ContainerInherit,ObjectInherit' } else { 'None' }
    foreach ($p in @('NT AUTHORITY\SYSTEM','BUILTIN\Administrators',$ServiceAccount))
    {
        $acl.AddAccessRule((New-Object Security.AccessControl.FileSystemAccessRule($p,'FullControl',$inh,'None','Allow')))
    }
    $acl.AddAccessRule((New-Object Security.AccessControl.FileSystemAccessRule(
        (New-Object Security.Principal.SecurityIdentifier($sid)),'ReadAndExecute,Synchronize',$inh,'None','Allow')))
    Set-Acl -Path $path -AclObject $acl
}

# 1) Register the service if absent.
if (-not (Get-Service $ServiceName -ErrorAction SilentlyContinue))
{
    if ([string]::IsNullOrEmpty($ServiceBinary) -or -not (Test-Path $ServiceBinary))
    {
        throw "Service '$ServiceName' is not registered and -ServiceBinary was not provided/found."
    }
    sc.exe create $ServiceName binPath= "`"$ServiceBinary`"" start= auto obj= $ServiceAccount DisplayName= "PowerToys Settings Service" | Out-Null
    sc.exe start  $ServiceName | Out-Null
    Write-Output "service '$ServiceName' registered + started."
}
else { Write-Output "service '$ServiceName' already present." }

# 2) Ensure the protected data root + this user's folder.
foreach ($d in @($nsRoot, (Join-Path $nsRoot $UserSid)))
{
    if (-not (Test-Path $d)) { New-Item -ItemType Directory -Force $d | Out-Null }
    Set-ProtectedAcl -path $d -isDir $true -sid $UserSid
}

# 3) Migrate this user's legacy file into the protected blob (idempotent).
$blob = Join-Path (Join-Path $nsRoot $UserSid) 'blob.bin'
if (Test-Path $blob)
{
    Write-Output "blob already present for $UserSid - nothing to migrate."
}
else
{
    $profilePath = (Get-ItemProperty "HKLM:\SOFTWARE\Microsoft\Windows NT\CurrentVersion\ProfileList\$UserSid" -Name ProfileImagePath -ErrorAction SilentlyContinue).ProfileImagePath
    $legacy = if ($profilePath) { Join-Path $profilePath $LegacyRelative } else { $null }
    if ($legacy -and (Test-Path $legacy))
    {
        [System.IO.File]::WriteAllBytes($blob, [System.IO.File]::ReadAllBytes($legacy))
        $blobSize = ([System.IO.FileInfo]::new($blob)).Length
        Write-Output "migrated legacy file for $UserSid ($blobSize bytes)."
    }
    else
    {
        [System.IO.File]::WriteAllBytes($blob, [byte[]]@())   # empty protected blob
        Write-Output "no legacy file; created empty protected blob for $UserSid."
    }
    Set-ProtectedAcl -path $blob -isDir $false -sid $UserSid
}

Write-Output "per-user hardening complete for $UserSid."
exit 0
