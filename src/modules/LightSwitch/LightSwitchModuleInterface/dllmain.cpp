#include "pch.h"
#include <interface/powertoy_module_interface.h>
#include "trace.h"
#include <common/logger/logger.h>
#include <common/SettingsAPI/settings_objects.h>
#include <common/SettingsAPI/settings_helpers.h>
#include <locale>
#include <codecvt>
#include <common/utils/logger_helper.h>
#include "ThemeHelper.h"

extern "C" IMAGE_DOS_HEADER __ImageBase;

namespace
{
    const wchar_t JSON_KEY_PROPERTIES[] = L"properties";
    const wchar_t JSON_KEY_WIN[] = L"win";
    const wchar_t JSON_KEY_ALT[] = L"alt";
    const wchar_t JSON_KEY_CTRL[] = L"ctrl";
    const wchar_t JSON_KEY_SHIFT[] = L"shift";
    const wchar_t JSON_KEY_CODE[] = L"code";
    const wchar_t JSON_KEY_TOGGLE_THEME_HOTKEY[] = L"toggle-theme-hotkey";
    const wchar_t JSON_KEY_VALUE[] = L"value";
}

BOOL APIENTRY DllMain(HMODULE hModule, DWORD ul_reason_for_call, LPVOID lpReserved)
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
const static wchar_t* MODULE_NAME = L"LightSwitch";
// Add a description that will we shown in the module settings page.
const static wchar_t* MODULE_DESC = L"This is a module that allows you to control light/dark theming via set times, sun rise, or directly invoking the change.";

enum class ScheduleMode
{
    FixedHours,
    SunsetToSunrise,
    // add more later
};

inline std::wstring ToString(ScheduleMode mode)
{
    switch (mode)
    {
    case ScheduleMode::SunsetToSunrise:
        return L"SunsetToSunrise";
    case ScheduleMode::FixedHours:
    default:
        return L"FixedHours";
    }
}

inline ScheduleMode FromString(const std::wstring& str)
{
    if (str == L"SunsetToSunrise")
        return ScheduleMode::SunsetToSunrise;
    return ScheduleMode::FixedHours;
}

// These are the properties shown in the Settings page.
struct ModuleSettings
{
    bool m_changeSystem = true;
    bool m_changeApps = true;
    ScheduleMode m_scheduleMode = ScheduleMode::FixedHours;
    int m_lightTime = 480;
    int m_darkTime = 1200;
    int m_sunrise_offset = 0;
    int m_sunset_offset = 0;
    std::wstring m_latitude = L"0.0";
    std::wstring m_longitude = L"0.0";
} g_settings;

class LightSwitchInterface : public PowertoyModuleIface
{
private:
    bool m_enabled = false;

    HANDLE m_process{ nullptr };
    HANDLE m_force_light_event_handle;
    HANDLE m_force_dark_event_handle;
    HANDLE m_manual_override_event_handle;

    static const constexpr int NUM_DEFAULT_HOTKEYS = 4;

    Hotkey m_toggle_theme_hotkey = { .win = true, .ctrl = true, .shift = true, .alt = false, .key = 'D' };

    void init_settings();

public:
    LightSwitchInterface()
    {
        LoggerHelpers::init_logger(L"LightSwitch", L"ModuleInterface", LogSettings::lightSwitchLoggerName);

        m_force_light_event_handle = CreateDefaultEvent(L"POWERTOYS_LIGHTSWITCH_FORCE_LIGHT");
        m_force_dark_event_handle = CreateDefaultEvent(L"POWERTOYS_LIGHTSWITCH_FORCE_DARK");
        m_manual_override_event_handle = CreateEventW(nullptr, TRUE, FALSE, L"POWERTOYS_LIGHTSWITCH_MANUAL_OVERRIDE");

        init_settings();
    };

    virtual const wchar_t* get_key() override
    {
        return L"LightSwitch";
    }

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

    // Return the configured status for the gpo policy for the module
    virtual powertoys_gpo::gpo_rule_configured_t gpo_policy_enabled_configuration() override
    {
        return powertoys_gpo::getConfiguredLightSwitchEnabledValue();
    }

    // Return JSON with the configuration options.
    virtual bool get_config(wchar_t* buffer, int* buffer_size) override
    {
        HINSTANCE hinstance = reinterpret_cast<HINSTANCE>(&__ImageBase);

        // Create a Settings object with your module name
        PowerToysSettings::Settings settings(hinstance, get_name());
        settings.set_description(MODULE_DESC);
        settings.set_overview_link(L"https://aka.ms/powertoys");

        // Boolean toggles
        settings.add_bool_toggle(
            L"changeSystem",
            L"Change System Theme",
            g_settings.m_changeSystem);

        settings.add_bool_toggle(
            L"changeApps",
            L"Change Apps Theme",
            g_settings.m_changeApps);

        settings.add_choice_group(
            L"scheduleMode",
            L"Theme schedule mode",
            ToString(g_settings.m_scheduleMode),
            { { L"FixedHours", L"Set hours manually" },
              { L"SunsetToSunrise", L"Use sunrise/sunset times" } });

        // Integer spinners
        settings.add_int_spinner(
            L"lightTime",
            L"Time to switch to light theme (minutes after midnight).",
            g_settings.m_lightTime,
            0,
            1439,
            1);

        settings.add_int_spinner(
            L"darkTime",
            L"Time to switch to dark theme (minutes after midnight).",
            g_settings.m_darkTime,
            0,
            1439,
            1);

        settings.add_int_spinner(
            L"sunrise_offset",
            L"Time to offset turning on your light theme.",
            g_settings.m_sunrise_offset,
            0,
            1439,
            1);

        settings.add_int_spinner(
            L"sunset_offset",
            L"Time to offset turning on your dark theme.",
            g_settings.m_sunset_offset,
            0,
            1439,
            1);

        // Strings for latitude and longitude
        settings.add_string(
            L"latitude",
            L"Your latitude in decimal degrees (e.g. 39.95).",
            g_settings.m_latitude);

        settings.add_string(
            L"longitude",
            L"Your longitude in decimal degrees (e.g. -75.16).",
            g_settings.m_longitude);

        // One-shot actions (buttons)
        settings.add_custom_action(
            L"forceLight",
            L"Switch immediately to light theme",
            L"Force Light",
            L"{}");

        settings.add_custom_action(
            L"forceDark",
            L"Switch immediately to dark theme",
            L"Force Dark",
            L"{}");

        // Hotkeys
        PowerToysSettings::HotkeyObject dm_hk = PowerToysSettings::HotkeyObject::from_settings(
            m_toggle_theme_hotkey.win,
            m_toggle_theme_hotkey.ctrl,
            m_toggle_theme_hotkey.alt,
            m_toggle_theme_hotkey.shift,
            m_toggle_theme_hotkey.key);

        settings.add_hotkey(
            L"toggle-theme-hotkey",
            L"Shortcut to toggle theme immediately",
            dm_hk);

        // Serialize to buffer for the PowerToys runner
        return settings.serialize_to_buffer(buffer, buffer_size);
    }

    // Signal from the Settings editor to call a custom action.
    // This can be used to spawn more complex editors.
    void call_custom_action(const wchar_t* action) override
    {
        try
        {
            auto action_object = PowerToysSettings::CustomActionObject::from_json_string(action);

            if (action_object.get_name() == L"forceLight")
            {
                Logger::info(L"[Light Switch] Custom action triggered: Force Light");
                SetSystemTheme(true);
                SetAppsTheme(true);
            }
            else if (action_object.get_name() == L"forceDark")
            {
                Logger::info(L"[Light Switch] Custom action triggered: Force Dark");
                SetSystemTheme(false);
                SetAppsTheme(false);
            }
        }
        catch (...)
        {
            Logger::error(L"[Light Switch] Invalid custom action JSON");
        }
    }

    // Called by the runner to pass the updated settings values as a serialized JSON.
    virtual void set_config(const wchar_t* config) override
    {
        try
        {
            auto values = PowerToysSettings::PowerToyValues::from_json_string(config, get_key());

            parse_hotkey(values);

            if (auto v = values.get_bool_value(L"changeSystem"))
            {
                g_settings.m_changeSystem = *v;
            }

            if (auto v = values.get_bool_value(L"changeApps"))
            {
                g_settings.m_changeApps = *v;
            }

            if (auto v = values.get_string_value(L"scheduleMode"))
            {
                g_settings.m_scheduleMode = FromString(*v);
            }

            if (auto v = values.get_int_value(L"lightTime"))
            {
                g_settings.m_lightTime = *v;
            }

            if (auto v = values.get_int_value(L"darkTime"))
            {
                g_settings.m_darkTime = *v;
            }

            if (auto v = values.get_int_value(L"sunrise_offset"))
            {
                g_settings.m_sunrise_offset = *v;
            }

            if (auto v = values.get_int_value(L"m_sunset_offset"))
            {
                g_settings.m_sunset_offset = *v;
            }

            if (auto v = values.get_string_value(L"latitude"))
            {
                g_settings.m_latitude = *v;
            }
            if (auto v = values.get_string_value(L"longitude"))
            {
                g_settings.m_longitude = *v;
            }

            values.save_to_settings_file();
        }
        catch (const std::exception&)
        {
            Logger::error("[Light Switch] set_config: Failed to parse or apply config.");
        }
    }

    virtual void enable()
    {
        m_enabled = true;
        Logger::info(L"Enabling Light Switch module...");

        unsigned long powertoys_pid = GetCurrentProcessId();
        std::wstring args = L"--pid " + std::to_wstring(powertoys_pid);
        std::wstring exe_name = L"LightSwitchService\\PowerToys.LightSwitchService.exe";

        std::wstring resolved_path(MAX_PATH, L'\0');
        DWORD result = SearchPathW(
            nullptr,
            exe_name.c_str(),
            nullptr,
            static_cast<DWORD>(resolved_path.size()),
            resolved_path.data(),
            nullptr);

        if (result == 0 || result >= resolved_path.size())
        {
            Logger::error(
                L"Failed to locate Light Switch executable named '{}' at location '{}'",
                exe_name,
                resolved_path.c_str());
            return;
        }

        resolved_path.resize(result);
        Logger::debug(L"Resolved executable path: {}", resolved_path);

        std::wstring command_line = L"\"" + resolved_path + L"\" " + args;

        STARTUPINFO si = { sizeof(si) };
        PROCESS_INFORMATION pi;

        if (!CreateProcessW(
                resolved_path.c_str(),
                command_line.data(),
                nullptr,
                nullptr,
                TRUE,
                0,
                nullptr,
                nullptr,
                &si,
                &pi))
        {
            Logger::error(L"Failed to launch Light Switch process. {}", get_last_error_or_default(GetLastError()));
            return;
        }

        Logger::info(L"Light Switch process launched successfully (PID: {}).", pi.dwProcessId);
        m_process = pi.hProcess;
        CloseHandle(pi.hThread);
    }

    // Disable the powertoy
    virtual void disable()
    {
        Logger::info("Light Switch disabling");
        m_enabled = false;

        if (m_process)
        {
            constexpr DWORD timeout_ms = 1500;
            DWORD result = WaitForSingleObject(m_process, timeout_ms);

            if (result == WAIT_TIMEOUT)
            {
                Logger::warn("Light Switch: Process didn't exit in time. Forcing termination.");
                TerminateProcess(m_process, 0);
            }

            CloseHandle(m_manual_override_event_handle);
            m_manual_override_event_handle = nullptr;

            CloseHandle(m_process);
            m_process = nullptr;
        }
    }

    // Returns if the powertoys is enabled
    virtual bool is_enabled() override
    {
        return m_enabled;
    }

    void parse_hotkey(PowerToysSettings::PowerToyValues& settings)
    {
        auto settingsObject = settings.get_raw_json();
        if (settingsObject.GetView().Size())
        {
            try
            {
                Hotkey _temp_toggle_theme;
                auto jsonHotkeyObject = settingsObject.GetNamedObject(JSON_KEY_PROPERTIES).GetNamedObject(JSON_KEY_TOGGLE_THEME_HOTKEY).GetNamedObject(JSON_KEY_VALUE);
                _temp_toggle_theme.win = jsonHotkeyObject.GetNamedBoolean(JSON_KEY_WIN);
                _temp_toggle_theme.alt = jsonHotkeyObject.GetNamedBoolean(JSON_KEY_ALT);
                _temp_toggle_theme.shift = jsonHotkeyObject.GetNamedBoolean(JSON_KEY_SHIFT);
                _temp_toggle_theme.ctrl = jsonHotkeyObject.GetNamedBoolean(JSON_KEY_CTRL);
                _temp_toggle_theme.key = static_cast<unsigned char>(jsonHotkeyObject.GetNamedNumber(JSON_KEY_CODE));
                m_toggle_theme_hotkey = _temp_toggle_theme;
            }
            catch (...)
            {
                Logger::error("Failed to initialize Light Switch force dark mode shortcut from settings. Value will keep unchanged.");
            }
        }
        else
        {
            Logger::info("Light Switch settings are empty");
        }
    }

    virtual size_t get_hotkeys(Hotkey* hotkeys, size_t buffer_size) override
    {
        if (hotkeys && buffer_size >= 1)
        {
            hotkeys[0] = m_toggle_theme_hotkey;
        }
        return 1;
    }

    virtual bool on_hotkey(size_t hotkeyId) override
    {
        if (m_enabled)
        {
            Logger::trace(L"Light Switch hotkey pressed");
            if (!is_process_running())
            {
                enable();
            }
            else if (hotkeyId == 0)
            {
                // get current will return true if in light mode; otherwise false
                Logger::info(L"[Light Switch] Hotkey triggered: Toggle Theme");
                if (g_settings.m_changeSystem)
                {
                    SetSystemTheme(!GetCurrentSystemTheme());
                }
                if (g_settings.m_changeApps)
                {
                    SetAppsTheme(!GetCurrentAppsTheme());
                }

                if (m_manual_override_event_handle)
                {
                    SetEvent(m_manual_override_event_handle);
                    Logger::debug(L"[Light Switch] Manual override event set");
                }
            }

            return true;
        }

        return false;
    }

    bool is_process_running()
    {
        return WaitForSingleObject(m_process, 0) == WAIT_TIMEOUT;
    }
};

std::wstring utf8_to_wstring(const std::string& str)
{
    if (str.empty())
        return std::wstring();

    int size_needed = MultiByteToWideChar(
        CP_UTF8,
        0,
        str.c_str(),
        static_cast<int>(str.size()),
        nullptr,
        0);

    std::wstring wstr(size_needed, 0);

    MultiByteToWideChar(
        CP_UTF8,
        0,
        str.c_str(),
        static_cast<int>(str.size()),
        &wstr[0],
        size_needed);

    return wstr;
}

// Load the settings file.
void LightSwitchInterface::init_settings()
{
    Logger::info(L"[Light Switch] init_settings: starting to load settings for module");

    try
    {
        PowerToysSettings::PowerToyValues settings =
            PowerToysSettings::PowerToyValues::load_from_settings_file(get_name());

        parse_hotkey(settings);

        if (auto v = settings.get_bool_value(L"changeSystem"))
            g_settings.m_changeSystem = *v;
        if (auto v = settings.get_bool_value(L"changeApps"))
            g_settings.m_changeApps = *v;
        if (auto v = settings.get_string_value(L"scheduleMode"))
            g_settings.m_scheduleMode = FromString(*v);
        if (auto v = settings.get_int_value(L"lightTime"))
            g_settings.m_lightTime = *v;
        if (auto v = settings.get_int_value(L"darkTime"))
            g_settings.m_darkTime = *v;
        if (auto v = settings.get_int_value(L"sunrise_offset"))
            g_settings.m_sunrise_offset = *v;
        if (auto v = settings.get_int_value(L"sunset_offset"))
            g_settings.m_sunset_offset = *v;
        if (auto v = settings.get_string_value(L"latitude"))
            g_settings.m_latitude = *v;
        if (auto v = settings.get_string_value(L"longitude"))
            g_settings.m_longitude = *v;

        Logger::info(L"[Light Switch] init_settings: loaded successfully");
    }
    catch (const winrt::hresult_error& e)
    {
        Logger::error(L"[Light Switch] init_settings: hresult_error 0x{:08X} - {}", e.code(), e.message().c_str());
    }
    catch (const std::exception& e)
    {
        std::wstring whatStr = utf8_to_wstring(e.what());
        Logger::error(L"[Light Switch] init_settings: std::exception - {}", whatStr);
    }
    catch (...)
    {
        Logger::error(L"[Light Switch] init_settings: unknown exception while loading settings");
    }
}

extern "C" __declspec(dllexport) PowertoyModuleIface* __cdecl powertoy_create()
{
    return new LightSwitchInterface();
}