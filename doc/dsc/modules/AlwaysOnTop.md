---
description: DSC configuration reference for PowerToys AlwaysOnTop module
ms.date:     10/18/2025
ms.topic:    reference
title:       AlwaysOnTop Module
---

# AlwaysOnTop Module

## Synopsis

Manages configuration for the Always On Top utility, which pins windows to stay on top of other windows.

## Description

The `AlwaysOnTop` module configures PowerToys Always On Top, a utility that
allows you to pin any window to remain visible above all other windows. This
is useful for keeping reference materials, chat windows, or monitoring tools
visible while working with other applications.

## Properties

The AlwaysOnTop module supports the following configurable properties:

### Hotkey

Sets the keyboard shortcut to toggle Always On Top for the active window.

**Type:** object  
**Properties:**

- `win` (boolean) - Windows key modifier.
- `ctrl` (boolean) - Ctrl key modifier.
- `alt` (boolean) - Alt key modifier.
- `shift` (boolean) - Shift key modifier.
- `code` (integer) - Virtual key code.
- `key` (string) - Key name.

**Default:** `Win+Ctrl+T`

### FrameEnabled

Controls whether a colored border is displayed around pinned windows.

**Type:** boolean  
**Default:** `true`

### FrameThickness

Sets the thickness of the border around pinned windows (in pixels).

**Type:** integer  
**Range:** `1` to `100`  
**Default:** `5`

### FrameColor

Sets the color of the border around pinned windows.

**Type:** string (hex color)  
**Format:** `"#RRGGBB"`  
**Default:** `"#FF0000"` (red)

### FrameOpacity

Sets the opacity of the border (0-100).

**Type:** integer  
**Range:** `0` to `100`  
**Default:** `100`

### FrameAccentColor

Controls whether to use the Windows accent color for the frame.

**Type:** boolean  
**Default:** `false`

### SoundEnabled

Controls whether a sound plays when toggling Always On Top.

**Type:** boolean  
**Default:** `false`

### DoNotActivateOnGameMode

Controls whether Always On Top is automatically disabled during game mode.

**Type:** boolean  
**Default:** `true`

### RoundCornersEnabled

Controls whether the frame has rounded corners.

**Type:** boolean  
**Default:** `true`

### ExcludedApps

List of applications excluded from Always On Top functionality.

**Type:** string (newline-separated list of executable names)

## Examples

### Example 1 - Enable with default settings using direct execution

This example enables Always On Top with default border appearance.

```powershell
$config = @{
    settings = @{
        properties = @{
            FrameEnabled = $true
            FrameThickness = 5
            FrameColor = "#FF0000"
            FrameOpacity = 100
        }
        name = "AlwaysOnTop"
        version = "1.0"
    }
} | ConvertTo-Json -Depth 10 -Compress

PowerToys.DSC.exe set --resource 'settings' --module AlwaysOnTop `
  --input $config
```

### Example 2 - Customize frame appearance with DSC

This example configures a custom border color and thickness.

```bash
dsc config set --file alwaysontop-appearance.dsc.yaml
```

```yaml
# alwaysontop-appearance.dsc.yaml
$schema: https://aka.ms/dsc/schemas/v3/bundled/config/document.json
resources:
  - name: Customize Always On Top frame
    type: Microsoft.PowerToys/AlwaysOnTopSettings
    properties:
      settings:
        properties:
          FrameEnabled: true
          FrameThickness: 8
          FrameColor: "#0078D7"
          FrameOpacity: 80
          RoundCornersEnabled: true
        name: AlwaysOnTop
        version: 1.0
```

### Example 3 - Configure with accent color using WinGet

This example installs PowerToys and configures Always On Top to use the
Windows accent color.

```bash
winget configure winget-alwaysontop.yaml
```

```yaml
# winget-alwaysontop.yaml
$schema: https://raw.githubusercontent.com/PowerShell/DSC/main/schemas/2023/08/config/document.json
metadata:
  winget:
    processor: dscv3
resources:
  - name: Install PowerToys
    type: Microsoft.WinGet.DSC/WinGetPackage
    properties:
      id: Microsoft.PowerToys
      source: winget
  
  - name: Configure Always On Top
    type: Microsoft.PowerToys/AlwaysOnTopSettings
    properties:
      settings:
        properties:
          FrameEnabled: true
          FrameAccentColor: true
          FrameThickness: 6
          SoundEnabled: true
        name: AlwaysOnTop
        version: 1.0
```

### Example 4 - Disable for gaming

This example ensures Always On Top is disabled during game mode.

```yaml
# alwaysontop-gaming.dsc.yaml
$schema: https://aka.ms/dsc/schemas/v3/bundled/config/document.json
resources:
  - name: Configure for gaming
    type: Microsoft.PowerToys/AlwaysOnTopSettings
    properties:
      settings:
        properties:
          DoNotActivateOnGameMode: true
        name: AlwaysOnTop
        version: 1.0
```

### Example 5 - Minimal border configuration

This example configures a subtle, thin border.

```powershell
$config = @{
    settings = @{
        properties = @{
            FrameEnabled = $true
            FrameThickness = 2
            FrameOpacity = 50
            RoundCornersEnabled = true
        }
        name = "AlwaysOnTop"
        version = "1.0"
    }
} | ConvertTo-Json -Depth 10 -Compress

PowerToys.DSC.exe set --resource 'settings' --module AlwaysOnTop --input $config
```

### Example 6 - Exclude specific applications

This example excludes certain applications from Always On Top.

```yaml
# alwaysontop-exclusions.dsc.yaml
$schema: https://aka.ms/dsc/schemas/v3/bundled/config/document.json
resources:
  - name: Exclude apps from Always On Top
    type: Microsoft.PowerToys/AlwaysOnTopSettings
    properties:
      settings:
        properties:
          ExcludedApps: |
            Game.exe
            FullScreenApp.exe
        name: AlwaysOnTop
        version: 1.0
```

## Use cases

### Reference material

Keep documentation or reference windows visible:

```yaml
resources:
  - name: Reference window settings
    type: Microsoft.PowerToys/AlwaysOnTopSettings
    properties:
      settings:
        properties:
          FrameEnabled: true
          FrameColor: "#00FF00"
          FrameOpacity: 60
        name: AlwaysOnTop
        version: 1.0
```

### Monitoring dashboards

Pin monitoring tools and dashboards:

```yaml
resources:
  - name: Monitoring settings
    type: Microsoft.PowerToys/AlwaysOnTopSettings
    properties:
      settings:
        properties:
          FrameEnabled: true
          FrameAccentColor: true
          SoundEnabled: false
        name: AlwaysOnTop
        version: 1.0
```

## See also

- [Settings Resource][01]
- [PowerToys DSC Overview][02]
- [FancyZones Module][03] - Window layout manager
- [PowerToys Always On Top Documentation][04]

<!-- Link reference definitions -->
[01]: ../settings-resource.md
[02]: ../overview.md
[03]: ./FancyZones.md
[04]: https://learn.microsoft.com/windows/powertoys/always-on-top
