# PowerToys Settings System

## Overview

PowerToys uses a JSON-based settings system to store user preferences and module configurations. The settings system is designed to be:

- Persistent across PowerToys sessions
- Easily modifiable by both the user and the application
- Accessible to all PowerToys modules
- Compatible with Group Policy settings

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

## Settings Implementation

### C++ Settings

For C++ modules, the settings system is implemented in the following files:

- `settings_objects.h` and `settings_objects.cpp`: Define the basic settings objects
- `settings_helpers.h` and `settings_helpers.cpp`: Helper functions for reading/writing settings
- `settings_manager.h` and `settings_manager.cpp`: Main interface for managing settings

Example of reading settings:

```cpp
#include <common/settings_objects.h>
#include <common/settings_helpers.h>

auto settings = PowerToysSettings::Settings::LoadSettings(L"ModuleName");
bool enabled = settings.GetValue(L"enabled", true);
```

Example of writing settings:

```cpp
PowerToysSettings::Settings settings(L"ModuleName");
settings.SetValue(L"setting_name", true);
settings.Save();
```

### C# Settings

For C# modules, the settings are accessed through the `SettingsUtils` class in the `Microsoft.PowerToys.Settings.UI.Library` namespace:

```csharp
using Microsoft.PowerToys.Settings.UI.Library;

// Read settings
var settings = SettingsUtils.GetSettings<ModuleSettings>("ModuleName");
bool enabled = settings.Enabled;

// Write settings
settings.Enabled = true;
SettingsUtils.SaveSettings(settings.ToJsonString(), "ModuleName");
```

## Settings UI

The PowerToys settings UI is a separate executable (`PowerToys.Settings.exe`) that communicates with the PowerToys runner through IPC. The settings UI is implemented as a WPF application.

### Key Components

- `OobeWindow.xaml`: First-run experience (OOBE) window
- `MainWindow.xaml`: Main settings window
- `ViewModels`: Contains view models for each settings page
- `Views`: Contains the XAML views for each settings page

### Adding a New Setting

To add a new setting to a module:

1. Add the setting to the module's settings model class
2. Update the module's settings view model to handle the new setting
3. Add UI controls for the setting in the module's settings view
4. Implement handling in the module to read and apply the new setting

## Settings Handling in Modules

Each PowerToys module must implement settings-related functions in its module interface:

```cpp
// Get the module's settings
virtual PowertoyModuleSettings get_settings() = 0;

// Called when settings are changed
virtual void set_config(const wchar_t* config_string) = 0;
```

When the user changes settings in the UI:

1. The settings UI serializes the settings to JSON
2. The JSON is sent to the PowerToys runner via IPC
3. The runner calls the `set_config` function on the appropriate module
4. The module parses the JSON and applies the new settings

## Debugging Settings

To debug settings issues:

1. Check the settings files in `%LOCALAPPDATA%\Microsoft\PowerToys\`
2. Ensure JSON is well-formed
3. Monitor IPC communication between settings UI and runner
4. Look for log messages related to settings changes

## Group Policy Integration

Settings can be overridden by Group Policy settings. When a setting is controlled by Group Policy:

1. The UI shows the setting as locked (disabled)
2. The module checks GPO settings before applying user settings
3. GPO settings take precedence over user settings

See [GPO Implementation](../processes/gpo.md) for more details.
