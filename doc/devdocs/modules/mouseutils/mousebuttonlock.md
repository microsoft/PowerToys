# Mouse Button Lock

Mouse Button Lock is a [ClickLock](https://support.microsoft.com/windows/make-the-mouse-easier-to-use-10733da7-6fb8-4ddf-9b30-9a72b814f5d5) equivalent for the **left**, **right**, and **middle** mouse buttons. Windows ships ClickLock for the left button only; this module closes that gap for the secondary and middle buttons, and can optionally cover the left (primary) button too, as a one-stop alternative to the built-in Windows ClickLock. Because the left button is the primary interaction button (and Windows already provides ClickLock for it), left-button locking is **off by default** and strictly opt-in.

## Behavior

For each enabled button (LMB, RMB, and/or MMB):

1. Hold the button for at least the configured hold duration, then release.
2. The physical button-up is suppressed inside the low-level mouse hook, so the OS keeps perceiving the button as held (the original click never completes). Moving the mouse then performs a true drag: dragging a window or file, marquee-selecting, orbiting a 3D/CAD camera, or selecting text.
3. Pressing **any** button releases the hold. The module injects the held button's synthetic up (and, when releasing via the *same* button, suppresses that release tap). Because the release is a real up injected cleanly (see deferred injection below), a text selection or drag made during the hold survives it.

Releasing on any button press (not only a same-button tap) is deliberate: a held button otherwise leaves the mouse "stuck" (its capture blocks other clicks) until you tap the exact same button. So a left-click frees a right-lock, etc.

This is a "hands-free drag" accessibility feature (for users with motor disabilities, RSI, or tremors), modeled on the reference project https://github.com/owenpkent/linux-quickdrag. It generalizes Windows' built-in ClickLock, which covers the left button only, to the right and middle buttons.

**Right-button caveat (context menu).** Releasing a right-button lock necessarily emits a right-button-up, which applications answer with a context menu (that is just what a right-click is). To keep hands-free right-drag usable, the module injects an `Esc` immediately after the release up to dismiss that menu. Normal quick right-clicks never lock and are untouched, so context menus still work; only a lock *release* auto-dismisses. The trade-off is that a genuine right-drag-drop menu (e.g. Explorer's copy/move) is also dismissed.

A moving hold does not lock. Any cursor move beyond the dead-zone during the hold marks the gesture as a drag (text selection, window or file drag), cancels the pending lock, and lets the button-up pass through normally. The "Drag threshold (pixels)" value (`move_cancel_pixels`) is the dead-zone that separates hand jitter from a deliberate drag: a larger value tolerates more hand tremor during the hold before the gesture is treated as a drag. Motion after the button actually locks never cancels, because `physicalDown` is already false by then, so a genuine lock-then-drag is unaffected.

(An earlier revision had an "Allow a held drag to lock" toggle that let a sustained hold-and-drag still lock, for a hands-free gaming camera pan. It was removed: this is an accessibility feature, the gaming case was speculative, and "on" could latch a lock during a slow or tremor-laden drag, which is exactly what the default guards against.)

## Architecture

This is a native C++ in-process module, like the other Mouse Utilities (Find My Mouse, Mouse Highlighter, Mouse Pointer Crosshairs, CursorWrap). The C++ implementation was ported from the standalone reference app at https://github.com/owenpkent/windows-right-click-lock.

- `dllmain.cpp` implements `PowertoyModuleIface`. `enable()` spins up a dedicated thread that installs `WH_MOUSE_LL` and runs a message pump (low-level hook callbacks are delivered to the installing thread). `disable()` signals a terminate event, joins the thread, and releases any locked button.
- The hook callback runs a small per-button state machine. On lock it suppresses the matching button-up (returns `1`), so the button stays held with the original click never completing. Any button-down releases every held button by injecting its synthetic up (`MOUSEEVENTF_LEFTUP` / `RIGHTUP` / `MIDDLEUP`); a same-button release tap is additionally suppressed and its paired up swallowed.
- **Deferred injection (critical):** `SendInput` is **not** called synchronously inside the hook callback. Doing so lets the very event being suppressed leak to applications (it reads as a stray click that collapses a text selection). Instead `WinInjector` posts a `WM_MBL_INJECT` thread message back to the hook thread; the message loop performs the `SendInput` after the callback has returned and the physical event is fully suppressed. On shutdown the target thread is cleared so `ReleaseAll`'s injections run inline (the loop is gone by then, and we are no longer inside a callback, so a direct `SendInput` is safe).
- **Context-menu dismiss:** after injecting a right-button up (always a lock release), `WinInjector` injects an `Esc` so the context menu the up would open is dismissed (see the right-button caveat under Behavior).
- Self-injection tag: every injected event sets `dwExtraInfo = 0x57494E4D` (`'WINM'`). The hook ignores events carrying this tag so it never recurses on its own synthetic input (the injected `Esc` is a keyboard event and the module hooks only the mouse, so it can't feed back either).
- The decision logic lives in `MouseButtonLockCore.h` as a Win32-free `Engine` (the clock is passed in and synthetic-up injection is behind the `IButtonUpInjector` interface), so `dllmain.cpp` is a thin Win32 adapter and the state machine is unit tested by `MouseButtonLock.UnitTests` (`EngineTests.cpp`).

## Settings

Stored at `%LOCALAPPDATA%\Microsoft\PowerToys\MouseButtonLock\settings.json`. Properties (snake_case keys must match between the C# `MouseButtonLockProperties` and the C++ `parse_settings`):

| Key | Type | Default | Meaning |
| --- | --- | --- | --- |
| `lmb_lock_enabled` | bool | `false` | Lock the left (primary) mouse button. Off by default; overlaps Windows' own left-button ClickLock |
| `rmb_lock_enabled` | bool | `true` | Lock the right mouse button |
| `mmb_lock_enabled` | bool | `false` | Lock the middle mouse button |
| `hold_duration_ms` | int | `1200` | How long to hold before locking. Default and range mirror Windows' built-in ClickLock (1200 ms, 200-2200 ms) |
| `move_cancel_pixels` | int | `5` | Drag threshold: dead-zone radius separating hand jitter from a deliberate drag. A move beyond it during the hold is treated as a drag and cancels the pending lock |

The module on/off state lives in `EnabledModules.MouseButtonLock` in the global settings, not in the per-module properties. The settings UI is a section on the shared Mouse Utilities page (`MouseUtilsPage.xaml` / `MouseUtilsViewModel.cs`). There is no activation hotkey: activation is the physical hold.

## Settings UI

Mouse Button Lock is a section on the shared **Settings > Mouse utilities** page (`MouseUtilsPage.xaml`, bound to `MouseUtilsViewModel`). The enable toggle is its own card; everything else lives in a "Buttons and behavior" expander that is greyed out until the module is on. Expanding it shows the button checkboxes, the hold-duration slider, and the drag-threshold box, plus an InfoBar recommending the right and middle buttons and pointing to Windows' built-in ClickLock (Control Panel > Mouse > Buttons) for the left button. The InfoBar sits as a sibling just below the expander with its `Visibility` bound to the expander's `IsExpanded` (through `BoolToVisibilityConverter`), so it appears only while the section is open. It is deliberately **not** an item inside `SettingsExpander.Items`: that compiles but throws at runtime when the item is realized (i.e. when the section is first expanded), so an `InfoBar` cannot be hosted there.

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
|   Drag threshold (pixels)                                [     5  ^v ] |
|      Movement beyond this distance during the hold is treated as a     |
|      drag and will not lock.                                           |
+------------------------------------------------------------------------+

+------------------------------------------------------------------------+
|  (i)  Recommended for the right and middle buttons.                    |
|       Windows already includes ClickLock for the left button;          |
|       turn it on in Control Panel > Mouse > Buttons.                   |
+------------------------------------------------------------------------+
        ^ InfoBar shown only while "Buttons and behavior" is expanded
```

Legend: `[icon]` module icon, `( On =O )` toggle switch, `[v]` expanded section, `[x]`/`[ ]` checked/unchecked checkbox, `[=====O======]` slider with its live value label (e.g. `1200 ms`), `[ 5 ^v ]` number box with spinner, `(i)` informational InfoBar (shown only while the expander is open). The inner controls show the shipping defaults; the master toggle is drawn On (its default is off) so the expander is not greyed out.

| Control | Type (UI range) | Setting key | ViewModel property | Default |
| --- | --- | --- | --- | --- |
| Mouse Button Lock | toggle | `EnabledModules.MouseButtonLock` | `IsMouseButtonLockEnabled` | off |
| Lock the left (primary) mouse button | checkbox | `lmb_lock_enabled` | `MouseButtonLockLmbEnabled` | off |
| Lock the right mouse button | checkbox | `rmb_lock_enabled` | `MouseButtonLockRmbEnabled` | on |
| Lock the middle mouse button | checkbox | `mmb_lock_enabled` | `MouseButtonLockMmbEnabled` | off |
| Hold duration (ms) | slider (200-2200 ms, snaps in 100 ms steps) | `hold_duration_ms` | `MouseButtonLockHoldDurationMs` | 1200 |
| Drag threshold (pixels) | number box (0-100, step 1) | `move_cancel_pixels` | `MouseButtonLockMoveCancelPixels` | 5 |

The UI slider snaps (`SnapsTo="StepValues"`, `StepFrequency="100"`) to 100 ms increments across the 200-2200 ms range (Short = 200 ms, Long = 2200 ms, 1200 ms default). This is finer than Windows' built-in ClickLock slider (which uses 200 ms notches) so shorter holds such as 300 ms are reachable from the UI. Visual tick marks were dropped (they looked cluttered inside the settings card, and `SnapsTo` gives the notch-to-notch feel without them); the live "N ms" label beside the track (`MillisecondsLabelConverter`) shows the current value. The C++ `parse_settings` clamps a hand-edited file to 200-60000 ms (same 200 ms floor, a looser ceiling) and accepts any value in that range, so a hand-edited file is not restricted to the 11 slider steps. The group is GPO-aware: when policy forces the module on or off, the enable toggle is disabled and a `GPOInfoControl` warning shows (`IsMouseButtonLockEnabledGpoConfigured`). For UI tests, the Settings group exposes `AutomationProperties.AutomationId="MouseUtils_MouseButtonLockTestId"`, and the hold-duration slider and the move-cancel number box carry `MouseUtils_MouseButtonLockHoldDurationId` and `MouseUtils_MouseButtonLockMoveCancelPixelsId`.

## Safety

A logically-locked button whose up was suppressed leaves the OS believing the button is held. The module injects a synthetic up for every locked button on `disable()` and on graceful hook-thread shutdown, so toggling the module off or exiting PowerToys releases cleanly. A hard `TerminateProcess` of the runner mid-lock is the residual risk (it would strand the button in the held state); see the open items below.

## Building and debugging

Mouse Button Lock builds as part of the PowerToys solution. The runtime is a native C++ DLL (`PowerToys.MouseButtonLock.dll`); the settings live in the shared C# Settings UI.

- Full build: open `PowerToys.slnx` in Visual Studio 2022 (Desktop C++, WinUI, and .NET desktop workloads) and build `x64`/`Release` (or `Debug`). The module DLL is emitted to `<repo>\x64\<Config>\PowerToys.MouseButtonLock.dll`, which is where the runner loads it from (`knownModules` in `src/runner/main.cpp`).
- Native module only (after a NuGet restore): `msbuild src/modules/MouseUtils/MouseButtonLock/MouseButtonLock.vcxproj /p:Platform=x64 /p:Configuration=Release`.
- Debugging: the hook runs inside the runner, so start or attach the debugger to `PowerToys.exe` and set breakpoints in `dllmain.cpp`. Enable the module from the Mouse Utilities settings page, then exercise the lock with a real right or middle button hold. Keep the hook callback non-blocking: a slow callback risks `LowLevelHooksTimeout` eviction by Windows.

Build status: build- and runtime-verified. The module builds under Visual Studio 2026 with the .NET 10 toolset the repo now targets (Visual Studio 2022 / MSBuild 17.x cannot build the .NET 10 managed projects; use the VS 2026 / MSBuild 18 toolset), linking `PowerToys.MouseButtonLock.dll` (exporting `powertoy_create`), and all 18 `MouseButtonLock.UnitTests` pass. At runtime the built runner loads the module via `knownModules`, the Settings UI shows the controls, and the lock latches/releases correctly in real apps: a text selection made during a hands-free left-drag survives release, any button press frees a held button, and a right-button hands-free drag releases without leaving a context menu (all verified live in Notepad and Chrome).

## Runtime verification

The unit tests cover the Win32-free `Engine`. The Win32 glue in `dllmain.cpp` (hook install, the message pump, the deferred `WM_MBL_INJECT` injection, suppression of the up by returning `1`, the `Esc` context-menu dismiss, and the injection-tag self-filter) was verified live against the running runner with temporary `MBL`-tagged trace logging in `dllmain.cpp` (since removed):

- `GetAsyncKeyState(VK_LBUTTON)` reads held only while the OS believes the button is down: it stays held through the drag and clears on release, a direct proxy for "the suppressed up took effect".
- The drag-select collapse (a text selection made during the hold vanishing on release) traced to `SendInput` being called synchronously inside the hook callback, which leaked the suppressed release click to the app. Deferring the injection to the hook thread's message loop (see Architecture) fixed it; the selection is now preserved.
- The RMB "left-click gets stuck" report traced to the lock only releasing on a same-button tap, so other clicks passed through but the still-held right button blocked them. Fixed by releasing every held button on any button-down. The context menu that a right release then produced is cleared by the injected `Esc`. Verified live in Notepad and Chrome.

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

9. **Elevated and UIAccess windows (known limitation).** The module runs inside the runner, which is medium integrity by default. Windows' UIPI (User Interface Privilege Isolation) then blocks the module from crossing the privilege boundary in both directions: a medium-integrity low-level hook cannot suppress the physical button-up headed to a higher-integrity window, and `SendInput` cannot inject the release up into it. So the lock never engages over windows running at a higher integrity level:
    - **Apps launched elevated / as administrator** (Device Manager and other `mmc.exe` snap-ins, Registry Editor, Task Manager, etc.). **Workaround:** run PowerToys as administrator (General settings > "Always run as administrator"), which raises it to high integrity and restores the lock over these apps.
    - **UIAccess accessibility apps** (the Windows On-Screen Keyboard `osk.exe`, and typically third-party on-screen keyboards). These sit *above* even an elevated admin process for input purposes, so running PowerToys as administrator does **not** cover them. Making them work needs the module (or the runner) to carry the UIAccess manifest flag, which requires a signed binary in a protected path (Program Files) plus a manifest change. This is the same unresolved constraint Keyboard Manager documents (see `doc/devdocs/modules/keyboardmanager/keyboardmanager.md` "UIPI Issues (not resolved)", tracking issues microsoft/PowerToys#3192 and #3255); Mouse Button Lock inherits it by running in the runner process.

Cross-checks against the `AGENTS.md` validation checklist: no third-party dependencies were added (no `NOTICE.md` change needed); the settings schema is mirrored on both sides (C# `MouseButtonLockProperties` and C++ `parse_settings`); the work from this pass (the `CheckMoveCancel` hardening, the fuzz project, the telemetry and UI-test-scope edits, and this doc) is committed on `feature/mouse-button-lock`; keep the PR atomic and link the proposal issue microsoft/PowerToys#48302.
