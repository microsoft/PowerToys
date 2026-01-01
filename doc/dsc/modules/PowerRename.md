---
description: DSC configuration reference for PowerToys PowerRename module
ms.date:     10/18/2025
ms.topic:    reference
title:       PowerRename Module
---

# PowerRename Module

## Synopsis

Manages configuration for the Power Rename utility, a bulk file and folder renaming tool.

## Description

The `PowerRename` module configures PowerToys Power Rename, a Windows shell
extension that enables bulk renaming of files and folders with advanced
features like regular expressions, preview, and undo functionality. It
integrates with the Windows Explorer context menu.

## Properties

The PowerRename module supports the following configurable properties:

### MRUEnabled

Controls whether the most recently used (MRU) search and replace terms are saved.

**Type:** boolean  
**Default:** `true`

### MaxMRUSize

Sets the maximum number of MRU entries to remember.

**Type:** integer  
**Range:** `0` to `20`  
**Default:** `10`

### ShowIcon

Controls whether the Power Rename icon appears in the Explorer context menu.

**Type:** boolean  
**Default:** `true`

### ExtendedContextMenuOnly

Controls whether Power Rename appears only in the extended context menu (Shift+right-click).

**Type:** boolean  
**Default:** `false`

### UseBoostLib

Controls whether the Boost library is used for regular expression processing.

**Type:** boolean  
**Default:** `false`

## Examples

### Example 1 - Configure MRU settings with direct execution

This example configures the most recently used list behavior.

```powershell
$config = @{
    settings = @{
        properties = @{
            MRUEnabled = $true
            MaxMRUSize = 15
        }
        name = "PowerRename"
        version = "1.0"
    }
} | ConvertTo-Json -Depth 10 -Compress

PowerToys.DSC.exe set --resource 'settings' --module PowerRename --input $config
```

### Example 2 - Configure context menu with DSC

This example configures Power Rename to appear in the extended context menu
only.

```bash
dsc config set --file powerrename-context.dsc.yaml
```

```yaml
# powerrename-context.dsc.yaml
$schema: https://aka.ms/dsc/schemas/v3/bundled/config/document.json
resources:
  - name: Configure Power Rename context menu
    type: Microsoft.PowerToys/PowerRenameSettings
    properties:
      settings:
        properties:
          ExtendedContextMenuOnly: true
          ShowIcon: true
        name: PowerRename
        version: 1.0
```

### Example 3 - Install and configure with WinGet

This example installs PowerToys and configures Power Rename.

```bash
winget configure winget-powerrename.yaml
```

```yaml
# winget-powerrename.yaml
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
  
  - name: Configure Power Rename
    type: Microsoft.PowerToys/PowerRenameSettings
    properties:
      settings:
        properties:
          MRUEnabled: true
          MaxMRUSize: 20
          ShowIcon: true
          UseBoostLib: true
        name: PowerRename
        version: 1.0
```

### Example 4 - Clean context menu configuration

This example minimizes context menu clutter.

```bash
dsc config set --file powerrename-minimal.dsc.yaml
```

```yaml
# powerrename-minimal.dsc.yaml
$schema: https://aka.ms/dsc/schemas/v3/bundled/config/document.json
resources:
  - name: Minimal context menu
    type: Microsoft.PowerToys/PowerRenameSettings
    properties:
      settings:
        properties:
          ExtendedContextMenuOnly: true
          ShowIcon: false
        name: PowerRename
        version: 1.0
```

### Example 5 - Advanced regex configuration

This example enables the Boost library for advanced regex features.

```powershell
$config = @{
    settings = @{
        properties = @{
            UseBoostLib = $true
            MRUEnabled = $true
            MaxMRUSize = 15
        }
        name = "PowerRename"
        version = "1.0"
    }
} | ConvertTo-Json -Depth 10 -Compress

PowerToys.DSC.exe set --resource 'settings' --module PowerRename --input $config
```

## Use cases

### Content management

Configure for frequent file renaming tasks:

```yaml
resources:
  - name: Content management
    type: Microsoft.PowerToys/PowerRenameSettings
    properties:
      settings:
        properties:
          MRUEnabled: true
          MaxMRUSize: 20
          ShowIcon: true
        name: PowerRename
        version: 1.0
```

### Clean interface

Configure for minimal context menu presence:

```yaml
resources:
  - name: Clean interface
    type: Microsoft.PowerToys/PowerRenameSettings
    properties:
      settings:
        properties:
          ExtendedContextMenuOnly: true
        name: PowerRename
        version: 1.0
```

## See also

- [Settings Resource][01]
- [PowerToys DSC Overview][02]
- [AdvancedPaste][03]
- [PowerToys PowerRename Documentation][04]

<!-- Link reference definitions -->
[01]: ../settings-resource.md
[02]: ../overview.md
[03]: ./AdvancedPaste.md
[04]: https://learn.microsoft.com/windows/powertoys/powerrename
