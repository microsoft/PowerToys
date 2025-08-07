#include "pch.h"
#include <interface/powertoy_module_interface.h>
#include "trace.h"
#include <common/logger/logger.h>
#include <common/SettingsAPI/settings_objects.h>
#include <common/SettingsAPI/settings_helpers.h>
#include <locale>
#include <codecvt>
#include <common/utils/logger_helper.h>

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
const static wchar_t* MODULE_NAME = L"DarkMode";
// Add a description that will we shown in the module settings page.
const static wchar_t* MODULE_DESC = L"This is a module that allows you to control light/dark theming via set times, sun rise, or directly invoking the change.";

// These are the properties shown in the Settings page.
struct ModuleSettings
{
    // Add the PowerToy module properties with default values.
    // Currently available types:
    // - int
    // - bool
    // - string

    //bool bool_prop = true;
    //int int_prop = 10;
    //std::wstring string_prop = L"The quick brown fox jumps over the lazy dog";
    //std::wstring color_prop = L"#1212FF";

    bool m_changeSystem = true;
    bool m_changeApps = true;
    bool m_useLocation = false;
    int m_lightTime = 480;
    int m_darkTime = 1200;
    std::wstring m_latitude = L"0.0";
    std::wstring m_longitude = L"0.0";

} g_settings;

// Implement the PowerToy Module Interface and all the required methods.
class DarkModeInterface : public PowertoyModuleIface
{
private:
    // The PowerToy state.
    bool m_enabled = false;

    HANDLE m_process{ nullptr };

    // Load initial settings from the persisted values.
    void init_settings();

    // TODO: write Enable() function that does work to enable the powertoy

    // TODO: write Disable() function that kills process and logs.

public:
    // Constructor
    DarkModeInterface()
    {
        init_settings();
        LoggerHelpers::init_logger(L"DarkMode", L"ModuleInterface", LogSettings::darkModeLoggerName);
    };

    virtual const wchar_t* get_key() override
    {
        return L"DarkMode"; // your unique key string
    }

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
    //virtual const wchar_t** get_events() override
    //{
    //    static const wchar_t* events[] = { nullptr };
    //    // Available events:
    //    // - ll_keyboard
    //    // - win_hook_event
    //    //
    //    // static const wchar_t* events[] = { ll_keyboard,
    //    //                                   win_hook_event,
    //    //                                   nullptr };

    //    return events;
    //}

    // Return JSON with the configuration options.
    virtual bool get_config(wchar_t* buffer, int* buffer_size) override
    {
        HINSTANCE hinstance = reinterpret_cast<HINSTANCE>(&__ImageBase);

        // Create a Settings object with your module name
        PowerToysSettings::Settings settings(hinstance, get_name());
        settings.set_description(MODULE_DESC);

        // Boolean toggles
        settings.add_bool_toggle(
            L"change_system",
            L"Change System Theme",
            g_settings.m_changeSystem);

        settings.add_bool_toggle(
            L"change_apps",
            L"Change Apps Theme",
            g_settings.m_changeApps);

        settings.add_bool_toggle(
            L"use_location",
            L"Use your location to switch themes based on sunrise and sunset.",
            g_settings.m_useLocation);

        // Integer spinners (for time in minutes since midnight)
        settings.add_int_spinner(
            L"light_time",
            L"Time to switch to light theme (minutes after midnight).",
            g_settings.m_lightTime,
            0,
            1439,
            1);

        settings.add_int_spinner(
            L"dark_time",
            L"Time to switch to dark theme (minutes after midnight).",
            g_settings.m_darkTime,
            0,
            1439,
            1);

        // Strings for latitude and longitude
        settings.add_string(
            L"latitude",
            L"Your latitude in decimal degrees (e.g. 39.95).",
            g_settings.m_latitude);

        settings.add_string(
            L"longitude",
            L"Your longitude in decimal degrees (e.g. -75.16).",
            g_settings.m_longitude);

        // Serialize to buffer for the PowerToys runner
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

            //if (action_object.get_name() == L"custom_action_id") {
            //  // Execute your custom action
            //}
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
            auto values = PowerToysSettings::PowerToyValues::from_json_string(config, get_key());

            if (auto v = values.get_bool_value(L"change_system"))
            {
                g_settings.m_changeSystem = *v;
            }

            if (auto v = values.get_bool_value(L"change_apps"))
            {
                g_settings.m_changeApps = *v;
            }

            if (auto v = values.get_bool_value(L"use_location"))
            {
                g_settings.m_useLocation = *v;
            }

            if (auto v = values.get_int_value(L"light_time"))
            {
                g_settings.m_lightTime = *v;
            }

            if (auto v = values.get_int_value(L"dark_time"))
            {
                g_settings.m_darkTime = *v;
            }

            if (auto v = values.get_string_value(L"latitude"))
            {
                g_settings.m_latitude = *v;
            }

            if (auto v = values.get_string_value(L"longitude"))
            {
                g_settings.m_longitude = *v;
            }

            values.save_to_settings_file();
        }
        catch (const std::exception&)
        {
            Logger::error("[DarkMode] set_config: Failed to parse or apply config.");
        }
    }

    // Enable the powertoy
    /* virtual void enable()
    {
        Logger::info("DarkMode enabling");
        m_enabled = true;

        unsigned long pid = GetCurrentProcessId();
        std::wstring args = L"--use-pt-config --pid " + std::to_wstring(pid);
        std::wstring exe = L"PowerToys.DarkMode.exe";
        std::wstring cmd = exe + L" " + args;

        STARTUPINFO si = { sizeof(si) };
        PROCESS_INFORMATION pi;

        if (!CreateProcess(exe.c_str(), cmd.data(), NULL, NULL, TRUE, 0, NULL, NULL, &si, &pi))
        {
            DWORD err = GetLastError();
            Logger::error("Failed to launch DarkMode process: " + std::to_string(err));

        }
        else
        {
            m_process = pi.hProcess;
        }
    } */

    virtual void enable()
    {
        m_enabled = true;
        Logger::trace(L"Starting DarkMode process");
        unsigned long powertoys_pid = GetCurrentProcessId();

        std::wstring executable_args = L"";
        executable_args.append(std::to_wstring(powertoys_pid));

        SHELLEXECUTEINFOW sei{ sizeof(sei) };
        sei.fMask = { SEE_MASK_NOCLOSEPROCESS | SEE_MASK_FLAG_NO_UI };
        sei.lpFile = L"PowerToys.DarkMode.exe";
        sei.nShow = SW_SHOWNORMAL;
        sei.lpParameters = executable_args.data();
        if (ShellExecuteExW(&sei))
        {
            Logger::trace("Successfully started the DarkMode process");
        }
        else
        {
            Logger::error(L"DarkMode failed to start. {}", get_last_error_or_default(GetLastError()));
        }

        m_process = sei.hProcess;
    }

    // Disable the powertoy
    virtual void disable()
    {
        Logger::info("DarkMode disabling");
        m_enabled = false;

        if (m_process)
        {
            // Try waiting briefly to allow graceful exit, if needed
            constexpr DWORD timeout_ms = 1500;
            DWORD result = WaitForSingleObject(m_process, timeout_ms);

            if (result == WAIT_TIMEOUT)
            {
                // Force kill if it didn’t exit in time
                Logger::warn("DarkMode: Process didn't exit in time. Forcing termination.");
                TerminateProcess(m_process, 0);
            }

            // Always clean up the handle
            CloseHandle(m_process);
            m_process = nullptr;
        }
    }

    // Returns if the powertoys is enabled
    virtual bool is_enabled() override
    {
        return m_enabled;
    }

    // Handle incoming event, data is event-specific
    //virtual intptr_t signal_event(const wchar_t* name, intptr_t data) override
    //{
    //    if (wcscmp(name, ll_keyboard) == 0)
    //    {
    //        auto& event = *(reinterpret_cast<LowlevelKeyboardEvent*>(data));
    //        // Return 1 if the keypress is to be suppressed (not forwarded to Windows),
    //        // otherwise return 0.
    //        return 0;
    //    }
    //    else if (wcscmp(name, win_hook_event) == 0)
    //    {
    //        auto& event = *(reinterpret_cast<WinHookEvent*>(data));
    //        // Return value is ignored
    //        return 0;
    //    }
    //    return 0;
    //}

    //// This methods are part of an experimental features not fully supported yet
    //virtual void register_system_menu_helper(PowertoySystemMenuIface* helper) override
    //{
    //}

    //virtual void signal_system_menu_action(const wchar_t* name) override
    //{
    //}
};

// Load the settings file.
void DarkModeInterface::init_settings()
{
    try
    {
        PowerToysSettings::PowerToyValues settings =
            PowerToysSettings::PowerToyValues::load_from_settings_file(get_name());

        if (auto v = settings.get_bool_value(L"change_system"))
            g_settings.m_changeSystem = *v;

        if (auto v = settings.get_bool_value(L"change_apps"))
            g_settings.m_changeApps = *v;

        if (auto v = settings.get_bool_value(L"use_location"))
            g_settings.m_useLocation = *v;

        if (auto v = settings.get_int_value(L"light_time"))
            g_settings.m_lightTime = *v;

        if (auto v = settings.get_int_value(L"dark_time"))
            g_settings.m_darkTime = *v;

        if (auto v = settings.get_string_value(L"latitude"))
            g_settings.m_latitude = *v;

        if (auto v = settings.get_string_value(L"longitude"))
            g_settings.m_longitude = *v;
    }
    catch (std::exception&)
    {
        // Failed to load, keep default settings
    }
}

// This method of saving the module settings is only required if you need to do any
// custom processing of the settings before saving them to disk.
//void $projectname$::save_settings() {
//  try {
//    // Create a PowerToyValues object for this PowerToy
//    PowerToysSettings::PowerToyValues values(get_name());
//
//    // Save a bool property.
//    //values.add_property(
//    //  L"bool_toggle_1", // property name
//    //  g_settings.bool_prop // property value
//    //  g_settings.bool_prop // property value
//    //);
//
//    // Save an int property.
//    //values.add_property(
//    //  L"int_spinner_1", // property name
//    //  g_settings.int_prop // property value
//    //);
//
//    // Save a string property.
//    //values.add_property(
//    //  L"string_text_1", // property name
//    //  g_settings.string_prop // property value
//    );
//
//    // Save a color property.
//    //values.add_property(
//    //  L"color_picker_1", // property name
//    //  g_settings.color_prop // property value
//    //);
//
//    // Save the PowerToyValues JSON to the power toy settings file.
//    values.save_to_settings_file();
//  }
//  catch (std::exception ex) {
//    // Couldn't save the settings.
//  }
//}

extern "C" __declspec(dllexport) PowertoyModuleIface* __cdecl powertoy_create()
{
    return new DarkModeInterface();
}