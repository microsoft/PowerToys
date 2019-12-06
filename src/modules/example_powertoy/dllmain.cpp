#include "pch.h"
#include <interface/powertoy_module_interface.h>
#include <interface/lowlevel_keyboard_event_data.h>
#include <interface/win_hook_event_data.h>
#include "trace.h"
#include <common/settings_objects.h>

extern "C" IMAGE_DOS_HEADER __ImageBase;

BOOL APIENTRY DllMain(HMODULE hModule, DWORD ul_reason_for_call, LPVOID lpReserved)
{
    switch (ul_reason_for_call)
    {
    case DLL_PROCESS_ATTACH:
        Trace::RegisterProvider();
        break;
    case DLL_THREAD_ATTACH:
    case DLL_THREAD_DETACH:
        break;
    case DLL_PROCESS_DETACH:
        Trace::UnregisterProvider();
        break;
    }
    return TRUE;
}

// PowerToy sample settings.
struct SampleSettings
{
    bool test_bool_prop = true;
    int test_int_prop = 10;
    std::wstring test_string_prop = L"The quick brown fox jumps over the lazy dog";
    std::wstring test_color_prop = L"#1212FF";
} g_settings;

// Implement the PowerToy Module Interface and all the required methods.
class ExamplePowertoy : public PowertoyModuleIface
{
private:
    // The PowerToy state.
    bool m_enabled = false;

    // Load and save Settings.
    void init_settings();
    void save_settings();

public:
    // Constructor
    ExamplePowertoy()
    {
        init_settings();
    };

    // Destroy the powertoy and free memory
    virtual void destroy() override
    {
        delete this;
    }

    // Return the display name of the powertoy, this will be cached
    virtual const wchar_t* get_name() override
    {
        return L"Example Powertoy";
    }

    // Return array of the names of all events that this powertoy listens for, with
    // nullptr as the last element of the array. Nullptr can also be retured for empty
    // list.
    // Right now there is only lowlevel keyboard hook event
    virtual const wchar_t** get_events() override
    {
        static const wchar_t* events[] = { ll_keyboard,
                                           win_hook_event,
                                           nullptr };
        return events;
    }

    // Return JSON with the configuration options.
    virtual bool get_config(wchar_t* buffer, int* buffer_size) override
    {
        HINSTANCE hinstance = reinterpret_cast<HINSTANCE>(&__ImageBase);

        // Create a Settings object.
        PowerToysSettings::Settings settings(hinstance, get_name());
        settings.set_description(L"Serves as an example powertoy, with example settings.");

        // Show an overview link in the Settings page
        settings.set_overview_link(L"https://github.com/microsoft/PowerToys");

        // Show a video link in the Settings page.
        settings.set_video_link(L"https://www.youtube.com/watch?v=d3LHo2yXKoY&t=21462");

        // Add a bool property with a toggle editor.
        settings.add_bool_toogle(
            L"test_bool_toggle", // property name.
            L"This is what a BoolToggle property looks like", // description or resource id of the localized string.
            g_settings.test_bool_prop // property value.
        );

        // Add an integer property with a spinner editor.
        settings.add_int_spinner(
            L"test_int_spinner", // property name
            L"This is what a IntSpinner property looks like", // description or resource id of the localized string.
            g_settings.test_int_prop, // property value.
            0, // min value.
            100, // max value.
            10 // incremental step.
        );

        // Add a string property with a textbox editor.
        settings.add_string(
            L"test_string_text", // property name.
            L"This is what a String property looks like", // description or resource id of the localized string.
            g_settings.test_string_prop // property value.
        );

        // Add a string property with a color picker editor.
        settings.add_color_picker(
            L"test_color_picker", // property name.
            L"This is what a ColorPicker property looks like", // description or resource id of the localized string.
            g_settings.test_color_prop // property value.
        );

        // Add a custom action property. When using this settings type, the "PowertoyModuleIface::call_custom_action()"
        // method should be overriden as well.
        settings.add_custom_action(
            L"test_custom_action", // action name.
            L"This is what a CustomAction property looks like", // label above the field.
            L"Call a custom action", // button text.
            L"Press the button to call a custom action in the Example PowerToy" // display values / extended info.
        );

        return settings.serialize_to_buffer(buffer, buffer_size);
    }

    // Signal from the Settings editor to call a custom action.
    // This can be used to spawn more complex editors.
    virtual void call_custom_action(const wchar_t* action) override
    {
        static UINT custom_action_num_calls = 0;
        try
        {
            // Parse the action values, including name.
            PowerToysSettings::CustomActionObject action_object =
                PowerToysSettings::CustomActionObject::from_json_string(action);

            if (action_object.get_name() == L"test_custom_action")
            {
                // Custom action code to increase and show a counter.
                ++custom_action_num_calls;
                std::wstring msg(L"I have been called ");
                msg += std::to_wstring(custom_action_num_calls);
                msg += L" time(s).";
                MessageBox(NULL, msg.c_str(), L"Custom action call.", MB_OK | MB_TOPMOST);
            }
        }
        catch (std::exception& ex)
        {
            // Improper JSON.
        }
    }

    // Called by the runner to pass the updated settings values as a serialized JSON.
    virtual void set_config(const wchar_t* config) override
    {
        try
        {
            // Parse the input JSON string.
            PowerToysSettings::PowerToyValues values =
                PowerToysSettings::PowerToyValues::from_json_string(config);

            // Update the bool property.
            if (values.is_bool_value(L"test_bool_toggle"))
            {
                g_settings.test_bool_prop = values.get_bool_value(L"test_bool_toggle");
            }

            // Update the int property.
            if (values.is_int_value(L"test_int_spinner"))
            {
                g_settings.test_int_prop = values.get_int_value(L"test_int_spinner");
            }

            // Update the string property.
            if (values.is_string_value(L"test_string_text"))
            {
                g_settings.test_string_prop = values.get_string_value(L"test_string_text");
            }

            // Update the color property.
            if (values.is_string_value(L"test_color_picker"))
            {
                g_settings.test_color_prop = values.get_string_value(L"test_color_picker");
            }

            // If you don't need to do any custom processing of the settings, proceed
            // to persists the values calling:
            values.save_to_settings_file();
            // Otherwise call a custom function to process the settings before saving them to disk:
            // save_settings();
        }
        catch (std::exception& ex)
        {
            // Improper JSON.
        }
    }

    // Enable the powertoy
    virtual void enable()
    {
        m_enabled = true;
    }

    // Disable the powertoy
    virtual void disable()
    {
        m_enabled = false;
    }

    // Returns if the powertoys is enabled
    virtual bool is_enabled() override
    {
        return m_enabled;
    }

    // Handle incoming event, data is event-specific
    virtual intptr_t signal_event(const wchar_t* name, intptr_t data) override
    {
        if (wcscmp(name, ll_keyboard) == 0)
        {
            auto& event = *(reinterpret_cast<LowlevelKeyboardEvent*>(data));
            // Return 1 if the keypress is to be suppressed (not forwarded to Windows),
            // otherwise return 0.
            return 0;
        }
        else if (wcscmp(name, win_hook_event) == 0)
        {
            auto& event = *(reinterpret_cast<WinHookEvent*>(data));
            // Return value is ignored
            return 0;
        }
        return 0;
    }

    virtual void register_system_menu_helper(PowertoySystemMenuIface* helper) override {}
    virtual void signal_system_menu_action(const wchar_t* name) override {}
};

// Load the settings file.
void ExamplePowertoy::init_settings()
{
    try
    {
        // Load and parse the settings file for this PowerToy.
        PowerToysSettings::PowerToyValues settings =
            PowerToysSettings::PowerToyValues::load_from_settings_file(ExamplePowertoy::get_name());

        // Load the bool property.
        if (settings.is_bool_value(L"test_bool_toggle"))
        {
            g_settings.test_bool_prop = settings.get_bool_value(L"test_bool_toggle");
        }

        // Load the int property.
        if (settings.is_int_value(L"test_int_spinner"))
        {
            g_settings.test_int_prop = settings.get_int_value(L"test_int_spinner");
        }

        // Load the string property.
        if (settings.is_string_value(L"test_string_text"))
        {
            g_settings.test_string_prop = settings.get_string_value(L"test_string_text");
        }

        // Load the color property.
        if (settings.is_string_value(L"test_color_picker"))
        {
            g_settings.test_color_prop = settings.get_string_value(L"test_color_picker");
        }
    }
    catch (std::exception& ex)
    {
        // Error while loading from the settings file. Let default values stay as they are.
    }
}

// This method of saving the module settings is only required if you need to do any
// custom processing of the settings before saving them to disk.
void ExamplePowertoy::save_settings()
{
    try
    {
        // Create a PowerToyValues object for this PowerToy
        PowerToysSettings::PowerToyValues values(get_name());

        // Save the bool property.
        values.add_property(
            L"test_bool_toggle", // property name
            g_settings.test_bool_prop // property value
        );

        // Save the int property.
        values.add_property(
            L"test_int_spinner", // property name
            g_settings.test_int_prop // property value
        );

        // Save the string property.
        values.add_property(
            L"test_string_text", // property name
            g_settings.test_string_prop // property value
        );

        // Save the color property.
        values.add_property(
            L"test_color_picker", // property name
            g_settings.test_color_prop // property value
        );

        // Save the PowerToyValues JSON to the power toy settings file.
        values.save_to_settings_file();
    }
    catch (std::exception& ex)
    {
        // Couldn't save the settings.
    }
}

extern "C" __declspec(dllexport) PowertoyModuleIface* __cdecl powertoy_create()
{
    return new ExamplePowertoy();
}
