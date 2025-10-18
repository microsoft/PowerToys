---
description: DSC configuration reference for PowerToys KeyboardManager module
ms.date:     10/18/2025
ms.topic:    reference
title:       KeyboardManager Module
---

# KeyboardManager Module

## Synopsis

Manages configuration for the Keyboard Manager utility, which allows key remapping and custom keyboard shortcuts.

## Description

The `KeyboardManager` module configures PowerToys Keyboard Manager, a utility that enables you to remap keys and create custom keyboard shortcuts. It allows reassigning keys, creating application-specific remappings, and defining shortcuts that run programs or commands.

## Properties

The KeyboardManager module supports the following configurable properties:

### Enabled

Controls whether Keyboard Manager is enabled.

**Type:** boolean  
**Default:** `true`

## Examples

### Example 1 - Enable Keyboard Manager with direct execution

This example enables the Keyboard Manager utility.

```powershell
$config = @{
    settings = @{
        properties = @{
            Enabled = $true
        }
        name = "KeyboardManager"
        version = "1.0"
    }
} | ConvertTo-Json -Depth 10 -Compress

PowerToys.DSC.exe set --resource 'settings' --module KeyboardManager --input $config
```

### Example 2 - Configure with DSC

This example enables Keyboard Manager through DSC configuration.

```yaml
# keyboardmanager-config.dsc.yaml
$schema: https://aka.ms/dsc/schemas/v3/bundled/config/document.json
resources:
  - name: Enable Keyboard Manager
    type: Microsoft.PowerToys/KeyboardManagerSettings
    properties:
      settings:
        properties:
          Enabled: true
        name: KeyboardManager
        version: 1.0
```

### Example 3 - Install and configure with WinGet

This example installs PowerToys and enables Keyboard Manager.

```yaml
# winget-keyboardmanager.yaml
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
  
  - name: Enable Keyboard Manager
    type: Microsoft.PowerToys/KeyboardManagerSettings
    properties:
      settings:
        properties:
          Enabled: true
        name: KeyboardManager
        version: 1.0
```

## Important notes

> **Note:** The Keyboard Manager module DSC configuration controls the enabled state only. Key remappings and shortcut definitions are managed through the Keyboard Manager UI and stored separately. This design ensures that complex remapping configurations are not accidentally overwritten by DSC operations.

To configure key remappings:
1. Enable Keyboard Manager using DSC
2. Open PowerToys Settings
3. Navigate to Keyboard Manager
4. Use "Remap a key" or "Remap a shortcut" to configure specific mappings

## Use cases

### Enable for deployment

Enable Keyboard Manager on new workstations:

```yaml
resources:
  - name: Enable Keyboard Manager
    type: Microsoft.PowerToys/KeyboardManagerSettings
    properties:
      settings:
        properties:
          Enabled: true
        name: KeyboardManager
        version: 1.0
```

## See also

- [Settings Resource][01]
- [PowerToys DSC Overview][02]
- [App Module][03] - For enabling/disabling Keyboard Manager utility
- [PowerToys Keyboard Manager Documentation][04]

<!-- Link reference definitions -->
[01]: ../settings-resource.md
[02]: ../overview.md
[03]: ./App.md
[04]: https://learn.microsoft.com/windows/powertoys/keyboard-manager
- [PowerToys Keyboard Manager Documentation](https://learn.microsoft.com/windows/powertoys/keyboard-manager)
