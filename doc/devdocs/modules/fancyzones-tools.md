# FancyZones Debugging Tools

## Overview

FancyZones has several specialized debugging tools to help diagnose issues with window management, zone detection, and rendering. These tools are designed to isolate and test specific components of the FancyZones functionality.

## Tools Summary

| Tool | Purpose | Key Functionality |
|------|---------|-------------------|
| FancyZones_HitTest | Tests zone hit detection | Shows which zone is under cursor with detailed metrics |
| FancyZones_DrawLayoutTest | Tests layout drawing | Renders zone layouts to debug display issues |
| FancyZones_zonable_tester | Tests window zonability | Determines if windows can be placed in zones |
| StylesReportTool | Analyzes window properties | Generates window style reports for debugging |

## FancyZones_HitTest

![Image of the FancyZones hit test tool](/doc/images/tools/fancyzones-hit-test.png)

### Purpose
Tests the FancyZones layout selection logic by displaying a window with zones and highlighting the zone under the mouse cursor.

### Functionality
- Displays a window with 5 sample zones
- Highlights the zone under the mouse cursor
- Shows metrics used for zone detection in a sidebar
- Helps diagnose issues with zone positioning and hit testing

### Usage
- Run the tool and move your mouse over the zones
- The currently detected zone will be highlighted
- The sidebar displays metrics used for determining the active zone
- Useful for debugging hit detection, positioning, and DPI issues

## FancyZones_DrawLayoutTest

### Purpose
Debug issues related to the drawing of zone layouts on screen.

### Functionality
- Simulates zone layouts (currently only column layout supported)
- Tests rendering of zones with different configurations
- Helps diagnose display issues across monitor configurations

### Usage
- Run the tool
- Press **W** key to toggle zone appearance on the primary screen
- Press **Q** key to exit the application
- The number of zones can be modified in the source code

### Technical Notes
The application is DPI unaware, meaning it doesn't scale for DPI changes and always assumes a scale factor of 100% (96 DPI). Scaling is automatically performed by the system.

## FancyZones_zonable_tester

![Image of the FancyZones zonable tester](/doc/images/tools/fancyzones-zonable-tester.png)

### Purpose
Tests if the window under the mouse cursor is "zonable" (can be placed in a FancyZones zone).

### Functionality
- Analyzes the window under the cursor
- Provides detailed window information:
  * HWND (window handle)
  * Process ID
  * HWND of foreground window
  * Window style flags
  * Extended style flags
  * Window class
  * Process path

### Usage
- Run the command-line application
- Hover the mouse over a window to test
- Review the console output for detailed window information
- Check if the window is considered zonable by FancyZones

### Limitations
Note that this tool may not be fully up-to-date with the latest zonable logic in the main FancyZones codebase.

## StylesReportTool

### Purpose
Generates detailed reports about window styles that affect zonability.

### Functionality
- Creates comprehensive window style reports
- Focuses on style flags that determine if windows can be placed in zones
- Outputs report to "WindowStyles.txt" on the desktop

### Usage
- Run the tool
- Focus the window you want to analyze
- Press **Ctrl+Alt+S** to generate a report
- Review WindowStyles.txt to understand why a window might not be zonable

## Debugging Workflow

For most effective debugging of FancyZones issues:

1. Use **StylesReportTool** to analyze window properties of problematic windows
2. Use **FancyZones_zonable_tester** to check if specific windows can be zoned
3. Use **FancyZones_draw** for layout rendering issues on different monitors
4. Use **FancyZones_HitTest** for diagnosing zone detection problems

## Testing Considerations

When testing FancyZones with these tools, consider:

- Testing on different Windows versions
- Testing with multiple monitors with different:
  * Resolutions
  * Scaling settings
  * Physical arrangements
- Testing with various window types:
  * Standard applications
  * Legacy applications
  * UWP/WinUI applications
  * Administrative windows
  * Special windows (like Task Manager)
- Testing various layouts:
  * Grid layouts
  * Custom layouts
  * Overlapping zones

## Initial Setup Issues

If encountering JSON token errors on first run:
1. Launch FancyZones Editor through PowerToys Settings UI
2. This initializes required configuration files
3. Direct project execution won't initialize configs properly
