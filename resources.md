---
title: Configure PowerToys with Microsoft DSC
description: >-
  Learn how to configure PowerToys utilities using Microsoft Desired State
  Configuration v3 with the PowerToys.DSC.exe command-line tool. Modern
  declarative configuration for PowerToys.
ms.date: 09/13/2026
ms.topic: how-to
no-loc: [PowerToys, Windows, Microsoft DSC, WinGet]
# customer intent: As a Windows power user or IT administrator, I want to
# configure PowerToys using Microsoft DSC v3 and PowerToys.DSC.exe.
---

# Configure PowerToys with Microsoft DSC

PowerToys includes a Microsoft Desired State Configuration (DSC) v3
implementation through the `PowerToys.DSC.exe` command-line tool, enabling
modern declarative configuration management of PowerToys settings.

## Overview

The `PowerToys.DSC.exe` command line-tool for PowerToys provides:

- Standalone configuration tool without PowerShell dependencies
- Individual DSC resource modules for each PowerToys utility
- Standard DSC v3 operations: get, set, test, export, schema, manifest
- JSON schema generation for validation
- DSC manifest generation for resource discovery
- Integration with WinGet and other orchestrator tools

**Available since:** PowerToys v0.95.0

## Prerequisites

- **PowerToys v0.95.0 or later** - The `PowerToys.DSC.exe` tool is included
  with PowerToys starting from version 0.95.0

### Optional: Using orchestration tools

If you want to use orchestration tools to manage PowerToys configuration:

- **WinGet v1.6.2631 or later** - Required for WinGet configuration file
  integration. Download from the [WinGet releases page][07]
- **Microsoft DSC (dsc.exe) v3.1.1 or later** - Required for using
  Microsoft DSC configuration documents with the `dsc` command-line tool.
  Download from the [DSC releases page][08]

> [!NOTE]
> These orchestration tools are optional. You can use `PowerToys.DSC.exe`
> directly without WinGet or `dsc.exe` for standalone configuration management.

## Location

The `PowerToys.DSC.exe` executable is installed with PowerToys:

- **Per-user installation:**
  `%LOCALAPPDATA%\PowerToys\PowerToys.DSC.exe`
- **Machine-wide installation:**
  `%ProgramFiles%\PowerToys\PowerToys.DSC.exe`

You can add the PowerToys directory to your `PATH` environment variable for
easier access, or use the full path to the executable.

## Usage

Microsoft DSC for PowerToys supports three usage patterns:

### 1. Direct command-line execution

Execute DSC operations directly using the `PowerToys.DSC.exe` tool:

```powershell
# Get current settings for a module
PowerToys.DSC.exe get --resource 'settings' --module Awake

# Set settings for a module
$input = '{"settings":{"properties":{"keepDisplayOn":true,"mode":1},"name":"Awake","version":"0.0.1"}}'
PowerToys.DSC.exe set --resource 'settings' --module Awake --input $input

# Test if settings match desired state
PowerToys.DSC.exe test --resource 'settings' --module Awake --input $input
```

### 2. Microsoft DSC configuration documents

Use standard Microsoft DSC configuration documents to define PowerToys settings.

Save the following configuration as `powertoys.dsc.config.yaml`:

```yaml
# yaml-language-server: $schema=https://aka.ms/dsc/schemas/v3/bundled/config/document.vscode.json
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

Apply the configuration using the `dsc` command-line interface (when
available) or through WinGet configuration.

### 3. WinGet configuration integration

Integrate PowerToys configuration with WinGet package installation. Save the
following configuration as `powertoys.dsc.config.winget`:

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

Apply with WinGet:

```powershell
winget configure powertoys.dsc.config.winget
```

## Common operations

### List supported modules

List all PowerToys utilities that can be configured:

```powershell
PowerToys.DSC.exe modules --resource 'settings'
```

### Get current configuration

Retrieve the current state of a module's settings:

```powershell
# Get settings for a specific module
PowerToys.DSC.exe get --resource 'settings' --module FancyZones

# Format output for readability
PowerToys.DSC.exe get --resource 'settings' --module Awake | ConvertFrom-Json | ConvertTo-Json -Depth 10
```

### Apply configuration

Set the desired configuration for a module:

```powershell
# Define desired configuration (using PowerShell)
$config = @{
    settings = @{
        properties = @{
            fancyzones_shiftDrag = $true
            fancyzones_mouseSwitch = $true
            fancyzones_overrideSnapHotkeys = $true
        }
        name = "FancyZones"
        version = "1.0"
    }
} | ConvertTo-Json -Depth 10 -Compress

# Apply configuration
PowerToys.DSC.exe set --resource 'settings' --module FancyZones --input $config
```

### Test configuration

Verify whether current settings match the desired state:

```powershell
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

# Test for drift
$result = PowerToys.DSC.exe test --resource 'settings' --module Awake --input $desired | ConvertFrom-Json

if ($result._inDesiredState) {
    Write-Host "Configuration matches desired state"
} else {
    Write-Host "Configuration has drifted"
}
```

### Generate JSON schema

Get the JSON schema for a module to understand available properties:

```powershell
# Get schema for a module
PowerToys.DSC.exe schema --resource 'settings' --module ColorPicker

# Format for readability
PowerToys.DSC.exe schema --resource 'settings' --module ColorPicker | ConvertFrom-Json | ConvertTo-Json -Depth 10
```

### Generate Microsoft DSC resource manifests

Create Microsoft DSC resource manifest files for PowerToys modules:

```powershell
# Generate manifest for a specific module
PowerToys.DSC.exe manifest --resource 'settings' --module Awake --outputDir C:\manifests

# Generate manifests for all modules
PowerToys.DSC.exe manifest --resource 'settings' --outputDir C:\manifests

# Print manifest to console
PowerToys.DSC.exe manifest --resource 'settings' --module FancyZones
```

> [!NOTE]
> Microsoft DSC discovers resource manifests in directories listed in the `PATH`
> environment variable by default. Add your output directory to `PATH`, or generate
> manifests in the PowerToys installation directory if it is already on `PATH`:
>
> ```powershell
> # Get the directory containing the PowerToys executable
> $powerToysDirectory = Split-Path -Path (Get-Command PowerToys.DSC.exe -ErrorAction Stop).Source -Parent
>
> # Generate manifests in that directory
> PowerToys.DSC.exe manifest --resource 'settings' --outputDir $powerToysDirectory
> ```
>
> This example requires the PowerToys installation directory to be on `PATH` and
> write permission to that directory. If `DSC_RESOURCE_PATH` is defined, Microsoft
> DSC searches those directories instead of `PATH`. Include your manifest directory
> in that variable instead. For more information, see
> [DSC environment variables][09].

## Configuration examples

### Example 1: Configure FancyZones

Save the following configuration as `fancyzones.dsc.config.yaml`:

```yaml
# yaml-language-server: $schema=https://aka.ms/dsc/schemas/v3/bundled/config/document.vscode.json
$schema: https://aka.ms/dsc/schemas/v3/bundled/config/document.json
resources:
  - name: Configure FancyZones window management
    type: Microsoft.PowerToys/FancyZonesSettings
    properties:
      settings:
        properties:
          fancyzones_shiftDrag: true
          fancyzones_mouseSwitch: false
          fancyzones_overrideSnapHotkeys: true
          fancyzones_displayOrWorkAreaChange_moveWindows: true
          fancyzones_zoneSetChange_moveWindows: true
        name: FancyZones
        version: 1.0
```

### Example 2: Configure multiple utilities

Save the following configuration as `multi-utility.dsc.config.yaml`:

```yaml
# yaml-language-server: $schema=https://aka.ms/dsc/schemas/v3/bundled/config/document.vscode.json
$schema: https://aka.ms/dsc/schemas/v3/bundled/config/document.json
resources:
  - name: Configure general app settings
    type: Microsoft.PowerToys/AppSettings
    properties:
      settings:
        properties:
          Enabled:
            Awake: true
            FancyZones: true
            PowerRename: true
            ColorPicker: true
          run_elevated: true
          startup: true
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

### Example 3: Install and configure with WinGet

Save the following configuration as `complete-setup.dsc.config.winget`:

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
      ensure: Present
  
  - name: Enable utilities
    type: Microsoft.PowerToys/AppSettings
    properties:
      settings:
        properties:
          startup: true
          theme: "dark"
        name: App
        version: 1.0
  
  - name: Configure PowerToys Run
    type: Microsoft.PowerToys/PowerLauncherSettings
    properties:
      settings:
        properties:
          maximum_number_of_results: 8
          clear_input_on_launch: true
        name: PowerLauncher
        version: 1.0
```

## Available resources

Microsoft DSC for PowerToys provides individual resource types for each
utility. The resource type naming follows the pattern:
`Microsoft.PowerToys/<UtilityName>Settings`

Common resources include:

- `Microsoft.PowerToys/AppSettings` - General application settings
- `Microsoft.PowerToys/AlwaysOnTopSettings` - Always On Top configuration
- `Microsoft.PowerToys/AwakeSettings` - Awake keep-awake settings
- `Microsoft.PowerToys/ColorPickerSettings` - Color Picker settings
- `Microsoft.PowerToys/FancyZonesSettings` - FancyZones window management
- `Microsoft.PowerToys/PowerLauncherSettings` - PowerToys Run settings
- `Microsoft.PowerToys/PowerRenameSettings` - PowerRename bulk rename
- And many more for each PowerToys utility

For a complete list of resources and their properties, see the
[developer documentation][01].

## Advanced usage

### Backup and restore configuration

Export all module configurations for backup:

```powershell
# Get list of all modules
$modules = PowerToys.DSC.exe modules --resource 'settings'

# Export each module
$backup = @{}
foreach ($module in $modules) {
    $config = PowerToys.DSC.exe export --resource 'settings' --module $module | ConvertFrom-Json
    $backup[$module] = $config
}

# Save backup
$backup | ConvertTo-Json -Depth 10 | Out-File powertoys-backup.json

# Restore from backup
$restore = Get-Content powertoys-backup.json | ConvertFrom-Json
foreach ($module in $restore.PSObject.Properties.Name) {
    $input = $restore.$module | ConvertTo-Json -Depth 10 -Compress
    PowerToys.DSC.exe set --resource 'settings' --module $module --input $input
}
```

## Migrating from PowerShell DSC

If you're migrating from the PowerShell DSC module
(`Microsoft.PowerToys.Configure`), note these key differences:

1. **Resource naming:** PowerShell DSC uses a single `PowerToysConfigure`
   resource with nested properties. Microsoft DSC uses individual resources
   per utility (e.g., `AwakeSettings`, `FancyZonesSettings`)

2. **Property format:** Some property names may differ slightly. Use the
   `schema` command to see available properties for each module

3. **Configuration structure:** Microsoft DSC uses the `settings` wrapper with
   `properties`, `name`, and `version` fields

4. **No PowerShell required:** Microsoft DSC runs standalone without
   PowerShell dependencies

## See also

- [Configure PowerToys with DSC overview][02]
- [Configure with PowerShell DSC][03]
- [PowerToys DSC developer documentation][01]
- [PowerToys installation guide][04]
- [Microsoft DSC documentation][05]
- [WinGet configuration documentation][06]

<!-- Link reference definitions -->
[01]: https://github.com/microsoft/PowerToys/tree/main/doc/dsc/overview.md
[02]: overview.md
[03]: psdsc.md
[04]: ../install.md
[05]: /powershell/dsc/overview
[06]: /windows/package-manager/configuration/
[07]: https://github.com/microsoft/winget-cli/releases
[08]: https://github.com/PowerShell/DSC/releases
[09]: /powershell/dsc/reference/cli/?view=dsc-3.0&preserve-view=true#environment-variables
