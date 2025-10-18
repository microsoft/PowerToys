---
description: Reference for the PowerToys DSC settings resource
ms.date:     10/18/2025
ms.topic:    reference
title:       Settings Resource
---

# Settings Resource

## Synopsis

Manages configuration settings for PowerToys utilities (modules).

## Description

The `settings` resource provides Microsoft Desired State Configuration (DSC)
support for managing PowerToys configuration. It enables declarative
configuration of PowerToys utilities, allowing you to define, test, and
enforce desired states for each module.

Each PowerToys utility (module) has its own configurable properties that can
be managed through this resource. The settings resource supports standard DSC
operations: get, set, test, export, schema, and manifest generation.

## Supported modules

The settings resource supports the following PowerToys modules:

- **App** - General application settings (enable/disable utilities, run at
  startup, theme, etc.).
- **AdvancedPaste** - Advanced clipboard and paste operations.
- **AlwaysOnTop** - Window pinning configuration.
- **Awake** - Keep-awake timer settings.
- **ColorPicker** - Color picker activation and format settings.
- **CropAndLock** - Window cropping settings.
- **EnvironmentVariables** - Environment variable editor settings.
- **FancyZones** - Window layout and zone configuration.
- **FileLocksmith** - File lock detection settings.
- **FindMyMouse** - Mouse locator settings.
- **Hosts** - Hosts file editor settings.
- **ImageResizer** - Image resize configuration.
- **KeyboardManager** - Key remapping and shortcut settings.
- **MeasureTool** - Screen measurement tool settings.
- **MouseHighlighter** - Mouse highlighting configuration.
- **MouseJump** - Mouse jump navigation settings.
- **MousePointerCrosshairs** - Crosshair display settings.
- **Peek** - File preview settings.
- **PowerAccent** - Accent character selection settings.
- **PowerOCR** - Text extraction settings.
- **PowerRename** - Bulk rename configuration.
- **RegistryPreview** - Registry file preview settings.
- **ShortcutGuide** - Keyboard shortcut overlay settings.
- **Workspaces** - Application workspace settings.
- **ZoomIt** - Screen zoom and annotation settings.

For detailed property information for each module, see the individual [module
documentation][01].

## Operations

### List supported modules

List all modules that can be configured with the settings resource.

**Direct execution:**

```powershell
# List all configurable modules.
PowerToys.DSC.exe modules --resource 'settings'
```

### Get current state

Retrieve the current configuration state for a module.

**Direct execution:**

```powershell
# Get current settings for a module.
PowerToys.DSC.exe get --resource 'settings' --module <ModuleName>
```

**DSC configuration:**

```yaml
$schema: https://aka.ms/dsc/schemas/v3/bundled/config/document.json
resources:
  - name: Get Awake settings
    type: Microsoft.PowerToys/AwakeSettings
    properties: {}
```

**WinGet configuration:**

```yaml
$schema: https://raw.githubusercontent.com/PowerShell/DSC/main/schemas/2023/08/config/document.json
metadata:
  winget:
    processor: dscv3
resources:
  - name: Get FancyZones settings
    type: Microsoft.PowerToys/FancyZonesSettings
    properties: {}
```

### Export current state

Export the current configuration state. The output is identical to the `get`
operation.

**Direct execution:**

```powershell
# Export current settings for a module.
PowerToys.DSC.exe export --resource 'settings' --module <ModuleName>
```

### Set desired state

Apply a configuration to a module, updating only the properties that differ
from the desired state.

**Direct execution:**

```powershell
# Set desired configuration for a module.
$input = '{
  "settings": {
    "properties": {
      "keepDisplayOn": true,
      "mode": 1
    },
    "name": "Awake",
    "version": "0.0.1"
  }
}'
PowerToys.DSC.exe set --resource 'settings' --module Awake --input $input
```

**DSC configuration:**

```yaml
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

**WinGet configuration:**

```yaml
$schema: https://raw.githubusercontent.com/PowerShell/DSC/main/schemas/2023/08/config/document.json
metadata:
  winget:
    processor: dscv3
resources:
  - name: Install and configure PowerToys
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
          fancyzones_displayOrWorkAreaChange_moveWindows: true
        name: FancyZones
        version: 1.0
```

### Test desired state

Verify whether the current configuration matches the desired state.

**Direct execution:**

```powershell
# Test if current state matches desired state.
$input = '{
  "settings": {
    "properties": {
      "keepDisplayOn": true,
      "mode": 1
    },
    "name": "Awake",
    "version": "0.0.1"
  }
}'
PowerToys.DSC.exe test --resource 'settings' --module Awake --input $input
```

The output includes an `_inDesiredState` property indicating whether the
configuration matches (`true`) or differs (`false`).

**DSC configuration:**

```yaml
$schema: https://aka.ms/dsc/schemas/v3/bundled/config/document.json
resources:
  - name: Test Awake configuration
    type: Microsoft.PowerToys/AwakeSettings
    properties:
      settings:
        properties:
          keepDisplayOn: true
          mode: 1
        name: Awake
        version: 0.0.1
```

### Get schema

Generate the JSON schema for a module's settings, describing all configurable
properties and their types.

**Direct execution:**

```powershell
# Get JSON schema for a module.
PowerToys.DSC.exe schema --resource 'settings' --module Awake

# Format for readability.
PowerToys.DSC.exe schema --resource 'settings' --module Awake `
  | ConvertFrom-Json | ConvertTo-Json -Depth 10
```

### Generate manifest

Create a DSC resource manifest file for one or all modules.

**Direct execution:**

```powershell
# Generate manifest for a specific module.
$outputDir = "C:\manifests"
PowerToys.DSC.exe manifest --resource 'settings' --module Awake `
  --outputDir $outputDir

# Generate manifests for all modules.
PowerToys.DSC.exe manifest --resource 'settings' --outputDir $outputDir

# Print manifest to console (omit --outputDir).
PowerToys.DSC.exe manifest --resource 'settings' --module Awake
```

## Examples

### Example 1 - Enable and configure FancyZones

This example enables FancyZones and configures window dragging behavior using
direct execution.

```powershell
# Get current FancyZones settings.
$current = PowerToys.DSC.exe get --resource 'settings' --module FancyZones `
  | ConvertFrom-Json

# Modify settings.
$desired = @{
    settings = @{
        properties = @{
            fancyzones_shiftDrag = $true
            fancyzones_mouseSwitch = $true
            fancyzones_displayOrWorkAreaChange_moveWindows = $true
        }
        name = "FancyZones"
        version = "1.0"
    }
} | ConvertTo-Json -Depth 10 -Compress

# Apply configuration.
PowerToys.DSC.exe set --resource 'settings' --module FancyZones `
  --input $desired
```

### Example 2 - Configure multiple utilities with DSC

This example configures multiple PowerToys utilities in a single DSC
configuration.

```yaml
# powertoys-multi.dsc.yaml
$schema: https://aka.ms/dsc/schemas/v3/bundled/config/document.json
resources:
  - name: Enable PowerToys utilities
    type: Microsoft.PowerToys/AppSettings
    properties:
      settings:
        properties:
          Enabled:
            Awake: true
            FancyZones: true
            PowerRename: true
            ColorPicker: true
        name: App
        version: 1.0
  
  - name: Configure Awake
    type: Microsoft.PowerToys/AwakeSettings
    properties:
      settings:
        properties:
          keepDisplayOn: true
          mode: 1
        name: Awake
        version: 0.0.1
  
  - name: Configure ColorPicker
    type: Microsoft.PowerToys/ColorPickerSettings
    properties:
      settings:
        properties:
          changecursor: true
          copiedcolorrepresentation: "HEX"
        name: ColorPicker
        version: 1.0
```

### Example 3 - Install and configure with WinGet

This example installs PowerToys and applies configuration using WinGet.

```yaml
# winget-powertoys-setup.yaml
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
      ensure: Present
  
  - name: Configure general settings
    type: Microsoft.PowerToys/AppSettings
    properties:
      settings:
        properties:
          run_elevated: true
          startup: true
          theme: "dark"
        name: App
        version: 1.0
  
  - name: Configure FancyZones
    type: Microsoft.PowerToys/FancyZonesSettings
    properties:
      settings:
        properties:
          fancyzones_shiftDrag: true
          fancyzones_zoneSetChange_moveWindows: true
        name: FancyZones
        version: 1.0
  
  - name: Configure ImageResizer
    type: Microsoft.PowerToys/ImageResizerSettings
    properties:
      settings:
        properties:
          ImageResizerSizes:
            - Name: Small
              Width: 854
              Height: 480
              Unit: Pixel
              Fit: Fit
            - Name: Medium
              Width: 1920
              Height: 1080
              Unit: Pixel
              Fit: Fit
        name: ImageResizer
        version: 1.0
```

Apply the configuration:

```powershell
winget configure winget-powertoys-setup.yaml
```

### Example 4 - Test configuration drift

This example tests whether the current configuration matches the desired
state.

```powershell
# Define desired state.
$desired = @{
    settings = @{
        properties = @{
            keepDisplayOn = $true
            mode = 1
        }
        name = "Awake"
        version = "0.0.1"
    }
} | ConvertTo-Json -Depth 10 -Compress

# Test for drift.
$result = PowerToys.DSC.exe test --resource 'settings' --module Awake `
  --input $desired | ConvertFrom-Json

if ($result._inDesiredState) {
    Write-Host "Configuration is in desired state"
} else {
    Write-Host "Configuration has drifted from desired state"
    
    # Apply configuration.
    PowerToys.DSC.exe set --resource 'settings' --module Awake `
      --input $desired
}
```

### Example 5 - Export all module configurations

This example exports configuration for all modules.

```powershell
# Get list of all modules.
$modules = PowerToys.DSC.exe modules --resource 'settings'

# Export each module's configuration.
$configurations = @{}
foreach ($module in $modules) {
    $config = PowerToys.DSC.exe export --resource 'settings' `
      --module $module | ConvertFrom-Json
    $configurations[$module] = $config
}

# Save to file.
$configurations | ConvertTo-Json -Depth 10 `
  | Out-File "powertoys-backup.json"
```

## See also

- [PowerToys DSC Overview][02]
- [Module Documentation][01]
- [WinGet Configuration][03]

<!-- Link reference definitions -->
[01]: ./modules/
[02]: ./overview.md
[03]: ./winget-configuration.md
