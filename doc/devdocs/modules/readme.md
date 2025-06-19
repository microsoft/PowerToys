# PowerToys Modules

This section contains documentation for individual PowerToys modules, including their architecture, implementation details, and debugging tools.

## Available Modules

| Module | Description |
|--------|-------------|
| [Environment Variables](environmentvariables.md) | Tool for managing user and system environment variables |
| [FancyZones](fancyzones.md) ([debugging tools](fancyzones-tools.md)) | Window manager utility for custom window layouts |
| [File Locksmith](filelocksmith.md) | Tool for finding processes that lock files |
| [Hosts File Editor](hostsfileeditor.md) | Tool for managing the system hosts file |
| [Keyboard Manager](keyboardmanager/README.md) | Tool for remapping keys and keyboard shortcuts |
| [Mouse Utilities](mouseutils/readme.md) | Collection of tools to enhance mouse and cursor functionality |
| [NewPlus](newplus.md) | Context menu extension for creating new files in File Explorer |
| [Quick Accent](quickaccent.md) | Tool for quickly inserting accented characters and special symbols |
| [Registry Preview](registrypreview.md) | Tool for visualizing and editing Registry files |
| [Screen Ruler](screenruler.md) | Tool for measuring pixel distances and color boundaries on screen |
| [Shortcut Guide](shortcut_guide.md) | Tool for displaying Windows keyboard shortcuts when holding the Windows key |
| [ZoomIt](zoomit.md) | Screen zoom and annotation tool |

## Adding New Module Documentation

When adding documentation for a new module:

1. Create a dedicated markdown file for the module (e.g., `modulename.md`)
2. If the module has specialized debugging tools, consider creating a separate tools document (e.g., `modulename-tools.md`)
3. Update this index with links to the new documentation
4. Follow the existing documentation structure for consistency
