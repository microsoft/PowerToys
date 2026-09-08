# Local acceptance — 2026-09-08

## Automatic maximize-to-region window manager

The actual out-of-process RegionWindowManager is integrated with mirror Start/Stop. The diagnostic flag --window-manager-only starts the same component without the driver or capture pipeline.

| Check | Result |
| --- | --- |
| Debug x64 application and native test builds | Both exit 0, no errors or warnings |
| Native tests | 42 passed, including 16 window-management tests |
| Release x64 ASan/libFuzzer build and run | Build exit 0; 579,968 executions in 31 seconds, exit 0 |
| Actual-manager integration script | 8 scenarios passed, exit 0; 3 successful automatic fits and no manager error |
| Real title-bar mouse interaction | Drag-in enrollment, native maximize, native restore and drag-out removal passed |
| Test cleanup | Manager and all disposable child processes exited |

The integration script uses disposable standard Win32 windows. Drag boundaries are controlled NotifyWinEvent events emitted by the child UI thread; maximize/restore use real system commands and OS-generated location events. The test never resizes a maximized window itself. It confirmed:

- Unregistered windows keep ordinary system maximization.
- Ordinary move/resize is unchanged before and after enrollment.
- Repeated native maximize fits the selected visible rectangle while keeping IsZoomed, WS_MAXIMIZE and SW_SHOWMAXIMIZED.
- Native Restore returns to the updated normal placement without custom restoration.
- Drag-out removes enrollment; re-enrollment works.
- After the manager stops, native maximization is no longer adjusted.

A separate real UI check used a title-bar drag and the actual Maximize/Restore controls. For region [500,300,1200,800], the maximized visible frame matched exactly, and native Restore returned to [490,381,1190,831], the normal position reached by the drag. After dragging out, maximization used the regular monitor work area [0,0,2560,1368]. The manager recorded one adjustment and no error.

Evidence (ignored by Git):

- artifacts/window-manager-validation/result.json and manager-result.json
- artifacts/ui-window-manager/result.json and manager.json
- artifacts/test-results-window-manager/*.trx
- artifacts/fuzz-run-window-manager.log

These runs used standard Win32 test windows at 144 DPI. Other application frameworks, mixed-DPI real applications, minimize-to-maximize restoration, and native drag-to-restore remain compatibility checks. The window-manager-only runs did not create virtual displays; prior capture/driver acceptance is recorded below.

## External maximized-window resize experiment

The standalone script scripts/Probe-MaximizedWindow.py passed on Windows 11 build 26200, x64, at 144 DPI (150% scaling).

The child process owns an ordinary WS_OVERLAPPEDWINDOW and delegates its messages to DefWindowProc. A separate parent process sends the native SC_MAXIMIZE command, calls SetWindowPos without first restoring the window, then sends SC_RESTORE. No DLL, hook, target-process message override, driver change, or existing user application is involved. Python ctypes allows the test to run without a repository build.

All 6 scenarios passed, each for 2 consecutive cycles (12 cycles total):

- Primary monitor, synchronous and asynchronous SetWindowPos.
- Negative-origin secondary monitor, synchronous and asynchronous SetWindowPos.
- Rectangle spanning the two monitors, synchronous and asynchronous SetWindowPos.

Each cycle confirmed:

- The requested outer rectangle is reached and stable.
- IsZoomed remains true, WS_MAXIMIZE remains set, and WINDOWPLACEMENT.showCmd remains SW_SHOWMAXIMIZED.
- The saved normal placement remains unchanged.
- Compensating the measured DWM frame margins aligns the visible window exactly with the desired rectangle, while preserving the same maximize state.
- Native SC_RESTORE clears maximize state and returns to the original normal rectangle, without manually restoring saved coordinates.

In this environment the maximized test window had 11-pixel invisible margins. These were measured from GetWindowRect and DWMWA_EXTENDED_FRAME_BOUNDS, not hardcoded into the test.

Evidence: artifacts/maximize-probe/result.json (ignored by Git).

This isolated probe demonstrates the primitive needed by an out-of-process maximize listener for a standard Win32 window. It did not test all applications, mixed DPI, drag-to-restore, Snap layouts, or physical maximize-button interaction. The later automatic-manager and real UI acceptance is recorded above.

## Cross-monitor extension

The cross-monitor version was validated on the same two active 2560×1440 physical displays.

| Check | Result |
| --- | --- |
| Debug x64 application and test builds | Both exit 0, no errors or warnings |
| Native unit tests via vstest.console | 26 passed, including 8 capture-tile tests |
| Release x64 ASan/libFuzzer build and run | Build exit 0; 527,290 executions in 31 seconds, exit 0 |
| Actual source rectangle | x=-640, y=540, width=1280, height=360; intersects 640×360 on each physical screen |
| Elevated cross-monitor session | Exit 0; 574 composite frames presented during 15 seconds |
| Per-source frames copied | 397 and 563; both source identities matched the baseline screens |
| Owned virtual-device removal | Confirmed |
| Original physical display layout | Both identities, positions and dimensions preserved |

Cross-monitor evidence is separate from the original single-monitor run:

- artifacts/virtual-display-validation-cross-monitor/acceptance.json
- artifacts/virtual-display-validation-cross-monitor/capture-result.json
- artifacts/virtual-display-validation-cross-monitor/displays-before.json
- artifacts/virtual-display-validation-cross-monitor/displays-after.json
- artifacts/virtual-display-validation-cross-monitor/validation.log
- artifacts/test-results-cross-monitor/*.trx
- artifacts/fuzz-run-cross-monitor.log

The real run used a CLI-specified cross-monitor rectangle. Horizontal/vertical layouts, negative origins, gaps and coordinate mapping are covered by unit tests and fuzzing; mixed-DPI output, actual gap pixels, mouse-drag interaction across screens and pixel-by-pixel image comparison were not separately exercised in this run. No meeting was joined or shared.

## Original single-monitor PoC

Validated on Windows 11 x64 (build 26200), with two active 2560×1440 physical displays, including a display at a negative desktop X coordinate.

| Check | Result |
| --- | --- |
| Standalone Debug x64 application build | Exit 0, no errors or warnings |
| Native unit-test project build | Exit 0 |
| Native unit tests via vstest.console | 18 passed, exit 0 |
| Release x64 ASan/libFuzzer build | Exit 0 |
| Bounded geometry/parser fuzz run | 541,198 executions in 31 seconds, exit 0 |
| Signed external driver preparation | Pinned SHA-256 and Windows CAT/DLL signature validation passed |
| Driver staging | Succeeded; Windows published the package as oem29.inf |
| Interactive selection | A selected rectangle appeared in the controller; Start became available |
| Non-elevated automatic run | Expected exit 1 and actionable error; zero frames and no virtual device |
| Elevated virtual-display session | Exit 0; 562 frames presented during a 15-second capture of a 640×360 region |
| Owned software-device removal | Confirmed after asynchronous PnP removal |
| Existing physical display layout | Both monitor identities, positions and dimensions preserved |

The first hardware run exposed GDI display-name renumbering during adapter arrival. The app now identifies monitors by their DISPLAYCONFIG target interface path, refreshes handles and the owned adapter's current GDI name, and waits for stable source/target geometry before capturing. The corrected integration run passed.

Device removal was asynchronous: the acceptance script initially still observed the owned PnP instance, then confirmed its disappearance and two matching physical-layout snapshots after approximately 3.1 seconds. The driver package remains staged, as authorized; no RegionMirror virtual device remains active.

Evidence is local and ignored by Git:

- artifacts/virtual-display-validation/acceptance.json
- artifacts/virtual-display-validation/capture-result.json
- artifacts/virtual-display-validation/displays-before.json
- artifacts/virtual-display-validation/displays-after.json
- artifacts/virtual-display-validation/validation.log
- artifacts/test-results/*.trx
- artifacts/fuzz-run.log
- Per-project build.debug.x64 / build.release.x64 logs

The first fuzz invocation used a relative dictionary path and exited before executing inputs. The completed run used absolute dictionary/corpus paths.

Not claimed by this acceptance: Teams/Zoom screen-sharing compatibility, pixel-by-pixel output comparison, HDR accuracy, mixed-DPI capture, prolonged performance, full interactive cancellation coverage, or OneFuzz deployment. The user stopped Computer Use with Escape after the successful integration run; no further UI automation was performed.
