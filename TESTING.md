# v6 prototype — one-click test setup

The CLI agent that wrote the prototype runs as a non-admin user, so it cannot
install the Windows service or apply the ACL on `%ProgramData%` itself.
Everything that needs admin has been bundled into one script.

## Step 1 — run the elevated setup (one time)

Open **PowerShell as Administrator** and run:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File D:\PowerToys-Workspaces-EoP-v6\setup-ptworkspacessvc.ps1
```

It will:

1. Remove any prior `PTWorkspacesSvc` install (idempotent).
2. Register `PTWorkspacesSvc` against `NT SERVICE\PTWorkspacesSvc`
   (virtual account, demand start, restart-on-failure).
3. Create `C:\ProgramData\Microsoft\PowerToys\Workspaces` with a PROTECTED DACL:
   - `NT SERVICE\PTWorkspacesSvc` → FullControl
   - `BUILTIN\Administrators`     → FullControl
   - `Authenticated Users`        → ReadAndExecute
   - inheritance from ProgramData stripped
4. Start the service and confirm it runs under the virtual account.

Log: `%TEMP%\ptworkspacessvc-setup.log`. Window stays open until you hit Enter.

## Step 2 — smoke test (as your normal user)

Open a **regular** PowerShell (not admin) and run:

```powershell
# 1) Build a fake "install folder" so the auth check accepts us.
$fake = "$env:TEMP\PTFakeInstall"
New-Item -ItemType Directory -Force $fake | Out-Null
Copy-Item D:\PowerToys-Workspaces-EoP-v6\x64\Debug\WorkspacesSvcSmokeTest\PowerToys.WorkspacesSvcSmokeTest.exe `
          "$fake\PowerToys.WorkspacesEditor.exe" -Force
$env:PT_DEV_INSTALL_FOLDER = $fake     # prototype-only override

# 2) Negative: the smoke test from its real location must be rejected.
& D:\PowerToys-Workspaces-EoP-v6\x64\Debug\WorkspacesSvcSmokeTest\PowerToys.WorkspacesSvcSmokeTest.exe ping
# Expected: AuthRejected

# 3) Positive: same exe, allowed name, allowed location.
& "$fake\PowerToys.WorkspacesEditor.exe" ping        # expect Ok
& "$fake\PowerToys.WorkspacesEditor.exe" get         # expect Ok (empty for new user)

# 4) Write a settings file through the service.
'{"workspaces":[]}' | Set-Content -Encoding UTF8 "$env:TEMP\sample.json"
& "$fake\PowerToys.WorkspacesEditor.exe" put "$env:TEMP\sample.json"   # expect Ok

# 5) Verify the service actually wrote the file.
$me = (whoami /user /fo csv /nh).Split(',')[1].Trim('"')
Get-Content "C:\ProgramData\Microsoft\PowerToys\Workspaces\$me\workspaces.json"

# 6) CORE EoP TEST — try to write directly as the same user.
#    Must be DENIED (this is the whole point of v6).
try {
    Set-Content "C:\ProgramData\Microsoft\PowerToys\Workspaces\$me\workspaces.json" '{"evil":true}'
    Write-Host "FAIL — direct write succeeded; DACL is not protecting the file" -ForegroundColor Red
} catch {
    Write-Host "PASS — direct write rejected: $($_.Exception.Message)" -ForegroundColor Green
}
```

## Cleanup (when done testing)

Elevated PowerShell:

```powershell
sc.exe stop   PTWorkspacesSvc
sc.exe delete PTWorkspacesSvc
Remove-Item -Recurse -Force C:\ProgramData\Microsoft\PowerToys\Workspaces
```

Normal PowerShell:

```powershell
Remove-Item Env:\PT_DEV_INSTALL_FOLDER
Remove-Item -Recurse -Force $env:TEMP\PTFakeInstall, $env:TEMP\sample.json
```

## Pass criteria

| Step | Expected |
|---|---|
| Setup script | "Setup complete" + service Running + owner = `NT SERVICE\PTWorkspacesSvc` |
| Smoke test step 2 | `AuthRejected` |
| Smoke test step 3 | `Ping=Ok`, `Get=Ok` (empty) |
| Smoke test step 4 | `Put=Ok` |
| Smoke test step 5 | JSON content prints |
| **Smoke test step 6** | **`PASS — direct write rejected: ...`** ← core EoP fix |
