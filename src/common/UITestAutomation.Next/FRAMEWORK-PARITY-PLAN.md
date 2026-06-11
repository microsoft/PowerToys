# UITestAutomation.Next — Parity & Hardening Plan

Tracks the gaps between the new winappcli-based framework (`UITestAutomation.Next`) and the
legacy WinAppDriver/Selenium framework (`UITestAutomation`), plus the ideal end state. Nothing
here is implemented yet — this is the backlog to work through later.

> Reference points:
> - Legacy base: `src/common/UITestAutomation/UITestBase.cs`
> - New base: `src/common/UITestAutomation.Next/UITestBase.cs`
> - New launch: `src/common/UITestAutomation.Next/SessionHelper.cs`

## Current `.Next` init flow (baseline)

`TestInit` does exactly:
1. Probe `winapp.exe` availability (fail fast with install hint).
2. `new SessionHelper(scope)` → `Init()` → launch (runner `--open-settings` for Settings scope) and
   wait for the first UIA window.

`TestCleanup` captures a single screenshot on failure, then a no-op `Session.Cleanup()`.

Everything below is present in the legacy harness but **missing or unwired** in `.Next`.

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

## Gap 3 — Module-enablement pre-config not wired in (HIGH, low risk)

- Legacy `StartExe(enableModules)` → `SettingsConfigHelper.ConfigureGlobalModuleSettings(...)` seeds
  `settings.json` **before** launch, so a test starts from a known module on/off state.
- `.Next` ships `SettingsConfigHelper.ConfigureGlobalModuleSettings` but **nothing calls it**. This is the
  root of the "test assumes module is ON" fragility class.

**Plan:** add an optional `string[]? enableModules = null` ctor param. When non-null, call
`ConfigureGlobalModuleSettings(enableModules)` in `TestInit` **before** launching the runner. Document that
passing it gives a deterministic module baseline.

## Gap 4 — No scope teardown on cleanup (MEDIUM, needs design)

- Legacy `TestCleanup` → `sessionHelper.Cleanup()` → `ExitScopeExe()` stops what it launched.
- `.Next` `Session.Cleanup()` is a no-op and `EnsureRunning`'s "did I launch it" bool is discarded, so the
  base never stops the process it started. (Individual tests like ColorPicker do their own `finally`.)

**Design call needed:** per-test teardown (kill scope process) vs. reuse a long-lived runner across a class.
Recommended: track the "launched-by-me" bool in `SessionHelper`, expose `StopIfStarted()`, and call it from
`TestCleanup` only when the base started the process. Add `RestartScope` convenience equivalent to legacy
`RestartScopeExe`.

## Gap 5 — Pipeline diagnostics (MEDIUM/LARGE, CI-only)

Legacy gates these on `EnvironmentConfig.IsInPipeline`:

| Behavior | Legacy | `.Next` | Notes |
|---|---|---|---|
| Normalize resolution to 1920×1080 | ✅ `ChangeDisplayResolution` | ❌ | Port to `MonitorInfo`/native helper |
| Monitor info snapshot | ✅ `GetMonitorInfo()` | ⚠️ `MonitorInfo` exists, not called in init | |
| Screenshot timer (1s cadence) | ✅ `ScreenCapture.TimerCallback` | ❌ | Needs port |
| Screen recording (FFmpeg) | ✅ `ScreenRecording` | ❌ | Needs FFmpeg dependency decision |
| On failure attach screenshots + recordings + **log files** | ✅ | ⚠️ single screenshot only | Add log-file + recording attach |

**Plan:** `.Next` `UITestBase` should branch on `EnvironmentConfig.IsInPipeline` and, when true, set up
screenshot timer + recording in `TestInit` and attach artifacts in `TestCleanup`. Treat FFmpeg recording as a
separate, optional sub-task (it's the heaviest dependency).

## Gap 6 — Editor scopes still launch the module exe directly (LOW, follow-up)

After the Settings-scope fix (`PowerToys.exe --open-settings`), editor scopes (Hosts, Workspaces,
CommandPalette, FancyZonesEditor, ScreenRuler) still launch their own exe in `SessionHelper.EnsureRunning`.
That is correct for editors that are meant to run standalone, but confirm each one against how the runner
launches it in production, and document the intended pattern per scope in `ModuleConfigData`.

---

## Suggested sequencing

1. **Phase 1 (quick wins, no API break risk to callers):** Gap 1 hygiene.
2. **Phase 2 (ctor surface):** Gaps 2 + 3 — add `WindowSize` and `enableModules` ctor params (defaulted, so
   existing `.Next` tests keep compiling). Unblocks porting legacy Settings/Hosts/Workspaces tests.
3. **Phase 3 (lifecycle):** Gap 4 teardown/restart design + implementation.
4. **Phase 4 (CI):** Gap 5 diagnostics, FFmpeg recording last.
5. **Phase 5 (cleanup):** Gap 6 per-scope launch audit + docs.

## Acceptance criteria (per phase)

- Existing `.Next` tests still compile and pass (defaulted params, no behavior change unless opted in).
- New behavior is opt-in or gated (e.g. pipeline-only) so local runs stay fast.
- Each ported behavior matches legacy semantics or documents the intentional difference.
- No product code changes — framework/test only.
