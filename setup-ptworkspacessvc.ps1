# Sets up PTWorkspacesSvc for local v6 prototype testing.
# Must be run elevated.

$ErrorActionPreference = 'Stop'

$svcName  = 'PTWorkspacesSvc'
$svcExe   = 'D:\PowerToys-Workspaces-EoP-v6\x64\Debug\WorkspacesSettingsService\PowerToys.WorkspacesSettingsSvc.exe'
$dataRoot = 'C:\ProgramData\Microsoft\PowerToys\Workspaces'
# Public location so the non-elevated agent can read it without admin.
$logPath   = 'C:\Users\Public\ptworkspacessvc-setup.log'
$flagBegin = 'C:\Users\Public\ptworkspacessvc-setup.STARTED'
$flagEnd   = 'C:\Users\Public\ptworkspacessvc-setup.DONE'

Remove-Item -Force $flagBegin, $flagEnd -ErrorAction SilentlyContinue
"started at $(Get-Date -Format o) as $([Security.Principal.WindowsIdentity]::GetCurrent().Name)" |
    Out-File -FilePath $flagBegin -Encoding utf8 -Force

Start-Transcript -Path $logPath -Force | Out-Null

try {
    Write-Host "=== PTWorkspacesSvc setup ===" -ForegroundColor Cyan
    Write-Host "Running as: $([Security.Principal.WindowsIdentity]::GetCurrent().Name)"

    if (-not (Test-Path $svcExe)) {
        throw "Service exe not found: $svcExe"
    }

    # 1) Remove any prior install so we can iterate cleanly.
    $existing = Get-Service -Name $svcName -ErrorAction SilentlyContinue
    if ($existing) {
        Write-Host "`n[1/4] Found existing service, removing..."
        if ($existing.Status -ne 'Stopped') { sc.exe stop $svcName | Out-Null; Start-Sleep -Seconds 2 }
        sc.exe delete $svcName | Out-Null
        Start-Sleep -Seconds 1
    } else {
        Write-Host "`n[1/4] No prior service install — clean slate."
    }

    # 2) Create the service with the virtual account.
    Write-Host "`n[2/4] Creating service $svcName under NT SERVICE\$svcName ..."
    $createOut = sc.exe create $svcName binPath= "`"$svcExe`"" start= demand obj= "NT SERVICE\$svcName" DisplayName= "PowerToys Workspaces Settings Service" 2>&1
    Write-Host $createOut
    if ($LASTEXITCODE -ne 0) { throw "sc.exe create failed: $createOut" }

    sc.exe description $svcName "Protects PowerToys Workspaces configuration from tampering by mediating all writes through an authenticated local channel." | Out-Null
    sc.exe failure $svcName reset= 86400 actions= restart/60000/restart/60000/`/`/0 | Out-Null

    # 3) Create %ProgramData%\Microsoft\PowerToys\Workspaces with PROTECTED DACL.
    Write-Host "`n[3/4] Creating $dataRoot with protective DACL ..."
    if (Test-Path $dataRoot) {
        Write-Host "  Folder exists, resetting ACL."
    } else {
        New-Item -ItemType Directory -Force $dataRoot | Out-Null
    }

    $acl = New-Object System.Security.AccessControl.DirectorySecurity
    $acl.SetAccessRuleProtection($true, $false)   # PROTECTED, drop inherited ACEs
    $svcPrincipal = New-Object System.Security.Principal.NTAccount("NT SERVICE\$svcName")
    $acl.AddAccessRule((New-Object System.Security.AccessControl.FileSystemAccessRule(
        $svcPrincipal, 'FullControl', 'ContainerInherit,ObjectInherit', 'None', 'Allow')))
    $acl.AddAccessRule((New-Object System.Security.AccessControl.FileSystemAccessRule(
        'BUILTIN\Administrators', 'FullControl', 'ContainerInherit,ObjectInherit', 'None', 'Allow')))
    $acl.AddAccessRule((New-Object System.Security.AccessControl.FileSystemAccessRule(
        'NT AUTHORITY\Authenticated Users', 'ReadAndExecute', 'ContainerInherit,ObjectInherit', 'None', 'Allow')))
    $acl.SetOwner((New-Object System.Security.Principal.NTAccount('BUILTIN\Administrators')))
    Set-Acl -Path $dataRoot -AclObject $acl

    Write-Host "  Resulting ACL:"
    (Get-Acl $dataRoot).Access | ForEach-Object {
        Write-Host ("    {0,-45} {1,-15} {2}" -f $_.IdentityReference, $_.FileSystemRights, $_.AccessControlType)
    }

    # 4) Start the service and verify it is running under the virtual account.
    Write-Host "`n[4/4] Starting service ..."
    sc.exe start $svcName | Out-Null
    Start-Sleep -Seconds 2

    $svc = Get-Service -Name $svcName
    Write-Host "  Service status: $($svc.Status)"

    if ($svc.Status -eq 'Running') {
        $proc = Get-CimInstance Win32_Process -Filter "Name = 'PowerToys.WorkspacesSettingsSvc.exe'" -ErrorAction SilentlyContinue
        if ($proc) {
            $owner = Invoke-CimMethod -InputObject $proc -MethodName GetOwner
            Write-Host "  Running as: $($owner.Domain)\$($owner.User)  (PID $($proc.ProcessId))"
        }
    } else {
        $errInfo = sc.exe query $svcName
        Write-Warning "Service is not Running. Detail:`n$errInfo"
    }

    Write-Host "`n=== Setup complete ===" -ForegroundColor Green
    Write-Host "Data root: $dataRoot"
    Write-Host "Service:   $svcName"
    Write-Host "Pipe:      \\.\pipe\$svcName"
    Write-Host "Log:       $logPath"
}
catch {
    Write-Host "`n!!! Setup FAILED: $_" -ForegroundColor Red
    Write-Host $_.ScriptStackTrace
}
finally {
    Stop-Transcript | Out-Null
    "done at $(Get-Date -Format o)" | Out-File -FilePath $flagEnd -Encoding utf8 -Force
    Write-Host "`nPress Enter to close this window..."
    [void][System.Console]::ReadLine()
}
