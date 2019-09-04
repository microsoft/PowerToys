# Windows Key Shortcut Guide

# Introduction
The Windows Key Shortcut Guide shows common keyboard shortcuts that use the Windows key.

# Usage
Press and hold the keyboard Windows key for about 1 second, an overlay appears showing keyboard shortcuts that use the Windows Key:
- Shortcuts for changing the position of the active window.
- Common Windows shortcuts.
- Taskbar shortcuts.

Releasing the Windows key will make the overlay disappear.  If the shortcut guide was visible for less than a second, the start menu will appear after the shortcut guide is dismissed.

![Image of the Overlay](/doc/images/shortcut_guide/usage.png)

Windows key keyboard shortcuts can be used while the guide is being shown and the result of those shortcuts (active window moved, arrow shortcut behavior changes, etc) will be displayed in the guide.

# Options
These configurations can be edited from the PowerToys Settings screen:
- "How long to press the Windows key before showing the Shortcut Guide (ms)" - How many milliseconds to press the Windows key before the Shortcut Guide is shown.
- "Opacity of the Shortcut Guide's overlay background (%)" - Changing this setting controls the opacity of the Shortcut Guide's overlay background, occluding the work environment beneath the Shortcut Guide to different degrees.

![Image of the Options](/doc/images/shortcut_guide/settings.png)

# Backlog
The backlog for the utility can be found [here](https://github.com/Microsoft/PowerToys/tree/master/doc/planning/ShortcutGuideBacklog.md) and the source code is [here](https://github.com/Microsoft/PowerToys/tree/master/src/modules/shortcut_guide).

# Code organization

#### [`dllmain.cpp`](./dllmain.cpp)
Contains DLL boilerplate code.

#### [`shortcut_guide.cpp`](./shortcut_guide.cpp)
Contains the module interface code. It initializes the settings values and the keyboard event listener.

#### [`overlay_window.cpp`](./overlay_window.cpp)
Contains the code for loading the SVGs, creating and rendering of the overlay window.

#### [`keyboard_state.cpp`](./keyboard_state.cpp)
Contains helper methods for checking the current state of the keyboard.

#### [`target_state.cpp`](./target_state.cpp)
State machine that handles the keyboard events. It’s responsible for deciding when to show the overlay, when to suppress the Start menu (if the overlay is displayed long enough), etc.

#### [`trace.cpp`](./trace.cpp)
Contains code for telemetry.
