# Seamless Multi-Screen Cursor

Moves the pointer between monitors at the same physical position, even when the monitors differ in resolution, size
or physical offset. Windows maps a border crossing by pixel coordinate only, so the pointer jumps when monitors have
different pixel pitches. The user aligns marker lines across each shared border once; the module then remaps every
crossing.

Design and decisions: [spec](../../../specs/mouse-utils-seamless-multi-screen-cursor.md).

## Code structure

| Path | Purpose |
|------|---------|
| [SeamlessCursor](/src/modules/MouseUtils/SeamlessCursor) | Module DLL `PowerToys.SeamlessCursor.dll`, loaded by the runner |
| [SeamlessCursorEngine.h/.cpp](/src/modules/MouseUtils/SeamlessCursor/SeamlessCursorEngine.cpp) | Pure mapping logic: shared borders, calibration validation, edge segments, crossing resolution |
| [MouseHookThread.h/.cpp](/src/modules/MouseUtils/SeamlessCursor/MouseHookThread.cpp) | `WH_MOUSE_LL` hook, display-change window, watchdog, ordered reload/preview/suspend requests |
| [CalibrationOverlay.h/.cpp](/src/modules/MouseUtils/SeamlessCursor/CalibrationOverlay.cpp) | One Direct2D window per monitor where the user drags the marker lines |
| [CalibrationStore.h/.cpp](/src/modules/MouseUtils/SeamlessCursor/CalibrationStore.cpp) | `calibration.json` load, save and delete; `overlay.json` load and save |
| [Resources.resx](/src/modules/MouseUtils/SeamlessCursor/Resources.resx) | Localizable strings; the build converts them, with `SeamlessCursor.base.rc` and `resource.base.h`, into `Generated Files` |
| [Monitors.h/.cpp](/src/modules/MouseUtils/SeamlessCursor/Monitors.cpp) | Monitor rectangles, stable IDs (`QueryDisplayConfig` device path) and physical size |
| [EdgeClaims.h/.cpp](/src/modules/MouseUtils/SeamlessCursor/EdgeClaims.cpp) | `SeamlessCursor_IsEdgeClaimed` and `SeamlessCursor_MapWrap` exports used by CursorWrap |
| [UnitTests](/src/modules/MouseUtils/SeamlessCursor/UnitTests) | Engine tests (`SeamlessCursor.UnitTests.dll`) |
| [OverlaySnapshots](/src/modules/MouseUtils/SeamlessCursor/OverlaySnapshots), [OverlaySnapshot.h](/src/modules/MouseUtils/SeamlessCursor/OverlaySnapshot.h) | Developer tool that renders the calibration overlay for made-up monitor layouts into PNG files |
| [MouseUtilsPage.xaml](/src/settings-ui/Settings.UI/SettingsXAML/Views/MouseUtilsPage.xaml), [MouseUtilsViewModel.cs](/src/settings-ui/Settings.UI/ViewModels/MouseUtilsViewModel.cs) | Settings: enable toggle, Calibrate and Reset buttons, "Stop the pointer where no monitor is physically across" and "Keep the physical position when CursorWrap wraps the pointer" toggles, notices |
| [CalibrateSeamlessCursorCommand.cs](/src/modules/cmdpal/ext/Microsoft.CmdPal.Ext.PowerToys/Commands/MouseUtils/CalibrateSeamlessCursorCommand.cs) | Command Palette "Calibrate Seamless Multi-Screen Cursor" |

## Threads

All coordinates are physical pixels; every module thread is Per-Monitor V2 DPI aware.

- **Hook thread:** owns the hook, the engine and a hidden top-level window for `WM_DISPLAYCHANGE`, `WM_DEVICECHANGE`
  and `WM_SETTINGCHANGE`. It does nothing else, so mouse input is never delayed. The hook is installed only while at
  least one usable border is calibrated.
- **Overlay thread:** exists only while the calibration overlay is open.
- **Calibrate listener:** waits for `SEAMLESS_CURSOR_CALIBRATE_EVENT` (set by Command Palette) and opens the overlay.

Requests from other threads (reload, preview while adjusting, suspend, options) are queued and applied on the hook
thread in order. Lines being tried while adjusting survive layout rebuilds until the overlay closes.

## How a crossing is remapped

1. On each `WM_MOUSEMOVE`, the hook takes the cursor position before the move (`GetPhysicalCursorPos`) and the
   requested position from `MSLLHOOKSTRUCT`. The requested position can lie beyond an outer desktop edge; Windows
   clamps it only after the hook runs.
2. If the requested point is still on the source monitor, nothing happens.
3. Otherwise the engine finds the edge crossed first and looks up the segment of that edge. Each segment maps the
   coordinate along the edge linearly to the target monitor. The scale and offset come from the two aligned marker
   pairs. Pixels map through their centers. Through a corner, a monitor that Windows connects without calibration
   is left to Windows, and a wall on one edge does not hide a crossing of the other.
4. The hook calls `SetPhysicalCursorPos` with the mapped point and swallows the original move.

Walls exist only on edges with a calibrated border, never where Windows connects an uncalibrated monitor, and they
depend on the `stop_at_empty_edges` setting (on by default; Settings and the overlay's "Stop at empty edges" switch
both change it, and the module saves the overlay's change to its settings file). With it on, where the calibration
says nothing is physically across, the engine returns a point on the source monitor instead (a wall): the cursor stops
at the edge and slides along it. This also applies to outer parts of edges that have a calibrated border, because
Windows' "Ease cursor movement between displays" would otherwise move the cursor to a nearby monitor. At such outer
walls the hook calls the next hooks first (CursorWrap, Mouse Without Borders) and stops the cursor only if none of
them handled the move. With the setting off, the cursor goes to the nearest corner of the monitor Windows connects
there, and Windows decides at outer edges.

Moves pass through unchanged for pen and touch input, for injected jumps of at least 128 px (automation and
remote-control tools), while another app confines the cursor (`ClipCursor`), while the cursor is hidden, and from a
display change until the layout is rebuilt.

## Calibration data

`%LOCALAPPDATA%\Microsoft\PowerToys\SeamlessCursor\calibration.json`, written only by the module (Settings only
reads it). Overlay preferences ("Try while adjusting", "Pointer trail", "Edge map") are in `overlay.json` next to it.
Marker positions are stored as fractions of each monitor's edge, keyed by monitor device path, so they survive
re-arranging monitors in Windows display settings and resolution changes. A record applies while its monitors are
connected in the stored orientation and either share that border or face each other across a gap on that side; when
they face each other across a gap instead of sharing the border, it acts as a glued pair.

Saving replaces only the records of the borders on screen in their current orientation. Malformed entries are skipped
on load. A file that cannot be parsed counts as empty and is copied to `calibration.json.bak` before a save replaces
it; when that copy fails, or while another program holds the file, the save fails. The hook thread reads the file when
it starts and on Reload (whenever the overlay closes, or after a reset). When another program holds the file, it keeps
the calibration read before and reads the file again on later rebuilds, which are retried with a growing delay, as
are rebuilds while Windows cannot tell the monitors' device paths; unlocking the session retries 500 ms later. Unlike
the hook thread, the overlay waits about a second for the file when it opens; if it still cannot read it, it shows
predicted lines, says at once that saving is not possible, dims Save, and refuses to save over the stored ones.

## Gluing

Windows cannot always make two monitors touch, for example two monitors standing on a wide one. `FindGlueCandidates`
finds pairs that face each other across a gap with nothing in between. Both monitors of such a pair show a
"Glue to <other monitor>" switch; a glued pair is calibrated and remapped like a shared border, except that only its
physical overlap is remapped, because Windows does not connect the two monitors anywhere.

## CursorWrap

CursorWrap wraps the cursor at outer desktop edges. Seamless also acts on outer edges where a monitor is physically
adjacent. Before wrapping, CursorWrap calls `SeamlessCursor_IsEdgeClaimed` and skips claimed parts of edges (only
crossings are claimed, not walls), so the result does not depend on which hook runs first. The facing edges of a glued
pair are claimed too, so CursorWrap does not wrap there.

When CursorWrap does wrap to another monitor, it asks `SeamlessCursor_MapWrap` for the coordinate on the target monitor
at the same physical position. Seamless answers when a chain of calibrated borders on edges of the wrap's orientation
links the two monitors, and the `map_cursor_wrap` setting is on; CursorWrap otherwise keeps the coordinate. When the
position lies off the monitor CursorWrap picked, CursorWrap takes the outermost of its other outer edges of that side
where the position lands, which can be the source monitor's own far edge (a glued pair across a wide gap). When none
fits, the pointer stays on the picked edge, 2 px in from its nearer end. The landing point always keeps 2 px clear of
the ends of the edge, which CursorWrap would take for the adjacent edge. When CursorWrap picks the source monitor
itself, Seamless is not asked.

At a corner where a wall meets an outer edge, CursorWrap picks its monitor from the requested point, which can be the
monitor across the wall.

## Testing and debugging

- Unit tests: build `src/modules/MouseUtils/SeamlessCursor/UnitTests/SeamlessCursorUnitTests.vcxproj` and run
  `x64\Debug\tests\SeamlessCursor\SeamlessCursor.UnitTests.dll` with `vstest.console.exe`.
- Overlay snapshots: build
  `src/modules/MouseUtils/SeamlessCursor/OverlaySnapshots/SeamlessCursorOverlaySnapshots.vcxproj` and run
  `x64\Debug\tests\SeamlessCursor\SeamlessCursor.OverlaySnapshots.exe <output folder>`. It draws the real overlay code,
  without windows, for made-up layouts that are hard to set up (mixed orientation and scale, a staircase, a monitor on
  every side, a tiny primary display, gaps, a remote session, and a pointer trail with a crossing and a CursorWrap
  wrap). For each layout it writes an overview of all monitors and the primary monitor at a readable size. Compare the
  images before and after a change to the overlay; add a layout in `main.cpp` to check another one. See its
  [README](/src/modules/MouseUtils/SeamlessCursor/OverlaySnapshots/README.md).
- Logs: `%LOCALAPPDATA%\Microsoft\PowerToys\SeamlessCursor\ModuleInterface\Logs`. Each layout rebuild logs the
  monitor count and whether remapping is active.
- Reset calibration: Settings → Reset calibration, or delete `calibration.json` and toggle the module.

## Known limitations

- While an elevated window has focus and PowerToys is not elevated, Windows sends the hook no input, so remapping
  pauses.
- Mouse Without Borders may switch machines at outer edges that Seamless takes over.
- Tools that inject absolute positions in small steps (remote desktop and VNC clients, Mouse Without Borders on the
  controlled machine) undo the remap with their next position, so the cursor flickers for one step at a calibrated
  crossing.
- Moving a monitor's cable to another port changes its device path, so that border needs recalibration.
