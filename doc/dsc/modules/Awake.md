---
description: DSC configuration reference for PowerToys Awake module
ms.date:     10/18/2025
ms.topic:    reference
title:       Awake Module
---

# Awake Module

## Synopsis

Manages configuration for the Awake utility, which keeps your computer awake without changing power settings.

## Description

The `Awake` module configures PowerToys Awake, a utility that prevents your
computer from going to sleep or turning off the display. This is useful
during installations, presentations, or any scenario where you need to
temporarily override power settings without permanently changing them.

Awake supports multiple modes including indefinite keep-awake, timed
intervals, and scheduled expiration. The display can be kept on or allowed
to turn off independently of the system sleep state.

## Properties

The Awake module supports the following configurable properties:

### keepDisplayOn

Controls whether the display remains on while Awake is active.

**Type:** boolean  
**Default:** `true`  
**Description:** When `true`, prevents the display from turning off. When
`false`, only prevents system sleep while allowing the display to turn off
according to power settings.

### mode

Specifies the Awake operating mode.

**Type:** integer  
**Allowed values:**

- `0` - Off (Awake is disabled).
- `1` - Keep awake indefinitely.
- `2` - Keep awake for a timed interval.
- `3` - Keep awake until a specific date/time.

**Default:** `0`

### intervalHours

Number of hours to keep the system awake (used when mode is `2`).

**Type:** integer  
**Range:** `0` to `999`  
**Default:** `0`

### intervalMinutes

Number of minutes to keep the system awake (used when mode is `2`).

**Type:** integer  
**Range:** `0` to `59`  
**Default:** `1`

### expirationDateTime

The date and time when Awake should automatically disable (used when mode is `3`).

**Type:** string (ISO 8601 datetime format)  
**Format:** `"YYYY-MM-DDTHH:mm:ss.fffffffzzz"`  
**Example:** `"2025-12-31T23:59:59.0000000-08:00"`

### customTrayTimes

Custom time intervals displayed in the system tray context menu for quick activation.

**Type:** object  
**Description:** A dictionary of custom time presets for quick access. Keys
are display names, values are time specifications.

## Examples

### Example 1 - Keep awake indefinitely with display on

This example configures Awake to keep the system and display awake
indefinitely using direct execution.

```powershell
$config = @{
    settings = @{
        properties = @{
            keepDisplayOn = $true
            mode = 1
        }
        name = "Awake"
        version = "0.0.1"
    }
} | ConvertTo-Json -Depth 10 -Compress

PowerToys.DSC.exe set --resource 'settings' --module Awake --input $config
```

### Example 2 - Keep awake for 2 hours with DSC

This example configures a timed keep-awake period.

```bash
dsc config set --file awake-timed.dsc.yaml
```

```yaml
# awake-timed.dsc.yaml
$schema: https://aka.ms/dsc/schemas/v3/bundled/config/document.json
resources:
  - name: Configure Awake for 2 hours
    type: Microsoft.PowerToys/AwakeSettings
    properties:
      settings:
        properties:
          keepDisplayOn: true
          mode: 2
          intervalHours: 2
          intervalMinutes: 0
        name: Awake
        version: 0.0.1
```

### Example 3 - Keep awake until specific time with WinGet

This example configures Awake to stay active until a specific date and time.

```bash
winget configure winget-awake-scheduled.yaml
```

```yaml
# winget-awake-scheduled.yaml
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
  
  - name: Keep awake until end of workday
    type: Microsoft.PowerToys/AwakeSettings
    properties:
      settings:
        properties:
          keepDisplayOn: true
          mode: 3
          expirationDateTime: "2025-10-18T17:00:00.0000000-07:00"
        name: Awake
        version: 0.0.1
```

### Example 4 - Disable Awake

This example disables Awake using direct execution.

```powershell
$config = @{
    settings = @{
        properties = @{
            mode = 0
        }
        name = "Awake"
        version = "0.0.1"
    }
} | ConvertTo-Json -Depth 10 -Compress

PowerToys.DSC.exe set --resource 'settings' --module Awake --input $config
```

### Example 5 - Keep system awake but allow display to sleep

This example keeps the system awake while allowing the display to turn off.

```bash
dsc config set --file awake-system-only.dsc.yaml
```

```yaml
# awake-system-only.dsc.yaml
$schema: https://aka.ms/dsc/schemas/v3/bundled/config/document.json
resources:
  - name: Keep system awake only
    type: Microsoft.PowerToys/AwakeSettings
    properties:
      settings:
        properties:
          keepDisplayOn: false
          mode: 1
        name: Awake
        version: 0.0.1
```

### Example 6 - Configure for presentation (4 hours)

This example configures Awake for a presentation scenario using WinGet.

```bash
winget configure presentation-mode.yaml
```

```yaml
# presentation-mode.yaml
$schema: https://raw.githubusercontent.com/PowerShell/DSC/main/schemas/2023/08/config/document.json
metadata:
  winget:
    processor: dscv3
resources:
  - name: Enable Awake for presentation
    type: Microsoft.PowerToys/AwakeSettings
    properties:
      settings:
        properties:
          keepDisplayOn: true
          mode: 2
          intervalHours: 4
          intervalMinutes: 0
        name: Awake
        version: 0.0.1
```

### Example 7 - Test current configuration

This example tests whether Awake is configured for indefinite keep-awake.

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

$result = PowerToys.DSC.exe test --resource 'settings' --module Awake --input $desired | ConvertFrom-Json

if ($result._inDesiredState) {
    Write-Host "Awake is configured for indefinite keep-awake"
} else {
    Write-Host "Awake configuration differs from desired state"
}
```

### Example 8 - Get current Awake configuration

This example retrieves the current Awake settings.

```powershell
PowerToys.DSC.exe get --resource 'settings' --module Awake | ConvertFrom-Json | ConvertTo-Json -Depth 10
```

### Example 9 - Get Awake schema

This example retrieves the JSON schema for Awake module properties.

```powershell
PowerToys.DSC.exe schema --resource 'settings' --module Awake | ConvertFrom-Json | ConvertTo-Json -Depth 10
```

## Use cases

### Development and builds

Keep the system awake during long-running builds or installations:

```yaml
resources:
  - name: Keep awake during build
    type: Microsoft.PowerToys/AwakeSettings
    properties:
      settings:
        properties:
          mode: 2
          intervalHours: 8
          intervalMinutes: 0
          keepDisplayOn: false
        name: Awake
        version: 0.0.1
```

### Presentations and demos

Ensure the system stays awake during presentations:

```yaml
resources:
  - name: Presentation mode
    type: Microsoft.PowerToys/AwakeSettings
    properties:
      settings:
        properties:
          mode: 1
          keepDisplayOn: true
        name: Awake
        version: 0.0.1
```

### Scheduled maintenance

Keep the system awake until a specific time for scheduled tasks:

```yaml
resources:
  - name: Maintenance window
    type: Microsoft.PowerToys/AwakeSettings
    properties:
      settings:
        properties:
          mode: 3
          expirationDateTime: "2025-10-19T02:00:00.0000000-07:00"
          keepDisplayOn: false
        name: Awake
        version: 0.0.1
```

## See also

- [Settings Resource][01]
- [PowerToys DSC Overview][02]
- [PowerRename][03]
- [PowerToys Awake Documentation][04]

<!-- Link reference definitions -->
[01]: ../settings-resource.md
[02]: ../overview.md
[03]: ./PowerRename.md
[04]: https://learn.microsoft.com/windows/powertoys/awake
