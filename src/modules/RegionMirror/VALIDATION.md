# Local acceptance — 2026-09-08

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
