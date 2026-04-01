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
var settings = SettingsUtils.Default.GetSettings<ModuleSettings>("ModuleName");
bool enabled = settings.Enabled;
```

### Writing Settings in C#

```csharp
using Microsoft.PowerToys.Settings.UI.Library;

// Write settings
settings.Enabled = true;
SettingsUtils.Default.SaveSettings(settings.ToJsonString(), "ModuleName");
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

# Shortcut Conflict Detection

Steps to enable conflict detection for a hotkey:

### 1. Implement module interface for hotkeys
Ensure the module interface provides either `size_t get_hotkeys(Hotkey* hotkeys, size_t buffer_size)` or `std::optional<HotkeyEx> GetHotkeyEx()`.

- If not yet implemented, you need to add it so that it returns all hotkeys used by the module.
- **Important**: The order of the returned hotkeys matters. This order is used as an index to uniquely identify each hotkey for conflict detection and lookup.
- For reference, see: `src/modules/AdvancedPaste/AdvancedPasteModuleInterface/dllmain.cpp`

### 2. Implement IHotkeyConfig in the module settings (UI side)
Make sure the module’s settings file inherits from `IHotkeyConfig` and implements `HotkeyAccessor[] GetAllHotkeyAccessors()`.

- This method should return all hotkeys used in the module.
- **Important**: The order of the returned hotkeys must be consistent with step 1 (`get_hotkeys()` or `GetHotkeyEx()`).
- For reference, see: `src/settings-ui/Settings.UI.Library/AdvancedPasteSettings.cs`
- **_Note:_** `HotkeyAccessor` is a wrapper around HotkeySettings. 
It provides both `getter` and `setter` methods to read and update the corresponding hotkey.
Additionally, each `HotkeyAccessor` requires a resource string that describes the purpose of the hotkey.
This string is typically defined in: `src/settings-ui/Settings.UI/Strings/en-us/Resources.resw`

### 3. Update the module’s ViewModel
The corresponding ViewModel should inherit from `PageViewModelBase` and implement `Dictionary<string, HotkeySettings[]> GetAllHotkeySettings()`.

- This method should return all hotkeys, maintaining the same order as in steps 1 and 2.
- For reference, see: `src/settings-ui/Settings.UI/ViewModels/AdvancedPasteViewModel.cs`

### 4. Ensure the module’s Views call `OnPageLoaded()`
Once the module’s view is loaded, make sure to invoke the ViewModel’s `OnPageLoaded()` method:
```cs
Loaded += (s, e) => ViewModel.OnPageLoaded();
```
- For reference, see: `src/settings-ui/Settings.UI/SettingsXAML/Views/AdvancedPaste.xaml.cs`

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

## Adding a New Module with Settings

Adding a new module with settings requires changes across multiple projects. Here's a step-by-step guide with references to real implementation examples:

### 1. Settings UI Library (Data Models)

Define the data models for your module's settings in the Settings UI Library project. These data models will be serialized to JSON configuration files stored in `%LOCALAPPDATA%\Microsoft\PowerToys\`.

Example: [Settings UI Library implementation](https://github.com/shuaiyuanxx/PowerToys/pull/3/files#diff-9be1cb88a52ce119e5ff990811e5fbb476c15d0d6b7d5de4877b1fd51d9241c3)

### 2. Settings UI (User Interface)

#### 2.1 Add a navigation item in ShellPage.xaml

The ShellPage.xaml is the entry point for the PowerToys settings, providing a navigation view of all modules. Add a navigation item for your new module.

Example: [Adding navigation item](https://github.com/shuaiyuanxx/PowerToys/pull/3/files#diff-5a06e6e7a5c99ae327c350c9dcc10036b49a2d66d66eac79a8364b4c99719c6b)

#### 2.2 Create a settings page for your module

Create a new XAML page that contains all the settings controls for your module.

Example: [New settings page](https://github.com/shuaiyuanxx/PowerToys/pull/3/files#diff-310fd49eba464ddf6a876dcf61f06a6f000ca6744f3a1f915c48c58384d7bacb)

#### 2.3 Implement the ViewModel

Create a ViewModel class that handles the settings data and operations for your module.

Example: [ViewModel implementation](https://github.com/shuaiyuanxx/PowerToys/pull/3/files#diff-409472a53326f2288c5b76b87c7ea8b5527c43ede12214a15b6caabe0403c1d0)

### 3. Module Implementation

#### 3.1 Implement PowertoyModuleIface in dllmain.cpp

The module interface must implement the PowertoyModuleIface to allow the runner to interact with it.

Reference: [PowertoyModuleIface definition](https://github.com/microsoft/PowerToys/blob/cc644b19982d09fcd2122fe7590c77496c4973b9/src/modules/interface/powertoy_module_interface.h#L6C1-L35C4)

#### 3.2 Implement Module UI

Create a UI for your module using either WPF (like ColorPicker) or WinUI3 (like Advanced Paste).

### 4. Runner Integration

Add your module to the known modules list in the runner so it can be brought up and initialized.

Example: [Runner integration](https://github.com/shuaiyuanxx/PowerToys/pull/3/files#diff-c07e4e5e9ce3c371d4c47f496b5f66734978a3c4f355c7e446c1ef19e086a4d6)

### 5. Testing and Debugging

1. Test each component individually:
   - Verify settings serialization/deserialization
   - Test module activation/deactivation
   - Test IPC communication

2. For signal-related issues, ensure all modules work correctly before debugging signal handling.

3. You can debug each module directly in Visual Studio or by attaching to running processes.

### Recommended Implementation Order

1. Module/ModuleUI implementation
2. Module interface (dllmain.cpp)
3. Runner integration
4. Settings UI implementation
5. OOBE (Out of Box Experience) integration
6. Other components
