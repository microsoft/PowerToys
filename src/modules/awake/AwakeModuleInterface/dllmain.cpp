#include "pch.h"
#include <interface/powertoy_module_interface.h>
#include <common/SettingsAPI/settings_objects.h>
#include <common/interop/shared_constants.h>
#include "trace.h"
#include "resource.h"
#include "AwakeConstants.h"
#include <common/logger/logger.h>
#include <common/SettingsAPI/settings_helpers.h>

#include <common/utils/elevation.h>
#include <common/utils/process_path.h>
#include <common/utils/resources.h>
#include <common/utils/os-detect.h>
#include <common/utils/winapi_error.h>

#include <filesystem>
#include <set>

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

const static wchar_t* MODULE_NAME = L"Awake";
const static wchar_t* MODULE_DESC = L"A module that keeps your computer awake on-demand.";

class Awake : public PowertoyModuleIface
{
    std::wstring app_name;
    std::wstring app_key;

private:
    // How long disable() waits for Awake to exit after signaling it before ending it forcibly.
    static constexpr DWORD exit_timeout_ms = 3000;

    bool m_enabled = false;
    HANDLE m_process = nullptr;

    bool is_process_running() const
    {
        return m_process != nullptr && WaitForSingleObject(m_process, 0) == WAIT_TIMEOUT;
    }

    void close_process_handle()
    {
        if (m_process != nullptr)
        {
            CloseHandle(m_process);
            m_process = nullptr;
        }
    }

    bool launch_process()
    {
        if (is_process_running())
        {
            Logger::trace(L"PowerToys Awake is already running");
            return true;
        }
        close_process_handle();

        // Launch by full path from this DLL's folder, not relative to the runner's working directory.
        std::filesystem::path executable = get_module_folderpath(reinterpret_cast<HMODULE>(&__ImageBase));
        executable /= L"PowerToys.Awake.exe";
        std::wstring command_line = L"\"" + executable.wstring() + L"\" --use-pt-config --pid " + std::to_wstring(GetCurrentProcessId());
        Logger::trace(L"Launching PowerToys Awake: {}", command_line);

        STARTUPINFO startup_info = { sizeof(startup_info) };
        PROCESS_INFORMATION process_info = {};
        // Awake is a console program for its command line. Here it gets no console at all:
        // CREATE_NO_WINDOW would still start a hidden console host that lives as long as Awake.
        // It inherits none of the runner's handles.
        if (!CreateProcess(executable.c_str(), command_line.data(), nullptr, nullptr, FALSE, DETACHED_PROCESS, nullptr, nullptr, &startup_info, &process_info))
        {
            Logger::error(L"PowerToys Awake failed to start. {}", get_last_error_or_default(GetLastError()));
            return false;
        }

        CloseHandle(process_info.hThread);
        m_process = process_info.hProcess;
        return true;
    }

    void stop_process()
    {
        if (!is_process_running())
        {
            close_process_handle();
            return;
        }

        // Awake creates this manual-reset event at startup; if it has not yet, creating it here
        // with the same reset mode keeps the signal until Awake starts waiting for it.
        HANDLE exit_event = CreateEvent(nullptr, TRUE, FALSE, CommonSharedConstants::AWAKE_EXIT_EVENT);
        bool exited = false;
        if (exit_event == nullptr || !SetEvent(exit_event))
        {
            Logger::warn(L"Failed to signal PowerToys Awake to exit. {}", get_last_error_or_default(GetLastError()));
        }
        else
        {
            exited = WaitForSingleObject(m_process, exit_timeout_ms) == WAIT_OBJECT_0;
        }

        if (!exited)
        {
            Logger::warn(L"PowerToys Awake did not exit in time; terminating it.");
            TerminateProcess(m_process, 1);
            WaitForSingleObject(m_process, exit_timeout_ms);
        }

        if (exit_event != nullptr)
        {
            CloseHandle(exit_event);
        }
        close_process_handle();
    }

public:
    Awake()
    {
        app_name = GET_RESOURCE_STRING(IDS_AWAKE_NAME);
        app_key = AwakeConstants::ModuleKey;
        std::filesystem::path logFilePath(PTSettingsHelper::get_module_save_folder_location(this->app_key));
        logFilePath.append(LogSettings::awakeLogPath);
        Logger::init(LogSettings::launcherLoggerName, logFilePath.wstring(), PTSettingsHelper::get_log_settings_file_location());
        Logger::info("Launcher object is constructing");
    };

    // Return the configured status for the gpo policy for the module
    virtual powertoys_gpo::gpo_rule_configured_t gpo_policy_enabled_configuration() override
    {
        return powertoys_gpo::getConfiguredAwakeEnabledValue();
    }

    virtual void destroy() override
    {
        // Awake exits by itself when the runner process ends; only the handle needs releasing.
        close_process_handle();
        delete this;
    }

    virtual const wchar_t* get_name() override
    {
        return MODULE_NAME;
    }

    virtual bool get_config(wchar_t* buffer, int* buffer_size) override
    {
        HINSTANCE hinstance = reinterpret_cast<HINSTANCE>(&__ImageBase);

        PowerToysSettings::Settings settings(hinstance, get_name());
        settings.set_description(MODULE_DESC);

        return settings.serialize_to_buffer(buffer, buffer_size);
    }

    virtual const wchar_t* get_key() override
    {
        return app_key.c_str();
    }

    virtual void set_config(const wchar_t* config) override
    {
        try
        {
            // Parse the input JSON string.
            PowerToysSettings::PowerToyValues values =
                PowerToysSettings::PowerToyValues::from_json_string(config, get_key());

            values.save_to_settings_file();

            // Awake picks the new settings up from the file; start it again if it is not running.
            if (m_enabled && !is_process_running())
            {
                launch_process();
            }
        }
        catch (std::exception&)
        {
            // Improper JSON.
        }
    }

    virtual void enable() override
    {
        Trace::EnableAwake(true);
        // The user's choice stands even if this launch fails; set_config retries it.
        m_enabled = true;
        launch_process();
    }

    virtual void disable() override
    {
        if (m_enabled)
        {
            Trace::EnableAwake(false);
            Logger::trace(L"Disabling Awake...");
            stop_process();
        }

        m_enabled = false;
    }

    virtual bool is_enabled() override
    {
        return m_enabled;
    }
};

extern "C" __declspec(dllexport) PowertoyModuleIface* __cdecl powertoy_create()
{
    return new Awake();
}