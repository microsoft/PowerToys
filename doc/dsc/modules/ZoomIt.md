---
description: DSC configuration reference for PowerToys ZoomIt module
ms.date:     10/18/2025
ms.topic:    reference
title:       ZoomIt Module
---

# ZoomIt Module

## Synopsis

Manages configuration for the ZoomIt utility, which provides screen zoom, annotation, and presentation tools.

## Description

The `ZoomIt` module configures PowerToys ZoomIt, a screen zoom and annotation utility for presentations and demonstrations. It provides live zoom, screen drawing, a break timer, and other presentation features activated through customizable keyboard shortcuts.

## Properties

The ZoomIt module supports the following configurable properties:

### ActivationShortcut

Sets the keyboard shortcut to activate the zoom mode.

**Type:** object  
**Properties:**
- `win` (boolean) - Windows key modifier
- `ctrl` (boolean) - Ctrl key modifier
- `alt` (boolean) - Alt key modifier
- `shift` (boolean) - Shift key modifier
- `code` (integer) - Virtual key code
- `key` (string) - Key name

**Default:** `Ctrl + 1` (VK code 49)

## Examples

### Example 1 - Configure activation shortcut with direct execution

This example sets a custom keyboard shortcut to activate ZoomIt.

```powershell
$config = @{
    settings = @{
        properties = @{
            ActivationShortcut = @{
                win = $false
                ctrl = $true
                alt = $false
                shift = $true
                code = 90
                key = "Z"
            }
        }
        name = "ZoomIt"
        version = "1.0"
    }
} | ConvertTo-Json -Depth 10 -Compress

PowerToys.DSC.exe set --resource 'settings' --module ZoomIt --input $config
```

### Example 2 - Configure with DSC

This example configures the ZoomIt activation shortcut using DSC.

```yaml
# zoomit-config.dsc.yaml
$schema: https://aka.ms/dsc/schemas/v3/bundled/config/document.json
resources:
  - name: Configure ZoomIt shortcut
    type: Microsoft.PowerToys/ZoomItSettings
    properties:
      settings:
        properties:
          ActivationShortcut:
            win: false
            ctrl: true
            alt: false
            shift: false
            code: 49
            key: "1"
        name: ZoomIt
        version: 1.0
```

### Example 3 - Install and configure with WinGet

This example installs PowerToys and configures ZoomIt.

```yaml
# winget-zoomit.yaml
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
  
  - name: Configure ZoomIt
    type: Microsoft.PowerToys/ZoomItSettings
    properties:
      settings:
        properties:
          ActivationShortcut:
            win: false
            ctrl: true
            alt: false
            shift: true
            code: 90
            key: Z
        name: ZoomIt
        version: 1.0
```

### Example 4 - Presentation mode hotkey

This example configures an easy-to-remember presentation hotkey.

```yaml
# zoomit-presentation.dsc.yaml
$schema: https://aka.ms/dsc/schemas/v3/bundled/config/document.json
resources:
  - name: Presentation hotkey
    type: Microsoft.PowerToys/ZoomItSettings
    properties:
      settings:
        properties:
          ActivationShortcut:
            win: true
            ctrl: false
            alt: false
            shift: false
            code: 187
            key: "="
        name: ZoomIt
        version: 1.0
```

## Use cases

### Presentations

Configure for easy screen zooming during presentations:

```yaml
resources:
  - name: Presentation setup
    type: Microsoft.PowerToys/ZoomItSettings
    properties:
      settings:
        properties:
          ActivationShortcut:
            win: false
            ctrl: true
            alt: false
            shift: false
            code: 49
            key: "1"
        name: ZoomIt
        version: 1.0
```

### Screen recording

Configure for quick access during screen recording sessions:

```yaml
resources:
  - name: Recording setup
    type: Microsoft.PowerToys/ZoomItSettings
    properties:
      settings:
        properties:
          ActivationShortcut:
            win: true
            ctrl: false
            alt: false
            shift: true
            code: 90
            key: Z
        name: ZoomIt
        version: 1.0
```

## See also

- [Settings Resource][01]
- [PowerToys DSC Overview][02]
- [App Module][03] - For enabling/disabling ZoomIt utility
- [PowerToys ZoomIt Documentation][04]

<!-- Link reference definitions -->
[01]: ../settings-resource.md
[02]: ../overview.md
[03]: ./App.md
[04]: https://learn.microsoft.com/windows/powertoys/zoomit
