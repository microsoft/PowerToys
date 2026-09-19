---
description: DSC configuration reference for PowerToys Peek module
ms.date:     10/18/2025
ms.topic:    reference
title:       Peek Module
---

# Peek Module

## Synopsis

Manages configuration for the Peek utility, a quick file preview tool.

## Description

The `Peek` module configures PowerToys Peek, a utility that provides quick
file previews without opening files. Activate it with a keyboard shortcut to
preview documents, images, videos, and more in a popup window.

## Properties

The Peek module supports the following configurable properties:

### ActivationShortcut

Sets the keyboard shortcut to activate Peek for the selected file.

**Type:** object  
**Properties:**

- `win` (boolean) - Windows key modifier
- `ctrl` (boolean) - Ctrl key modifier
- `alt` (boolean) - Alt key modifier
- `shift` (boolean) - Shift key modifier
- `code` (integer) - Virtual key code
- `key` (string) - Key name

**Default:** `Ctrl+Space`

### CloseAfterLosingFocus

Controls whether Peek window closes when it loses focus.

**Type:** boolean  
**Default:** `true`

## Examples

### Example 1 - Configure activation shortcut with direct execution

This example customizes the Peek activation shortcut.

```powershell
$config = @{
    settings = @{
        properties = @{
            ActivationShortcut = @{
                win = $false
                ctrl = $true
                alt = $false
                shift = $false
                code = 32
                key = "Space"
            }
        }
        name = "Peek"
        version = "1.0"
    }
} | ConvertTo-Json -Depth 10 -Compress

PowerToys.DSC.exe set --resource 'settings' --module Peek --input $config
```

### Example 2 - Configure focus behavior with DSC

This example configures Peek to remain open after losing focus.

Save the following configuration as `peek-focus.dsc.config.yaml`:

```yaml
# yaml-language-server: $schema=https://aka.ms/dsc/schemas/v3/bundled/config/document.vscode.json
$schema: https://aka.ms/dsc/schemas/v3/bundled/config/document.json
resources:
  - name: Configure Peek focus behavior
    type: Microsoft.PowerToys/PeekSettings
    properties:
      settings:
        properties:
          CloseAfterLosingFocus: false
        name: Peek
        version: 1.0
```

Apply the configuration with Microsoft DSC:

```bash
dsc config set --file peek-focus.dsc.config.yaml
```

### Example 3 - Install and configure with WinGet

This example installs PowerToys and configures Peek.

Save the following configuration as `peek.dsc.config.winget`:

```yaml
# yaml-language-server: $schema=https://aka.ms/configuration-dsc-schema/0.2
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
  
  - name: Configure Peek
    type: Microsoft.PowerToys/PeekSettings
    properties:
      settings:
        properties:
          CloseAfterLosingFocus: true
        name: Peek
        version: 1.0
```

Apply the configuration with WinGet:

```bash
winget configure peek.dsc.config.winget
```

### Example 4 - Alternative activation shortcut

This example uses Ctrl+Shift+Space as the activation shortcut.

Save the following configuration as `peek-altkey.dsc.config.yaml`:

```yaml
# yaml-language-server: $schema=https://aka.ms/dsc/schemas/v3/bundled/config/document.vscode.json
$schema: https://aka.ms/dsc/schemas/v3/bundled/config/document.json
resources:
  - name: Alternative Peek shortcut
    type: Microsoft.PowerToys/PeekSettings
    properties:
      settings:
        properties:
          ActivationShortcut:
            win: false
            ctrl: true
            alt: false
            shift: true
            code: 32
            key: Space
        name: Peek
        version: 1.0
```

Apply the configuration with Microsoft DSC:

```bash
dsc config set --file peek-altkey.dsc.config.yaml
```

## Use cases

### File browsing

Configure for quick file preview during browsing:

```yaml
resources:
  - name: File browsing configuration
    type: Microsoft.PowerToys/PeekSettings
    properties:
      settings:
        properties:
          CloseAfterLosingFocus: true
        name: Peek
        version: 1.0
```

### Content review

Configure for extended content review:

```yaml
resources:
  - name: Review configuration
    type: Microsoft.PowerToys/PeekSettings
    properties:
      settings:
        properties:
          CloseAfterLosingFocus: false
        name: Peek
        version: 1.0
```

## See also

- [Settings Resource][01]
- [PowerToys DSC Overview][02]
- [ShortcutGuide][03]
- [PowerToys Peek Documentation][04]

<!-- Link reference definitions -->
[01]: ../settings-resource.md
[02]: ../overview.md
[03]: ./ShortcutGuide.md
[04]: https://learn.microsoft.com/windows/powertoys/peek
