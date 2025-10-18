---
description: DSC configuration reference for PowerToys FancyZones module
ms.date:     10/18/2025
ms.topic:    reference
title:       FancyZones Module
---

# FancyZones Module

## Synopsis

Manages configuration for the FancyZones utility, a window layout manager that arranges and snaps windows into efficient layouts.

## Description

The `FancyZones` module configures PowerToys FancyZones, a window manager utility that helps organize windows into custom layouts called zones. Fan## See also

- [Settings Resource][01]
- [PowerToys DSC Overview][02]
- [App Module][03] - For enabling/disabling FancyZones utility
- [PowerToys FancyZones Documentation][04]

<!-- Link reference definitions -->
[01]: ../settings-resource.md
[02]: ../overview.md
[03]: ./App.md
[04]: https://learn.microsoft.com/windows/powertoys/fancyzoness allows you to create multiple zone layouts for different displays and quickly snap windows into position using keyboard shortcuts or mouse actions.

This module controls activation methods, window behavior, zone appearance, editor settings, and other FancyZones preferences.

## Properties

The FancyZones module supports the following configurable properties:

### fancyzones_shiftDrag

Controls whether holding Shift while dragging a window activates zone snapping.

**Type:** boolean  
**Default:** `true`

### fancyzones_mouseSwitch

Controls whether moving a window across monitors triggers zone selection.

**Type:** boolean  
**Default:** `false`

### fancyzones_overrideSnapHotkeys

Controls whether FancyZones overrides the Windows Snap hotkeys (Win + Arrow keys).

**Type:** boolean  
**Default:** `false`

### fancyzones_moveWindowsAcrossMonitors

Controls whether moving windows between monitors is enabled.

**Type:** boolean  
**Default:** `false`

### fancyzones_moveWindowsBasedOnPosition

Controls whether windows move to zones based on cursor position rather than window position.

**Type:** boolean  
**Default:** `false`

### fancyzones_overlappingZonesAlgorithm

Determines the algorithm used when multiple zones overlap.

**Type:** integer  
**Allowed values:**
- `0` - Smallest zone
- `1` - Largest zone
- `2` - Positional (based on cursor/window position)

**Default:** `0`

### fancyzones_displayOrWorkAreaChange_moveWindows

Controls whether windows are moved to fit when display or work area changes.

**Type:** boolean  
**Default:** `false`

### fancyzones_zoneSetChange_flashZones

Controls whether zones flash briefly when the zone set changes.

**Type:** boolean  
**Default:** `false`

### fancyzones_zoneSetChange_moveWindows

Controls whether windows are automatically moved when the zone set changes.

**Type:** boolean  
**Default:** `false`

### fancyzones_appLastZone_moveWindows

Controls whether windows are moved to their last known zone when reopened.

**Type:** boolean  
**Default:** `true`

### fancyzones_openWindowOnActiveMonitor

Controls whether newly opened windows appear on the currently active monitor.

**Type:** boolean  
**Default:** `false`

### fancyzones_spanZonesAcrossMonitors

Controls whether zones can span across multiple monitors.

**Type:** boolean  
**Default:** `false`

### fancyzones_makeDraggedWindowTransparent

Controls whether dragged windows become transparent to show zones underneath.

**Type:** boolean  
**Default:** `true`

### fancyzones_zoneColor

Sets the color of zone areas.

**Type:** string (hex color)  
**Format:** `"#RRGGBB"`  
**Example:** `"#0078D7"`  
**Default:** `"#0078D7"`

### fancyzones_zoneBorderColor

Sets the color of zone borders.

**Type:** string (hex color)  
**Format:** `"#RRGGBB"`  
**Example:** `"#FFFFFF"`  
**Default:** `"#FFFFFF"`

### fancyzones_zoneHighlightColor

Sets the highlight color when a zone is activated.

**Type:** string (hex color)  
**Format:** `"#RRGGBB"`  
**Example:** `"#0078D7"`  
**Default:** `"#0078D7"`

### fancyzones_highlightOpacity

Sets the opacity of zone highlights (0-100).

**Type:** integer  
**Range:** `0` to `100`  
**Default:** `50`

### fancyzones_editorHotkey

Sets the keyboard shortcut to open the FancyZones editor.

**Type:** object  
**Properties:**
- `win` (boolean) - Windows key modifier
- `ctrl` (boolean) - Ctrl key modifier
- `alt` (boolean) - Alt key modifier
- `shift` (boolean) - Shift key modifier
- `code` (integer) - Virtual key code
- `key` (string) - Key name

**Default:** `Win + Shift + ~`

### fancyzones_windowSwitching

Controls whether window switching with arrow keys is enabled.

**Type:** boolean  
**Default:** `true`

### fancyzones_nextTabHotkey

Sets the keyboard shortcut to switch to the next tab/window in a zone.

**Type:** object (same structure as fancyzones_editorHotkey)

### fancyzones_prevTabHotkey

Sets the keyboard shortcut to switch to the previous tab/window in a zone.

**Type:** object (same structure as fancyzones_editorHotkey)

### fancyzones_excludedApps

List of applications excluded from FancyZones snapping.

**Type:** string (newline-separated list of executable names)  
**Example:** `"Notepad.exe\nCalc.exe"`

## Examples

### Example 1 - Enable basic zone snapping with direct execution

This example enables Shift-drag zone snapping and mouse-based monitor switching.

```powershell
$config = @{
    settings = @{
        properties = @{
            fancyzones_shiftDrag = $true
            fancyzones_mouseSwitch = $true
        }
        name = "FancyZones"
        version = "1.0"
    }
} | ConvertTo-Json -Depth 10 -Compress

PowerToys.DSC.exe set --resource 'settings' --module FancyZones --input $config
```

### Example 2 - Configure window movement behavior with DSC

This example configures how windows behave when displays or zones change.

```yaml
# fancyzones-window-behavior.dsc.yaml
$schema: https://aka.ms/dsc/schemas/v3/bundled/config/document.json
resources:
  - name: Configure FancyZones window behavior
    type: Microsoft.PowerToys/FancyZonesSettings
    properties:
      settings:
        properties:
          fancyzones_displayOrWorkAreaChange_moveWindows: true
          fancyzones_zoneSetChange_moveWindows: true
          fancyzones_appLastZone_moveWindows: true
          fancyzones_moveWindowsAcrossMonitors: true
        name: FancyZones
        version: 1.0
```

### Example 3 - Customize zone appearance with WinGet

This example installs PowerToys and configures custom zone colors and opacity.

```yaml
# winget-fancyzones-appearance.yaml
$schema: https://aka.ms/dsc/schemas/v3/bundled/config/document.json
metadata:
  winget:
    processor: dscv3
resources:
  - name: Install PowerToys
    type: Microsoft.WinGet.DSC/WinGetPackage
    properties:
      id: Microsoft.PowerToys
      source: winget
  
  - name: Customize FancyZones appearance
    type: Microsoft.PowerToys/FancyZonesSettings
    properties:
      settings:
        properties:
          fancyzones_zoneColor: "#2D2D30"
          fancyzones_zoneBorderColor: "#007ACC"
          fancyzones_zoneHighlightColor: "#007ACC"
          fancyzones_highlightOpacity: 75
          fancyzones_makeDraggedWindowTransparent: true
        name: FancyZones
        version: 1.0
```

### Example 4 - Override Windows Snap hotkeys

This example configures FancyZones to replace Windows default snap functionality.

```yaml
# fancyzones-snap-override.dsc.yaml
$schema: https://aka.ms/dsc/schemas/v3/bundled/config/document.json
resources:
  - name: Override Windows Snap
    type: Microsoft.PowerToys/FancyZonesSettings
    properties:
      settings:
        properties:
          fancyzones_overrideSnapHotkeys: true
          fancyzones_moveWindowsBasedOnPosition: true
        name: FancyZones
        version: 1.0
```

### Example 5 - Configure editor hotkey

This example changes the FancyZones editor hotkey to Ctrl+Shift+Alt+F.

```powershell
$config = @{
    settings = @{
        properties = @{
            fancyzones_editorHotkey = @{
                win = $false
                ctrl = $true
                alt = $true
                shift = $true
                code = 70  # F key
                key = "F"
            }
        }
        name = "FancyZones"
        version = "1.0"
    }
} | ConvertTo-Json -Depth 10 -Compress

PowerToys.DSC.exe set --resource 'settings' --module FancyZones --input $config
```

### Example 6 - Exclude applications from zone snapping

This example configures FancyZones to ignore specific applications.

```yaml
# fancyzones-exclusions.dsc.yaml
$schema: https://aka.ms/dsc/schemas/v3/bundled/config/document.json
resources:
  - name: Exclude apps from FancyZones
    type: Microsoft.PowerToys/FancyZonesSettings
    properties:
      settings:
        properties:
          fancyzones_excludedApps: |
            Notepad.exe
            Calculator.exe
            mspaint.exe
        name: FancyZones
        version: 1.0
```

### Example 7 - Multi-monitor configuration

This example configures FancyZones for optimal multi-monitor workflow.

```yaml
# fancyzones-multimonitor.dsc.yaml
$schema: https://aka.ms/dsc/schemas/v3/bundled/config/document.json
resources:
  - name: Multi-monitor FancyZones setup
    type: Microsoft.PowerToys/FancyZonesSettings
    properties:
      settings:
        properties:
          fancyzones_shiftDrag: true
          fancyzones_mouseSwitch: true
          fancyzones_moveWindowsAcrossMonitors: true
          fancyzones_spanZonesAcrossMonitors: false
          fancyzones_openWindowOnActiveMonitor: true
          fancyzones_displayOrWorkAreaChange_moveWindows: true
        name: FancyZones
        version: 1.0
```

### Example 8 - Complete FancyZones configuration with WinGet

This example shows a comprehensive FancyZones setup with installation.

```yaml
# winget-fancyzones-complete.yaml
$schema: https://aka.ms/dsc/schemas/v3/bundled/config/document.json
metadata:
  winget:
    processor: dscv3
resources:
  - name: Install PowerToys
    type: Microsoft.WinGet.DSC/WinGetPackage
    properties:
      id: Microsoft.PowerToys
      source: winget
  
  - name: Enable FancyZones
    type: Microsoft.PowerToys/AppSettings
    properties:
      settings:
        properties:
          Enabled:
            FancyZones: true
        name: App
        version: 1.0
  
  - name: Configure FancyZones
    type: Microsoft.PowerToys/FancyZonesSettings
    properties:
      settings:
        properties:
          # Activation
          fancyzones_shiftDrag: true
          fancyzones_mouseSwitch: true
          fancyzones_overrideSnapHotkeys: false
          
          # Window behavior
          fancyzones_moveWindowsAcrossMonitors: true
          fancyzones_moveWindowsBasedOnPosition: false
          fancyzones_displayOrWorkAreaChange_moveWindows: true
          fancyzones_zoneSetChange_moveWindows: false
          fancyzones_appLastZone_moveWindows: true
          
          # Appearance
          fancyzones_makeDraggedWindowTransparent: true
          fancyzones_zoneColor: "#0078D7"
          fancyzones_zoneBorderColor: "#FFFFFF"
          fancyzones_zoneHighlightColor: "#0078D7"
          fancyzones_highlightOpacity: 50
          
          # Multi-monitor
          fancyzones_openWindowOnActiveMonitor: true
          fancyzones_spanZonesAcrossMonitors: false
        name: FancyZones
        version: 1.0
```

### Example 9 - Test FancyZones configuration

This example tests whether FancyZones is configured for multi-monitor use.

```powershell
$desired = @{
    settings = @{
        properties = @{
            fancyzones_moveWindowsAcrossMonitors = $true
            fancyzones_openWindowOnActiveMonitor = $true
        }
        name = "FancyZones"
        version = "1.0"
    }
} | ConvertTo-Json -Depth 10 -Compress

$result = PowerToys.DSC.exe test --resource 'settings' --module FancyZones --input $desired | ConvertFrom-Json

if ($result._inDesiredState) {
    Write-Host "FancyZones is configured for multi-monitor"
} else {
    Write-Host "FancyZones configuration needs updating"
}
```

### Example 10 - Get FancyZones schema

This example retrieves the complete JSON schema for FancyZones properties.

```powershell
PowerToys.DSC.exe schema --resource 'settings' --module FancyZones | ConvertFrom-Json | ConvertTo-Json -Depth 10
```

## Use cases

### Development workflow

Configure FancyZones for efficient development with IDE, browser, and terminal windows:

```yaml
resources:
  - name: Developer layout
    type: Microsoft.PowerToys/FancyZonesSettings
    properties:
      settings:
        properties:
          fancyzones_shiftDrag: true
          fancyzones_overrideSnapHotkeys: true
          fancyzones_appLastZone_moveWindows: true
        name: FancyZones
        version: 1.0
```

### Presentation mode

Optimize window management for presentations and screen sharing:

```yaml
resources:
  - name: Presentation layout
    type: Microsoft.PowerToys/FancyZonesSettings
    properties:
      settings:
        properties:
          fancyzones_openWindowOnActiveMonitor: true
          fancyzones_highlightOpacity: 30
          fancyzones_makeDraggedWindowTransparent: false
        name: FancyZones
        version: 1.0
```

### Home office setup

Configure for docking/undocking laptop scenarios:

```yaml
resources:
  - name: Home office configuration
    type: Microsoft.PowerToys/FancyZonesSettings
    properties:
      settings:
        properties:
          fancyzones_displayOrWorkAreaChange_moveWindows: true
          fancyzones_moveWindowsAcrossMonitors: true
          fancyzones_appLastZone_moveWindows: true
        name: FancyZones
        version: 1.0
```

## See also

- [Settings Resource](../settings-resource.md)
- [PowerToys DSC Overview](../overview.md)
- [App Module](./App.md) - For enabling/disabling FancyZones
- [PowerToys FancyZones Documentation](https://learn.microsoft.com/windows/powertoys/fancyzones)
