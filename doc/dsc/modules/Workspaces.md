---
description: DSC configuration reference for PowerToys Workspaces module
ms.date:     10/18/2025
ms.topic:    reference
title:       Workspaces Module
---

# Workspaces Module

## Synopsis

Manages configuration for the Workspaces utility, which launches application sets and arranges windows.

## Description

The `Workspaces` module configures PowerToys Workspaces, a utility that allows
you to save and restore sets of applications with their window positions. It
enables you to quickly switch between different work contexts by launching and
arranging multiple applications at once.

## Properties

The Workspaces module supports the following configurable properties:

### LaunchHotkey

Sets the keyboard shortcut to launch the Workspaces editor.

**Type:** object  
**Properties:**

- `win` (boolean) - Windows key modifier
- `ctrl` (boolean) - Ctrl key modifier
- `alt` (boolean) - Alt key modifier
- `shift` (boolean) - Shift key modifier
- `code` (integer) - Virtual key code
- `key` (string) - Key name

**Default:** `Win+Shift+;` (VK code 186)

### MoveExistingWindows

Controls whether existing application windows are moved when launching a workspace.

**Type:** boolean  
**Default:** `false`

### SpanZonesAcrossMonitors

Controls whether workspace zones can span across multiple monitors.

**Type:** boolean  
**Default:** `false`

## Examples

### Example 1 - Configure launch hotkey with direct execution

This example sets a custom hotkey to launch the Workspaces editor.

```powershell
$config = @{
    settings = @{
        properties = @{
            LaunchHotkey = @{
                win = $true
                ctrl = $true
                alt = $false
                shift = $false
                code = 87
                key = "W"
            }
        }
        name = "Workspaces"
        version = "1.0"
    }
} | ConvertTo-Json -Depth 10 -Compress

PowerToys.DSC.exe set --resource 'settings' --module Workspaces --input $config
```

### Example 2 - Configure window behavior with DSC

This example enables moving existing windows when launching workspaces.

```bash
dsc config set --file workspaces-behavior.dsc.yaml
```

```yaml
# workspaces-behavior.dsc.yaml
$schema: https://aka.ms/dsc/schemas/v3/bundled/config/document.json
resources:
  - name: Configure Workspaces window behavior
    type: Microsoft.PowerToys/WorkspacesSettings
    properties:
      settings:
        properties:
          MoveExistingWindows: true
          SpanZonesAcrossMonitors: false
        name: Workspaces
        version: 1.0
```

### Example 3 - Install and configure with WinGet

This example installs PowerToys and configures Workspaces.

```bash
winget configure winget-workspaces.yaml
```

```yaml
# winget-workspaces.yaml
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
  
  - name: Configure Workspaces
    type: Microsoft.PowerToys/WorkspacesSettings
    properties:
      settings:
        properties:
          LaunchHotkey:
            win: true
            ctrl: false
            alt: false
            shift: true
            code: 186
            key: ";"
          MoveExistingWindows: true
        name: Workspaces
        version: 1.0
```

### Example 4 - Multi-monitor setup

This example configures for multi-monitor workspace management.

```bash
dsc config set --file workspaces-multimonitor.dsc.yaml
```

```yaml
# workspaces-multimonitor.dsc.yaml
$schema: https://aka.ms/dsc/schemas/v3/bundled/config/document.json
resources:
  - name: Multi-monitor configuration
    type: Microsoft.PowerToys/WorkspacesSettings
    properties:
      settings:
        properties:
          SpanZonesAcrossMonitors: true
          MoveExistingWindows: true
        name: Workspaces
        version: 1.0
```

### Example 5 - Simple hotkey

This example sets a simple single-key hotkey combination.

```powershell
$config = @{
    settings = @{
        properties = @{
            LaunchHotkey = @{
                win = $true
                ctrl = $false
                alt = $true
                shift = $false
                code = 192
                key = "~"
            }
        }
        name = "Workspaces"
        version = "1.0"
    }
} | ConvertTo-Json -Depth 10 -Compress

PowerToys.DSC.exe set --resource 'settings' --module Workspaces --input $config
```

## Use cases

### Development environments

Configure for quick switching between development workspaces:

```yaml
resources:
  - name: Development workspace
    type: Microsoft.PowerToys/WorkspacesSettings
    properties:
      settings:
        properties:
          MoveExistingWindows: true
          SpanZonesAcrossMonitors: true
        name: Workspaces
        version: 1.0
```

### Single monitor usage

Configure for single-monitor workflow:

```yaml
resources:
  - name: Single monitor setup
    type: Microsoft.PowerToys/WorkspacesSettings
    properties:
      settings:
        properties:
          SpanZonesAcrossMonitors: false
          MoveExistingWindows: false
        name: Workspaces
        version: 1.0
```

## See also

- [Settings Resource][01]
- [PowerToys DSC Overview][02]
- [ColorPicker Module][03] - For additional PowerToys configuration
- [PowerToys Workspaces Documentation][04]

<!-- Link reference definitions -->
[01]: ../settings-resource.md
[02]: ../overview.md
[03]: ./ColorPicker.md
[04]: https://learn.microsoft.com/windows/powertoys/workspaces
