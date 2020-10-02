#include "pch.h"
#include <interface/powertoy_module_interface.h>
#include <interface/lowlevel_keyboard_event_data.h>
#include <interface/win_hook_event_data.h>
#include <common/settings_objects.h>

extern "C" IMAGE_DOS_HEADER __ImageBase;

BOOL APIENTRY DllMain(HMODULE hModule, DWORD ul_reason_for_call, LPVOID lpReserved)
{
    return TRUE;
}

// The PowerToy name that will be shown in the settings.
const static wchar_t* MODULE_NAME = L"FastDelete";
// Add a description that will we shown in the module settings page.
const static wchar_t* MODULE_DESC = L"Quickly delete large directory trees";

// These are the properties shown in the Settings page.
struct ModuleSettings
{
    bool enabled = true;

} g_settings;

// Implement the PowerToy Module Interface and all the required methods.
class FastDeleteModule : public PowertoyModuleIface
{
private:
    // The PowerToy state.
    bool m_enabled = false;

    // Load initial settings from the persisted values.
    void init_settings();

public:
    // Constructor
    FastDeleteModule()
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
    // nullptr as the last element of the array. Nullptr can also be returned for empty
    // list.
    virtual const wchar_t** get_events() override
    {
        static const wchar_t* events[] = { nullptr };
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

        settings.add_bool_toggle(
            L"enabled",
            L"Enable this shell extension",
            g_settings.enabled);

        return settings.serialize_to_buffer(buffer, buffer_size);
    }

    // Signal from the Settings editor to call a custom action.
    // This can be used to spawn more complex editors.
    virtual void call_custom_action(const wchar_t* action) override
    {
        // Not needed.
    }

    // Called by the runner to pass the updated settings values as a serialized JSON.
    virtual void set_config(const wchar_t* config) override
    {
        try
        {
            // Parse the input JSON string.
            PowerToysSettings::PowerToyValues values =
                PowerToysSettings::PowerToyValues::from_json_string(config);

            if (auto v = values.get_bool_value(L"enabled")) {
              g_settings.enabled = *v;
            }

            if (g_settings.enabled)
            {
                HKEY key;
                LONG err = RegCreateKeyExW(HKEY_CURRENT_USER, L"SOFTWARE\\Microsoft\\PowerToys FastDelete", 0, nullptr, 0, KEY_WRITE, nullptr, &key, nullptr);

                if (err == ERROR_SUCCESS)
                {
                    DWORD value = g_settings.enabled ? 0x1 : 0x0;
                    RegSetValueExW(key, L"Enabled", 0, REG_DWORD, (const BYTE *)&value, sizeof(value));
                    RegCloseKey(key);
                }
            }

            values.save_to_settings_file();
        }
        catch (std::exception&)
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
        return 0;
    }

    // This methods are part of an experimental features not fully supported yet
    virtual void register_system_menu_helper(PowertoySystemMenuIface* helper) override
    {
    }

    virtual void signal_system_menu_action(const wchar_t* name) override
    {
    }
};

// Load the settings file.
void FastDeleteModule::init_settings()
{
    try
    {
        // Load and parse the settings file for this PowerToy.
        PowerToysSettings::PowerToyValues settings =
            PowerToysSettings::PowerToyValues::load_from_settings_file(FastDeleteModule::get_name());

        if (auto v = settings.get_bool_value(L"enabled")) {
          g_settings.enabled = *v;
        }
    }
    catch (std::exception&)
    {
        // Error while loading from the settings file. Let default values stay as they are.
    }
}

extern "C" __declspec(dllexport) PowertoyModuleIface* __cdecl powertoy_create()
{
    return new FastDeleteModule();
}