# File Locksmith — module verification profile

**PT module**: `File Locksmith` (shows which processes are using selected files/dirs and lets you kill them)
**Source**: `<PT-repo>\src\modules\FileLocksmith\` (PT repo)
**Settings file (module)**: `%LOCALAPPDATA%\Microsoft\PowerToys\File Locksmith\settings.json` (`{"properties":{"bool_show_extended_menu":{...}}}`) and `file-locksmith-settings.json` (`{"showInExtendedContextMenu":bool}`)
**Enable flag**: `%LOCALAPPDATA%\Microsoft\PowerToys\settings.json` → `enabled."File Locksmith"` (general settings; runner-owned)
**Logs**: `%LOCALAPPDATA%\Microsoft\PowerToys\File Locksmith\FileLocksmithUI\Logs\…`
**Exes**: UI = `%LOCALAPPDATA%\PowerToys\WinUI3Apps\PowerToys.FileLocksmithUI.exe`; CLI = `%LOCALAPPDATA%\PowerToys\FileLocksmithCLI.exe`
**Context menu**: Win11 packaged `IExplorerCommand` CLSID `{AAF1E27D-4976-49C2-8895-AAFA743C0A7E}` (sparse pkg `Microsoft.PowerToys.FileLocksmithContextMenu`); legacy `FileLocksmithExt.dll`. Caption resource = "What's using this file?".
**Named Event / DSC**: no Named Event. DSC resource `microsoft.powertoys.FileLocksmith.settings` exists (controls module settings, not the master enable flag).
**No global hotkey** — entry is the Explorer context menu only.

## The two back-doors that make this module fully drivable (no Explorer needed)

### 1. `FileLocksmithCLI.exe` — deterministic engine ground-truth (PREFER for assertions)
Accepts paths as args; `--json`, `--kill`, `--wait`, `--timeout`. Uses the **same** `find_processes_recursive` engine as the UI.
```powershell
$cli = "$env:LOCALAPPDATA\PowerToys\FileLocksmithCLI.exe"
& $cli "<file|dir|drive>" --json | ConvertFrom-Json   # {processes:[{pid,name,user,files[]}]}
& $cli "<path>" --kill                                  # terminate lockers (== End task)
```
Detection = open **File handles** + **loaded modules** under the path (exact file, exact dir, dir-prefix recursive). `FileLocksmith.cpp:18-113`.

### 2. `last-run.log` IPC + launch UI — exercises the REAL UI code path
The context-menu handler writes selected paths to `…\File Locksmith\last-run.log` then launches the UI; the UI reads them in `MainViewModel()` ctor. Reproduce it:
```powershell
# UTF-16LE, each path + WCHAR \n, trailing empty-line terminator, NO BOM
function Write-LastRun([string[]]$Paths){
  $f="$env:LOCALAPPDATA\Microsoft\PowerToys\File Locksmith\last-run.log"; $ms=[IO.MemoryStream]::new()
  foreach($p in $Paths){$b=[Text.Encoding]::Unicode.GetBytes($p);$ms.Write($b,0,$b.Length);$n=[Text.Encoding]::Unicode.GetBytes("`n");$ms.Write($n,0,$n.Length)}
  $n=[Text.Encoding]::Unicode.GetBytes("`n");$ms.Write($n,0,$n.Length);[IO.File]::WriteAllBytes($f,$ms.ToArray())
}
Write-LastRun @("C:\path\to\file"); Start-Process "$env:LOCALAPPDATA\PowerToys\WinUI3Apps\PowerToys.FileLocksmithUI.exe"
```
Source: `ExplorerCommand.cpp:182-227`, `dllmain.cpp:94-159`, `IPC.cpp`, `NativeMethods.cpp:62-97`.

### UI selectors (winapp ui)
- Window title: `Administrator: File Locksmith` (elevated) vs `File Locksmith` (non-elevated).
- Header button = the path label (`btn-<pathname>-…`); top-right `btn-…` = **Reload** (tooltip "Reload").
- Per-row End task button = `btn-…` (parent of `lbl-endtask-…`); invoke it (`InvokePattern`).
- `RestartAsAdminBtn` = shield icon, **visible only when non-elevated** (`MainPage.xaml:72-82`).
- `ProcessesListView` is virtualized; use `winapp ui scroll ProcessesListView --direction down/up`.

## Recipes — a control/observation map, NOT a per-test-case answer key

> Maps each capability to **how to drive it** and **where the result shows**. No canned process counts / paths / assertions — design those at runtime from the actual checklist item.

| # | Capability | Drive (entry-path / control) | Observe (where the result shows) |
|---|---|---|---|
| 1 | A locked file lists all its locking processes | CLI + UI on one locked file (with multiple lockers) | each locker shows as a ListItem |
| 2 | "End task" kills the locker and de-lists it | `winapp ui invoke` the End-task button | locker PID dies + row removed |
| 3 | Reload rediscovers a locker started after the UI opened | start a new locker → invoke Reload | the new locker appears |
| 4 | Closing a locker externally auto-removes it | external `Stop-Process` on a locker | auto-delisted via `WatchProcess`; empty state shown |
| 5 | Directory path finds lockers recursively | CLI/UI with a directory path | lockers inside the tree are listed |
| 6 | Drive root lists many lockers without crashing | CLI/UI with a drive root | large list renders; no crash |
| 7 | Non-elevated FL does NOT see the elevated runner | run CLI/UI non-elevated via scheduled task `RunLevel Limited` | `PowerToys.exe` absent (medium-IL FL can't see elevated procs) |
| 8 | "Restart as administrator" surfaces elevated-only lockers | non-elev UI shows the button; elevated run shows them | elevated run lists `PowerToys.exe` (UAC consent click NOT automatable) |
| 9 | Scrolling a large list doesn't crash | UI on a drive root + `winapp ui scroll` | process alive + responsive after scroll |
| 10 | Disabling FL removes the Explorer context-menu entry | Settings toggle Off (winapp ui) | `enabled."File Locksmith"`→false; `GetState→ECS_HIDDEN` (source) |

> **Mapping process**: read the actual checklist item → identify the capability → find its row → drive the named control and design your own inputs + assertions. If no row matches, drive ad-hoc and add a row (capability + control + observation point; no canned inputs).

## Common BLOCKED traps (avoid)
- **Don't BLOCK the lock-detection / End-task / scroll-doesn't-crash items as "needs a real installer / right-click"** — both the CLI and the `last-run.log`+UI back-door fully drive them with any locked file. The context menu literally just writes `last-run.log` + launches the same exe.
- **Launching the UI from an elevated shell makes it elevated** (title "Administrator: …") and hides the `RestartAsAdminBtn`. To test the non-elevated case (Recipes 7-8), launch via a **scheduled task with `-RunLevel Limited` / LogonType Interactive** — it lands on the user's desktop at medium IL.
- **`set-value` does fire the Reload** here (TogglePattern/Invoke work); no TextChanged gotchas.

## Elevation semantics (non-elevated FL invisibility — Recipes 7-8 core)
A medium-IL File Locksmith can't `DuplicateHandle`/read modules of higher-integrity processes, so the **elevated PowerToys.exe runner is invisible** to a non-elevated FL and **visible** to an elevated FL (which also calls `SetDebugPrivilege`, `App.xaml.cs:53-61`). Per-user installs put PowerToys under `%LOCALAPPDATA%\PowerToys`, not "Program Files" — use the PT install dir as the stand-in and note the caveat.

## Context-menu disable gate (Recipe 10)
`GetEnabled()` reads general `enabled."File Locksmith"` (`Settings.cpp:53-77`). When false: Win11 `GetState→ECS_HIDDEN` (`dllmain.cpp:81-84`); legacy `QueryContextMenu→E_FAIL` (`ExplorerCommand.cpp:116-119`). Disabling does NOT unregister the sparse package (stays `Status Ok`) — the entry is hidden dynamically. The packaged `IExplorerCommand` is **not** enumerable via Shell `Verbs()` and **not** `CoCreate`-able from a non-Explorer host (`REGDB_E_CLASSNOTREGISTERED`), so the pixel-level render is the only un-automatable bit (`BLK-VISUAL-RENDER` if you need it).

## Fixture files needed
None pre-canned. Create a temp file and lock it with a helper process (pwsh holding `File.Open(path, OpenOrCreate, Read, ReadWrite)` so multiple lockers coexist).

## Source citations
- `FileLocksmithLibInterop\FileLocksmith.cpp:18-113` — `find_processes_recursive` (handles + modules, recursive).
- `FileLocksmithLibInterop\NativeMethods.cpp:62-140` — `ReadPathsFromFile`, `StartAsElevated` (runas/--elevated).
- `FileLocksmithLib\IPC.cpp`, `Constants.h` — last-run.log format/path.
- `FileLocksmithUI\…\MainViewModel.cs:80-183` — load/EndTask/WatchProcess/RestartElevated.
- `FileLocksmithUI\…\MainPage.xaml` — control layout + RestartAsAdminBtn visibility.
- `FileLocksmithExt\ExplorerCommand.cpp`, `FileLocksmithContextMenu\dllmain.cpp`, `FileLocksmithLib\Settings.cpp` — enable gate.

## Ceiling
10/10 PASS observed (2026-06-08). The lock-detection / End-task / refresh / drive-scroll items cleanly driven; the Restart-as-admin item PASS-with-caveat (UAC consent click not automatable; outcome verified). **The disable-removes-menu item PASS — behaviorally verified** by a real Explorer right-click: with FL enabled the Win11 menu shows `MenuItem "Unlock with File Locksmith"`; after disabling in Settings the same right-click menu no longer shows it (no Explorer restart needed — `GetState` re-reads `enabled."File Locksmith"` live). NB the **shipped caption is "Unlock with File Locksmith"**, not the checklist's "What's using this file?". The right-click test needs an **unlocked interactive desktop** (a 4-hour idle auto-lock makes `GetForegroundWindow()=0` → `BLK-ENV`).

## Real right-click verification (Recipe 10) — works on an unlocked desktop
**Use the shared flow: `references/explorer-context-menu-flow.md` + `scripts/pt-explorer-contextmenu.ps1`.** FL's caption is **"Unlock with File Locksmith"**. Quick version:
```powershell
. "$skill\scripts\pt-explorer-contextmenu.ps1"
$menu = Open-PtExplorerContextMenu -ExplorerHwnd $hwnd -FileName 'target.txt'   # synthetic right-click (+retry)
# present/absent assertion:
(Get-PtContextMenuItems -MenuHwnd $menu) -contains 'Unlock with File Locksmith'  # true enabled / false disabled
# real launch (UIA invoke by name):
Invoke-PtContextMenuItem -MenuHwnd $menu -ItemName 'Unlock with File Locksmith'  # -> launches PowerToys.FileLocksmithUI.exe (non-elevated)
# Toggle FL off in Settings, re-open menu, assert the caption is gone. No Explorer restart needed (GetState re-reads live).
```

## Don'ts
- Don't expect `Shell.Application.Verbs()` to show the FL entry — it's a Win11 packaged command, invisible to classic verbs.
- Don't kill processes by name; use `Stop-Process -Id <pid>`.
- Don't forget to restore `enabled."File Locksmith"=true` and close test-spawned UI/Settings after the disable-removes-menu test.
