# Quick Accent

## Quick Links

[All Issues](https://github.com/microsoft/PowerToys/issues?q=state%3Aopen%20label%3A%22Product-Quick%20Accent%22)
[PRs](https://github.com/microsoft/PowerToys/pulls?q=is%3Apr+label%3A%22Product-Quick+Accent%22+)

## Overview

Quick Accent (formerly known as Power Accent) is a PowerToys module that allows users to quickly insert accented characters by holding a key and pressing an activation key (like the Space key or arrow keys). For example, holding 'a' might display options like 'à', 'á', 'â', etc. This tool enhances productivity by streamlining the input of special characters without the need to memorize keyboard shortcuts.

## Architecture

The Quick Accent module consists of four main components:

```
poweraccent/
├── PowerAccent.Core/               # Core component containing Language Sets
├── PowerAccent.UI/                 # The character selector UI
├── PowerAccentKeyboardService/     # Keyboard Hook
└── PowerAccentModuleInterface/     # DLL interface
```

### Module Interface (PowerAccentModuleInterface)

The Module Interface, implemented in `PowerAccentModuleInterface/dllmain.cpp`, is responsible for:
- Handling communication between PowerToys Runner and the PowerAccent process
- Managing module lifecycle (enable/disable/settings)
- Launching and terminating the PowerToys.PowerAccent.exe process

### Core Logic (PowerAccent.Core)

The Core component contains:
- Main accent character logic
- Keyboard input detection
- Character mappings for different languages
- Management of language sets and special characters (currency, math symbols, etc.)
- Usage statistics for frequently used characters

### UI Layer (PowerAccent.UI)

The UI component is responsible for:
- Displaying the toolbar with accent options
- Handling user selection of accented characters
- Managing the visual positioning of the toolbar

### Keyboard Service (PowerAccentKeyboardService)

This component:
- Implements keyboard hooks to detect key presses
- Manages the trigger mechanism for displaying the accent toolbar
- Handles keyboard input processing

## Implementation Details

### Activation Mechanism

The Quick Accent is activated when:
1. A user presses and holds a character key (e.g., 'a')
2. After a brief delay (around 300ms), the accent toolbar appears
3. The user can select an accented variant using the trigger key
4. Upon releasing the keys, the selected accented character is inserted

### Character Sets

The module includes multiple language-specific character sets and special character sets:
- Various language sets for different alphabets and writing systems
- Special character sets (currency symbols, mathematical notations, etc.)
- These sets are defined in the core component and can be extended

### Known Behaviors

- The module has a specific timing mechanism for activation that users have become accustomed to. Initially, this was considered a bug (where the toolbar would still appear even after quickly tapping and releasing keys), but it has been maintained as expected behavior since users rely on it.
- Multiple rapid key presses can trigger multiple background tasks.

## Future Considerations

- Potential refinements to the activation timing mechanism
- Additional language and special character sets
- Improved UI positioning in different application contexts
