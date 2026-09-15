# Try Run (experimental prototype)

Run Windows applications and Windows/Linux scripts through MXC, inspect the
output, and keep selected results separately from the original files. This is an opt-in developer
prototype, not an installed PowerToy or a security boundary. MXC currently warns
that its profiles are not security boundaries.

## Milestones

Complete and validate each milestone before starting the next.

1. **Execution foundation — implemented and validated**: standalone window, Windows/privilege checks, isolated
   worker using the MXC C# SDK, bounded output, timeout and explicit stop; unit tests
   and a trusted-script smoke test.
2. **File workflow — implemented and validated**: copy selected input into a temporary workspace, reject links
   and oversized inputs, compare results, export to a new directory, clean up;
   filesystem unit tests and fuzz targets.
3. **Windows and Linux workloads — implemented, native tests passing**: Windows EXE, PowerShell and
   batch profiles through ProcessContainer; Linux scripts and executables through
   WSLC; image preparation, backend discovery, and shared file review/export.
4. **Selection-driven tasks — implemented, tests and startup UI flow passing**: drag/drop or startup paths,
   read-only entry point detection, copied Windows application bundles, nested
   working folders, and a simpler run summary with optional advanced controls.
5. **Explorer entry — implemented, registration and native tests passing**: a per-user classic
   menu verb passes the entire selection through an out-of-process COM helper
   to the same task workflow; it does not inspect files inside Explorer.
6. **Environment and isolation report — implemented and validated**:
   pre-run summary, native block-mode denial capture, attributed observations,
   original-file hash verification, and Windows/Linux demonstration tasks.
7. **Windows window borders — implemented, native integration verified**: cyan, noninteractive borders for the
   running Windows process and observable descendants; track window geometry,
   visibility and session lifetime without marking ordinary instances.
8. **Two-step workflow — implemented**: separate configuration and run/results
   pages, an immediately accessible EXE chooser, persistent previous results,
   and image preparation logs kept with environment setup.
9. **Configurable policies — implemented**: the full public
   policy and backend configuration surface used by the pinned MXC .NET Windows
   ProcessContainer and Linux WSLC paths. See [Run permissions](POLICY-OPTIONS.md)
   for every option, default and backend limitation. Session lifecycle and
   additional backends remain separate work.
10. **Command Palette entry — not started**: an explicitly enabled developer command opens the
   window, without changing ordinary Run or existing module settings; build and
   launch validation.

Installer, Runner/GPO registration, automatic write-back, multi-call session
lifecycle, and additional backend entry points are outside this
prototype. Production integration needs the normal PowerToys dependency, signing,
privacy and security reviews and resolution of MXC's preview limitations.

Before running, select **Run permissions…** to edit the default policy. All
44 top-level controls and nested network-rule fields are documented in
[Run permissions](POLICY-OPTIONS.md), including backend-specific availability.
The descriptions of offline execution below refer to the default configuration.
Explicit writable host grants allow changes to originals, and permissive capture
allows ungranted access; the configuration page and run report identify those
choices. Changing permissions does not execute the selected program.

## Dependency

Use the separate Microsoft MXC checkout at commit
`4a941b0b` (the full revision is recorded in `mxc-version.txt`). Pass its absolute
root as `MxcRoot` when building the worker. The managed SDK and native libraries
are built together; do not mix releases. Rust 1.93 and the Windows C++ build tools
are required. No Node runtime is required. See the root `NOTICE.md` entry.

The PowerShell profile uses the Windows system runtime with
`-NoProfile -NonInteractive`. The application profile runs the selected EXE,
and batch scripts use the system `cmd.exe`. Linux profiles use a dedicated MXC
WSLC container and a prepared image; they do not execute in a user's existing
WSL distribution. It never runs a workload directly when MXC is
missing or unavailable. Only Windows 11 24H2+ is supported. Elevated sessions
are refused. Script text and output are not sent to telemetry.

PowerShell requires Windows UI initialization. The default policy therefore allows
windows and uses MXC's documented `Desktop` UI compatibility setting; clipboard,
input injection, system-settings changes and desktop system control stay blocked.
The window permissions are shown before execution. This is not a no-GUI policy.

## Validation record

### Configurable permissions

The x64 Debug standalone build passed. The full run in **policy-full.trx**
passed all 124 tests with no skips, including the previously failing
Windows native-denial demo and the border raster test. This records a successful
run with the new build; it does not establish the cause of the older capture
discrepancy described below. The native build retains its existing Windows Python
Store-alias advisory; Linux Python uses the prepared image.

The policy tests verify every catalog field has a default and a form control,
round-trip and reject malformed policy requests, exercise 2,007 policy-protocol
fuzz inputs, preserve nested network-rule details, inspect the mapped SDK request,
and run Windows/Linux workloads with customized read-only grants and environment
variables. They also verify the policy time limit and preserve backend drafts and
previous-result snapshots.

After preserving whitespace in environment values and correcting the EXE-import
hint for customized permissions, the final build passed and all 17 targeted
checks in **policy-final-regression.trx** passed. This includes an additional
real native allow-mode capture run of the trusted Windows findstr utility against
an owned read-only fixture; its report never labels the permitted read as blocked.

The Explorer entry was updated to **x64/Debug/TryRun-Policies** and the registered
COM-to-window test passed. Interactive screenshot acceptance could not launch the
window because the desktop tool returned **GetCursorPos failed: Access is denied
(0x80070005)** on both attempts. WPF control/default coverage and real two-step
window workflows passed in the tests; a visual walkthrough remains pending.

### Configuration followed by run and review

Try Run opens on **Prepare your run**. A standalone launch defaults to
**Installed app / inline script** and **Windows · Application (.exe)**, with
**Choose file…** immediately visible. Explorer selections open the same setup
page. A single EXE selects **Installed app / inline script**; other selections use
**Selected files / folder** with an automatically detected profile.
Input files, the execution profile, Linux image/runtime, and time limit are
configuration controls. Advanced options contain arguments, a local image
archive, and denial-capture settings; choosing an EXE no longer requires them.

**Run** validates the configuration before switching to the separate run page.
It shows preparation/start/run/review stages and the terminal outcome, followed
by **Output**, **Files**, and **Isolation report** tabs. Completion leaves the
selected tab in place. Back navigation is disabled while work is active. Once
finished, **Back to configuration** retains output, file review/export state,
the actual run environment and status. **View previous run** reopens them.
Starting another run still invokes the existing unexported-results confirmation.
Editing the next configuration does not relabel the previous run.

Image preparation stays on the configuration page with a separate log and
cancel control. It never clears a previous workload's results. File drops are
accepted only on the configuration page. The Run button stays disabled until a
program, script or copied entry point has been selected.

`WorkflowTests` covers initial EXE access, Explorer selection without execution,
validation errors, page transitions through a real MXC run, retained unexported
results, failed image preparation, reruns, and failed/stopped/timed-out outcomes.

The x64 Debug build succeeded. All six workflow tests passed, and interactive
acceptance covered EXE selection, the transition to a running/completed page,
back navigation, and a Windows demo's output, file comparison and report tabs.
The full run (`two-step-full.trx`) passed 105 of 106 tests with no skips,
including the previously unavailable border raster test. The remaining
`WindowsDemoReportsBlockedProbeAndUnchangedOriginals` check failed because MXC
returned a completed capture containing zero native denial records. The demo
still reported the host-only read as blocked/unavailable, and both selected
original files were unchanged. The failure reproduced in isolated reruns; an
older `TryRun-Explorer` worker passed the comparison. This capture discrepancy
remains unresolved. Its assertion and the execution/capture policy are retained.

The single-EXE import fix built with no errors or warnings. Targeted workflow,
file-safety, selection and fuzz checks passed 41 of 42 cases on the first run
(`exe-selection-import.trx`). The new native application test supplied arguments
on one line instead of using the UI's one-argument-per-line format; correcting
that test made the remaining case pass (`exe-selection-run-recheck.trx`). The
checks cover hard-linked EXEs, application replacement with retained data,
unchanged configuration after failed imports, copied folders/multiple files,
Explorer EXE startup, actual MXC execution, and ignored drops during runs/results.
The full native suite, including the known capture discrepancy above, was not
rerun for this UI-only fix.
Interactive acceptance of Explorer's **Show more options → Try Run** for
`C:\Windows\System32\winver.exe` opened the installed-application configuration
with Run enabled and no import error. Cross-window mouse dragging could not be
automated because the desktop tool only permits drag endpoints in its target
window; the shared drop/Explorer selection handler is covered by the tests.

### Windows window borders

Windows application windows receive a four-DIP cyan border while their Try Run
session is active. The Worker announces the MXC root PID and Windows creation
time only after spawn succeeds and policy warnings have been checked. The UI
opens that exact live process, discovers descendants from Windows process
snapshots, and checks parent/child creation and exit times. Retained process
handles prevent a stale identity from being accepted merely by executable name
or PID. No window title or workload output is used for association.

The border is a separate transparent WPF window: it does not modify or inject
code into the application. It is nonactivating, absent from Alt+Tab/taskbar, and
click-through, including over the resize edges. Visible frame bounds use native
physical coordinates, with WPF scaling the stroke for DPI. Each border is placed
immediately above its target, below covering windows, rather than globally
topmost. Hidden, minimized and DWM-cloaked windows are excluded. Movement is
checked every 100 ms and process discovery every 500 ms while a run is active;
tracking is bounded to 256 processes and 32 visible windows.

Stop, normal exit, timeout, worker failure and closing Try Run remove the borders.
The marker associates a window with a Try Run session; it is not an additional
security boundary or a certification of the application. Linux GUI remains
unsupported. Windows handed off to an existing unrelated process/broker are not
marked. Very short-lived intermediate parents can escape snapshot discovery;
unverified windows are left unmarked rather than guessed from their names.

`WindowBorderTests` exercises stale identities, the live Worker identity, a real
MXC `winver.exe`, a GUI child launched through PowerShell, and an ordinary
`winver.exe` alongside each run. It checks movement/resizing, z-order, input/focus
styles, minimize/restore, and stop/normal-close/timeout cleanup. Set
`POWERTOYS_TRYRUN_GUI_TESTS=1` for the native GUI cases. Manual acceptance also
needs mixed-DPI, multiple-monitor coverage before production integration.

The separate raster test first renders a solid-color control. If even that
control is empty (as observed in the earlier unavailable desktop capture
session), the test reports **inconclusive**, rather than claiming that the
border's pixels passed. Reconnect/unlock an interactive desktop to rerun
`BorderPaintsCyanEdgesAndLeavesTheApplicationVisible` and visually inspect the
border. This does not skip the native association and lifecycle tests.

The x64 Debug solution build succeeded. The full regression run passed 99 tests
with the single raster test inconclusive (`window-borders-validated.trx`). The
local deliverable and registered Explorer entry use `x64/Debug/TryRun-Borders`.
Topmost/non-topmost transitions are also covered: the marker follows the target's
band and never becomes topmost merely because an unrelated topmost window is
immediately above the target.

Interactive acceptance on 2026-09-15 also verified the cyan frame around
`winver.exe` on the desktop, normal interaction with its OK button, and return
to the completed run page after the application closed.

### Run environment and isolation report

The run page summarizes the selected runtime/backend, writable copies,
read-only installation folder (custom installed-app mode), network policy,
time limit, Linux image and resource limits, and capture availability.
The **Isolation report** tab retains the actual request's environment snapshot
so editing the next task does not relabel evidence from the previous run.
Windows has no additional CPU/memory cap; Linux requests 2 CPUs and 2 GiB.
These are configured restrictions, not proof that every attempted operation
was observed or a certification that arbitrary software is safe.

**Record blocked access with MXC** is enabled by default in the UI. It adds
`ProcessContainerContainment.CaptureDenials` with `Mode=Block` and
`RetainEtl=false` only when MXC's native capture probe succeeds. The pinned probe
starts/stops native PSEC/V2 capture; it does not advertise the elevated guarded
WPR path. The fixed Try Run policy uses neither LPAC, a network proxy, nor denied
path overrides, so it is compatible with that native capture path. This utility
does not enable permissive audit mode or automatically retry with broader grants.
Unavailable native capture is shown as **Not collected** and ordinary restricted
execution remains available. A capture/report failure remains visible; missing
or empty diagnostics never become a claim that nothing was attempted.

The report separates evidence sources:

- **MXC denial capture (block)**: native denied resources and access types.
- **Workload file (self-reported)**: optional `tryrun-observations.json` in the
  working folder. These may be supplied or modified by the workload, including
  pre-existing input content; their claims are not independently trusted.
- **Host verification**: the demo's harmless host-only fixture is checked before
  and after execution. Selected original input file hashes are also rechecked
  by the UI after the run. This verifies those file contents, not the whole host;
  changes by another host process are not attributed to MXC.

Native documents are kept in a session-owned Diagnostics sibling outside the
Work/Temp grants and are removed with the session. Reads reject paths outside
the expected directory, links, junctions, hard links, and oversized files. Native
JSON is capped at 4 MiB with at most 100 displayed denials; truncation is explicit.
Workload observations are capped at 32 KiB, 20 rows and 512 characters per field,
and cannot override their source label. A capture-finalization error may retain
an MXC-owned ETL file outside the session; its location is shown for diagnosis.
Stop allows a bounded five-second report-drain period. If finalization cannot
finish, the UI marks diagnostics incomplete while keeping file review available.

Choose **Windows isolation demo** or **Linux isolation demo**, then **Run**.
The buttons load the corresponding Samples folder and supply a harmless fixture
outside the granted workspace. Each demo reads and modifies a copy of notes.txt,
creates a result, and attempts to read the fixture. The Linux demo additionally
lists network interfaces; that observation is not a complete network test.
Expected results: one modified input, a result file and observation file added,
two original inputs unchanged, and a host fixture unchanged. Inspect **Isolation
report**, then export the desired files through **Review file changes**.
The sample folders also work through Explorer; the host-fixture check is enabled
by the demo buttons, and an ordinary sample-file invocation labels it not tested.

Validation: the standalone x64 Debug build exits 0 and all **94 tests pass**
with no skips (`isolation-report-full.trx`). This host advertises native denial
capture. Both demo integration tests verified generated results, a blocked or
unavailable host-fixture read reported by the script, host-fixture integrity,
unchanged original input hashes, and export of the modified copy. Windows also
returned native blocked-access events. Timeout still produced a finalized report;
the existing Windows/Linux Stop, timeout and Explorer handoff regressions passed.
Report fuzz coverage includes 1,004 valid/mutated inputs, alongside bounded-parser,
source-attribution, BOM, truncation, path-boundary and link-rejection tests.
The actual demo-button → Run → file-review → isolation-report UI flow was checked
for both backends. The Windows UI showed 13 native denials plus separately labeled
workload observations and host verification. Linux showed its self-reported
fixture/interface observations with native denial capture marked Not collected.
Both UI runs completed with exit code 0 and two original input hashes unchanged.

### Explorer context menu

Build the complete standalone solution, then register the built copy for the
current user. Keep a File Explorer window open while running the script:

```powershell
./Set-ExplorerIntegration.ps1 -Action Register -TryRunDirectory 'C:\source\PowerToys\x64\Debug\TryRun-Explorer'
```

Select files or folders in Explorer, right-click, choose **Show more options →
Try Run**. All selected items arrive in one new Try Run window. Resolve multiple
entry points if needed, then click Run. Opening the menu or importing selection
does not run a workload. This milestone intentionally uses the classic menu;
Windows 11's first-level menu needs a separate packaged extension and is not
registered by this prototype.

Use the same script with `-Action Status` to check this build's registration or
`-Action Unregister` to remove the experimental entry. Registration affects only
the Try Run verb under `HKCU\Software\Classes\*\shell` and `Directory\shell`,
and its dedicated CLSID and AppID `{D2B3BC02-CFF8-4A26-94B4-62441A1FF659}`. Unregistering
refuses to delete keys without the Try Run ownership marker. Registering another
build updates this entry; unregister removes the currently registered prototype.
It does not modify file associations, default actions, installer, Runner/GPO,
or system policy, and does not restart Explorer. Keep the registered build in
place; unregister before deleting it.

The script dispatches registration through the desktop Shell object obtained
from a running Explorer window. It does not create a new Shell.Application
object and use that object's ShellExecute directly. The developer host can
present a different registry view: writes and status checks succeeded there,
while Explorer omitted the verb and COM returned `REGDB_E_CLASSNOTREG`.
Registering in the desktop Explorer context resolved automatic COM activation
and made Try Run appear in the Shell's actual menu enumeration.

Registration now returns a correlated receipt from that desktop helper and
verifies activation of the registered `IExecuteCommand` before reporting success.
The receipt uses a generated GUID filename in the helper's build folder and is
removed after reading. It carries registration status only, not selected files.
`Status` and `Unregister` use the same desktop context. Reopen a menu that was
already visible before registration; the script sends an association-change
notification without restarting Explorer.

The helper follows Microsoft's [ExecuteCommand verb sample](https://github.com/microsoft/Windows-classic-samples/blob/main/Samples/Win7Samples/winui/shell/appshellintegration/ExecuteCommandVerb/ExecuteCommandVerb.cpp):
`IExecuteCommand` + `IObjectWithSelection`, `LocalServer32`, `DelegateExecute`,
`MultiSelectModel=Player`. There is no managed or native Try Run DLL loaded into
Explorer. The COM call queues work and returns promptly. A single-use helper
receives the whole `IShellItemArray`, reads only filesystem names, starts the
fixed adjacent UI, and exits. Abandoned activation has a 60-second idle limit.
Each invocation has its own helper and UI, so concurrent selections do not merge.
The command captures the server thread's dispatcher at construction. Managed
COM calls can arrive on RPC pool threads; creating a dispatcher on such a thread
queues work to a message loop that does not exist. Selection state is locked
while accepting a request, and the accepted selection is retained for the
server-thread callback.

The UI receives versioned JSON on redirected standard input, with a fixed
`--selection-stdin` switch. Names are never interpolated into a command, file
association, or temporary manifest. The message is bounded to 200,000 characters,
1,000 paths and 32,768 total path characters, with a 10-second receive timeout.
Malformed, missing, non-local and oversized selections produce an error instead
of a direct-execution fallback. Shell-provided parameters, current directory and
silent-execution hints are ignored. The UI performs the same bounded file
inspection and copying as drag/drop; MXC is loaded only by the execution Worker.

Validation: standalone x64 Debug build exits 0. The original Explorer milestone
passed all 81 tests in `explorer-full.trx`. After the desktop-context fix, all
11 targeted registration/protocol/fuzz regressions passed in
`explorer-desktop-final.trx`, including the added receipt-path test and 753
selection-message seeds/mutations. A desktop-context unregister/status/register
round trip also passed, with the entry left enabled.
Native checks verified automatic COM activation and the expected delegate for
files, directories, `.txt`, and `.ps1`; enumerating the current sample file's
Shell verbs returned **Try Run**.

The click-handoff regression was reproduced in `explorer-click-repro.trx`:
Execute returned success, but no task window appeared. After binding dispatch
to the server thread, all 13 Explorer/protocol/fuzz regressions passed in
`explorer-click-fixed.trx`. The opt-in integration test activates the registered
COM class, sends three real shell items, invokes Execute, and checks that exactly
one task window opens. Its accessibility tree confirmed all three paths arrived.
Run this check with `POWERTOYS_TRYRUN_EXPLORER_TESTS=1` and
`POWERTOYS_TRYRUN_APP` set to the built application's full path. The test leaves
the prepared task window open and never runs its selected scripts.
A separate Windows ShellExecute invocation of the registered verb also opened
Try Run. Full right-click-to-Run mouse automation remains pending; these checks
use the real Windows verb/COM path and inspect the resulting window.

### Select files and run

Drop a single EXE on the configuration page to select it as an installed
application, just like **Choose file…**. This also applies to a single EXE sent
from Explorer. Its application directory is read-only, existing supporting inputs
are retained, and arguments from a previous application are cleared. Windows
components such as `winver.exe` may be hard-linked; this path uses the existing
runtime-file policy instead of trying to import them as copied data. It still
requires an explicit **Run** and uses MXC. A failed selection leaves the previous
application, arguments, input files and mode intact.

Drop a folder, scripts or multiple files on the configuration page, or use
**Add files / Add folder**, to use the copied-input workflow.
One recognized entry point is selected automatically. Multiple candidates require
choosing one; data-only selections require adding a script/program or enabling
**Installed app / inline script** on the configuration page.
Importing never starts the selected code. Select **Run** after reviewing the summary.

Selected folders retain their names and contents. Overlapping parent/child
selections are deduplicated; unselected siblings are never implicitly imported.
Different roots with the same name are rejected with an explanation. A script
or program runs in its copied parent folder, so relative data paths work. All
selected files accompany it. EXEs selected in a folder or with other files run
from the copy and may write alongside themselves; the original application
directory is not granted access.
Include dependencies by selecting them or their containing folder.

Detection reads extensions and at most 4 KiB of each regular file. It recognizes
PowerShell, batch, shell, Python, PE EXEs and ELF candidates, plus simple allowlisted
shell/Python shebangs. It never evaluates a shebang command, reads instructions
from a README, installs dependencies, or invokes a file association. Header
recognition is a suggestion, not a guarantee of compatibility; unusual PE headers,
ELF shared objects and unsupported runtimes may require a custom run. Python is
recommended through Linux; Windows Python can be selected as an installed runtime
in the custom mode. Images must contain the chosen interpreter and dependencies.

The executable also accepts bounded, absolute local file/folder paths as separate
startup arguments (optional leading `--`). Paths import a task; there is no
automatic run switch. For example:

```powershell
& 'C:\source\PowerToys\x64\Debug\TryRun-Selection\PowerToys.TryRun.exe' -- 'C:\Downloads\my package' 'C:\Downloads\data.csv'
```

Command Palette integration is not part of this milestone. Output, errors,
Stop, timeout, before/after review and explicit export
continue to use the same MXC worker and file workflow.

Current selection milestone verification: the standalone x64 Debug solution
builds with exit code 0; all **74 tests pass** (48 unit/fuzz, 18 Windows integration,
8 WSLC integration), with no skipped tests. Results are in
`x64/Debug/tests/TryRun.UnitTests/TestResults/selection-full.trx`. New coverage
includes overlap deduplication, preserved folders, data-only/ambiguous selections,
bounded header and startup parsing, links, limits, copied EXEs, nested Windows
PowerShell/batch tasks, Linux folder tasks, and unchanged original files. The
existing fuzz target now also exercises header detection and launch argument parsing.

The final window build was checked through startup arguments containing
`Samples/windows.ps1` and `Samples/notes.txt`: both inputs appeared, PowerShell was
automatically selected, Run was enabled, and clicking it completed with exit 0.
The review showed one added and one modified file, with correct before/after
previews; the host original stayed unchanged. The empty layout and Add files
dialog were also inspected. Cross-window drag gestures and the full export-dialog
click sequence have not been manually completed; their shared file handling and
export logic are covered by the tests. A transient desktop automation failure
(`foreground window did not report a process id`) interrupted the file picker
walkthrough; reselecting the live startup window allowed the run/review check.

The current deliverable is `x64/Debug/TryRun-Selection/PowerToys.TryRun.exe`.
Its adjacent Worker cache contains the prepared Alpine and Python images.
To try it, add or drop `Samples/windows.ps1` together with `Samples/notes.txt`,
then select Run. For Linux, select `Samples/linux.sh` with `Samples/notes.txt`.
Selecting the entire Samples folder instead demonstrates choosing among multiple
entry points and running with the original folder structure.

### Windows and Linux profiles

For an installed application or inline script, select **Installed app / inline
script**, choose the **Application type** under **Run environment**, and choose a
file or enter a script. Arguments are in **Advanced options** and
are one literal argument per line; surrounding quotes are unnecessary. Choosing
a script file disables the inline editor for that run. Scripts and data are
copied into the workspace; a custom-mode EXE is run from its original location with its
containing directory granted read-only access, so adjacent DLLs/resources remain
available. System execution policy is not bypassed for PowerShell script files.
Batch arguments reject command-expansion characters; put complex batch commands
in the script itself.

Linux supports shell scripts, Python or another image-provided runtime, and
selected Linux executable files. **Script runtime** names the interpreter inside
the image (for example `/bin/sh`, `bash`, `python3`, or `node`). The runtime,
libraries and executable architecture must match the selected image. Host paths
are explicitly mapped to their MXC `/mnt/<drive>/...` paths. Only this run's Work
and Temp folders are mounted. Linux runs request 2 CPUs and 2 GiB, with networking
disabled. WSLC does not offer interactive stdin through this SDK's one-shot API.

**Prepare image** invokes MXC's official `wxc-exec.exe --setup-wslc` helper to
download an image into `Worker/WslcImages`. This is a separate operation with
network access; normal workload runs use that cache with networking off. A local
image archive can also be supplied. Preparing images creates persistent cache
files; running creates temporary data and consumes host CPU/memory. No claim of
zero host impact or universal application compatibility is made. MXC remains a
preview, not a verified security boundary. Installed apps requiring services,
drivers, elevation, package activation, additional runtimes, or writable install
directories may not work in ProcessContainer.

Known compatibility result on the current host: `charmap.exe` exits with code 0
without showing a window under this profile; granting read-only system fonts did
not resolve it. The GUI smoke test uses `winver.exe`, which does show a window
and can be stopped through MXC. The application profile also grants read-only
access to installed system fonts for GUI workloads.

Build the Windows/Linux worker with the matching optional native components:

```powershell
& ..\..\..\tools\build\build.ps1 -Platform x64 -Configuration Debug -Path . -ExtraArgs @('/p:MxcRoot=C:/source/mxc', '/p:MxcWithWslc=true')
```

To build beside an already-open prototype, add
`/p:TryRunOutputRoot=C:/source/PowerToys/x64/Debug/TryRun-Multi` to that argument
array. This changes only the application and Worker destinations; do not use a
global `OutDir` override, which can make shared project outputs remove each
other during incremental builds.

The WSLC-enabled build stages MXC's image helper, WSLC daemon and pinned
Microsoft.WSL.Containers SDK alongside the worker. WSL must already be installed
and compatible (MXC currently requires WSL 2.9.9+). Try Run does not enable Windows
features, update WSL, invoke global host preparation, or run an ordinary WSL
process as fallback. MXC owns the selected backend's permission mechanisms;
its AppContainer fallback can use DACL augmentation. The tested host reports
BaseContainer with no DACL augmentation requirement.
Backend discovery describes the host's capability, not proof that every request
can be enforced. A failed launch retains its MXC error.

For a two-backend demo:

1. Windows application: choose a classic EXE such as `C:\Windows\System32\winver.exe`
   and Run; Stop ends the contained application. Or choose a `.ps1`/`.cmd` file
   using its matching Windows profile.
2. Linux shell: prepare `alpine:3.22`, then run `uname -s; printf linux > result.txt`.
   Review and export `result.txt`.
3. Linux Python: prepare `python:3.12-alpine`, choose a `.py` file, and use `python3`
   as the script runtime. Other languages require an image that contains them.

Opt-in Linux integration tests require the two prepared images and
`POWERTOYS_TRYRUN_WSLC_TESTS=1`. The optional native GUI test requires
`POWERTOYS_TRYRUN_GUI_TESTS=1` and briefly opens the Windows version dialog through MXC.

### File workflow

1. Choose **Add files** or **Add folder**. Files are copied under their original
   names; a folder keeps its top-level name and contents. For example, selecting
   `notes.txt` makes it available as `TryRun:\notes.txt`. Selecting `Documents`
   makes its files available under `TryRun:\Documents\`. Duplicate top-level
   names are rejected rather than renamed or overwritten.
2. Enter a script using relative paths and choose **Run**. Every run gets fresh
   copies; changes to a previous run are not reused. Inputs are opened read-only.
3. Open **Review file changes**. SHA-256 content comparison labels each file as
   added, modified, deleted, or unchanged. Selecting a row shows the before/after
   text, limited to the first 8 KiB. UTF-8 and BOM-marked UTF-16 are supported;
   binary or unsupported encodings show an explanation instead. This is a file
   comparison with side-by-side previews, not a line-level diff editor.
4. New and modified files are checked by default. Check any unchanged files you
   also want, then choose **Export checked files** and a destination parent.
   Export creates a new `TryRun results <timestamp>-<id>` directory, preserves
   relative paths, and never overwrites existing files or applies deletions to
   originals. **Open export folder** opens the completed result location.
5. Export before starting another run or closing the window. A prompt protects
   unexported new/modified results. A failed or timed-out script can still have
   useful partial results; review them before exporting. **Refresh review** can
   retry a stopped review without running the script again.

Input and result scans allow at most 1,000 files/folders, 16 path components,
240 characters per relative path, 32 MiB per file, and 100 MiB total file content.
Links, junctions, hard-linked files, network/device paths, alternate-stream path
syntax, and reserved Windows names are rejected. Directory handles are held
during traversal and copying, and file handles deny writes while content is read.
Export rechecks the reviewed contents before copying. These are import/review/
export limits, not a disk quota on the running script.

Only primary file contents are copied; ACLs, alternate streams and original
timestamps are not carried over. Empty folders are copied on import but do not
have result rows and are not exported. Rename appears as deletion plus addition.
A canceled or failed export may leave partial copies in its newly created
directory; the error identifies it. Try Run never recursively deletes the
user-selected export destination. Unexported session files are cleaned up when
the window closes or a new run begins.

For a quick manual test, create `notes.txt` containing `original`, add it, then run:

```powershell
Set-Content notes.txt changed -NoNewline
Set-Content new.txt created -NoNewline
```

Expect one modified file and one added file, with `original`/`changed` in the
before/after preview. Export both and confirm the selected original still reads
`original`, while the new results folder contains `changed` and `created`.

### Build and tests

Build from this directory using the repository scripts (replace the MXC path):

```powershell
& ..\..\..\tools\build\build.ps1 -Platform x64 -Configuration Debug -Path . -ExtraArgs /p:MxcRoot=C:/source/mxc
```

Run `x64\Debug\TryRun\PowerToys.TryRun.exe` from the repository root. The worker
and its matching native libraries must remain in the adjacent `Worker` directory.
The application does not need to run as administrator.

Build tests first using the command above, then run `vstest.console.exe` from the
same terminal. The integration tests run only when the worker path is supplied:

```powershell
$env:POWERTOYS_TRYRUN_WORKER = 'C:/source/PowerToys/x64/Debug/TryRun/Worker/PowerToys.TryRun.Worker.exe'
& '<Visual Studio>/Common7/IDE/CommonExtensions/Microsoft/TestWindow/vstest.console.exe' '../../../x64/Debug/tests/TryRun.UnitTests/TryRun.UnitTests.dll' /Platform:x64
```

Before enabling Run, the application executes a fixed, harmless workspace
write/read check. MXC platform discovery alone does not validate file grants.
Try Run never prepares the host automatically, never retries scripts with broader
permissions, and exits before running user code if it cannot enter the workspace.

The sandbox starts in a PowerShell drive named `TryRun:\`, rooted at the granted
workspace. Use relative paths, for example `Set-Content result.txt hello` and
`Get-Content result.txt`. This drive exists only inside the child PowerShell
session. It lets PowerShell normalize paths without inspecting ungranted parent
directories such as `C:\Users`.

Sessions resolve their newly created directory through a Windows file handle
before passing paths to MXC. Packaged development hosts can redirect AppData
creation to a package-private directory while reads of an existing parent use a
merged view. Resolving the unique session directory keeps the worker's grant and
the actual files aligned without changing host permissions.

Windows PowerShell may use Constrained Language under local security policy.
Try Run preserves that policy. The smoke tests use normal cmdlets, not unrestricted
.NET method invocation. PowerShell CLIXML diagnostics are decoded as bounded XML
with DTD/entity resolution disabled, and displayed as text.

The fuzz target is built with the solution. Its OneFuzz entry is disabled until
the production fuzzing owner and service configuration are assigned; no remote
fuzzing job is submitted by building this prototype.

Previous dual-backend milestone verification: the standalone x64 Debug solution builds with exit code 0.
All 62 tests pass: 40 unit/fuzz cases, 15 Windows integration cases (including
the optional GUI window/stop test), and 7 WSLC integration cases. Fuzz smoke
coverage includes 2,004 original protocol inputs, 1,506 additional multi-backend
request seeds/mutations, 1,007 path/preview inputs and 48 file round trips.
The native tests
cover the startup workspace probe, file creation and relative navigation,
cross-session write denial, environment isolation, timeout, stop, and bounded
output, the complete file workflow, export of partial results after timeout,
native Windows arguments, PowerShell/batch files, Linux shell/Python/ELF files,
unmounted host files, network-interface isolation, and Linux stop/timeout.
Results are in
`x64/Debug/tests/TryRun.UnitTests/TestResults/multibackend-full.trx`.
The final script-type validation change also passed all 8 targeted protocol,
argument, fuzz and Windows script regressions in `multibackend-input-regression.trx`.
The M2 window was also checked for successful startup (**Ready**, Run enabled),
the input-file dialog, and the review/preview layout. Native file execution and
export are covered by integration tests; the complete sequence of GUI clicks
has not been automated.
The new multi-backend window builds successfully, but its full UI walkthrough
is pending: desktop automation returned `GetCursorPos failed: Access is denied
(0x80070005)` before launch. The native GUI process test passes independently.
The MXC image-helper build emits an existing advisory about a Windows Python
Store alias; Linux Python uses its own prepared image and passed its tests.
The first full PowerToys essentials build is blocked by missing MSVC Spectre
libraries in this development environment; the standalone prototype does not
require that full build. ARM64 has not been validated on hardware.

If an older window reports **Restricted workspace access is unavailable** with
Run disabled, save its script, close it, rebuild, and reopen the application.
For an inline command, select **Installed app / inline script**, choose
**Windows · PowerShell** in **Run environment**, then
try `Set-Content result.txt hello; Get-Content result.txt`; expect `hello` in the
output and exit code 0. Setting the time limit to 2 seconds and running
`Start-Sleep -Seconds 10` should report that the time limit was reached.
