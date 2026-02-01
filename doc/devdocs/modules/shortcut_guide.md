# Shortcut Guide

[Public overview - Microsoft Learn](https://learn.microsoft.com/en-us/windows/powertoys/shortcut-guide)

## Quick Links

[All Issues](https://github.com/microsoft/PowerToys/issues?q=is%3Aopen%20label%3A%22Product-Shortcut%20Guide%22)<br>
[Bugs](https://github.com/microsoft/PowerToys/issues?q=is%3Aopen%20label%3AIssue-Bug%20label%3A%22Product-Shortcut%20Guide%22)<br>
[Pull Requests](https://github.com/microsoft/PowerToys/pulls?q=is%3Apr+is%3Aopen+label%3A%22Product-Shortcut+Guide%22+)

## Overview
Shortcut Guide is a PowerToy that displays an overlay of available keyboard shortcuts when a user-set keyboard shortcut is pressed. It helps users discover and remember keyboard shortcuts for Windows and apps.

> [!NOTE]
> The spec for the manifest files is in development and will be linked here once available.

## Usage
- Press the user-defined hotkey to display the overlay
- Press the hotkey again or press ESC to dismiss the overlay

## Build and Debug Instructions

### Build
1. Open PowerToys.slnx in Visual Studio
2. Select Release or Debug in the Solutions Configuration drop-down menu
3. From the Build menu, choose Build Solution
4. The executable is named PowerToys.ShortcutGuide.exe

### Debug
1. Right-click the ShortcutGuide.Ui project and select 'Set as Startup Project'
2. Right-click the project again and select 'Debug'

> [!NOTE]
> When run in debug mode, the window behaves differently than in release mode. It will not automatically close when loosing focus, it will be displayed on top of all other windows, and it is not hidden from the taskbar. 

## Project Structure

The Shortcut Guide module consists of the following 4 projects:

### [`ShortcutGuide.Ui`](/src/modules/ShortcutGuide/ShortcutGuide.Ui/ShortcutGuide.Ui.csproj

This is the main UI project for the Shortcut Guide module. Upon startup it does the following tasks:

1. Copies the built-in manifest files to the users manifest directory (overwriting existing files).
2. Generate the `index.yml` manifest file.
3. Populate the PowerToys shortcut manifest with the user-defined shortcuts.
4. Starts the UI.

### [`ShortcutGuide.CPPProject`](/src/modules/ShortcutGuide/ShortcutGuide.CPPProject/ShortcutGuide.CPPProject.vcxproj)

This project exports certain functions to be used by the Shortcut Guide module, that were not able to be implemented in C#.

#### [`excluded_app.cpp`](/src/modules/ShortcutGuide/ShortcutGuide.CPPProject/excluded_app.cpp)

This file contains one function with the following signature:

```cpp
__declspec(dllexport) bool IsCurrentWindowExcludedFromShortcutGuide()
```

This function checks if the current window is excluded from the Shortcut Guide overlay. It returns `true` if the current window is excluded otherwise it returns `false`.

#### [`tasklist_positions.cpp`](/src/modules/ShortcutGuide/ShortcutGuide.CPPProject/tasklist_positions.cpp)

This file contains helper functions to retrieve the positions of the taskbar buttons. It exports the following function:

```cpp
__declspec(dllexport) TasklistButton* get_buttons(HMONITOR monitor, int* size)
```

This function retrieves the positions of the taskbar buttons for a given monitor. It returns an array of `TasklistButton` structures (max 10), which contain the position and size of each button.

`monitor` must be the monitor handle of the monitor containing the taskbar instance of which the buttons should be retrieved.

`size` will contain the resulting array size.

It determines the positions through Windows `FindWindowEx` function.
For the primary taskbar it searches for:
* A window called "Shell_TrayWnd"
* that contains a window called "ReBarWindow32"
* that contains a window called "MSTaskSwWClass"
* that contains a window called "MSTaskListWClass"

For any secondary taskbar it searches for:
* A window called "Shell_SecondaryTrayWnd"
* that contains a window called "WorkerW"
* that contains a window called "MSTaskListWClass"

It then enumerates all the button elements inside "MSTaskListWClass" while skipping such with a same name (which implies the user does not use combining taskbar buttons) 

### [`ShortcutGuide.IndexYmlGenerator`](/src/modules/ShortcutGuide/ShortcutGuide.IndexYmlGenerator/)

This application generates the `index.yml` manifest file.

It is a separate project so that its code can be easier ported to WinGet in the future.

### [`ShortcutGuideModuleInterface`](/src/modules/ShortcutGuide/ShortcutGuideModuleInterface/ShortcutGuideModuleInterface.vcxproj)

The module interface that handles opening and closing the user interface.

## Features and Limitations

- Currently the displayed shortcuts (Except the ones from PowerToys) are not localized.
- It's currently rated as a P3 (lower priority) module

## Future Development

- Implementing with WinGet to get new shortcut manifest files
- Adding localization support for the built-in manifest files