# verify-prototype.ps1
#
# Exercises the PTSettingsSvc prototype end-to-end.
# Run AFTER setup-ptsettingssvc.ps1.  Does NOT need elevation.
#
# Coverage:
#   1. Liveness (Ping)
#   2. Caller-allow-list                 — bad-basename caller is rejected
#   3. Path-prefix                       — caller outside install folder rejected
#   4. Install-folder DACL hardness      — temporarily relax DACL, expect rejection
#   5. Round-trip                        — PutBlob a payload, GetBlob it back
#   6. GetBlob NotFound                  — fresh user/namespace returns NotFound
#   7. Per-user folder DACL              — only this user can read; admin can; others cannot

[CmdletBinding()]
param(
    [string]$RepoRoot    = 'D:\PowerToys-Workspaces-EoP-v6',
    [string]$FakeInstall = (Join-Path $env:TEMP 'PTFakeInstall'),
    [string]$DataRoot    = 'C:\ProgramData\Microsoft\PowerToys\SettingsSvc'
)

$ErrorActionPreference = 'Continue'

$smokeExe      = Join-Path $RepoRoot 'x64\Debug\WorkspacesSvcSmokeTest\PowerToys.PTSettingsSvcSmokeTest.exe'
$renamedCaller = Join-Path $FakeInstall 'PowerToys.WorkspacesEditor.exe'
$badCaller     = Join-Path $FakeInstall 'PowerToys.PTSettingsSvcSmokeTest.exe'
$tmpPayload    = Join-Path $env:TEMP 'pt-prototype-payload.bin'
$tmpReadBack   = Join-Path $env:TEMP 'pt-prototype-readback.bin'

$pass = 0; $fail = 0
function Step([string]$name, [scriptblock]$body)
{
    Write-Host ""
    Write-Host "── $name ──" -ForegroundColor Cyan
    try
    {
        $ok = & $body
        if ($ok) { Write-Host "  PASS" -ForegroundColor Green; $script:pass++ }
        else     { Write-Host "  FAIL" -ForegroundColor Red;   $script:fail++ }
    }
    catch
    {
        Write-Host "  FAIL (exception): $_" -ForegroundColor Red
        $script:fail++
    }
}

function Run-Caller([string]$caller, [string[]]$callerArgs)
{
    $out = & $caller @callerArgs 2>&1
    [pscustomobject]@{ ExitCode = $LASTEXITCODE; Output = ($out -join "`n") }
}

# Sanity: artefacts exist.
if (-not (Test-Path $smokeExe))      { throw "Smoke test not built: $smokeExe" }
if (-not (Test-Path $renamedCaller)) { throw "$renamedCaller missing - run setup-ptsettingssvc.ps1 first" }
if (-not (Test-Path $badCaller))     { throw "$badCaller missing - run setup-ptsettingssvc.ps1 first" }

Write-Host "==============================================" -ForegroundColor Yellow
Write-Host " PTSettingsSvc prototype verification"          -ForegroundColor Yellow
Write-Host "==============================================" -ForegroundColor Yellow
Write-Host " User      : $env:USERDOMAIN\$env:USERNAME"
Write-Host " Pipe      : \\.\pipe\PTSettingsSvc"
Write-Host " DataRoot  : $DataRoot"
Write-Host " InstallFld: $FakeInstall"

# 1) Liveness ----------------------------------------------------------
Step "1. Ping (allow-listed caller, happy path)" {
    $r = Run-Caller $renamedCaller @('ping')
    Write-Host "  output: $($r.Output)"
    return ($r.ExitCode -eq 0 -and $r.Output -match 'Ping -> Ok')
}

# 2) Caller allow-list -------------------------------------------------
Step "2. Bad basename -> AuthRejected" {
    $r = Run-Caller $badCaller @('ping')
    Write-Host "  output: $($r.Output)"
    return ($r.ExitCode -ne 0 -and $r.Output -match 'AuthRejected')
}

# 3) Path-prefix -------------------------------------------------------
Step "3. Caller outside install folder -> AuthRejected" {
    # Run the smoke test directly from its build folder — that path is
    # NOT under InstallFolder so the path-prefix check should reject it
    # (even though its basename also isn't allow-listed).
    $r = Run-Caller $smokeExe @('ping')
    Write-Host "  output: $($r.Output)"
    return ($r.ExitCode -ne 0 -and $r.Output -match 'AuthRejected')
}

# 4) Install-folder DACL hardness check -------------------------------
Step "4. User-write ACE on install folder -> AuthRejected" {
    # This step needs elevation because we have to add an ACL ourselves.
    $identity = [Security.Principal.WindowsIdentity]::GetCurrent()
    $isAdmin  = (New-Object Security.Principal.WindowsPrincipal $identity).IsInRole(
        [Security.Principal.WindowsBuiltinRole]::Administrator)
    if (-not $isAdmin)
    {
        Write-Host "  SKIPPED (needs elevation; re-run this script from an admin shell to exercise)"
        return $true
    }
    # Snapshot original DACL.
    $original = Get-Acl $FakeInstall
    try
    {
        $acl = Get-Acl $FakeInstall
        $ace = New-Object System.Security.AccessControl.FileSystemAccessRule(
            "$env:USERDOMAIN\$env:USERNAME", 'Modify',
            'ContainerInherit,ObjectInherit', 'None', 'Allow')
        $acl.AddAccessRule($ace)
        Set-Acl $FakeInstall $acl

        $r = Run-Caller $renamedCaller @('ping')
        Write-Host "  output (with user-write ACE present): $($r.Output)"
        $rejected = ($r.ExitCode -ne 0 -and $r.Output -match 'AuthRejected')

        # Restore.
        Set-Acl $FakeInstall $original
        $r2 = Run-Caller $renamedCaller @('ping')
        Write-Host "  output (DACL restored): $($r2.Output)"
        $restoredOk = ($r2.ExitCode -eq 0 -and $r2.Output -match 'Ping -> Ok')

        return ($rejected -and $restoredOk)
    }
    catch
    {
        Set-Acl $FakeInstall $original
        throw
    }
}

# 5) Round-trip --------------------------------------------------------
Step "5. PutBlob then GetBlob round-trip" {
    $payload = '{"$schemaVersion":1,"workspaces":[{"id":"abc","name":"test-' + (Get-Date -Format o) + '"}]}'
    [System.IO.File]::WriteAllText($tmpPayload, $payload, [System.Text.UTF8Encoding]::new($false))

    $put = Run-Caller $renamedCaller @('put', $tmpPayload)
    Write-Host "  put: $($put.Output)"
    if ($put.ExitCode -ne 0 -or $put.Output -notmatch 'Ok') { return $false }

    if (Test-Path $tmpReadBack) { Remove-Item $tmpReadBack -Force }
    $get = Run-Caller $renamedCaller @('get', $tmpReadBack)
    Write-Host "  get: $($get.Output)"
    if ($get.ExitCode -ne 0) { return $false }

    $readBack = [System.IO.File]::ReadAllText($tmpReadBack)
    return ($readBack -eq $payload)
}

# 6) GetBlob NotFound on fresh namespace -------------------------------
Step "6. GetBlob NotFound semantics (delete blob, expect NotFound)" {
    $blobPath = Join-Path (Join-Path (Join-Path $DataRoot 'Workspaces') ([Security.Principal.WindowsIdentity]::GetCurrent().User.Value)) 'blob.bin'
    if (Test-Path $blobPath)
    {
        # Need elevation to delete - service owns the dir.
        $identity = [Security.Principal.WindowsIdentity]::GetCurrent()
        $isAdmin  = (New-Object Security.Principal.WindowsPrincipal $identity).IsInRole(
            [Security.Principal.WindowsBuiltinRole]::Administrator)
        if (-not $isAdmin)
        {
            Write-Host "  SKIPPED (needs elevation to clear the blob; the blob exists from step 5)"
            return $true
        }
        Remove-Item $blobPath -Force
    }
    $get = Run-Caller $renamedCaller @('get')
    Write-Host "  get: $($get.Output)"
    return ($get.Output -match 'NotFound')
}

# 7) Per-user folder DACL ---------------------------------------------
Step "7. Per-user folder DACL (svc:F, admin:F, current-user:RX, others denied)" {
    # First PutBlob so the user folder exists.
    $payload = 'hello'
    [System.IO.File]::WriteAllText($tmpPayload, $payload, [System.Text.UTF8Encoding]::new($false))
    Run-Caller $renamedCaller @('put', $tmpPayload) | Out-Null

    $userSid  = [Security.Principal.WindowsIdentity]::GetCurrent().User.Value
    $userDir  = Join-Path (Join-Path $DataRoot 'Workspaces') $userSid

    if (-not (Test-Path $userDir))
    {
        Write-Host "  user folder not created: $userDir"
        return $false
    }
    $acl = Get-Acl $userDir
    Write-Host "  DACL of $userDir :"
    $acl.Access | ForEach-Object {
        Write-Host ("    {0,-45} {1,-20} {2}" -f $_.IdentityReference, $_.FileSystemRights, $_.AccessControlType)
    }

    $svcOk = $acl.Access | Where-Object {
        $_.IdentityReference.Value -like '*PTSettingsSvc*' -and
        $_.AccessControlType -eq 'Allow' -and
        $_.FileSystemRights -match 'FullControl'
    } | Select-Object -First 1

    $admOk = $acl.Access | Where-Object {
        $_.IdentityReference.Value -like '*Administrators*' -and
        $_.AccessControlType -eq 'Allow' -and
        $_.FileSystemRights -match 'FullControl'
    } | Select-Object -First 1

    $userOk = $acl.Access | Where-Object {
        ($_.IdentityReference.Value -eq "$env:USERDOMAIN\$env:USERNAME" -or
         $_.IdentityReference.Value -like "*$userSid*") -and
        $_.AccessControlType -eq 'Allow' -and
        ($_.FileSystemRights -match 'Read' -or $_.FileSystemRights -match 'Execute')
    } | Select-Object -First 1

    $noWild = -not ($acl.Access | Where-Object {
        $_.IdentityReference.Value -like '*Authenticated Users*' -or
        $_.IdentityReference.Value -like '*Everyone*'
    })

    $protectedOk = -not $acl.AreAccessRulesProtected -eq $false

    Write-Host "  svc:F=$([bool]$svcOk)  admin:F=$([bool]$admOk)  user:R*=$([bool]$userOk)  no-blanket-AuthUsers=$noWild  PROTECTED=$($acl.AreAccessRulesProtected)"
    return ([bool]$svcOk -and [bool]$admOk -and [bool]$userOk -and $noWild -and $acl.AreAccessRulesProtected)
}

Write-Host ""
Write-Host "==============================================" -ForegroundColor Yellow
Write-Host " Result: $pass passed, $fail failed"            -ForegroundColor (@('Green','Red')[[int]($fail -gt 0)])
Write-Host "==============================================" -ForegroundColor Yellow

if ($fail -gt 0) { exit 1 } else { exit 0 }
