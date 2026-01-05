// dllmain.cpp : Defines the entry point for the DLL Application.
#include "pch.h"
#include <interface/powertoy_module_interface.h>
#include <common/SettingsAPI/settings_objects.h>
#include "trace.h"
#include <common/interop/shared_constants.h>
#include <common/utils/string_utils.h>
#include <common/utils/winapi_error.h>
#include <common/utils/logger_helper.h>
#include <common/utils/resources.h>

#include "resource.h"

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

class PowerDisplayModule : public PowertoyModuleIface
{
private:
    bool m_enabled = false;

    // Windows Events for IPC (persistent handles - ColorPicker pattern)
    // Note: SettingsUpdatedEvent and HotkeyUpdatedEvent are NOT created here.
    // They are created on-demand by EventHelper.SignalEvent() in Settings UI
    // and NativeEventWaiter.WaitForEventLoop() in PowerDisplay.exe.
    HANDLE m_hProcess = nullptr;
    HANDLE m_hToggleEvent = nullptr;
    HANDLE m_hTerminateEvent = nullptr;
    HANDLE m_hRefreshEvent = nullptr;
    HANDLE m_hApplyProfileEvent = nullptr;
    HANDLE m_hSendSettingsTelemetryEvent = nullptr;

    // Helper method to check if PowerDisplay.exe process is still running
    bool is_process_running()
    {
        if (m_hProcess == nullptr)
        {
            Logger::trace(L"is_process_running: Process handle is null, returning false");
            return false;
        }
        DWORD waitResult = WaitForSingleObject(m_hProcess, 0);
        bool running = (waitResult == WAIT_TIMEOUT);
        Logger::trace(L"is_process_running: WaitForSingleObject returned {}, process running={}", waitResult, running);
        return running;
    }

    // Helper method to launch PowerDisplay.exe process
    void launch_process()
    {
        Logger::info(L"launch_process: Starting PowerDisplay process");
        unsigned long powertoys_pid = GetCurrentProcessId();
        Logger::trace(L"launch_process: PowerToys runner PID={}", powertoys_pid);

        std::wstring executable_args = std::to_wstring(powertoys_pid);
        Logger::trace(L"launch_process: Executable args: {}", executable_args);

        SHELLEXECUTEINFOW sei{ sizeof(sei) };
        sei.fMask = SEE_MASK_NOCLOSEPROCESS | SEE_MASK_FLAG_NO_UI;
        sei.lpFile = L"WinUI3Apps\\PowerToys.PowerDisplay.exe";
        sei.nShow = SW_SHOWNORMAL;
        sei.lpParameters = executable_args.data();

        Logger::trace(L"launch_process: Calling ShellExecuteExW with lpFile={}", sei.lpFile);

        if (ShellExecuteExW(&sei))
        {
            Logger::info(L"launch_process: Successfully started PowerDisplay process, handle={}", reinterpret_cast<void*>(sei.hProcess));
            m_hProcess = sei.hProcess;
        }
        else
        {
            DWORD lastError = GetLastError();
            Logger::error(L"launch_process: PowerDisplay process failed to start. Error code={}, message: {}",
                         lastError, get_last_error_or_default(lastError));
        }
    }

    // Helper method to ensure PowerDisplay process is running
    // Checks if process is running, launches it if needed
    // Note: No wait needed - PowerDisplay uses RedirectActivationToAsync for single instance
    void EnsureProcessRunning()
    {
        Logger::trace(L"EnsureProcessRunning: Checking if PowerDisplay process is running");
        if (!is_process_running())
        {
            Logger::info(L"EnsureProcessRunning: PowerDisplay process not running, launching");
            launch_process();
            Logger::info(L"EnsureProcessRunning: Launch completed");
        }
        else
        {
            Logger::trace(L"EnsureProcessRunning: PowerDisplay process is already running");
        }
    }

public:
    PowerDisplayModule()
    {
        LoggerHelpers::init_logger(MODULE_NAME, L"ModuleInterface", LogSettings::powerDisplayLoggerName);
        Logger::info("Power Display module is constructing");

        // Create all Windows Events (persistent handles - ColorPicker pattern)
        Logger::trace(L"Creating Windows Events for IPC...");
        m_hToggleEvent = CreateDefaultEvent(CommonSharedConstants::TOGGLE_POWER_DISPLAY_EVENT);
        Logger::trace(L"Created TOGGLE_POWER_DISPLAY_EVENT: handle={}", reinterpret_cast<void*>(m_hToggleEvent));
        m_hTerminateEvent = CreateDefaultEvent(CommonSharedConstants::TERMINATE_POWER_DISPLAY_EVENT);
        Logger::trace(L"Created TERMINATE_POWER_DISPLAY_EVENT: handle={}", reinterpret_cast<void*>(m_hTerminateEvent));
        m_hRefreshEvent = CreateDefaultEvent(CommonSharedConstants::REFRESH_POWER_DISPLAY_MONITORS_EVENT);
        Logger::trace(L"Created REFRESH_MONITORS_EVENT: handle={}", reinterpret_cast<void*>(m_hRefreshEvent));
        m_hApplyProfileEvent = CreateDefaultEvent(CommonSharedConstants::APPLY_PROFILE_POWER_DISPLAY_EVENT);
        Logger::trace(L"Created APPLY_PROFILE_EVENT: handle={}", reinterpret_cast<void*>(m_hApplyProfileEvent));
        m_hSendSettingsTelemetryEvent = CreateDefaultEvent(CommonSharedConstants::POWER_DISPLAY_SEND_SETTINGS_TELEMETRY_EVENT);
        Logger::trace(L"Created SEND_SETTINGS_TELEMETRY_EVENT: handle={}", reinterpret_cast<void*>(m_hSendSettingsTelemetryEvent));

        if (!m_hToggleEvent || !m_hTerminateEvent || !m_hRefreshEvent || !m_hApplyProfileEvent || !m_hSendSettingsTelemetryEvent)
        {
            Logger::error(L"Failed to create one or more event handles: Toggle={}, Terminate={}, Refresh={}, ApplyProfile={}, SettingsTelemetry={}",
                         reinterpret_cast<void*>(m_hToggleEvent),
                         reinterpret_cast<void*>(m_hTerminateEvent), reinterpret_cast<void*>(m_hRefreshEvent),
                         reinterpret_cast<void*>(m_hApplyProfileEvent), reinterpret_cast<void*>(m_hSendSettingsTelemetryEvent));
        }
        else
        {
            Logger::info(L"All Windows Events created successfully");
        }
    }

    ~PowerDisplayModule()
    {
        if (m_enabled)
        {
            disable();
        }

        // Clean up all event handles
        if (m_hToggleEvent)
        {
            CloseHandle(m_hToggleEvent);
            m_hToggleEvent = nullptr;
        }
        if (m_hTerminateEvent)
        {
            CloseHandle(m_hTerminateEvent);
            m_hTerminateEvent = nullptr;
        }
        if (m_hRefreshEvent)
        {
            CloseHandle(m_hRefreshEvent);
            m_hRefreshEvent = nullptr;
        }
        if (m_hApplyProfileEvent)
        {
            CloseHandle(m_hApplyProfileEvent);
            m_hApplyProfileEvent = nullptr;
        }
        if (m_hSendSettingsTelemetryEvent)
        {
            CloseHandle(m_hSendSettingsTelemetryEvent);
            m_hSendSettingsTelemetryEvent = nullptr;
        }
    }

    virtual void destroy() override
    {
        Logger::trace("PowerDisplay::destroy()");
        delete this;
    }

    virtual const wchar_t* get_name() override
    {
        return MODULE_NAME;
    }

    virtual const wchar_t* get_key() override
    {
        return MODULE_NAME;
    }

    virtual powertoys_gpo::gpo_rule_configured_t gpo_policy_enabled_configuration() override
    {
        return powertoys_gpo::getConfiguredPowerDisplayEnabledValue();
    }

    virtual bool get_config(wchar_t* buffer, int* buffer_size) override
    {
        HINSTANCE hinstance = reinterpret_cast<HINSTANCE>(&__ImageBase);

        PowerToysSettings::Settings settings(hinstance, get_name());
        settings.set_description(MODULE_DESC);

        return settings.serialize_to_buffer(buffer, buffer_size);
    }

    virtual void call_custom_action(const wchar_t* action) override
    {
        try
        {
            PowerToysSettings::CustomActionObject action_object =
                PowerToysSettings::CustomActionObject::from_json_string(action);

            if (action_object.get_name() == L"Launch")
            {
                Logger::trace(L"Launch action received");

                // ColorPicker pattern: check if process is running, re-launch if needed
                if (!is_process_running())
                {
                    Logger::trace(L"PowerDisplay process not running, re-launching");
                    launch_process();
                }

                if (m_hToggleEvent)
                {
                    Logger::trace(L"Signaling toggle event");
                    SetEvent(m_hToggleEvent);
                }
                Trace::ActivatePowerDisplay();
            }
            else if (action_object.get_name() == L"RefreshMonitors")
            {
                Logger::trace(L"RefreshMonitors action received, signaling refresh event");
                if (m_hRefreshEvent)
                {
                    SetEvent(m_hRefreshEvent);
                }
                else
                {
                    Logger::warn(L"Refresh event handle is null");
                }
            }
            else if (action_object.get_name() == L"ApplyProfile")
            {
                Logger::trace(L"ApplyProfile action received");

                // Ensure PowerDisplay process is running before signaling event
                EnsureProcessRunning();

                if (m_hApplyProfileEvent)
                {
                    Logger::trace(L"Signaling apply profile event");
                    SetEvent(m_hApplyProfileEvent);
                }
                else
                {
                    Logger::warn(L"Apply profile event handle is null");
                }
            }
        }
        catch (std::exception&)
        {
            Logger::error(L"Failed to parse action. {}", action);
        }
    }

    virtual void set_config(const wchar_t* /*config*/) override
    {
        // Settings changes are handled via dedicated Windows Events:
        // - HotkeyUpdatedPowerDisplayEvent: triggered by Settings UI when activation shortcut changes
        // - SettingsUpdatedPowerDisplayEvent: triggered for tray icon visibility changes
        // - ApplyProfilePowerDisplayEvent: for profile settings
        // PowerDisplay.exe reads settings directly from file when these events are signaled.
    }

    virtual void enable() override
    {
        Logger::info(L"enable: PowerDisplay module is being enabled");
        m_enabled = true;
        Trace::EnablePowerDisplay(true);

        // Launch PowerDisplay.exe if not already running (ColorPicker pattern)
        Logger::trace(L"enable: Checking if PowerDisplay process needs to be launched");
        if (!is_process_running())
        {
            Logger::info(L"enable: Launching PowerDisplay process");
            launch_process();
            // Don't wait for ready here - let the process initialize in background
            Logger::info(L"enable: PowerDisplay process launch initiated");
        }
        else
        {
            Logger::info(L"enable: PowerDisplay process already running, skipping launch");
        }
        Logger::info(L"enable: PowerDisplay module enabled successfully");
    }

    virtual void disable() override
    {
        Logger::trace(L"PowerDisplay::disable()");

        if (m_enabled)
        {
            // Reset toggle event to prevent accidental activation during shutdown
            if (m_hToggleEvent)
            {
                ResetEvent(m_hToggleEvent);
            }

            // Signal terminate event and wait for graceful shutdown
            if (m_hTerminateEvent)
            {
                Logger::trace(L"Signaling PowerDisplay to exit");
                SetEvent(m_hTerminateEvent);
            }
            else
            {
                Logger::warn(L"Terminate event handle is null");
            }

            // Wait for process to exit gracefully, then force terminate if needed
            // (ColorPicker/Peek/Hosts pattern: SetEvent → Wait 1500ms → TerminateProcess)
            if (m_hProcess)
            {
                Logger::trace(L"Waiting for PowerDisplay process to exit (max 1500ms)");
                DWORD waitResult = WaitForSingleObject(m_hProcess, 1500);
                if (waitResult == WAIT_TIMEOUT)
                {
                    Logger::warn(L"PowerDisplay process did not exit gracefully, force terminating");
                    TerminateProcess(m_hProcess, 1);
                }
                else if (waitResult == WAIT_OBJECT_0)
                {
                    Logger::trace(L"PowerDisplay process exited gracefully");
                }
                else
                {
                    Logger::error(L"WaitForSingleObject returned unexpected result: {}", waitResult);
                }
                CloseHandle(m_hProcess);
                m_hProcess = nullptr;
            }

            // Reset terminate event after process cleanup
            if (m_hTerminateEvent)
            {
                ResetEvent(m_hTerminateEvent);
            }
        }

        m_enabled = false;
        Trace::EnablePowerDisplay(false);
    }

    virtual bool is_enabled() override
    {
        return m_enabled;
    }

    // NOTE: Hotkey handling is done in-process by PowerDisplay.exe using RegisterHotKey,
    // similar to CmdPal pattern. This avoids IPC timing issues where Deactivated event
    // fires before the Toggle event arrives from Runner.
    virtual bool on_hotkey(size_t /*hotkeyId*/) override
    {
        // PowerDisplay handles hotkeys in-process, not via Runner IPC
        return false;
    }

    virtual size_t get_hotkeys(Hotkey* /*hotkeys*/, size_t /*buffer_size*/) override
    {
        // PowerDisplay handles hotkeys in-process, not via Runner
        // Return 0 to tell Runner we don't want any hotkeys registered
        return 0;
    }

    virtual void send_settings_telemetry() override
    {
        Logger::trace(L"send_settings_telemetry: Signaling settings telemetry event");
        if (m_hSendSettingsTelemetryEvent)
        {
            SetEvent(m_hSendSettingsTelemetryEvent);
        }
        else
        {
            Logger::warn(L"send_settings_telemetry: Event handle is null");
        }
    }
};

extern "C" __declspec(dllexport) PowertoyModuleIface* __cdecl powertoy_create()
{
    return new PowerDisplayModule();
}
