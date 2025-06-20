# Image Resizer

[Public overview - Microsoft Learn](https://learn.microsoft.com/en-us/windows/powertoys/image-resizer)

## Quick Links

[All Issues](https://github.com/microsoft/PowerToys/issues?q=is%3Aopen%20label%3A%22Product-Image%20Resizer%22)<br>
[Bugs](https://github.com/microsoft/PowerToys/issues?q=is%3Aopen%20label%3AIssue-Bug%20label%3A%22Product-Image%20Resizer%22)<br>
[Pull Requests](https://github.com/microsoft/PowerToys/pulls?q=is%3Apr+is%3Aopen+label%3A%22Product-Image+Resizer%22)

Image Resizer is a Windows shell extension that enables batch resizing of images directly from File Explorer.

## Overview

Image Resizer provides a convenient way to resize images without opening a dedicated image editing application. It is accessible through the Windows context menu and allows users to:
- Resize single or multiple images at once
- Choose from predefined sizing presets or enter custom dimensions
- Maintain or modify aspect ratios
- Create copies or overwrite original files
- Apply custom filename formats to resized images
- Select different encoding quality settings

## Architecture

Image Resizer consists of multiple components:
- Shell Extension DLL (context menu integration)
- WinUI 3 UI application
- Core image processing library

### Technology Stack
- C++/WinRT
- WPF (UI components)
- Windows Imaging Component (WIC) for image processing
- COM for shell integration

## Context Menu Integration

Image Resizer integrates with the Windows context menu following the [PowerToys Context Menu Handlers](../common/context-menus.md) pattern. It uses a dual registration approach to ensure compatibility with both Windows 10 and Windows 11.

### Registration Process

The context menu integration follows the same pattern as PowerRename, using:
- A traditional shell extension for Windows 10
- A sparse MSIX package for Windows 11 context menus

For more details on the implementation approach, see the [Dual Registration section](../common/context-menus.md#1-dual-registration-eg-imageresizer-powerrename) in the context menu documentation.

### Context Menu Appearance Logic

Image Resizer dynamically determines when to show the context menu option:
- `AppxManifest.xml` registers the extension for all file types (`Type="*"`)
- The shell extension checks if the selected files are images using `AssocGetPerceivedType()`
- The menu appears only for image files (returns `ECS_ENABLED`), otherwise it remains hidden (returns `ECS_HIDDEN`)

This approach provides flexibility to support additional file types by modifying only the detection logic without changing the system-level registration.

## UI Implementation

Image Resizer uses WPF for its user interface, as evidenced by the App.xaml.cs file. The UI allows users to:
- Select from predefined size presets or enter custom dimensions
- Configure filename format for the resized images
- Set encoding quality and format options
- Choose whether to replace or create copies of the original files

From the App.xaml.cs file, we can see that the application:
- Supports localization through `LanguageHelper.LoadLanguage()`
- Processes command line arguments via `ResizeBatch.FromCommandLine()`
- Uses a view model pattern with `MainViewModel`
- Respects Group Policy settings via `GPOWrapper.GetConfiguredImageResizerEnabledValue()`

## Debugging

### Debugging the Context Menu

See the [Debugging Context Menu Handlers](../common/context-menus.md#debugging-context-menu-handlers) section for general guidance on debugging PowerToys context menu extensions.

For Image Resizer specifically, there are several approaches:

#### Option 1: Manual Registration via Registry

1. Create a registry file (e.g., `register.reg`) with the following content:
```
Windows Registry Editor Version 5.00

[HKEY_CLASSES_ROOT\CLSID\{51B4D7E5-7568-4234-B4BB-47FB3C016A69}]
@="PowerToys Image Resizer Extension"

[HKEY_CLASSES_ROOT\CLSID\{51B4D7E5-7568-4234-B4BB-47FB3C016A69}\InprocServer32]
@="D:\\PowerToys\\x64\\Debug\\PowerToys.ImageResizerExt.dll"
"ThreadingModel"="Apartment"

[HKEY_CURRENT_USER\Software\Classes\SystemFileAssociations\.png\ShellEx\ContextMenuHandlers\ImageResizer]
@="{51B4D7E5-7568-4234-B4BB-47FB3C016A69}"
```

2. Import the registry file:
```
reg import register.reg
```

3. Restart Explorer to apply changes:
```
taskkill /f /im explorer.exe && start explorer.exe
```

4. Attach the debugger to `explorer.exe`

5. Add breakpoints to relevant code in the Image Resizer shell extension

#### Option 2: Using regsvr32

1. Register the shell extension DLL:
```
regsvr32 "D:\PowerToys\x64\Debug\PowerToys.ImageResizerExt.dll"
```

2. Restart Explorer and attach the debugger as in Option 1

### Common Issues

- Context menu not appearing: 
  - Ensure the extension is properly registered
  - Verify you're right-clicking on supported image files
  - Restart Explorer to clear context menu cache
  
- For Windows 11, check AppX package registration:
  - Use `get-appxpackage -Name *imageresizer*` to verify installation
  - Use `Remove-AppxPackage` to remove problematic registrations

- Missing UI or processing failures:
  - Check Event Viewer for application errors
  - Verify file permissions for both source and destination folders
