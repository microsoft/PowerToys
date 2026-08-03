# Local UI-test VM setup

Use this reference for the one-time dockur/windows baseline. The VM is persistent: Docker runs QEMU,
while Windows and installed tools live in the named `/storage` volume.

## Host requirements

- Host: Windows 11 with hardware virtualization and nested virtualization enabled. This host
  requirement does not select the guest OS.
- Docker Desktop using the WSL2 Linux backend.
- PowerShell 7, `docker`, and `wsl.exe` on `PATH`.
- Enough free RAM for the green-first guest (half of host RAM), at least 4 GB of Docker/WSL
  overhead, and 128 GB of free disk.
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

The default `green-first` resource profile derives half of host RAM and about 60% of physical cores,
rounded up to an even count (8 host cores becomes 6 guest vCPUs). Run
`Start-LocalVm.ps1 -PlanOnly` to inspect the result. Ensure `%UserProfile%\.wslconfig` gives WSL2 at
least 4 GB more than the resolved guest RAM, then run `wsl --shutdown` and restart Docker Desktop
after changing that ceiling. Use the constrained profile only after the target suite is green.

The default guest is Windows 10 Enterprise LTSC 2021 (`WINDOWS_VERSION=10l`, build 19044/21H2),
which is newer than Windows 10 20H2. Pin `DOCKUR_IMAGE` to a tested tag or digest for a team baseline.
Do not replace this default with consumer Windows 10 Pro for a long-lived .NET 10 baseline.

For mixed-version requirements, run the ordinary or cross-version suite on this Windows 10 guest.
Create another scaffold such as `X:\PowerToysUiTestVm-Win11` only when a requirement explicitly
depends on Windows 11. Set `WINDOWS_VERSION=11` plus unique `VM_CONTAINER_NAME` and `VM_VOLUME_NAME`
values there. Stop the Windows 10 VM first or assign different loopback ports. Never turn the
Windows 10 volume into the Windows 11 baseline.

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

The dockur image is a Linux/QEMU wrapper, not a preinstalled Windows disk. The first boot downloads
official Windows media and performs a complete unattended installation into the named volume. It can
take many minutes. Watch progress at `http://127.0.0.1:8006/`.

### Watch the guest in the VS Code integrated browser (agent-visible)

The viewer at `http://127.0.0.1:<VM_VIEWER_PORT>/` (default `8006`; use the VM's own port, for example
`8007` for a second Windows 11 VM) is a live noVNC canvas. Open it in VS Code's integrated Simple
Browser so an agent can watch the unattended install, confirm the `PTUser` desktop, or drive Windows
Setup on the rare occasion it needs input:

- **Automatic (agent-driven).** Have the agent open the viewer URL with its integrated-browser tool
  (the VS Code browser `open_browser_page`/Simple Browser integration). VS Code opens the Simple
  Browser tab and returns a page id, and the page is shared with the agent immediately — no manual
  step. The agent then reads or screenshots the noVNC canvas (a screenshot shows the install
  percentage and, later, the live desktop) and can click or type into the guest.

The canvas is for observation and occasional input only; it is never a durable control or result
channel. Always drive tests and collect evidence through WinRM and the shared exchange.

A clean supplemental Windows 11 installation can exceed four hours on nested virtualization. Use a
long bounded readiness window such as `-TimeoutMinutes 720`; a timeout does not stop the container or
destroy the named volume, so resume the same VM instead of recreating it when Setup is still visibly
progressing.

On a host that is itself a virtual machine, this workflow is deeply nested: host hypervisor ->
Windows host -> WSL2/Docker Desktop -> KVM/QEMU -> Windows guest. Green-first CPU and RAM remove
artificial resource scarcity, but Windows Setup image application can still use only a few vCPUs and
issue low-throughput synchronous writes while expanding `install.wim`. Treat the first ISO install as
a one-time baseline build. After provisioning and validation, stop and clone/snapshot the named
volume for future clean runs instead of reinstalling Windows.

Seeing the Windows desktop does not mean provisioning is complete. A visible command window may run
`C:\OEM\install.bat`, which configures `PTUser`, HTTPS WinRM, auto-logon, and optional prerequisites.
Do not close it or start tests. Wait for it to exit, for `C:\OEM\ProvisioningReady.json` to exist, and
for the final desktop to log on as `PTUser`, not the administrator.

After login:

1. Set Windows display resolution to 1920x1080 and scaling to the baseline required by the suite.
2. Confirm Explorer is running and the desktop remains unlocked.
3. Confirm `C:\OEM\ProvisioningReady.json` exists.
4. Confirm `\\host.lan\Data` opens from `PTUser`; `Z:` is optional and must not be used as a durable
   control/result path because Explorer restarts can remove it.
5. Confirm the Windows license is not an expired time-based evaluation. Expired evaluations shut
  down hourly and cannot provide a stable test baseline; replace the media or use a properly
  licensed image rather than disabling licensing enforcement.

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
