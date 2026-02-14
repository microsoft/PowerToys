---
description: Overview of PowerToys Desired State Configuration (DSC) support
ms.date:     10/18/2025
ms.topic:    overview
title:       PowerToys DSC Overview
---

# PowerToys DSC Overview

## Synopsis

PowerToys supports Desired State Configuration (DSC) v3 for declarative configuration management of PowerToys settings.

## Description

PowerToys includes Microsoft Desired State Configuration (DSC) support
through the `PowerToys.DSC.exe` command-line tool, enabling you to:

- Declare and enforce desired configuration states for PowerToys
  utilities.
- Automate PowerToys configuration across multiple systems.
- Integrate PowerToys configuration with WinGet and other DSC-compatible
  tools.
- Version control your PowerToys settings as code.

The PowerToys DSC implementation provides a **settings** resource that
manages configuration for all PowerToys utilities (modules). Each utility
can be configured independently, allowing granular control over your
PowerToys environment.

## Usage methods

PowerToys DSC can be used in three ways:

### 1. Direct execution with PowerToys.DSC.exe

Execute DSC operations directly using the PowerToys.DSC.exe command-line
tool:

```powershell
# Get current settings for a module
PowerToys.DSC.exe get --resource 'settings' --module Awake

# Set settings for a module
$input = '{"settings":{...}}'
PowerToys.DSC.exe set --resource 'settings' --module Awake --input $input

# Test if settings match desired state
PowerToys.DSC.exe test --resource 'settings' --module Awake --input $input
```

For detailed information, see [PowerToys.DSC.exe command reference][01].

### 2. Microsoft Desired State Configuration (DSC)

Use PowerToys DSC resources in standard DSC configuration documents:

```yaml
# powertoys-config.dsc.yaml
$schema: https://aka.ms/dsc/schemas/v3/bundled/config/document.json
resources:
  - name: Configure Awake
    type: Microsoft.PowerToys/AwakeSettings
    properties:
      settings:
        properties:
          keepDisplayOn: true
          mode: 1
        name: Awake
        version: 0.0.1
```

### 3. WinGet Configuration

Integrate PowerToys configuration with WinGet package installation:

```yaml
# winget-powertoys.yaml
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
  
  - name: Configure FancyZones
    type: Microsoft.PowerToys/FancyZonesSettings
    properties:
      settings:
        properties:
          fancyzones_shiftDrag: true
          fancyzones_mouseSwitch: true
        name: FancyZones
        version: 1.0
```

## Available resources

PowerToys DSC provides the following resource:

| Resource   | Description                                          |
| ---------- | ---------------------------------------------------- |
| `settings` | Manages configuration for PowerToys utility modules. |

For detailed information about the settings resource, see [Settings
Resource Reference][03].

## Available modules

The settings resource supports configuration for the following PowerToys
utilities:

| Module                 | Description                                  | Documentation                         |
| ---------------------- | -------------------------------------------- | ------------------------------------- |
| App                    | General PowerToys application settings.      | [App module][04]                      |
| AdvancedPaste          | Advanced clipboard operations.               | [AdvancedPaste module][05]            |
| AlwaysOnTop            | Pin windows to stay on top.                  | [AlwaysOnTop module][06]              |
| Awake                  | Keep computer awake.                         | [Awake module][07]                    |
| ColorPicker            | System-wide color picker utility.            | [ColorPicker module][08]              |
| CropAndLock            | Crop and lock portions of windows.           | [CropAndLock module][09]              |
| EnvironmentVariables   | Manage environment variables.                | [EnvironmentVariables module][10]     |
| FancyZones             | Window layout manager.                       | [FancyZones module][11]               |
| FileLocksmith          | Identify what's locking files.               | [FileLocksmith module][12]            |
| FindMyMouse            | Locate your mouse cursor.                    | [FindMyMouse module][13]              |
| Hosts                  | Quick hosts file editor.                     | [Hosts module][14]                    |
| ImageResizer           | Resize images from context menu.             | [ImageResizer module][15]             |
| KeyboardManager        | Remap keys and create shortcuts.             | [KeyboardManager module][16]          |
| MeasureTool            | Measure pixels on screen.                    | [MeasureTool module][17]              |
| MouseHighlighter       | Highlight mouse cursor.                      | [MouseHighlighter module][18]         |
| MouseJump              | Jump across large or multiple displays.      | [MouseJump module][19]                |
| MousePointerCrosshairs | Display crosshairs centered on mouse.        | [MousePointerCrosshairs module][20]   |
| Peek                   | Quick file previewer.                        | [Peek module][21]                     |
| PowerAccent            | Quick accent character selector.             | [PowerAccent module][22]              |
| PowerOCR               | Extract text from images.                    | [PowerOCR module][23]                 |
| PowerRename            | Bulk rename files.                           | [PowerRename module][24]              |
| RegistryPreview        | Visualize and edit registry files.           | [RegistryPreview module][25]          |
| ShortcutGuide          | Display keyboard shortcuts.                  | [ShortcutGuide module][26]            |
| Workspaces             | Save and restore application sets.           | [Workspaces module][27]               |
| ZoomIt                 | Screen zoom and annotation tool.             | [ZoomIt module][28]                   |

## Common operations

### List all supported modules

```powershell
PowerToys.DSC.exe modules --resource 'settings'
```

### Get current configuration

```powershell
# Get configuration for a specific module.
PowerToys.DSC.exe get --resource 'settings' --module FancyZones

# Export configuration (identical to get).
PowerToys.DSC.exe export --resource 'settings' --module FancyZones
```

### Apply configuration

```powershell
# Set configuration for a module.
$input = '{"settings":{...}}'
PowerToys.DSC.exe set --resource 'settings' --module FancyZones --input $input
```

### Validate configuration

```powershell
# Test if current state matches desired state.
$input = '{"settings":{...}}'
PowerToys.DSC.exe test --resource 'settings' --module FancyZones --input $input
```

### Generate schema

```powershell
# Get JSON schema for a module's settings.
PowerToys.DSC.exe schema --resource 'settings' --module FancyZones
```

### Generate DSC manifest

```powershell
# Generate manifest for a specific module.
$outputDir = "C:\manifests"
PowerToys.DSC.exe manifest --resource 'settings' --module FancyZones `
  --outputDir $outputDir

# Generate manifests for all modules.
PowerToys.DSC.exe manifest --resource 'settings' --outputDir $outputDir
```

## Examples

For complete examples, see:

- [Settings Resource Examples][29]
- Individual module documentation in the [modules][30] folder

## See also

- [Settings Resource Reference][03]
- [PowerToys.DSC.exe Command Reference][01]
- [Module Documentation][30]
- [Microsoft DSC Documentation][31]
- [WinGet Configuration Documentation][32]

<!-- Link reference definitions -->
[01]: ./modules/
[03]: ./settings-resource.md
[04]: ./modules/App.md
[05]: ./modules/AdvancedPaste.md
[06]: ./modules/AlwaysOnTop.md
[07]: ./modules/Awake.md
[08]: ./modules/ColorPicker.md
[09]: ./modules/CropAndLock.md
[10]: ./modules/EnvironmentVariables.md
[11]: ./modules/FancyZones.md
[12]: ./modules/FileLocksmith.md
[13]: ./modules/FindMyMouse.md
[14]: ./modules/Hosts.md
[15]: ./modules/ImageResizer.md
[16]: ./modules/KeyboardManager.md
[17]: ./modules/MeasureTool.md
[18]: ./modules/MouseHighlighter.md
[19]: ./modules/MouseJump.md
[20]: ./modules/MousePointerCrosshairs.md
[21]: ./modules/Peek.md
[22]: ./modules/PowerAccent.md
[23]: ./modules/PowerOCR.md
[24]: ./modules/PowerRename.md
[25]: ./modules/RegistryPreview.md
[26]: ./modules/ShortcutGuide.md
[27]: ./modules/Workspaces.md
[28]: ./modules/ZoomIt.md
[29]: ./settings-resource.md#examples
[30]: ./modules/
[31]: https://learn.microsoft.com/powershell/dsc/overview
[32]: https://learn.microsoft.com/windows/package-manager/configuration/
