# Mouse Utilities

[Public overview - Microsoft Learn](https://learn.microsoft.com/en-us/windows/powertoys/mouse-utilities)

## Quick Links

[All Issues](https://github.com/microsoft/PowerToys/issues?q=is%3Aopen%20label%3A%22Product-Mouse%20Utilities%22)<br>
[Bugs](https://github.com/microsoft/PowerToys/issues?q=is%3Aopen%20label%3AIssue-Bug%20label%3A%22Product-Mouse%20Utilities%22)<br>
[Pull Requests](https://github.com/microsoft/PowerToys/pulls?q=is%3Apr+is%3Aopen+label%3A%22Product-Mouse+Utilities%22)

Mouse Utilities is a collection of tools designed to enhance mouse and cursor functionality on Windows. The module contains five sub-utilities that provide different mouse-related features.

## Overview

Mouse Utilities includes the following sub-modules:

- **[Find My Mouse](findmymouse.md)**: Helps locate the mouse pointer by creating a visual spotlight effect when activated
- **[Mouse Highlighter](mousehighlighter.md)**: Visualizes mouse clicks with customizable highlights
- **[Mouse Jump](mousejump.md)**: Allows quick cursor movement to specific screen locations
- **[Mouse Pointer Crosshairs](mousepointer.md)**: Displays crosshair lines that follow the mouse cursor
- **Mouse Scroll Remap**: Remaps Shift+MouseWheel to Shift+Ctrl+MouseWheel for consistent horizontal scrolling across all applications

## Architecture

Most of the sub-modules (Find My Mouse, Mouse Highlighter, Mouse Pointer Crosshairs, and Mouse Scroll Remap) run within the PowerToys Runner process as separate threads. Mouse Jump is more complex and runs as a separate process that communicates with the Runner via events.

### Code Structure

#### Settings UI
- [MouseUtilsPage.xaml](/src/settings-ui/Settings.UI/SettingsXAML/Views/MouseUtilsPage.xaml)
- [MouseJumpPanel.xaml](/src/settings-ui/Settings.UI/SettingsXAML/Panels/MouseJumpPanel.xaml)
- [MouseJumpPanel.xaml.cs](/src/settings-ui/Settings.UI/SettingsXAML/Panels/MouseJumpPanel.xaml.cs)
- [MouseUtilsViewModel.cs](/src/settings-ui/Settings.UI/ViewModels/MouseUtilsViewModel.cs)
- [MouseUtilsViewModel_MouseJump.cs](/src/settings-ui/Settings.UI/ViewModels/MouseUtilsViewModel_MouseJump.cs)

#### Runner and Module Implementation
- [FindMyMouse](/src/modules/MouseUtils/FindMyMouse)
- [MouseHighlighter](/src/modules/MouseUtils/MouseHighlighter)
- [MousePointerCrosshairs](/src/modules/MouseUtils/MousePointerCrosshairs)
- [MouseScrollRemap](/src/modules/MouseUtils/MouseScrollRemap)
- [MouseJump](/src/modules/MouseUtils/MouseJump)
- [MouseJumpUI](/src/modules/MouseUtils/MouseJumpUI)
- [MouseJump.Common](/src/modules/MouseUtils/MouseJump.Common)

## Community Contributors

- **Michael Clayton (@mikeclayton)**: Contributed the initial version of the Mouse Jump tool and several updates based on his FancyMouse utility
- **Raymond Chen (@oldnewthing)**: Find My Mouse is based on Raymond Chen's SuperSonar

## Known Issues

- Mouse Highlighter has a reported bug where the highlight color stays on after toggling opacity to 0

## UI Test Automation

Mouse Utilities is currently undergoing a UI Test migration process to improve automated testing coverage. You can track the progress of this migration at:

[Mouse Utils UI Test Migration Progress](https://github.com/microsoft/PowerToys/blob/feature/UITestAutomation/src/modules/MouseUtils/MouseUtils.UITests/Release-Test-Checklist-Migration-Progress.md)

## See Also

For more detailed implementation information, please refer to the individual utility documentation pages linked above.
#### Activation Process
1. A keyboard hook detects the activation shortcut (typically double-press of Ctrl)
2. A `WM_PRIV_SHORTCUT` message is sent to the sonar window
3. `StartSonar()` is called to display a spotlight animation centered on the mouse pointer
4. The animation automatically fades or can be cancelled by user input

### Mouse Highlighter

Mouse Highlighter visualizes mouse clicks by displaying a highlight effect around the cursor when clicked.

#### Key Components
- Uses Windows Composition API for rendering
- Main implementation in `MouseHighlighter.cpp`
- Core logic handled by the `WndProc` function

#### Activation Process
1. When activated, it creates a transparent overlay window
2. A mouse hook monitors for click events
3. On click detection, the highlighter draws a circle or other visual indicator
4. The highlight effect fades over time based on user settings

### Mouse Pointer Crosshairs

Displays horizontal and vertical lines that intersect at the mouse cursor position.

#### Key Components
- Uses Windows Composition API for rendering
- Core implementation in `InclusiveCrosshairs.cpp`
- Main logic handled by the `WndProc` function

#### Activation Process
1. Creates a transparent, layered window for drawing crosshairs
2. When activated via shortcut, calls `StartDrawing()`
3. Sets a low-level mouse hook to track cursor movement
4. Updates crosshairs position on every mouse movement
5. Includes auto-hide functionality for cursor inactivity

### Mouse Jump

Allows quick mouse cursor repositioning to any screen location through a grid-based UI.

#### Key Components
- Runs as a separate process (`PowerToys.MouseJumpUI.exe`)
- Communicates with Runner process via events
- UI implemented in `MainForm.cs`

#### Activation Process
1. When shortcut is pressed, Runner triggers the shared event `MOUSE_JUMP_SHOW_PREVIEW_EVENT`
2. The MouseJumpUI process displays a screen overlay
3. User selects a destination point on the overlay
4. Mouse cursor is moved to the selected position
5. The UI process can be terminated via the `TERMINATE_MOUSE_JUMP_SHARED_EVENT`

### Mouse Scroll Remap

Remaps Shift+MouseWheel to Shift+Ctrl+MouseWheel for horizontal scrolling consistency across applications.

#### Key Components
- Implemented as a DLL loaded by the Runner process
- Uses a low-level mouse hook (WH_MOUSE_LL) to intercept mouse wheel events
- Main implementation in `dllmain.cpp`

#### How It Works
1. When enabled, installs a low-level mouse hook via SetWindowsHookEx
2. Monitors WM_MOUSEWHEEL events
3. Uses GetAsyncKeyState to detect if Shift is pressed (but not Ctrl)
4. If Shift+MouseWheel is detected:
   - Blocks the original event
   - Injects Ctrl key down event
   - Injects mouse wheel event
   - Injects Ctrl key up event
5. Result: Applications receive Shift+Ctrl+MouseWheel instead of Shift+MouseWheel

#### Use Case
- Provides consistent horizontal scrolling behavior across all applications
- Particularly useful for Microsoft Office apps which use Ctrl+Shift+MouseWheel instead of the more common Shift+MouseWheel pattern used by browsers and other applications

## Debugging

### Find My Mouse, Mouse Highlighter, Mouse Pointer Crosshairs, and Mouse Scroll Remap
- Debug by attaching to the Runner process directly
- Set breakpoints in the respective utility code files (e.g., `FindMyMouse.cpp`, `MouseHighlighter.cpp`, `InclusiveCrosshairs.cpp`, `dllmain.cpp` in MouseScrollRemap)
- Call the respective utility by using the activation shortcut (e.g., double Ctrl press for Find My Mouse)
- During debugging, visual effects may appear glitchy due to the debugger's overhead

### Mouse Jump
- Start by debugging the Runner process
- Then attach the debugger to the MouseJumpUI process
- Note: Debugging MouseJumpUI directly is challenging as it requires the Runner's process ID as a parameter

## Known Issues

- Mouse Highlighter has a reported bug where the highlight color stays on after toggling opacity to 0
