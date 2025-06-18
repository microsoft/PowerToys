# PowerToys Modules

This section contains documentation for individual PowerToys modules, including their architecture, implementation details, and debugging tools.

## Available Modules

| Module | Description | Documentation |
|--------|-------------|---------------|
| Environment Variables | Tool for managing user and system environment variables | [Architecture & Implementation](environmentvariables.md) |
| FancyZones | Window manager utility for custom window layouts | [Architecture & Implementation](fancyzones.md), [Debugging Tools](fancyzones-tools.md) |
| Keyboard Manager | Tool for remapping keys and keyboard shortcuts | [Documentation](keyboardmanager/README.md) |
| NewPlus | Context menu extension for creating new files in File Explorer | [Architecture & Implementation](newplus.md) |
| Quick Accent | Tool for quickly inserting accented characters and special symbols | [Architecture & Implementation](quickaccent.md) |
| Registry Preview | Tool for visualizing and editing Registry files | [Architecture & Implementation](registrypreview.md) |
| Screen Ruler | Tool for measuring pixel distances and color boundaries on screen | [Architecture & Implementation](screenruler.md) |
| Shortcut Guide | Tool for displaying Windows keyboard shortcuts when holding the Windows key | [Architecture & Implementation](shortcut_guide.md) |
| ZoomIt | Screen zoom and annotation tool | [Architecture & Implementation](zoomit.md) |

## Adding New Module Documentation

When adding documentation for a new module:

1. Create a dedicated markdown file for the module (e.g., `modulename.md`)
2. If the module has specialized debugging tools, consider creating a separate tools document (e.g., `modulename-tools.md`)
3. Update this index with links to the new documentation
4. Follow the existing documentation structure for consistency
