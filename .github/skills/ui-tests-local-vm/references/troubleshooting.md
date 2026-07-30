# Troubleshooting local VM UI tests

Classify the first failed boundary before changing test code.

| Symptom | Boundary | Action |
|---|---|---|
| `/dev/kvm` missing or QEMU uses software emulation | Docker Desktop WSL/KVM | Verify Windows 11, firmware virtualization, nested virtualization, and `wsl -d docker-desktop -u root -- modprobe kvm`. Run the provided start script. |
| Container exits during installation | Storage/media/resources | Inspect `docker logs`, free disk/RAM, and OEM log. Use the named native Docker volume rather than an NTFS bind for `/storage`. |
| Viewer works but WinRM never opens | OEM/HTTPS listener | Check `C:\OEM\Provision-UiTestVm.log`, `ProvisioningReady.json`, listener on 5986, guest firewall, and compose loopback mapping. |
| TLS/CN error from `New-PSSession` | Self-signed WinRM certificate | Use the controller's default skip-CA/CN options on loopback. Do not disable TLS or expose the endpoint remotely. |
| Credential rejected | Control account/DPAPI file | Recreate the credential file manually with the compose administrator name/password. DPAPI files do not roam between host users/machines. |
| Desktop probe times out | No logged-on standard user | Open the viewer, verify `PTUser` is the active console user, Explorer is running, and the scheduled task uses `Interactive`/`Limited`. Reboot after first OEM provisioning if auto-login has not switched users. |
| Probe says user is administrator | Wrong account/baseline | Remove the test user from Administrators and log on again. Do not accept `RunLevel=Limited` as proof when UAC is disabled; inspect the token as the probe does. |
| Probe reports wrong dimensions | Persistent display setting/viewer | Set Windows display resolution and scaling in the VM. Use both desktop parameters as zero only for nonvisual tests where size is irrelevant. |
| `Z:` disappears after Explorer restart | Session-scoped mapped drive | Use `\\host.lan\Data` in requests and runner actions. The shared guest runner canonicalizes mapped roots when possible. |
| UNC inaccessible to test user | Dockur share/account | Verify `./shared:/shared`, open `\\host.lan\Data` interactively, and keep the exchange below `<VmRoot>\shared`. |
| No `status.json` but task ended | Guest runner/finalization | Inspect scheduled-task `LastTaskResult`, guest-local transcript, request path, and share access. A completed UI is not a completion signal. |
| `status.json` exists but is temporarily empty | SMB creation/write race | Wait for parseable JSON with matching `RunId`; never finish on file existence alone. The controller already does this. |
| One attachment subtree fails export | Transient SMB tree copy | Inspect `ExportErrors`. The shared runner uses bounded `robocopy` retries for directories and writes status even when an artifact cannot be copied. |
| Zero tests/MTP exit 8 | Filter | Qualify the filter with `Name=`, `Name~`, `FullyQualifiedName~`, or `TestCategory=`. Treat as `BLOCKED`. |
| Tests run but some fail only in this VM | Profile/display/foreground/environment | Preserve TRX and media, report pass rate and failure groups, and compare guest user/session/display with CI. Do not edit stabilized tests unless asked. |
| Windows Search or another shell surface owns foreground | Persistent desktop state | Dismiss/reset the shell state or restart the VM before rerunning. Classify as environment when the test is already stable in CI. |
| WebView/Monaco stays loading | WebView2/runtime/profile | Verify baseline WebView2 version or stage the signed installer for the run. Preserve WebView logs and screenshots. |
| Reuse reports missing manifest | First run or cleaned work root | Run once without `-ReuseStagedPayload`, then reuse. A recreated volume necessarily needs a full first stage. |
| Changed archive is not refreshed | Hash/request mismatch | Compare request SHA-256 values with actual archives and inspect `RefreshedComponents`. Do not compare only apphost EXE hashes. |
| Remote debugger cannot connect | Port/firewall/monitor identity | Verify `msvsmon` is running, TCP 4026 is mapped to the chosen host port, guest firewall permits it, and Visual Studio uses `127.0.0.1:<host-port>`. |
| VM state hides a first-run defect | Retained profile/cache | Restore a known snapshot or create a fresh named volume. |

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
