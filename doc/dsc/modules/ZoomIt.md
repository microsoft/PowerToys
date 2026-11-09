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

### ToggleKey

Sets the keyboard shortcut that toggles zoom mode. Like every hotkey property, the shortcut is wrapped in a `value` object.

**Type:** object  
**Properties of `value`:**

- `win` (boolean) - Windows key modifier
- `ctrl` (boolean) - Ctrl key modifier
- `alt` (boolean) - Alt key modifier
- `shift` (boolean) - Shift key modifier
- `code` (integer) - Virtual key code
- `key` (string) - Key name

**Default:** `Ctrl+1` (VK code 49)

The other ZoomIt settings (drawing, break timer, recording, webcam) use the property names of the ZoomIt settings model, for example `DrawToggleKey`, `BreakTimeout` or `RecordFormat`. Run `PowerToys.DSC.exe schema --resource 'settings' --module ZoomIt` for the complete list.

## Examples

### Example 1 - Configure activation shortcut with direct execution

This example sets a custom keyboard shortcut to activate ZoomIt.

```powershell
$config = @{
    settings = @{
        properties = @{
            ToggleKey = @{
                value = @{
                    win = $false
                    ctrl = $true
                    alt = $false
                    shift = $true
                    code = 90
                    key = "Z"
                }
            }
        }
        name = "ZoomIt"
        version = "1.0"
    }
} | ConvertTo-Json -Depth 10 -Compress

PowerToys.DSC.exe set --resource 'settings' --module ZoomIt --input $config
```

### Example 2 - Configure with Microsoft DSC

This example configures the ZoomIt activation shortcut using Microsoft DSC.

```bash
dsc config set --file zoomit-config.dsc.yaml
```

```yaml
# zoomit-config.dsc.yaml
$schema: https://aka.ms/dsc/schemas/v3/bundled/config/document.json
resources:
  - name: Configure ZoomIt shortcut
    type: Microsoft.PowerToys/ZoomItSettings
    properties:
      settings:
        properties:
          ToggleKey:
            value:
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

This example installs PowerToys and configures ZoomIt using WinGet.

```bash
winget configure winget-zoomit.yaml
```

```yaml
# winget-zoomit.yaml
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
  
  - name: Configure ZoomIt
    type: Microsoft.PowerToys/ZoomItSettings
    properties:
      settings:
        properties:
          ToggleKey:
            value:
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

```bash
dsc config set --file zoomit-presentation.dsc.yaml
```

```yaml
# zoomit-presentation.dsc.yaml
$schema: https://aka.ms/dsc/schemas/v3/bundled/config/document.json
resources:
  - name: Presentation hotkey
    type: Microsoft.PowerToys/ZoomItSettings
    properties:
      settings:
        properties:
          ToggleKey:
            value:
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
          ToggleKey:
            value:
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
          ToggleKey:
            value:
              win: true
              ctrl: false
              alt: false
              shift: true
              code: 90
              key: Z
        name: ZoomIt
        version: 1.0
```

## Important notes

> **Note:** ZoomIt stores its settings in the registry
> (`HKCU\Software\Sysinternals\ZoomIt`), not in a `settings.json` file. The
> `settings` resource reads and writes them through the ZoomIt settings
> interop, the same component the PowerToys Settings app uses. Properties that
> are not part of the configuration keep their current value. A running ZoomIt
> instance reloads the settings immediately; otherwise they take effect the
> next time ZoomIt starts.

## See also

- [Settings Resource][01]
- [PowerToys DSC Overview][02]
- [CropAndLock Module][03] - For additional PowerToys configuration
- [PowerToys ZoomIt Documentation][04]

<!-- Link reference definitions -->
[01]: ../settings-resource.md
[02]: ../overview.md
[03]: ./CropAndLock.md
[04]: https://learn.microsoft.com/windows/powertoys/zoomit
