# Local acceptance — 2026-09-08

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
