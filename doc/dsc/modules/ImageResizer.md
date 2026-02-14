---
description: DSC configuration reference for PowerToys ImageResizer module
ms.date:     10/18/2025
ms.topic:    reference
title:       ImageResizer Module
---

# ImageResizer Module

## Synopsis

Manages configuration for the Image Resizer utility, which provides quick
image resizing from the Windows Explorer context menu.

## Description

The `ImageResizer` module configures PowerToys Image Resizer, a Windows shell
extension that allows you to resize one or multiple images directly from the
File Explorer context menu. It supports custom size presets and various
resize options.

## Properties

The ImageResizer module supports the following configurable properties:

### ImageResizerSizes

Defines the preset sizes available in the Image Resizer interface.

**Type:** array of objects  
**Object properties:**

- `Name` (string) - Display name for the preset
- `Width` (integer) - Width value
- `Height` (integer) - Height value
- `Unit` (string) - Unit of measurement: `"Pixel"`, `"Percent"`, `"Centimeter"`, `"Inch"`
- `Fit` (string) - Resize mode: `"Fit"`, `"Fill"`, `"Stretch"`

### ImageresizerSelectedSizeIndex

Sets the default selected size preset (0-based index).

**Type:** integer  
**Default:** `0`

### ImageresizerShrinkOnly

Controls whether images are only resized if they're larger than the target size.

**Type:** boolean  
**Default:** `false`

### ImageresizerReplace

Controls whether resized images replace the original files.

**Type:** boolean  
**Default:** `false`

### ImageresizerIgnoreOrientation

Controls whether EXIF orientation data is ignored.

**Type:** boolean  
**Default:** `true`

### ImageresizerJpegQualityLevel

Sets the JPEG quality level for resized images (1-100).

**Type:** integer  
**Range:** `1` to `100`  
**Default:** `90`

### ImageresizerPngInterlaceOption

Sets the PNG interlace option.

**Type:** integer  
**Allowed values:**

- `0` - No interlacing
- `1` - Interlaced

**Default:** `0`

### ImageresizerTiffCompressOption

Sets the TIFF compression option.

**Type:** integer  
**Allowed values:**

- `0` - No compression
- `1` - LZW compression
- `2` - ZIP compression

**Default:** `0`

### ImageresizerFileName

Sets the naming pattern for resized images.

**Type:** string  
**Default:** `"%1 (%2)"`  
**Placeholders:**

- `%1` - Original filename
- `%2` - Size name
- `%3` - Selected width
- `%4` - Selected height
- `%5` - Actual width
- `%6` - Actual height

### ImageresizerKeepDateModified

Controls whether the original file's modified date is preserved.

**Type:** boolean  
**Default:** `false`

### ImageresizerFallbackEncoder

Sets the fallback encoder for unsupported formats.

**Type:** string  
**Allowed values:** `"png"`, `"jpg"`, `"bmp"`, `"tiff"`, `"gif"`  
**Default:** `"png"`

## Examples

### Example 1 - Configure custom size presets with direct execution

This example defines custom image resize presets.

```powershell
$config = @{
    settings = @{
        properties = @{
            ImageResizerSizes = @(
                @{
                    Name = "Small"
                    Width = 640
                    Height = 480
                    Unit = "Pixel"
                    Fit = "Fit"
                },
                @{
                    Name = "Medium"
                    Width = 1280
                    Height = 720
                    Unit = "Pixel"
                    Fit = "Fit"
                },
                @{
                    Name = "Large"
                    Width = 1920
                    Height = 1080
                    Unit = "Pixel"
                    Fit = "Fit"
                }
            )
        }
        name = "ImageResizer"
        version = "1.0"
    }
} | ConvertTo-Json -Depth 10 -Compress

PowerToys.DSC.exe set --resource 'settings' --module ImageResizer `
    --input $config
```

### Example 2 - Configure quality settings with DSC

This example configures image quality and format options.

```bash
dsc config set --file imageresizer-quality.dsc.yaml
```

```yaml
# imageresizer-quality.dsc.yaml
$schema: https://aka.ms/dsc/schemas/v3/bundled/config/document.json
resources:
  - name: Configure Image Resizer quality
    type: Microsoft.PowerToys/ImageResizerSettings
    properties:
      settings:
        properties:
          ImageresizerJpegQualityLevel: 95
          ImageresizerShrinkOnly: true
          ImageresizerKeepDateModified: true
        name: ImageResizer
        version: 1.0
```

### Example 3 - Install and configure with WinGet

This example installs PowerToys and configures Image Resizer with
web-optimized presets.

```bash
winget configure winget-imageresizer.yaml
```

```yaml
# winget-imageresizer.yaml
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
  
  - name: Configure Image Resizer
    type: Microsoft.PowerToys/ImageResizerSettings
    properties:
      settings:
        properties:
          ImageResizerSizes:
            - Name: Thumbnail
              Width: 320
              Height: 240
              Unit: Pixel
              Fit: Fit
            - Name: Web Small
              Width: 800
              Height: 600
              Unit: Pixel
              Fit: Fit
            - Name: Web Large
              Width: 1920
              Height: 1080
              Unit: Pixel
              Fit: Fit
          ImageresizerJpegQualityLevel: 85
          ImageresizerFileName: "%1_resized_%2"
        name: ImageResizer
        version: 1.0
```

### Example 4 - Photography workflow

This example configures for photography with high quality and metadata
preservation.

```bash
dsc config set --file imageresizer-photo.dsc.yaml
```

```yaml
# imageresizer-photography.dsc.yaml
$schema: https://aka.ms/dsc/schemas/v3/bundled/config/document.json
resources:
  - name: Photography configuration
    type: Microsoft.PowerToys/ImageResizerSettings
    properties:
      settings:
        properties:
          ImageresizerJpegQualityLevel: 100
          ImageresizerKeepDateModified: true
          ImageresizerIgnoreOrientation: false
          ImageresizerShrinkOnly: true
        name: ImageResizer
        version: 1.0
```

### Example 5 - Social media presets

This example defines presets for social media platforms.

```powershell
$config = @{
    settings = @{
        properties = @{
            ImageResizerSizes = @(
                @{ Name = "Instagram Square"; Width = 1080; Height = 1080; Unit = "Pixel"; Fit = "Fill" },
                @{ Name = "Instagram Portrait"; Width = 1080; Height = 1350; Unit = "Pixel"; Fit = "Fill" },
                @{ Name = "Facebook Cover"; Width = 820; Height = 312; Unit = "Pixel"; Fit = "Fill" },
                @{ Name = "Twitter Header"; Width = 1500; Height = 500; Unit = "Pixel"; Fit = "Fill" }
            )
            ImageresizerJpegQualityLevel = 90
        }
        name = "ImageResizer"
        version = "1.0"
    }
} | ConvertTo-Json -Depth 10 -Compress

PowerToys.DSC.exe set --resource 'settings' --module ImageResizer `
    --input $config
```

## Use cases

### Web development

Configure for web-optimized images:

```yaml
resources:
  - name: Web optimization
    type: Microsoft.PowerToys/ImageResizerSettings
    properties:
      settings:
        properties:
          ImageresizerJpegQualityLevel: 85
          ImageresizerShrinkOnly: true
        name: ImageResizer
        version: 1.0
```

### Content creation

Configure for social media and content platforms:

```yaml
resources:
  - name: Content creation
    type: Microsoft.PowerToys/ImageResizerSettings
    properties:
      settings:
        properties:
          ImageResizerSizes:
            - Name: HD
              Width: 1920
              Height: 1080
              Unit: Pixel
              Fit: Fit
          ImageresizerJpegQualityLevel: 90
        name: ImageResizer
        version: 1.0
```

## See also

- [Settings Resource][01]
- [PowerToys DSC Overview][02]
- [MeasureTool][03]
- [PowerToys Image Resizer Documentation][04]

<!-- Link reference definitions -->
[01]: ../settings-resource.md
[02]: ../overview.md
[03]: ./MeasureTool.md
[04]: https://learn.microsoft.com/windows/powertoys/image-resizer
