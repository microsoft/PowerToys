# CursorBrake

CursorBrake adds resistance where two monitors meet. The cursor stops at an edge shared by two monitors until the user pushes past it for a set distance, so a fast move toward a target near the edge (a scrollbar, a close button, a taskbar, a browser tab) does not overshoot onto the next monitor. Holding the configured cross key (Ctrl by default, or Shift) crosses freely. CursorBrake never locks the cursor: every edge yields to a long enough push.

## Behavior

- **Inner edges only.** An edge counts when two monitors share it. Outer edges, gaps between monitors and single-monitor setups are left to Windows, including its own corner and dead-zone easing.
- **Push-through.** A move that would land on another monitor across an inner edge is held just inside the source monitor, and its overshoot perpendicular to the edge adds to a push. When the push reaches the threshold, the move crosses and the push resets.
- **DPI scaling.** The threshold is `push_distance * sourceDpi / 96`, using the DPI of the monitor the cursor leaves. Windows moves relative input about `sourceDpi / 96` pixels per mouse count, so the hand travel to cross is the same in both directions across a mixed-DPI edge.
- **Cursor pixels, not hand distance.** The push is measured in the cursor travel Windows produces, after pointer speed and acceleration. Windows does not know a mouse's DPI, so the same strength takes less hand travel on a high-DPI mouse and follows the pointer speed setting. At pointer speed 10 with no acceleration, the default 300 is about 9.5 mm of hand travel at 800 DPI and 4.8 mm at 1600.
- **Move cap.** Each event adds at most 10 logical pixels (scaled the same way) to the push, so a fling or a shake holds at the edge instead of crossing in one or two events.
- **Moving away drains.** The component of a move away from the pushed edge comes off the push, floored at 0. A move along the edge costs nothing. The push belongs to the source monitor and the crossed side, so a hard push into a corner that lands on two neighbors in turn keeps adding up.
- **Free return.** With the setting on, the cursor can cross straight back over the edge it just crossed with no push. The allowance ends 100 logical pixels from that edge, on another crossing, or on a bypassed move.
- **Cursor clip.** When an app confines the cursor with `ClipCursor`, as a full-screen game does, Windows clamps every move into the clip after the low-level hook, which still sees the unclamped point. The engine judges each move between the clamped points, so a clip on one monitor never holds the cursor, and a clip spanning an edge brakes it as usual. With no app's clip, `GetClipCursor` reports the virtual screen's bounding rect, so a move past an outer edge is judged where that rect puts it: a diagonal at an inner corner holds, and an excursion along an outer edge keeps the push. The push and the free return start over when the clip starts or stops letting the cursor through where they lead, as when a game confines or releases the cursor. A failed `GetClipCursor` keeps the last clip.
- **Bypass.** Holding the configured cross key (`cross_modifier`: Ctrl or Shift, never both), or holding a mouse button while "Disable resistance while dragging" is on, crosses freely and resets the push and the free return.
- **Input classification.** Pen and touch input passes. In a remote session the module stands down entirely. Injected input passes, unless `dwExtraInfo` carries the test marker `0x4342524B` and Windows does not flag it as injected from a lower integrity level (`LLMHF_LOWER_IL_INJECTED`). The module brakes such a move like hardware so scripted tests can drive it.
- **Display changes.** The monitor layout is rebuilt on `WM_DISPLAYCHANGE`, `WM_SETTINGCHANGE` with `SPI_SETWORKAREA`, and `WM_DEVICECHANGE`, debounced by 500 ms. A layout or settings change resets all push and free-return state.

## Architecture

CursorBrake is a native C++ in-process module (`PowerToys.CursorBrake.dll`), like the other passive Mouse Utilities.

- `CursorBrakeCore.h` holds the decisions as a header-only, Win32-free `cursorbrake::Engine`. Its input is the monitor rects with their DPI, the current and proposed points, the input kind, and the modifier and button state. Its output is `Allow` or `Hold` with the point to place the cursor at. It is deterministic, which is what lets `CursorBrake.UnitTests` and `CursorBrake.FuzzTests` drive it directly.
- `dllmain.cpp` implements `PowertoyModuleIface`. `enable()` starts a dedicated thread that installs `WH_MOUSE_LL` and pumps messages, since low-level hook callbacks are delivered to the installing thread. `disable()` signals a terminate event and joins the thread.
- The hook handles `WM_MOUSEMOVE` only. It classifies the event (`ClassifyInput`), reads the current point with `GetCursorPos`, the cursor clip with `GetClipCursor`, and the modifier and buttons with `GetAsyncKeyState`, and asks the engine. On `Hold` it calls `SetCursorPos` and returns `1`. `SetCursorPos` raises no low-level hook event, so the module never sees its own correction.
- A hidden top-level window on the hook thread receives the display, device and session-change broadcasts. A message-only window does not receive those broadcasts.
- The hook path does not log, per `.github/copilot-instructions.md`. Settings, layout and session changes log through the `cursor-brake` logger.

## Settings

Stored at `%LOCALAPPDATA%\Microsoft\PowerToys\CursorBrake\settings.json`. The snake_case keys must match between the C# `CursorBrakeProperties` and the C++ `parse_settings`.

| Key | Type | Default | Meaning |
| --- | --- | --- | --- |
| `push_distance` | int | `300` | Strength: how far to push past an edge before crossing, in logical pixels. Clamped to 0-1000; 0 turns resistance off |
| `free_return` | bool | `true` | One free crossing straight back over the edge just crossed |
| `edge_filter` | int | `0` | Apply to: 0 all edges between monitors, 1 side-by-side only, 2 stacked only |
| `disable_while_dragging` | bool | `false` | Cross freely while any mouse button is held |
| `cross_modifier` | int | `0` | Cross freely while holding: 0 Ctrl, 1 Shift |

`parse_settings` keeps the previous value for a non-finite number, an out-of-range choice, or a key of the wrong type. The module on/off state lives in `EnabledModules.CursorBrake` in the global settings. There is no activation hotkey: the brake is on whenever the module is.

## Settings UI

CursorBrake is a section on the shared **Settings > Mouse utilities** page (`MouseUtilsPage.xaml`, bound to `MouseUtilsViewModel`), first in the page's alphabetical order. The enable toggle is its own card. Everything else lives in a "Resistance and behavior" expander that is disabled until the module is on.

| Control | Type (UI range) | Setting key | ViewModel property | Default |
| --- | --- | --- | --- | --- |
| CursorBrake | toggle | `EnabledModules.CursorBrake` | `IsCursorBrakeEnabled` | off |
| Strength | slider (0-1000, 25 px steps; the thumb tooltip shows the value) | `push_distance` | `CursorBrakePushDistance` | 300 |
| Free return | checkbox | `free_return` | `CursorBrakeFreeReturn` | on |
| Apply to | combo box | `edge_filter` | `CursorBrakeEdgeFilter` | All edges between monitors |
| Disable resistance while dragging | checkbox | `disable_while_dragging` | `CursorBrakeDisableWhileDragging` | off |
| Cross freely while | combo box | `cross_modifier` | `CursorBrakeCrossModifier` | Holding Ctrl |

When the page loads, the ViewModel repairs nulls and out-of-range values from a hand-edited file to the defaults and the module's ranges, in the settings object it saves. The controls therefore show what the next save writes and sends. Until that save, the module ignores an out-of-range choice and keeps its last valid one, and it clamps an out-of-range distance the same way the ViewModel does. The group is GPO-aware through `ConfigureEnabledUtilityCursorBrake`: when policy forces the module on or off, the toggle is disabled and a `GPOInfoControl` warning shows. UI tests find controls by automation id: `MouseUtils_CursorBrakeTestId` (group), `MouseUtils_CursorBrakeToggleId`, `MouseUtils_CursorBrakeOptionsId`, `MouseUtils_CursorBrakePushDistanceId`, `MouseUtils_CursorBrakeFreeReturnId`, `MouseUtils_CursorBrakeEdgeFilterId`, `MouseUtils_CursorBrakeDisableWhileDraggingId` and `MouseUtils_CursorBrakeCrossModifierId`.

## Building and debugging

- Native module only: `msbuild src/modules/MouseUtils/CursorBrake/CursorBrake.vcxproj /p:Platform=x64 /p:Configuration=Release`. The DLL lands in `x64\<Config>\PowerToys.CursorBrake.dll`, where the runner loads it from (`knownModules` in `src/runner/main.cpp`).
- Without the runner: `tools\module_loader` loads the DLL with a `settings.json` beside it (`ModuleLoader.exe PowerToys.CursorBrake.dll`). Exit any running PowerToys first, since two CursorBrake hooks would both brake.
- Debugging: the hook runs inside the runner, so attach to `PowerToys.exe` and break in `dllmain.cpp`. A breakpoint inside `HandleMove` stalls system-wide mouse input and risks `LowLevelHooksTimeout` removing the hook; break in `CursorBrakeCore.h` from the unit tests instead.
- Inner-edge behavior needs two or more displays: multi-monitor hardware, or a guest with several virtual displays. In either, `SendInput` moves carrying the test marker exercise the brake from a script.

## Testing

- `CursorBrake.UnitTests`: table-driven CppUnitTest cases over synthetic layouts (side by side, stacked, partial overlap, mixed DPI, single monitor, four-monitor junction) covering push accumulation, draining, the move cap, DPI scaling, the direction filter, modifiers, dragging, free return, the cursor clip and settings or layout resets.
- `CursorBrake.FuzzTests`: a libFuzzer target over the engine, with clips set and lifted between moves, including empty and inverted ones. Invariants: a `Hold` point is always inside the source monitor, only a move that lands on another monitor once clamped into the clip is held, `Allow` never alters the point, and no input crashes. It builds to `x64\Release\tests\CursorBrake.FuzzTests\`, which the OneFuzz pipeline's `**/tests/*.FuzzTests/**` glob picks up.
- `MouseUtils.UITests.Next/CursorBrakeSettingsTests.cs`: the options follow the enable toggle, every setting persists across a restart, and marked moves within one monitor land where they were sent. CI agents have one monitor, so inner-edge behavior needs multi-monitor hardware or a guest with several virtual displays. Before moving, the no-op case waits for a fresh `CursorBrake hook installed.` line in the module log. It fails on `Failed to install CursorBrake mouse hook`, and reports inconclusive when the session line before the install says the session is remote.

## Known limitations

- **Elevated windows.** The runner is medium integrity, so UIPI keeps its low-level hook from seeing input over a window running as administrator. The brake does not apply there unless PowerToys runs as administrator.
- **Injected relative input.** Gamepad mappers, scripts and Mouse Without Borders relative mode inject their moves, so they pass unbraked.
