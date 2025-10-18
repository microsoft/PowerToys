---
description: DSC configuration reference for PowerToys MouseJump module
ms.date:     10/18/2025
ms.topic:    reference
title:       MouseJump Module
---

# MouseJump Module

## Synopsis

Manages configuration for the Mouse Jump utility, which enables quick navigation across large or multiple displays.

## Description

The `MouseJump` module configures PowerToys Mouse Jump, a utility that provides a miniature preview of all your displays, allowing you to quickly jump your mouse cursor to any location. This is particularly useful with large monitors or multi-monitor setups.

## Properties

The MouseJump module supports the following configurable properties:

### ActivationShortcut

Sets the keyboard shortcut to activate Mouse Jump.

**Type:** object  
**Properties:**
- `win` (boolean) - Windows key modifier
- `ctrl` (boolean) - Ctrl key modifier
- `alt` (boolean) - Alt key modifier
- `shift` (boolean) - Shift key modifier
- `code` (integer) - Virtual key code
- `key` (string) - Key name

**Default:** `Win + Shift + D`

### ThumbnailSize

Sets the size of the screen thumbnail preview.

**Type:** string  
**Allowed values:**
- `"small"` - Smaller thumbnail for faster performance
- `"medium"` - Balanced size and performance
- `"large"` - Larger thumbnail for better visibility

**Default:** `"medium"`

## Examples

### Example 1 - Configure activation shortcut with direct execution

This example customizes the Mouse Jump activation shortcut.

```powershell
$config = @{
    settings = @{
        properties = @{
            ActivationShortcut = @{
                win = $true
                ctrl = $false
                alt = $false
                shift = $true
                code = 68
                key = "D"
            }
        }
        name = "MouseJump"
        version = "1.0"
    }
} | ConvertTo-Json -Depth 10 -Compress

PowerToys.DSC.exe set --resource 'settings' --module MouseJump --input $config
```

### Example 2 - Configure thumbnail size with DSC

This example sets a larger thumbnail for better visibility.

```yaml
# mousejump-size.dsc.yaml
$schema: https://aka.ms/dsc/schemas/v3/bundled/config/document.json
resources:
  - name: Configure Mouse Jump thumbnail
    type: Microsoft.PowerToys/MouseJumpSettings
    properties:
      settings:
        properties:
          ThumbnailSize: large
        name: MouseJump
        version: 1.0
```

### Example 3 - Install and configure with WinGet

This example installs PowerToys and configures Mouse Jump for multi-monitor setups.

```yaml
# winget-mousejump.yaml
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
  
  - name: Configure Mouse Jump
    type: Microsoft.PowerToys/MouseJumpSettings
    properties:
      settings:
        properties:
          ThumbnailSize: medium
        name: MouseJump
        version: 1.0
```

### Example 4 - Performance-optimized configuration

This example uses a smaller thumbnail for better performance.

```yaml
# mousejump-performance.dsc.yaml
$schema: https://aka.ms/dsc/schemas/v3/bundled/config/document.json
resources:
  - name: Performance-optimized Mouse Jump
    type: Microsoft.PowerToys/MouseJumpSettings
    properties:
      settings:
        properties:
          ThumbnailSize: small
        name: MouseJump
        version: 1.0
```

### Example 5 - Large display configuration

This example configures for large or high-DPI displays.

```powershell
$config = @{
    settings = @{
        properties = @{
            ThumbnailSize = "large"
        }
        name = "MouseJump"
        version = "1.0"
    }
} | ConvertTo-Json -Depth 10 -Compress

PowerToys.DSC.exe set --resource 'settings' --module MouseJump --input $config
```

## Use cases

### Multi-monitor workstations

Configure for efficient navigation across multiple displays:

```yaml
resources:
  - name: Multi-monitor configuration
    type: Microsoft.PowerToys/MouseJumpSettings
    properties:
      settings:
        properties:
          ThumbnailSize: medium
        name: MouseJump
        version: 1.0
```

### Large displays

Configure for ultra-wide or 4K+ displays:

```yaml
resources:
  - name: Large display configuration
    type: Microsoft.PowerToys/MouseJumpSettings
    properties:
      settings:
        properties:
          ThumbnailSize: large
        name: MouseJump
        version: 1.0
```

## See also

- [Settings Resource][01]
- [PowerToys DSC Overview][02]
- [App Module][03] - For enabling/disabling Mouse Jump utility
- [PowerToys Mouse Jump Documentation][04]

<!-- Link reference definitions -->
[01]: ../settings-resource.md
[02]: ../overview.md
[03]: ./App.md
[04]: https://learn.microsoft.com/windows/powertoys/mouse-jump
- [PowerToys Mouse Utilities Documentation](https://learn.microsoft.com/windows/powertoys/mouse-utilities)
