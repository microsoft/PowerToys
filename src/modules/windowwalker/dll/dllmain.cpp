#include "pch.h"
#include <interface/powertoy_module_interface.h>
#include <interface/lowlevel_keyboard_event_data.h>
#include <interface/win_hook_event_data.h>
#include <common/settings_objects.h>
#include "trace.h"
#include "resource.h"

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

// These are the properties shown in the Settings page.
struct ModuleSettings
{

} g_settings;

// Implement the PowerToy Module Interface and all the required methods.
class WindowWalker : public PowertoyModuleIface
{
private:
    // The PowerToy name that will be show in the settings.
    std::wstring m_appName;

    // The PowerToy state.
    bool m_enabled = false;

    // Load initial settings from the persisted values.
    void init_settings();

    // Handle to the Window Walker app we launch
    HANDLE m_hProcess;

public:
    // Constructor
    WindowWalker()
    {
        init_settings();
        m_appName = GET_RESOURCE_STRING(IDS_MODULE_NAME);
    };

    // Destroy the powertoy and free memory
    virtual void destroy() override
    {
        if (m_enabled)
        {
            TerminateProcess(m_hProcess, 1);
        }

        delete this;
    }

    // Return the display name of the powertoy, this will be cached by the runner
    virtual const wchar_t* get_name() override
    {
        return m_appName.c_str();
    }

    // Return array of the names of all events that this powertoy listens for, with
    // nullptr as the last element of the array. Nullptr can also be retured for empty
    // list.
    virtual const wchar_t** get_events() override
    {
        static const wchar_t* events[] = { nullptr };

        return events;
    }

    // Return JSON with the configuration options.
    virtual bool get_config(wchar_t* buffer, int* buffer_size) override
    {
        HINSTANCE hinstance = reinterpret_cast<HINSTANCE>(&__ImageBase);

        // Create a Settings object.
        PowerToysSettings::Settings settings(hinstance, get_name());
        settings.set_description(GET_RESOURCE_STRING(IDS_GENERAL_DESCRIPTION));
        settings.set_overview_link(GET_RESOURCE_STRING(IDS_OVERVIEW_LINK));
        settings.set_icon_key(L"pt-window-walker");


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
        catch (std::exception&)
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
        SHELLEXECUTEINFO sei{ sizeof(sei) };
        sei.fMask = { SEE_MASK_NOCLOSEPROCESS | SEE_MASK_FLAG_NO_UI };
        sei.lpFile = L"modules\\WindowWalker.exe";
        sei.nShow = SW_SHOWNORMAL;
        ShellExecuteEx(&sei);

        m_hProcess = sei.hProcess;

        m_enabled = true;
    }

    // Disable the powertoy
    virtual void disable()
    {
        if (m_enabled)
        {
            TerminateProcess(m_hProcess, 1);
        }

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

    // This methods are part of an experimental features not fully supported yet
    virtual void register_system_menu_helper(PowertoySystemMenuIface* helper) override
    {
    }

    virtual void signal_system_menu_action(const wchar_t* name) override
    {
    }
};

// Load the settings file.
void WindowWalker::init_settings()
{
    try
    {
        // Load and parse the settings file for this PowerToy.
        PowerToysSettings::PowerToyValues settings =
            PowerToysSettings::PowerToyValues::load_from_settings_file(WindowWalker::get_name());
    }
    catch (std::exception&)
    {
        // Error while loading from the settings file. Let default values stay as they are.
    }
}

extern "C" __declspec(dllexport) PowertoyModuleIface* __cdecl powertoy_create()
{
    return new WindowWalker();
}