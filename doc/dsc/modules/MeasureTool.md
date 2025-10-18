---
description: DSC configuration reference for PowerToys MeasureTool module
ms.date:     10/18/2025
ms.topic:    reference
title:       MeasureTool Module
---

# MeasureTool Module

## Synopsis

Manages configuration for the Measure Tool (Screen Ruler) utility, which measures pixels on your screen.

## Description

The `MeasureTool` module configures PowerToys Measure Tool (also known as Screen Ruler), a utility that allows you to measure the distance between two points on your screen in pixels. It's useful for designers, developers, and anyone who needs to measure UI elements or screen distances.

## Properties

The MeasureTool module supports the following configurable properties:

### ActivationShortcut

Sets the keyboard shortcut to activate the measure tool.

**Type:** object  
**Properties:**
- `win` (boolean) - Windows key modifier
- `ctrl` (boolean) - Ctrl key modifier
- `alt` (boolean) - Alt key modifier
- `shift` (boolean) - Shift key modifier
- `code` (integer) - Virtual key code
- `key` (string) - Key name

**Default:** `Win + Shift + M`

### ContinuousCapture

Controls whether continuous capture mode is enabled.

**Type:** boolean  
**Default:** `false`

### DrawFeetOnCross

Controls whether measurement lines extend to screen edges.

**Type:** boolean  
**Default:** `true`

### PerColorChannelEdgeDetection

Controls whether edge detection is per-color-channel or luminosity-based.

**Type:** boolean  
**Default:** `false`

### PixelTolerance

Sets the pixel tolerance for edge detection (0-255).

**Type:** integer  
**Range:** `0` to `255`  
**Default:** `30`

### MeasureCrossColor

Sets the color of the measurement crosshair.

**Type:** string (hex color)  
**Format:** `"#RRGGBBAA"` (with alpha)  
**Default:** `"#FF4500FF"`

## Examples

### Example 1 - Configure activation shortcut with direct execution

This example customizes the measure tool activation shortcut.

```powershell
$config = @{
    settings = @{
        properties = @{
            ActivationShortcut = @{
                win = $true
                ctrl = $false
                alt = $false
                shift = $true
                code = 77
                key = "M"
            }
        }
        name = "MeasureTool"
        version = "1.0"
    }
} | ConvertTo-Json -Depth 10 -Compress

PowerToys.DSC.exe set --resource 'settings' --module MeasureTool --input $config
```

### Example 2 - Configure measurement appearance with DSC

This example customizes the crosshair color and measurement behavior.

```yaml
# measuretool-appearance.dsc.yaml
$schema: https://aka.ms/dsc/schemas/v3/bundled/config/document.json
resources:
  - name: Configure Measure Tool appearance
    type: Microsoft.PowerToys/MeasureToolSettings
    properties:
      settings:
        properties:
          MeasureCrossColor: "#00FF00FF"
          DrawFeetOnCross: true
          ContinuousCapture: false
        name: MeasureTool
        version: 1.0
```

### Example 3 - Install and configure with WinGet

This example installs PowerToys and configures Measure Tool with edge detection.

```yaml
# winget-measuretool.yaml
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
  
  - name: Configure Measure Tool
    type: Microsoft.PowerToys/MeasureToolSettings
    properties:
      settings:
        properties:
          PixelTolerance: 20
          PerColorChannelEdgeDetection: true
          DrawFeetOnCross: true
        name: MeasureTool
        version: 1.0
```

### Example 4 - High contrast configuration

This example configures for high visibility measurements.

```yaml
# measuretool-highcontrast.dsc.yaml
$schema: https://aka.ms/dsc/schemas/v3/bundled/config/document.json
resources:
  - name: High contrast Measure Tool
    type: Microsoft.PowerToys/MeasureToolSettings
    properties:
      settings:
        properties:
          MeasureCrossColor: "#FFFF00FF"
          DrawFeetOnCross: true
        name: MeasureTool
        version: 1.0
```

### Example 5 - Continuous capture mode

This example enables continuous capture for repeated measurements.

```powershell
$config = @{
    settings = @{
        properties = @{
            ContinuousCapture = $true
            PixelTolerance = 25
        }
        name = "MeasureTool"
        version = "1.0"
    }
} | ConvertTo-Json -Depth 10 -Compress

PowerToys.DSC.exe set --resource 'settings' --module MeasureTool --input $config
```

## Use cases

### UI/UX design

Configure for design work with precise measurements:

```yaml
resources:
  - name: Design configuration
    type: Microsoft.PowerToys/MeasureToolSettings
    properties:
      settings:
        properties:
          PixelTolerance: 15
          DrawFeetOnCross: true
        name: MeasureTool
        version: 1.0
```

### Web development

Configure for layout debugging:

```yaml
resources:
  - name: Developer configuration
    type: Microsoft.PowerToys/MeasureToolSettings
    properties:
      settings:
        properties:
          ContinuousCapture: true
          MeasureCrossColor: "#0078D7FF"
        name: MeasureTool
        version: 1.0
```

## See also

- [Settings Resource][01]
- [PowerToys DSC Overview][02]
- [App Module][03] - For enabling/disabling Measure Tool utility
- [PowerToys Screen Ruler Documentation][04]

<!-- Link reference definitions -->
[01]: ../settings-resource.md
[02]: ../overview.md
[03]: ./App.md
[04]: https://learn.microsoft.com/windows/powertoys/screen-ruler
- [PowerToys Screen Ruler Documentation](https://learn.microsoft.com/windows/powertoys/screen-ruler)
