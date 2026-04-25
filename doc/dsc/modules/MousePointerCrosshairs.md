---
description: DSC configuration reference for PowerToys MousePointerCrosshairs module
ms.date:     10/18/2025
ms.topic:    reference
title:       MousePointerCrosshairs Module
---

# MousePointerCrosshairs Module

## Synopsis

Manages configuration for the Mouse Pointer Crosshairs utility, which
displays crosshairs centered on your mouse pointer.

## Description

The `MousePointerCrosshairs` module configures PowerToys Mouse Pointer
Crosshairs, a utility that displays customizable crosshairs overlaid on your
screen, centered on the mouse cursor. This is useful for presentations,
design work, or improving cursor visibility.

## Properties

The MousePointerCrosshairs module supports the following configurable properties:

### ActivationShortcut

Sets the keyboard shortcut to toggle crosshairs display.

**Type:** object  
**Properties:**

- `win` (boolean) - Windows key modifier
- `ctrl` (boolean) - Ctrl key modifier
- `alt` (boolean) - Alt key modifier
- `shift` (boolean) - Shift key modifier
- `code` (integer) - Virtual key code
- `key` (string) - Key name

**Default:** `Win+Alt+P`

### CrosshairsColor

Sets the color of the crosshairs.

**Type:** string (hex color)  
**Format:** `"#RRGGBB"`  
**Default:** `"#FF0000"` (red)

### CrosshairsOpacity

Sets the opacity of the crosshairs (0-100).

**Type:** integer  
**Range:** `0` to `100`  
**Default:** `75`

### CrosshairsRadius

Sets the length of the crosshair lines in pixels.

**Type:** integer  
**Range:** `0` to `9999`  
**Default:** `100`

### CrosshairsThickness

Sets the thickness of the crosshair lines in pixels.

**Type:** integer  
**Range:** `1` to `50`  
**Default:** `5`

### CrosshairsBorderColor

Sets the border color of the crosshairs.

**Type:** string (hex color)  
**Format:** `"#RRGGBB"`  
**Default:** `"#FFFFFF"` (white)

### CrosshairsBorderSize

Sets the width of the crosshair border in pixels.

**Type:** integer  
**Range:** `0` to `50`  
**Default:** `1`

### CrosshairsAutoHide

Controls whether crosshairs automatically hide when the mouse is not moving.

**Type:** boolean  
**Default:** `false`

### CrosshairsIsFixedLengthEnabled

Controls whether crosshairs have a fixed length or extend to screen edges.

**Type:** boolean  
**Default:** `true`

### CrosshairsFixedLength

Sets the fixed length of crosshairs when fixed length mode is enabled.

**Type:** integer  
**Range:** `0` to `9999`  
**Default:** `100`

## Examples

### Example 1 - Configure crosshair appearance with direct execution

This example customizes the crosshair color and size.

```powershell
$config = @{
    settings = @{
        properties = @{
            CrosshairsColor = "#00FF00"
            CrosshairsOpacity = 85
            CrosshairsThickness = 3
            CrosshairsRadius = 150
        }
        name = "MousePointerCrosshairs"
        version = "1.0"
    }
} | ConvertTo-Json -Depth 10 -Compress

PowerToys.DSC.exe set --resource 'settings' --module MousePointerCrosshairs `
    --input $config
```

### Example 2 - Configure with border with DSC

This example adds a border to the crosshairs for better visibility.

```bash
dsc config set --file mousecrosshairs-border.dsc.yaml
```

```yaml
# mousecrosshairs-border.dsc.yaml
$schema: https://aka.ms/dsc/schemas/v3/bundled/config/document.json
resources:
  - name: Configure crosshairs with border
    type: Microsoft.PowerToys/MousePointerCrosshairsSettings
    properties:
      settings:
        properties:
          CrosshairsColor: "#FF0000"
          CrosshairsBorderColor: "#FFFFFF"
          CrosshairsBorderSize: 2
          CrosshairsThickness: 4
        name: MousePointerCrosshairs
        version: 1.0
```

### Example 3 - Install and configure with WinGet

This example installs PowerToys and configures crosshairs for presentations.

```bash
winget configure winget-mousecrosshairs.yaml
```

```yaml
# winget-mousecrosshairs.yaml
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
  
  - name: Configure Mouse Crosshairs
    type: Microsoft.PowerToys/MousePointerCrosshairsSettings
    properties:
      settings:
        properties:
          CrosshairsColor: "#FFFF00"
          CrosshairsOpacity: 90
          CrosshairsRadius: 120
          CrosshairsThickness: 5
          CrosshairsBorderSize: 2
        name: MousePointerCrosshairs
        version: 1.0
```

### Example 4 - Full-screen crosshairs

This example configures crosshairs that extend to screen edges.

```bash
dsc config set --file mousecrosshairs-fullscreen.dsc.yaml
```

```yaml
# mousecrosshairs-fullscreen.dsc.yaml
$schema: https://aka.ms/dsc/schemas/v3/bundled/config/document.json
resources:
  - name: Full-screen crosshairs
    type: Microsoft.PowerToys/MousePointerCrosshairsSettings
    properties:
      settings:
        properties:
          CrosshairsIsFixedLengthEnabled: false
          CrosshairsOpacity: 60
        name: MousePointerCrosshairs
        version: 1.0
```

### Example 5 - Subtle crosshairs with auto-hide

This example creates subtle crosshairs that hide when idle.

```powershell
$config = @{
    settings = @{
        properties = @{
            CrosshairsColor = "#FFFFFF"
            CrosshairsOpacity = 50
            CrosshairsThickness = 2
            CrosshairsRadius = 80
            CrosshairsAutoHide = $true
        }
        name = "MousePointerCrosshairs"
        version = "1.0"
    }
} | ConvertTo-Json -Depth 10 -Compress

PowerToys.DSC.exe set --resource 'settings' --module MousePointerCrosshairs --input $config
```

## Use cases

### Presentations and demos

Configure for clear cursor tracking during presentations:

```yaml
resources:
  - name: Presentation crosshairs
    type: Microsoft.PowerToys/MousePointerCrosshairsSettings
    properties:
      settings:
        properties:
          CrosshairsColor: "#FFFF00"
          CrosshairsOpacity: 85
          CrosshairsRadius: 150
        name: MousePointerCrosshairs
        version: 1.0
```

### Design and alignment

Configure for precise alignment work:

```yaml
resources:
  - name: Design crosshairs
    type: Microsoft.PowerToys/MousePointerCrosshairsSettings
    properties:
      settings:
        properties:
          CrosshairsIsFixedLengthEnabled: false
          CrosshairsThickness: 1
          CrosshairsOpacity: 70
        name: MousePointerCrosshairs
        version: 1.0
```

## See also

- [Settings Resource][01]
- [PowerToys DSC Overview][02]
- [MouseHighlighter][03]
- [PowerToys Mouse Utilities Documentation][04]

<!-- Link reference definitions -->
[01]: ../settings-resource.md
[02]: ../overview.md
[03]: ./MouseHighlighter.md
[04]: https://learn.microsoft.com/windows/powertoys/mouse-utilities
