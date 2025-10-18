---
description: DSC configuration reference for PowerToys Hosts module
ms.date:     10/18/2025
ms.topic:    reference
title:       Hosts Module
---

# Hosts Module

## Synopsis

Manages configuration for the Hosts File Editor utility, a quick editor for the Windows hosts file.

## Description

The `Hosts` module configures PowerToys Hosts File Editor, a utility that provides a user-friendly interface for viewing and editing the Windows hosts file. It simplifies the process of adding, modifying, and managing DNS entries in the hosts file.

## Properties

The Hosts module supports the following configurable properties:

### LaunchAdministrator

Controls whether the Hosts File Editor launches with administrator privileges by default.

**Type:** boolean  
**Default:** `false`  
**Description:** When enabled, the editor will always attempt to launch with elevated permissions, which is required to edit the hosts file.

### LoopbackDuplicates

Controls how duplicate loopback addresses are handled.

**Type:** boolean  
**Default:** `false`

### AdditionalLinesPosition

Controls where additional lines are positioned when editing entries.

**Type:** integer  
**Allowed values:**
- `0` - Top
- `1` - Bottom

**Default:** `0`

## Examples

### Example 1 - Enable admin launch with direct execution

This example configures the Hosts editor to always launch with admin rights.

```powershell
$config = @{
    settings = @{
        properties = @{
            LaunchAdministrator = $true
        }
        name = "Hosts"
        version = "1.0"
    }
} | ConvertTo-Json -Depth 10 -Compress

PowerToys.DSC.exe set --resource 'settings' --module Hosts --input $config
```

### Example 2 - Configure with DSC

This example enables administrator launch and configures line positioning.

```yaml
# hosts-config.dsc.yaml
$schema: https://aka.ms/dsc/schemas/v3/bundled/config/document.json
resources:
  - name: Configure Hosts File Editor
    type: Microsoft.PowerToys/HostsSettings
    properties:
      settings:
        properties:
          LaunchAdministrator: true
          AdditionalLinesPosition: 1
        name: Hosts
        version: 1.0
```

### Example 3 - Install and configure with WinGet

This example installs PowerToys and configures the Hosts editor for admin launch.

```yaml
# winget-hosts.yaml
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
  
  - name: Configure Hosts File Editor
    type: Microsoft.PowerToys/HostsSettings
    properties:
      settings:
        properties:
          LaunchAdministrator: true
          LoopbackDuplicates: false
        name: Hosts
        version: 1.0
```

### Example 4 - Development configuration

This example configures for development use with new entries at the bottom.

```yaml
# hosts-development.dsc.yaml
$schema: https://aka.ms/dsc/schemas/v3/bundled/config/document.json
resources:
  - name: Development hosts configuration
    type: Microsoft.PowerToys/HostsSettings
    properties:
      settings:
        properties:
          LaunchAdministrator: true
          AdditionalLinesPosition: 1
        name: Hosts
        version: 1.0
```

## Use cases

### System administration

Configure for frequent hosts file editing:

```yaml
resources:
  - name: Admin configuration
    type: Microsoft.PowerToys/HostsSettings
    properties:
      settings:
        properties:
          LaunchAdministrator: true
        name: Hosts
        version: 1.0
```

### Web development

Configure for development environment management:

```yaml
resources:
  - name: Developer configuration
    type: Microsoft.PowerToys/HostsSettings
    properties:
      settings:
        properties:
          LaunchAdministrator: true
          AdditionalLinesPosition: 1
        name: Hosts
        version: 1.0
```

## See also

- [Settings Resource][01]
- [PowerToys DSC Overview][02]
- [App Module][03] - For enabling/disabling Hosts File Editor utility
- [PowerToys Hosts File Editor Documentation][04]

<!-- Link reference definitions -->
[01]: ../settings-resource.md
[02]: ../overview.md
[03]: ./App.md
[04]: https://learn.microsoft.com/windows/powertoys/hosts-file-editor
