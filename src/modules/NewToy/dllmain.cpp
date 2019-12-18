#include "pch.h"
#include <interface/powertoy_module_interface.h>
#include <interface/lowlevel_keyboard_event_data.h>
#include <interface/win_hook_event_data.h>
#include <common/settings_objects.h>
#include "trace.h"
#include "NewToy.h"
#include <common/common.h>
#include "Settings.h"

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

// The PowerToy name that will be shown in the settings.
const static wchar_t* MODULE_NAME = L"NewToy";
// Add a description that will we shown in the module settings page.
const static wchar_t* MODULE_DESC = L"This powertoy is used to demonstrate keyboard hooks.";

// These are the properties shown in the Settings page.


// Implement the PowerToy Module Interface and all the required methods.
class NewToy : public PowertoyModuleIface
{
private:
    // The PowerToy state.
    bool m_enabled = false;
    // Load initial settings from the persisted values.
    void init_settings();
    const std::wstring m_showHotkeyName = L"newtoy_show_hotkey";
    const std::wstring m_editHotkeyName = L"newtoy_edit_hotkey";
    ModuleSettings* g_settings;

public:
    // Constructor
    NewToy()
    {
        init_settings();
    };

    // Destroy the powertoy and free memory
    virtual void destroy() override
    {
        delete this;
    }

    // Return the display name of the powertoy, this will be cached by the runner
    virtual const wchar_t* get_name() override
    {
        return MODULE_NAME;
    }

    // Return array of the names of all events that this powertoy listens for, with
    // nullptr as the last element of the array. Nullptr can also be retured for empty
    // list.
    virtual const wchar_t** get_events() override
    {
        static const wchar_t* events[] = { ll_keyboard, nullptr };
        // Available events:
        // - ll_keyboard
        // - win_hook_event
        //
        // static const wchar_t* events[] = { ll_keyboard,
        //                                   win_hook_event,
        //                                   nullptr };

        return events;
    }

    // Return JSON with the configuration options.
    virtual bool get_config(wchar_t* buffer, int* buffer_size) override
    {
        HINSTANCE hinstance = reinterpret_cast<HINSTANCE>(&__ImageBase);

        // Create a Settings object.
        PowerToysSettings::Settings settings(hinstance, get_name());
        settings.set_description(MODULE_DESC);

        // Show Hotkey property
        settings.add_hotkey(m_showHotkeyName, L"New Toy Show Hotkey", g_settings->newToyShowHotkey);
        
        // Edit Hotkey property
        settings.add_hotkey(m_editHotkeyName, L"New Toy Edit Hotkey", g_settings->newToyEditHotkey);

        // A bool property with a toggle editor.
        settings.add_bool_toogle(
            L"bool_toggle_1", // property name.
            L"This is what a BoolToggle property looks like", // description or resource id of the localized string.
            g_settings->bool_prop // property value.
        );

        // An integer property with a spinner editor.
        settings.add_int_spinner(
            L"int_spinner_1", // property name
            L"This is what a IntSpinner property looks like", // description or resource id of the localized string.
            g_settings->int_prop, // property value.
            0, // min value.
            100, // max value.
            10 // incremental step.
        );

        // A string property with a textbox editor.
        settings.add_string(
            L"string_text_1", // property name.
            L"This is what a String property looks like", // description or resource id of the localized string.
            g_settings->string_prop // property value.
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
        }
        catch (std::exception ex)
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
            if (!g_settings) 
            {
                g_settings = new ModuleSettings();
            }

            // Update Show Hotkey property
            auto showHotkeyProp = values.get_json(m_showHotkeyName);
            if (showHotkeyProp)
            {
                g_settings->newToyShowHotkey = PowerToysSettings::HotkeyObject::from_json(*showHotkeyProp);
            }

            // Update Edit Hotkey property
            auto editHotkeyProp = values.get_json(m_editHotkeyName);
            if (editHotkeyProp)
            {
                g_settings->newToyEditHotkey = PowerToysSettings::HotkeyObject::from_json(*editHotkeyProp);
            }

            // Update the bool property.
            auto boolProp = values.get_bool_value(L"bool_toggle_1");
            if (boolProp)
            {
                g_settings->bool_prop = boolProp.value();
            }

            // Update an int property.
            auto intProp = values.get_int_value(L"int_spinner_1");
            if (intProp)
            {
                g_settings->int_prop = intProp.value();
            }

            // Update a string property.
            auto stringProp = values.get_string_value(L"string_text_1");
            if (stringProp)
            {
                g_settings->string_prop = stringProp.value();
            }

            // If you don't need to do any custom processing of the settings, proceed
            // to persists the values calling:
            values.save_to_settings_file();
            m_app->HotkeyChanged();
        }
        catch (std::exception ex)
        {
            // Improper JSON.
        }
    }

    // Enable the powertoy
    virtual void enable()
    {
        if (!m_app)
        {
            m_app = MakeNewToy(reinterpret_cast<HINSTANCE>(&__ImageBase), g_settings);
            if (m_app)
            {
                m_app->Run();
            }
        }
        m_enabled = true;
    }

    // Disable the powertoy
    virtual void disable()
    {
        m_enabled = false;
        if (m_app)
        {
            m_app->Destroy();
            m_app = nullptr;
        }
    }

    // Returns if the powertoys is enabled
    virtual bool is_enabled() override
    {
        return m_enabled;
    }

    // Handle incoming event, data is event-specific
    virtual intptr_t signal_event(const wchar_t* name, intptr_t data) override
    {

        if (m_app)
        {
            if (wcscmp(name, ll_keyboard) == 0)
            {
                auto& event = *(reinterpret_cast<LowlevelKeyboardEvent*>(data));
                // Return 1 if the keypress is to be suppressed (not forwarded to Windows), otherwise return 0.
                return HandleKeyboardHookEvent(&event);
            }
            else if (wcscmp(name, win_hook_event) == 0)
            {
                auto& event = *(reinterpret_cast<WinHookEvent*>(data));
                // Return value is ignored
                //HandleWinHookEvent(reinterpret_cast<WinHookEvent*>(data));
                return 0;
            }
        }
        return 0;

    }

    virtual void register_system_menu_helper(PowertoySystemMenuIface* helper) override {}
    virtual void signal_system_menu_action(const wchar_t* name) override {}

    // Function to catch a keyboard event
    intptr_t HandleKeyboardHookEvent(LowlevelKeyboardEvent* data) noexcept;
    winrt::com_ptr<INewToy> m_app;
};

intptr_t NewToy::HandleKeyboardHookEvent(LowlevelKeyboardEvent* data) noexcept
{
    if (data->wParam == WM_KEYDOWN)
    {
        return m_app.as<INewToy>()->OnKeyDown(data->lParam) ? 1 : 0;
    }
    return 0;
}

// Load the settings file.
void NewToy::init_settings()
{
    try
    {
        g_settings = new ModuleSettings();
        // Load and parse the settings file for this PowerToy.
        PowerToysSettings::PowerToyValues settings =
            PowerToysSettings::PowerToyValues::load_from_settings_file(get_name());

        // Load Show Hotkey property
        auto showHotkeyProp = settings.get_json(m_showHotkeyName);
        if (showHotkeyProp)
        {
            g_settings->newToyShowHotkey = PowerToysSettings::HotkeyObject::from_json(*showHotkeyProp);
        }

        // Load Edit Hotkey property
        auto editHotkeyProp = settings.get_json(m_editHotkeyName);
        if (editHotkeyProp)
        {
            g_settings->newToyEditHotkey = PowerToysSettings::HotkeyObject::from_json(*editHotkeyProp);
        }


        // Load a bool property.
        auto boolProp = settings.get_bool_value(L"bool_toggle_1");
        if (boolProp)
        {
            g_settings->bool_prop = boolProp.value();
        }

        // Load an int property.
        auto intProp = settings.get_int_value(L"int_spinner_1");
        if (intProp)
        {
            g_settings->int_prop = intProp.value();
        }

        // Load a string property.
        auto stringProp = settings.get_string_value(L"string_text_1");
        if (stringProp)
        {
            g_settings->string_prop = stringProp.value();
        }
    }
    catch (std::exception ex)
    {
        // Error while loading from the settings file. Let default values stay as they are.
    }
}

extern "C" __declspec(dllexport) PowertoyModuleIface* __cdecl powertoy_create()
{
    return new NewToy();
}
