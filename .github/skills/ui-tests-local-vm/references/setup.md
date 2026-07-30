# Local UI-test VM setup

Use this reference for the one-time dockur/windows baseline. The VM is persistent: Docker runs QEMU,
while Windows and installed tools live in the named `/storage` volume.

## Host requirements

- Windows 11 with hardware virtualization and nested virtualization enabled.
- Docker Desktop using the WSL2 Linux backend.
- PowerShell 7, `docker`, and `wsl.exe` on `PATH`.
- At least 8 GB free RAM and 128 GB free disk for the recommended defaults.
- Loopback ports `8006`, `13389`, and `15986` available, or changed in `.env`.

Docker Desktop on Windows requires KVM inside its `docker-desktop` WSL distribution for this
workflow. The start script loads `kvm` plus `kvm_intel` or `kvm_amd` before starting QEMU.

Keep the VM outside the repository. To keep all large data on another drive, move Docker Desktop's
WSL disk image to that drive in Docker Desktop settings before creating the named volume. Do not bind
mount the Windows system disk to NTFS; use the native Docker volume in the compose template.

## 1. Scaffold the VM

```pwsh
pwsh .github\skills\ui-tests-local-vm\scripts\Initialize-LocalVm.ps1 `
  -DestinationRoot X:\PowerToysUiTestVm
```

The result contains:

```text
X:\PowerToysUiTestVm\
|-- compose.yml
|-- .env.example
|-- .gitignore
|-- Start-LocalVm.ps1
|-- Stop-LocalVm.ps1
|-- oem\
|   |-- install.bat
|   `-- Provision-UiTestVm.ps1
`-- shared\
```

Copy `.env.example` to `.env`, then set a unique administrator password. `.env` is ignored by the
scaffold and must never be committed or sent through chat.

Pin `DOCKUR_IMAGE` to a tested tag or digest for a team baseline. Choose `WINDOWS_VERSION=11` for the
broadest current .NET 10 support. Windows 10 Enterprise LTSC is appropriate when Win10 behavior is
the explicit target; consumer Windows 10 Pro is not the preferred long-lived .NET 10 baseline.

## 2. Understand the two accounts

The compose administrator (`VM_ADMIN_USERNAME`, default `PTAdmin`) is the control identity used only
for HTTPS WinRM and scheduled-task registration.

OEM provisioning creates `PTUser` with a random password inside the VM, removes it from
Administrators, grants it access to `C:\PowerToysUiTestRun`, and configures console auto-logon.
Tests run as `PTUser` with `Interactive` logon and `Limited` run level. The controller fails before
tests when that token is elevated, session 0, missing Explorer, or unable to reach the shared UNC.

Windows auto-logon necessarily stores recoverable credentials in the guest. Treat this as an
isolated test VM, bind all management ports to loopback, and never reuse either account elsewhere.

## 3. Optional OEM prerequisites

Place signed offline installers in `oem` before the first boot:

- `dotnet-sdk-10*-win-x64.exe`, or `windowsdesktop-runtime-10*-win-x64.exe`.
- `MicrosoftEdgeWebView2RuntimeInstaller*.exe`.

The provisioning script installs matching files silently. See [customization.md](customization.md)
for .NET pinning, remote debugging, and golden baseline guidance.

## 4. Start and install Windows

```pwsh
cd X:\PowerToysUiTestVm
pwsh .\Start-LocalVm.ps1 -WaitForWinRM -TimeoutMinutes 45
```

The first boot downloads and installs Windows and can take many minutes. Watch progress at
`http://127.0.0.1:8006/`. The final desktop must log on as `PTUser`, not the administrator.

After login:

1. Set Windows display resolution to 1920x1080 and scaling to the baseline required by the suite.
2. Confirm Explorer is running and the desktop remains unlocked.
3. Confirm `C:\OEM\ProvisioningReady.json` exists.
4. Confirm `\\host.lan\Data` opens from `PTUser`; `Z:` is optional and must not be used as a durable
   control/result path because Explorer restarts can remove it.

## 5. Save the administrator credential

Create a DPAPI-protected credential file manually. Type the password directly into the secure prompt;
never provide it to an agent.

```pwsh
$credentialRoot = Join-Path $env:LOCALAPPDATA 'PowerToysUiTestVm'
New-Item $credentialRoot -ItemType Directory -Force | Out-Null
Get-Credential -UserName PTAdmin -Message 'Local UI-test VM administrator' |
  Export-Clixml (Join-Path $credentialRoot 'admin.credential.xml')
```

The file can be decrypted only by the same Windows user on the same host. The controller defaults to
this path. Use `-CredentialPath` for another location.

Verify HTTPS WinRM without exposing the password:

```pwsh
$credential = Import-Clixml "$env:LOCALAPPDATA\PowerToysUiTestVm\admin.credential.xml"
$option = New-PSSessionOption -SkipCACheck -SkipCNCheck -SkipRevocationCheck
$session = New-PSSession `
  -ConnectionUri https://127.0.0.1:15986/wsman `
  -Authentication Basic -Credential $credential -SessionOption $option
Invoke-Command $session { whoami; Get-Content C:\OEM\ProvisioningReady.json }
Remove-PSSession $session
```

The self-signed certificate is acceptable because the compose port is loopback-only. Do not expose
Basic WinRM to a LAN or the Internet.

## 6. Verify the controller without running tests

Stage the archives described in [agentic-loop.md](agentic-loop.md), then run `-PlanOnly`:

```pwsh
pwsh .github\skills\ui-tests-local-vm\scripts\Invoke-LocalVmUiTest.ps1 `
  -VmRoot X:\PowerToysUiTestVm `
  -ExchangeRoot X:\PowerToysUiTestVm\shared\PowerToysUiTests\MyModule `
  -TestExecutable MyModule.UITests.Next.exe `
  -Filter 'Name=MyModule.FocusedTest' `
  -PlanOnly
```

Inspect the generated `controller-plan.json` and `request.json`. Confirm the guest exchange is under
`\\host.lan\Data`, filters are qualified, hashes are nonempty, and no credential appears in either
file.

## Lifecycle and reset

`Start-LocalVm.ps1` reuses the existing disk. `Stop-LocalVm.ps1` stops QEMU but preserves the disk.
The test controller leaves the VM running by default; pass `-StopVmAfterRun` when desired.

Deleting the named Docker volume destroys the Windows installation. Do it only for an intentional
clean reset after stopping the VM and preserving required evidence. Prefer restoring a known stopped
volume snapshot or creating a fresh volume for repeatable clean-profile checks.

## Existing HTTP WinRM VM

An older local VM may expose HTTP WinRM on another loopback port. The controller supports
`-UseHttpWinRM -WinRmPort <port>` only as a migration path. It uses Negotiate/NTLM message
encryption rather than Basic authentication. Prefer rerunning OEM provisioning to create the HTTPS
listener, then remove the HTTP port mapping.
