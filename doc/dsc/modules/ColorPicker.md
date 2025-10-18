---
description: DSC configuration reference for PowerToys ColorPicker module
ms.date:     10/18/2025
ms.topic:    reference
title:       ColorPicker Module
---

# ColorPicker Module

## Synopsis

Manages configuration for the Color Picker utility, a system-wide color selection and identification tool.

## Description

The `ColorPicker` module configures PowerToys Color Picker, a utility that allows you to pick colors from anywhere on your screen and copy them to the clipboard in various formats. It's useful for designers, developers, and anyone working with colors.

## Properties

The ColorPicker module supports the following configurable properties:

### ActivationShortcut

Sets the keyboard shortcut to activate the color picker.

**Type:** object  
**Properties:**

- `win` (boolean) - Windows key modifier.
- `ctrl` (boolean) - Ctrl key modifier.
- `alt` (boolean) - Alt key modifier.
- `shift` (boolean) - Shift key modifier.
- `code` (integer) - Virtual key code.
- `key` (string) - Key name.

**Default:** `Win + Shift + C`

### changecursor

Controls whether the cursor changes when the color picker is activated.

**Type:** boolean  
**Default:** `true`

### copiedcolorrepresentation

Sets the default color format copied to the clipboard.

**Type:** string  
**Allowed values:** `"HEX"`, `"RGB"`, `"HSL"`, `"HSV"`, `"CMYK"`, `"HSB"`,
`"HSI"`, `"HWB"`, `"NCol"`  
**Default:** `"HEX"`

### activationaction

Controls the action when the color picker activation shortcut is pressed.

**Type:** integer  
**Allowed values:**

- `0` - Open color picker and show editor
- `1` - Open color picker only
- `2` - Open editor only

**Default:** `0`

### showColorName

Controls whether color names are displayed in the color picker.

**Type:** boolean  
**Default:** `false`

### VisibleColorFormats

Defines which color formats are visible in the picker interface.

**Type:** object with boolean properties for each format:

- `HEX` (boolean)
- `RGB` (boolean)
- `HSL` (boolean)
- `HSV` (boolean)
- `CMYK` (boolean)
- `HSB` (boolean)
- `HSI` (boolean)
- `HWB` (boolean)
- `NCol` (boolean)
- `Decimal` (boolean)

## Examples

### Example 1 - Configure default color format with direct execution

This example sets the default copied color format to RGB.

```powershell
$config = @{
    settings = @{
        properties = @{
            copiedcolorrepresentation = "RGB"
        }
        name = "ColorPicker"
        version = "1.0"
    }
} | ConvertTo-Json -Depth 10 -Compress

PowerToys.DSC.exe set --resource 'settings' --module ColorPicker --input $config
```

### Example 2 - Configure activation behavior with DSC

This example configures the color picker to open directly without the
editor.

```bash
dsc config set --file colorpicker-activation.dsc.yaml
```

```yaml
# colorpicker-activation.dsc.yaml
$schema: https://aka.ms/dsc/schemas/v3/bundled/config/document.json
resources:
  - name: Configure Color Picker activation
    type: Microsoft.PowerToys/ColorPickerSettings
    properties:
      settings:
        properties:
          activationaction: 1
          changecursor: true
          copiedcolorrepresentation: HEX
        name: ColorPicker
        version: 1.0
```

### Example 3 - Install and configure for web development with WinGet

This example installs PowerToys and configures Color Picker for web
developers.

```bash
winget configure winget-colorpicker-webdev.yaml
```

```yaml
# winget-colorpicker-webdev.yaml
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
  
  - name: Configure Color Picker for web development
    type: Microsoft.PowerToys/ColorPickerSettings
    properties:
      settings:
        properties:
          copiedcolorrepresentation: HEX
          showColorName: true
          changecursor: true
          VisibleColorFormats:
            HEX: true
            RGB: true
            HSL: true
            HSV: false
            CMYK: false
        name: ColorPicker
        version: 1.0
```

### Example 4 - Configure visible formats

This example enables only HEX, RGB, and HSL formats.

```bash
dsc config set --file colorpicker-formats.dsc.yaml
```

```yaml
# colorpicker-formats.dsc.yaml
$schema: https://aka.ms/dsc/schemas/v3/bundled/config/document.json
resources:
  - name: Configure visible color formats
    type: Microsoft.PowerToys/ColorPickerSettings
    properties:
      settings:
        properties:
          VisibleColorFormats:
            HEX: true
            RGB: true
            HSL: true
            HSV: false
            CMYK: false
            HSB: false
            HSI: false
            HWB: false
            NCol: false
            Decimal: false
        name: ColorPicker
        version: 1.0
```

### Example 5 - Configure for graphic design

This example configures Color Picker for graphic designers with CMYK support.

```powershell
$config = @{
    settings = @{
        properties = @{
            copiedcolorrepresentation = "CMYK"
            showColorName = $true
            VisibleColorFormats = @{
                HEX = $true
                RGB = $true
                CMYK = $true
                HSL = $true
                HSV = $true
            }
        }
        name = "ColorPicker"
        version = "1.0"
    }
} | ConvertTo-Json -Depth 10 -Compress

PowerToys.DSC.exe set --resource 'settings' --module ColorPicker --input $config
```

### Example 6 - Test configuration

This example tests whether HEX is the default format.

```powershell
$desired = @{
    settings = @{
        properties = @{
            copiedcolorrepresentation = "HEX"
        }
        name = "ColorPicker"
        version = "1.0"
    }
} | ConvertTo-Json -Depth 10 -Compress

$result = PowerToys.DSC.exe test --resource 'settings' --module ColorPicker `
    --input $desired | ConvertFrom-Json

if ($result._inDesiredState) {
    Write-Host "HEX is the default format"
}
```

## Use cases

### Web development

Configure for HTML/CSS color codes:

```yaml
resources:
  - name: Web developer settings
    type: Microsoft.PowerToys/ColorPickerSettings
    properties:
      settings:
        properties:
          copiedcolorrepresentation: HEX
          VisibleColorFormats:
            HEX: true
            RGB: true
            HSL: true
        name: ColorPicker
        version: 1.0
```

### Print design

Configure for CMYK color space:

```yaml
resources:
  - name: Print designer settings
    type: Microsoft.PowerToys/ColorPickerSettings
    properties:
      settings:
        properties:
          copiedcolorrepresentation: CMYK
          showColorName: true
        name: ColorPicker
        version: 1.0
```

## See also

- [Settings Resource][01]
- [PowerToys DSC Overview][02]
- [ImageResizer][03]
- [PowerToys Color Picker Documentation][04]

<!-- Link reference definitions -->
[01]: ../settings-resource.md
[02]: ../overview.md
[03]: ./ImageResizer.md
[04]: https://learn.microsoft.com/windows/powertoys/color-picker
