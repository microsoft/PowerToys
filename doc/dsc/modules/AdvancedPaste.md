---
description: DSC configuration reference for PowerToys AdvancedPaste module
ms.date:     10/18/2025
ms.topic:    reference
title:       AdvancedPaste Module
---

# AdvancedPaste Module

## Synopsis

Manages configuration for the Advanced Paste utility, which provides advanced clipboard operations and custom paste formats.

## Description

The `AdvancedPaste` module configures PowerToys Advanced Paste, a utility that extends clipboard functionality with AI-powered transformations, custom formats, and advanced paste options. It allows you to paste clipboard content with transformations like plain text conversion, markdown formatting, JSON formatting, and AI-based text processing.

## Properties

The AdvancedPaste module supports the following configurable properties:

### IsAdvancedAIEnabled

Controls whether AI-powered paste transformations are enabled.

**Type:** boolean  
**Default:** `false`  
**Description:** Enables AI-based clipboard transformations such as summarization, translation, and content reformatting.

### PasteAsPlainTextHotkey

Sets the keyboard shortcut for pasting as plain text.

**Type:** object  
**Properties:**

- `win` (boolean) - Windows key modifier.
- `ctrl` (boolean) - Ctrl key modifier.
- `alt` (boolean) - Alt key modifier.
- `shift` (boolean) - Shift key modifier.
- `code` (integer) - Virtual key code.
- `key` (string) - Key name.

**Default:** `Ctrl + Win + V`

### PasteAsMarkdownHotkey

Sets the keyboard shortcut for pasting as markdown.

**Type:** object (same structure as PasteAsPlainTextHotkey)  
**Default:** `Ctrl + Win + Shift + V`

### PasteAsJsonHotkey

Sets the keyboard shortcut for pasting as JSON.

**Type:** object (same structure as PasteAsPlainTextHotkey)

### ShowCustomPreview

Controls whether a preview window is shown before pasting custom formats.

**Type:** boolean  
**Default:** `true`

### CloseAfterLosingFocus

Controls whether the Advanced Paste window closes when it loses focus.

**Type:** boolean  
**Default:** `false`

## Examples

### Example 1 - Enable AI features with direct execution

This example enables AI-powered paste transformations.

```powershell
$config = @{
    settings = @{
        properties = @{
            IsAdvancedAIEnabled = $true
        }
        name = "AdvancedPaste"
        version = "1.0"
    }
} | ConvertTo-Json -Depth 10 -Compress

PowerToys.DSC.exe set --resource 'settings' --module AdvancedPaste --input $config
```

### Example 2 - Configure paste hotkeys with DSC

This example customizes keyboard shortcuts for different paste formats.

```yaml
# advancedpaste-hotkeys.dsc.yaml
$schema: https://aka.ms/dsc/schemas/v3/bundled/config/document.json
resources:
  - name: Configure Advanced Paste hotkeys
    type: Microsoft.PowerToys/AdvancedPasteSettings
    properties:
      settings:
        properties:
          PasteAsPlainTextHotkey:
            win: true
            ctrl: true
            alt: false
            shift: false
            code: 86
            key: V
          PasteAsMarkdownHotkey:
            win: true
            ctrl: true
            alt: false
            shift: true
            code: 86
            key: V
        name: AdvancedPaste
        version: 1.0
```

### Example 3 - Install and configure with WinGet

This example installs PowerToys and configures Advanced Paste with AI enabled.

```yaml
# winget-advancedpaste.yaml
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
  
  - name: Configure Advanced Paste
    type: Microsoft.PowerToys/AdvancedPasteSettings
    properties:
      settings:
        properties:
          IsAdvancedAIEnabled: true
          ShowCustomPreview: true
          CloseAfterLosingFocus: true
        name: AdvancedPaste
        version: 1.0
```

### Example 4 - Enable with custom preview settings

This example configures preview behavior for custom paste formats.

```yaml
# advancedpaste-preview.dsc.yaml
$schema: https://aka.ms/dsc/schemas/v3/bundled/config/document.json
resources:
  - name: Configure preview settings
    type: Microsoft.PowerToys/AdvancedPasteSettings
    properties:
      settings:
        properties:
          ShowCustomPreview: true
          CloseAfterLosingFocus: false
        name: AdvancedPaste
        version: 1.0
```

### Example 5 - Test AI enablement

This example tests whether AI features are enabled.

```powershell
$desired = @{
    settings = @{
        properties = @{
            IsAdvancedAIEnabled = $true
        }
        name = "AdvancedPaste"
        version = "1.0"
    }
} | ConvertTo-Json -Depth 10 -Compress

$result = PowerToys.DSC.exe test --resource 'settings' --module AdvancedPaste --input $desired | ConvertFrom-Json

if ($result._inDesiredState) {
    Write-Host "AI features are enabled"
} else {
    Write-Host "AI features need to be enabled"
}
```

## Use cases

### Development workflow

Enable AI transformations for code snippets and documentation:

```yaml
resources:
  - name: Developer paste settings
    type: Microsoft.PowerToys/AdvancedPasteSettings
    properties:
      settings:
        properties:
          IsAdvancedAIEnabled: true
          ShowCustomPreview: true
        name: AdvancedPaste
        version: 1.0
```

### Content creation

Configure for markdown and formatted text operations:

```yaml
resources:
  - name: Content creator settings
    type: Microsoft.PowerToys/AdvancedPasteSettings
    properties:
      settings:
        properties:
          ShowCustomPreview: true
          CloseAfterLosingFocus: false
        name: AdvancedPaste
        version: 1.0
```

## See also

- [Settings Resource][01]
- [PowerToys DSC Overview][02]
- [App Module][03] - For enabling/disabling Advanced Paste utility
- [PowerToys Advanced Paste Documentation][04]

<!-- Link reference definitions -->
[01]: ../settings-resource.md
[02]: ../overview.md
[03]: ./App.md
[04]: https://learn.microsoft.com/windows/powertoys/advanced-paste
