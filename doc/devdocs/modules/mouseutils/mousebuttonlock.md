# Mouse Button Lock

Mouse Button Lock is a [ClickLock](https://support.microsoft.com/windows/make-the-mouse-easier-to-use-10733da7-6fb8-4ddf-9b30-9a72b814f5d5) equivalent for the **left**, **right**, and **middle** mouse buttons. Windows ships ClickLock for the left button only; this module closes that gap for the secondary and middle buttons, and can optionally cover the left (primary) button too, as a one-stop alternative to the built-in Windows ClickLock. Because the left button is the primary interaction button (and Windows already provides ClickLock for it), left-button locking is **off by default** and strictly opt-in.

## Behavior

For each enabled button (LMB, RMB, and/or MMB):

1. Hold the button for at least the configured hold duration, then release.
2. The physical button-up is suppressed inside the low-level mouse hook, so the OS keeps perceiving the button as held.
3. The next physical tap of that button releases the synthetic hold (and that tap is swallowed cleanly, so downstream apps only ever see the injected up).

If "cancel on move" is enabled and the cursor moves beyond the dead-zone *before* the hold threshold elapses, the gesture is treated as a drag and does not lock. Once the threshold elapses the lock is armed and further motion no longer prevents it, so a held-button camera drag (the primary gaming use case) still locks.

## Architecture

This is a native C++ in-process module, like the other Mouse Utilities (Find My Mouse, Mouse Highlighter, Mouse Pointer Crosshairs, CursorWrap). The C++ implementation was ported from the standalone reference app at https://github.com/owenpkent/windows-right-click-lock.

- `dllmain.cpp` implements `PowertoyModuleIface`. `enable()` spins up a dedicated thread that installs `WH_MOUSE_LL` and runs a message pump (low-level hook callbacks are delivered to the installing thread). `disable()` signals a terminate event, joins the thread, and releases any locked button.
- The hook callback runs a small per-button state machine. It suppresses the matching button-up (`WM_RBUTTONUP` / `WM_MBUTTONUP`) by returning `1`, and injects the synthetic release (`MOUSEEVENTF_RIGHTUP` / `MOUSEEVENTF_MIDDLEUP`) via `SendInput`.
- Self-injection tag: every injected event sets `dwExtraInfo = 0x57494E4D` (`'WINM'`). The hook ignores events carrying this tag so it never recurses on its own synthetic input.
- The decision logic lives in `MouseButtonLockCore.h` as a Win32-free `Engine` (the clock is passed in and synthetic-up injection is behind an `IButtonUpInjector` interface), so `dllmain.cpp` is a thin Win32 adapter and the state machine is unit tested by `MouseButtonLock.UnitTests` (`EngineTests.cpp`).

## Settings

Stored at `%LOCALAPPDATA%\Microsoft\PowerToys\MouseButtonLock\settings.json`. Properties (snake_case keys must match between the C# `MouseButtonLockProperties` and the C++ `parse_settings`):

| Key | Type | Default | Meaning |
| --- | --- | --- | --- |
| `lmb_lock_enabled` | bool | `false` | Lock the left (primary) mouse button. Off by default; overlaps Windows' own left-button ClickLock |
| `rmb_lock_enabled` | bool | `true` | Lock the right mouse button |
| `mmb_lock_enabled` | bool | `false` | Lock the middle mouse button |
| `hold_duration_ms` | int | `1200` | How long to hold before locking. Default and range mirror Windows' built-in ClickLock (1200 ms, 200-2200 ms) |
| `move_cancel_enabled` | bool | `true` | Cancel locking if the cursor moves during the hold |
| `move_cancel_pixels` | int | `5` | Dead-zone radius for move-cancel |

The module on/off state lives in `EnabledModules.MouseButtonLock` in the global settings, not in the per-module properties. The settings UI is a section on the shared Mouse Utilities page (`MouseUtilsPage.xaml` / `MouseUtilsViewModel.cs`). There is no activation hotkey: activation is the physical hold.

## Settings UI

Mouse Button Lock is a section on the shared **Settings > Mouse utilities** page (`MouseUtilsPage.xaml`, bound to `MouseUtilsViewModel`). The enable toggle is its own card; everything else lives in a "Buttons and behavior" expander that is greyed out until the module is on, and "Move cancel distance" is greyed out unless "Cancel locking..." is checked.

```text
Settings  >  Mouse utilities

+------------------------------------------------------------------------+
|   [icon]   Mouse Button Lock                                ( On  =O ) |
+------------------------------------------------------------------------+

+------------------------------------------------------------------------+
|   [v]   Buttons and behavior                                           |
|         Choose which buttons lock and tune the hold-to-lock behavior.  |
+------------------------------------------------------------------------+
|   [ ]   Lock the left (primary) mouse button                           |
|   [x]   Lock the right mouse button                                    |
|   [ ]   Lock the middle mouse button                                   |
|                                                                        |
|   Hold duration (ms)                           1200 ms  [=====O======] |
|      How long to hold a button before it locks                         |
|                                                                        |
|   [x]   Cancel locking if the cursor moves while holding               |
|                                                                        |
|   Move cancel distance (pixels)                          [     5  ^v ] |
|      Movement beyond this distance during the hold is treated as a     |
|      drag and will not lock.                                           |
+------------------------------------------------------------------------+
```

Legend: `[icon]` module icon, `( On =O )` toggle switch, `[v]` expanded section, `[x]`/`[ ]` checked/unchecked checkbox, `[=====O======]` slider with its live value label (e.g. `1200 ms`), `[ 5 ^v ]` number box with spinner. The inner controls show the shipping defaults; the master toggle is drawn On (its default is off) so the expander is not greyed out.

| Control | Type (UI range) | Setting key | ViewModel property | Default |
| --- | --- | --- | --- | --- |
| Mouse Button Lock | toggle | `EnabledModules.MouseButtonLock` | `IsMouseButtonLockEnabled` | off |
| Lock the left (primary) mouse button | checkbox | `lmb_lock_enabled` | `MouseButtonLockLmbEnabled` | off |
| Lock the right mouse button | checkbox | `rmb_lock_enabled` | `MouseButtonLockRmbEnabled` | on |
| Lock the middle mouse button | checkbox | `mmb_lock_enabled` | `MouseButtonLockMmbEnabled` | off |
| Hold duration (ms) | slider (200-2200 ms, snaps in 200 ms steps) | `hold_duration_ms` | `MouseButtonLockHoldDurationMs` | 1200 |
| Cancel locking if the cursor moves while holding | checkbox | `move_cancel_enabled` | `MouseButtonLockMoveCancelEnabled` | on |
| Move cancel distance (pixels) | number box (0-100, step 1) | `move_cancel_pixels` | `MouseButtonLockMoveCancelPixels` | 5 |

The UI slider snaps (`SnapsTo="StepValues"`, `StepFrequency="200"`) to 200 ms increments, i.e. the same 11 values as Windows' built-in ClickLock slider: each step is 200 ms, the middle value is the 1200 ms default, so Short = 200 ms and Long = 2200 ms. Visual tick marks were dropped (they looked cluttered inside the settings card, and `SnapsTo` gives the notch-to-notch feel without them); the live "N ms" label beside the track (`MillisecondsLabelConverter`) shows the current value. The C++ `parse_settings` clamps a hand-edited file to 200-60000 ms (same 200 ms floor, a looser ceiling) and accepts any value in that range, so a hand-edited file is not restricted to the 11 slider steps. The group is GPO-aware: when policy forces the module on or off, the enable toggle is disabled and a `GPOInfoControl` warning shows (`IsMouseButtonLockEnabledGpoConfigured`). For UI tests, the Settings group exposes `AutomationProperties.AutomationId="MouseUtils_MouseButtonLockTestId"`, and the hold-duration slider and the move-cancel number box carry `MouseUtils_MouseButtonLockHoldDurationId` and `MouseUtils_MouseButtonLockMoveCancelPixelsId`.

## Safety

A logically-locked button whose up was suppressed leaves the OS believing the button is held. The module injects a synthetic up for every locked button on `disable()` and on graceful hook-thread shutdown, so toggling the module off or exiting PowerToys releases cleanly. A hard `TerminateProcess` of the runner mid-lock is the residual risk (same class of risk as the standalone reference app); see the open items below.

## Building and debugging

Mouse Button Lock builds as part of the PowerToys solution. The runtime is a native C++ DLL (`PowerToys.MouseButtonLock.dll`); the settings live in the shared C# Settings UI.

- Full build: open `PowerToys.slnx` in Visual Studio 2022 (Desktop C++, WinUI, and .NET desktop workloads) and build `x64`/`Release` (or `Debug`). The module DLL is emitted to `<repo>\x64\<Config>\PowerToys.MouseButtonLock.dll`, which is where the runner loads it from (`knownModules` in `src/runner/main.cpp`).
- Native module only (after a NuGet restore): `msbuild src/modules/MouseUtils/MouseButtonLock/MouseButtonLock.vcxproj /p:Platform=x64 /p:Configuration=Release`.
- Debugging: the hook runs inside the runner, so start or attach the debugger to `PowerToys.exe` and set breakpoints in `dllmain.cpp`. Enable the module from the Mouse Utilities settings page, then exercise the lock with a real right or middle button hold. Keep the hook callback non-blocking: a slow callback risks `LowLevelHooksTimeout` eviction by Windows.

Build status: build- and runtime-verified. The module builds in the full `PowerToys.slnx` solution (x64 Release) under Visual Studio 2026 with the .NET 10 toolset the repo now targets, linking `PowerToys.MouseButtonLock.dll` (exporting `powertoy_create`) with clean C++ code analysis, and all 18 `MouseButtonLock.UnitTests` pass. At runtime the built runner loads the module via `knownModules`, the Settings UI shows the controls, and the lock latches/releases correctly in real apps (see Open items #4).

## Runtime verification

The unit tests cover the Win32-free `Engine`. The Win32 glue in `dllmain.cpp` (hook install, the message pump, suppression by returning `1`, the tagged `SendInput` injection, and the injection-tag self-filter) is exercised end to end by a small standalone harness:

- The harness `LoadLibrary`s the built `PowerToys.MouseButtonLock.dll`, calls the exported `powertoy_create()` and `enable()` to install the real `WH_MOUSE_LL` hook on the module's own thread, then synthesizes gestures with untagged `SendInput`. Untagged injected input is indistinguishable from physical input to the hook (the hook filters only its own events, by `dwExtraInfo == 0x57494E4D`), so this drives the production code path rather than a stub.
- It observes `GetAsyncKeyState(VK_RBUTTON)`, which reads held only while the OS believes the button is down. That is a direct proxy for "the suppressed up actually took effect".
- Scenarios, all passing (3 runs, 5/5): baseline up; a quick tap below the threshold does not lock; a hold past the threshold then release locks (up suppressed, button stays held); a release tap clears the lock; a drag past the dead-zone cancels the lock. Clicks are contained to a transient top-most scratch window under the cursor, and the harness force-releases the button and disables the module on exit.

Source and build script currently live in the gitignored build output at `x64\Release\` (`mbl_harness.cpp`, `build_harness.cmd`). Rebuild with `build_harness.cmd`; run `x64\Release\mbl_harness.exe` from that folder so it finds the DLL. The harness is ephemeral there (a clean of `x64\` wipes it). To keep it, promote it to a tracked `tools\` project per the [tools convention](../../tools/readme.md) (PowerToys keeps standalone debug/test apps under `tools\`, built into `{install}\tools`), with a short page under `doc\devdocs\tools\` and an entry in that readme.

What the harness does not cover, and still needs the manual pass below: real mouse hardware (vs `SendInput`, which the hook treats identically but is not literally the same), the runner actually loading the DLL via `knownModules`, the C# Settings UI plumbing, and behavior inside real apps (context menu staying open, drag-select continuing).

## Fuzzing

PowerToys requires fuzzing for user-input modules (`AGENTS.md`: "New modules handling file I/O or user input must implement fuzzing tests"). `MouseButtonLock.FuzzTests` (folder `src/modules/MouseUtils/MouseButtonLock.FuzzingTest/`) is a C++ libFuzzer target over the Win32-free `Engine`, modeled on the repo's one existing C++ fuzz project (`PowerRename.FuzzingTest`).

- Target: `MouseButtonLock.FuzzingTest.cpp` decodes the fuzzer's bytes into a sequence of engine events (button down/up, cursor move, settings changes, and the lifecycle calls `EnforceEnabled` / `ReleaseAll` / `ResetTransient` / `IsLocked`) with adversarial ticks, coordinates, and settings, driving `mousebuttonlock::Engine` with a recording stand-in for the `SendInput` injector. The engine is header-only and Win32-free, so the target needs no project reference and touches no OS state.
- Registration: listed in `PowerToys.slnx` under `/modules/MouseUtils/Tests/` (ARM64 build disabled, which OneFuzz does not support) and emitted to `x64\Release\tests\MouseButtonLock.FuzzTests\`, so the OneFuzz pipeline's existing `**/tests/*.FuzzTests/**` glob (`.pipelines/v2/templates/job-fuzz.yml`) picks it up with no pipeline edit. `OneFuzzConfig.json` uses the repo's v3 schema (the shape `PowerRename` uses, not the older `fuzzers[]` example in `fuzzingtesting.md`); the `adoTemplate` `AssignedTo` / notification fields carry the repo defaults and must be set by whoever owns the OneFuzz submission.
- Local run: the ASan toolchain is available via the "C++ AddressSanitizer" VS component. `build_mbl_fuzzer.cmd` (in the gitignored `x64\Release\`) compiles the target with `/fsanitize=address /fsanitize=fuzzer` directly through cl.exe (bypassing the repo's vcpkg-gated msbuild) and copies the ASan runtime DLL beside it. A 26-second run executed 815,727 inputs with zero crashes and no ASan findings.
- Hardening it surfaced: `Engine::CheckMoveCancel` computed the squared cursor displacement as `long long` (`dx*dx + dy*dy`), which can overflow signed 64-bit for extreme coordinates. Production coordinates are screen-bounded so it never triggered, but the comparison is now done in `double` to stay defined across the full coordinate range the fuzzer drives. All unit tests still pass after the change.
- Possible second target (not yet added): the settings-JSON path (`parse_settings` in `dllmain.cpp`). It needs the WinRT JSON parser and SettingsAPI wired in, so it is heavier than the header-only engine target, which already covers the core user-input state machine. The one known sharp edge there, `readInt` accepting a non-finite `GetNamedNumber` result (a NaN slips past the `< min` / `> max` clamp and makes `static_cast<int>` undefined), is now guarded explicitly: non-finite values are rejected and the previous value kept. Raised in PR review (#49279).

## Open items / not yet wired

Done: GPO is fully wired end to end (`gpo.h` constant + getter, GPOWrapper `idl`/`h`/`cpp`, both `ModuleGpoHelper`s, and the ADMX/ADML templates in `src/gpo/assets/`); ESRP signing lists `PowerToys.MouseButtonLock.dll`; and the spell-check dictionary has the new tokens. The in-process DLL is harvested into the installer by the existing `$(Platform)\Release\*.dll` glob (like the other MouseUtils DLLs), so no per-module `.wxs` is needed. A 36x36 settings/dashboard icon (`MouseButtonLock.png`) and a C++ unit-test project (`MouseButtonLock.UnitTests`, 18 tests over the state machine, including left-button coverage) are in place; the test runs in CI via the solution's `Build;Test` target, so no pipeline edit is needed. The left (primary) mouse button is supported alongside right and middle (off by default; see the Settings section), and the fuzz target exercises all three buttons.

Still open, roughly by priority. Items 1-3 and 7 were found by auditing the module against the dev docs and the conventions of the sibling MouseUtils modules; none are runtime blockers, but 1-3 are parity/registration gaps a reviewer will expect closed.

1. **Telemetry registration in `DATA_AND_PRIVACY.md` (done).** Added a Mouse Button Lock section listing `Microsoft.PowerToys.MouseButtonLock_EnableMouseButtonLock` (the enable/disable event from `trace.cpp`), mirroring the sibling entries. No ETW manifest change is needed: modules share the `Microsoft.PowerToys` provider, so this doc is the only cross-module telemetry registry.

2. **Command Palette toggle (decided: skip).** Every sibling MouseUtils module is toggleable from the Command Palette, but those commands fire a module **activation/trigger event** (e.g. `ToggleCursorWrapCommand` -> `Constants.CursorWrapTriggerEvent()`, which the CursorWrap module listens for to run its activation-hotkey logic). Mouse Button Lock has no activation hotkey and no toggle action (activation is the physical hold), and `ModuleEnablementService` is read-only, so there is nothing to mirror cleanly. Settings are already reachable from the palette via the generic "Open in Settings" MouseUtils command, so no per-module command is added. The only per-module command that would be meaningful is a "release currently held buttons" quick-action, which would require a new trigger event in the shared interop layer (`shared_constants.h` + `Constants.idl`/`.h`/`.cpp`, an ABI-sensitive change) plus a module-side listener; left as an optional future feature, not a parity gap.

3. **UI test (partial).** Mouse Button Lock is now included in the `MouseUtils.UITests` restart scope (`FindMyMouseTests.cs`), matching CursorWrap. A dedicated `MouseButtonLockTests.cs` remains a follow-up: no passive MouseUtils sibling (including CursorWrap, the module this was copied from) has one, the shared `MouseUtilsSettings` helper would need a Mouse Button Lock entry (enum + accessibility id + name maps), and authoring/verifying it needs WinAppDriver, which is not available in this environment. The engine is already covered by `MouseButtonLock.UnitTests`; the module's Settings group exposes `AutomationProperties.AutomationId="MouseUtils_MouseButtonLockTestId"` for when the test is written.

4. **Full-solution build + manual runtime validation (done).** The full `PowerToys.slnx` was built x64 Release under Visual Studio 2026 (the repo targets `net10.0`, which needs VS 2026 or a VS 2022 17.14 servicing new enough to drive the .NET 10 SDK; older VS falls back to .NET 9 and fails restore with NETSDK1045). The built `x64\Release\PowerToys.exe` runner loaded the module via `knownModules`, the Settings UI showed the Mouse Button Lock controls including the left-button checkbox, and the lock was confirmed in a real app: with left-button lock on, a hold-past-threshold then release latches the button so a drag/text-selection continues with no button held, and a tap releases it. Toggling the module off mid-lock releases cleanly. (Build note: `build-essentials.cmd` restores + builds runner/Settings.UI; a whole-solution build via `build.ps1` also builds the other module DLLs so the runner starts without "failed to load" dialogs for unbuilt modules.)

5. **Game compatibility validation.** `WH_MOUSE_LL` may not observe or suppress input in titles using Raw Input or exclusive DirectInput. Needs a real-game pass; neither the harness nor the LL hook can prove this.

6. **Release the lock on session lock / fast-user-switch** (`WTSRegisterSessionNotification` -> `ReleaseAll`). The standalone reference app did this; in the module a button locked when the session locks stays logically held until the next physical tap. Minor and self-healing.

7. **`trace.cpp` include style (done).** `trace.cpp` no longer carries the redundant deep relative `#include "../../../../common/Telemetry/TraceBase.h"`; it includes `trace.h`, which already pulls in `<common/Telemetry/TraceBase.h>` via the angle-bracket form the sibling MouseUtils modules use and the project's include dirs resolve. Raised in PR review (#49279).

8. **ETW telemetry** beyond the enable/disable event.

Cross-checks against the `AGENTS.md` validation checklist: no third-party dependencies were added (no `NOTICE.md` change needed); the settings schema is mirrored on both sides (C# `MouseButtonLockProperties` and C++ `parse_settings`); the work from this pass (the `CheckMoveCancel` hardening, the fuzz project, the telemetry and UI-test-scope edits, and this doc) is committed on `feature/mouse-button-lock`; keep the PR atomic and link the proposal issue microsoft/PowerToys#48302.
