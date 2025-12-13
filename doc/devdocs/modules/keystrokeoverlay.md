# Keystroke Overlay - Will live in doc/devdocs/modules


[Public overview - Microsoft Learn](#)

## Quick Links

[All Issues](#)<br>
[Bugs](#)<br>
[Pull Requests](#)

## Overview

Keystroke Overlay is a PowerToys module that displays keyboard input live. There are three different display modes. Single-key display shows the last key pressed, e.g. "o." Last five keys display shows the last five keys pressed, e.g. "h e l l o." Shortcut display supports shortcuts, e.g. "ctrl + v." This module is meant to be used as a helpful tool for educators, presenters, and/or visually impaired users.

## Architecture

The Keystroke Overlay module consists of three main components:

```
keystrokeoverlay/
├── KeystrokeOverlayKeyboardService/     # Keyboard Hook
├── KeystrokeOverlayXAML/                # The overlaying display UI
└── KeystrokeOverlayModuleInterface/     # DLL Interface
```

### Keyboard Service (KeystrokeOverlayKeyboardService)

The Keyboard Service component is responsible for:
- Compiles KeystrokeEvent.h, EventQueue.h, Batcher.cpp, and KeyboardListener.cpp into PowerToys.KeystrokeOverlayKeystrokeServer.exe
- Implements keyboard hooks to detect key presses
- Manages the trigger mechanism for displaying the keystroke overlay
- Handles keyboard input processing

### UI Layer (KeystrokeOverlayXAML)

The UI component is responsible for:
- Displaying the overlay of pressed keys
- Managing the visual positioning of the overlay

### Module Interface (KeystrokeOverlayModuleInterface)

The Module Interface, implemented in `KeystrokeOverlayModuleInterface/dllmain.cpp`, is responsible for:
- Handling communication between PowerToys Runner and the KeystrokeOverlay process
- Managing module lifecycle (enable/disable/settings)
- Launching and terminating the PowerToys.KeystrokeOverlay.exe process (as well as PowerToys.KeystrokeOverlayKeystrokeServer.exe as a child process)

## Implementation Details

### Activation Mechanism

The Keystroke Overlay is activated when:
1. A user presses or holds any key
2. After a brief delay (around 300ms per setting), the overlay appears
3. Upon releasing the key(s), the overlay hovers for around 300ms or a value specified in settings, then disappears

### Character Sets
The module supports multiple language-specific characters. Since the module uses keyboard codes to detect key presses and trigger the display, various keyboards and languages are supported by Keystroke Overlay, as long as they are supported by Windows.

### Known Behaviors
- If a key is pressed and held, this is processed as several presses of the same key in rapid succession
- Stream mode detects capital letters as shortcuts because the shift key is pressed and separates them from words; e.g. “Hello” in stream mode appears as “H ello”

### Future Considerations

- Add support for different appearances, other than left-to-right. Right-to-left or center-outwards would be beneficial, especially for users who communicate in languages that read right-to-left
- Add support for default positioning, e.g. top-left, bottom-center, middle-right, … 


## Debugging

To debug the Keystroke Overlay module via **runner** approach, follow these steps:

0. Get familiar with the overall [Debugging Process](../development/debugging.md) for PowerToys.
1. **Build** the entire PowerToys solution in Visual Studio
2. Navigate to the **KeystrokeOverlay** folder in Solution Explorer
3. Open the file you want to debug and set **breakpoints** at the relevant locations
4. Find the **runner** project in the root of the solution
5. Right-click on the **runner** project and select "*Set as Startup Project*"
6. Start debugging by pressing `F5` or clicking the "*Start*" button
7. When the PowerToys Runner launches, **enable** the Keystroke Overlay module in the UI
8. Use the Visual Studio Debug menu or press `Ctrl+Alt+P` to open "*Reattach to Process*"
9. Find and select "**PowerToys.KeystrokeOverlay.exe**" in the process list
10. Trigger the action in Keystroke Overlay that should hit your breakpoint
11. Verify that the debugger breaks at your breakpoint and you can inspect variables and step through code

This process allows you to debug the Keystroke Overlay module while it's running as part of the full PowerToys application.

### Alternative Debugging Approach

To directly debug the Keystroke Overlay UI component:

0. Get familiar with the overall [Debugging Process](../development/debugging.md) for PowerToys.
1. **Build** the entire PowerToys solution in Visual Studio
2. Navigate to the **KeystrokeOverlay** folder in Solution Explorer
3. Open the file you want to debug and set **breakpoints** at the relevant locations
4. Right-click on the **KeystrokeOverlayUI** project and select "*Set as Startup Project*"
5. Start debugging by pressing `F5` or clicking the "*Start*" button
6. Verify that the debugger breaks at your breakpoint and you can inspect variables and step through code




## Any known issues with debugging here
