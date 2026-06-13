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
#include <common/utils/EventWaiter.h>

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
        triggerEventWaiter.start(CommonSharedConstants::SHORTCUT_GUIDE_TRIGGER_EVENT, [this](DWORD) {
            OnHotkeyEx();
        });

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
            if (IsProcessActive())
            {
                TerminateProcess(m_hProcess, 0);
            }
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

        delete this;
    }

    virtual std::optional<HotkeyEx> GetHotkeyEx() override
    {
        Logger::trace("GetHotkeyEx()");

        // When the user opted into the legacy long-press Windows-key activation,
        // suppress the customized shortcut so the two activation methods stay
        // mutually exclusive (matches v0.99 behavior).
        if (m_shouldReactToPressedWinKey)
        {
            return std::nullopt;
        }

        return m_hotkey;
    }

    virtual void OnHotkeyEx() override
    {
        Logger::trace("OnHotkeyEx()");
        if (!_enabled)
        {
            return;
        }

        if (IsProcessActive())
        {
            // Toggle: re-pressing the hotkey while SG is open dismisses it.
            // In a Debug build the SG window doesn't auto-close on Deactivated
            // (#if !DEBUG guard in MainWindow.xaml.cs), so a held-over instance
            // can cause every other long-press to land here instead of launching
            // a new window. In Release the deactivate-handler closes SG, so this
            // path only fires when the user explicitly re-invokes the hotkey.
            Logger::trace("OnHotkeyEx: existing SG instance is alive, terminating it");
            TerminateProcess(m_hProcess, 0);
            return;
        }

        if (m_hProcess)
        {
            CloseHandle(m_hProcess);
            m_hProcess = nullptr;
        }

        StartProcess();
    }

    virtual void send_settings_telemetry() override
    {
        Logger::trace("Send settings telemetry");
        if (!StartProcess(L"telemetry"))
        {
            Logger::error("Failed to create a process to send settings telemetry");
        }
    }

    // Enables the legacy long-press-Windows-key activation in the runner's
    // CentralizedKeyboardHook. The runner re-asks this method whenever settings
    // change (via UpdateHotkeyEx), so toggling the setting takes effect immediately.
    virtual bool keep_track_of_pressed_win_key() override
    {
        return m_shouldReactToPressedWinKey;
    }

    virtual UINT milliseconds_win_key_must_be_pressed() override
    {
        return m_millisecondsWinKeyPressTime;
    }

private:
    std::wstring app_name;
    //contains the non localized key of the powertoy
    std::wstring app_key;
    bool _enabled = false;
    HANDLE m_hProcess = nullptr;

    // Hotkey to invoke the module
    HotkeyEx m_hotkey;

    // Legacy long-press-Windows-key activation (mutually exclusive with m_hotkey).
    static constexpr UINT DEFAULT_MILLISECONDS_WIN_KEY_PRESS_TIME = 900;
    bool m_shouldReactToPressedWinKey = false;
    UINT m_millisecondsWinKeyPressTime = DEFAULT_MILLISECONDS_WIN_KEY_PRESS_TIME;

    HANDLE triggerEvent;
    HANDLE exitEvent;
    EventWaiter triggerEventWaiter;

    bool StartProcess(std::wstring args = L"")
    {
        if (exitEvent)
        {
            ResetEvent(exitEvent);
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

        Logger::trace(L"Started SG process with pid={}", GetProcessId(sei.hProcess));
        m_hProcess = sei.hProcess;
        return true;
    }

    bool IsProcessActive()
    {
        if (!m_hProcess)
        {
            return false;
        }
        auto result = WaitForSingleObject(m_hProcess, 0);
        if (result == WAIT_FAILED)
        {
            Logger::error("Failed to wait for SG process.");
        }
        return result == WAIT_TIMEOUT;
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

        // Reset to defaults so a removed key in settings.json restores default behavior.
        m_shouldReactToPressedWinKey = false;
        m_millisecondsWinKeyPressTime = DEFAULT_MILLISECONDS_WIN_KEY_PRESS_TIME;

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

            // Parse legacy long-press-Windows-key activation. Both keys are optional;
            // missing or malformed entries leave the defaults above untouched.
            try
            {
                auto propertiesObject = settingsObject.GetNamedObject(L"properties");
                if (propertiesObject.HasKey(L"use_legacy_press_win_key_behavior"))
                {
                    auto legacyObject = propertiesObject.GetNamedObject(L"use_legacy_press_win_key_behavior");
                    m_shouldReactToPressedWinKey = legacyObject.GetNamedBoolean(L"value", false);
                }

                if (propertiesObject.HasKey(L"press_time"))
                {
                    auto pressTimeObject = propertiesObject.GetNamedObject(L"press_time");
                    int value = static_cast<int>(pressTimeObject.GetNamedNumber(L"value", DEFAULT_MILLISECONDS_WIN_KEY_PRESS_TIME));
                    if (value > 0)
                    {
                        m_millisecondsWinKeyPressTime = static_cast<UINT>(value);
                    }
                }
            }
            catch (...)
            {
                Logger::warn("Failed to parse Shortcut Guide legacy activation settings");
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
};

extern "C" __declspec(dllexport) PowertoyModuleIface* __cdecl powertoy_create()
{
    return new ShortcutGuideModule();
}