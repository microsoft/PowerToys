---
description: DSC configuration reference for PowerToys FileLocksmith module
ms.date:     10/18/2025
ms.topic:    reference
title:       FileLocksmith Module
---

# FileLocksmith Module

## Synopsis

Manages configuration for the File Locksmith utility, which identifies
processes that are locking files or folders.

## Description

The `FileLocksmith` module configures PowerToys File Locksmith, a Windows
shell extension that helps identify which processes are using (locking)
specific files or folders. It integrates with the Windows Explorer context
menu for easy access.

## Properties

The FileLocksmith module supports the following configurable properties:

### ExtendedContextMenuOnly

Controls whether File Locksmith appears only in the extended context menu.

**Type:** boolean  
**Default:** `false`  
**Description:** When `true`, File Locksmith only appears in the context menu
when you hold Shift while right-clicking. When `false`, it appears in the
standard context menu.

## Examples

### Example 1 - Show in standard context menu with direct execution

This example configures File Locksmith to appear in the standard context menu.

```powershell
$config = @{
    settings = @{
        properties = @{
            ExtendedContextMenuOnly = $false
        }
        name = "FileLocksmith"
        version = "1.0"
    }
} | ConvertTo-Json -Depth 10 -Compress

PowerToys.DSC.exe set --resource 'settings' --module FileLocksmith `
    --input $config
```

### Example 2 - Extended menu only with DSC

This example configures File Locksmith to appear only in the extended
context menu.

```bash
dsc config set --file filelocksmith-extended.dsc.yaml
```

```yaml
# filelocksmith-extended.dsc.yaml
$schema: https://aka.ms/dsc/schemas/v3/bundled/config/document.json
resources:
  - name: Configure File Locksmith for extended menu
    type: Microsoft.PowerToys/FileLocksmithSettings
    properties:
      settings:
        properties:
          ExtendedContextMenuOnly: true
        name: FileLocksmith
        version: 1.0
```

### Example 3 - Install and configure with WinGet

This example installs PowerToys and configures File Locksmith for standard
menu access.

```bash
winget configure winget-filelocksmith.yaml
```

```yaml
# winget-filelocksmith.yaml
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
  
  - name: Configure File Locksmith
    type: Microsoft.PowerToys/FileLocksmithSettings
    properties:
      settings:
        properties:
          ExtendedContextMenuOnly: false
        name: FileLocksmith
        version: 1.0
```

### Example 4 - Minimize context menu clutter

This example configures for extended menu to reduce clutter.

```bash
dsc config set --file filelocksmith-minimal.dsc.yaml
```

```yaml
# filelocksmith-minimal.dsc.yaml
$schema: https://aka.ms/dsc/schemas/v3/bundled/config/document.json
resources:
  - name: Minimal context menu
    type: Microsoft.PowerToys/FileLocksmithSettings
    properties:
      settings:
        properties:
          ExtendedContextMenuOnly: true
        name: FileLocksmith
        version: 1.0
```

## Use cases

### System administration

Quick access for troubleshooting file locks:

```yaml
resources:
  - name: Admin quick access
    type: Microsoft.PowerToys/FileLocksmithSettings
    properties:
      settings:
        properties:
          ExtendedContextMenuOnly: false
        name: FileLocksmith
        version: 1.0
```

### Clean context menu

Reduce menu clutter for casual users:

```yaml
resources:
  - name: Clean menu
    type: Microsoft.PowerToys/FileLocksmithSettings
    properties:
      settings:
        properties:
          ExtendedContextMenuOnly: true
        name: FileLocksmith
        version: 1.0
```

## See also

- [Settings Resource][01]
- [PowerToys DSC Overview][02]
- [RegistryPreview][03]
- [PowerToys File Locksmith Documentation][04]

<!-- Link reference definitions -->
[01]: ../settings-resource.md
[02]: ../overview.md
[03]: ./RegistryPreview.md
[04]: https://learn.microsoft.com/windows/powertoys/file-locksmith
