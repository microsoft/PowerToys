#include "pch.h"

#include <modules/interface/powertoy_module_interface.h>
#include <common/SettingsAPI/settings_objects.h>

#include "trace.h"
#include <common/logger/logger.h>
#include <common/utils/logger_helper.h>
#include <common/utils/resources.h>
#include <common/utils/winapi_error.h>
#include <common/interop/shared_constants.h>

#include <shellapi.h>
#include <common/interop/shared_constants.h>

#include <atomic>

namespace NonLocalizable
{
    const wchar_t ModulePath[] = L"PowerToys.ZoomIt.exe";
    const inline wchar_t ModuleKey[] = L"ZoomIt";
}

BOOL APIENTRY DllMain( HMODULE /*hModule*/,
                       DWORD  ul_reason_for_call,
                       LPVOID /*lpReserved*/
                     )
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

class ZoomItModuleInterface : public PowertoyModuleIface
{
public:
    // Return the localized display name of the powertoy
    virtual PCWSTR get_name() override
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
        return powertoys_gpo::getConfiguredZoomItEnabledValue();
    }

    // Return JSON with the configuration options.
    // These are the settings shown on the settings page along with their current values.
    virtual bool get_config(wchar_t* buffer, int* buffer_size) override
    {
        HINSTANCE hinstance = reinterpret_cast<HINSTANCE>(&__ImageBase);

        // TODO: Read settings from Registry.

        // Create a Settings object.
        PowerToysSettings::Settings settings(hinstance, get_name());

        return settings.serialize_to_buffer(buffer, buffer_size);
    }

    // Passes JSON with the configuration settings for the powertoy.
    // This is called when the user hits Save on the settings page.
    virtual void set_config(const wchar_t*) override
    {
        try
        {
            // Parse the input JSON string.
            // TODO: Save settings to registry.
        }
        catch (std::exception&)
        {
            // Improper JSON.
        }
    }

    // Enable the powertoy
    virtual void enable()
    {
        Logger::info("ZoomIt enabling");
        Enable();
    }

    // Disable the powertoy
    virtual void disable()
    {
        Logger::info("ZoomIt disabling");
        Disable(true);
    }

    // Returns if the powertoy is enabled
    virtual bool is_enabled() override
    {
        return m_enabled;
    }

    // Destroy the powertoy and free memory
    virtual void destroy() override
    {
        Disable(false);
        delete this;
    }

    ZoomItModuleInterface()
    {
        app_name = L"ZoomIt";
        app_key = NonLocalizable::ModuleKey;
        LoggerHelpers::init_logger(app_key, L"ModuleInterface", LogSettings::zoomItLoggerName);
        m_reload_settings_event_handle = CreateDefaultEvent(CommonSharedConstants::ZOOMIT_REFRESH_SETTINGS_EVENT);
        m_exit_event_handle = CreateDefaultEvent(CommonSharedConstants::ZOOMIT_EXIT_EVENT);
    }

private:
    bool is_enabled_by_default() const override
    {
        return false;
    }

    // Called by the thread pool when the ZoomIt process exits.
    static void CALLBACK OnZoomItProcessExited(PVOID lpParameter, BOOLEAN /*TimerOrWaitFired*/)
    {
        reinterpret_cast<ZoomItModuleInterface*>(lpParameter)->HandleUnexpectedExit();
    }

    // Handles an unexpected ZoomIt process exit by restarting it when still enabled.
    void HandleUnexpectedExit()
    {
        // Atomically clear the restart flag. If it was already cleared (e.g. by Disable()),
        // another thread got here first or an intentional exit is in progress — do nothing.
        if (!m_restartOnCrash.exchange(false))
        {
            return;
        }

        // The wait handle is auto-deregistered because WT_EXECUTEONLYONCE was used.
        m_processWaitHandle = NULL;

        if (!m_enabled)
        {
            // Disable() was called concurrently; do not restart.
            return;
        }

        Logger::error(L"ZoomIt process exited unexpectedly. Attempting to restart...");

        // Atomically take ownership of the stale process handle to avoid a double-close
        // race with Disable(), then release it.
        HANDLE hOldProcess = reinterpret_cast<HANDLE>(
            InterlockedExchangePointer(reinterpret_cast<PVOID*>(&m_hProcess), nullptr));
        if (hOldProcess)
        {
            CloseHandle(hOldProcess);
        }

        Enable();
    }

    void Enable()
    {
        m_enabled = true;

        // Cancel any previous crash-monitoring wait before starting a new process.
        if (m_processWaitHandle)
        {
            UnregisterWait(m_processWaitHandle);
            m_processWaitHandle = NULL;
        }

        // Close any leftover process handle from a previous run.
        if (m_hProcess)
        {
            CloseHandle(m_hProcess);
            m_hProcess = nullptr;
        }

        // Log telemetry
        Trace::EnableZoomIt(true);

        // Pass the PID.
        unsigned long powertoys_pid = GetCurrentProcessId();
        std::wstring executable_args = L"";
        executable_args.append(std::to_wstring(powertoys_pid));

        ResetEvent(m_reload_settings_event_handle);
        ResetEvent(m_exit_event_handle);

        SHELLEXECUTEINFOW sei{ sizeof(sei) };
        sei.fMask = { SEE_MASK_NOCLOSEPROCESS | SEE_MASK_FLAG_NO_UI };
        sei.lpFile = NonLocalizable::ModulePath;
        sei.nShow = SW_SHOWNORMAL;
        sei.lpParameters = executable_args.data();
        if (ShellExecuteExW(&sei) == false)
        {
            Logger::error(L"Failed to start zoomIt");
            auto message = get_last_error_message(GetLastError());
            if (message.has_value())
            {
                Logger::error(message.value());
            }
        }
        else
        {
            m_hProcess = sei.hProcess;

            // Register a one-shot thread-pool wait so we are notified if ZoomIt exits
            // unexpectedly (e.g. due to a crash) and can restart it automatically.
            m_restartOnCrash = true;
            if (!RegisterWaitForSingleObject(
                    &m_processWaitHandle,
                    m_hProcess,
                    OnZoomItProcessExited,
                    this,
                    INFINITE,
                    WT_EXECUTEONLYONCE))
            {
                m_restartOnCrash = false;
                Logger::error(L"Failed to register ZoomIt crash monitor: {}", get_last_error_or_default(GetLastError()));
            }
        }
    }

    void Disable(bool const traceEvent)
    {
        m_enabled = false;

        // Signal that any pending crash-monitoring callback must not restart ZoomIt.
        m_restartOnCrash = false;

        // Unregister the crash-monitoring wait (non-blocking; a callback already in
        // flight will still see m_restartOnCrash == false and bail out early).
        if (m_processWaitHandle)
        {
            UnregisterWait(m_processWaitHandle);
            m_processWaitHandle = NULL;
        }

        // Log telemetry
        if (traceEvent)
        {
            Trace::EnableZoomIt(false);
        }

        // Tell the ZoomIt process to exit.
        SetEvent(m_exit_event_handle);

        ResetEvent(m_reload_settings_event_handle);

        if (m_hProcess)
        {
            // Atomically take ownership of the process handle to avoid a double-close
            // race with HandleUnexpectedExit().
            HANDLE hProcess = reinterpret_cast<HANDLE>(
                InterlockedExchangePointer(reinterpret_cast<PVOID*>(&m_hProcess), nullptr));
            if (hProcess)
            {
                // Wait for 1.5 seconds for the process to end correctly and stop etw tracer
                WaitForSingleObject(hProcess, 1500);

                // If process is still running, terminate it
                TerminateProcess(hProcess, 0);
                CloseHandle(hProcess);
            }
        }
    }

    bool is_process_running()
    {
        return m_hProcess != nullptr && WaitForSingleObject(m_hProcess, 0) == WAIT_TIMEOUT;
    }

    virtual void call_custom_action(const wchar_t* action) override
    {
        try
        {
            PowerToysSettings::CustomActionObject action_object =
                PowerToysSettings::CustomActionObject::from_json_string(action);

            if (action_object.get_name() == L"refresh_settings")
            {
                SetEvent(m_reload_settings_event_handle);
            }
        }
        catch (std::exception&)
        {
            Logger::error(L"Failed to parse action. {}", action);
        }
    }

    std::wstring app_name;
    std::wstring app_key; //contains the non localized key of the powertoy

    bool m_enabled = false;
    HANDLE m_hProcess = nullptr;
    HANDLE m_processWaitHandle = NULL;
    std::atomic<bool> m_restartOnCrash{ false };

    HANDLE m_reload_settings_event_handle = NULL;
    HANDLE m_exit_event_handle = NULL;
};

extern "C" __declspec(dllexport) PowertoyModuleIface* __cdecl powertoy_create()
{
    return new ZoomItModuleInterface();
}
