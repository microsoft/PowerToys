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
    [string]$LegacyRelative = 'AppData\Local\Microsoft\PowerToys\Workspaces\workspaces.json',
    [string]$ServiceAccount = 'NT SERVICE\PTSettingsSvc'
)

$ErrorActionPreference = 'Stop'

$programData = [Environment]::GetFolderPath('CommonApplicationData')
$nsRoot      = Join-Path $programData "Microsoft\PowerToys\SettingsSvc\$NamespaceId"

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

Get-ChildItem $profileListKey -ErrorAction SilentlyContinue | ForEach-Object {
    $sid = $_.PSChildName

    # Only real interactive users: local (S-1-5-21-*) or AAD/MSA (S-1-12-1-*).
    if ($sid -notmatch '^(S-1-5-21-|S-1-12-1-)') { return }

    $profilePath = (Get-ItemProperty $_.PSPath -Name ProfileImagePath -ErrorAction SilentlyContinue).ProfileImagePath
    if ([string]::IsNullOrEmpty($profilePath)) { return }

    $legacy = Join-Path $profilePath $LegacyRelative
    if (-not (Test-Path $legacy)) { return }

    $userDir = Join-Path $nsRoot $sid
    $blob    = Join-Path $userDir 'blob.bin'
    if (Test-Path $blob) { $skipped++; return }   # idempotent

    try
    {
        New-ProtectedDir -path $nsRoot  -userSid $sid    # namespace root (re-asserts each time; cheap)
        New-ProtectedDir -path $userDir -userSid $sid
        [System.IO.File]::WriteAllBytes($blob, [System.IO.File]::ReadAllBytes($legacy))

        # Lock the blob itself: owner SYSTEM, PROTECTED, SYSTEM/admin/svc:F, user:RX.
        $facl = New-Object System.Security.AccessControl.FileSecurity
        $facl.SetAccessRuleProtection($true, $false)
        $facl.SetOwner([System.Security.Principal.SecurityIdentifier]'S-1-5-18')
        $facl.AddAccessRule((New-Object Security.AccessControl.FileSystemAccessRule('NT AUTHORITY\SYSTEM','FullControl','Allow')))
        $facl.AddAccessRule((New-Object Security.AccessControl.FileSystemAccessRule('BUILTIN\Administrators','FullControl','Allow')))
        $facl.AddAccessRule((New-Object Security.AccessControl.FileSystemAccessRule($ServiceAccount,'FullControl','Allow')))
        $facl.AddAccessRule((New-Object Security.AccessControl.FileSystemAccessRule(
            (New-Object Security.Principal.SecurityIdentifier($sid)),'ReadAndExecute,Synchronize','Allow')))
        Set-Acl -Path $blob -AclObject $facl

        Write-Output "seeded: $sid  ($([System.IO.FileInfo]::new($blob).Length) bytes)"
        $seeded++
    }
    catch
    {
        Write-Output "FAILED for $sid : $($_.Exception.Message)"
    }
}

Write-Output "PTSettingsSvc seeding done: $seeded seeded, $skipped already present."
exit 0
