# UITestAutomation.Next — Parity & Hardening Plan

Tracks the gaps between the new winappcli-based framework (`UITestAutomation.Next`) and the
legacy WinAppDriver/Selenium framework (`UITestAutomation`), plus the ideal end state. **All gaps
below are now implemented** — see the per-gap **Done** notes and the Status summary. The detailed
sections are kept as the rationale/record.

> Reference points:
> - Legacy base: `src/common/UITestAutomation/UITestBase.cs`
> - New base: `src/common/UITestAutomation.Next/UITestBase.cs`
> - New launch: `src/common/UITestAutomation.Next/SessionHelper.cs`

## Status — implemented

| Gap | Status | Where |
|---|---|---|
| 1 — Clean-slate hygiene | ✅ Done | `UITestBase.PreTestHygiene()` + `virtual StaleProcessNames`; `WindowControl.TryKillProcessByName` |
| 2 — `WindowSize` wired in | ✅ Done | `UITestBase` ctor `size` param + `ApplyWindowSize()` |
| 3 — Module-enablement pre-config | ✅ Done | `UITestBase` ctor `enableModules` param → `ConfigureGlobalModuleSettings` before launch |
| 4 — Scope teardown / restart | ✅ Done | `SessionHelper.launchedByUs` / `StopIfStarted()` / `Restart()`; `UITestBase.RestartScope(...)` |
| 5 — Pipeline diagnostics | ✅ Done (pipeline-gated) | new `ScreenCapture.cs`, `ScreenRecording.cs`, `DisplayHelper.cs`; wired in `UITestBase` |
| 6 — Editor-scope launch audit | ✅ Documented | per-scope launch model in `ModuleConfigData.cs` (`PowerToysModule` doc) |

Framework/test-only change — no product code touched. Harness + both `.Next` consumers
(`ColorPicker.UITests`, `Settings.UITests`) build clean (exit 0).

## Current `.Next` init flow (baseline)

`TestInit` does exactly:
1. Probe `winapp.exe` availability (fail fast with install hint).
2. `new SessionHelper(scope)` → `Init()` → launch (runner `--open-settings` for Settings scope) and
   wait for the first UIA window.

`TestCleanup` captures a single screenshot on failure, then a no-op `Session.Cleanup()`.

> Historical (pre-implementation) baseline. Everything below was present in the legacy harness but
> **missing or unwired** in `.Next` at the time of writing — now implemented (see Status above).

---

## Gap 1 — Clean-slate / window hygiene (HIGH, low risk)

Legacy `TestInit` starts every test from a known desktop state; `.Next` does none of it.

| Behavior | Legacy | `.Next` | Plumbing exists? |
|---|---|---|---|
| Minimize all windows (`Win+M`) | ✅ `KeyboardHelper.SendKeys(Key.Win, Key.M)` | ❌ | ✅ `SendKeys(Key.LWin, Key.M)` |
| Kill stale processes (`PowerToys`, `PowerToys.Settings`, `PowerToys.FancyZonesEditor`) | ✅ `CloseOtherApplications()` | ❌ | ✅ `WindowControl.TryKillProcess` |
| Dismiss popups (`{ESC}`) before launch | ✅ | ❌ | ✅ `KeyboardHelper` |

**Plan:** add a `PreTestHygiene()` step at the top of `TestInit` (before `SessionHelper.Init`):
minimize-all → ESC → kill known stale processes. Make the stale-process list a `virtual` property so
module suites can extend it.

**Done:** `UITestBase.PreTestHygiene()` runs at the top of `TestInit` — `Win+M` → `Esc` → kill each
name in the new `virtual StaleProcessNames` property. Uses the new `WindowControl.TryKillProcessByName`
(exact-name match) instead of the Contains-based `TryKillProcess`, so a `PowerToys.*.UITests` test
host is never caught by the "PowerToys" entry.

## Gap 2 — `WindowSize` not wired into the base (HIGH, low risk)

- Legacy ctor: `UITestBase(PowerToysModule scope, WindowSize size, string[]? commandLineArgs)` and applies
  `size` during `Session` construction.
- `.Next` already has `WindowHelper.SetWindowSize`, the `WindowSize` enum, and `Session.Attach(size)` —
  but `UITestBase` has no `size` parameter and never applies one. Every `.Next` test runs at the window's
  default size.
- Blocks porting tests that rely on a fixed size, e.g. `src/settings-ui/UITest-Settings/SettingsTests.cs`
  (`WindowSize.Large`), Hosts/Workspaces (`WindowSize.Medium`), Peek (`Small_Vertical`).

**Plan:** add `WindowSize size = WindowSize.UnSpecified` to the `UITestBase` ctor; after `Init()` resolves
the window, call `WindowHelper.SetWindowSize(hwnd, size)` when `size != UnSpecified`.

**Done:** `UITestBase` ctor now takes `WindowSize size = UnSpecified` (defaulted). `ApplyWindowSize()`
runs after `Init()` (and after every `RestartScope`) and calls
`WindowHelper.SetWindowSize(new IntPtr(Session.WindowHandle), size)` when set.

## Gap 3 — Module-enablement pre-config not wired in (HIGH, low risk)

- Legacy `StartExe(enableModules)` → `SettingsConfigHelper.ConfigureGlobalModuleSettings(...)` seeds
  `settings.json` **before** launch, so a test starts from a known module on/off state.
- `.Next` ships `SettingsConfigHelper.ConfigureGlobalModuleSettings` but **nothing calls it**. This is the
  root of the "test assumes module is ON" fragility class.

**Plan:** add an optional `string[]? enableModules = null` ctor param. When non-null, call
`ConfigureGlobalModuleSettings(enableModules)` in `TestInit` **before** launching the runner. Document that
passing it gives a deterministic module baseline.

**Done:** `UITestBase` ctor takes `string[]? enableModules = null`; `TestInit` calls
`SettingsConfigHelper.ConfigureGlobalModuleSettings(enableModules)` before `SessionHelper.Init` when it's
non-null. The ctor value is also re-applied by `RestartScope()` (unless that call overrides it).

## Gap 4 — No scope teardown on cleanup (MEDIUM, needs design)

- Legacy `TestCleanup` → `sessionHelper.Cleanup()` → `ExitScopeExe()` stops what it launched.
- `.Next` `Session.Cleanup()` is a no-op and `EnsureRunning`'s "did I launch it" bool is discarded, so the
  base never stops the process it started. (Individual tests like ColorPicker do their own `finally`.)

**Design call needed:** per-test teardown (kill scope process) vs. reuse a long-lived runner across a class.
Recommended: track the "launched-by-me" bool in `SessionHelper`, expose `StopIfStarted()`, and call it from
`TestCleanup` only when the base started the process. Add `RestartScope` convenience equivalent to legacy
`RestartScopeExe`.

**Done:** `SessionHelper` stores `launchedByUs` (set from `EnsureRunning`). `StopIfStarted()` tears down
**only** what we launched — kills the scope process and, for the Settings scope, the runner (exact-name
match); `TestCleanup` calls it. Instance `SessionHelper.Restart()` does kill → relaunch → rebind.
`UITestBase.RestartScope(string[]? enableModules = null)` re-seeds modules (ctor value if null), restarts,
reapplies window size, and returns the new `Session` — the `RestartScopeExe` equivalent.

## Gap 5 — Pipeline diagnostics (MEDIUM/LARGE, CI-only)

Legacy gates these on `EnvironmentConfig.IsInPipeline`:

| Behavior | Legacy | `.Next` | Notes |
|---|---|---|---|
| Normalize resolution to 1920×1080 | ✅ `ChangeDisplayResolution` | ❌ | Port to `MonitorInfo`/native helper |
| Monitor info snapshot | ✅ `GetMonitorInfo()` | ⚠️ `MonitorInfo` exists, not called in init | |
| Screenshot timer (1s cadence) | ✅ `ScreenCapture.TimerCallback` | ❌ | Needs port |
| Screen recording (FFmpeg) | ✅ `ScreenRecording` | ❌ | Needs port |
| On failure attach screenshots + recordings + **log files** | ✅ | ⚠️ single screenshot only | Add log-file + recording attach |

**Plan:** `.Next` `UITestBase` should branch on `EnvironmentConfig.IsInPipeline` and, when true, set up
screenshot timer + recording in `TestInit` and attach artifacts in `TestCleanup`. Treat FFmpeg recording as a
must have.

**Done (pipeline-gated on `EnvironmentConfig.IsInPipeline`):** new files `ScreenCapture.cs` (1s screenshot
timer), `ScreenRecording.cs` (FFmpeg encode), `DisplayHelper.cs` (`NormalizeResolution(1920,1080)` +
`LogMonitors`). `TestInit` normalizes resolution, logs the monitor topology, and starts the timer +
recording before launch; `TestCleanup` stops them and, on failure, attaches screenshots + recordings + the
PowerToys `*.log` files (`AddLogFilesToTestResults`), cleaning recordings on pass. The local (non-pipeline)
path still grabs the single winappcli `--capture-screen` failure shot. *Intentional difference:*
`NormalizeResolution` sets `DM_PELSWIDTH | DM_PELSHEIGHT` on the current mode (the documented, reliable
request) rather than the legacy's fields-unset call.

## Gap 6 — Editor scopes still launch the module exe directly (LOW, follow-up)

After the Settings-scope fix (`PowerToys.exe --open-settings`), editor scopes (Hosts, Workspaces,
CommandPalette, FancyZonesEditor, ScreenRuler) still launch their own exe in `SessionHelper.EnsureRunning`.
That is correct for editors that are meant to run standalone, but confirm each one against how the runner
launches it in production, and document the intended pattern per scope in `ModuleConfigData`.

**Done:** the launch model is now documented on the `PowerToysModule` enum in `ModuleConfigData.cs` —
runner-owned Settings (`--open-settings`), the runner itself, standalone editor scopes (FancyZonesEditor,
Hosts, Workspaces, PowerRename, CommandPalette, ScreenRuler), and overlay/background modules (ColorPicker,
LightSwitch) that should be driven through the Settings scope rather than launched standalone.

---

## Suggested sequencing

1. ✅ **Phase 1 (quick wins, no API break risk to callers):** Gap 1 hygiene.
2. ✅ **Phase 2 (ctor surface):** Gaps 2 + 3 — add `WindowSize` and `enableModules` ctor params (defaulted, so
   existing `.Next` tests keep compiling). Unblocks porting legacy Settings/Hosts/Workspaces tests.
3. ✅ **Phase 3 (lifecycle):** Gap 4 teardown/restart design + implementation.
4. ✅ **Phase 4 (CI):** Gap 5 diagnostics, FFmpeg recording.
5. ✅ **Phase 5 (cleanup):** Gap 6 per-scope launch audit + docs.

## Acceptance criteria (per phase)

- Existing `.Next` tests still compile and pass (defaulted params, no behavior change unless opted in).
- New behavior is opt-in or gated (e.g. pipeline-only) so local runs stay fast.
- Each ported behavior matches legacy semantics or documents the intentional difference.
- No product code changes — framework/test only.
