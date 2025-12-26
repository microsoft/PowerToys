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

## EXIF Metadata Support

PowerRename supports using EXIF metadata from image files in rename patterns. Users can insert metadata fields like `$CAMERA_MAKE`, `$CAMERA_MODEL`, `$ISO`, `$DATE_TAKEN_YYYY`, etc. in the replace field to rename files based on their embedded metadata.

### Supported Image Formats

The metadata extraction feature uses Windows Imaging Component (WIC) and supports the following formats:

| Format | Extensions | Requirements | Metadata Types |
|--------|-----------|--------------|----------------|
| JPEG | `.jpg`, `.jpeg` | Built-in | EXIF, XMP, GPS, IPTC |
| TIFF | `.tif`, `.tiff` | Built-in | EXIF, XMP, GPS, IPTC |
| PNG | `.png` | Built-in | Text chunks |
| HEIC/HEIF | `.heic`, `.heif` | Requires codec installation | EXIF, XMP |
| WebP | `.webp` | Windows 10 1809+ | EXIF, XMP |
| AVIF | `.avif` | Requires codec installation | EXIF, XMP |
| JPEG XR | `.jxr`, `.wdp` | Built-in | EXIF, XMP |
| DNG | `.dng` | Built-in | EXIF, XMP |

### Installing Image Codec Support

Some modern image formats require additional codecs to be installed on Windows:

#### HEIC/HEIF Support (iPhone/iOS images)

1. **Windows 10/11**: Install [HEIF Image Extensions](https://www.microsoft.com/store/productId/9PMMSR1CGPWG) from the Microsoft Store
   - This is a free extension from Microsoft
   - Alternatively, you can install the paid version [HEVC Video Extensions](https://www.microsoft.com/store/productId/9NMZLZ57R3T7) which also includes HEIF support
2. After installation, Windows can decode HEIC/HEIF files for thumbnail generation, preview, and metadata extraction
3. PowerRename will automatically extract EXIF data from HEIC files once the codec is installed

#### AVIF Support

1. **Windows 10/11**: Install [AV1 Video Extension](https://www.microsoft.com/store/productId/9MVZQVXJBQ9V) from the Microsoft Store
   - Free extension from Microsoft
   - Note: HEIF Image Extensions (mentioned above) also provides AVIF decoding support
2. After installation, PowerRename can extract metadata from AVIF files

### How It Works

1. When a user includes metadata patterns (e.g., `$CAMERA_MAKE`) in the replace field, PowerRename checks if the file format supports metadata extraction
2. The file extension is checked against the supported formats list in [`Helpers.cpp::isMetadataUsed()`](/src/modules/powerrename/lib/Helpers.cpp)
3. If supported, [`WICMetadataExtractor`](/src/modules/powerrename/lib/WICMetadataExtractor.cpp) uses WIC to create a decoder for the file
4. Metadata is extracted using WIC's metadata query reader interface
5. The extracted values are cached in [`MetadataResultCache`](/src/modules/powerrename/lib/MetadataResultCache.cpp) for performance
6. The metadata patterns in the filename are replaced with actual values

### Troubleshooting Metadata Extraction

If metadata patterns are not being replaced (e.g., `$CAMERA_MAKE` stays as-is in the filename):

1. **Check file format support**: Verify the file extension is in the supported list above
2. **Install required codecs**: For HEIC/AVIF files, ensure the required extensions are installed from Microsoft Store
3. **Verify metadata exists**: Not all images contain EXIF data. Use Windows File Explorer properties to check if metadata is present
4. **Check file permissions**: Ensure PowerRename has read access to the file
5. **Review debug logs**: In debug builds, WIC decoder errors are logged to the debug output

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