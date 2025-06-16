# Settings Implementation

This document describes how settings are implemented in PowerToys modules, including code examples for C++ and C# modules, and details on debugging settings issues.

## C++ Settings Implementation

For C++ modules, the settings system is implemented in the following files:

- `settings_objects.h` and `settings_objects.cpp`: Define the basic settings objects
- `settings_helpers.h` and `settings_helpers.cpp`: Helper functions for reading/writing settings
- `settings_manager.h` and `settings_manager.cpp`: Main interface for managing settings

### Reading Settings in C++

```cpp
#include <common/settings_objects.h>
#include <common/settings_helpers.h>

auto settings = PowerToysSettings::Settings::LoadSettings(L"ModuleName");
bool enabled = settings.GetValue(L"enabled", true);
```

### Writing Settings in C++

```cpp
PowerToysSettings::Settings settings(L"ModuleName");
settings.SetValue(L"setting_name", true);
settings.Save();
```

## C# Settings Implementation

For C# modules, the settings are accessed through the `SettingsUtils` class in the `Microsoft.PowerToys.Settings.UI.Library` namespace:

### Reading Settings in C#

```csharp
using Microsoft.PowerToys.Settings.UI.Library;

// Read settings
var settings = SettingsUtils.GetSettings<ModuleSettings>("ModuleName");
bool enabled = settings.Enabled;
```

### Writing Settings in C#

```csharp
using Microsoft.PowerToys.Settings.UI.Library;

// Write settings
settings.Enabled = true;
SettingsUtils.SaveSettings(settings.ToJsonString(), "ModuleName");
```

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
3. Monitor IPC communication between settings UI and runner using debugger breakpoints at key points:
   - In the Settings UI when sending configuration changes
   - In the Runner when receiving and dispatching changes
   - In the Module when applying changes
4. Look for log messages related to settings changes in the PowerToys logs

### Common Issues

- **Settings not saving**: Check file permissions or conflicts with other processes accessing the file
- **Settings not applied**: Verify IPC communication is working and the module is properly handling the configuration
- **Incorrect settings values**: Check JSON parsing and type conversion in the module code
