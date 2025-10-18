---
description: DSC configuration reference for PowerToys PowerAccent module
ms.date:     10/18/2025
ms.topic:    reference
title:       PowerAccent Module
---

# PowerAccent Module

## Synopsis

Manages configuration for the Power Accent utility, a quick accent character selector.

## Description

The `PowerAccent` module configures PowerToys Power Accent (Quick Accent), a utility that provides quick access to accented characters. Hold down a key and use arrow keys or numbers to select from available accent variations.

## Properties

The PowerAccent module supports the following configurable properties:

### ActivationKey

Sets which key triggers the accent selection.

**Type:** string  
**Allowed values:**
- `"LeftRightArrow"` - Hold left or right arrow keys
- `"Space"` - Hold spacebar  
- `"Both"` - Hold either left/right arrows or spacebar

**Default:** `"Both"`

### InputTime

Sets how long the activation key must be held (in milliseconds) before showing accents.

**Type:** integer  
**Range:** `100` to `1000`  
**Default:** `300`

### ExcludedApps

List of applications where Power Accent is disabled.

**Type:** string (newline-separated list of executable names)

### ToolbarPosition

Sets the position of the accent selection toolbar.

**Type:** string  
**Allowed values:**
- `"Top"` - Above the cursor
- `"Bottom"` - Below the cursor
- `"Left"` - To the left of cursor
- `"Right"` - To the right of cursor
- `"Center"` - Centered on screen

**Default:** `"Top"`

### ShowUnicodeDescription

Controls whether Unicode descriptions are shown for each accent character.

**Type:** boolean  
**Default:** `false`

### SortByUsageFrequency

Controls whether accent characters are sorted by usage frequency.

**Type:** boolean  
**Default:** `false`

### StartSelectionFromTheLeft

Controls whether selection starts from the left side.

**Type:** boolean  
**Default:** `false`

## Examples

### Example 1 - Configure activation method with direct execution

This example sets spacebar as the activation key.

```powershell
$config = @{
    settings = @{
        properties = @{
            ActivationKey = "Space"
            InputTime = 250
        }
        name = "PowerAccent"
        version = "1.0"
    }
} | ConvertTo-Json -Depth 10 -Compress

PowerToys.DSC.exe set --resource 'settings' --module PowerAccent --input $config
```

### Example 2 - Configure toolbar appearance with DSC

This example customizes the toolbar position and display options.

```yaml
# poweraccent-toolbar.dsc.yaml
$schema: https://aka.ms/dsc/schemas/v3/bundled/config/document.json
resources:
  - name: Configure Power Accent toolbar
    type: Microsoft.PowerToys/PowerAccentSettings
    properties:
      settings:
        properties:
          ToolbarPosition: Bottom
          ShowUnicodeDescription: true
          SortByUsageFrequency: true
        name: PowerAccent
        version: 1.0
```

### Example 3 - Install and configure with WinGet

This example installs PowerToys and configures Power Accent for multilingual typing.

```yaml
# winget-poweraccent.yaml
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
  
  - name: Configure Power Accent
    type: Microsoft.PowerToys/PowerAccentSettings
    properties:
      settings:
        properties:
          ActivationKey: Space
          InputTime: 300
          ToolbarPosition: Top
          SortByUsageFrequency: true
        name: PowerAccent
        version: 1.0
```

### Example 4 - Fast activation configuration

This example configures for quick accent selection.

```yaml
# poweraccent-fast.dsc.yaml
$schema: https://aka.ms/dsc/schemas/v3/bundled/config/document.json
resources:
  - name: Fast accent activation
    type: Microsoft.PowerToys/PowerAccentSettings
    properties:
      settings:
        properties:
          InputTime: 150
          SortByUsageFrequency: true
        name: PowerAccent
        version: 1.0
```

### Example 5 - Exclude applications

This example excludes specific applications from Power Accent.

```powershell
$config = @{
    settings = @{
        properties = @{
            ExcludedApps = "notepad.exe`nWordPad.exe"
        }
        name = "PowerAccent"
        version = "1.0"
    }
} | ConvertTo-Json -Depth 10 -Compress

PowerToys.DSC.exe set --resource 'settings' --module PowerAccent --input $config
```

## Use cases

### Multilingual content creation

Configure for efficient multilingual typing:

```yaml
resources:
  - name: Multilingual configuration
    type: Microsoft.PowerToys/PowerAccentSettings
    properties:
      settings:
        properties:
          ActivationKey: Space
          SortByUsageFrequency: true
          ShowUnicodeDescription: false
        name: PowerAccent
        version: 1.0
```

### Language learning

Configure for language learning with Unicode descriptions:

```yaml
resources:
  - name: Learning configuration
    type: Microsoft.PowerToys/PowerAccentSettings
    properties:
      settings:
        properties:
          ShowUnicodeDescription: true
          InputTime: 400
        name: PowerAccent
        version: 1.0
```

## See also

- [Settings Resource][01]
- [PowerToys DSC Overview][02]
- [App Module][03] - For enabling/disabling Power Accent utility
- [PowerToys Quick Accent Documentation][04]

<!-- Link reference definitions -->
[01]: ../settings-resource.md
[02]: ../overview.md
[03]: ./App.md
[04]: https://learn.microsoft.com/windows/powertoys/quick-accent
- [PowerToys Quick Accent Documentation](https://learn.microsoft.com/windows/powertoys/quick-accent)
