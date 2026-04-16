# ShowDesktop Module

## Overview

ShowDesktop brings macOS Sonoma's "click wallpaper to reveal desktop" feature to Windows. When enabled, clicking on empty desktop wallpaper minimizes all windows to reveal the desktop. Clicking again (or clicking any app/taskbar) restores all windows to their previous positions.

## Architecture

ShowDesktop follows the **External Application Launcher** pattern:

```
Runner
  └─ ShowDesktopModuleInterface.dll (C++ interface DLL)
       └─ PowerToys.ShowDesktop.exe (C# application)
```

### Components

| Component | Description |
|-----------|-------------|
| **ShowDesktopModuleInterface** | C++ DLL implementing `PowertoyModuleIface`. Handles module lifecycle (enable/disable) and spawns the C# app. |
| **PowerToys.ShowDesktop.exe** | C# application containing the core desktop-peek logic. Runs a Win32 message loop for the low-level mouse hook. |

### Core C# Classes

| Class | Purpose |
|-------|---------|
| `DesktopPeek` | Core state machine (Idle ↔ Peeking). Orchestrates click detection, window tracking, and restore. |
| `MouseHook` | Low-level mouse hook (`WH_MOUSE_LL`) for detecting clicks on desktop wallpaper. |
| `FocusWatcher` | `WinEventHook(EVENT_SYSTEM_FOREGROUND)` for monitoring foreground window changes. |
| `WindowTracker` | Enumerates, captures, minimizes, restores, and animates windows. |
| `DesktopDetector` | Identifies whether a click landed on desktop wallpaper, desktop icons, or the taskbar. |
| `VirtualDesktopService` | Virtual desktop management via undocumented COM interfaces. |
| `NativeMethods` | P/Invoke declarations for Win32 APIs. |

## Settings

Settings are stored in `%LOCALAPPDATA%\Microsoft\PowerToys\ShowDesktop\settings.json`.

| Setting | Type | Default | Description |
|---------|------|---------|-------------|
| `peek-mode` | int | 0 | 0=Native Show Desktop, 1=Minimize, 2=Fly Away |
| `require-double-click` | bool | false | Require double-click on wallpaper to activate |
| `enable-taskbar-peek` | bool | false | Also trigger peek from empty taskbar space |
| `enable-gaming-detection` | bool | true | Auto-pause during fullscreen gaming |
| `fly-away-animation-duration-ms` | int | 300 | Animation duration for Fly Away mode (ms) |

## Peek Modes

- **Native Show Desktop (0)**: Uses the built-in Win+D mechanism via `SendInput`.
- **Minimize (1)**: Minimizes each window individually, preserving exact `WINDOWPLACEMENT` state for restore.
- **Fly Away (2)**: Animated offscreen movement of windows, then restore with reverse animation.

## IPC

Communication between the interface DLL and the C# app uses Windows named events:

- `SHOW_DESKTOP_TERMINATE_EVENT`: Signaled by the interface DLL to terminate the C# app on disable.

## GPO Policy

Registry value: `ConfigureEnabledUtilityShowDesktop` under `SOFTWARE\Policies\PowerToys`

## Credits

Based on [PeekDesktop](https://github.com/shanselman/PeekDesktop) by Scott Hanselman.
