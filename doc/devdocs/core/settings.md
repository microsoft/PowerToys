# PowerToys Settings System

PowerToys provides a comprehensive settings system that allows users to configure various aspects of the application and its modules. This document provides an overview of the settings system architecture and links to more detailed documentation.

## Overview

The PowerToys settings system is built on a Windows App SDK WinUI3 .NET Unpackaged desktop application. It follows the MVVM (Model-View-ViewModel) architectural pattern to separate the user interface from the business logic.

The settings system is responsible for:

- Providing a user interface to configure PowerToys modules
- Storing and retrieving user preferences
- Communicating configuration changes to the runner and modules
- Providing a consistent experience across all PowerToys modules

## Settings Files

PowerToys settings are stored in JSON files in the following locations:

- Main settings: `%LOCALAPPDATA%\Microsoft\PowerToys\settings.json`
- Module-specific settings: `%LOCALAPPDATA%\Microsoft\PowerToys\<module_name>\settings.json`

### General Settings File Structure

The main settings file contains general PowerToys settings and a list of enabled/disabled modules:

```json
{
  "general": {
    "startup": true,
    "enabled": {
      "Fancy Zones": true,
      "Image Resizer": true,
      "Keyboard Manager": false,
      // ...other modules
    }
  },
  "version": "0.89.0"
}
```

### Module Settings File Structure

Each module can have its own settings file with module-specific configurations:

```json
{
  "properties": {
    "fancyzones_shiftDrag": {
      "value": true
    },
    "fancyzones_mouseSwitch": {
      "value": false
    },
    // ...other module-specific settings
  },
  "version": "1.0"
}
```

## Detailed Documentation

For more detailed documentation on the settings system, please refer to the following resources:

- [Settings v2 Documentation](/doc/devdocs/settingsv2/readme.md) - Comprehensive guide to the settings system
- [UI Architecture](/doc/devdocs/settingsv2/ui-architecture.md) - Details on the UI components and their relationships
- [ViewModels](/doc/devdocs/settingsv2/viewmodels.md) - Information about the data models
- [Settings Utilities](/doc/devdocs/settingsv2/settings-utilities.md) - Settings file operations and utilities
- [IPC Communication](/doc/devdocs/settingsv2/runner-ipc.md) - How settings communicate with the runner
- [Module Communication](/doc/devdocs/settingsv2/communication-with-modules.md) - How settings changes are propagated to modules
- [Custom HotKey Control](/doc/devdocs/settingsv2/hotkeycontrol.md) - Documentation on the custom hotkey control
- [Settings Implementation](/doc/devdocs/settingsv2/settings-implementation.md) - How settings are implemented in C++ and C# modules
- [Group Policy Integration](/doc/devdocs/settingsv2/gpo-integration.md) - How settings integrate with Group Policy
- [DSC Configuration](/doc/devdocs/settingsv2/dsc-configure.md) - How to use `winget configure` with PowerToys
