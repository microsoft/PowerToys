---
description: DSC configuration reference for PowerToys App module (general settings)
ms.date:     10/18/2025
ms.topic:    reference
title:       App module
---

# App Module

## Synopsis

Manages general PowerToys application settings, including utility enable/disable states, startup behavior, and theme preferences.

## Description

The `App` module controls global PowerToys settings that affect the entire
application. This includes which utilities are enabled, whether PowerToys
runs at startup, the application theme, and other general preferences.

Unlike other modules that configure specific utilities, the App module
manages PowerToys-wide settings and the enabled state of all utilities.

## Properties

The App module supports the following configurable properties:

### Enabled

Controls which PowerToys utilities are enabled or disabled.

**Type:** Object  
**Properties:**

- `AdvancedPaste` (boolean) - Enable/disable Advanced Paste utility.
- `AlwaysOnTop` (boolean) - Enable/disable Always On Top utility.
- `Awake` (boolean) - Enable/disable Awake utility.
- `ColorPicker` (boolean) - Enable/disable Color Picker utility.
- `CropAndLock` (boolean) - Enable/disable Crop And Lock utility.
- `EnvironmentVariables` (boolean) - Enable/disable Environment Variables
  utility.
- `FancyZones` (boolean) - Enable/disable FancyZones utility.
- `FileLocksmith` (boolean) - Enable/disable File Locksmith utility.
- `FindMyMouse` (boolean) - Enable/disable Find My Mouse utility.
- `Hosts` (boolean) - Enable/disable Hosts File Editor utility.
- `ImageResizer` (boolean) - Enable/disable Image Resizer utility.
- `KeyboardManager` (boolean) - Enable/disable Keyboard Manager utility.
- `MeasureTool` (boolean) - Enable/disable Measure Tool utility.
- `MouseHighlighter` (boolean) - Enable/disable Mouse Highlighter utility.
- `MouseJump` (boolean) - Enable/disable Mouse Jump utility.
- `MousePointerCrosshairs` (boolean) - Enable/disable Mouse Pointer
  Crosshairs utility.
- `Peek` (boolean) - Enable/disable Peek utility.
- `PowerAccent` (boolean) - Enable/disable Power Accent utility.
- `PowerOCR` (boolean) - Enable/disable Power OCR utility.
- `PowerRename` (boolean) - Enable/disable Power Rename utility.
- `RegistryPreview` (boolean) - Enable/disable Registry Preview utility.
- `ShortcutGuide` (boolean) - Enable/disable Shortcut Guide utility.
- `Workspaces` (boolean) - Enable/disable Workspaces utility.
- `ZoomIt` (boolean) - Enable/disable ZoomIt utility.

### startup

Controls whether PowerToys starts automatically when you sign in.

**Type:** boolean  
**Default:** `true`

### run_elevated

Controls whether PowerToys runs with administrator privileges.

**Type:** boolean  
**Default:** `false`

### theme

Sets the application theme.

**Type:** string  
**Allowed values:** `"light"`, `"dark"`, `"system"`  
**Default:** `"system"`

## Examples

### Example 1 - Enable specific utilities with direct execution

This example enables only FancyZones, PowerRename, and ColorPicker while
disabling all others.

```powershell
$config = @{
    settings = @{
        properties = @{
            Enabled = @{
                AdvancedPaste = $false
                AlwaysOnTop = $false
                Awake = $false
                ColorPicker = $true
                CropAndLock = $false
                EnvironmentVariables = $false
                FancyZones = $true
                FileLocksmith = $false
                FindMyMouse = $false
                Hosts = $false
                ImageResizer = $false
                KeyboardManager = $false
                MeasureTool = $false
                MouseHighlighter = $false
                MouseJump = $false
                MousePointerCrosshairs = $false
                Peek = $false
                PowerAccent = $false
                PowerOCR = $false
                PowerRename = $true
                RegistryPreview = $false
                ShortcutGuide = $false
                Workspaces = $false
                ZoomIt = $false
            }
        }
        name = "App"
        version = "1.0"
    }
} | ConvertTo-Json -Depth 10 -Compress

PowerToys.DSC.exe set --resource 'settings' --module App --input $config
```

### Example 2 - Configure startup and theme with DSC

This example configures PowerToys to run at startup with elevated privileges
and use dark theme.

```bash
dsc config set --file app-config.dsc.yaml
```

```yaml
# app-config.dsc.yaml
$schema: https://aka.ms/dsc/schemas/v3/bundled/config/document.json
resources:
  - name: Configure PowerToys general settings
    type: Microsoft.PowerToys/AppSettings
    properties:
      settings:
        properties:
          startup: true
          run_elevated: true
          theme: dark
        name: App
        version: 1.0
```

### Example 3 - Enable all utilities with WinGet

This example installs PowerToys and enables all available utilities.

```bash
winget configure winget-enable-all.yaml
```

```yaml
# winget-enable-all.yaml
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
  
  - name: Enable all utilities
    type: Microsoft.PowerToys/AppSettings
    properties:
      settings:
        properties:
          Enabled:
            AdvancedPaste: true
            AlwaysOnTop: true
            Awake: true
            ColorPicker: true
            CropAndLock: true
            EnvironmentVariables: true
            FancyZones: true
            FileLocksmith: true
            FindMyMouse: true
            Hosts: true
            ImageResizer: true
            KeyboardManager: true
            MeasureTool: true
            MouseHighlighter: true
            MouseJump: true
            MousePointerCrosshairs: true
            Peek: true
            PowerAccent: true
            PowerOCR: true
            PowerRename: true
            RegistryPreview: true
            ShortcutGuide: true
            Workspaces: true
            ZoomIt: true
        name: App
        version: 1.0
```

### Example 4 - Test if specific utilities are enabled

This example tests whether FancyZones and PowerRename are enabled.

```powershell
$desired = @{
    settings = @{
        properties = @{
            Enabled = @{
                FancyZones = $true
                PowerRename = $true
            }
        }
        name = "App"
        version = "1.0"
    }
} | ConvertTo-Json -Depth 10 -Compress

$result = PowerToys.DSC.exe test --resource 'settings' --module App `
    --input $desired | ConvertFrom-Json

if ($result._inDesiredState) {
    Write-Host "FancyZones and PowerRename are enabled"
} else {
    Write-Host "Configuration needs to be updated"
}
```

### Example 5 - Individual resource for each utility

This example shows enabling utilities individually, which provides better granularity for complex configurations.

```powershell
# Get current state
PowerToys.DSC.exe get --resource 'settings' --module App

# Enable individual utilities
$config = @{
    settings = @{
        properties = @{
            Enabled = @{
                FancyZones = $true
            }
        }
        name = "App"
        version = "1.0"
    }
} | ConvertTo-Json -Depth 10 -Compress

PowerToys.DSC.exe set --resource 'settings' --module App --input $config
```

### Example 6 - Get schema for App module

This example retrieves the complete JSON schema for the App module.

```powershell
PowerToys.DSC.exe schema --resource 'settings' --module App | `
    ConvertFrom-Json | ConvertTo-Json -Depth 10
```

## See also

- [Settings Resource][01]
- [PowerToys DSC Overview][02]
- [Awake][03]

<!-- Link reference definitions -->
[01]: ../settings-resource.md
[02]: ../overview.md
[03]: ./Awake.md
