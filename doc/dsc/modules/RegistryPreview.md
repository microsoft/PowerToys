---
description: DSC configuration reference for PowerToys RegistryPreview module
ms.date:     10/18/2025
ms.topic:    reference
title:       RegistryPreview Module
---

# RegistryPreview Module

## Synopsis

Manages configuration for the Registry Preview utility, which visualizes and edits Windows registry files (.reg).

## Description

The `RegistryPreview` module configures PowerToys Registry Preview, a utility
that provides a visual preview and editing interface for Windows registry
(.reg) files. It helps you understand and safely edit registry files before
applying them to your system.

## Properties

The RegistryPreview module supports the following configurable properties:

### DefaultRegApp

Controls whether Registry Preview is set as the default application for .reg files.

**Type:** boolean  
**Default:** `false`

## Examples

### Example 1 - Set as default .reg handler with direct execution

This example sets Registry Preview as the default application for .reg files.

```powershell
$config = @{
    settings = @{
        properties = @{
            DefaultRegApp = $true
        }
        name = "RegistryPreview"
        version = "1.0"
    }
} | ConvertTo-Json -Depth 10 -Compress

PowerToys.DSC.exe set --resource 'settings' --module RegistryPreview --input $config
```

### Example 2 - Configure with DSC

This example configures Registry Preview as the default handler.

```bash
dsc config set --file registrypreview-default.dsc.yaml
```

```yaml
# registrypreview-default.dsc.yaml
$schema: https://aka.ms/dsc/schemas/v3/bundled/config/document.json
resources:
  - name: Set Registry Preview as default
    type: Microsoft.PowerToys/RegistryPreviewSettings
    properties:
      settings:
        properties:
          DefaultRegApp: true
        name: RegistryPreview
        version: 1.0
```

### Example 3 - Install and configure with WinGet

This example installs PowerToys and sets Registry Preview as the default .reg
handler.

```bash
winget configure winget-registrypreview.yaml
```

```yaml
# winget-registrypreview.yaml
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
  
  - name: Configure Registry Preview
    type: Microsoft.PowerToys/RegistryPreviewSettings
    properties:
      settings:
        properties:
          DefaultRegApp: true
        name: RegistryPreview
        version: 1.0
```

### Example 4 - Disable as default handler

This example ensures Registry Preview is not the default .reg handler.

```bash
dsc config set --file registrypreview-notdefault.dsc.yaml
```

```yaml
# registrypreview-notdefault.dsc.yaml
$schema: https://aka.ms/dsc/schemas/v3/bundled/config/document.json
resources:
  - name: Do not use as default
    type: Microsoft.PowerToys/RegistryPreviewSettings
    properties:
      settings:
        properties:
          DefaultRegApp: false
        name: RegistryPreview
        version: 1.0
```

## Use cases

### System administration

Configure as default for safe registry file handling:

```yaml
resources:
  - name: Admin configuration
    type: Microsoft.PowerToys/RegistryPreviewSettings
    properties:
      settings:
        properties:
          DefaultRegApp: true
        name: RegistryPreview
        version: 1.0
```

### Optional tool

Keep as optional tool without default file association:

```yaml
resources:
  - name: Optional tool
    type: Microsoft.PowerToys/RegistryPreviewSettings
    properties:
      settings:
        properties:
          DefaultRegApp: false
        name: RegistryPreview
        version: 1.0
```

## See also

- [Settings Resource][01]
- [PowerToys DSC Overview][02]
- [FileLocksmith][03]
- [PowerToys Registry Preview Documentation][04]

<!-- Link reference definitions -->
[01]: ../settings-resource.md
[02]: ../overview.md
[03]: ./FileLocksmith.md
[04]: https://learn.microsoft.com/windows/powertoys/registry-preview
