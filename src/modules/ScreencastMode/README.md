
# Microsoft PowerToys - Screencast Mode

## What is Screencast Mode

Screencast Mode is a new utility within PowerToys that allows users to Visualize their Keystrokes. The module and its design was influenced by [Issue 981 from the PowerToys Repository](https://github.com/microsoft/PowerToys/issues/981).  

## Features
- Visualize Keystrokes in an On-Screen Overlay
- Customizable Overlay Position
- Customizable Overlay Text Size
- Customizable Overlay Background and Text Color
- Preview Window to see changes in real-time
- Enable/Disable Overlay with a Keyboard Shortcut


## Known Bugs/Notes:
- The size of the text in the preview window is not entirely accurate when compared to the overlay text size
- When an application is maximized from the taskbar, the overlay window might go behind it. This can be fixed by restarting the overlay
- Currently the "Learn More..." on the Screencast Mode settings page links to the general PowerToys page
- The "Welcome to PowerToys" page does not include ScreencastMode
- There are currently no Unit Tests set up for Screencast Mode

## Implementation Details

### The Screencast Mode Settings
Much like the other PowerToys modules, Screencast Mode's Settings are implemented in the `Settings.UI` and `Settings.UI.Library` folders. In the Settings.UI.Library, there are the `ScreencastModeSettings` and `ScreencastModeProperties` classes. The `ScreencastModeSettings`  implements `ISettingsConfig` Interface and inherits from `BasePTModuleSettings`, primarily to set up the JSON serialization. The `ScreencastModeProperties` class holds the actual settings that are bound to the UI elements in the Settings.UI project, and also sets up their default values.

In the `Views` folder of the `Settings.UI` project, there is the `ScreencastModePage.xaml` file which contains the XAML code for the settings page, and the `ScreencastModePage.xaml.cs` file which contains the code-behind for the settings page. The code-behind file was kept minimal. We also modified the `ShellPage.xaml` file to add a navigation item for the Screencast Mode settings page. To add all the buttons and descriptions, we had to add elements to `Resources.resw` file. We tried to follow convention as much as possible when naming the resources.

Last but not at least, we designed assets for Screencast Mode, which can be found in the `Settings.UI/Icons` folder and the preview image is in the `Settings.UI/Modules` folder.

### The Screencast Mode Overlay

The overlay is implemented as a WinUI 3 application in the `ScreencastModeUI` project.

#### Project Structure

| File/Folder | Purpose |
|-------------|---------|
| `App.xaml` / `App.xaml.cs` |Entry point and sets up the Logger |
| `MainWindow.xaml` / `MainWindow.xaml.cs` | Overlay window UI and display logic |
| `Keyboard/KeyboardListener.cs` | Low-level keyboard hook to capture system-wide keystrokes |
| `Keyboard/KeyboardEventArgs.cs` | Event arguments for keyboard events |
| `Keyboard/KeyDisplayer.cs` | Formats and manages the display of captured keystrokes |
| `Assets/ScreencastMode/` | Module icons and visual assets |

#### Architecture

1. **Keyboard Capture**: `KeyboardListener` uses Windows low-level keyboard hooks to intercept keystrokes globally, even when other applications have focus. We don't believe Keystroke capture is possible without importing the DLLs from Win32. We took inspiration from [an old blog post on making a low level keyboard hook in C#](https://learn.microsoft.com/en-us/archive/blogs/toub/low-level-keyboard-hook-in-c).

2. **Key Processing**: `KeyDisplayer` receives raw key events and converts them into human-readable text (handling modifiers like Ctrl, Alt, Shift, and special keys). The events are received via the Virtual Key Codes, which are then translated to their string representations.

3. **Overlay Window**: `MainWindow` renders an always-on-top, click-through window that displays the formatted keystrokes. It gets the settings from the Settings.JSON using methods that are similar to those used in other PowerToys modules. We had to add Win32 APIs here so that the window would not show up as an application on Task Manager, would be click through, and would stay on top of other windows.


#### KeyDisplayer Implementation Details

The `KeyDisplayer` class (`Keyboard/KeyDisplayer.cs`) manages keystroke state and
builds display strings optimized for on-screen presentation.

**State Tracking:**
| Field | Type | Purpose |
|-------|------|---------|
| `_displayedKeys` | `List<string>` | Ordered list of keys/separators to display |
| `_activeModifiers` | `HashSet<VirtualKey>` | Currently held modifier keys |
| `_needsPlusSeparator` | `bool` | Whether next key needs a `+` prefix |

**Key Processing Flow:**

```
KeyDown Event
    │
    ├─► Modifier Key? ──► Normalize (LeftShift → Shift)
    │                     └─► Add to _activeModifiers if new
    │                     └─► Append to display with "+"
    │
    ├─► Clear Key (Backspace/Esc)? ──► Clear display, show key alone
    │
    └─► Regular Key ──► Check overflow (>40 chars)
                        └─► If overflow: clear but re-add held modifiers
                        └─► Append key with "+" if modifiers active
```

**Modifier Normalization:**
Left/right modifier variants are normalized to generic forms for cleaner display:
- `LeftShift` / `RightShift` → `Shift`
- `LeftControl` / `RightControl` → `Control`
- `LeftMenu` / `RightMenu` → `Menu` (Alt)
- `LeftWindows` / `RightWindows` → `LeftWindows`

**Display Name Mapping:**
Keys are mapped to short, readable names optimized for readability:
- Unknown keys fall back to `PowerToys.Interop.LayoutMapManaged`; This is great for graceful handling in case some keys were missed during implementation. We decided not to use this for every key because some of the names are too long for screencast display.

**Overflow Handling:**
When display text exceeds ~40 characters, the display clears but preserves
currently held modifiers. This ensures continuous typing doesn't create an
unreadable string while maintaining modifier context. In the future, it is would be great to make the max character/word count configurable.

**Event Model:**
The `DisplayUpdated` event fires after each key-down, allowing the UI to refresh
via data binding or direct subscription.


#### Dependencies

- WinUI 3 / Windows App SDK
- Shared PowerToys libraries (`ManagedCommon`, `Settings.UI.Library`)
- Windows low-level keyboard hooks (Win32 interop)


### Screencast Mode Module Interface

The module interface (`ScreencastModeModuleInterface`) is a C++ DLL that integrates
Screencast Mode with the PowerToys Runner. It follows the standard PowerToys module
pattern by implementing `PowertoyModuleIface`.

#### Project Structure

| File | Purpose |
|------|---------|
| `dllmain.cpp` | Module implementation and `PowertoyModuleIface` |

There trace provide and precompiled header files follow standard PowerToys conventions, and we did not change them much.

#### Module Lifecycle

```
PowerToys Runner
    │
    ├─► LoadLibrary() ──► DllMain(DLL_PROCESS_ATTACH)
    │                     └─► Trace::RegisterProvider()
    │
    ├─► powertoy_create() ──► new ScreencastMode()
    │                         └─► init_settings()
    │                         └─► parse_hotkey()
    │
    ├─► enable() ──► Launch UI process (skipped on first enable/startup)
    │
    ├─► on_hotkey() ──► Toggle overlay visibility
    │
    ├─► disable() ──► Terminate UI process
    │
    └─► destroy() ──► Cleanup and delete
```

#### Key Implementation Details

**Module Registration:**
The DLL exports `powertoy_create()` which the Runner calls to instantiate the module:

```cpp
extern "C" __declspec(dllexport) PowertoyModuleIface* __cdecl powertoy_create()
{
    return new ScreencastMode();
}
```

**Settings & Hotkey Parsing:**
Settings are loaded from the standard PowerToys settings JSON file using
`PowerToysSettings::PowerToyValues`. The hotkey configuration is parsed from:

```json
{
  "properties": {
    "ScreencastModeShortcut": {
      "win": true,
      "alt": true,
      "ctrl": false,
      "shift": false,
      "code": 83
    }
  }
}
```

Default hotkey: `Win + Alt + S` (0x53 = 'S')

**Process Management:**
The module spawns the WinUI 3 overlay as a separate process:

| Method | Behavior |
|--------|----------|
| `launch_process()` | Starts `WinUI3Apps\PowerToys.ScreencastModeUI.exe` via `ShellExecuteExW` |
| `terminate_process()` | Graceful shutdown with 1s timeout, then `TerminateProcess` |
| `is_viewer_running()` | Checks process handle with `WaitForSingleObject` |

The Runner's PID is passed as a command-line argument to the UI process.

**First-Enable Behavior:**
The overlay does NOT launch automatically when PowerToys starts. The `m_firstEnable`
flag ensures `enable()` only shows the overlay on subsequent toggles from Settings,
not on initial startup. Users must press the hotkey to show the overlay. We decided that it would be a better user experience this way, as some users may not want the overlay to show up immediately on startup.

**Hotkey Toggle:**
`on_hotkey()` toggles visibility — if the overlay is running, it terminates the
process; otherwise, it launches it. The hotkey only works when the module is
enabled via Settings.

#### Dependencies

- `interface/powertoy_module_interface.h` — Base interface
- `common/SettingsAPI/settings_objects.h` — Settings parsing
- `common/logger/logger.h` — Logging infrastructure
- `common/utils/process_path.h`, `winapi_error.h` — Win32 utilities

#### Future Work

- We did not get a chance to implement the GPO support for Screencast Mode. This work would mostly involve adding the appropriate checks in the `enable()` method of the `ScreencastMode` class in `dllmain.cpp`. There is a commented out piece of code in `dllmain.cpp` regarding the GPO. We see no reason to implement GPO for each individual setting, as Screencast Mode is a fairly simple module.
