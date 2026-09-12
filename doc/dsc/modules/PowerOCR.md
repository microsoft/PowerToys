---
description: DSC configuration reference for PowerToys PowerOCR module
ms.date:     10/18/2025
ms.topic:    reference
title:       PowerOCR Module
---

# PowerOCR Module

## Synopsis

Manages configuration for the Power OCR (Text Extractor) utility, which
extracts text from images and screen regions.

## Description

The `PowerOCR` module configures PowerToys Power OCR (Text Extractor), a
utility that uses optical character recognition (OCR) to extract text from
any screen region and copy it to the clipboard. It's useful for capturing
text from images, videos, PDFs, or any on-screen content.

## Properties

The PowerOCR module supports the following configurable properties:

### ActivationShortcut

Sets the keyboard shortcut to activate text extraction.

**Type:** object  
**Properties:**

- `win` (boolean) - Windows key modifier
- `ctrl` (boolean) - Ctrl key modifier
- `alt` (boolean) - Alt key modifier
- `shift` (boolean) - Shift key modifier
- `code` (integer) - Virtual key code
- `key` (string) - Key name

**Default:** `Win+Shift+T`

### PreferredLanguage

Sets the preferred language for OCR recognition.

**Type:** string  
**Default:** System language

## Examples

### Example 1 - Configure activation shortcut with direct execution

This example customizes the OCR activation shortcut.

```powershell
$config = @{
    settings = @{
        properties = @{
            ActivationShortcut = @{
                win = $true
                ctrl = $false
                alt = $false
                shift = $true
                code = 84
                key = "T"
            }
        }
        name = "PowerOCR"
        version = "1.0"
    }
} | ConvertTo-Json -Depth 10 -Compress

PowerToys.DSC.exe set --resource 'settings' --module PowerOCR `
    --input $config
```

### Example 2 - Configure language with DSC

This example sets the preferred OCR language.

Save the following configuration as `powerocr-language.dsc.config.yaml`:

```yaml
# yaml-language-server: $schema=https://aka.ms/dsc/schemas/v3/bundled/config/document.vscode.json
$schema: https://aka.ms/dsc/schemas/v3/bundled/config/document.json
resources:
  - name: Configure Power OCR language
    type: Microsoft.PowerToys/PowerOCRSettings
    properties:
      settings:
        properties:
          PreferredLanguage: en-US
        name: PowerOCR
        version: 1.0
```

Apply the configuration with Microsoft DSC:

```bash
dsc config set --file powerocr-language.dsc.config.yaml
```

### Example 3 - Install and configure with WinGet

This example installs PowerToys and configures Power OCR.

Save the following configuration as `powerocr.dsc.config.winget`:

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
  
  - name: Configure Power OCR
    type: Microsoft.PowerToys/PowerOCRSettings
    properties:
      settings:
        properties:
          PreferredLanguage: en-US
        name: PowerOCR
        version: 1.0
```

Apply the configuration with WinGet:

```bash
winget configure powerocr.dsc.config.winget
```

### Example 4 - Multilingual configuration

This example configures for multilingual text extraction.

Save the following configuration as `powerocr-multilingual.dsc.config.yaml`:

```yaml
# yaml-language-server: $schema=https://aka.ms/dsc/schemas/v3/bundled/config/document.vscode.json
$schema: https://aka.ms/dsc/schemas/v3/bundled/config/document.json
resources:
  - name: Multilingual OCR
    type: Microsoft.PowerToys/PowerOCRSettings
    properties:
      settings:
        properties:
          PreferredLanguage: fr-FR
        name: PowerOCR
        version: 1.0
```

Apply the configuration with Microsoft DSC:

```bash
dsc config set --file powerocr-multilingual.dsc.config.yaml
```

## Use cases

### Document digitization

Configure for extracting text from documents:

```yaml
resources:
  - name: Document OCR
    type: Microsoft.PowerToys/PowerOCRSettings
    properties:
      settings:
        properties:
          PreferredLanguage: en-US
        name: PowerOCR
        version: 1.0
```

### International content

Configure for multilingual content extraction:

```yaml
resources:
  - name: Multilingual OCR
    type: Microsoft.PowerToys/PowerOCRSettings
    properties:
      settings:
        properties:
          PreferredLanguage: es-ES
        name: PowerOCR
        version: 1.0
```

## See also

- [Settings Resource][01]
- [PowerToys DSC Overview][02]
- [ZoomIt][03]
- [PowerToys Text Extractor Documentation][04]

<!-- Link reference definitions -->
[01]: ../settings-resource.md
[02]: ../overview.md
[03]: ./ZoomIt.md
[04]: https://learn.microsoft.com/windows/powertoys/text-extractor
