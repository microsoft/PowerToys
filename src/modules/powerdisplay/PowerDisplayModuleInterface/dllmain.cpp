// dllmain.cpp : Defines the entry point for the DLL Application.
#include "pch.h"
#include <interface/powertoy_module_interface.h>
#include <common/SettingsAPI/settings_objects.h>
#include "trace.h"
#include "PowerDisplayProcessManager.h"
#include <common/interop/shared_constants.h>
#include <common/utils/string_utils.h>
#include <common/utils/winapi_error.h>
#include <common/utils/logger_helper.h>
#include <common/utils/resources.h>
#include <thread>
#include <atomic>

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

    // Process manager handles Named Pipe communication and process lifecycle
    PowerDisplayProcessManager m_processManager;

    // Windows Events for Settings UI triggered events (these are still needed)
    // Note: These events are created on-demand by EventHelper.SignalEvent() in Settings UI
    // and NativeEventWaiter.WaitForEventLoop() in PowerDisplay.exe.
    HANDLE m_hRefreshEvent = nullptr;
    HANDLE m_hSendSettingsTelemetryEvent = nullptr;

    // Toggle event handle and listener thread for Quick Access support
    HANDLE m_hToggleEvent = nullptr;
    HANDLE m_hStopEvent = nullptr;  // Manual-reset event to signal thread termination
    std::thread m_toggleEventThread;

public:
    PowerDisplayModule()
    {
        LoggerHelpers::init_logger(MODULE_NAME, L"ModuleInterface", LogSettings::powerDisplayLoggerName);
        Logger::info("Power Display module is constructing");

        // Create Windows Events for Settings UI triggered operations
        // These events are signaled by Settings UI, not by module DLL
        Logger::trace(L"Creating Windows Events for Settings UI IPC...");
        m_hRefreshEvent = CreateDefaultEvent(CommonSharedConstants::REFRESH_POWER_DISPLAY_MONITORS_EVENT);
        Logger::trace(L"Created REFRESH_MONITORS_EVENT: handle={}", reinterpret_cast<void*>(m_hRefreshEvent));
        m_hSendSettingsTelemetryEvent = CreateDefaultEvent(CommonSharedConstants::POWER_DISPLAY_SEND_SETTINGS_TELEMETRY_EVENT);
        Logger::trace(L"Created SEND_SETTINGS_TELEMETRY_EVENT: handle={}", reinterpret_cast<void*>(m_hSendSettingsTelemetryEvent));

        // Create Toggle event for Quick Access support
        // This allows Quick Access to launch PowerDisplay even when module is not enabled
        m_hToggleEvent = CreateDefaultEvent(CommonSharedConstants::TOGGLE_POWER_DISPLAY_EVENT);
        Logger::trace(L"Created TOGGLE_EVENT: handle={}", reinterpret_cast<void*>(m_hToggleEvent));

        // Create manual-reset stop event for clean thread termination
        m_hStopEvent = CreateEventW(nullptr, TRUE, FALSE, nullptr);
        Logger::trace(L"Created STOP_EVENT: handle={}", reinterpret_cast<void*>(m_hStopEvent));

        if (!m_hRefreshEvent || !m_hSendSettingsTelemetryEvent || !m_hToggleEvent || !m_hStopEvent)
        {
            Logger::error(L"Failed to create one or more event handles: Refresh={}, SettingsTelemetry={}, Toggle={}",
                         reinterpret_cast<void*>(m_hRefreshEvent),
                         reinterpret_cast<void*>(m_hSendSettingsTelemetryEvent),
                         reinterpret_cast<void*>(m_hToggleEvent));
        }
        else
        {
            Logger::info(L"All Windows Events created successfully");
        }

        // Start toggle event listener thread for Quick Access support
        StartToggleEventListener();
    }

    ~PowerDisplayModule()
    {
        if (m_enabled)
        {
            disable();
        }

        // Stop toggle event listener thread
        StopToggleEventListener();

        // Clean up event handles
        if (m_hRefreshEvent)
        {
            CloseHandle(m_hRefreshEvent);
            m_hRefreshEvent = nullptr;
        }
        if (m_hSendSettingsTelemetryEvent)
        {
            CloseHandle(m_hSendSettingsTelemetryEvent);
            m_hSendSettingsTelemetryEvent = nullptr;
        }
        if (m_hToggleEvent)
        {
            CloseHandle(m_hToggleEvent);
            m_hToggleEvent = nullptr;
        }
        if (m_hStopEvent)
        {
            CloseHandle(m_hStopEvent);
            m_hStopEvent = nullptr;
        }
    }

    void StartToggleEventListener()
    {
        if (!m_hToggleEvent || !m_hStopEvent)
        {
            return;
        }

        // Reset stop event before starting thread
        ResetEvent(m_hStopEvent);

        m_toggleEventThread = std::thread([this]() {
            Logger::info(L"Toggle event listener thread started");

            HANDLE handles[] = { m_hToggleEvent, m_hStopEvent };
            constexpr DWORD TOGGLE_EVENT_INDEX = 0;
            constexpr DWORD STOP_EVENT_INDEX = 1;

            while (true)
            {
                // Wait indefinitely for either toggle event or stop event
                DWORD result = WaitForMultipleObjects(2, handles, FALSE, INFINITE);

                if (result == WAIT_OBJECT_0 + TOGGLE_EVENT_INDEX)
                {
                    Logger::trace(L"Toggle event received");
                    TogglePowerDisplay();
                }
                else if (result == WAIT_OBJECT_0 + STOP_EVENT_INDEX)
                {
                    // Stop event signaled - exit the loop
                    Logger::trace(L"Stop event received, exiting toggle listener");
                    break;
                }
                else
                {
                    // WAIT_FAILED or unexpected result
                    Logger::warn(L"WaitForMultipleObjects returned unexpected result: {}", result);
                    break;
                }
            }

            Logger::info(L"Toggle event listener thread stopped");
        });
    }

    void StopToggleEventListener()
    {
        if (m_hStopEvent)
        {
            // Signal the stop event to wake up the waiting thread
            SetEvent(m_hStopEvent);
        }

        if (m_toggleEventThread.joinable())
        {
            m_toggleEventThread.join();
        }
    }

    /// <summary>
    /// Toggle PowerDisplay window visibility.
    /// If process is running, launches again to trigger redirect activation (OnActivated handles toggle).
    /// If process is not running, starts it via Named Pipe and sends toggle message.
    /// </summary>
    void TogglePowerDisplay()
    {
        if (m_processManager.is_running())
        {
            // Process running - launch to trigger single instance redirect, OnActivated will toggle
            SHELLEXECUTEINFOW sei{ sizeof(sei) };
            sei.fMask = SEE_MASK_FLAG_NO_UI;
            sei.lpFile = L"WinUI3Apps\\PowerToys.PowerDisplay.exe";
            sei.nShow = SW_SHOWNORMAL;
            ShellExecuteExW(&sei);
        }
        else
        {
            // Process not running - start and send toggle via Named Pipe
            m_processManager.send_message(CommonSharedConstants::POWER_DISPLAY_TOGGLE_MESSAGE);
        }
        Trace::ActivatePowerDisplay();
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
                TogglePowerDisplay();
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

                // Get the profile name from the action value
                std::wstring profileName = action_object.get_value();
                Logger::trace(L"ApplyProfile: profile name = '{}'", profileName);

                // Send ApplyProfile message with profile name via Named Pipe
                m_processManager.send_message(CommonSharedConstants::POWER_DISPLAY_APPLY_PROFILE_MESSAGE, profileName);
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
        // PowerDisplay.exe reads settings directly from file when these events are signaled.
    }

    virtual void enable() override
    {
        Logger::info(L"enable: PowerDisplay module is being enabled");
        m_enabled = true;
        Trace::EnablePowerDisplay(true);

        // Start the process manager (launches PowerDisplay.exe with Named Pipe)
        m_processManager.start();

        Logger::info(L"enable: PowerDisplay module enabled successfully");
    }

    virtual void disable() override
    {
        Logger::trace(L"PowerDisplay::disable()");

        if (m_enabled)
        {
            // Stop the process manager (sends terminate message and waits for exit)
            m_processManager.stop();
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
