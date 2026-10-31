# Multi-monitor guest

The default guest has one display, the Hyper-V synthetic display. Behavior that depends on monitor
edges, layout, or mixed DPI (Mouse Utilities, FancyZones, Mouse Without Borders) needs more. This
layer adds three virtual monitors inside the guest's console session, arranges them, and captures a
second baseline checkpoint. It runs after [setup.md](setup.md) has produced `provisioned-baseline`.

| Concern | Mechanism |
|---|---|
| Virtual monitors | The IndirectDisplay sample from [microsoft/Windows-driver-samples](https://github.com/microsoft/Windows-driver-samples/tree/main/video/IndirectDisplay), built at a pinned commit, unmodified |
| Driver trust | The guest signs the catalog with the built-in `Set-AuthenticodeSignature` and a certificate that exists only in the guest; no SDK tool enters the guest |
| Layout | `ChangeDisplaySettingsEx`, the same API as `Set-GuestResolution.ps1` |
| Per-monitor scale | `DisplayConfigSetDeviceInfo`, read back with `GetDpiForMonitor` |
| Verification | `Capture-MonitorLayout.ps1` from CursorWrap's tests, `tools\MonitorReportTool`, a full virtual-desktop PNG |
| Baseline | `Reset-LocalVm.ps1 -CreateBaseline -CheckpointName multimon-baseline` |

VMConnect and `Get-VmConsoleImage.ps1` show only the synthetic display. The virtual monitors are
seen through `desktop.png`, which `Save-VirtualScreenshot.ps1` writes from inside the session. Keep
the synthetic display primary. Indirect display drivers in a Hyper-V guest are reported to leave the
viewer black when a virtual monitor is primary.

## 1. Build the driver

```pwsh
pwsh .github\skills\ui-tests-local-vm\scripts\Build-IddSampleDriver.ps1 -VmRoot X:\PowerToysUiTestVm
```

Visual Studio needs the **Desktop development with C++** workload plus two individual components:
**Windows Driver Kit**, for the `WindowsUserModeDriver10.0` toolset, and **C++ Spectre-mitigated
libraries** for the target platform. The script checks both before fetching anything.

The script sparse-checks-out only the sample at the pinned commit. It then restores the WDK NuGet
packages with Visual Studio's MSBuild (`-t:restore -p:RestorePackagesConfig=true`), so no
`nuget.exe` is needed. `RestoreSources` names nuget.org as the only source, which replaces every
source configured on the machine. `RestoreAdditionalProjectSources` would add to them instead. The
repository's `packages.config` sits at its root, and MSBuild restore reads only a `packages.config`
next to a project. So the script writes one beside `IddSampleDriver.vcxproj`, carrying the root
file's exact pins for one platform. The build runs with `SignMode=Off` and copies the `.inf`,
`.cat`, and `.dll` to `Multimon.DriverPath`, by default `multimon\IddSampleDriver` under the VM
root, where `Initialize-LocalVmMultimon.ps1` reads them.

Raise the pinned commit deliberately, after reading the sample's changes, the same way
`Get-WindowsMedia.ps1` pins Fido.

### MonitorReportTool (optional)

`Initialize-LocalVmMultimon.ps1` records `monitor_ids.txt` with `tools\MonitorReportTool` when it
finds a build, and skips that report otherwise. The tool runs in the guest, so build it for the
platform that matches `ProcessorArchitecture` (`x64` for `amd64`, `arm64` for `arm64`):

```pwsh
.\tools\build\build.ps1 -Platform x64 -Configuration Release -Path tools\MonitorReportTool
```

The build writes `PowerToys.MonitorReportTool.exe` to `<platform>\Release\MonitorReportTool` under
the repository root, where the script looks by default. `Multimon.MonitorReportToolPath` or
`-MonitorReportToolPath` names another build.

## 2. Apply a layout and capture the baseline

The guest must be running with the standard user logged on (`Start-LocalVm.ps1 -Wait`). Run with
`-WhatIf` first to list every copy and step:

```pwsh
pwsh .github\skills\ui-tests-local-vm\scripts\Initialize-LocalVmMultimon.ps1 -VmRoot X:\PowerToysUiTestVm -WhatIf
pwsh .github\skills\ui-tests-local-vm\scripts\Initialize-LocalVmMultimon.ps1 -VmRoot X:\PowerToysUiTestVm
```

Build `multimon-baseline` from a guest whose standard user has completed the first sign-in. A guest
restored from `provisioned-baseline` can still be finishing it, and Windows then opens Start once,
which the checkpoint would keep. After restoring `provisioned-baseline`, restart the guest once with
`Stop-LocalVm.ps1` and `Start-LocalVm.ps1 -Wait`. The script refuses with `BLOCKED` before the
checkpoint when `Get-SessionWindows.ps1` finds Start or Search open, and sends no keys to close it.

It copies the guest scripts and payloads to `C:\PowerToysUiTestRun\Multimon` and installs the
driver over PowerShell Direct (`Install-IddSampleDriver.ps1`). In the standard user's desktop it
then sets the pointer baseline, applies the preset, and records the layout. Before it checkpoints,
it checks that the captured layout has the monitors `layout-applied.json` lists, with the synthetic
display as primary, and that none of its guest tasks still runs. Evidence lands in
`evidence\multimon\<preset>` under the VM root: `cursorwrap_monitor_layout.json`,
`monitor_ids.txt`, `layout-applied.json`, and `desktop.png`, plus a `<script>.log` with every
output stream and a `<script>.exitcode` for each guest script. Each run deletes and rebuilds that
folder, so `-EvidenceRoot` accepts only a folder inside `evidence\multimon`.

`Multimon.CheckpointName` must differ from `BaselineCheckpointName`, because
`Reset-LocalVm.ps1 -CreateBaseline` replaces a checkpoint of the same name. With
`Multimon.DisconnectNetwork = $true`, a failed checkpoint reconnects the network adapters.

Each guest script runs through `Invoke-LocalVmInteractive.ps1` as a limited interactive scheduled
task, started through `conhost.exe --headless` so the task asks for no console window. The headless
console host always exits 0, so the script's own exit code travels in the `.exitcode` file instead.
Pass `-ShowConsole` to `Invoke-LocalVmInteractive.ps1` to watch a script in a visible window.
Task Scheduler ends only the console host when it stops a task, so after every run the wrapper
also ends the run's processes that are still attached to it, then copies the output back and
unregisters the task. Runs that wait share the `out` folder and must not overlap; a `-NoWait` run
keeps its files in `diag`.

A guest task that starts `pwsh` directly gets a console, which a Windows 11 guest hands to Windows
Terminal, so a terminal window covers the primary display and appears in `desktop.png`. When a
window shows up that nobody opened, pass `-DiagnoseWindows` to `Initialize-LocalVmMultimon.ps1`. It
writes `windows-NN-<step>.json` snapshots before the first step and after each one, plus
`diag\windows-watch.json` from a watcher that samples every 250 ms across the whole run. Both record
every visible window and every console, terminal, and shell process in the session, with owning
process, command line, parent chain, and creation time. They also record the Start and Search
windows, which `EnumWindows` does not return, with `StartOpen` and `SearchOpen` flags that read
visible and uncloaked as open. A Terminal window has class
`CASCADIA_HOSTING_WINDOW_CLASS`, and the watcher's first-seen time places it inside one step.

Every installation step reads the current state first, so a rerun after a failure is safe. The host
copies its unsigned build to `driver-source` in the guest, and the guest signs a copy in `driver`. A
rerun with the same build keeps that signed copy and leaves a working device untouched. When
Windows asks for a restart after the driver install, the device waits at problem code 14 and the
script stops. Restart the guest with `Stop-LocalVm.ps1` and `Start-LocalVm.ps1 -Wait`, then rerun.

### Problem code 52

Code 52 means Windows rejected the driver signature even with the guest-local certificate trusted.
The script stops and changes nothing. User-mode IddCx drivers signed with a locally trusted
certificate are expected to load with Secure Boot on. If this guest refuses, the choices are test
signing (`Set-VMFirmware -EnableSecureBoot Off` with the guest off, then
`bcdedit /set testsigning on` in the guest) or a driver signed by a publicly trusted certificate.
Both are decisions for a person, not for the script.

## 3. Presets

The unmodified sample creates three monitors. Its first monitor reports a `DEL` EDID with
2560x1440, 1920x1080, and 1024x768 modes. Its second reports `LEN` with 3840x2160, 1600x900, and
1024x768. Its third has no EDID and offers 1920x1080. The `-Preset` parameter of
`Set-MonitorLayout.ps1` defines the presets, and `Initialize-LocalVmMultimon.ps1` reads its list
from there. Each preset but `Single` attaches two of the monitors next to the 1920x1080 primary:

| Preset | DEL monitor | LEN monitor | EDID-less monitor | Exercises |
|---|---|---|---|---|
| `SideBySide` | 1920x1080 at (1920,0) | detached | 1920x1080 at (-1920,0) | two full vertical shared edges |
| `Stacked` | 1920x1080 at (0,-1080) | detached | 1920x1080 at (0,1080) | horizontal shared edges |
| `Offset` | 1920x1080 at (1920,540) | detached | 1920x1080 at (-1920,-300) | partial shared edges and exposed corners |
| `MixedDpi` | 2560x1440 at 150%, at (1920,0) | 3840x2160 at 200%, at (0,-2160) | detached | edges between monitors of different scale |
| `Single` | detached | detached | detached | one-monitor behavior |

Positions are physical pixels. Windows may move a monitor to close a gap, so
`cursorwrap_monitor_layout.json` is the truth, not this table.

Each attached monitor gets a solid wallpaper from the 75% color bars of ITU-R BT.471-1, with the
colors picked per monitor count so neighboring monitors contrast; `Multimon.WallpaperPath` replaces
them with your own images in file-name order.

Switch presets on a running guest by running `Set-MonitorLayout.ps1` through
`Invoke-LocalVmInteractive.ps1`, which also exercises a module's display-change handling.
`-ScriptArguments` takes one element per token, and each value reaches the script as a literal. The
task runs as the configured standard user, so pass the names from `vm.config.psd1`:

```pwsh
$config = Import-PowerShellDataFile X:\PowerToysUiTestVm\vm.config.psd1
& .github\skills\ui-tests-local-vm\scripts\Invoke-LocalVmInteractive.ps1 `
  -VmName $config.VmName `
  -StandardUser $config.StandardUser `
  -ScriptPath .github\skills\ui-tests-local-vm\templates\multimon\Set-MonitorLayout.ps1 `
  -ScriptArguments '-Preset', 'Offset' `
  -HostOutputRoot X:\PowerToysUiTestVm\evidence\multimon\offset
```

Switch to `Single` to run one-monitor tests in the multimon guest without restoring
`provisioned-baseline`. `Initialize-LocalVmMultimon.ps1` does not offer it, because its checkpoint
would hold one monitor.

Per-monitor scale has no documented API. `Set-MonitorLayout.ps1` uses the
`DisplayConfigSetDeviceInfo` request the Settings app uses and fails when `GetDpiForMonitor` reports
a different scale. It also fails when the attached monitors differ from the preset's, or when the
primary is not at (0,0) at 1920x1080.

## 4. Input in the guest

`Set-PointerBaseline.ps1` sets pointer speed 10 and turns acceleration off, so a relative
`SendInput` move of N mickeys moves the cursor N pixels. `Watch-LowLevelMouse.ps1` records what a
`WH_MOUSE_LL` hook receives from each input source. That shows whether VMConnect, RDP, and
`SendInput` set `LLMHF_INJECTED` and whether `SetCursorPos` reaches the hook. Neither replaces a
real mouse on real hardware: device polling, acceleration curves, and touchpads stay untested here.

## 5. DPI awareness in guest scripts

Positions, sizes, and screenshots in this layer are physical pixels, so every guest script runs on a
per-monitor-aware thread. `Invoke-LocalVmInteractive.ps1` runs `Enable-PerMonitorDpiAwareness.ps1`
on the task's thread before each script, which covers upstream `Capture-MonitorLayout.ps1` too, and
fails the task when Windows refuses. `Set-MonitorLayout.ps1` and `Save-VirtualScreenshot.ps1` run it
again and refuse to work unaware, so a direct `pwsh -File` run is covered. `Get-SessionWindows.ps1`
records the awareness it got instead of refusing, and `Initialize-LocalVmMultimon.ps1` runs it with
`-DpiReportOnly`, so the diagnostics still run in a session that refuses. A process-wide request is not enough. `pwsh`
replays a per-user startup JIT profile, and once any `pwsh` of that user has driven UI Automation,
every later one starts system aware and Windows refuses the process request. A guest script that
drives UI Automation must call `[System.Runtime.ProfileOptimization]::StartProfile($null)` before
loading it, which stops the recording and leaves later scripts unaffected. That call does not undo a
profile already recorded: delete `$env:LOCALAPPDATA\Microsoft\PowerShell\StartupProfileData-NonInteractive`
for that user, or restore the checkpoint.

## 6. Reset

```pwsh
pwsh X:\PowerToysUiTestVm\Reset-LocalVm.ps1 -Restore -CheckpointName multimon-baseline -StartAfterRestore
```

The standard checkpoint includes memory, so the restore returns to the arranged, logged-on desktop.
