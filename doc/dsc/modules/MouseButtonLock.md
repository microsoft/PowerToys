---
description: DSC configuration reference for PowerToys MouseButtonLock module
ms.date:     08/01/2026
ms.topic:    reference
title:       MouseButtonLock Module
---

# MouseButtonLock Module

## Synopsis

Manages configuration for the Mouse Button Lock utility, which lets you
hold a mouse button down without keeping it physically pressed.

## Description

The `MouseButtonLock` module configures PowerToys Mouse Button Lock, a
Mouse Utilities sub-module that provides ClickLock-style hold-to-lock
behavior for the left, right, and middle mouse buttons. Hold a button past
the configured hold duration, then release it: the button stays logically
held so you can drag hands-free. Pressing any mouse button releases the
lock.

## Properties

The MouseButtonLock module supports the following configurable properties.
Each property is set by its JSON key, and its value is wrapped in an object
with a single `value` field (for example, `rmb_lock_enabled: { value: true }`).

### lmb_lock_enabled

Controls whether the left (primary) mouse button can be locked.

**Type:** boolean  
**Default:** `false`  
**Description:** Off by default because Windows already ships ClickLock
for the left button.

### rmb_lock_enabled

Controls whether the right mouse button can be locked.

**Type:** boolean  
**Default:** `true`

### mmb_lock_enabled

Controls whether the middle mouse button can be locked.

**Type:** boolean  
**Default:** `false`

### hold_duration_ms

Sets how long a button must be held, in milliseconds, before it locks.

**Type:** integer  
**Range:** `200` to `60000` (the Settings UI slider covers `200` to
`2200`; a hand-edited value is clamped to the full range)  
**Default:** `1200`

### move_cancel_pixels

Sets the drag threshold in pixels that separates hand jitter from a
deliberate drag. Moving the cursor beyond this distance during the hold
cancels the pending lock.

**Type:** integer  
**Default:** `5`

## Examples

### Example 1 - Configure button locks with direct execution

This example enables locking for the left and middle buttons in addition
to the default right button.

```powershell
$config = @{
    settings = @{
        properties = @{
            lmb_lock_enabled = @{ value = $true }
            rmb_lock_enabled = @{ value = $true }
            mmb_lock_enabled = @{ value = $true }
        }
        name = "MouseButtonLock"
        version = "1.0"
    }
} | ConvertTo-Json -Depth 10 -Compress

PowerToys.DSC.exe set --resource 'settings' --module MouseButtonLock --input $config
```

### Example 2 - Configure hold timing with DSC

This example lengthens the hold duration and widens the drag threshold.

```bash
dsc config set --file mousebuttonlock-timing.dsc.yaml
```

```yaml
# mousebuttonlock-timing.dsc.yaml
$schema: https://aka.ms/dsc/schemas/v3/bundled/config/document.json
resources:
  - name: Configure Mouse Button Lock timing
    type: Microsoft.PowerToys/MouseButtonLockSettings
    properties:
      settings:
        properties:
          hold_duration_ms:
            value: 1600
          move_cancel_pixels:
            value: 10
        name: MouseButtonLock
        version: 1.0
```

### Example 3 - Install and configure with WinGet

This example installs PowerToys and configures Mouse Button Lock for
right-button dragging.

```bash
winget configure winget-mousebuttonlock.yaml
```

```yaml
# winget-mousebuttonlock.yaml
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
  
  - name: Configure Mouse Button Lock
    type: Microsoft.PowerToys/MouseButtonLockSettings
    properties:
      settings:
        properties:
          rmb_lock_enabled:
            value: true
          hold_duration_ms:
            value: 1000
        name: MouseButtonLock
        version: 1.0
```

### Example 4 - Middle button locking for panning

This example enables the middle mouse button lock, which is useful for
hands-free panning in map and design applications.

```bash
dsc config set --file mousebuttonlock-middle.dsc.yaml
```

```yaml
# mousebuttonlock-middle.dsc.yaml
$schema: https://aka.ms/dsc/schemas/v3/bundled/config/document.json
resources:
  - name: Enable middle button lock
    type: Microsoft.PowerToys/MouseButtonLockSettings
    properties:
      settings:
        properties:
          mmb_lock_enabled:
            value: true
        name: MouseButtonLock
        version: 1.0
```

## Use cases

### Hands-free dragging

Configure a short hold duration for quick, deliberate drags:

```yaml
resources:
  - name: Quick drag configuration
    type: Microsoft.PowerToys/MouseButtonLockSettings
    properties:
      settings:
        properties:
          rmb_lock_enabled:
            value: true
          hold_duration_ms:
            value: 600
          move_cancel_pixels:
            value: 3
        name: MouseButtonLock
        version: 1.0
```

### Accessibility

Configure a longer hold duration and wider drag threshold to reduce
accidental locks or cancellations:

```yaml
resources:
  - name: Accessibility configuration
    type: Microsoft.PowerToys/MouseButtonLockSettings
    properties:
      settings:
        properties:
          lmb_lock_enabled:
            value: true
          rmb_lock_enabled:
            value: true
          hold_duration_ms:
            value: 2000
          move_cancel_pixels:
            value: 15
        name: MouseButtonLock
        version: 1.0
```

## See also

- [Settings Resource][01]
- [PowerToys DSC Overview][02]
- [MouseHighlighter][03]

<!-- Link reference definitions -->
[01]: ../settings-resource.md
[02]: ../overview.md
[03]: ./MouseHighlighter.md
