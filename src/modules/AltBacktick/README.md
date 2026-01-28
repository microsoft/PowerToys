# AltBacktick

AltBacktick enables you to quickly switch between windows of the same application, similar to macOS's <kbd>Cmd</kbd>+<kbd>`</kbd> functionality.

## Features

- **Quick Window Switching**: Press <kbd>Alt+</kbd> (or <kbd>Win+`</kbd>) to cycle through windows of the currently focused application
- **Reverse Cycling**: Hold <kbd>Shift</kbd> to cycle in reverse order
- **Customizable Modifier Key**: Choose between <kbd>Alt</kbd> or <kbd>Win</kbd> as the modifier key
- **Virtual Desktop Aware**: Only shows windows on the current virtual desktop
- **MRU Order**: Switches windows in most-recently-used order for intuitive navigation
- **Minimized Windows**: Option to ignore minimized windows in the cycle

## Settings

### Modifier Key
Choose which modifier key to use:
- **Alt + `** (default) - Similar to macOS behavior
- **Win + `** - Alternative for users who prefer the Windows key

### Ignore Minimized Windows
When enabled (default), minimized windows will be skipped during cycling.

## Usage

1. Focus on any window of an application (e.g., Chrome, VSCode, File Explorer)
2. Press <kbd>Alt</kbd>+<kbd>`</kbd> to switch to the next window of the same application
3. Keep pressing <kbd>`</kbd> while holding <kbd>Alt</kbd> to continue cycling
4. Add <kbd>Shift</kbd> to cycle backwards: <kbd>Alt</kbd>+<kbd>Shift</kbd>+<kbd>`</kbd>
5. Release <kbd>Alt</kbd> to select the current window

## Technical Details

- Uses a standalone keyboard hook for modifier key detection
- Maintains MRU (Most Recently Used) lists per application per virtual desktop
- Filters windows by process name and virtual desktop
- Excludes tool windows, invisible windows, and popup windows (unless they have WS_EX_APPWINDOW style)

## Attribution

This module is based on [AltBacktick](https://github.com/akiver/AltBacktick) by Anthony Kiver, used under the MIT license.
