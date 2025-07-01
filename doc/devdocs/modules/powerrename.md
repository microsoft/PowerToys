# PowerRename

[Public overview - Microsoft Learn](https://learn.microsoft.com/en-us/windows/powertoys/powerrename)

## Quick Links

[All Issues](https://github.com/microsoft/PowerToys/issues?q=is%3Aopen%20label%3AProduct-PowerRename)<br>
[Bugs](https://github.com/microsoft/PowerToys/issues?q=is%3Aopen%20label%3AIssue-Bug%20label%3AProduct-PowerRename)<br>
[Pull Requests](https://github.com/microsoft/PowerToys/pulls?q=is%3Apr+is%3Aopen+label%3AProduct-PowerRename)

PowerRename is a Windows shell extension that enables batch renaming of files using search and replace or regular expressions.

## Overview

PowerRename provides a powerful and flexible way to rename files in File Explorer. It is accessible through the Windows context menu and allows users to:
- Preview changes before applying them
- Use search and replace with regular expressions
- Filter items by type (files or folders)
- Apply case-sensitive or case-insensitive matching
- Save and reuse recent search/replace patterns

## Architecture

PowerRename consists of multiple components:
- Shell Extension DLL (context menu integration)
- WinUI 3 UI application
- Core renaming library

### Technology Stack
- C++/WinRT
- WinUI 3
- COM for shell integration

## Context Menu Integration

PowerRename integrates with the Windows context menu following the [PowerToys Context Menu Handlers](../common/context-menus.md) pattern. It uses a dual registration approach to ensure compatibility with both Windows 10 and Windows 11.

### Registration Process

The context menu registration entry point is in `PowerRenameExt/dllmain.cpp::enable`, which registers:
- A traditional shell extension for Windows 10
- A sparse MSIX package for Windows 11 context menus

For more details on the implementation approach, see the [Dual Registration section](../common/context-menus.md#1-dual-registration-eg-imageresizer-powerrename) in the context menu documentation.

## Code Components

### [`dllmain.cpp`](/src/modules/powerrename/dll/dllmain.cpp)
Contains the DLL entry point and module activation/deactivation code. The key function `RunPowerRename` is called when the context menu option is invoked, which launches the PowerRenameUI.

### [`PowerRenameExt.cpp`](/src/modules/powerrename/dll/PowerRenameExt.cpp)
Implements the shell extension COM interfaces required for context menu integration, including:
- `IShellExtInit` for initialization 
- `IContextMenu` for traditional context menu support
- `IExplorerCommand` for Windows 11 context menu support

### [`Helpers.cpp`](/src/modules/powerrename/lib/Helpers.cpp)
Utility functions used throughout the PowerRename module, including file system operations and string manipulation.

### [`PowerRenameItem.cpp`](/src/modules/powerrename/lib/PowerRenameItem.cpp)
Represents a single item (file or folder) to be renamed. Tracks original and new names and maintains state.

### [`PowerRenameManager.cpp`](/src/modules/powerrename/lib/PowerRenameManager.cpp)
Manages the collection of items to be renamed and coordinates the rename operation.

### [`PowerRenameRegEx.cpp`](/src/modules/powerrename/lib/PowerRenameRegEx.cpp)
Implements the regular expression search and replace functionality used for renaming.

### [`Settings.cpp`](/src/modules/powerrename/lib/Settings.cpp)
Manages user preferences and settings for the PowerRename module.

### [`trace.cpp`](/src/modules/powerrename/lib/trace.cpp)
Implements telemetry and logging functionality.

## UI Implementation

PowerRename uses WinUI 3 for its user interface. The UI allows users to:
- Enter search and replace patterns
- Preview rename results in real-time
- Access previous search/replace patterns via MRU (Most Recently Used) lists
- Configure various options

### Key UI Components

- Search/Replace input fields with x:Bind to `SearchMRU`/`ReplaceMRU` collections
- Preview list showing original and new filenames
- Settings panel for configuring rename options
- Event handling for `SearchReplaceChanged` to update the preview in real-time

## Debugging

### Debugging the Context Menu

See the [Debugging Context Menu Handlers](../common/context-menus.md#debugging-context-menu-handlers) section for general guidance on debugging PowerToys context menu extensions.

### Debugging the UI

To debug the PowerRename UI:

1. Add file paths manually in `\src\modules\powerrename\PowerRenameUILib\PowerRenameXAML\App.xaml.cpp`
2. Set the PowerRenameUI project as the startup project
3. Run in debug mode to test with the manually specified files

### Common Issues

- Context menu not appearing: Ensure the extension is properly registered and Explorer has been restarted
- UI not launching: Check Event Viewer for errors related to WinUI 3 application activation
- Rename operations failing: Verify file permissions and check for locked files