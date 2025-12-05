#include "pch.h"

#include <interface/powertoy_module_interface.h>
#include <common/SettingsAPI/settings_helpers.h>
#include <common/hooks/LowlevelKeyboardEvent.h>
#include <common/hooks/WinHookEvent.h>
#include <common/SettingsAPI/settings_objects.h>
#include <common/utils/gpo.h>
#include <common/logger/logger.h>
#include <common/utils/process_path.h>
#include <common/utils/logger_helper.h>

#include "trace.h"
#include <iostream>

extern "C" IMAGE_DOS_HEADER __ImageBase;

BOOL APIENTRY DllMain(HMODULE /*hModule*/, DWORD ul_reason_for_call, LPVOID /*lpReserved*/)
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
const static wchar_t* MODULE_NAME = L"Keystroke Overlay";
const static wchar_t* MODULE_KEY = L"KeystrokeOverlay";
// Add a description that will we shown in the module settings page.
const static wchar_t* MODULE_DESC = L"<no description>";

const static wchar_t* win_hook_event = L"win_hook_event";
// Note: low-level keyboard hook and batching are handled by the external
// KeystrokeServer.exe. The module no longer declares an internal event queue
// or batcher to avoid duplicate IPC servers.

// These are the properties shown in the Settings page.
struct ModuleSettings
{
    // Add the PowerToy module properties with default values.

    bool is_draggable = true;
    int overlay_timeout = 3000;
    int display_mode = 0; // NEW: 0=Standard, 1=Compact, etc. (Define your modes)
    
    int text_size = 24;
    int text_opacity = 100;

    int bg_opacity = 50;

    std::wstring text_color = L"#FFFFFF";
    std::wstring bg_color = L"#000000";

    PowerToysSettings::HotkeyObject switch_monitor_hotkey =
        PowerToysSettings::HotkeyObject::from_settings(true, false, false, true, 0xBF); // Default: Win+Ctrl+/

} g_settings;

class KeystrokeOverlay : public PowertoyModuleIface
{
    std::wstring app_name;
    std::wstring app_key;

private:
    // The PowerToy state.
    bool m_enabled = false;
    HANDLE m_hProcess = nullptr;
    // Handle for the hidden keystroke server process started by this module
    HANDLE m_hServerProcess = nullptr;

    // Load initial settings from the persisted values.
    void init_settings();

    // Launch and terminate the Keystroke Overlay process.
    void launch_process()
    {
        Logger::trace(L"Launching Keystroke Overlay process");
        unsigned long powertoys_pid = GetCurrentProcessId();

        std::wstring executable_args = L"" + std::to_wstring(powertoys_pid);
        std::wstring application_path = L"PowerToys.KeystrokeOverlay.exe";
        std::wstring full_command_path = application_path + L" " + executable_args.data();
        Logger::trace(L"PowerToys KeystrokeOverlay launching: " + full_command_path);

        STARTUPINFO info = { sizeof(info) };
        PROCESS_INFORMATION p_info = {};
        if (!CreateProcess(application_path.c_str(), full_command_path.data(), NULL, NULL, true, NULL, NULL, NULL, &info, &p_info))
        {
            DWORD error = GetLastError();
            std::wstring message = L"Keystroke Overlay failed to start with error: ";
            message += std::to_wstring(error);
            Logger::error(message);
        }
        
        m_hProcess = p_info.hProcess;
        CloseHandle(p_info.hThread);

        // Launch the hidden keystroke server executable located next to this module DLL
        // Resolve folder of this module using __ImageBase
        WCHAR modulePath[MAX_PATH] = { 0 };
        if (GetModuleFileNameW(reinterpret_cast<HMODULE>(&__ImageBase), modulePath, ARRAYSIZE(modulePath)) != 0)
        {
            std::wstring folder = modulePath;
            size_t pos = folder.find_last_of(L"\\/");
            if (pos != std::wstring::npos) folder.resize(pos + 1);

            std::wstring errrrrrr = L"Launching KeystrokeServer from folder: " + folder + L"\n";
            OutputDebugStringW(errrrrrr.c_str()); // visible in Visual Studio Output / DebugView
            Logger::trace(errrrrrr);

            std::wstring serverPath = folder + L"KeystrokeServer.exe"; // ensure exe is named exactly like this
            std::wstring serverCmd = serverPath + L" " + std::to_wstring(powertoys_pid);

            STARTUPINFOW si = {};
            si.cb = sizeof(si);
            si.dwFlags = STARTF_USESHOWWINDOW;
            si.wShowWindow = SW_HIDE;

            PROCESS_INFORMATION piServer = {};
            DWORD creationFlags = CREATE_NO_WINDOW;

            // CreateProcessW may modify the command line buffer, so pass a modifiable pointer
            if (!CreateProcessW(serverPath.c_str(), &serverCmd[0], nullptr, nullptr, FALSE, creationFlags, nullptr, nullptr, &si, &piServer))
            {
                DWORD err = GetLastError();
                std::wstring msg = L"KeystrokeServer failed to start with error: " + std::to_wstring(err);
                Logger::error(msg);
            }
            else
            {
                m_hServerProcess = piServer.hProcess;
                CloseHandle(piServer.hThread);
                Logger::trace(L"KeystrokeServer started successfully");
            }
        }
    }

    void terminate_process()
    {
        if (m_hProcess)
        {
            Logger::trace(L"Terminating Keystroke Overlay process");

            TerminateProcess(m_hProcess, 0);
            CloseHandle(m_hProcess);
            m_hProcess = nullptr;
        }
        if (m_hServerProcess)
        {
            Logger::trace(L"Terminating KeystrokeServer process");
            TerminateProcess(m_hServerProcess, 0);
            CloseHandle(m_hServerProcess);
            m_hServerProcess = nullptr;
        }
    }

public:
    // Constructor
    KeystrokeOverlay()
    {
        init_settings();
        app_name = MODULE_NAME;
        app_key = MODULE_KEY;
        LoggerHelpers::init_logger(app_key, L"ModuleInterface", "KeystrokeOverlay");
        Logger::info("Keystroke Overlay ModuleInterface object is constructing");
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

    virtual const wchar_t* get_key() override
    {
        return MODULE_KEY;
    }

    // Return array of the names of all events that this powertoy listens for, with
    // nullptr as the last element of the array. We no longer request ll_keyboard
    // events from the runner because the external KeystrokeServer.exe owns the
    // low-level keyboard hook and IPC delivery.
    virtual const wchar_t** get_events()
    {
        static const wchar_t* events[] = { win_hook_event, nullptr };
        return events;
    }

    // Return JSON with the configuration options.
    virtual bool get_config(wchar_t* buffer, int* buffer_size) override
    {
        HINSTANCE hinstance = reinterpret_cast<HINSTANCE>(&__ImageBase);

        // Create a Settings object.
        PowerToysSettings::Settings settings(hinstance, get_name());
        settings.set_description(MODULE_DESC);

        // Show an overview link in the Settings page
        // commented out for now
        //settings.set_overview_link(L"https://");

        // Show a video link in the Settings page.
        //settings.set_video_link(L"https://");

        // enable_draggable_overlay Toggle
        settings.add_bool_toggle(
            L"enable_draggable_overlay",   // property name.
            L"Enable Draggable Overlay",    // description or resource id of the localized string.
            g_settings.is_draggable         // property value.
        );

        settings.add_int_spinner(
            L"display_mode", // property name
            L"Display Mode", // description
            g_settings.display_mode, // current value
            0, // Min value (e.g., LastFiveKeystrokes)
            2, // Max value (e.g., ShortcutsOnly)
            1 // Step
        );

        settings.add_hotkey(
            L"switch_monitor_hotkey",
            L"Move to Next Monitor",
            g_settings.switch_monitor_hotkey
        );

        // Overlay Timeout Spinner
        settings.add_int_spinner(
            L"overlay_timeout",              // property name
            L"Overlay Timeout (ms)",        // description or resource id of the localized string.
            g_settings.overlay_timeout,     // property value.
            500,                            // Min
            10000,                          // Max
            100                             // Step
        );

        // 3. Text Size Spinner
        settings.add_int_spinner(
            L"text_size",                    // property name
            L"Text Font Size",              // description or resource id of the localized string.    
            g_settings.text_size,           // property value.
            10,                             // Min
            72,                             // Max
            2                               // Step
        );

        // 4. Text Opacity
        settings.add_int_spinner(
            L"text_opacity",                 // property name
            L"Text Opacity (%)",            // description or resource id of the localized string.
            g_settings.text_opacity,        // property value.
            0,                              // Min
            100,                            // Max
            5                               // Step
        );

        // 5. Background Opacity
        settings.add_int_spinner(
            L"background_opacity",           // property name
            L"Background Opacity (%)",      // description or resource id of the localized string.
            g_settings.bg_opacity,          // property value.
            0,                              // Min
            100,                            // Max
            5                               // Step
        );

        // 6. Text Color
        settings.add_color_picker(
            L"text_color",                   // property name.
            L"Text Color",                   // description or resource id of the localized string.
            g_settings.text_color            // property value.
        );

        // 7. Background Color
        settings.add_color_picker(
            L"background_color",               // property name.
            L"Background Color",               // description or resource id of the localized string.
            g_settings.bg_color                // property value.
        );

        return settings.serialize_to_buffer(buffer, buffer_size);
    }

    // Return the configured status for the gpo policy for the module
    virtual powertoys_gpo::gpo_rule_configured_t gpo_policy_enabled_configuration() override
    {
        return powertoys_gpo::gpo_rule_configured_t::gpo_rule_configured_not_configured;
        // return powertoys_gpo::getConfiguredKeystrokeOverlayEnabledValue();
    }

    // Called by the runner to pass the updated settings values as a serialized JSON.
    virtual void set_config(const wchar_t* config) override
    {
        try
        {
            // Parse the input JSON string.
            PowerToysSettings::PowerToyValues values =
                PowerToysSettings::PowerToyValues::from_json_string(config, get_key());

            if (auto v = values.get_bool_value(L"enable_draggable_overlay")) {
                g_settings.is_draggable = *v;
            }
            if (auto v = values.get_int_value(L"overlay_timeout")) {
                g_settings.overlay_timeout = *v;
            }
            if (auto v = values.get_int_value(L"text_size")) {
                g_settings.text_size = *v;
            }
            if (auto v = values.get_int_value(L"text_opacity")) {
                g_settings.text_opacity = *v;
            }
            if (auto v = values.get_int_value(L"background_opacity")) {
                g_settings.bg_opacity = *v;
            }
            if (auto v = values.get_string_value(L"text_color")) {
                g_settings.text_color = *v;
            }
            if (auto v = values.get_string_value(L"background_color")) {
                g_settings.bg_color = *v;
            }
            if (auto v = values.get_int_value(L"display_mode"))
            {
                g_settings.display_mode = *v;
            }
            if (auto v = values.get_json(L"switch_monitor_hotkey"))
            {
                g_settings.switch_monitor_hotkey = PowerToysSettings::HotkeyObject::from_json(*v);
            }

            // Save to disk so the C# App can read the updated settings.json
            // If you don't need to do any custom processing of the settings, proceed
            // to persists the values calling:
            values.save_to_settings_file();
            
            // Otherwise call a custom function to process the settings before saving them to disk:
            // save_settings();
        }
        catch (std::exception&)
        {
            // Improper JSON.
        }
    }

    // Enable the powertoy

    // where the keystrokeoverlay.exe is/should be launched
    virtual void enable()
    {
        launch_process();
        m_enabled = true;
        Trace::EnableKeystrokeOverlay(true);
    }

    // Disable the powertoy
    virtual void disable()
    {
        terminate_process();
        m_enabled = false;
        // External server handles batching; nothing else to stop here.
        Trace::EnableKeystrokeOverlay(false);
    }

    // Returns if the powertoys is enabled
    virtual bool is_enabled() override
    {
        return m_enabled;
    }

    // Handle incoming event, data is event-specific
    // virtual intptr_t signal_event(const wchar_t* name, intptr_t data)
    // {
    //    if (wcscmp(name, win_hook_event) == 0)
    //    {
    //        /* auto& event = *(reinterpret_cast<WinHookEvent*>(data)); */
    //        // Return value is ignored
    //        return 0;
    //    }
    //    return 0;
    // }
};

// Load the settings file.
void KeystrokeOverlay::init_settings()
{
    try
    {
        // Load and parse the settings file for this PowerToy.
        PowerToysSettings::PowerToyValues settings =
            PowerToysSettings::PowerToyValues::load_from_settings_file(KeystrokeOverlay::get_name());

        if (auto v = settings.get_bool_value(L"enable_draggable_overlay")) {
            g_settings.is_draggable = *v;
        }
        if (auto v = settings.get_int_value(L"overlay_timeout")) {
            g_settings.overlay_timeout = *v;
        }
        if (auto v = settings.get_int_value(L"text_size")) {
            g_settings.text_size = *v;
        }
        if (auto v = settings.get_int_value(L"text_opacity")) {
            g_settings.text_opacity = *v;
        }
        if (auto v = settings.get_int_value(L"background_opacity")) {
            g_settings.bg_opacity = *v;
        }
        if (auto v = settings.get_string_value(L"text_color")) {
            g_settings.text_color = *v;
        }
        if (auto v = settings.get_string_value(L"background_color")) {
            g_settings.bg_color = *v;
        }
        if (auto v = settings.get_int_value(L"display_mode"))
        {
            g_settings.display_mode = *v;
        }
        if (auto v = settings.get_json(L"switch_monitor_hotkey"))
        {
            g_settings.switch_monitor_hotkey = PowerToysSettings::HotkeyObject::from_json(*v);
        }
    }
    catch (std::exception&)
    {
        // Error while loading from the settings file. Let default values stay as they are.
    }
}

extern "C" __declspec(dllexport) PowertoyModuleIface* __cdecl powertoy_create()
{
    return new KeystrokeOverlay();
}