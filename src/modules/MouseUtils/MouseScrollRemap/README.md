# Mouse Scroll Remap

Mouse Scroll Remap is a PowerToys utility that remaps `Shift + MouseWheel` to `Shift + Ctrl + MouseWheel` for horizontal scrolling.

## Overview

Many applications (like Google Chrome, JetBrains IDEs, GIMP, etc.) use `Shift + MouseWheel` for horizontal scrolling. However, Microsoft Office applications (Word, Excel, PowerPoint, etc.) use `Ctrl + Shift + MouseWheel` instead. This utility bridges that gap by automatically converting `Shift + MouseWheel` to `Shift + Ctrl + MouseWheel` system-wide.

## How It Works

When enabled, Mouse Scroll Remap installs a low-level mouse hook that:
1. Monitors mouse wheel events
2. Detects when Shift key is pressed (but not Ctrl)
3. Intercepts the mouse wheel event
4. Injects a Ctrl key press before the mouse wheel event
5. Releases the Ctrl key after the event

This allows for consistent horizontal scrolling behavior across all applications.

## Usage

1. Enable Mouse Scroll Remap in PowerToys settings
2. Hold Shift and scroll with your mouse wheel
3. The scroll will be converted to horizontal scrolling in supported applications

## Technical Details

- **Hook Type**: WH_MOUSE_LL (low-level mouse hook)
- **Event Intercepted**: WM_MOUSEWHEEL
- **Key Detection**: Uses GetAsyncKeyState to check for Shift and Ctrl keys
- **Input Injection**: Uses SendInput API to inject keyboard and mouse events

## Compatibility

This utility works with any application that supports horizontal scrolling via keyboard modifiers and mouse wheel, including:
- Microsoft Office (Word, Excel, PowerPoint, etc.)
- Web browsers (Chrome, Firefox, Edge, etc.)
- IDEs and text editors
- Image viewers and editors

## Settings

Mouse Scroll Remap can be enabled or disabled from the PowerToys settings window. When disabled, the mouse hook is removed and normal scroll behavior is restored.
