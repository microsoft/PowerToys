---
description: DSC configuration reference for PowerToys EnvironmentVariables module
ms.date:     10/18/2025
ms.topic:    reference
title:       EnvironmentVariables Module
---

# EnvironmentVariables Module

## Synopsis

Manages configuration for the Environment Variables utility, a quick editor for system and user environment variables.

## Description

The `EnvironmentVariables` module configures PowerToys Environment Variables, a utility that provides an enhanced interface for viewing and editing Windows environment variables. It offers a more user-friendly alternative to the standard Windows environment variable editor.

## Properties

The EnvironmentVariables module supports the following configurable properties:

### LaunchAdministrator

Controls whether the Environment Variables editor launches with administrator privileges by default.

**Type:** boolean  
**Default:** `false`  
**Description:** When enabled, the editor will always attempt to launch with elevated permissions, allowing editing of system-wide environment variables.

## Examples

### Example 1 - Enable admin launch with direct execution

This example configures the Environment Variables editor to always launch with admin rights.

```powershell
$config = @{
    settings = @{
        properties = @{
            LaunchAdministrator = $true
        }
        name = "EnvironmentVariables"
        version = "1.0"
    }
} | ConvertTo-Json -Depth 10 -Compress

PowerToys.DSC.exe set --resource 'settings' --module EnvironmentVariables --input $config
```

### Example 2 - Configure with DSC

This example enables administrator launch through DSC configuration.

```bash
dsc config set --file environmentvariables-config.dsc.yaml
```

```yaml
# environmentvariables-config.dsc.yaml
$schema: https://aka.ms/dsc/schemas/v3/bundled/config/document.json
resources:
  - name: Configure Environment Variables editor
    type: Microsoft.PowerToys/EnvironmentVariablesSettings
    properties:
      settings:
        properties:
          LaunchAdministrator: true
        name: EnvironmentVariables
        version: 1.0
```

### Example 3 - Install and configure with WinGet

This example installs PowerToys and configures Environment Variables for
admin launch.

```bash
winget configure winget-envvars.yaml
```

```yaml
# winget-envvars.yaml
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
  
  - name: Configure Environment Variables
    type: Microsoft.PowerToys/EnvironmentVariablesSettings
    properties:
      settings:
        properties:
          LaunchAdministrator: true
        name: EnvironmentVariables
        version: 1.0
```

### Example 4 - Standard user mode

This example configures for standard user access (no elevation).

```bash
dsc config set --file envvars-user.dsc.yaml
```

```yaml
# envvars-user.dsc.yaml
$schema: https://aka.ms/dsc/schemas/v3/bundled/config/document.json
resources:
  - name: User-level Environment Variables
    type: Microsoft.PowerToys/EnvironmentVariablesSettings
    properties:
      settings:
        properties:
          LaunchAdministrator: false
        name: EnvironmentVariables
        version: 1.0
```

### Example 5 - Test admin launch configuration

This example tests whether admin launch is enabled.

```powershell
$desired = @{
    settings = @{
        properties = @{
            LaunchAdministrator = $true
        }
        name = "EnvironmentVariables"
        version = "1.0"
    }
} | ConvertTo-Json -Depth 10 -Compress

$result = PowerToys.DSC.exe test --resource 'settings' --module EnvironmentVariables --input $desired | ConvertFrom-Json

if ($result._inDesiredState) {
    Write-Host "Admin launch is enabled"
}
```

## Use cases

### System administration

Configure for system-wide environment variable management:

```yaml
resources:
  - name: Admin configuration
    type: Microsoft.PowerToys/EnvironmentVariablesSettings
    properties:
      settings:
        properties:
          LaunchAdministrator: true
        name: EnvironmentVariables
        version: 1.0
```

### Development workstations

Configure for user-level variable management:

```yaml
resources:
  - name: Developer configuration
    type: Microsoft.PowerToys/EnvironmentVariablesSettings
    properties:
      settings:
        properties:
          LaunchAdministrator: false
        name: EnvironmentVariables
        version: 1.0
```

## See also

- [Settings Resource][01]
- [PowerToys DSC Overview][02]
- [Hosts][03]
- [PowerToys Environment Variables Documentation][04]

<!-- Link reference definitions -->
[01]: ../settings-resource.md
[02]: ../overview.md
[03]: ./Hosts.md
[04]: https://learn.microsoft.com/windows/powertoys/environment-variables
