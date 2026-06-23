# setup-ptsettingssvc.ps1
#
# Stands up PTSettingsSvc for local v6 prototype validation:
#   * Registers the service under NT SERVICE\PTSettingsSvc
#   * Creates the PROTECTED data root at %ProgramData%\Microsoft\PowerToys\SettingsSvc
#   * Creates a fake "install folder" under %TEMP%, locks its DACL to admin-only,
#     copies the smoke-test exe in renamed to an allow-listed basename
#     (PowerToys.WorkspacesEditor.exe)
#   * Sets HKLM\SOFTWARE\Classes\PowerToys\InstallFolder so the service finds
#     the fake install folder via the same code path the production MSI uses
#
# Must be run elevated.

[CmdletBinding()]
param(
    [string]$RepoRoot   = (Resolve-Path (Join-Path $PSScriptRoot '..\..\..\..\..')).Path,
    [string]$SvcName    = 'PTSettingsSvc',
    [string]$DisplayName= 'PowerToys Settings Service',
    [string]$Description= 'Provides tamper-resistant storage for PowerToys module settings. Stopping this service prevents affected modules (e.g. Workspaces) from saving configuration changes.',
    [string]$FakeInstall= (Join-Path $env:TEMP 'PTFakeInstall')
)

$ErrorActionPreference = 'Stop'

$svcExe        = Join-Path $RepoRoot 'x64\Debug\WorkspacesSettingsService\PowerToys.PTSettingsSvc.exe'
$smokeExe      = Join-Path $RepoRoot 'x64\Debug\WorkspacesSvcSmokeTest\PowerToys.PTSettingsSvcSmokeTest.exe'
$dataRoot      = 'C:\ProgramData\Microsoft\PowerToys\SettingsSvc'
$renamedCaller = Join-Path $FakeInstall 'PowerToys.WorkspacesEditor.exe'
$badCaller     = Join-Path $FakeInstall 'PowerToys.PTSettingsSvcSmokeTest.exe'

if (-not (Test-Path $svcExe))   { throw "Service exe not found: $svcExe`nBuild WorkspacesSettingsService.vcxproj first." }
if (-not (Test-Path $smokeExe)) { throw "Smoke-test exe not found: $smokeExe`nBuild WorkspacesSvcSmokeTest.vcxproj first." }

Write-Host "=== Setting up PTSettingsSvc for local validation ===" -ForegroundColor Cyan
Write-Host "Running as: $([Security.Principal.WindowsIdentity]::GetCurrent().Name)"

# -------------------------------------------------------------------
# 1) Stop & remove any prior install so we can iterate cleanly.
# -------------------------------------------------------------------
$existing = Get-Service -Name $SvcName -ErrorAction SilentlyContinue
if ($existing)
{
    Write-Host "`n[1/5] Found existing service $SvcName - removing ..."
    if ($existing.Status -ne 'Stopped')
    {
        sc.exe stop $SvcName | Out-Null
        Start-Sleep -Seconds 2
    }
    sc.exe delete $SvcName | Out-Null
    Start-Sleep -Seconds 1
}
else
{
    Write-Host "`n[1/5] No prior install of $SvcName - clean slate."
}

# Also clean any legacy PTWorkspacesSvc from earlier prototype builds.
$legacy = Get-Service -Name 'PTWorkspacesSvc' -ErrorAction SilentlyContinue
if ($legacy)
{
    Write-Host "      Removing legacy PTWorkspacesSvc from earlier prototype ..."
    if ($legacy.Status -ne 'Stopped') { sc.exe stop 'PTWorkspacesSvc' | Out-Null; Start-Sleep 2 }
    sc.exe delete 'PTWorkspacesSvc' | Out-Null
}

# -------------------------------------------------------------------
# 2) Create the service under the virtual account.
# -------------------------------------------------------------------
Write-Host "`n[2/5] Creating service $SvcName under NT SERVICE\$SvcName ..."
$out = sc.exe create $SvcName binPath= "`"$svcExe`"" start= demand `
    obj= "NT SERVICE\$SvcName" DisplayName= "$DisplayName" 2>&1
Write-Host $out
if ($LASTEXITCODE -ne 0) { throw "sc.exe create failed (exit $LASTEXITCODE)" }

sc.exe description $SvcName "$Description" | Out-Null
sc.exe failure $SvcName reset= 86400 actions= restart/60000/restart/60000/``/``/0 | Out-Null

# -------------------------------------------------------------------
# 3) Create the data root with PROTECTED admin-only DACL.
# -------------------------------------------------------------------
Write-Host "`n[3/5] Setting up data root $dataRoot ..."
if (Test-Path $dataRoot)
{
    Write-Host "      Folder exists - resetting ACL."
}
else
{
    New-Item -ItemType Directory -Force $dataRoot | Out-Null
}

$acl = New-Object System.Security.AccessControl.DirectorySecurity
$acl.SetAccessRuleProtection($true, $false)   # PROTECTED, drop inherited ACEs
$svcPrincipal = New-Object System.Security.Principal.NTAccount("NT SERVICE\$SvcName")
$acl.AddAccessRule((New-Object System.Security.AccessControl.FileSystemAccessRule(
    $svcPrincipal, 'FullControl', 'ContainerInherit,ObjectInherit', 'None', 'Allow')))
$acl.AddAccessRule((New-Object System.Security.AccessControl.FileSystemAccessRule(
    'BUILTIN\Administrators', 'FullControl', 'ContainerInherit,ObjectInherit', 'None', 'Allow')))
$acl.AddAccessRule((New-Object System.Security.AccessControl.FileSystemAccessRule(
    'NT AUTHORITY\Authenticated Users', 'ReadAndExecute', 'ContainerInherit,ObjectInherit', 'None', 'Allow')))
$acl.SetOwner((New-Object System.Security.Principal.NTAccount('BUILTIN\Administrators')))
Set-Acl -Path $dataRoot -AclObject $acl

Write-Host "      DACL:"
(Get-Acl $dataRoot).Access | ForEach-Object {
    Write-Host ("        {0,-45} {1,-20} {2}" -f $_.IdentityReference, $_.FileSystemRights, $_.AccessControlType)
}

# -------------------------------------------------------------------
# 4) Set up fake install folder so the smoke test can pass auth.
# -------------------------------------------------------------------
Write-Host "`n[4/5] Setting up fake install folder $FakeInstall ..."
if (Test-Path $FakeInstall) { Remove-Item $FakeInstall -Recurse -Force }
New-Item -ItemType Directory -Force $FakeInstall | Out-Null

# Admin-only DACL.  Without this the service's IsFolderAdminOnlyWritable
# check (see Paths.cpp) rejects the install folder and every caller fails
# AuthFailCaller, regardless of binary name.
$ial = New-Object System.Security.AccessControl.DirectorySecurity
$ial.SetAccessRuleProtection($true, $false)
$ial.AddAccessRule((New-Object System.Security.AccessControl.FileSystemAccessRule(
    'NT AUTHORITY\SYSTEM', 'FullControl', 'ContainerInherit,ObjectInherit', 'None', 'Allow')))
$ial.AddAccessRule((New-Object System.Security.AccessControl.FileSystemAccessRule(
    'BUILTIN\Administrators', 'FullControl', 'ContainerInherit,ObjectInherit', 'None', 'Allow')))
# Virtual service account needs RX so it can read the folder's own DACL
# from inside IsFolderAdminOnlyWritable.  Production WiX will grant this
# explicitly to NT SERVICE\PTSettingsSvc; for the smoke test we grant it
# to the whole NT SERVICE bucket which is equivalent for the lookup.
$ial.AddAccessRule((New-Object System.Security.AccessControl.FileSystemAccessRule(
    'NT SERVICE\ALL SERVICES', 'ReadAndExecute', 'ContainerInherit,ObjectInherit', 'None', 'Allow')))
# Non-admin user needs RX too so the smoke test exe can actually launch
# from this folder under our current login.
$ial.AddAccessRule((New-Object System.Security.AccessControl.FileSystemAccessRule(
    'BUILTIN\Users', 'ReadAndExecute', 'ContainerInherit,ObjectInherit', 'None', 'Allow')))
$ial.SetOwner((New-Object System.Security.Principal.NTAccount('BUILTIN\Administrators')))
Set-Acl -Path $FakeInstall -AclObject $ial

# Copy smoke test twice: one renamed to an allow-listed basename (positive
# case), one keeping its real name (negative case: AuthRejected).
Copy-Item $smokeExe $renamedCaller -Force
Copy-Item $smokeExe $badCaller     -Force

Write-Host "      Copied:"
Write-Host "        $renamedCaller   (allow-listed basename, should pass auth)"
Write-Host "        $badCaller   (real name, should be rejected)"

# Point the service at the fake install folder via the same HKLM key the
# production MSI writes.  Without this the service reads InstallFolder=""
# and rejects every caller.
$hklmKey = 'HKLM:\SOFTWARE\Classes\PowerToys'
if (-not (Test-Path $hklmKey)) { New-Item -Path $hklmKey -Force | Out-Null }
Set-ItemProperty -Path $hklmKey -Name 'InstallFolder' -Value $FakeInstall -Type String
Write-Host "      HKLM\SOFTWARE\Classes\PowerToys\InstallFolder = $FakeInstall"

# -------------------------------------------------------------------
# 5) Start the service.
# -------------------------------------------------------------------
Write-Host "`n[5/5] Starting service ..."
sc.exe start $SvcName | Out-Null
Start-Sleep -Seconds 2

$svc = Get-Service -Name $SvcName
Write-Host "      Status: $($svc.Status)"

if ($svc.Status -eq 'Running')
{
    $proc = Get-CimInstance Win32_Process -Filter "Name = 'PowerToys.PTSettingsSvc.exe'" -ErrorAction SilentlyContinue
    if ($proc)
    {
        $owner = Invoke-CimMethod -InputObject $proc -MethodName GetOwner
        Write-Host "      Running as: $($owner.Domain)\$($owner.User)  (PID $($proc.ProcessId))"
    }
}
else
{
    Write-Warning "Service is not Running. sc.exe query output:"
    sc.exe query $SvcName
}

Write-Host "`n=== Setup complete ===" -ForegroundColor Green
Write-Host "Pipe:       \\.\pipe\$SvcName"
Write-Host "DataRoot:   $dataRoot"
Write-Host "InstallFld: $FakeInstall"
Write-Host ""
Write-Host "Next: run verify-prototype.ps1 (does not need elevation)."
