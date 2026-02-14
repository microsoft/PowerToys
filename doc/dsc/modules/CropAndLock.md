---
description: DSC configuration reference for PowerToys CropAndLock module
ms.date:     10/18/2025
ms.topic:    reference
title:       CropAndLock Module
---

# CropAndLock Module

## Synopsis

Manages configuration for the Crop And Lock utility, which crops and locks portions of windows.

## Description

The `CropAndLock` module configures PowerToys Crop And Lock, a utility that allows you to crop a portion of any window and keep it visible as a thumbnail. This is useful for monitoring specific parts of applications, keeping reference information visible, or focusing on particular UI elements.

## Properties

The CropAndLock module supports the following configurable properties:

### Hotkey

Sets the keyboard shortcut to activate Crop And Lock for the active window.

**Type:** object  
**Properties:**

- `win` (boolean) - Windows key modifier.
- `ctrl` (boolean) - Ctrl key modifier.
- `alt` (boolean) - Alt key modifier.
- `shift` (boolean) - Shift key modifier.
- `code` (integer) - Virtual key code.
- `key` (string) - Key name.

**Default:** `Win+Ctrl+Shift+T`

### ReparentHotkey

Sets the keyboard shortcut to change the parent window of a cropped thumbnail.

**Type:** object (same structure as Hotkey)

### ThumbnailOpacity

Sets the opacity of cropped thumbnails (0-100).

**Type:** integer  
**Range:** `0` to `100`  
**Default:** `100`

## Examples

### Example 1 - Configure basic settings with direct execution

This example sets the Crop And Lock hotkey and thumbnail opacity.

```powershell
$config = @{
    settings = @{
        properties = @{
            ThumbnailOpacity = 90
        }
        name = "CropAndLock"
        version = "1.0"
    }
} | ConvertTo-Json -Depth 10 -Compress

PowerToys.DSC.exe set --resource 'settings' --module CropAndLock --input $config
```

### Example 2 - Configure hotkeys with DSC

This example configures custom hotkeys for cropping and reparenting.

```bash
dsc config set --file cropandlock-hotkeys.dsc.yaml
```

```yaml
# cropandlock-hotkeys.dsc.yaml
$schema: https://aka.ms/dsc/schemas/v3/bundled/config/document.json
resources:
  - name: Configure Crop And Lock hotkeys
    type: Microsoft.PowerToys/CropAndLockSettings
    properties:
      settings:
        properties:
          Hotkey:
            win: true
            ctrl: true
            shift: true
            alt: false
            code: 84
            key: T
          ThumbnailOpacity: 85
        name: CropAndLock
        version: 1.0
```

### Example 3 - Install and configure with WinGet

This example installs PowerToys and configures Crop And Lock.

```bash
winget configure winget-cropandlock.yaml
```

```yaml
# winget-cropandlock.yaml
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
  
  - name: Configure Crop And Lock
    type: Microsoft.PowerToys/CropAndLockSettings
    properties:
      settings:
        properties:
          ThumbnailOpacity: 75
        name: CropAndLock
        version: 1.0
```

### Example 4 - Semi-transparent thumbnails

This example configures thumbnails to be semi-transparent for overlay use.

```yaml
# cropandlock-transparent.dsc.yaml
$schema: https://aka.ms/dsc/schemas/v3/bundled/config/document.json
resources:
  - name: Semi-transparent thumbnails
    type: Microsoft.PowerToys/CropAndLockSettings
    properties:
      settings:
        properties:
          ThumbnailOpacity: 60
        name: CropAndLock
        version: 1.0
```

## Use cases

### Monitoring dashboards

Keep portions of monitoring tools visible:

```yaml
resources:
  - name: Monitoring configuration
    type: Microsoft.PowerToys/CropAndLockSettings
    properties:
      settings:
        properties:
          ThumbnailOpacity: 80
        name: CropAndLock
        version: 1.0
```

### Reference material

Crop and display reference information:

```yaml
resources:
  - name: Reference display
    type: Microsoft.PowerToys/CropAndLockSettings
    properties:
      settings:
        properties:
          ThumbnailOpacity: 95
        name: CropAndLock
        version: 1.0
```

## See also

- [Settings Resource][01]
- [PowerToys DSC Overview][02]
- [MouseJump][03]
- [PowerToys Crop And Lock Documentation][04]

<!-- Link reference definitions -->
[01]: ../settings-resource.md
[02]: ../overview.md
[03]: ./MouseJump.md
[04]: https://learn.microsoft.com/windows/powertoys/crop-and-lock
