# Keyboard Manager Debugging Guide

This document provides guidance on debugging the Keyboard Manager module in PowerToys.

## Module Overview

Keyboard Manager consists of two main components:
- **Keyboard Manager Editor**: UI application for configuring key and shortcut remappings
- **Keyboard Manager Engine**: Background process that intercepts and handles keyboard events

## Development Environment Setup

1. Clone the PowerToys repository
2. Open `PowerToys.slnx` in Visual Studio
3. Ensure all NuGet packages are restored
4. Build the entire solution in Debug configuration

## Debugging the Editor (UI)

### Setup

1. In Visual Studio, right-click on the `KeyboardManagerEditor` project
2. Select "Set as Startup Project"

### Common Debugging Scenarios

#### UI Rendering Issues

Breakpoints to consider:
- `EditKeyboardWindow.cpp`: `CreateWindow()` method 
- `EditShortcutsWindow.cpp`: `CreateWindow()` method

#### Configuration Changes

When debugging configuration changes:
1. Set breakpoints in `KeyboardManagerState.cpp` around the `SetRemappedKeys()` or `SetRemappedShortcuts()` methods
2. Monitor the JSON serialization process in the save functions

### Testing UI Behavior

The `KeyboardManagerEditorTest` project contains tests for the UI functionality. Run these tests to validate UI changes.

## Debugging the Engine (Remapping Logic)

### Setup

1. In Visual Studio, right-click on the `KeyboardManagerEngine` project
2. Select "Set as Startup Project"
3. Press F5 to start debugging

### Key Event Flow

The keyboard event processing follows this sequence:
1. Low-level keyboard hook captures an event
2. `KeyboardEventHandlers.cpp` processes the event
3. `KeyboardManager.cpp` applies remapping logic
4. Event is either suppressed, modified, or passed through

### Breakpoints to Consider

- `main.cpp`: `StartLowlevelKeyboardHook()` - Hook initialization
- `KeyboardEventHandlers.cpp`: `HandleKeyboardEvent()` - Entry point for each keyboard event
- `KeyboardManager.cpp`: `HandleKeyEvent()` - Processing individual key events
- `KeyboardManager.cpp`: `HandleShortcutRemapEvent()` - Processing shortcut remapping

### Logging and Trace

Enable detailed logging by setting the `_DEBUG` and `KBM_VERBOSE_LOGGING` preprocessor definitions.

## Common Issues and Troubleshooting

### Multiple Instances

If you encounter issues with multiple instances, check the mutex logic in `KeyboardManagerEditor.cpp`. The editor uses `PowerToys_KBMEditor_InstanceMutex` to ensure single instance.

### Key Events Not Being Intercepted

1. Verify the hook is properly installed by setting a breakpoint in the hook procedure
2. Check if any other application is capturing keyboard events at a lower level
3. Ensure the correct configuration is being loaded from the settings JSON

### UI Freezes or Crashes

1. Check XAML Islands initialization in the Editor
2. Verify UI thread is not being blocked by IO operations
3. Look for exceptions in the event handling code

## Advanced Debugging

### Debugging Both Components Simultaneously

To debug both the Editor and Engine:
1. Launch the Engine first in debug mode
2. Attach the debugger to the Editor process when it starts
