// dllmain.cpp : Defines the entry point for the DLL application.
#include "pch.h"
#include <interface/powertoy_module_interface.h>
#include <common/SettingsAPI/settings_objects.h>
#include "trace.h"
#include <common/interop/shared_constants.h>
#include <common/utils/string_utils.h>
#include <common/utils/winapi_error.h>
#include <common/utils/logger_helper.h>
#include <common/utils/resources.h>
#include <common/utils/process_path.h>

#include "resource.h"
#include "Constants.h"

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

const static wchar_t* MODULE_NAME = L"PowerDisplay";
const static wchar_t* MODULE_DESC = L"A utility to manage display brightness and color temperature across multiple monitors.";

namespace
{
    const wchar_t JSON_KEY_PROPERTIES[] = L"properties";
    const wchar_t JSON_KEY_ENABLED[] = L"enabled";
    const wchar_t JSON_KEY_HOTKEY_ENABLED[] = L"hotkey_enabled";
}

class PowerDisplayModule : public PowertoyModuleIface
{

private:
    bool m_enabled = false;
    bool m_hotkey_enabled = false;

    PROCESS_INFORMATION p_info = {};

    bool is_process_running()
    {
        return WaitForSingleObject(p_info.hProcess, 0) == WAIT_TIMEOUT;
    }

    bool graceful_shutdown_process()
    {
        if (!is_process_running())
        {
            return true; // Already not running
        }

        try
        {
            // Send WM_CLOSE message to the main window
            DWORD processId = GetProcessId(p_info.hProcess);
            if (processId != 0)
            {
                // Find the main window of the PowerDisplay process
                HWND hwnd = find_main_window(processId);
                if (hwnd != NULL)
                {
                    Logger::trace(L"Sending WM_CLOSE to PowerDisplay window");
                    PostMessage(hwnd, WM_CLOSE, 0, 0);

                    // Wait up to 2 seconds for graceful shutdown
                    DWORD wait_result = WaitForSingleObject(p_info.hProcess, 2000);
                    if (wait_result == WAIT_OBJECT_0)
                    {
                        return true; // Process exited gracefully
                    }

                    // If WM_CLOSE didn't work, try WM_QUIT
                    Logger::trace(L"WM_CLOSE failed, trying WM_QUIT");
                    PostMessage(hwnd, WM_QUIT, 0, 0);
                    wait_result = WaitForSingleObject(p_info.hProcess, 1000);
                    if (wait_result == WAIT_OBJECT_0)
                    {
                        return true;
                    }
                }
            }
        }
        catch (...)
        {
            Logger::error(L"Exception during graceful shutdown attempt");
        }

        return false; // Graceful shutdown failed
    }

    struct EnumWindowsData
    {
        DWORD processId;
        HWND foundWindow;
    };

    static BOOL CALLBACK enum_windows_callback(HWND hwnd, LPARAM lParam)
    {
        EnumWindowsData* data = reinterpret_cast<EnumWindowsData*>(lParam);
        DWORD windowProcessId;
        GetWindowThreadProcessId(hwnd, &windowProcessId);

        if (windowProcessId == data->processId)
        {
            // Check if this is a main window (visible and has no parent)
            if (IsWindowVisible(hwnd) && GetParent(hwnd) == NULL)
            {
                wchar_t className[256];
                GetClassName(hwnd, className, sizeof(className) / sizeof(wchar_t));

                // Look for WinUI3 window class or PowerDisplay specific window
                if (wcsstr(className, L"WindowsForms") ||
                    wcsstr(className, L"WinUIDesktopWin32WindowClass") ||
                    wcsstr(className, L"PowerDisplay"))
                {
                    data->foundWindow = hwnd;
                    return FALSE; // Stop enumeration
                }
            }
        }
        return TRUE; // Continue enumeration
    }

    HWND find_main_window(DWORD processId)
    {
        EnumWindowsData data = { processId, NULL };
        EnumWindows(enum_windows_callback, reinterpret_cast<LPARAM>(&data));
        return data.foundWindow;
    }

    void launch_process()
    {
        if (m_enabled)
        {
            Logger::trace(L"Starting Power Display process");
            unsigned long powertoys_pid = GetCurrentProcessId();

            std::wstring executable_args = L"--pid " + std::to_wstring(powertoys_pid);
            std::wstring application_path = L"WinUI3Apps\\PowerToys.PowerDisplay.exe";
            std::wstring full_command_path = application_path + L" " + executable_args;
            Logger::trace(L"PowerDisplay launching with parameters: " + executable_args);

            STARTUPINFO info = { sizeof(info) };

            if (!CreateProcess(NULL, full_command_path.data(), NULL, NULL, true, NULL, NULL, NULL, &info, &p_info))
            {
                DWORD error = GetLastError();
                std::wstring message = L"PowerDisplay failed to start with error: ";
                message += std::to_wstring(error);
                Logger::error(message);
            }
            else
            {
                Logger::trace("Successfully started the PowerDisplay process");
            }
        }
    }


    void parse_hotkey_settings(PowerToysSettings::PowerToyValues settings)
    {
        auto settingsObject = settings.get_raw_json();
        if (settingsObject.GetView().Size())
        {
            try
            {
                // Check if properties object exists before accessing it
                if (settingsObject.HasKey(JSON_KEY_PROPERTIES))
                {
                    auto properties = settingsObject.GetNamedObject(JSON_KEY_PROPERTIES);
                    // Use the overload with default value to avoid exception
                    m_hotkey_enabled = properties.GetNamedBoolean(JSON_KEY_HOTKEY_ENABLED, false);
                }
                else
                {
                    Logger::info("Properties object not found in settings, using defaults");
                    m_hotkey_enabled = false;
                }
            }
            catch (...)
            {
                Logger::info("Failed to parse hotkey settings, using defaults");
                m_hotkey_enabled = false;  // Use default value
            }
        }
        else
        {
            Logger::info("Power Display settings are empty");
            m_hotkey_enabled = false;  // Use default value
        }
    }

    // Load the settings file.
    void init_settings()
    {
        try
        {
            // Load and parse the settings file for this PowerToy.
            PowerToysSettings::PowerToyValues settings =
                PowerToysSettings::PowerToyValues::load_from_settings_file(get_key());

            parse_hotkey_settings(settings);
        }
        catch (std::exception&)
        {
            Logger::error("Invalid json when trying to load the Power Display settings json from file.");
        }
    }

public:
    PowerDisplayModule()
    {
        LoggerHelpers::init_logger(MODULE_NAME, L"ModuleInterface", "PowerDisplay");
        Logger::info("Power Display object is constructing");

        init_settings();
    }

    ~PowerDisplayModule()
    {
        if (m_enabled)
        {
            TerminateProcess(p_info.hProcess, 1);
            CloseHandle(p_info.hProcess);
            CloseHandle(p_info.hThread);
        }
        m_enabled = false;
    }

    // Destroy the powertoy and free memory
    virtual void destroy() override
    {
        delete this;
    }

    // Return the localized display name of the powertoy
    virtual const wchar_t* get_name() override
    {
        return MODULE_NAME;
    }

    // Return the non localized key of the powertoy, this will be cached by the runner
    virtual const wchar_t* get_key() override
    {
        return MODULE_NAME;
    }

    // Return the configured status for the gpo policy for the module
    virtual powertoys_gpo::gpo_rule_configured_t gpo_policy_enabled_configuration() override
    {
        return powertoys_gpo::gpo_rule_configured_not_configured;
    }

    // Return JSON with the configuration options.
    virtual bool get_config(wchar_t* buffer, int* buffer_size) override
    {
        HINSTANCE hinstance = reinterpret_cast<HINSTANCE>(&__ImageBase);

        // Create a Settings object.
        PowerToysSettings::Settings settings(hinstance, get_name());
        settings.set_description(MODULE_DESC);

        return settings.serialize_to_buffer(buffer, buffer_size);
    }

    // Pop open the app, if the OOBE page asks it to
    virtual void call_custom_action(const wchar_t* action) override
    {
        try
        {
            PowerToysSettings::CustomActionObject action_object =
                PowerToysSettings::CustomActionObject::from_json_string(action);

            if (action_object.get_name() == L"Launch")
            {
                if (is_process_running())
                {
                    Logger::trace(L"PowerDisplay process is already running. Skipping launch.");
                }
                else
                {
                    launch_process();
                }
                Trace::ActivatePowerDisplay();
            }
        }
        catch (std::exception&)
        {
            Logger::error(L"Failed to parse action. {}", action);
        }
    }

    // Called by the runner to pass the updated settings values as a serialized JSON.
    virtual void set_config(const wchar_t* config) override
    {
        try
        {
            // Parse the input JSON string.
            PowerToysSettings::PowerToyValues values = PowerToysSettings::PowerToyValues::from_json_string(config, get_key());

            parse_hotkey_settings(values);

            values.save_to_settings_file();
        }
        catch (std::exception&)
        {
            Logger::error(L"Invalid json when trying to parse Power Display settings json.");
        }
    }

    // Enable the powertoy
    virtual void enable()
    {
        m_enabled = true;
        if (!is_process_running())
        {
            launch_process();  // Start the PowerDisplay process
        }
        else
        {
            Logger::trace(L"PowerDisplay process is already running. Skipping launch on enable.");
        }
        Logger::trace(L"PowerDisplay enabled");
    };

    virtual void disable()
    {
        if (m_enabled)
        {
            Logger::trace(L"Disabling Power Display...");

            // Try graceful shutdown first
            if (graceful_shutdown_process())
            {
                Logger::trace(L"PowerDisplay shutdown gracefully");
            }
            else
            {
                Logger::trace(L"PowerDisplay graceful shutdown failed, forcing termination");
                // Fallback to force termination
                TerminateProcess(p_info.hProcess, 1);
            }

            // Clean up handles
            CloseHandle(p_info.hProcess);
            CloseHandle(p_info.hThread);
        }

        m_enabled = false;
    }

    // Returns if the powertoys is enabled
    virtual bool is_enabled() override
    {
        return m_enabled;
    }

    // Respond to a "click" from the launcher
    virtual bool on_hotkey(size_t /*hotkeyId*/) override
    {
        if (m_enabled)
        {
            Logger::trace(L"Power Display hotkey pressed");
            if (is_process_running())
            {
                TerminateProcess(p_info.hProcess, 1);
            }
            else
            {
                launch_process();
            }

            return true;
        }

        return false;
    }
};

extern "C" __declspec(dllexport) PowertoyModuleIface* __cdecl powertoy_create()
{
    return new PowerDisplayModule();
}