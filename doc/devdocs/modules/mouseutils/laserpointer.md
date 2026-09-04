# Laser Pointer

Laser Pointer draws a fading trail behind the pointer, so an audience can follow what is being pointed at. It also mirrors a chosen window into a second, off-screen window with the trail drawn into it, so the laser survives per-window screen sharing.

## Implementation

The module runs inside the PowerToys Runner process. `LaserPointerMain` starts a thread that owns a transparent, click-through overlay spanning the virtual desktop, a low-level mouse hook, and a render loop driven by a 16 ms `WM_TIMER`.

### Key files

- `src/modules/MouseUtils/LaserPointer/LaserPointer.cpp` — overlay window, input handling, render loop, presenter orchestration
- `src/modules/MouseUtils/LaserPointer/LaserStroke.{h,cpp}` — the trail model: sample storage, taper, decay, streamline smoothing, outline generation. Deliberately free of Windows dependencies
- `src/modules/MouseUtils/LaserPointer/PresenterWindow.{h,cpp}` — the shareable mirror window and its Windows.Graphics.Capture session
- `src/modules/MouseUtils/LaserPointer/dllmain.cpp` — module interface, settings parsing, hotkeys, event waiters

### Shortcuts

The runner identifies hotkeys **by index**, so the order of `HotkeyId` in `dllmain.cpp` must stay aligned with `GetAllHotkeyAccessors()` in `LaserPointerSettings.cs`. Adding one in the middle silently rebinds the others.

| Index | Id | Default | Action |
|---|---|---|---|
| 0 | `HotkeyMouse` | Win+Shift+L | Arms the mouse |
| 1 | `HotkeyPen` | *(none)* | Arms the pen. No default: leaving it unset is how pen support stays off |
| 2 | `HotkeyPresenterShare` | Ctrl+Shift+Win+W | Starts sharing, or moves the share to the window under the pointer |
| 3 | `HotkeyPresenterStop` | Ctrl+Shift+Win+Q | Stops sharing. Inert when nothing is shared |

Quick Access reaches the same actions through named events (`LASER_POINTER_PRESENTER_EVENT`, `LASER_POINTER_PRESENTER_STOP_EVENT`) rather than hotkeys.

### Picking a target from the flyout

The flyout is itself in front when its button is pressed, so the window under the pointer is the flyout and cannot be the target. The module therefore defers the pick: it polls for a presentable foreground window for a short while, and otherwise falls back to the last application window it saw in the foreground, tracked with a `EVENT_SYSTEM_FOREGROUND` hook.

The fallback is not belt-and-braces - it is the only thing that works by touch. Dismissing the flyout with a tap leaves the taskbar in front rather than the application behind it, so nothing presentable ever returns to the foreground and polling alone finds nothing. A mouse click restores focus to the window underneath, which is why the mouse path appeared to work on its own. Shell windows, Quick Access and the presenter all fail `IsPresentableWindow`, so opening the flyout cannot overwrite the remembered window with itself.

## Drawing and input

Arming only installs the hook; nothing is drawn until the activation button is held. The mouse contributes every position the hook reports, so the trail follows the real path rather than a resampling of it.

The pen is a separate path. Pen input reaches the hook as promoted mouse messages carrying `MI_WP_SIGNATURE` in `dwExtraInfo`, but over the taskbar and any DirectManipulation surface the app claims the pointer stream first and promotion is delayed or never happens. Two things follow from that:

- Positions come from **raw HID digitizer reports** (`RIDEV_INPUTSINK`, `HID_USAGE_DIGITIZER_PEN`), which arrive regardless of what the app underneath does.
- While an armed pen is in range the overlay stops being click-through *only within a small patch around the pen's last reported position*, so contact hit-tests to the overlay rather than to the app. Capturing the whole screen also worked, but swallowed mouse clicks anywhere on it.

`GetCurrentInputMessageSource` is not usable to tell pen from mouse here — it reports `IMDT_UNAVAILABLE` on real hardware. The raw HID reports are what supply that signal.

## Click-through

`WS_EX_TRANSPARENT` plus `HTTRANSPARENT` is not sufficient for a full-screen overlay in practice. Click-through is achieved by **window region**: when there is nothing to draw, the overlay's region is reduced to the presenter's border frame plus, while the notice is up, a narrow band across the top of the shared window. Only when a trail is actually on screen does the overlay take the whole surface and move to the top of the z-order.

Note that `SetWindowRgn` clips painting as well as hit-testing, so anything that must be drawn has to be inside the region.

## Presenter window

Per-window sharing captures only the target's own visual tree, so a separate overlay is never included — which is why the laser is invisible to remote viewers unless the whole screen is shared. The presenter window is a capturable surface containing both: the target's pixels via `Windows.Graphics.Capture`, and the trail composited on top.

Details that are easy to get wrong:

- It lives **off the virtual desktop**. Windows keeps compositing off-screen windows, and both Teams and OBS still list them.
- It is `WS_POPUP` + `WS_EX_APPWINDOW`, with no caption. `WS_EX_NOACTIVATE` must **not** be set — that is what keeps a window out of the taskbar and out of sharing pickers. Focus is avoided with `SW_SHOWNOACTIVATE` instead.
- Geometry comes from `DWMWA_EXTENDED_FRAME_BOUNDS`, not `GetWindowRect`. The latter includes the invisible resize border and is several pixels larger than what capture produces.
- Retargeting keeps the same HWND and only swaps the capture session and resizes. Destroying and recreating it would pull the shared surface out from under the viewer.
- `WM_CLOSE` (taskbar jump list, Alt+F4) is forwarded to the module rather than handled locally, so the capture session, overlay state and published state all wind down together.

### The on-screen border

The red outline is drawn by the overlay, not by the presenter window, so it is subject to
the overlay's z-order. While only the border is showing, the overlay is parked in the
shared window's own slot and the window manager clips it correctly. A visible trail
changes that: the overlay goes topmost over the whole screen, and the border went with it
- painting the shared window's outline on top of whatever was covering it.

`TargetMostlyCovered` is the guard. While the overlay is topmost it checks the windows
above the target, ignoring cloaked ones, and skips the border and its label when
something covers a fifth or more of the shared window. The answer is cached for 200 ms
rather than recomputed per frame.

### Sharing state

There is no API that tells a window it is being captured — `Windows.Graphics.Capture` lives entirely in the capturing process. This was measured, not assumed: an off-screen window reports `S_OK` from `Present(DXGI_PRESENT_TEST)` and an unchanged `DWMWA_CLOAKED` whether or not a capture session is attached.

Because Quick Access runs in its own process, the module publishes whether the presenter is up through a manual-reset named event, `LASER_POINTER_PRESENTER_ACTIVE_EVENT`. The flyout opens it, reads it with `WaitOne(0)` and closes it again; a handle held open would keep the name alive and report a stale value after the module exits.

## Cursor

While the mouse is armed, the system cursor is replaced with a laser-pointer glyph. The overlay is click-through, so the cursor belongs to whatever window is underneath — there is no per-window way to do this, and `SetSystemCursor` is system-wide. Restoring it is therefore not optional: every path that disarms goes through `RestoreSystemCursor`, which calls `SystemParametersInfo(SPI_SETCURSORS)` so the user's own scheme comes back rather than a snapshot. Four shapes are replaced (arrow, I-beam, crosshair, hand) because mouse-move messages are not suppressed and apps keep swapping their cursor as the pointer travels.

A pen-driven stroke deliberately does not change the cursor.

## Settings and localization

Settings live in `LaserPointerProperties.cs` and are mirrored by `LaserPointerSettings` in `LaserPointer.h`. The C# default and the C++ fallback must be kept in step — the C# constant seeds a fresh `settings.json`, and the C++ value is what the module uses when a setting is missing.

The module's three user-visible native strings (`Present: `, `window`, `Capturing: `) live in `Resources.resx` and are reached with `GET_RESOURCE_STRING`. `resource.base.h` and `LaserPointer.base.rc` are templates; `convert-resx-to-rc.ps1` generates `Generated Files/resource.h` and `Generated Files/LaserPointer.rc` at build time. Non-localized ids are assigned in the 2000 range because the converter numbers the string table from 101 upwards.

## Tests

`LaserPointerTests/LaserStrokeChecks.cpp` covers the stroke model — taper, decay, streamline, pruning, degenerate input. It is not an MSBuild project and does not run in CI; see the README beside it for how to build and run it. Note that `LaserStroke.cpp` includes `"pch.h"`, which resolves next to the source file, so a standalone build needs the model copied beside a stub `pch.h`.

Behaviour that involves the overlay, the hook, capture or the presenter is not covered by automated tests.
