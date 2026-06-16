# Mouse Button Lock

Mouse Button Lock is a [ClickLock](https://support.microsoft.com/windows/make-the-mouse-easier-to-use-10733da7-6fb8-4ddf-9b30-9a72b814f5d5) equivalent for the **right** and **middle** mouse buttons. Windows ships ClickLock for the left button only; this module closes that gap for the secondary and middle buttons.

## Behavior

For each enabled button (RMB and/or MMB):

1. Hold the button for at least the configured hold duration, then release.
2. The physical button-up is suppressed inside the low-level mouse hook, so the OS keeps perceiving the button as held.
3. The next physical tap of that button releases the synthetic hold (and that tap is swallowed cleanly, so downstream apps only ever see the injected up).

If "cancel on move" is enabled and the cursor moves beyond the dead-zone *before* the hold threshold elapses, the gesture is treated as a drag and does not lock. Once the threshold elapses the lock is armed and further motion no longer prevents it, so a held-button camera drag (the primary gaming use case) still locks.

## Architecture

This is a native C++ in-process module, like the other Mouse Utilities (Find My Mouse, Mouse Highlighter, Mouse Pointer Crosshairs, CursorWrap). The C++ implementation was ported from the standalone reference app at https://github.com/owenpkent/windows-right-click-lock.

- `dllmain.cpp` implements `PowertoyModuleIface`. `enable()` spins up a dedicated thread that installs `WH_MOUSE_LL` and runs a message pump (low-level hook callbacks are delivered to the installing thread). `disable()` signals a terminate event, joins the thread, and releases any locked button.
- The hook callback runs a small per-button state machine. It suppresses the matching button-up (`WM_RBUTTONUP` / `WM_MBUTTONUP`) by returning `1`, and injects the synthetic release (`MOUSEEVENTF_RIGHTUP` / `MOUSEEVENTF_MIDDLEUP`) via `SendInput`.
- Self-injection tag: every injected event sets `dwExtraInfo = 0x57494E4D` (`'WINM'`). The hook ignores events carrying this tag so it never recurses on its own synthetic input.

## Settings

Stored at `%LOCALAPPDATA%\Microsoft\PowerToys\MouseButtonLock\settings.json`. Properties (snake_case keys must match between the C# `MouseButtonLockProperties` and the C++ `parse_settings`):

| Key | Type | Default | Meaning |
| --- | --- | --- | --- |
| `rmb_lock_enabled` | bool | `true` | Lock the right mouse button |
| `mmb_lock_enabled` | bool | `false` | Lock the middle mouse button |
| `hold_duration_ms` | int | `300` | How long to hold before locking |
| `move_cancel_enabled` | bool | `true` | Cancel locking if the cursor moves during the hold |
| `move_cancel_pixels` | int | `5` | Dead-zone radius for move-cancel |

The module on/off state lives in `EnabledModules.MouseButtonLock` in the global settings, not in the per-module properties. The settings UI is a section on the shared Mouse Utilities page (`MouseUtilsPage.xaml` / `MouseUtilsViewModel.cs`). There is no activation hotkey: activation is the physical hold.

## Safety

A logically-locked button whose up was suppressed leaves the OS believing the button is held. The module injects a synthetic up for every locked button on `disable()` and on graceful hook-thread shutdown, so toggling the module off or exiting PowerToys releases cleanly. A hard `TerminateProcess` of the runner mid-lock is the residual risk (same class of risk as the standalone reference app); see the open items below.

## Building and debugging

Mouse Button Lock builds as part of the PowerToys solution. The runtime is a native C++ DLL (`PowerToys.MouseButtonLock.dll`); the settings live in the shared C# Settings UI.

- Full build: open `PowerToys.slnx` in Visual Studio 2022 (Desktop C++, WinUI, and .NET desktop workloads) and build `x64`/`Release` (or `Debug`). The module DLL is emitted to `<repo>\x64\<Config>\PowerToys.MouseButtonLock.dll`, which is where the runner loads it from (`knownModules` in `src/runner/main.cpp`).
- Native module only (after a NuGet restore): `msbuild src/modules/MouseUtils/MouseButtonLock/MouseButtonLock.vcxproj /p:Platform=x64 /p:Configuration=Release`.
- Debugging: the hook runs inside the runner, so start or attach the debugger to `PowerToys.exe` and set breakpoints in `dllmain.cpp`. Enable the module from the Mouse Utilities settings page, then exercise the lock with a real right or middle button hold. Keep the hook callback non-blocking: a slow callback risks `LowLevelHooksTimeout` eviction by Windows.

## Open items / not yet wired

Done: GPO is fully wired end to end (`gpo.h` constant + getter, GPOWrapper `idl`/`h`/`cpp`, both `ModuleGpoHelper`s, and the ADMX/ADML templates in `src/gpo/assets/`); ESRP signing lists `PowerToys.MouseButtonLock.dll`; and the spell-check dictionary has the new tokens. The in-process DLL is harvested into the installer by the existing `$(Platform)\Release\*.dll` glob (like the other MouseUtils DLLs), so no per-module `.wxs` is needed.

Still open, and intentionally deferred until the approach is agreed on the proposal issue (microsoft/PowerToys#48302):

- A dashboard/settings icon asset (`src/settings-ui/Settings.UI/Assets/Settings/Icons/MouseButtonLock.png`, 32px, matching the sibling Mouse* icons). Referenced by the settings card and the dashboard tile; renders blank until added.
- ETW telemetry beyond the enable/disable event, and unit tests for the per-button state machine.
- Fuzzing coverage (PowerToys requires fuzzing for user-input modules).
- Game compatibility validation: `WH_MOUSE_LL` may not observe or suppress input in titles using Raw Input or exclusive DirectInput.
- Release the lock on session lock / fast-user-switch (`WTSRegisterSessionNotification` -> `ReleaseAllLocked`). The standalone reference app did this; in the module a button locked when the session locks stays logically held until the next physical tap. Minor and self-healing, tracked as a parity follow-up.
- A full x64 Release build of `PowerToys.slnx` to confirm the module compiles, links, loads in the runner, and that the DLL lands in the harvested Release root.
