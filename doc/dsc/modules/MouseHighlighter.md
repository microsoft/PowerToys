---
description: DSC configuration reference for PowerToys MouseHighlighter module
ms.date:     10/18/2025
ms.topic:    reference
title:       MouseHighlighter Module
---

# MouseHighlighter Module

## Synopsis

Manages configuration for the Mouse Highlighter utility, which highlights your mouse cursor and clicks.

## Description

The `MouseHighlighter` module configures PowerToys Mouse Highlighter, a utility that adds visual highlights to your mouse cursor and click locations. This is useful for presentations, tutorials, screen recordings, or accessibility purposes.

## Properties

The MouseHighlighter module supports the following configurable properties:

### ActivationShortcut

Sets the keyboard shortcut to toggle mouse highlighting.

**Type:** object  
**Properties:**
- `win` (boolean) - Windows key modifier
- `ctrl` (boolean) - Ctrl key modifier
- `alt` (boolean) - Alt key modifier
- `shift` (boolean) - Shift key modifier
- `code` (integer) - Virtual key code
- `key` (string) - Key name

**Default:** `Win + Shift + H`

### LeftButtonClickColor

Sets the color for left mouse button clicks.

**Type:** string (hex color)  
**Format:** `"#RRGGBB"`  
**Default:** `"#FFFF00"` (yellow)

### RightButtonClickColor

Sets the color for right mouse button clicks.

**Type:** string (hex color)  
**Format:** `"#RRGGBB"`  
**Default:** `"#0000FF"` (blue)

### HighlightOpacity

Sets the opacity of click highlights (0-100).

**Type:** integer  
**Range:** `0` to `100`  
**Default:** `160`

### HighlightRadius

Sets the radius of click highlights in pixels.

**Type:** integer  
**Range:** `1` to `500`  
**Default:** `20`

### HighlightFadeDelayMs

Sets how long highlights remain visible in milliseconds.

**Type:** integer  
**Range:** `0` to `10000`  
**Default:** `500`

### HighlightFadeDurationMs

Sets the duration of the highlight fade animation in milliseconds.

**Type:** integer  
**Range:** `0` to `10000`  
**Default:** `250`

### AutoActivate

Controls whether Mouse Highlighter activates automatically during presentations.

**Type:** boolean  
**Default:** `false`

## Examples

### Example 1 - Configure highlight colors with direct execution

This example customizes the click highlight colors.

```powershell
$config = @{
    settings = @{
        properties = @{
            LeftButtonClickColor = "#00FF00"
            RightButtonClickColor = "#FF0000"
            HighlightOpacity = 200
        }
        name = "MouseHighlighter"
        version = "1.0"
    }
} | ConvertTo-Json -Depth 10 -Compress

PowerToys.DSC.exe set --resource 'settings' --module MouseHighlighter --input $config
```

### Example 2 - Configure highlight animation with DSC

This example customizes the animation timing and appearance.

```yaml
# mousehighlighter-animation.dsc.yaml
$schema: https://aka.ms/dsc/schemas/v3/bundled/config/document.json
resources:
  - name: Configure Mouse Highlighter animation
    type: Microsoft.PowerToys/MouseHighlighterSettings
    properties:
      settings:
        properties:
          HighlightRadius: 30
          HighlightFadeDelayMs: 750
          HighlightFadeDurationMs: 400
        name: MouseHighlighter
        version: 1.0
```

### Example 3 - Install and configure for presentations with WinGet

This example installs PowerToys and configures Mouse Highlighter for presentations.

```yaml
# winget-mousehighlighter.yaml
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
  
  - name: Configure Mouse Highlighter for presentations
    type: Microsoft.PowerToys/MouseHighlighterSettings
    properties:
      settings:
        properties:
          LeftButtonClickColor: "#FFD700"
          RightButtonClickColor: "#FF4500"
          HighlightOpacity: 220
          HighlightRadius: 25
          AutoActivate: true
        name: MouseHighlighter
        version: 1.0
```

### Example 4 - Subtle highlighting

This example configures subtle, less distracting highlights.

```yaml
# mousehighlighter-subtle.dsc.yaml
$schema: https://aka.ms/dsc/schemas/v3/bundled/config/document.json
resources:
  - name: Subtle mouse highlighting
    type: Microsoft.PowerToys/MouseHighlighterSettings
    properties:
      settings:
        properties:
          HighlightOpacity: 100
          HighlightRadius: 15
          HighlightFadeDelayMs: 300
        name: MouseHighlighter
        version: 1.0
```

### Example 5 - High visibility for accessibility

This example configures high-contrast, long-lasting highlights.

```powershell
$config = @{
    settings = @{
        properties = @{
            LeftButtonClickColor = "#FFFFFF"
            RightButtonClickColor = "#FF0000"
            HighlightOpacity = 255
            HighlightRadius = 40
            HighlightFadeDelayMs = 1500
            HighlightFadeDurationMs = 500
        }
        name = "MouseHighlighter"
        version = "1.0"
    }
} | ConvertTo-Json -Depth 10 -Compress

PowerToys.DSC.exe set --resource 'settings' --module MouseHighlighter --input $config
```

## Use cases

### Presentations and demos

Configure for clear visibility during presentations:

```yaml
resources:
  - name: Presentation highlighting
    type: Microsoft.PowerToys/MouseHighlighterSettings
    properties:
      settings:
        properties:
          LeftButtonClickColor: "#FFD700"
          HighlightOpacity: 200
          HighlightRadius: 25
          AutoActivate: true
        name: MouseHighlighter
        version: 1.0
```

### Screen recording

Configure for video tutorials and recordings:

```yaml
resources:
  - name: Recording configuration
    type: Microsoft.PowerToys/MouseHighlighterSettings
    properties:
      settings:
        properties:
          HighlightOpacity: 180
          HighlightFadeDelayMs: 600
        name: MouseHighlighter
        version: 1.0
```

## See also

- [Settings Resource][01]
- [PowerToys DSC Overview][02]
- [App Module][03] - For enabling/disabling Mouse Highlighter utility
- [PowerToys Mouse Utilities Documentation][04]

<!-- Link reference definitions -->
[01]: ../settings-resource.md
[02]: ../overview.md
[03]: ./App.md
[04]: https://learn.microsoft.com/windows/powertoys/mouse-utilities
- [PowerToys Mouse Utilities Documentation](https://learn.microsoft.com/windows/powertoys/mouse-utilities)
