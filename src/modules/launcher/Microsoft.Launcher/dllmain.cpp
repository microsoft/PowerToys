#include "pch.h"
#include <interface/powertoy_module_interface.h>
#include <common/SettingsAPI/settings_objects.h>
#include <common/interop/shared_constants.h>
#include "trace.h"
#include "Generated Files/resource.h"
#include <launcher\Microsoft.Launcher\LauncherConstants.h>
#include <common/logger/logger.h>
#include <common/SettingsAPI/settings_helpers.h>

#include <common/utils/elevation.h>
#include <common/utils/process_path.h>
#include <common/utils/resources.h>
#include <common/utils/winapi_error.h>

#include <filesystem>
#include <mutex>

namespace
{
    const wchar_t POWER_LAUNCHER_PID_SHARED_FILE[] = L"Local\\PowerLauncherPidSharedFile-3cbfbad4-199b-4e2c-9825-942d5d3d3c74";
    const wchar_t JSON_KEY_PROPERTIES[] = L"properties";
    const wchar_t JSON_KEY_WIN[] = L"win";
    const wchar_t JSON_KEY_ALT[] = L"alt";
    const wchar_t JSON_KEY_CTRL[] = L"ctrl";
    const wchar_t JSON_KEY_SHIFT[] = L"shift";
    const wchar_t JSON_KEY_CODE[] = L"code";
    const wchar_t JSON_KEY_OPEN_POWERLAUNCHER[] = L"open_powerlauncher";
    const wchar_t JSON_KEY_USE_CENTRALIZED_KEYBOARD_HOOK[] = L"use_centralized_keyboard_hook";
}

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

// These are the properties shown in the Settings page.
struct ModuleSettings
{
} g_settings;

// Implement the PowerToy Module Interface and all the required methods.
class Microsoft_Launcher : public PowertoyModuleIface
{
private:
    // The PowerToy state.
    bool m_enabled = false;

    // Load initial settings from the persisted values.
    void init_settings();

    bool processStarting = false;
    std::mutex processStartingMutex;
    bool processStarted = false;

    //contains the name of the powerToys
    std::wstring app_name;

    //contains the non localized key of the powertoy
    std::wstring app_key;

    // Time to wait for process to close after sending WM_CLOSE signal
    static const int MAX_WAIT_MILLISEC = 10000;

    // Hotkey to invoke the module
    Hotkey m_hotkey = { .key = 0 };

    // If the centralized keyboard hook should be used to activate PowerToys Run
    bool m_use_centralized_keyboard_hook = false;

    // Helper function to extract the hotkey from the settings
    void parse_hotkey(PowerToysSettings::PowerToyValues& settings);

    // Handle to event used to invoke the Runner
    HANDLE m_hEvent;
    HANDLE m_hCentralizedKeyboardHookEvent;

    HANDLE send_telemetry_event;

    // Handle a case when a user started standalone PowerToys Run or for some reason the process is leaked
    void TerminateRunningInstance()
    {
        auto exitEvent = CreateEvent(nullptr, false, false, CommonSharedConstants::RUN_EXIT_EVENT);
        if (!exitEvent)
        {
            Logger::warn(L"Failed to create exitEvent. {}", get_last_error_or_default(GetLastError()));
        }
        else
        {
            Logger::trace(L"Signaled exitEvent");
            if (!SetEvent(exitEvent))
            {
                Logger::warn(L"Failed to signal exitEvent. {}", get_last_error_or_default(GetLastError()));
            }

            ResetEvent(exitEvent);
            CloseHandle(exitEvent);
        }
    }

public:
    // Constructor
    Microsoft_Launcher()
    {
        app_name = GET_RESOURCE_STRING(IDS_LAUNCHER_NAME);
        app_key = LauncherConstants::ModuleKey;
        std::filesystem::path logFilePath(PTSettingsHelper::get_module_save_folder_location(this->app_key));
        logFilePath.append(LogSettings::launcherLogPath);
        Logger::init(LogSettings::launcherLoggerName, logFilePath.wstring(), PTSettingsHelper::get_log_settings_file_location());
        Logger::info("Launcher object is constructing");
        init_settings();

        m_hEvent = CreateDefaultEvent(CommonSharedConstants::POWER_LAUNCHER_SHARED_EVENT);
        m_hCentralizedKeyboardHookEvent = CreateDefaultEvent(CommonSharedConstants::POWER_LAUNCHER_CENTRALIZED_HOOK_SHARED_EVENT);

        send_telemetry_event = CreateDefaultEvent(CommonSharedConstants::RUN_SEND_SETTINGS_TELEMETRY_EVENT);
    };

    ~Microsoft_Launcher()
    {
        Logger::info("Launcher object is destroying");
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
        return app_name.c_str();
    }

    // Return the non localized key of the powertoy, this will be cached by the runner
    virtual const wchar_t* get_key() override
    {
        return app_key.c_str();
    }

    // Return the configured status for the gpo policy for the module
    virtual powertoys_gpo::gpo_rule_configured_t gpo_policy_enabled_configuration() override
    {
        return powertoys_gpo::getConfiguredPowerLauncherEnabledValue();
    }

    // Return JSON with the configuration options.
    virtual bool get_config(wchar_t* buffer, int* buffer_size) override
    {
        HINSTANCE hinstance = reinterpret_cast<HINSTANCE>(&__ImageBase);

        // Create a Settings object.
        PowerToysSettings::Settings settings(hinstance, get_name());
        settings.set_description(GET_RESOURCE_STRING(IDS_LAUNCHER_SETTINGS_DESC));
        settings.set_overview_link(L"https://aka.ms/PowerToysOverview_PowerToysRun");

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
                PowerToysSettings::PowerToyValues::from_json_string(config, get_key());

            parse_hotkey(values);
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
    virtual void enable()
    {
        Logger::info("Microsoft_Launcher::enable() begin");

        // This synchronization code is here since we've seen logs of this function being entered twice in the same process/thread pair.
        // The theory here is that the call to ShellExecuteExW might be enabling some context switching that allows the low level keyboard hook to be run.
        // Ref: https://github.com/microsoft/PowerToys/issues/12908#issuecomment-986995633
        // We want only one instance to be started at the same time.
        processStartingMutex.lock();
        if (processStarting)
        {
            processStartingMutex.unlock();
            Logger::warn(L"Two PowerToys Run processes were trying to get started at the same time.");
            return;
        }
        else
        {
            processStarting = true;
            processStartingMutex.unlock();
        }

        ResetEvent(m_hCentralizedKeyboardHookEvent);
        ResetEvent(send_telemetry_event);

        unsigned long powertoys_pid = GetCurrentProcessId();
        TerminateRunningInstance();
        if (is_process_elevated(false))
        {
            Logger::trace("Starting PowerToys Run from elevated process");
            const auto modulePath = get_module_folderpath();
            std::wstring runExecutablePath = modulePath;
            std::wstring params;
            params += L" -powerToysPid " + std::to_wstring(powertoys_pid) + L" ";
            params += L"--started-from-runner ";
            runExecutablePath += L"\\PowerToys.PowerLauncher.exe";
            processStarted = RunNonElevatedFailsafe(runExecutablePath, params, modulePath).has_value();
        }
        else
        {
            Logger::trace("Starting PowerToys Run from not elevated process");
            std::wstring executable_args;
            executable_args += L" -powerToysPid ";
            executable_args += std::to_wstring(powertoys_pid);
            executable_args += L" --started-from-runner";

            SHELLEXECUTEINFOW sei{ sizeof(sei) };
            sei.fMask = { SEE_MASK_NOCLOSEPROCESS | SEE_MASK_FLAG_NO_UI };
            sei.lpFile = L"PowerToys.PowerLauncher.exe";
            sei.nShow = SW_SHOWNORMAL;
            sei.lpParameters = executable_args.data();

            if (ShellExecuteExW(&sei))
            {
                processStarted = true;
                Logger::trace("Started PowerToys Run");
            }
            else
            {
                Logger::error("Launcher failed to start");
            }
        }
        processStarting = false;
        m_enabled = true;
        Logger::info("Microsoft_Launcher::enable() end");
    }

    // Disable the powertoy
    virtual void disable()
    {
        Logger::info("Launcher is disabling");
        if (m_enabled)
        {
            TerminateRunningInstance();
            processStarted = false;
            ResetEvent(m_hEvent);
            ResetEvent(m_hCentralizedKeyboardHookEvent);
            ResetEvent(send_telemetry_event);
        }

        m_enabled = false;
    }

    // Returns if the powertoys is enabled
    virtual bool is_enabled() override
    {
        return m_enabled;
    }

    // Return the invocation hotkey
    virtual size_t get_hotkeys(Hotkey* hotkeys, size_t buffer_size) override
    {
        if (m_hotkey.key)
        {
            if (hotkeys && buffer_size >= 1)
            {
                hotkeys[0] = m_hotkey;
            }

            return 1;
        }
        else
        {
            return 0;
        }
    }

    // Process the hotkey event
    virtual bool on_hotkey(size_t /*hotkeyId*/) override
    {
        // For now, hotkeyId will always be zero
        if (m_enabled)
        {
            if (!processStarted)
            {
                Logger::warn("PowerToys Run hasn't been started. Starting PowerToys Run");
                enable();
            }

            /* Now, PowerToys Run uses a global hotkey so that it can get focus.
             * Activate it with the centralized keyboard hook only if the setting is on.*/
            if (m_use_centralized_keyboard_hook)
            {
                Logger::trace("Set POWER_LAUNCHER_SHARED_EVENT");
                SetEvent(m_hCentralizedKeyboardHookEvent);
                return true;
            }
        }

        return false;
    }

    // Callback to send WM_CLOSE signal to each top level window.
    static BOOL CALLBACK requestMainWindowClose(HWND nextWindow, LPARAM closePid)
    {
        DWORD windowPid;
        GetWindowThreadProcessId(nextWindow, &windowPid);

        if (windowPid == static_cast<DWORD>(closePid))
            ::PostMessage(nextWindow, WM_CLOSE, 0, 0);

        return true;
    }

    virtual void send_settings_telemetry() override
    {
        Logger::info("Send settings telemetry");
        SetEvent(send_telemetry_event);
    }
};

// Load the settings file.
void Microsoft_Launcher::init_settings()
{
    try
    {
        // Load and parse the settings file for this PowerToy.
        PowerToysSettings::PowerToyValues settings =
            PowerToysSettings::PowerToyValues::load_from_settings_file(get_key());

        parse_hotkey(settings);
    }
    catch (std::exception&)
    {
        // Error while loading from the settings file. Let default values stay as they are.
    }
}

void Microsoft_Launcher::parse_hotkey(PowerToysSettings::PowerToyValues& settings)
{
    m_use_centralized_keyboard_hook = false;
    auto settingsObject = settings.get_raw_json();
    if (settingsObject.GetView().Size())
    {
        try
        {
            auto jsonHotkeyObject = settingsObject.GetNamedObject(JSON_KEY_PROPERTIES).GetNamedObject(JSON_KEY_OPEN_POWERLAUNCHER);
            m_hotkey.win = jsonHotkeyObject.GetNamedBoolean(JSON_KEY_WIN);
            m_hotkey.alt = jsonHotkeyObject.GetNamedBoolean(JSON_KEY_ALT);
            m_hotkey.shift = jsonHotkeyObject.GetNamedBoolean(JSON_KEY_SHIFT);
            m_hotkey.ctrl = jsonHotkeyObject.GetNamedBoolean(JSON_KEY_CTRL);
            m_hotkey.key = static_cast<unsigned char>(jsonHotkeyObject.GetNamedNumber(JSON_KEY_CODE));
        }
        catch (...)
        {
            Logger::error("Failed to initialize PT Run start shortcut");
        }
        try
        {
            auto jsonPropertiesObject = settingsObject.GetNamedObject(JSON_KEY_PROPERTIES);
            m_use_centralized_keyboard_hook =jsonPropertiesObject.GetNamedBoolean(JSON_KEY_USE_CENTRALIZED_KEYBOARD_HOOK);
        }
        catch (...)
        {
            Logger::warn("Failed to get centralized keyboard hook setting");
        }
    }
    else
    {
        Logger::info("PT Run settings are empty");
    }

    if (!m_hotkey.key)
    {
        Logger::info("PT Run is going to use default shortcut");
        m_hotkey.win = false;
        m_hotkey.alt = true;
        m_hotkey.shift = false;
        m_hotkey.ctrl = false;
        m_hotkey.key = VK_SPACE;
    }
}

extern "C" __declspec(dllexport) PowertoyModuleIface* __cdecl powertoy_create()
{
    return new Microsoft_Launcher();
}
