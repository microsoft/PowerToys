# Troubleshooting local VM UI tests

Classify the first failed boundary before changing test code.

## Host and guest lifecycle

| Symptom | Boundary | Action |
|---|---|---|
| `Get-VM` / `New-PSSession -VMName` fails with `You do not have the required permission` | Host Hyper-V access | Rerun from an elevated PowerShell 7 terminal, or add the account to the local `Hyper-V Administrators` group (one-time elevated change, effective after signing out and back in). Creating a guest still needs full elevation because it partitions and mounts disks. |
| `Mount-VHD` fails `0x80070522`, or `Initialize-Disk`/`Get-WindowsImage` is denied | Guest creation without elevation | Group membership is not enough for disk and image APIs. Run `New-UiTestVm.ps1` from a genuinely elevated terminal. Report `BLOCKED` instead of looking for a workaround. |
| Hyper-V operations hang at 0% CPU, `vmms`/`vmwp` never return, and even `Get-VM` blocks | VHDX on a Dev Drive | Move `VhdPath`/`VmPath` to NTFS; the exchange can stay where it is. Observed on one host and fixed by the move - not a property of ReFS, which Hyper-V supports. `New-UiTestVm.ps1` refuses ReFS by default as a proxy for Dev Drive; `-AllowReFsVolume` overrides. Recovery usually needs a `vmms` restart or a host reboot; wrap suspect Hyper-V calls in `Start-Job` + `Wait-Job -Timeout` so a wedged service does not hang the agent. |
| Guest boots to `0xc000000e` after applying an image on the host | Native-VHD boot entries | Do not `bcdboot` a mounted VHDX from the host: the BCD keeps `vhd=[X:]\...` device references that only resolve in the host's drive view. Create the guest by running Windows Setup inside the VM from an answer-file ISO, which is what `New-UiTestVm.ps1` does. |
| Guest boots to Setup instead of the desktop | Answer file was not applied | Confirm the disk was built by `New-UiTestVm.ps1` rather than attached from an unprepared image, and read the guest's `C:\Windows\Panther\setupact.log`. |
| Setup shows a cancel prompt, or stops around 10% | Key injection overshoot | Installation media waits for "Press any key to boot from CD or DVD", so the script types Enter through `Msvm_Keyboard` - but only until the framebuffer brightens, and at most a bounded number of times. Do not send unbounded keystrokes; later Enters land on Setup's Cancel button. |
| Windows 10 servicing fails just after reboot with `0x8024001E` | Windows Update service transition | Refresh the skill scripts and rerun `Update-LocalVmGuest.ps1`. The updater retries this bounded `WU_E_SERVICE_STOP` transition while the service settles; do not recreate the guest. |
| Guest cleanly shuts down about hourly; System event 1074 names `wlms.exe` | Expired Windows evaluation | Check `slmgr.vbs /dlv` or `SoftwareLicensingProduct`. Do not bypass licensing enforcement. Recreate the VM with current evaluation media or a properly licensed Windows image. The controller blocks expired time-based evaluations before test dispatch. |
| Console shows a second, empty session | Enhanced session mode | Turn enhanced session off in the VMConnect View menu. It opens an RDP session that displaces the console session where the standard user is logged on. |
| Guest disk grows without bound | Accumulated checkpoints | `Reset-LocalVm.ps1 -List`, then remove obsolete checkpoints. Budget at least twice `DiskSizeGB` plus `MemoryStartupGB` per standard checkpoint. |

## Control channel and desktop

| Symptom | Boundary | Action |
|---|---|---|
| `New-PSSession -VMName` fails with a logon error | Control account/DPAPI file | Recreate the credential file with `Get-Credential \| Export-Clixml`, typed directly into the prompt. DPAPI files do not roam between host users or machines. |
| `New-PSSession -VMName` reports the guest is not ready | Guest still booting, or PowerShell Direct disabled | Read the console with `Get-VmConsoleImage.ps1`. PowerShell Direct needs the guest running and the Hyper-V integration services enabled; it does not need networking. |
| `Copy-VMFile` fails `The Guest Service Interface is not enabled` | Integration service off | `Enable-VMIntegrationService -VMName <name> -Name 'Guest Service Interface'`. Without it, the controller falls back to the much slower session copy. |
| A large archive copy stalls near completion | Session-copy fallback on a big file | `Copy-Item -ToSession` stalls on archives approaching a gigabyte. Confirm `Copy-VMFile` is being used; run with `-Verbose` to see why it fell back. |
| Desktop probe times out | No logged-on standard user | Read the console image, verify `PTUser` is the active console user, Explorer is running, and the scheduled task uses `Interactive`/`Limited`. Refresh the scaffold and use the current stop/start scripts: `Set-UiTestAutoLogon.ps1` must update the protected LSA secret and remove stale Winlogon password/count values. |
| Probe says user is administrator | Wrong account/baseline | Remove the test user from Administrators and log on again. Do not accept `RunLevel=Limited` as proof when UAC is disabled; inspect the token as the probe does. |
| Probe reports wrong dimensions | Resolution task did not run or the synthetic display was still initializing | Provisioning registers a logon task that retries `ChangeDisplaySettings` in the interactive session; check `C:\PowerToysUiTestRun\set-resolution.json`. Refresh the skill scripts and rerun the task after Explorer is stable if an older baseline still reports `ChangeDisplaySettingsResult=-1`. Display settings cannot be applied from the PowerShell Direct session. Use zero for both desktop parameters only for nonvisual tests. |

## Run dispatch and evidence

| Symptom | Boundary | Action |
|---|---|---|
| No `status.json` but the task ended | Guest runner/finalization | The controller stops on the bounded task-state check. Inspect `LastTaskResult`, the guest-local transcript, and the request path; task exit is not completion. |
| Test task remains `Ready` and has no `LastRunTime` | Task dispatch | The controller stops after the 30-second launch deadline. Verify the interactive PTUser session and Task Scheduler rather than waiting for the suite timeout. |
| Interactive `powershell.exe` task stays `Running`, but a local `cmd.exe` probe writes immediately | PTUser PowerShell task host | Stop and unregister only the failed task. Stage and extract payloads through the administrator session, then run a guest-local `.cmd` as an `Interactive`/`Limited` PTUser task. Write the exit code and TRX locally and export one evidence archive afterwards. |
| `status.json` exists but is temporarily empty | Create/write race | Wait for parseable JSON with matching `RunId`; never finish on file existence alone. The controller already does this. |
| One attachment subtree fails export | Transient copy failure | Inspect `ExportErrors`. The shared runner uses bounded `robocopy` retries for directories and writes status even when an artifact cannot be copied. |
| Zero tests/MTP exit 8 | Filter | Qualify the filter with `Name=`, `Name~`, `FullyQualifiedName~`, or `TestCategory=`. Treat as `BLOCKED`. |
| Test process exits 0 but TRX has skipped/`NotExecuted` tests | Incomplete suite | Treat the run as `FAIL`. The controller and guest runner require `total > 0` and `executed == total`; inspect inconclusive messages and restore missing prerequisites instead of accepting the process exit code. |
| Reuse reports a missing manifest | First run or cleaned work root | Run once without `-ReuseStagedPayload`, then reuse. A recreated guest necessarily needs a full first stage. |
| Changed archive is not refreshed | Hash/request mismatch | Compare request SHA-256 values with the actual archives and inspect `RefreshedComponents`. Do not compare only apphost EXE hashes. |

## Test behavior in the VM

| Symptom | Boundary | Action |
|---|---|---|
| A legitimate winappcli UIA call is killed after 60 seconds on a resource-limited guest | Per-call process guard | The local-VM runner sets `WINAPP_CLI_INVOKE_TIMEOUT_SECONDS=180`. Increase it only for a measured slow guest call; accepted values are 1-3600 seconds, and command-specific `-t`/`--timeout` plus grace still takes precedence. |
| Visual baselines are never found, or the run behaves unexpectedly like CI | `-Platform` value | `-Platform` flows to the guest as `platform`, names baselines (`<Class>_<Test>_<Platform>.png`), and any non-empty value marks the run as pipeline-like. Use only `x64Win10`, `x64Win11`, or `ARM64`. |
| Win11 tier-1 command is absent and the module log reports MSIX registration error `0x800B0100` | Unsigned local context-menu package | Sign the local MSIX with a test certificate whose subject matches the manifest publisher, import only its public certificate into the guest machine `TrustedPeople` and `Root` stores, and verify `Get-AuthenticodeSignature` reports `Valid`. Sign at packaging time - see [setup.md](setup.md), step 6a. A successful classic COM registration does not validate the modern menu. |
| Windows Search or another shell surface owns foreground | Persistent desktop state | Dismiss/reset the shell state or restart the VM before rerunning. Classify as environment when the test is already stable in CI. |
| A fixture/helper console owns foreground while Explorer cannot become stable | Activating helper launch | Inspect the failure PNG/MP4 and `GetForegroundWindowInfo()` first. If the foreground PID is the fixture, make it non-activating from creation; do not loosen Explorer's foreground assertion. An Explorer-opened `.cmd` can create a console despite `start /b`, and hiding the first enumerated window is racy. Use a direct `CreateNoWindow` child when integrity permits, or a hidden medium-integrity launcher such as `WScript.Shell.Run(..., 0, False)`, then verify no main window/foreground ownership. |
| Exact-HWND foreground check fails, but PNG shows the target usable (or foreground is the same process under a new HWND / zero) | Foreground requirement is broader than the interaction | Check what happens next. Keep strict foreground for Explorer menus, SendInput, coordinates, and drags. For coordinate-free UIA search/invoke, focus can be best-effort: bind readiness to the live process/window and authoritative UIA element, and retain foreground details as diagnostics. |
| WebView/Monaco stays loading | WebView2/runtime/profile | Verify the baseline WebView2 version or stage the signed installer for the run. Preserve WebView logs and screenshots. |
| Remote debugger cannot connect | Firewall/monitor identity | Verify `msvsmon` is running in the guest and that the guest is reachable on the chosen adapter. See [customization.md](customization.md). |
| Tests run but some fail only in this VM | Profile/display/foreground/environment | Preserve TRX and media, report pass rate and failure groups, and compare guest user/session/display with CI. Do not edit stabilized tests unless asked. |
| VM state hides a first-run defect | Retained profile/cache | Restore the baseline checkpoint with `Reset-LocalVm.ps1 -Restore`, or rebuild the guest. |

## Host diagnostics

```pwsh
Get-VM PowerToysUiTest-Win11 | Format-List Name, State, Status, Uptime, ProcessorCount, MemoryAssigned
Get-VMIntegrationService -VMName PowerToysUiTest-Win11 | Select-Object Name, Enabled, PrimaryStatusDescription
Get-VMSnapshot -VMName PowerToysUiTest-Win11 | Select-Object Name, SnapshotType, CreationTime
(Get-Volume -DriveLetter C).FileSystemType          # NTFS expected for VhdPath/VmPath
pwsh .github\skills\ui-tests-local-vm\scripts\Get-VmConsoleImage.ps1 `
  -VmName PowerToysUiTest-Win11 -Path X:\evidence\console.png
```

If a Hyper-V call may be wedged, bound it rather than blocking the agent:

```pwsh
$job = Start-Job { Get-VM }
if (-not (Wait-Job $job -Timeout 30)) { 'BLOCKED: VMMS is not responding' }
```

## Guest control diagnostics

Use the administrator DPAPI credential over PowerShell Direct:

```pwsh
& .github\skills\ui-tests-local-vm\scripts\Invoke-GuestScript.ps1 `
  -VmName PowerToysUiTest-Win11 `
  -ScriptBlock {
      Get-ScheduledTask -TaskName 'PowerToysUiTest-*' -ErrorAction SilentlyContinue |
        Select-Object TaskName, State, @{n='User';e={$_.Principal.UserId}}, @{n='RunLevel';e={$_.Principal.RunLevel}}
      query user
      Get-CimInstance Win32_Process -Filter "Name='explorer.exe'" | Select-Object ProcessId, SessionId
  }
```

Do not launch UI tests directly from that session: it is not the interactive desktop. Use the limited
interactive scheduled task created by the controller.

## Guest-local evidence before termination

If a run appears hung, use the administrator session only to copy diagnostics into the result folder.
Do not kill Explorer or the test host until process state, foreground details, and the live transcript
are preserved. Let the guest runner's `finally` write status whenever possible.

## Cleanup

The controller unregisters its scheduled tasks. Stop the VM with `Stop-LocalVm.ps1`; delete the guest
disk only when destroying the baseline is intentional. Preserve run folders before resetting.
