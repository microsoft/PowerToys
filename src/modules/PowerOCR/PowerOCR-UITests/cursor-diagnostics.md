# PowerOCR cursor flicker diagnostics

This opt-in trace helps distinguish XAML pointer routing, native window message handling, and the cursor Windows actually displays. It observes cursor state without setting the cursor or changing message handling; it does not claim to fix flicker.

Use the updated binary from this branch. Restart the running PowerToys/PowerOCR process when replacing its binary. The marker below is checked whenever an overlay opens, so changing the marker alone does not require restarting Runner.

## Enable tracing

Run in PowerShell:

```powershell
$cursorLogRoot = Join-Path $env:LOCALAPPDATA 'Microsoft\PowerToys\TextExtractor\Logs'
New-Item -ItemType Directory -Path $cursorLogRoot -Force | Out-Null
New-Item -ItemType File -Path (Join-Path $cursorLogRoot 'cursor-diagnostics.enabled') -Force | Out-Null
```

Each overlay records for at most 60 seconds, or until it closes. Closing it with **Esc** saves the trace asynchronously; the 60-second limit also triggers saving. Allow a moment for the file to appear:

```text
%LOCALAPPDATA%\Microsoft\PowerToys\TextExtractor\Logs\<version>\CursorTrace_<utc>_<pid>_<hwnd>.log
```

There is one file per overlay, including separate files for multiple monitors. The ordinary PowerOCR log records the trace path and any save failure.

The current diagnostic revision writes `format=2` on the first line. If a new trace still says `format=1`, stop the old process and run the updated binary. The marker and output directory are unchanged.

## Rejected experiment: removing the transparent backdrop

The 2026-09-08 manual test removed `TransparentTintBackdrop` to isolate its extra compositor and native transparency setup. The user observed a new white flash when the overlay opened, and the cursor still switched. The change was reverted: retain the transparent backdrop. The screenshot does not cover every frame during window startup, so removing native transparency can expose the default window background before the screenshot is rendered.

This test rejects backdrop removal as a mitigation; it does not establish the exact SDK hit-test failure condition. The confirmed evidence remains the independent input thread's `CursorManager::RecalculateLiftedCursorOwner` path issuing Arrow requests while the island cursor stays Cross. Any SDK-version comparison must use complete, matching dependencies and a separate output directory rather than replacing DLLs in shared `WinUI3Apps` output.

## Isolated Windows App SDK comparison

[Cursor-SdkComparison.ps1](Cursor-SdkComparison.ps1) prepares two copies of the current PowerOCR application source, using SDK 2.2.0 and 2.4.0. Both retain `TransparentTintBackdrop` and the same diagnostic code. SDK 2.4.0 is a candidate for comparison; its [release notes](https://learn.microsoft.com/en-us/windows/apps/windows-app-sdk/release-notes/windows-app-sdk-2-0?pivots=stable) do not establish that this cursor issue is fixed.

Run these commands from the repository root in PowerShell 7. `Prepare` copies source only; `Restore` runs only the repository wrapper's solution restore path. Neither command builds the application.

```powershell
$comparisonScript = '.\src\modules\PowerOCR\PowerOCR-UITests\Cursor-SdkComparison.ps1'
& $comparisonScript -Mode Prepare
& $comparisonScript -Mode Restore -SdkVersion 2.2.0
& $comparisonScript -Mode Restore -SdkVersion 2.4.0
& $comparisonScript -Mode Status -SdkVersion 2.2.0
& $comparisonScript -Mode Status -SdkVersion 2.4.0
```

`Status` returns the absolute solution, project, and output paths under `artifacts/PowerOcrCursorSdkComparison/<session>/<SDK>/`. Each copy has its own restore, XAML generation, intermediate, and output directories. The SDK 2.4 copy overrides the central SDK, Foundation, AI, and Runtime versions together. Existing non-SDK project outputs are reused with `BuildProjectReferences=false`; keep those outputs and the common repository configuration unchanged between tests. Preparing a new pair is necessary after changing application source or shared dependencies.

The snapshot's local `Directory.Build.targets` preserves Debug/x64 for references outside the small solution, including transitive references. It also passes the original repository `SolutionDir` to reference queries so native WinMD paths resolve to the baseline output. Without these settings, VS can unset the platform and resolve dependencies under `AnyCPU` or `bin/Debug`, while GPOWrapper resolves relative to the comparison solution. Successful NuGet restore alone does not verify these paths.

The pair also copies the inherited `src/.editorconfig` above each snapshot's `project/` directory. This preserves the original analyzer settings, including `dotnet_code_quality.ca1051.exclude_structs = true` for native interop structures such as `OSInterop.RECT`. Omitting it changes the build policy even when the C# source is identical. The original configuration, each copy, and their frozen hashes are verified together.

For a pair prepared before these configurations were added, update both copies in place, then close and reopen the comparison solution in VS to load the new files:

```powershell
& $comparisonScript -Mode Repair -SdkVersion 2.2.0
& $comparisonScript -Mode Repair -SdkVersion 2.4.0
```

Repair updates the snapshot's reference and inherited analyzer configurations; it does not restore or build. A reference-only MSBuild check can use `ResolveProjectReferences` with `BuildProjectReferences=false`, `BuildingInsideVisualStudio=true`, and the comparison's `SolutionDir`, then inspect `_ResolvedProjectReferencePaths` and each `ReferenceAssembly` for existence, and `EditorConfigFiles` for the inherited configuration. This exercises VS reference resolution without compiling C# or XAML; it is not a substitute for the subsequent user build.

For Visual Studio testing:

1. Exit Runner and the old PowerOCR process. Open the **comparison solution** returned by Status, starting with 2.2.0. Use **Debug / x64**, set PowerOCR as the startup project, and select its ordinary **PowerOCR** launch profile. Leave the native cursor tracepoints disabled for this comparison.
2. Build the comparison project in VS. After it succeeds, check the output payload with `& $comparisonScript -Mode Verify -SdkVersion 2.2.0`, then press F5. The original solution still uses the original SDK; launching that solution does not test the comparison copy.
3. Use the activation shortcut. Check startup for a white flash, move rapidly in the canvas center for 10–15 seconds, pause, and then test a selection drag. Press Esc to cancel; allow the trace to save before stopping debugging.
4. Stop that process, open the 2.4.0 comparison solution, and repeat with `-SdkVersion 2.4.0` for verification. Report white-flash and cursor-flicker observations separately for each version.

Alternatively, these explicit commands build only the chosen application using the repository wrapper, verify the output, and launch it:

```powershell
& $comparisonScript -Mode Build -SdkVersion 2.4.0
& $comparisonScript -Mode Run -SdkVersion 2.4.0
```

Restore verification checks the exact SDK package graph, source snapshot hashes, and original protected-file hashes. Output verification additionally checks the input/composition/XAML native DLLs against their restored NuGet payloads using SHA256; matching file version labels alone are insufficient. Reports are saved as `restore-verification.json` and `verification.json` beside the comparison solution. A failed verification invalidates the comparison and must be investigated before interpreting its result.

Cursor traces use the existing marker and log directory. The `app-base` header identifies the comparison output used by that process; `window-host` should show `TransparentTintBackdrop`. A stable 2.4 trace is evidence for an SDK-dependent mitigation on the tested configuration, not proof of the precise private hit-test failure. If both versions still flicker, retain the restored backdrop and continue from the native input-owner evidence.

### Result from the 2026-09-08 comparison

The SDK 2.4.0 run still reproduced flicker. Its trace identified the isolated 2.4 output directory and retained `TransparentTintBackdrop`; all seven checked SDK native files matched their restored package payloads. The process had already exited when inspected, so this check verifies the output files rather than a live loaded-module map.

In the main-display trace for PID 54224, 13 retained global Arrow samples occurred over the overlay with the latest XAML source being SelectionCanvas and the latest input-source cursor being Cross. Four occurred while the left button was down and WinUI reported one capture. At 8085.580 ms, the Cross input observation was 6.437 ms old; the global cursor was Arrow and recovered to Cross by the sample at 8101.073 ms. Cursor notifications originated from TID 25944, distinct from UI TID 14520. These are sampled observations, not an exact flicker count or a new native setter stack. The 2.2 and 2.4 runs used different durations and actions, so they cannot establish a comparative failure rate.

The SDK update is therefore not a demonstrated mitigation on this machine. Keep the existing SDK comparison results as evidence; further investigation should identify the native cursor-owner decision or isolate the input surface.

### How other modules own the selection cursor

| Module | Selection surface and cursor handling | Relevance to PowerOCR |
| --- | --- | --- |
| Screen Ruler | `MeasureToolCore/OverlayUI.cpp` creates a separate native HWND per display with `IDC_CROSS` as its window-class cursor and Direct2D rendering. The WinUI toolbar occupies a separate HWND; its rectangle is excluded from the native overlay region. | This avoids making the full-screen selection area a WinUI ContentIsland. Its architecture is the useful comparison, rather than its toolbar's window styling. |
| Screen Ruler modes | Bounds mode hides the system cursor while dragging, then restores it. Spacing mode hides the system cursor and draws measurement lines. There is no per-movement `SetCursor` loop or `SetCapture` implementation in MeasureToolCore. | Visible measurement lines and a hidden system cursor do not demonstrate hardware Cross stability. Hiding the pointer would also change PowerOCR's interaction. |
| Crop And Lock | `CropAndLock/OverlayWindow.cpp` creates a native overlay HWND and chooses Arrow or Cross in `WM_SETCURSOR`. | A useful native overlay reference. Adding that handler to the current WinUI window alone does not prevent a separate thread's direct `SetCursor` call. |
| Color Picker | `ColorPickerUI/Mouse/CursorManager.cs` changes the user's Arrow/IBeam cursor paths and reloads system cursors for the picking session, then restores them. | An Arrow fallback can still look like the picker cursor. This changes desktop-wide cursor appearance and does not preserve PowerOCR's canvas/toolbar distinction. |
| Earlier WPF PowerOCR | Before the WinUI migration, `OCROverlay.xaml` assigned Cross on the selection canvas, with the toolbar as its sibling. Mouse capture and clipping were used during selection. | The current canvas already maintains the equivalent Cross assignment and capture. Repeating those assignments is not the missing mechanism shown by the traces. |

The strongest implementation reference is Screen Ruler's separation of a native selection surface from a WinUI toolbar. A small prototype should first verify that ownership boundary with a real system Cross cursor and unchanged screenshot/startup behavior; only then should it replace the existing selection host. The current evidence does not establish that replacing the host will necessarily fix this SDK behavior.

## Native selection surface prototype

The current original PowerOCR project includes an opt-in native selection surface. It uses a dedicated STA thread and unowned Win32 HWND for each display, with a real system Cross cursor and native mouse capture. An opaque screenshot and dimmed frame are prepared before showing the native window; the existing WinUI backdrop remains in place. GDI draws a one-pixel white selection outline on the native surface, coalescing redraws in `WM_PAINT`. The WinUI toolbar and InfoBar remain accessible through excluded regions, and popup/processing UI temporarily exposes the underlying WinUI window. Selection results use the existing OCR/clipboard service. The prototype leaves the SDK version unchanged.

The HWND deliberately has no WinUI owner: cross-thread ownership [implicitly attaches input queues](https://devblogs.microsoft.com/oldnewthing/20130412-00/?p=4683). WinUI explicitly coordinates closing and native z-order after toolbar activation. The ready log records the actual `GW_OWNER` value, expected to be zero. This removes that ownership relationship from the comparison; it does not prove that every SDK input-state dependency is isolated.

The first native test reported stable movement and no startup white flash, but a white flash on Escape. In all six captured runs, exit samples briefly returned from the native HWND to its underlying WinUI host before that host disappeared. The trace does not record pixels, but this sequence can expose XAML/backdrop teardown. Closing now hides all displays' WinUI hosts before disposing any native surface or closing the WinUI windows. Pending native callbacks stop updating visibility during that phase, and a native Alt+F4 request waits for the same coordinated close path. The native window also hides before destroying its HWND and drawing resources.

Use the **updated original PowerOCR project**, not either earlier SDK comparison snapshot. Those copies predate this prototype. Enable it from the repository root in PowerShell 7:

```powershell
.\src\modules\PowerOCR\PowerOCR-UITests\Cursor-NativeOverlay.ps1 -Mode Enable
```

This creates the native-prototype marker and enables the existing cursor trace marker. Build the original PowerOCR project in VS using Debug/x64, then F5 with the ordinary PowerOCR launch profile and no arguments. Exit Runner and any old PowerOCR process first. Use the configured Text Extractor shortcut to open an overlay.

The normal PowerOCR log should contain `PowerOCR native selection: ready`, including the native HWND, owner, thread, monitor bounds, and actual client geometry. Each native window also starts an opt-in `NativeCursorTrace_<utc>_<pid>_<hwnd>.log` in the normal versioned log directory. Its header identifies the original app output and native HWND. Collection lasts at most 60 seconds or until the window closes. Esc closes the overlay; wait briefly for trace saving before stopping VS debugging.

Test after the native surface is ready:

1. Check repeated opening for white or black flashes, then rapidly move and pause in the canvas center. Verify the actual system Cross remains visible.
2. Drag across the toolbar's former position and release. The native surface covers its toolbar hole while dragging. Check the selection rectangle and OCR result; repeat with Shift held to translate the selection.
3. Move between canvas and toolbar, open/close the language dropdown and right-click menu, and confirm the native selection surface returns afterward. Try Tab from the native surface to move keyboard focus to the toolbar.
4. Repeat on another monitor, including different DPI and negative display coordinates. Check that toolbar holes and right-click placement align.
5. Cancel an active drag with Esc and with Alt+F4. Check that all overlays close without a white flash and the cursor can move freely across the desktop; repeat opening and closing quickly. Repeat after an OCR error, while using the toolbar, and with both monitors enabled. A popup may consume the first Escape; check that the next Escape dismisses the overlay cleanly.

`NativeCursorTrace` records distinguish `own-native` from `toolbar-or-other`. Arrow is expected on toolbar controls; inspect `ownSurfaceVisibleArrowSamples` for Arrow sampled over the native selection window. The count is the number of observations, not a flicker count. Compare `maxSampleGapMs` and retained/overwritten records; polling can miss transitions shorter than its actual interval. The trace does not itself identify a setter call stack.

For a WinUI control run using the same binary, close existing overlays and disable only the prototype:

```powershell
.\src\modules\PowerOCR\PowerOCR-UITests\Cursor-NativeOverlay.ps1 -Mode Disable
```

The next activation uses the existing WinUI canvas again; cursor tracing remains enabled. `-Mode Status` reports both markers. This is a mouse-focused diagnostic prototype: native input, popup interaction, OCR recovery, multi-monitor cleanup, touch, and accessibility still require acceptance testing before treating it as a product change.

## Start directly from Visual Studio

1. Exit any running PowerToys Runner and stop any old `PowerToys.PowerOCR.exe` instance. Closing an overlay with Esc leaves its process running; the single-instance mutex makes a second PowerOCR process exit immediately. Leaving Runner active can launch its installed copy when the shortcut is pressed.
2. Set the `PowerOCR` application project as the startup project, use the desired configuration/platform, and leave command-line arguments empty. A numeric argument selects Runner-managed mode instead of the standalone keyboard listener.
3. Enable the marker above, then press F5 after the project compiles successfully. Starting the process does not immediately display an overlay.
4. Press your configured Text Extractor activation shortcut (default **Win+Shift+T**). Opening the overlay starts collection. If you customized the shortcut in Settings, use that combination.
5. Reproduce, then press Esc to close the overlay. Wait for the trace file to appear, or for `PowerOCR cursor trace saved` in the ordinary log/VS Debug output, before stopping debugging. VS Stop Debugging can terminate the process before the background writer saves the trace.

You can keep the same debugging session running and press the activation shortcut again for another trace.

## Capture native setter call stacks in Visual Studio

When global cursor state changes while `InputPointerSource.Cursor` stays Cross, use [Cursor-NativeTrace.ps1](Cursor-NativeTrace.ps1) to capture the actual native function-entry stacks. This procedure uses the installed Visual Studio instance, supports **x64**, and does not build or launch PowerOCR itself.

The launch profile is local configuration: this repository ignores `Properties/launchSettings.json`, and the tracepoint script does not create it. On a fresh checkout, create `src/modules/PowerOCR/PowerOCR/Properties/launchSettings.json` with the following profiles. If that file already exists, merge the native profile into its `profiles` object while preserving your other profiles:

```json
{
  "profiles": {
    "PowerOCR": {
      "commandName": "Project"
    },
    "PowerOCR (native cursor trace)": {
      "commandName": "Project",
      "nativeDebugging": true
    }
  }
}
```

1. Stop the old debugging session and select the **Debug / x64** solution configuration. From the repository root run:

   ```powershell
   .\src\modules\PowerOCR\PowerOCR-UITests\Cursor-NativeTrace.ps1 -Mode Setup
   ```

2. Select **PowerOCR (native cursor trace)** in the launch-profile dropdown beside the VS Start button, then press F5. This profile requests native debugging. Before reproducing, run the script with `-Mode Status` while the process is running and confirm both `BoundLocations` values are at least 1. CoreCLR-only debugging cannot bind these native tracepoints. If needed, detach from the process and reattach with the **Native** code type in VS, then check binding again; do not infer native attachment from the profile name alone. The script creates two tracepoints, `user32.dll!SetCursor` and `win32u.dll!NtUserSetCursor`, filtered to PowerOCR and the current system Arrow handle. Their Actions print PID, native TID, tick count, cursor argument and `$CALLSTACK`, then continue automatically.
3. Open the overlay with the configured shortcut. Reproduce for **about five seconds**, then press Esc. This is a manual capture window: Esc closes the overlay but does not disable the persistent tracepoints. Tracepoint processing can slow execution; use these stacks for attribution, not timing measurements.
4. **Keep the PowerOCR process/debug session running after Esc** and save before starting another session:

   ```powershell
   .\src\modules\PowerOCR\PowerOCR-UITests\Cursor-NativeTrace.ps1 -Mode Save
   ```

   Save disables this script's tracepoints, exports only the current capture session's marked blocks from the VS Debug output, and records the running PowerOCR module paths, versions, base addresses and sizes. Files are written to `%LOCALAPPDATA%\Microsoft\PowerToys\TextExtractor\CursorNativeStacks\CursorNativeStacks_*.log` and the accompanying `.modules.json`. This directory is beside `Logs`, so the application's old-version log cleanup cannot delete the capture state. Module bases allow unsymbolized addresses to be resolved later; do not discard that file. If PowerOCR has already exited, stack export can still work, but the module map may be unavailable.

5. After saving, stop debugging normally. `-Mode Status` lists the tracepoint state; `-Mode Remove` deletes only tracepoints tagged by this script. Run Setup again to start a new capture session. If using a different VS major version, pass its automation ProgID, for example `-VisualStudioProgId VisualStudio.DTE.17.0`.

This does not set the cursor or change its return value. The two APIs can record the same call at different layers. A stack at an Arrow request establishes who made the request, not whether Windows ultimately displayed it. Correlate its PID/TID with the regular `CursorTrace` records. VS tracepoint syntax is documented in [Using tracepoints](https://learn.microsoft.com/en-us/visualstudio/debugger/using-tracepoints?view=visualstudio).

## Reproduce in separate runs

Open a fresh PowerOCR overlay for each run. Spend about 10–15 seconds on each, note whether flicker occurred, then close with **Esc**.

1. **Canvas center, no button pressed:** move the mouse rapidly well away from the toolbar and display edges, pause for 2–3 seconds, then move again. Note whether the cursor also changes while stationary.
2. **Toolbar boundary:** repeatedly move onto the toolbar and back onto the selection canvas. Note whether flicker also occurs after returning to the canvas center.
3. **Selection drag:** hold the left mouse button while moving rapidly and compare with the first run. Press **Esc while still holding the button** to cancel without starting OCR, then release the button.

If available, repeat across two monitors with different DPI settings. Include the display arrangement/scaling, which runs flickered, and all trace files from those runs.

List the newest traces in PowerShell:

```powershell
$cursorLogRoot = Join-Path $env:LOCALAPPDATA 'Microsoft\PowerToys\TextExtractor\Logs'
Get-ChildItem -LiteralPath $cursorLogRoot -Filter 'CursorTrace_*.log' -File -Recurse |
    Sort-Object LastWriteTime -Descending |
    Select-Object LastWriteTime, FullName
```

## Read the evidence

- **`native-window` metadata:** maps each observed `hwnd` to its overlay `root`, window `class`, owner `tid`/`pid`, registered `classCursor`, and subclass status. `skipped-other-thread` means this observer could not inspect that child's message dispatch.
- **`probe-health` / `native-probe-health`:** checks each attached same-thread HWND at initialization, Loaded, the first pointer event, and approximately once per second. `membership` checks the subclass registration, while `initialWndproc`/`currentWndproc` identify procedure changes. If the loaded comctl32 does not expose `GetWindowSubclass`, that optional query is disabled once per process and `membership=unavailable`; the independent WNDPROC and heartbeat checks still run. A registered private message with an integer token tests callback reachability; `heartbeatReceived=True` proves that probe message reached the callback. It does not prove every input message follows the same path. The check does not reinstall or reorder subclasses.
- **`input-source-status` / `input-source`:** after the first XAML pointer event, looks up the existing island containing the canvas visual and its WinUI input source. `sourceCursor` reads `InputPointerSource.Cursor`; `canvasCrossAssigned` reads the canvas property. The trace also observes movement, enter/exit, routing and capture-lost events from that source. A 100 ms UI timer reads the cursor during pauses. All these objects belong to WinUI and are only borrowed, not disposed or modified.
- **`cursor-event-status` / `cursor-event`:** an out-of-context WinEvent hook listens for `EVENT_OBJECT_NAMECHANGE` on `OBJID_CURSOR`. `eventTid`/`eventPid` identify the notification's source thread and its owner process (PID 0 means unknown); `callbackTid` identifies the receiving thread. `eventTimeMs`, `callbackQpc`, `observedQpc` and `deliveryDelayMs` distinguish event generation from later handling. `callbackCursor` is read during the callback and may already differ from the cursor that triggered the notification. `lastInput`/`inputAgeMs` and `lastUi`/`uiAgeMs` are earlier observations, not simultaneous hit tests. The source thread is not a call stack or guaranteed identification of a particular cursor-setting function.
- **`canvas` and `pointer`:** show whether the canvas has its cross cursor assigned, the routed event's `source`/`canvasSource`, `captures`, button state, `captureHwnd`, and the UI thread's current cursor. `crossAssigned=True` describes the canvas property; it is not proof that Windows displayed a cross at that instant.
- **`native`:** records `before`/`after` cursor handles around forwarding a native message, its `result`, HWND, `startQpc`, and `durationMs`. For `WM_SETCURSOR`, `hitTest` is the signed low word of `lParam`; `trigger` is the high word. A `Cross` to `Arrow` change narrows the search to handling of that message on that HWND, including nested dispatch.
- **`sample`:** independently reads global `GetCursorInfo`, reporting `globalCursor`, visibility `flags`, screen position, `underHwnd`/`underRoot`, owner thread/process, foreground window, the most recently recorded `lastUi` event, and `lastInput` cursor state with its age. This helps check whether a visible transition coincides with another window or routing target.
- **`summary` and header:** include sample counts, observed changes, native message/change counts, `inputEvents`, `cursorEvents`, `maxSampleGapMs`, and retained/overwritten entry counts. `cursorEvents` counts received cursor notifications even when the callback's current position was outside this overlay's display. `cleanupCompleted=True` means the UI cleanup method finished; inspect failure records to check whether each native removal succeeded. At automatic expiry the background sampler waits up to two seconds for UI cleanup, then saves even if cleanup is still pending. Compare rows using `tMs` within a trace or `qpc` across overlays; callbacks and sampling can append rows out of timestamp order.

Observed native message codes:

| Code | Message |
| --- | --- |
| `0x0020` | `WM_SETCURSOR` |
| `0x0084` | `WM_NCHITTEST` |
| `0x0200`, `0x00A0` | `WM_MOUSEMOVE`, `WM_NCMOUSEMOVE` |
| `0x0245`, `0x0249`, `0x024A` | `WM_POINTERUPDATE`, `WM_POINTERENTER`, `WM_POINTERLEAVE` |
| `0x0006`, `0x0007`, `0x0008` | `WM_ACTIVATE`, `WM_SETFOCUS`, `WM_KILLFOCUS` |
| `0x0215`, `0x02A3` | `WM_CAPTURECHANGED`, `WM_MOUSELEAVE` |

The mouse-up/drag comparison matters because Windows sends `WM_SETCURSOR` for mouse movement when mouse input is not captured. Default processing can delegate to a parent and select a class cursor or an arrow. See [Microsoft's WM_SETCURSOR documentation](https://learn.microsoft.com/en-us/windows/win32/menurc/wm-setcursor).

The trace retains the most recent 16,000 entries in memory. Repetitive unchanged movement/native messages are throttled to 50 ms; observed native cursor changes are retained. Global sampling requests a 2 ms interval, but scheduling can make it slower: inspect `maxSampleGapMs`. `Cross`/`Arrow` labels compare handles with standard system cursors; `Other` is retained as a raw handle.

`lastUi` is an earlier routed event, not a simultaneous hit test; check `uiAgeMs`. Identical native `before`/`after` values can hide a cursor that changes and changes back during dispatch. Very short global transitions can occur between samples. Cross-thread child windows are listed but not subclassed. A trace without an observed transition therefore does not prove stability or identify every possible cursor setter.

For the second diagnostic round, first inspect `probe-health` to establish whether the native listener remained reachable. Then compare `sourceCursor`, global samples and `cursor-event` source threads. A stable Cross input source with an Arrow global cursor points to a different layer than an input source that itself changes to Arrow. Because notifications and sampling are asynchronous, use multiple observations before assigning a cause. See [ContentIsland.GetByVisual](https://learn.microsoft.com/en-us/windows/windows-app-sdk/api/winrt/microsoft.ui.content.contentisland.getbyvisual?view=windows-app-sdk-1.8), [InputPointerSource.GetForIsland](https://learn.microsoft.com/en-us/windows/windows-app-sdk/api/winrt/microsoft.ui.input.inputpointersource.getforisland?view=windows-app-sdk-1.8), and [SetWinEventHook](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-setwineventhook).

## Clipboard persistence failures

The 2026-09-08 logs recorded two `COMException` failures with HRESULT `0x800401D0` (`CLIPBRD_E_CANT_OPEN`), both at `Clipboard.Flush()` after `SetContent` returned successfully. The original service did not retry. This can mean text was already placed on the clipboard but could not yet be made independent of the application's lifetime.

The service now writes once and retries only a busy `Flush`, at most five attempts with 50 ms asynchronous waits. Continuations retain the caller's UI synchronization context. Session cancellation is checked before writing, before each flush, and during waits, so Escape stops later attempts. SetContent failures, non-busy exceptions, and exhausted retries still use the existing ClipboardFailed path. Successful recovery logs `Clipboard flush succeeded after N attempts` without logging extracted text.

`ClipboardWriteOperationTests` links the production retry policy into the Core unit-test project and uses fake delegates; its tests do not read or overwrite the system clipboard.

## Disable tracing

Close existing overlays, then remove only the marker. Existing traces remain available.

```powershell
$cursorLogRoot = Join-Path $env:LOCALAPPDATA 'Microsoft\PowerToys\TextExtractor\Logs'
Remove-Item -LiteralPath (Join-Path $cursorLogRoot 'cursor-diagnostics.enabled') -ErrorAction SilentlyContinue
```
