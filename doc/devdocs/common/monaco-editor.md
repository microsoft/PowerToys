# Monaco Editor in PowerToys

## Overview

Monaco is the text editor that powers Visual Studio Code. In PowerToys, Monaco is integrated as a component to provide advanced text editing capabilities with features like syntax highlighting, line numbering, and intelligent code editing.

## Where Monaco is Used in PowerToys

Monaco is primarily used in:
- Registry Preview module - For editing registry files
- File Preview handlers - For syntax highlighting when previewing code files
- Peek module - For preview a file

## Technical Implementation

Monaco is embedded into PowerToys' WinUI 3 applications using WebView2. This integration allows PowerToys to leverage Monaco's web-based capabilities within desktop applications.

### Directory Structure

The Monaco editor files are located in the relevant module directories. For example, in Registry Preview, Monaco files are bundled with the application resources.

## Versioning and Updates

### Current Version

The current Monaco version can be found in the `loader.js` file, specifically in the variable named `versionMonaco`.

### Update Process

Updating Monaco requires several steps:

1. Download the latest version of Monaco
2. Replace/override the main folder with the new version
3. Generate the new Monaco language JSON file
4. Override the existing JSON file

For detailed step-by-step instructions, see the [FilePreviewCommon documentation](FilePreviewCommon.md#update-monaco-editor).

#### Estimated Time for Update

The Monaco update process typically takes approximately 30 minutes.

#### Reference PRs

When updating Monaco, you can refer to previous Monaco update PRs as examples, as they mostly involve copy-pasting the Monaco source code with minor adjustments.

## Customizing Monaco

### Adding New Language Definitions

Monaco can be customized to support new language definitions for syntax highlighting:

1. Identify the language you want to add
2. Create or modify the appropriate language definition files
3. Update the Monaco configuration to recognize the new language

For detailed instructions on adding language definitions, see the [FilePreviewCommon documentation](FilePreviewCommon.md#add-a-new-language-definition).

### Adding File Extensions to Existing Languages

To make Monaco handle additional file extensions using existing language definitions:

1. Locate the language mapping configuration
2. Add the new file extension to the appropriate language entry
3. Update the file extension registry

For detailed instructions on adding file extensions, see the [FilePreviewCommon documentation](FilePreviewCommon.md#add-a-new-file-extension-to-an-existing-language).

Example: If Monaco processes TXT files and you want it to preview LOG files the same way, you can add LOG extensions to the TXT language definition.

## Installer Handling

Monaco source files are managed via a script (`Generate-Monaco-wxs.ps1`) that:
1. Automatically generates the installer manifest to include all Monaco files
2. Avoids manually listing all Monaco files in the installer configuration

This approach simplifies maintenance and updates of the Monaco editor within PowerToys.
