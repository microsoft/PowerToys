---
description: DSC configuration reference for PowerToys CursorBrake module
ms.date:     10/13/2026
ms.topic:    reference
title:       CursorBrake Module
---

# CursorBrake Module

## Synopsis

Manages configuration for the CursorBrake utility, which adds resistance
where two monitors meet.

## Description

The `CursorBrake` module configures PowerToys CursorBrake, a Mouse
Utilities sub-module. The cursor stops at an edge shared by two monitors
until you push past it for a set distance. Moving back from the
edge drains the push. Holding Ctrl, or Shift if `cross_modifier` is 1,
crosses freely. Outer edges and single-monitor setups are left untouched.

## Properties

The CursorBrake module supports the following configurable properties.
Each property is set by its JSON key, and its value is wrapped in an object
with a single `value` field (for example, `push_distance: { value: 300 }`).

### push_distance

Sets how far you push past an edge before the cursor crosses, in pixels at
100% scale. The distance scales with the DPI of the monitor the cursor
leaves. `0` turns the resistance off.

**Type:** integer  
**Range:** `0` to `1000` (a hand-edited value is clamped to this range)  
**Default:** `300`

### free_return

Controls whether the cursor can cross straight back over an edge it just
crossed without resistance.

**Type:** boolean  
**Default:** `true`

### edge_filter

Selects which edges between monitors resist the cursor.

**Type:** integer  
**Allowed values:**

- `0` - All edges between monitors
- `1` - Side-by-side only
- `2` - Stacked only

**Default:** `0`

### disable_while_dragging

Controls whether the cursor crosses freely while a mouse button is held.

**Type:** boolean  
**Default:** `false`

### cross_modifier

Selects the key that crosses any edge freely while held.

**Type:** integer  
**Allowed values:**

- `0` - Ctrl
- `1` - Shift

**Default:** `0`

## Examples

### Example 1 - Configure strength with direct execution

This example sets a higher strength.

```powershell
$config = @{
    settings = @{
        properties = @{
            push_distance = @{ value = 500 }
        }
        name = "CursorBrake"
        version = "1.0"
    }
} | ConvertTo-Json -Depth 10 -Compress

PowerToys.DSC.exe set --resource 'settings' --module CursorBrake --input $config
```

### Example 2 - Brake only side-by-side edges with DSC

This example limits the resistance to edges between monitors placed next to
each other and uses Shift to cross freely.

```bash
dsc config set --file cursorbrake-side-by-side.dsc.yaml
```

```yaml
# cursorbrake-side-by-side.dsc.yaml
$schema: https://aka.ms/dsc/schemas/v3/bundled/config/document.json
resources:
  - name: Configure CursorBrake edges
    type: Microsoft.PowerToys/CursorBrakeSettings
    properties:
      settings:
        properties:
          edge_filter:
            value: 1
          cross_modifier:
            value: 1
        name: CursorBrake
        version: 1.0
```

### Example 3 - Install and configure with WinGet

This example installs PowerToys and configures CursorBrake to brake every
crossing, including the way back.

```bash
winget configure winget-cursorbrake.yaml
```

```yaml
# winget-cursorbrake.yaml
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

  - name: Configure CursorBrake
    type: Microsoft.PowerToys/CursorBrakeSettings
    properties:
      settings:
        properties:
          push_distance:
            value: 300
          free_return:
            value: false
        name: CursorBrake
        version: 1.0
```

## See also

- [Settings Resource][01]
- [PowerToys DSC Overview][02]
- [FindMyMouse][03]

<!-- Link reference definitions -->
[01]: ../settings-resource.md
[02]: ../overview.md
[03]: ./FindMyMouse.md
