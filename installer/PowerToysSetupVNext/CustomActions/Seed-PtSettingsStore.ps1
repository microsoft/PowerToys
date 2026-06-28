<#
.SYNOPSIS
  PTSettingsSvc - install-time per-machine seeding (Design-v6-Final.md section 11 MIGRATION).

  Runs as SYSTEM from the per-machine MSI (deferred CustomAction).  Seeds every
  existing user's protected blob from their legacy %LocalAppData% Workspaces file:

     %LocalAppData%\Microsoft\PowerToys\Workspaces\workspaces.json   (user U)
        ->  %ProgramData%\Microsoft\PowerToys\SettingsSvc\<ns>\<SID(U)>\blob.bin

  Direct SYSTEM file write - no service round-trip, no migration opcode.  The blob
  is created with owner=SYSTEM and a PROTECTED DACL (svc:F, admin:F, system:F,
  <user>:RX) so the user can read but never tamper.  Idempotent (skips a SID that
  already has a blob).

.NOTES
  Standalone (no modules); safe to invoke via `powershell -ExecutionPolicy Bypass -File`.
#>
[CmdletBinding()]
param(
    [string]$NamespaceId    = 'Workspaces',
    [string]$FileName       = 'workspaces.json',
    [string]$LegacyRelative = 'AppData\Local\Microsoft\PowerToys\Workspaces\workspaces.json',
    [string]$ServiceAccount = 'NT SERVICE\PTSettingsSvc'
)

$ErrorActionPreference = 'Stop'

$programData = [Environment]::GetFolderPath('CommonApplicationData')
# SID-first layout: <storeRoot>\<sid>\<namespace>\<file>
$storeRoot   = Join-Path $programData 'Microsoft\PowerToys\Settings'

# Store root: SYSTEM/Admins/service Full, Authenticated Users RX (so each user
# can traverse to their own <sid> node), owner SYSTEM, PROTECTED.
function New-RootDir([string]$path)
{
    if (-not (Test-Path $path)) { New-Item -ItemType Directory -Force $path | Out-Null }
    $acl = New-Object System.Security.AccessControl.DirectorySecurity
    $acl.SetAccessRuleProtection($true, $false)
    $acl.SetOwner([System.Security.Principal.SecurityIdentifier]'S-1-5-18')   # SYSTEM
    $inherit = 'ContainerInherit,ObjectInherit'
    foreach ($p in @('NT AUTHORITY\SYSTEM','BUILTIN\Administrators',$ServiceAccount))
    {
        $acl.AddAccessRule((New-Object Security.AccessControl.FileSystemAccessRule($p,'FullControl',$inherit,'None','Allow')))
    }
    $acl.AddAccessRule((New-Object Security.AccessControl.FileSystemAccessRule(
        'NT AUTHORITY\Authenticated Users','ReadAndExecute',$inherit,'None','Allow')))
    Set-Acl -Path $path -AclObject $acl
}

function New-ProtectedDir([string]$path, [string]$userSid)
{
    if (-not (Test-Path $path)) { New-Item -ItemType Directory -Force $path | Out-Null }

    # Build a PROTECTED DACL: SYSTEM:F, Administrators:F, service:F, <user>:RX.
    $acl = New-Object System.Security.AccessControl.DirectorySecurity
    $acl.SetAccessRuleProtection($true, $false)   # protected, drop inheritance
    $acl.SetOwner([System.Security.Principal.SecurityIdentifier]'S-1-5-18')   # SYSTEM
    $inherit = 'ContainerInherit,ObjectInherit'

    $rules = @(
        (New-Object Security.AccessControl.FileSystemAccessRule('NT AUTHORITY\SYSTEM','FullControl',$inherit,'None','Allow')),
        (New-Object Security.AccessControl.FileSystemAccessRule('BUILTIN\Administrators','FullControl',$inherit,'None','Allow')),
        (New-Object Security.AccessControl.FileSystemAccessRule($ServiceAccount,'FullControl',$inherit,'None','Allow')),
        (New-Object Security.AccessControl.FileSystemAccessRule(
            (New-Object Security.Principal.SecurityIdentifier($userSid)),'ReadAndExecute,Synchronize',$inherit,'None','Allow'))
    )
    foreach ($r in $rules) { $acl.AddAccessRule($r) }
    Set-Acl -Path $path -AclObject $acl
}

# Enumerate real user profiles from ProfileList (SID -> profile path).
$profileListKey = 'HKLM:\SOFTWARE\Microsoft\Windows NT\CurrentVersion\ProfileList'
$seeded = 0; $skipped = 0

# Ensure the store root exists with the traversable root DACL (once).
New-RootDir -path $storeRoot

Get-ChildItem $profileListKey -ErrorAction SilentlyContinue | ForEach-Object {
    $sid = $_.PSChildName

    # Only real interactive users: local (S-1-5-21-*) or AAD/MSA (S-1-12-1-*).
    if ($sid -notmatch '^(S-1-5-21-|S-1-12-1-)') { return }

    $profilePath = (Get-ItemProperty $_.PSPath -Name ProfileImagePath -ErrorAction SilentlyContinue).ProfileImagePath
    if ([string]::IsNullOrEmpty($profilePath)) { return }

    $legacy = Join-Path $profilePath $LegacyRelative
    if (-not (Test-Path $legacy)) { return }

    $userRoot = Join-Path $storeRoot $sid          # per-user node (protected, inherits down)
    $nsFolder = Join-Path $userRoot $NamespaceId
    $file     = Join-Path $nsFolder $FileName
    if (Test-Path $file) { $skipped++; return }     # idempotent

    try
    {
        # Protect the <sid> node once; the namespace folder + file inherit it.
        New-ProtectedDir -path $userRoot -userSid $sid
        if (-not (Test-Path $nsFolder)) { New-Item -ItemType Directory -Force $nsFolder | Out-Null }
        [System.IO.File]::WriteAllBytes($file, [System.IO.File]::ReadAllBytes($legacy))

        $bytes = ([System.IO.FileInfo]::new($file)).Length
        Write-Output "seeded: $sid  ($bytes bytes)"
        $seeded++
    }
    catch
    {
        Write-Output "FAILED for $sid : $($_.Exception.Message)"
    }
}

Write-Output "PTSettingsSvc seeding done: $seeded seeded, $skipped already present."
exit 0
