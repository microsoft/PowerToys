# Troubleshooting local VM UI tests

Classify the first failed boundary before changing test code.

| Symptom | Boundary | Action |
|---|---|---|
| `/dev/kvm` missing or QEMU uses software emulation | Docker Desktop WSL/KVM | See **KVM and nested virtualization** below. Distinguish unloaded modules (`vmx` > 0 → `modprobe kvm_intel`/`kvm_amd`, or run the start script) from nested virt not exposed (`vmx` == 0 → check `.wslconfig` + `wsl --shutdown`, then revert the offending OS/security update). |
| Container exits during installation | Storage/media/resources | Inspect `docker logs`, free disk/RAM, and OEM log. Use the named native Docker volume rather than an NTFS bind for `/storage`. |
| Windows Setup remains at the same percentage for hours, the guest disk mtime/allocation is static, and the guest IP is unreachable | Stalled clean installation | Confirm the screen, `docker stats`, `/storage/data.img`, and guest neighbor state. Restart only the container once; the named volume is preserved and unattended Setup can resume. Do not delete the volume while Setup is visibly progressing or disk writes continue. |
| Guest cleanly shuts down about hourly; System event 1074 names `wlms.exe` | Expired Windows evaluation | Check `slmgr.vbs /dlv` or `SoftwareLicensingProduct`. Do not bypass licensing enforcement. Recreate the VM with current evaluation media or a properly licensed Windows image. The controller blocks expired time-based evaluations before test dispatch. |
| Viewer works but WinRM never opens | OEM/HTTPS listener | Check `C:\OEM\Provision-UiTestVm.log`, `ProvisioningReady.json`, listener on 5986, guest firewall, and compose loopback mapping. |
| TLS/CN error from `New-PSSession` | Self-signed WinRM certificate | Use the controller's default skip-CA/CN options on loopback. Do not disable TLS or expose the endpoint remotely. |
| Credential rejected | Control account/DPAPI file | Recreate the credential file manually with the compose administrator name/password. DPAPI files do not roam between host users/machines. |
| Desktop probe times out | No logged-on standard user | Open the viewer, verify `PTUser` is the active console user, Explorer is running, and the scheduled task uses `Interactive`/`Limited`. Reboot after first OEM provisioning if auto-login has not switched users. |
| Probe says user is administrator | Wrong account/baseline | Remove the test user from Administrators and log on again. Do not accept `RunLevel=Limited` as proof when UAC is disabled; inspect the token as the probe does. |
| Probe reports wrong dimensions | Persistent display setting/viewer | Set Windows display resolution and scaling in the VM. Use both desktop parameters as zero only for nonvisual tests where size is irrelevant. |
| `Z:` disappears after Explorer restart | Session-scoped mapped drive | Use `\\host.lan\Data` in requests and runner actions. The shared guest runner canonicalizes mapped roots when possible. |
| UNC inaccessible to test user | Dockur share/account | Verify `./shared:/shared`, open `\\host.lan\Data` interactively, and keep the exchange below `<VmRoot>\shared`. |
| No `status.json` but task ended | Guest runner/finalization | Inspect scheduled-task `LastTaskResult`, guest-local transcript, request path, and share access. A completed UI is not a completion signal. |
| Interactive `powershell.exe` task stays `Running`, but a local `cmd.exe` probe writes immediately | PTUser PowerShell task host | Stop and unregister only the failed task. Stage and extract payloads through PTAdmin WinRM, then run a guest-local `.cmd` as an `Interactive`/`Limited` PTUser task. Write the exit code and TRX locally and export one evidence archive through PTAdmin. If the cmd task also fails, launch the prepared batch through the viewer. |
| `status.json` exists but is temporarily empty | SMB creation/write race | Wait for parseable JSON with matching `RunId`; never finish on file existence alone. The controller already does this. |
| One attachment subtree fails export | Transient SMB tree copy | Inspect `ExportErrors`. The shared runner uses bounded `robocopy` retries for directories and writes status even when an artifact cannot be copied. |
| Zero tests/MTP exit 8 | Filter | Qualify the filter with `Name=`, `Name~`, `FullyQualifiedName~`, or `TestCategory=`. Treat as `BLOCKED`. |
| A legitimate winappcli UIA call is killed after 60 seconds on a resource-limited guest | Per-call process guard | The local-VM runner sets `WINAPP_CLI_INVOKE_TIMEOUT_SECONDS=180`. Increase it only for a measured slow guest call; accepted values are 1-3600 seconds, and command-specific `-t`/`--timeout` plus grace still takes precedence. |
| Tests run but some fail only in this VM | Profile/display/foreground/environment | Preserve TRX and media, report pass rate and failure groups, and compare guest user/session/display with CI. Do not edit stabilized tests unless asked. |
| Win11 tier-1 command is absent and the module log reports MSIX registration error `0x800B0100` | Unsigned local context-menu package | Sign the local MSIX with a test certificate whose subject matches the manifest publisher, import only its public certificate into the guest machine `TrustedPeople` and `Root` stores, and verify `Get-AuthenticodeSignature` reports `Valid`. Keep the signed package and certificate out of source control. A successful classic COM registration does not validate the modern menu. |
| Windows Search or another shell surface owns foreground | Persistent desktop state | Dismiss/reset the shell state or restart the VM before rerunning. Classify as environment when the test is already stable in CI. |
| WebView/Monaco stays loading | WebView2/runtime/profile | Verify baseline WebView2 version or stage the signed installer for the run. Preserve WebView logs and screenshots. |
| Reuse reports missing manifest | First run or cleaned work root | Run once without `-ReuseStagedPayload`, then reuse. A recreated volume necessarily needs a full first stage. |
| Changed archive is not refreshed | Hash/request mismatch | Compare request SHA-256 values with actual archives and inspect `RefreshedComponents`. Do not compare only apphost EXE hashes. |
| Remote debugger cannot connect | Port/firewall/monitor identity | Verify `msvsmon` is running, TCP 4026 is mapped to the chosen host port, guest firewall permits it, and Visual Studio uses `127.0.0.1:<host-port>`. |
| VM state hides a first-run defect | Retained profile/cache | Restore a known snapshot or create a fresh named volume. |

## KVM and nested virtualization (`/dev/kvm`)

The dockur container maps `/dev/kvm`; without it it never starts, failing with
`error gathering device information while adding custom device "/dev/kvm": no such file or directory`
(and any premature `docker compose up` leaves the container `Exited (255)`). Two very different causes
share this symptom — separate them first, in the `docker-desktop` distribution:

```pwsh
# Is VMX exposed to the WSL2 utility VM? 0 = nested virt NOT exposed; >0 = exposed.
wsl.exe -d docker-desktop -u root -- grep -c -w vmx /proc/cpuinfo
# Can the module load, and does the device exist?
wsl.exe -d docker-desktop -u root -- modprobe kvm_intel      # or kvm_amd on AMD
wsl.exe -d docker-desktop -u root -- sh -lc 'ls -l /dev/kvm; lsmod | grep kvm'
wsl.exe -d docker-desktop -u root -- sh -lc 'dmesg | grep -i kvm | tail'
```

- **`vmx` > 0 but `/dev/kvm` is missing — the modules just are not loaded (normal after any reboot).**
  A fresh WSL utility VM starts with no KVM modules, so a container launched too early exits `255`.
  Load them (`modprobe kvm_intel`/`kvm_amd`) — or simply run `Start-LocalVm.ps1` (or the older start
  script), which `modprobe`s before `docker compose up` — then start the VM. No host change needed.

- **`vmx` == 0 — nested virtualization is not exposed to WSL2 (host-level).** `modprobe kvm_intel`
  reports `Not supported` and dmesg shows `kvm: VMX not supported by CPU`. First rule out config:
  confirm `%UserProfile%\.wslconfig` has `nestedVirtualization=true` under `[wsl2]` (a `processors=`
  value matching `nproc` proves the section is applied), that WSL is current (`wsl --version`), then
  `wsl --shutdown` and let Docker Desktop recreate the utility VM. If `vmx` is still 0 after that clean
  restart, the L1 hypervisor is withholding VT-x from the child VM — usually a **host OS/build change,
  not your config**:
  - A feature update or a **forced/managed update** can flip virtualization-based security (VBS/VSM)
    enforcement so it no longer exposes nested VT-x. Correlate the break with the last boot
    (`(Get-CimInstance Win32_OperatingSystem).LastBootUpTime`), the update history
    (`Get-HotFix | Sort-Object InstalledOn -Descending`), and VBS state
    (`Get-CimInstance -Namespace root\Microsoft\Windows\DeviceGuard Win32_DeviceGuard |`
    ` Select-Object VirtualizationBasedSecurityStatus, SecurityServicesRunning`).
  - **Resolutions, least invasive first:**
    1. **Reboot** once — an update may be only half-applied.
    2. **Revert the offending OS update / roll back the build**: `wusa /uninstall /kb:<id>` or
       Settings → Windows Update → Update history → *Uninstall updates* (or roll back an Insider
       flight), then reboot and re-check `vmx`. A bare reboot alone will not help if the update
       re-applies the same enforcement each boot — removing the update is the fix.
    3. **Managed device:** on an Intune/enterprise-managed machine the VBS policy is locked and cannot
       be toggled locally — escalate to whoever owns the update/flight to hold or revert it.
    4. **Use a different host** that exposes nested virtualization (another box, or a cloud VM whose
       provider allows nesting).
  - Avoid disabling Credential Guard / Memory Integrity as a first move: it is frequently policy-locked,
    needs elevation plus a reboot, and in practice the *same* VBS configuration can work again after a
    later update — so the update, not the security feature per se, is the variable to act on.

## Host diagnostics

```pwsh
docker context use desktop-linux
docker compose --env-file <VmRoot>\.env -f <VmRoot>\compose.yml ps --all
docker logs <container-name> --tail 200
wsl.exe -d docker-desktop -u root -- sh -lc 'ls -l /dev/kvm; lsmod | grep kvm'
Test-NetConnection 127.0.0.1 -Port 15986
```

## Guest control diagnostics

Use the administrator DPAPI credential and HTTPS session from [setup.md](setup.md):

```pwsh
Invoke-Command $session {
  Get-ChildItem WSMan:\localhost\Listener
  Get-ScheduledTask -TaskName 'PowerToysUiTest-*' -ErrorAction SilentlyContinue |
    Select-Object TaskName,State,@{n='User';e={$_.Principal.UserId}},@{n='RunLevel';e={$_.Principal.RunLevel}}
  query user
  Get-CimInstance Win32_Process -Filter "Name='explorer.exe'"
}
```

Do not launch UI tests directly from the WinRM session: it is not the interactive desktop. Use the
limited interactive scheduled task created by the controller.

## Guest-local evidence before termination

If a run appears hung, use administrator WinRM only to copy diagnostics into the UNC result folder.
Do not kill Explorer or the test host until process state, foreground details, and the live transcript
are preserved. Let the guest runner's `finally` write status whenever possible.

## Cleanup

The controller unregisters its scheduled tasks. Stop the VM with `Stop-LocalVm.ps1`; do not run
`docker compose down -v` unless destroying the baseline is intentional. Preserve run folders before
resetting the volume.
