# Settings

## Overview

PowerToys provides a common framework for settings. It can be used to save and load settings on disk, and provides a user interface for changing the options.

## Initialization
When a PowerToy module is created, it should load its configuration using [`PowerToyValues`](/src/common/settings_objects.h) class. The class provides static `load_from_settings_file` method which takes one parameter - the PowerToy module name. The `PowerToyValues` class provides methods to extract values. The method return `std::optional` - it is possible, that the method will return `std::nullopt` in which case you must use defaults.
```c++
class ExamplePowertoy : public PowertoyModuleIface
{
public:
    ExamplePowertoy()
    {
        auto settings = PowerToySettings::PowerToyValues::load_from_settings_file(L"Example Powertoy");
        // See if value is set, otherwise keep the default value
        if (auto int_value = settings.get_int_value(L"int_setting"))
        {
            m_int_setting = *int_value;
        }
        if (auto string_value = setting.get_string_value("string_setting"))
        {
            m_string_setting = *string_value;
        }
    }
    // ...
private:
    // Settings and their default values
    int m_int_setting = 10;
    std::wstring m_string_setting = L"default";
}
```

## Settings screen
When users starts the settings screen, the runner will call the [`get_config()`](modules/interface.md) method. The interface expects the method to fill provided buffer. Use the [`Settings`](/src/common/settings_objects.h) class to construct proper response with proper format. The class has helper method to set description and links fields, it also provides a way to define the content of the settings screen. Keep all the strings in the resource file and provide the resource IDs to the methods.
```c++
extern "C" IMAGE_DOS_HEADER __ImageBase; // Needed to get strings from the resource file

bool ExamplePowertoy::get_config(wchar_t* buffer, int* buffer_size)
{
    PowerToySettings::Settings settings(reinterpret_cast<HINSTANCE>(&__ImageBase), L"Example Powertoy");
    // Set PowerToy description
    settings.set_description(IDS_POWERTOY_DESCRIPTION);
    settings.set_icon_key("pt_icon_key");
    settings.set_overview_link(IDS_POWERTOY_OVERVIEW_LINK);
    settings.set_video_link(IDS_POWERTOY_OVERVIEW_LINK);

    // Add int and string settings, provide current values:
    settings.add_int_spinner(L"int_setting", IDS_INT_SETTING_DESCRIPTION, m_int_setting, 0, 100, 10);
    settings.add_string(L"string_setting", IDS_STRING_SETTING_DESCRIPTION, m_string_setting);

    // Use the build-in machinery to return the configuration:
    return settings.serialize_to_buffer(buffer, buffer_size);
}
```
The list of all the available settings elements and their description is [further in this doc](#available-settings-elements). New PowerToy icons need to be [added to the `settings-web` project](https://github.com/microsoft/PowerToys/blob/master/doc/devdocs/settings-web.md#updating-the-icons).

## User changes settings
When user closes the settings screen, the runner will call the [`get_config()`](modules/interface.md) method. Use [`PowerToyValues`](/src/common/settings_objects.h) class static `from_json_string` method to parse the settings. After that, the code is similar to loading the settings from disk:
```c++
void ExamplePowertoy::set_config(const wchar_t* config)
{
    auto settings = PowerToySettings::PowerToyValues::from_json_string(config);
    // See if value is set update the values
    if (auto int_value = settings.get_int_value(L"int_setting"))
    {
        m_int_setting = *int_value;
    }
    if (auto string_value = setting.get_string_value("string_setting"))
    {
        m_string_setting = *string_value;
    }
    // Save the new settings to disk
    settings.save_to_settings_file();
}
```

## Detailed reference
For a detailed reference of how the settings are implemented in the runner and in the settings editor, consult [this detailed guide](settings-reference.md).

# Available settings elements

## Bool toggle
<table><tr><td align="center">
<img src="../images/settings/bool_toggle.png" width="80%">
</td></tr></table>

```c++
settings.add_bool_toogle(name, description, value) 
```
A simple on-off toggle. Parameters:
  * `name` - Key for the element in the JSON.
  * `description` - Resource ID of the text displayed to the user.
  * `value` - Initial state of the toggle (`true` - on, `false` - off).

The toggle value is stored as bool:
```c++
std::optional<bool> bool_value = settings.get_bool_value(L"bool_name");
```

## Int Spinner
<table><tr><td align="center">
<img src="../images/settings/int_spinner.png" width="80%">
</td></tr></table>

```c++
settings.add_int_spinner(name, description, value, min, max, step)
```
Numeric input with dials to increment and decrement the value. Parameters:
  * `name` - Key for element in the JSON.
  * `description` - Resource ID of the text displayed to the user.
  * `value` - Initial control value.
  * `min`, `max` - Minimum and maximum values for the input. User cannot use dials to move beyond those values, if a value out of range is inserted using the keyboard, it will get clamped to the allowed range.
  * `step` - How much the dials change the value.

The spinner value is stored as int:
```c++
std::optional<int> int_value = settings.get_int_value(L"int_spinner_name");
```

## String
<table><tr><td align="center">
<img src="../images/settings/string.png" width="80%">
</td></tr></table>

```c++
settings.add_string(name, description, value)
```
Single line text input. Parameters:
  * `name` - Key for element in the JSON.
  * `description` - Resource ID of the text displayed to the user.
  * `value` - Default value for the input.

The input value is stored as `std::wstring`:
```c++
std::optional<std::wstring> string_value = settings.get_string_value(L"string_name");
```

## Multiline string
<table><tr><td align="center">
<img src="../images/settings/multiline.png" width="80%">
</td></tr></table>

```c++
settings.add_multiline_string(name, description, value)
```
Multiline text input. Parameters:
  * `name` - Key for element in the JSON.
  * `description` - Resource ID of the text displayed to the user.
  * `value` - Default value for the input. Can have multiple lines.

The input value is stored as string:
```c++
std::optional<std::wstring> value = settings.get_string_value(L"multiline_name");
```

## Color picker
<table><tr><td align="center">
<img src="../images/settings/color_picker.png" width="80%">
</td></tr></table>

```c++
settings.add_color_picker(name, description, value)
```

Allows user to pick a color. Parameters:
  * `name` - Key for element in the JSON.
  * `description` - Resource ID of the text displayed to the user.
  * `value` - Initial color, as a string in `"#RRGGBB"` format.

The color picker value is stored as `std::wstring` as `#RRGGBB`:
```c++
std::optional<std::wstring> value = settings.get_string_value(L"colorpicker_name");
```

## Hotkey
<table><tr><td align="center">
<img src="../images/settings/hotkey.png" width="80%">
</td></tr></table>

```c++
settings.add_hotkey(name, description, hotkey)
```
Input for capturing hotkeys. Parameters:
  * `name` - Key for element in the JSON.
  * `description` - Resource ID of the text displayed to the user.
  * `hotkey` - Instance of `PowerToysSettings::HotkeyObject` class.

You can create `PowerToysSettings::HotkeyObject` object either by using helper `from_settings` static method or by providing a JSON object to `from_json` static method.

The `PowerToysSettings::HotkeyObject::from_settings` take following parameters:
  * `win_pressed` - Is the WinKey pressed.
  * `ctrl_pressed` - Is the Ctrl key pressed.
  * `alt_pressed` - Is the Alt key pressed.
  * `shift_pressed` - Is the Shift key pressed.
  * `vk_code` - The [virtual key-code](https://docs.microsoft.com/en-us/windows/win32/inputdev/virtual-key-codes) of the key.

The displayed key is deduced from the `vk_code` using the users keyboard layout and language settings.

The hotkey value is returned as JSON, which can be used with the `from_json` method to create a `HotkeyObject` object. A typical example of registering a hotkey:
```c++
std::optional<json::JsonObject> value = settings.get_json(L"hotkey_name");
if (value) {
    auto hotkey = PowerToysSettings::HotkeyObject::from_json(*value);
    RegisterHotKey(hwnd, 1, hotkey.get_modifiers(), hotkey.get_code());
}
```

## Choice group
<table><tr><td align="center">
<img src="../images/settings/choice_group.png" width="80%">
</td></tr></table>

```c++
add_choice_group(name, description, value, vector<pair<wstring, UINT>> keys_and_texts)
```
A radio buttons group. Parameters:
  * `name` - Key for element in the JSON.
  * `description` - Resource ID of the text displayed to the user.
  * `value` - Key selected by default.
  * `keys_and_text` - Vector of radio buttons definitions: key and the displayed label. 


The chosen button value is stored as a string with the key of the button selected by the user:
```c++
std::optional<std::wstring> value = settings.get_string_value(L"choice_group_name");
```

## Dropdown
<table>
<tr><td align="center">
<img src="../images/settings/dropdown_1.png" width="80%">
</td></tr>
<tr><td align="center">
<img src="../images/settings/dropdown_2.png" width="80%">
</td></tr>
</table>

```c++
add_dropdown(name, description, value, vector<pair<wstring, UINT>> keys_and_texts)
```

A dropdown. Parameters:
  * `name` - Key for element in the JSON.
  * `description` - Resource ID of the text displayed to the user.
  * `value` - Key selected by default.
  * `keys_and_text` - Vector of the options definitions: key and the displayed label.

The chosen value is stored as a string with the key of the option selected by the user:
```c++
std::optional<std::wstring> value = settings.get_string_value(L"dropdown_name");
```
## Custom action
<table><tr><td align="center">
<img src="../images/settings/custom_action.png" width="80%">
</td></tr></table>

```c++
add_custom_action(name, description, button_text, ext_description)
```

Adds a button with a description. Parameters:
  * `name` - Key for element in the JSON.
  * `description` - Resource ID of the text displayed to the user.
  * `button_text` - Resource ID for the button label.
  * `ext_description` - Resource ID for the extended description.

When the button is pressed, the `call_custom_action` method of the module will be called, with JSON containing the name of the action. Parse it using `PowerToysSettings::CustomActionObject`:
```c++
void ExamplePowertoy::call_custom_action(const wchar_t* action) override
{
    auto action_object = PowerToysSettings::CustomActionObject::from_json_string(action);
    auto name = action_object.get_name(); // same value as the 'name' parameter
    // .. do stuff ..
}
```

# File organization

### [main.cpp](/src/settings/main.cpp)
Contains the main executable code, initializing and managing the Window containing the WebView and communication with the main PowerToys executable.

### [StreamURIResolverFromFile.cpp](/src/settings/StreamURIResolverFromFile.cpp)
Defines a class implementing `IUriToStreamResolver`. Allows the WebView to navigate to filesystem files in this Win32 project.

### [settings-html/](/src/settings/settings-html/)
Contains the assets file from building the [Web project for the Settings UI](/src/settings./settings-web). It will be loaded by the WebView.
