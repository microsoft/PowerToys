# Registry Preview Module


[Public overview - Microsoft Learn](https://learn.microsoft.com/en-us/windows/powertoys/registry-preview)

## Quick Links

[All Issues](https://github.com/microsoft/PowerToys/issues?q=is%3Aopen%20label%3A%22Product-Registry%20Preview%22)<br>
[Bugs](https://github.com/microsoft/PowerToys/issues?q=is%3Aopen%20label%3AIssue-Bug%20label%3A%22Product-Registry%20Preview%22)<br>
[Pull Requests](https://github.com/microsoft/PowerToys/pulls?q=is%3Apr+is%3Aopen+label%3A%22Product-Registry+Preview%22)
[CheckList](https://github.com/microsoft/PowerToys/blob/releaseChecklist/doc/releases/tests-checklist-template.md?plain=1#L641)

## Overview

Registry Preview simplifies the process of visualizing and editing complex Windows Registry files. It provides a powerful interface to preview, edit, and write changes to the Windows Registry. The module leverages the [Monaco Editor](../common/monaco-editor.md) to provide features like syntax highlighting and line numbering for registry files.

## Technical Architecture

Registry Preview is built using WinUI 3 with the [Monaco Editor](../common/monaco-editor.md) embedded for text editing capabilities. Monaco was originally designed for web environments but has been integrated into this desktop application to leverage its powerful editing features.

The module consists of several key components:

1. **Main Windows Interface** - Handles the UI interactions, window messaging, and resource loading
2. **Monaco Editor Integration** - Embeds the Monaco web-based editor into WinUI 3 (see [Monaco Editor documentation](../common/monaco-editor.md) for details)
3. **Registry Parser** - Parses registry files and builds a tree structure for visualization
4. **Editor Control** - Manages the editing capabilities and syntax highlighting

## Code Structure

The Registry Preview module is organized into the following projects:

- **RegistryPreview** - Main window implementation, including Windows message handling, resource loading, and service injection
- **RegistryPreviewUILib** - UI implementation details and backend logic
- **RegistryPreviewExt** - Project configuration and setup
- **RegistryPreview.FuzzTests** - Fuzzing tests for the module

Key files and components:

1. **MonacoEditorControl** - Handles the embedding of [Monaco](../common/monaco-editor.md) into WinUI 3 and sets up the WebView container
2. **MainWindow** - Manages all event handling in one place
3. **Utilities** - Contains shared helper methods and utility classes

## Main Functions

- **MonacoEditorControl**: Controls editing in Monaco
- **GetRuntimeMonacoDirectory**: Gets the current directory path
- **OpenRegistryFile**: Opens and processes a registry file (first-time open)
- **RefreshRegistryFile**: Re-opens and processes an already opened file
- **ParseRegistryFile**: Parses text from the editor
- **AddTextToTree**: Creates TreeView nodes from registry keys
- **ShowMessageBox**: Wrapper method for displaying message boxes

## Debugging Registry Preview

### Setup Debugging Environment

1. Set the PowerToys Runner as the parent process
2. Set the RegistryPreviewUILib project as the child process for debugging
3. Use the PowerToys Development Utility tool to configure debugging

### Debugging Tips

1. The main application logic is in the RegistryPreviewUILib project
2. Monaco-related issues may require debugging the WebView component (see [Monaco Editor documentation](../common/monaco-editor.md) for details)
3. For parsing issues, add breakpoints in the ParseRegistryFile method
4. UI issues are typically handled in the main RegistryPreview project

## UI Automation

Currently, Registry Preview does not have UI automation tests implemented. This is a potential area for future development.

## Recent Updates

Registry Preview has received community contributions, including:
- UI improvements
- New buttons and functionality
- Data preview enhancements
- Save button improvements

## Future Considerations

- Adding UI automation tests
- Further [Monaco editor](../common/monaco-editor.md) updates
- Enhanced registry parsing capabilities
- Improved visualization options
