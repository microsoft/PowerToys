---
description: DSC configuration reference for PowerToys ShortcutGuide module
ms.date:     10/18/2025
ms.topic:    reference
title:       ShortcutGuide Module
---

# ShortcutGuide Module

## Synopsis

Manages configuration for the Shortcut Guide utility, which displays available keyboard shortcuts.

## Description

The `ShortcutGuide` module configures PowerToys Shortcut Guide, a utility that
displays an overlay showing available Windows keyboard shortcuts when you hold
the Windows key. It helps users discover and learn keyboard shortcuts.

## Properties

The ShortcutGuide module supports the following configurable properties:

### OpenShortcutGuide

Sets the keyboard shortcut or method to open the shortcut guide.

**Type:** object  
**Properties:**

- `win` (boolean) - Windows key modifier
- `ctrl` (boolean) - Ctrl key modifier
- `alt` (boolean) - Alt key modifier
- `shift` (boolean) - Shift key modifier
- `code` (integer) - Virtual key code
- `key` (string) - Key name

**Default:** Hold Windows key for 900ms

### OverlayOpacity

Sets the opacity of the shortcut guide overlay (0-100).

**Type:** integer  
**Range:** `0` to `100`  
**Default:** `90`

### Theme

Sets the theme for the shortcut guide.

**Type:** string  
**Allowed values:** `"light"`, `"dark"`, `"system"`  
**Default:** `"dark"`

### PressTime

Sets how long the Windows key must be held before showing the guide (in milliseconds).

**Type:** integer  
**Range:** `100` to `10000`  
**Default:** `900`

### ExcludedApps

List of applications where Shortcut Guide is disabled.

**Type:** string (newline-separated list of executable names)

## Examples

### Example 1 - Configure activation time with direct execution

This example sets a faster activation time for the shortcut guide.

```powershell
$config = @{
    settings = @{
        properties = @{
            PressTime = 600
        }
        name = "ShortcutGuide"
        version = "1.0"
    }
} | ConvertTo-Json -Depth 10 -Compress

PowerToys.DSC.exe set --resource 'settings' --module ShortcutGuide `
    --input $config
```

### Example 2 - Configure appearance with DSC

This example customizes the overlay appearance.

```bash
dsc config set --file shortcutguide-appearance.dsc.yaml
```

```yaml
# shortcutguide-appearance.dsc.yaml
$schema: https://aka.ms/dsc/schemas/v3/bundled/config/document.json
resources:
  - name: Configure Shortcut Guide appearance
    type: Microsoft.PowerToys/ShortcutGuideSettings
    properties:
      settings:
        properties:
          OverlayOpacity: 95
          Theme: light
        name: ShortcutGuide
        version: 1.0
```

### Example 3 - Install and configure with WinGet

This example installs PowerToys and configures Shortcut Guide.

```bash
winget configure winget-shortcutguide.yaml
```

```yaml
# winget-shortcutguide.yaml
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
  
  - name: Configure Shortcut Guide
    type: Microsoft.PowerToys/ShortcutGuideSettings
    properties:
      settings:
        properties:
          PressTime: 700
          OverlayOpacity: 90
          Theme: dark
        name: ShortcutGuide
        version: 1.0
```

### Example 4 - Quick activation

This example configures for quick activation with a short press time.

```bash
dsc config set --file shortcutguide-quick.dsc.yaml
```

```yaml
# shortcutguide-quick.dsc.yaml
$schema: https://aka.ms/dsc/schemas/v3/bundled/config/document.json
resources:
  - name: Quick activation
    type: Microsoft.PowerToys/ShortcutGuideSettings
    properties:
      settings:
        properties:
          PressTime: 400
        name: ShortcutGuide
        version: 1.0
```

### Example 5 - High opacity for visibility

This example maximizes opacity for better visibility.

```powershell
$config = @{
    settings = @{
        properties = @{
            OverlayOpacity = 100
            Theme = "dark"
        }
        name = "ShortcutGuide"
        version = "1.0"
    }
} | ConvertTo-Json -Depth 10 -Compress

PowerToys.DSC.exe set --resource 'settings' --module ShortcutGuide --input $config
```

### Example 6 - Exclude applications

This example excludes Shortcut Guide from specific applications.

```bash
dsc config set --file shortcutguide-exclusions.dsc.yaml
```

```yaml
# shortcutguide-exclusions.dsc.yaml
$schema: https://aka.ms/dsc/schemas/v3/bundled/config/document.json
resources:
  - name: Exclude apps
    type: Microsoft.PowerToys/ShortcutGuideSettings
    properties:
      settings:
        properties:
          ExcludedApps: |
            Game.exe
            FullScreenApp.exe
        name: ShortcutGuide
        version: 1.0
```

## Use cases

### New users

Configure for easy keyboard shortcut discovery:

```yaml
resources:
  - name: New user configuration
    type: Microsoft.PowerToys/ShortcutGuideSettings
    properties:
      settings:
        properties:
          PressTime: 800
          OverlayOpacity: 95
        name: ShortcutGuide
        version: 1.0
```

### Power users

Configure for quick access without accidental activation:

```yaml
resources:
  - name: Power user configuration
    type: Microsoft.PowerToys/ShortcutGuideSettings
    properties:
      settings:
        properties:
          PressTime: 1200
          OverlayOpacity: 85
        name: ShortcutGuide
        version: 1.0
```

## See also

- [Settings Resource][01]
- [PowerToys DSC Overview][02]
- [Peek][03]
- [PowerToys Keyboard Shortcut Guide Documentation][04]

<!-- Link reference definitions -->
[01]: ../settings-resource.md
[02]: ../overview.md
[03]: ./Peek.md
[04]: https://learn.microsoft.com/windows/powertoys/shortcut-guide
