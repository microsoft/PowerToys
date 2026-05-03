# Macro

## Quick Links

[All Issues](https://github.com/microsoft/PowerToys/issues?q=is%3Aopen%20label%3A%22Product-Macro%22)<br>
[Bugs](https://github.com/microsoft/PowerToys/issues?q=is%3Aopen%20label%3AIssue-Bug%20label%3A%22Product-Macro%22)<br>
[Pull Requests](https://github.com/microsoft/PowerToys/pulls?q=is%3Apr+is%3Aopen+label%3A%22Product-Macro%22)

## Overview

Macro lets users record and replay keyboard sequences triggered by a configurable hotkey. Each macro is a named, ordered list of steps (key press, type text, wait, repeat) scoped optionally to a specific application.

## Architecture

Three projects:

| Project | Type | Purpose |
|---------|------|---------|
| `MacroCommon` | .NET class library | Shared models (`MacroDefinition`, `MacroStep`, `StepType`), AOT-compatible JSON serializer (`MacroJsonContext`), IPC contract (`IMacroEngineRpc`) |
| `MacroEngine` | .NET executable | Background host process: hotkey registration via `RegisterHotKey`, step execution via `SendInput`, IPC server over named pipe |
| `MacroModuleInterface` | C++ DLL | PowerToys Runner integration — implements `PowertoyModuleIface`, launches/stops MacroEngine |

## Implementation Details

### Hotkey Registration

`HotkeyManager` registers hotkeys with `RegisterHotKey` on a WinForms message pump thread. When a hotkey fires, it resolves the matching `MacroDefinition` and dispatches `MacroExecutor.ExecuteAsync`.

### Step Execution

`MacroExecutor` iterates `MacroDefinition.Steps` and dispatches each step:

| StepType | Implementation |
|----------|---------------|
| `PressKey` | `SendInputHelper.PressKeyCombo` — builds a `SendInput` sequence for modifier + key down/up |
| `TypeText` | `SendInputHelper.TypeText` — one `SendInput` pair per character via `VK_PACKET` |
| `Wait` | `Task.Delay(ms)` |
| `Repeat` | Iterates sub-steps `Count` times |

### Macro Storage

Macros are stored as JSON at `%APPDATA%\Microsoft\PowerToys\Macro\macros.json`. `MacroLoader` watches this file with `FileSystemWatcher` and hot-reloads on change.

### IPC

Settings UI communicates with MacroEngine over a named pipe using `IMacroEngineRpc`. The engine hosts `MacroRpcServer`; the Settings UI calls methods to enable/disable macros and trigger reload.

### App Scope

`AppScopeChecker` compares the focused window's process name against `MacroDefinition.AppScope`. A null scope means the macro is global.

## Settings UI

Settings UI (`MacroPage`, `MacroEditDialog`) lives in `src/settings-ui/Settings.UI`. The MVVM layer (`MacroViewModel`, `MacroEditViewModel`, `MacroStepViewModel`) maps between `MacroDefinition` and UI state. Hotkey capture uses `MacroHotkeyControl` (a `UserControl` wrapping `HotkeySettingsControlHook`).

## Data Format

```json
{
  "id": "...",
  "name": "Open Terminal",
  "hotkey": { "win": false, "ctrl": true, "alt": false, "shift": false, "code": 120 },
  "appScope": null,
  "isEnabled": true,
  "steps": [
    { "type": "press_key", "key": "Ctrl+Alt+T" }
  ]
}
```
