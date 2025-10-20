# PowerToys Modules

This section contains documentation for individual PowerToys modules, including their architecture, implementation details, and debugging tools.

## Available Modules

| Module | Description |
|--------|-------------|
| [Advanced Paste](advancedpaste.md) | Tool for enhanced clipboard pasting with formatting options |
| [Always on Top](alwaysontop.md) | Tool for pinning windows to stay on top of other windows |
| [Awake](awake.md) | Tool to keep your computer awake without modifying power settings |
| [MCP Server](mcp.md) | Model Context Protocol bridge exposing PowerToys tools to AI agents |
| [Color Picker](colorpicker.md) | Tool for selecting and managing colors from the screen |
| [Command Not Found](commandnotfound.md) | Tool suggesting package installations for missing commands |
| [Crop and Lock](cropandlock.md) | Tool for cropping application windows into smaller windows or thumbnails |
| [Environment Variables](environmentvariables.md) | Tool for managing user and system environment variables |
| [FancyZones](fancyzones.md) ([debugging tools](fancyzones-tools.md)) | Window manager utility for custom window layouts |
| [File Explorer add-ons](fileexploreraddons.md) | Extensions for enhancing Windows File Explorer functionality |
| [File Locksmith](filelocksmith.md) | Tool for finding processes that lock files |
| [Hosts File Editor](hostsfileeditor.md) | Tool for managing the system hosts file |
| [Image Resizer](imageresizer.md) | Tool for quickly resizing images within File Explorer |
| [Keyboard Manager](keyboardmanager/README.md) | Tool for remapping keys and keyboard shortcuts |
| [Mouse Utilities](mouseutils/readme.md) | Collection of tools to enhance mouse and cursor functionality |
| [Mouse Without Borders](mousewithoutborders.md) | Tool for controlling multiple computers with a single mouse and keyboard |
| [NewPlus](newplus.md) | Context menu extension for creating new files in File Explorer |
| [Peek](peek/readme.md) | File preview utility for quick file content viewing |
| [Power Rename](powerrename.md) | Bulk file renaming tool with search and replace functionality |
| [PowerToys Run (deprecation soon)](launcher/readme.md) | Quick application launcher and search utility |
| [Quick Accent](quickaccent.md) | Tool for quickly inserting accented characters and special symbols |
| [Registry Preview](registrypreview.md) | Tool for visualizing and editing Registry files |
| [Screen Ruler](screenruler.md) | Tool for measuring pixel distances and color boundaries on screen |
| [Shortcut Guide](shortcut_guide.md) | Tool for displaying Windows keyboard shortcuts when holding the Windows key |
| [Text Extractor](textextractor.md) | Tool for extracting text from images and screenshots |
| [Workspaces](workspaces.md) | Tool for saving and restoring window layouts for different projects |
| [ZoomIt](zoomit.md) | Screen zoom and annotation tool |

## Adding New Module Documentation

When adding documentation for a new module:

1. Create a dedicated markdown file for the module (e.g., `modulename.md`)
2. If the module has specialized debugging tools, consider creating a separate tools document (e.g., `modulename-tools.md`)
3. Update this index with links to the new documentation
4. Follow the existing documentation structure for consistency
