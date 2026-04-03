---
description: DSC configuration reference for PowerToys FindMyMouse module
ms.date:     10/18/2025
ms.topic:    reference
title:       FindMyMouse Module
---

# FindMyMouse Module

## Synopsis

Manages configuration for the Find My Mouse utility, which helps locate your
mouse cursor on the screen.

## Description

The `FindMyMouse` module configures PowerToys Find My Mouse, a utility that
highlights your mouse cursor location when you press the Ctrl key. This is
particularly useful on large or multiple displays where the cursor can be
difficult to locate.

## Properties

The FindMyMouse module supports the following configurable properties:

### DoNotActivateOnGameMode

Controls whether Find My Mouse is disabled during game mode.

**Type:** boolean  
**Default:** `true`  
**Description:** When enabled, Find My Mouse will not activate when Windows
game mode is active.

### BackgroundColor

Sets the background color of the spotlight effect.

**Type:** string (hex color)  
**Format:** `"#RRGGBB"`  
**Default:** `"#000000"` (black)

### SpotlightColor

Sets the color of the spotlight circle around the cursor.

**Type:** string (hex color)  
**Format:** `"#RRGGBB"`  
**Default:** `"#FFFFFF"` (white)

### OverlayOpacity

Sets the opacity of the background overlay (0-100).

**Type:** integer  
**Range:** `0` to `100`  
**Default:** `50`

### SpotlightRadius

Sets the radius of the spotlight in pixels.

**Type:** integer  
**Range:** `50` to `500`  
**Default:** `100`

### AnimationDurationMs

Sets the duration of the spotlight animation in milliseconds.

**Type:** integer  
**Range:** `0` to `5000`  
**Default:** `500`

### SpotlightInitialZoom

Sets the initial zoom level of the spotlight effect.

**Type:** integer  
**Range:** `100` to `1000`  
**Default:** `200`

### ExcludedApps

List of applications where Find My Mouse is disabled.

**Type:** string (newline-separated list of executable names)

## Examples

### Example 1 - Configure spotlight appearance with direct execution

This example customizes the spotlight colors and radius.

```powershell
$config = @{
    settings = @{
        properties = @{
            BackgroundColor = "#000000"
            SpotlightColor = "#00FF00"
            SpotlightRadius = 150
            OverlayOpacity = 60
        }
        name = "FindMyMouse"
        version = "1.0"
    }
} | ConvertTo-Json -Depth 10 -Compress

PowerToys.DSC.exe set --resource 'settings' --module FindMyMouse --input $config
```

### Example 2 - Configure animation with DSC

This example customizes the spotlight animation behavior.

```bash
dsc config set --file findmymouse-animation.dsc.yaml
```

```yaml
# findmymouse-animation.dsc.yaml
$schema: https://aka.ms/dsc/schemas/v3/bundled/config/document.json
resources:
  - name: Configure Find My Mouse animation
    type: Microsoft.PowerToys/FindMyMouseSettings
    properties:
      settings:
        properties:
          AnimationDurationMs: 750
          SpotlightInitialZoom: 300
          SpotlightRadius: 120
        name: FindMyMouse
        version: 1.0
```

### Example 3 - Install and configure with WinGet

This example installs PowerToys and configures Find My Mouse with custom
colors.

```bash
winget configure winget-findmymouse.yaml
```

```yaml
# winget-findmymouse.yaml
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
  
  - name: Configure Find My Mouse
    type: Microsoft.PowerToys/FindMyMouseSettings
    properties:
      settings:
        properties:
          BackgroundColor: "#000000"
          SpotlightColor: "#0078D7"
          OverlayOpacity: 70
          SpotlightRadius: 140
          DoNotActivateOnGameMode: true
        name: FindMyMouse
        version: 1.0
```

### Example 4 - Subtle configuration

This example creates a subtle, less intrusive spotlight effect.

```bash
dsc config set --file findmymouse-subtle.dsc.yaml
```

```yaml
# findmymouse-subtle.dsc.yaml
$schema: https://aka.ms/dsc/schemas/v3/bundled/config/document.json
resources:
  - name: Subtle spotlight
    type: Microsoft.PowerToys/FindMyMouseSettings
    properties:
      settings:
        properties:
          OverlayOpacity: 30
          SpotlightRadius: 100
          AnimationDurationMs: 300
        name: FindMyMouse
        version: 1.0
```

### Example 5 - High visibility configuration

This example creates a high-visibility spotlight for accessibility.

```powershell
$config = @{
    settings = @{
        properties = @{
            BackgroundColor = "#000000"
            SpotlightColor = "#FFFF00"
            OverlayOpacity = 80
            SpotlightRadius = 200
            SpotlightInitialZoom = 400
        }
        name = "FindMyMouse"
        version = "1.0"
    }
} | ConvertTo-Json -Depth 10 -Compress

PowerToys.DSC.exe set --resource 'settings' --module FindMyMouse --input $config
```

### Example 6 - Disable during gaming

This example ensures Find My Mouse doesn't interfere with games.

```bash
dsc config set --file findmymouse-gaming.dsc.yaml
```

```yaml
# findmymouse-gaming.dsc.yaml
$schema: https://aka.ms/dsc/schemas/v3/bundled/config/document.json
resources:
  - name: Gaming configuration
    type: Microsoft.PowerToys/FindMyMouseSettings
    properties:
      settings:
        properties:
          DoNotActivateOnGameMode: true
        name: FindMyMouse
        version: 1.0
```

## Use cases

### Large displays

Configure for high visibility on large screens:

```yaml
resources:
  - name: Large display configuration
    type: Microsoft.PowerToys/FindMyMouseSettings
    properties:
      settings:
        properties:
          SpotlightRadius: 180
          OverlayOpacity: 70
          SpotlightColor: "#FFFFFF"
        name: FindMyMouse
        version: 1.0
```

### Accessibility

Configure for maximum visibility:

```yaml
resources:
  - name: Accessibility configuration
    type: Microsoft.PowerToys/FindMyMouseSettings
    properties:
      settings:
        properties:
          SpotlightColor: "#FFFF00"
          OverlayOpacity: 80
          SpotlightRadius: 200
          AnimationDurationMs: 1000
        name: FindMyMouse
        version: 1.0
```

## See also

- [Settings Resource][01]
- [PowerToys DSC Overview][02]
- [MouseHighlighter][03]

<!-- Link reference definitions -->
[01]: ../settings-resource.md
[02]: ../overview.md
[03]: ./MouseHighlighter.md
