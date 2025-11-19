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
    const wchar_t JSON_KEY_ACTIVATION_SHORTCUT[] = L"activation_shortcut";
    const wchar_t JSON_KEY_WIN[] = L"win";
    const wchar_t JSON_KEY_ALT[] = L"alt";
    const wchar_t JSON_KEY_CTRL[] = L"ctrl";
    const wchar_t JSON_KEY_SHIFT[] = L"shift";
    const wchar_t JSON_KEY_CODE[] = L"code";
}

class PowerDisplayModule : public PowertoyModuleIface
{
private:
    bool m_enabled = false;
    bool m_hotkey_enabled = false;
    Hotkey m_activation_hotkey = { .win = true, .ctrl = false, .shift = false, .alt = true, .key = 'M' };

    // Windows Events for IPC (persistent handles - ColorPicker pattern)
    HANDLE m_hProcess = nullptr;
    HANDLE m_hInvokeEvent = nullptr;
    HANDLE m_hToggleEvent = nullptr;
    HANDLE m_hTerminateEvent = nullptr;
    HANDLE m_hRefreshEvent = nullptr;
    HANDLE m_hSettingsUpdatedEvent = nullptr;
    HANDLE m_hApplyColorTemperatureEvent = nullptr;
    HANDLE m_hApplyProfileEvent = nullptr;

    void parse_hotkey_settings(PowerToysSettings::PowerToyValues settings)
    {
        auto settingsObject = settings.get_raw_json();
        if (settingsObject.GetView().Size())
        {
            try
            {
                if (settingsObject.HasKey(JSON_KEY_PROPERTIES))
                {
                    auto properties = settingsObject.GetNamedObject(JSON_KEY_PROPERTIES);
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
                m_hotkey_enabled = false;
            }
        }
        else
        {
            Logger::info("Power Display settings are empty");
            m_hotkey_enabled = false;
        }
    }

    void parse_activation_hotkey(PowerToysSettings::PowerToyValues& settings)
    {
        auto settingsObject = settings.get_raw_json();
        if (settingsObject.GetView().Size())
        {
            try
            {
                if (settingsObject.HasKey(JSON_KEY_PROPERTIES))
                {
                    auto properties = settingsObject.GetNamedObject(JSON_KEY_PROPERTIES);
                    if (properties.HasKey(JSON_KEY_ACTIVATION_SHORTCUT))
                    {
                        auto jsonHotkeyObject = properties.GetNamedObject(JSON_KEY_ACTIVATION_SHORTCUT);
                        m_activation_hotkey.win = jsonHotkeyObject.GetNamedBoolean(JSON_KEY_WIN);
                        m_activation_hotkey.alt = jsonHotkeyObject.GetNamedBoolean(JSON_KEY_ALT);
                        m_activation_hotkey.shift = jsonHotkeyObject.GetNamedBoolean(JSON_KEY_SHIFT);
                        m_activation_hotkey.ctrl = jsonHotkeyObject.GetNamedBoolean(JSON_KEY_CTRL);
                        m_activation_hotkey.key = static_cast<unsigned char>(jsonHotkeyObject.GetNamedNumber(JSON_KEY_CODE));
                        m_activation_hotkey.isShown = true;
                        Logger::trace(L"Parsed activation hotkey: Win={} Ctrl={} Alt={} Shift={} Key={}",
                                     m_activation_hotkey.win, m_activation_hotkey.ctrl, m_activation_hotkey.alt,
                                     m_activation_hotkey.shift, m_activation_hotkey.key);
                    }
                    else
                    {
                        Logger::info("ActivationShortcut not found in settings, using default Win+Alt+M");
                        m_activation_hotkey.isShown = true;
                    }
                }
            }
            catch (...)
            {
                Logger::error("Failed to parse PowerDisplay activation shortcut, using default Win+Alt+M");
                m_activation_hotkey.isShown = true;
            }
        }
    }

    void init_settings()
    {
        try
        {
            PowerToysSettings::PowerToyValues settings =
                PowerToysSettings::PowerToyValues::load_from_settings_file(get_key());

            parse_hotkey_settings(settings);
            parse_activation_hotkey(settings);
        }
        catch (std::exception&)
        {
            Logger::error("Invalid json when trying to load the Power Display settings json from file.");
        }
    }

    // Helper method to check if PowerDisplay.exe process is still running
    bool is_process_running()
    {
        if (m_hProcess == nullptr)
        {
            return false;
        }
        return WaitForSingleObject(m_hProcess, 0) == WAIT_TIMEOUT;
    }

    // Helper method to launch PowerDisplay.exe process
    void launch_process()
    {
        Logger::trace(L"Starting PowerDisplay process");
        unsigned long powertoys_pid = GetCurrentProcessId();

        std::wstring executable_args = std::to_wstring(powertoys_pid);

        SHELLEXECUTEINFOW sei{ sizeof(sei) };
        sei.fMask = SEE_MASK_NOCLOSEPROCESS | SEE_MASK_FLAG_NO_UI;
        sei.lpFile = L"WinUI3Apps\\PowerToys.PowerDisplay.exe";
        sei.nShow = SW_SHOWNORMAL;
        sei.lpParameters = executable_args.data();

        if (ShellExecuteExW(&sei))
        {
            Logger::trace(L"Successfully started PowerDisplay process");
            m_hProcess = sei.hProcess;
        }
        else
        {
            Logger::error(L"PowerDisplay process failed to start. {}",
                         get_last_error_or_default(GetLastError()));
        }
    }

public:
    PowerDisplayModule()
    {
        LoggerHelpers::init_logger(MODULE_NAME, L"ModuleInterface", "PowerDisplay");
        Logger::info("Power Display object is constructing");

        init_settings();

        // Create all Windows Events (persistent handles - ColorPicker pattern)
        m_hInvokeEvent = CreateDefaultEvent(CommonSharedConstants::SHOW_POWER_DISPLAY_EVENT);
        m_hToggleEvent = CreateDefaultEvent(CommonSharedConstants::TOGGLE_POWER_DISPLAY_EVENT);
        m_hTerminateEvent = CreateDefaultEvent(CommonSharedConstants::TERMINATE_POWER_DISPLAY_EVENT);
        m_hRefreshEvent = CreateDefaultEvent(CommonSharedConstants::REFRESH_POWER_DISPLAY_MONITORS_EVENT);
        m_hSettingsUpdatedEvent = CreateDefaultEvent(CommonSharedConstants::SETTINGS_UPDATED_POWER_DISPLAY_EVENT);
        m_hApplyColorTemperatureEvent = CreateDefaultEvent(CommonSharedConstants::APPLY_COLOR_TEMPERATURE_POWER_DISPLAY_EVENT);
        m_hApplyProfileEvent = CreateDefaultEvent(CommonSharedConstants::APPLY_PROFILE_POWER_DISPLAY_EVENT);

        if (!m_hInvokeEvent || !m_hToggleEvent || !m_hTerminateEvent || !m_hRefreshEvent || !m_hSettingsUpdatedEvent || !m_hApplyColorTemperatureEvent || !m_hApplyProfileEvent)
        {
            Logger::error(L"Failed to create one or more event handles");
        }
    }

    ~PowerDisplayModule()
    {
        if (m_enabled)
        {
            disable();
        }

        // Clean up all event handles
        if (m_hInvokeEvent)
        {
            CloseHandle(m_hInvokeEvent);
            m_hInvokeEvent = nullptr;
        }
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
        if (m_hSettingsUpdatedEvent)
        {
            CloseHandle(m_hSettingsUpdatedEvent);
            m_hSettingsUpdatedEvent = nullptr;
        }
        if (m_hApplyColorTemperatureEvent)
        {
            CloseHandle(m_hApplyColorTemperatureEvent);
            m_hApplyColorTemperatureEvent = nullptr;
        }
        if (m_hApplyProfileEvent)
        {
            CloseHandle(m_hApplyProfileEvent);
            m_hApplyProfileEvent = nullptr;
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
        return powertoys_gpo::gpo_rule_configured_not_configured;
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
            else if (action_object.get_name() == L"ApplyColorTemperature")
            {
                Logger::trace(L"ApplyColorTemperature action received");
                if (m_hApplyColorTemperatureEvent)
                {
                    Logger::trace(L"Signaling apply color temperature event");
                    SetEvent(m_hApplyColorTemperatureEvent);
                }
                else
                {
                    Logger::warn(L"Apply color temperature event handle is null");
                }
            }
            else if (action_object.get_name() == L"ApplyProfile")
            {
                Logger::trace(L"ApplyProfile action received");
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

    virtual void set_config(const wchar_t* config) override
    {
        try
        {
            PowerToysSettings::PowerToyValues values =
                PowerToysSettings::PowerToyValues::from_json_string(config, get_key());

            parse_hotkey_settings(values);
            parse_activation_hotkey(values);

            // Signal settings updated event
            if (m_hSettingsUpdatedEvent)
            {
                Logger::trace(L"Signaling settings updated event");
                SetEvent(m_hSettingsUpdatedEvent);
            }
            else
            {
                Logger::warn(L"Settings updated event handle is null");
            }
        }
        catch (std::exception&)
        {
            Logger::error(L"Invalid json when trying to parse Power Display settings json.");
        }
    }

    virtual void enable() override
    {
        Logger::trace(L"PowerDisplay::enable()");
        m_enabled = true;
        Trace::EnablePowerDisplay(true);

        // Launch PowerDisplay.exe with PID only (Awake pattern)
        launch_process();
    }

    virtual void disable() override
    {
        Logger::trace(L"PowerDisplay::disable()");

        if (m_enabled)
        {
            // Reset invoke event to prevent accidental activation during shutdown
            if (m_hInvokeEvent)
            {
                ResetEvent(m_hInvokeEvent);
            }

            // Signal terminate event
            if (m_hTerminateEvent)
            {
                Logger::trace(L"Signaling PowerDisplay to exit");
                SetEvent(m_hTerminateEvent);
            }
            else
            {
                Logger::warn(L"Terminate event handle is null");
            }

            // Close process handle (don't wait, don't force terminate - Awake pattern)
            if (m_hProcess)
            {
                CloseHandle(m_hProcess);
                m_hProcess = nullptr;
            }
        }

        m_enabled = false;
        Trace::EnablePowerDisplay(false);
    }

    virtual bool is_enabled() override
    {
        return m_enabled;
    }

    virtual bool on_hotkey(size_t /*hotkeyId*/) override
    {
        if (m_enabled && m_hToggleEvent)
        {
            Logger::trace(L"Power Display hotkey pressed");

            // ColorPicker pattern: check if process is running, re-launch if needed
            if (!is_process_running())
            {
                Logger::trace(L"PowerDisplay process not running, re-launching");
                launch_process();
            }

            Logger::trace(L"Signaling toggle event");
            SetEvent(m_hToggleEvent);
            return true;
        }

        return false;
    }

    virtual size_t get_hotkeys(Hotkey* hotkeys, size_t buffer_size) override
    {
        if (m_activation_hotkey.key != 0)
        {
            if (hotkeys && buffer_size >= 1)
            {
                hotkeys[0] = m_activation_hotkey;
            }
            return 1;
        }
        return 0;
    }
};

extern "C" __declspec(dllexport) PowertoyModuleIface* __cdecl powertoy_create()
{
    return new PowerDisplayModule();
}
