// dllmain.cpp : Defines the entry point for the DLL application.
#include "pch.h"

#include <mutex>
#include <common/SettingsAPI/settings_helpers.h>
#include <common/utils/winapi_error.h>
#include <common/utils/logger_helper.h>
#include <common/interop/shared_constants.h>

#include "../interface/powertoy_module_interface.h"
#include "Generated Files/resource.h"
#include <common/SettingsAPI/settings_objects.h>

BOOL APIENTRY DllMain(HMODULE /*hModule*/, DWORD /*ul_reason_for_call*/, LPVOID /*lpReserved*/)
{
    return TRUE;
}

class ShortcutGuideModule : public PowertoyModuleIface
{
public:
    ShortcutGuideModule()
    {
        app_name = GET_RESOURCE_STRING(IDS_SHORTCUT_GUIDE);
        app_key = L"Shortcut Guide";
        LoggerHelpers::init_logger(app_key, L"ModuleInterface", LogSettings::shortcutGuideLoggerName);

        std::filesystem::path oldLogPath(PTSettingsHelper::get_module_save_folder_location(app_key));
        oldLogPath.append("ShortcutGuideLogs");
        LoggerHelpers::delete_old_log_folder(oldLogPath);

        exitEvent = CreateEvent(nullptr, false, false, CommonSharedConstants::SHORTCUT_GUIDE_EXIT_EVENT);
        if (!exitEvent)
        {
            Logger::warn(L"Failed to create {} event. {}", CommonSharedConstants::SHORTCUT_GUIDE_EXIT_EVENT, get_last_error_or_default(GetLastError()));
        }

        triggerEvent = CreateEvent(nullptr, false, false, CommonSharedConstants::SHORTCUT_GUIDE_TRIGGER_EVENT);
        if (!triggerEvent)
        {
            Logger::warn(L"Failed to create {} event. {}", CommonSharedConstants::SHORTCUT_GUIDE_TRIGGER_EVENT, get_last_error_or_default(GetLastError()));
        }

        winKeyHoldEvent = CreateEvent(nullptr, false, false, CommonSharedConstants::SHORTCUT_GUIDE_WIN_KEY_HOLD_EVENT);
        if (!winKeyHoldEvent)
        {
            Logger::warn(L"Failed to create {} event. {}", CommonSharedConstants::SHORTCUT_GUIDE_WIN_KEY_HOLD_EVENT, get_last_error_or_default(GetLastError()));
        }

        InitSettings();
    }

    virtual const wchar_t* get_name() override
    {
        return app_name.c_str();
    }

    virtual const wchar_t* get_key() override
    {
        return app_key.c_str();
    }

    // Return the configured status for the gpo policy for the module
    virtual powertoys_gpo::gpo_rule_configured_t gpo_policy_enabled_configuration() override
    {
        return powertoys_gpo::getConfiguredShortcutGuideEnabledValue();
    }

    virtual bool get_config(wchar_t* buffer, int* buffer_size) override
    {
        HINSTANCE hinstance = reinterpret_cast<HINSTANCE>(&__ImageBase);
        PowerToysSettings::Settings settings(hinstance, get_name());
        return settings.serialize_to_buffer(buffer, buffer_size);
    }

    virtual void set_config(const wchar_t* config) override
    {
        Logger::trace("set_config()");
        try
        {
            // Parse the input JSON string.
            PowerToysSettings::PowerToyValues values =
                PowerToysSettings::PowerToyValues::from_json_string(config, get_key());

            ParseSettings(values);
        }
        catch (std::exception& ex)
        {
            Logger::error("Failed to parse settings. {}", ex.what());
        }
    }

    virtual void enable() override
    {
        Logger::info("Shortcut Guide is enabling");

        if (!_enabled)
        {
            _enabled = true;
            StartProcess();
        }
        else
        {
            Logger::warn("Shortcut guide is already enabled");
        }
    }

    virtual void disable() override
    {
        Logger::info("ShortcutGuideModule::disable()");
        if (_enabled)
        {
            _enabled = false;
            StopProcess();
        }
        else
        {
            Logger::warn("Shortcut Guide is already disabled");
        }
    }

    virtual bool is_enabled() override
    {
        return _enabled;
    }

    virtual void destroy() override
    {
        this->disable();
        if (exitEvent)
        {
            CloseHandle(exitEvent);
        }
        if (triggerEvent)
        {
            CloseHandle(triggerEvent);
        }
        if (winKeyHoldEvent)
        {
            CloseHandle(winKeyHoldEvent);
        }

        delete this;
    }

    virtual std::optional<HotkeyEx> GetHotkeyEx() override
    {
        Logger::trace("GetHotkeyEx()");
        return m_hotkey;
    }

    virtual void OnHotkeyEx() override
    {
        SignalEvent(triggerEvent, CommonSharedConstants::SHORTCUT_GUIDE_TRIGGER_EVENT, L"regular hotkey");
    }

    virtual bool on_hotkey(size_t hotkeyId) override
    {
        if (hotkeyId == PowertoyModuleIface::WIN_KEY_HOLD_HOTKEY_ID && m_windowsKeyAction != WindowsKeyAction::Off)
        {
            SignalEvent(winKeyHoldEvent, CommonSharedConstants::SHORTCUT_GUIDE_WIN_KEY_HOLD_EVENT, L"Windows key hold");
        }

        return false;
    }

    virtual void send_settings_telemetry() override
    {
        Logger::trace("Send settings telemetry");
        if (!StartProcess(L"telemetry"))
        {
            Logger::error("Failed to create a process to send settings telemetry");
        }
    }
    virtual bool keep_track_of_pressed_win_key() override { return true; }
    virtual UINT milliseconds_win_key_must_be_pressed() override { return m_millisecondsWinKeyPressTimeForGlobalWindowsShortcuts; }

private:
    enum class WindowsKeyAction
    {
        Off = 0,
        TaskbarIndicators = 1,
        OpenShortcutGuide = 2,
    };

    std::wstring app_name;
    //contains the non localized key of the powertoy
    std::wstring app_key;
    bool _enabled = false;
    winrt::handle m_process;

    // Hotkey to invoke the module
    HotkeyEx m_hotkey;

    // If the module should be activated through the legacy pressing windows key behavior.
    const UINT DEFAULT_MILLISECONDS_WIN_KEY_PRESS_TIME_FOR_GLOBAL_WINDOWS_SHORTCUTS = 900;
    const UINT DEFAULT_MILLISECONDS_WIN_KEY_PRESS_TIME_FOR_TASKBAR_ICON_SHORTCUTS = 900;
    UINT m_millisecondsWinKeyPressTimeForGlobalWindowsShortcuts = DEFAULT_MILLISECONDS_WIN_KEY_PRESS_TIME_FOR_GLOBAL_WINDOWS_SHORTCUTS;
    UINT m_millisecondsWinKeyPressTimeForTaskbarIconShortcuts = DEFAULT_MILLISECONDS_WIN_KEY_PRESS_TIME_FOR_TASKBAR_ICON_SHORTCUTS;

    HANDLE triggerEvent;
    HANDLE winKeyHoldEvent;
    HANDLE exitEvent;
    WindowsKeyAction m_windowsKeyAction = WindowsKeyAction::TaskbarIndicators;

    void SignalEvent(HANDLE eventHandle, const wchar_t* eventName, const wchar_t* activationSource)
    {
        Logger::trace(L"Shortcut Guide was invoked by {}", activationSource);
        if (!_enabled)
        {
            return;
        }

        if (!IsProcessActive() && !StartProcess())
        {
            return;
        }

        if (!SetEvent(eventHandle))
        {
            Logger::error(L"Failed to signal {}. {}", eventName, get_last_error_or_default(GetLastError()));
        }
    }

    bool StartProcess(std::wstring args = L"")
    {
        const bool trackProcess = args.empty();
        if (trackProcess && IsProcessActive())
        {
            return true;
        }

        if (trackProcess)
        {
            if (exitEvent)
            {
                ResetEvent(exitEvent);
            }

            if (triggerEvent)
            {
                ResetEvent(triggerEvent);
            }

            if (winKeyHoldEvent)
            {
                ResetEvent(winKeyHoldEvent);
            }
        }

        unsigned long powertoys_pid = GetCurrentProcessId();
        std::wstring executable_args = L"";
        executable_args.append(std::to_wstring(powertoys_pid));
        if (!args.empty())
        {
            executable_args.append(L" ");
            executable_args.append(args);
        }

        SHELLEXECUTEINFOW sei{ sizeof(sei) };
        sei.fMask = { SEE_MASK_NOCLOSEPROCESS | SEE_MASK_FLAG_NO_UI };
        sei.lpFile = L"WinUI3Apps\\PowerToys.ShortcutGuide.exe";
        sei.nShow = SW_SHOWNORMAL;
        sei.lpParameters = executable_args.data();
        if (ShellExecuteExW(&sei) == false)
        {
            Logger::error(L"Failed to start SG process. {}", get_last_error_or_default(GetLastError()));
            auto message = get_last_error_message(GetLastError());
            if (message.has_value())
            {
                Logger::error(message.value());
            }

            return false;
        }

        winrt::handle launchedProcess{ sei.hProcess };
        Logger::trace(L"Started SG process with pid={}", GetProcessId(launchedProcess.get()));
        if (trackProcess)
        {
            m_process = std::move(launchedProcess);
        }

        return true;
    }

    bool IsProcessActive()
    {
        if (!m_process)
        {
            return false;
        }

        auto result = WaitForSingleObject(m_process.get(), 0);
        if (result == WAIT_FAILED)
        {
            Logger::error("Failed to wait for SG process.");
        }

        if (result == WAIT_OBJECT_0)
        {
            m_process = {};
        }

        return result == WAIT_TIMEOUT;
    }

    void StopProcess()
    {
        if (exitEvent)
        {
            if (!SetEvent(exitEvent))
            {
                Logger::error(L"Failed to signal {}. {}", CommonSharedConstants::SHORTCUT_GUIDE_EXIT_EVENT, get_last_error_or_default(GetLastError()));
            }
        }

        if (!m_process)
        {
            return;
        }

        if (!IsProcessActive())
        {
            return;
        }

        constexpr DWORD gracefulShutdownTimeoutMs = 2000;
        constexpr DWORD forcedShutdownTimeoutMs = 5000;
        auto waitResult = WaitForSingleObject(m_process.get(), gracefulShutdownTimeoutMs);
        if (waitResult == WAIT_TIMEOUT)
        {
            Logger::warn("Shortcut Guide did not exit gracefully; terminating it.");
            if (!TerminateProcess(m_process.get(), 0))
            {
                Logger::error(L"Failed to terminate Shortcut Guide. {}", get_last_error_or_default(GetLastError()));
            }
            else if (WaitForSingleObject(m_process.get(), forcedShutdownTimeoutMs) != WAIT_OBJECT_0)
            {
                Logger::error("Shortcut Guide did not terminate within the timeout.");
            }
        }
        else if (waitResult == WAIT_FAILED)
        {
            Logger::error(L"Failed to wait for Shortcut Guide shutdown. {}", get_last_error_or_default(GetLastError()));
        }

        m_process = {};
    }

    void InitSettings()
    {
        try
        {
            PowerToysSettings::PowerToyValues settings =
                PowerToysSettings::PowerToyValues::load_from_settings_file(app_key);

            ParseSettings(settings);
        }
        catch (std::exception& ex)
        {
            Logger::error("Failed to init settings. {}", ex.what());
        }
        catch (...)
        {
            Logger::error("Failed to init settings");
        }
    }

    void ParseSettings(PowerToysSettings::PowerToyValues& settings)
    {
        auto settingsObject = settings.get_raw_json();
        if (settingsObject.GetView().Size())
        {
            try
            {
                // Parse HotKey
                auto jsonHotkeyObject = settingsObject.GetNamedObject(L"properties").GetNamedObject(L"open_shortcutguide");
                auto hotkey = PowerToysSettings::HotkeyObject::from_json(jsonHotkeyObject);
                m_hotkey = HotkeyEx();
                if (hotkey.win_pressed())
                {
                    m_hotkey.modifiersMask |= MOD_WIN;
                }

                if (hotkey.ctrl_pressed())
                {
                    m_hotkey.modifiersMask |= MOD_CONTROL;
                }

                if (hotkey.shift_pressed())
                {
                    m_hotkey.modifiersMask |= MOD_SHIFT;
                }

                if (hotkey.alt_pressed())
                {
                    m_hotkey.modifiersMask |= MOD_ALT;
                }

                m_hotkey.vkCode = static_cast<WORD>(hotkey.get_code());
            }
            catch (...)
            {
                Logger::warn("Failed to initialize Shortcut Guide start shortcut");
            }

            try
            {
                auto propertiesObject = settingsObject.GetNamedObject(L"properties");
                if (propertiesObject.HasKey(L"press_time"))
                {
                    auto jsonDurationObject = propertiesObject.GetNamedObject(L"press_time");
                    if (jsonDurationObject.HasKey(L"value"))
                    {
                        auto pressTime = static_cast<UINT>(jsonDurationObject.GetNamedNumber(L"value"));
                        if (pressTime < 100)
                        {
                            pressTime = 100;
                        }
                        else if (pressTime > 5000)
                        {
                            pressTime = 5000;
                        }

                        m_millisecondsWinKeyPressTimeForGlobalWindowsShortcuts = pressTime;
                    }
                }
            }
            catch (...)
            { /* Keep defaults */
            }

            try
            {
                auto propertiesObject = settingsObject.GetNamedObject(L"properties");
                if (propertiesObject.HasKey(L"win_key_action"))
                {
                    const auto value = static_cast<int>(propertiesObject.GetNamedObject(L"win_key_action").GetNamedNumber(L"value"));
                    switch (value)
                    {
                    case static_cast<int>(WindowsKeyAction::Off):
                        m_windowsKeyAction = WindowsKeyAction::Off;
                        break;
                    case static_cast<int>(WindowsKeyAction::OpenShortcutGuide):
                        m_windowsKeyAction = WindowsKeyAction::OpenShortcutGuide;
                        break;
                    case static_cast<int>(WindowsKeyAction::TaskbarIndicators):
                    default:
                        m_windowsKeyAction = WindowsKeyAction::TaskbarIndicators;
                        break;
                    }
                }
            }
            catch (...)
            { /* Keep defaults */
            }
        }
        else
        {
            Logger::info("Shortcut Guide settings are empty");
        }

        if (!m_hotkey.modifiersMask)
        {
            Logger::info("Shortcut Guide is going to use default shortcut");
            m_hotkey.modifiersMask = MOD_SHIFT | MOD_WIN;
            m_hotkey.vkCode = VK_OEM_2;
        }
    }

    void WindowsKeyPressBehavior()
    {
        StopProcess();
    }
};

extern "C" __declspec(dllexport) PowertoyModuleIface* __cdecl powertoy_create()
{
    return new ShortcutGuideModule();
}