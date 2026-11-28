# UI-test framework guide

`Microsoft.PowerToys.UITest` lives in [UITestAutomation](../../../../src/common/UITestAutomation/).
Tests run as MSTest executables on Microsoft.Testing.Platform. The framework calls `winapp.exe` for
UI Automation and uses shared helpers for input, window management, and diagnostics.

## Command execution and element snapshots

- Set `WINAPP_CLI_PATH` if `winapp.exe` is not on `PATH`. `UITestBase` checks availability and fails
  early with an install hint. A build does not need the CLI.
- CLI operations run in independent subprocesses. For expected transient absence, use `Has*` and
  `WaitFor*` instead of catching a failed `Find`. For unexpected failures, read the reported command,
  exit code, and stderr along with the test artifacts.
- `Element` holds a selector and a `Session`. `Name`, `ClassName`, `ControlType`, and
  `X/Y/Width/Height` are snapshots taken at lookup time. `GetValue()`, `GetProperty(...)`, and
  `IsEnabled` read fresh UIA state. Find the element again after navigation or a geometry change.

## Selectors and session scope

- Prefer `By.AccessibilityId(id)` (also `By.Id(id)`) for a stable automation ID.
- `By.Name(text)` is a case-insensitive substring match over Name/AutomationId. If several elements
  match, use a typed wrapper, scope the search under a container, or filter `FindAll<T>` results by
  exact `Name` or `ClassName`.
- Use `By.Slug(slug)` for the current UIA tree when necessary. Do not retain slugs across tree
  refreshes. Use `Element.Find<T>` for descendant searches.
- A **window session** (`-w <hwnd>`, the default from `UITestBase`) pins one window. Use it when
  a process owns several windows and the test needs one specific target.
- A **process session** (`-a <name|pid>`, via `Session.FromProcess`) resolves the process's windows
  on each call. Use it when the surface can move to a separate window or recreate its HWND.
- To locate another window, use `WindowsFinder.WaitForWindowByApp`, `WaitForWindowByTitle`, or
  `WaitForWindowByProcess` and verify the returned session before acting. `ListAll` omits windows
  without a Win32 title; use `ListByApp` or `WaitForWindowByApp` for those.

## Test lifecycle

- Use `PowerToysModule.PowerToysSettings` for most tests, especially when Runner owns the module
  toggle or global hotkey. Direct editor scopes are appropriate for standalone editor windows.
- Set `enableModules` with the exact keys under `"enabled"` in global `settings.json`
  (e.g. `"ColorPicker"` or `"Measure Tool"`) for a deterministic startup state. When supplied, it
  enables only those modules. For an assertion about Settings changing module state at runtime,
  use the actual Settings control and assert the resulting state without restarting.
- `UITestBase` performs pre-test desktop/process hygiene and applies the selected `WindowSize`.
  Its cleanup stops the scope it launched. Close test-owned secondary windows and restore any
  other changed state in test cleanup. `RestartScope` returns a new session when a restart is
  itself necessary for the scenario.
- Override `protected override bool ReuseScopeAcrossTests => true;` for a suite with a stable
  class-scoped Runner or Shell registration. Keep per-test state isolated. The default is per-test
  startup and cleanup; an exited shared scope is recreated when needed.
- A derived `[TestCleanup]` runs before base cleanup. If it closes a window that explains a
  failure, call `CaptureFailureArtifactsBeforeCleanupAsync(...)` first.

See the [API reference](api-reference.md) for actions and windows, and
[CI stability](ci-stability.md) for readiness and failure diagnosis.
