# Scroll Acceleration

## Overview

Scroll Acceleration multiplies the mouse wheel delta when the wheel is spun quickly and continuously,
so long documents and pages can be traversed fast while slow turns stay precise. It is off by default.

## Code structure

| Location | Responsibility |
|---|---|
| `src\modules\MouseUtils\ScrollAcceleration\AccelerationCalculator.h` | Pure acceleration logic (no Win32), unit-testable |
| `src\modules\MouseUtils\ScrollAcceleration\main.cpp` | Out-of-process exe: `WH_MOUSE_LL` hook, wheel re-injection, settings reload |
| `src\modules\MouseUtils\ScrollAccelerationModuleInterface` | Runner module interface and exe lifecycle |
| `src\settings-ui\Settings.UI\SettingsXAML\Views\MouseUtilsPage.xaml` | Settings UI: a "Scroll Acceleration" group inside the Mouse utilities page (view model: `ScrollAccelerationViewModel`) |

## How it works

1. The low-level mouse hook sees `WM_MOUSEWHEEL` (and optionally `WM_MOUSEHWHEEL`).
2. `AccelerationCalculator` tracks the streak of ticks in the same direction that arrive within the
   configured time window. The multiplier is `min(base + acceleration * streak, max)`, where `base` is the slow scroll distance (below 1 makes isolated ticks fine and precise). Changing direction or
   pausing longer than the window resets the streak.
3. When the multiplier is not 1 the original event is swallowed and a scaled wheel event is injected with
   `SendInput`. Injected events carry a marker in `dwExtraInfo`, and all injected input is ignored by the hook
   to prevent recursion.
4. The hot path does no logging or allocation.
5. Optional smooth scroll (`momentum_enabled` in settings.json, shown as "Smooth scroll"). It has two parts.
   `SmoothingCalculator` queues each delivered tick and releases it in small ease-out steps from a 16 ms timer
   on the injector thread, so content glides instead of jumping; an opposite tick drops the queued remainder.
   `MomentumCalculator` adds inertia: `MomentumCalculator` estimates the wheel velocity from the delivered ticks. After three or
   more quick ticks in the same direction, once the wheel has been quiet for the acceleration window, the injector thread
   (16 ms timer) injects decaying wheel deltas until the configured duration ends. Any new physical tick, a direction
   change or Ctrl cancels it. Momentum events carry the same marker, so they never feed back into the hook.

## Settings (`settings.json` properties)

| Property | Default | Meaning |
|---|---|---|
| `time_threshold_ms` | 100 | Max gap between ticks to count as continuous (10-1000) |
| `slow_scroll_percent` | 15 | Distance of a single slow tick in percent of normal (5-100); acceleration ramps up from here |
| `acceleration_percent` | 100 | Multiplier added per consecutive tick, in percent (0-500) |
| `max_multiplier` | 8 | Upper bound for the multiplier (1-50) |
| `apply_to_horizontal_wheel` | false | Also accelerate `WM_MOUSEHWHEEL` |
| `ignore_with_ctrl` | true | Skip acceleration while Ctrl is held (zoom) |
| `exclude_app_shortcut` | Win+Ctrl+Alt+X | Adds / removes the foreground app in `excluded_apps` (a short beep confirms) |
| `excluded_apps` | empty | Executable names, one per line, for which the foreground app is left untouched |
| `activation_shortcut` | Win+Ctrl+Alt+W | Pauses / resumes acceleration and momentum without disabling the utility |
| `momentum_enabled` | false | "Smooth scroll": glide every tick and keep gliding after a fast spin |
| `momentum_duration_ms` | 600 | How long the glide lasts after the wheel stops (100-3000) |

## Known interactions

- Low-level hooks run in LIFO order. The re-injected event is visible to other hooks (FancyZones, Keyboard
  Manager, Grab and Move ignores injected events), so wheel-driven features that depend on non-injected input
  may not see accelerated ticks.
- The process must run at the same integrity level as the runner so elevated windows also scroll accelerated.

## Testing

- Unit tests: `src\modules\MouseUtils\ScrollAcceleration.UnitTests` (acceleration, smoothing and momentum logic).
- Fuzzing: `src\modules\MouseUtils\ScrollAcceleration.FuzzTests` (libFuzzer + ASan over the calculator).
- End to end: `src\modules\MouseUtils\ScrollAcceleration.E2ETests\E2EWheelTest.py` runs a test-only exe build
  (`SCROLLACCELERATION_TEST_ACCEPT_INJECTED`) and checks the accelerated wheel events with a real hook.
- `SendInput` is never called from inside the hook callback (it would stall for the hook timeout); a dedicated
  injector thread re-injects the scaled ticks in order.
