// dllmain.cpp : Defines the entry point for the DLL application.
#include "pch.h"

#include <interface/powertoy_module_interface.h>
#include <common/SettingsAPI/settings_objects.h>
#include <common/utils/gpo.h>

#include "AltWindowCycle.h"
#include "trace.h"

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

namespace
{
    const wchar_t JSON_KEY_PROPERTIES[] = L"properties";
    const wchar_t JSON_KEY_WIN[] = L"win";
    const wchar_t JSON_KEY_ALT[] = L"alt";
    const wchar_t JSON_KEY_CTRL[] = L"ctrl";
    const wchar_t JSON_KEY_SHIFT[] = L"shift";
    const wchar_t JSON_KEY_CODE[] = L"code";
    const wchar_t JSON_KEY_NEXT_WINDOW_SHORTCUT[] = L"next_window_shortcut";
    const wchar_t JSON_KEY_PREVIOUS_WINDOW_SHORTCUT[] = L"previous_window_shortcut";

    // ` ~ key (VK_OEM_3) on US layouts.
    const unsigned char DEFAULT_BACKTICK_VK = 0xC0;

    // Hotkey ids, matching the order returned by get_hotkeys() (and the order in
    // the Settings UI). on_hotkey() receives these indices.
    enum HotkeyId : size_t
    {
        HotkeyNext = 0,
        HotkeyPrevious = 1
    };
}

// Implement the PowerToy Module Interface and all the required methods.
class AltWindowCycle : public PowertoyModuleIface
{
private:
    bool m_enabled = false;

    // Cycle to the next window of the focused app.
    PowertoyModuleIface::Hotkey m_nextHotkey;
    // Cycle to the previous window of the focused app.
    PowertoyModuleIface::Hotkey m_previousHotkey;

    void init_settings();
    void parse_settings(PowerToysSettings::PowerToyValues& settings);

    static void parse_hotkey(const winrt::Windows::Data::Json::JsonObject& properties,
                             const wchar_t* key,
                             PowertoyModuleIface::Hotkey& hotkey)
    {
        try
        {
            auto jsonHotkeyObject = properties.GetNamedObject(key);
            hotkey.win = jsonHotkeyObject.GetNamedBoolean(JSON_KEY_WIN);
            hotkey.alt = jsonHotkeyObject.GetNamedBoolean(JSON_KEY_ALT);
            hotkey.shift = jsonHotkeyObject.GetNamedBoolean(JSON_KEY_SHIFT);
            hotkey.ctrl = jsonHotkeyObject.GetNamedBoolean(JSON_KEY_CTRL);
            hotkey.key = static_cast<unsigned char>(jsonHotkeyObject.GetNamedNumber(JSON_KEY_CODE));
        }
        catch (...)
        {
            Logger::error("Failed to initialize AltWindowCycle shortcut from settings");
        }
    }

public:
    AltWindowCycle()
    {
        LoggerHelpers::init_logger(L"AltWindowCycle", L"ModuleInterface", "AltWindowCycle");
        init_settings();
    }

    virtual void destroy() override
    {
        ShutdownAltWindowCycle(); // idempotent
        delete this;
    }

    virtual const wchar_t* get_name() override
    {
        return L"AltWindowCycle";
    }

    virtual const wchar_t* get_key() override
    {
        return L"AltWindowCycle";
    }

    virtual powertoys_gpo::gpo_rule_configured_t gpo_policy_enabled_configuration() override
    {
        return powertoys_gpo::getConfiguredAltWindowCycleEnabledValue();
    }

    virtual bool get_config(wchar_t* buffer, int* buffer_size) override
    {
        HINSTANCE hinstance = reinterpret_cast<HINSTANCE>(&__ImageBase);

        PowerToysSettings::Settings settings(hinstance, get_name());
        settings.set_description(L"Cycle between the windows of the currently focused application.");

        return settings.serialize_to_buffer(buffer, buffer_size);
    }

    virtual void call_custom_action(const wchar_t* /*action*/) override
    {
    }

    virtual void set_config(const wchar_t* config) override
    {
        try
        {
            PowerToysSettings::PowerToyValues values =
                PowerToysSettings::PowerToyValues::from_json_string(config, get_key());

            parse_settings(values);
            values.save_to_settings_file();
        }
        catch (std::exception&)
        {
            // Improper JSON.
        }
    }

    virtual void enable() override
    {
        m_enabled = true;
        Trace::EnableAltWindowCycle(true);
        InitializeAltWindowCycle(reinterpret_cast<HINSTANCE>(&__ImageBase));
    }

    virtual void disable() override
    {
        m_enabled = false;
        Trace::EnableAltWindowCycle(false);
        ShutdownAltWindowCycle();
    }

    virtual bool is_enabled() override
    {
        return m_enabled;
    }

    virtual size_t get_hotkeys(Hotkey* hotkeys, size_t buffer_size) override
    {
        if (hotkeys && buffer_size >= 2)
        {
            hotkeys[HotkeyNext] = m_nextHotkey;
            hotkeys[HotkeyPrevious] = m_previousHotkey;
        }

        return 2;
    }

    virtual bool on_hotkey(size_t hotkeyId) override
    {
        if (!m_enabled)
        {
            return false;
        }

        if (hotkeyId == HotkeyNext)
        {
            Logger::trace(L"AltWindowCycle next-window hotkey pressed");
            Trace::CycleWindow(true);
            return HandleAltWindowCycleHotkey(true);
        }

        if (hotkeyId == HotkeyPrevious)
        {
            Logger::trace(L"AltWindowCycle previous-window hotkey pressed");
            Trace::CycleWindow(false);
            return HandleAltWindowCycleHotkey(false);
        }

        return false;
    }
};

void AltWindowCycle::init_settings()
{
    try
    {
        PowerToysSettings::PowerToyValues settings =
            PowerToysSettings::PowerToyValues::load_from_settings_file(AltWindowCycle::get_key());
        parse_settings(settings);
    }
    catch (std::exception&)
    {
        // Error while loading from the settings file. Let default values stay as they are.
    }
}

void AltWindowCycle::parse_settings(PowerToysSettings::PowerToyValues& settings)
{
    // Reset to defaults before parsing so removed/invalid values fall back cleanly.
    m_nextHotkey = PowertoyModuleIface::Hotkey{};
    m_previousHotkey = PowertoyModuleIface::Hotkey{};

    auto settingsObject = settings.get_raw_json();
    if (settingsObject.GetView().Size() && settingsObject.HasKey(JSON_KEY_PROPERTIES))
    {
        auto properties = settingsObject.GetNamedObject(JSON_KEY_PROPERTIES);
        if (properties.HasKey(JSON_KEY_NEXT_WINDOW_SHORTCUT))
        {
            parse_hotkey(properties, JSON_KEY_NEXT_WINDOW_SHORTCUT, m_nextHotkey);
        }
        if (properties.HasKey(JSON_KEY_PREVIOUS_WINDOW_SHORTCUT))
        {
            parse_hotkey(properties, JSON_KEY_PREVIOUS_WINDOW_SHORTCUT, m_previousHotkey);
        }
    }
    else
    {
        Logger::info("AltWindowCycle settings are empty");
    }

    // Default: Alt+` cycles to the next window of the focused app.
    if (!m_nextHotkey.key)
    {
        m_nextHotkey.win = false;
        m_nextHotkey.ctrl = false;
        m_nextHotkey.shift = false;
        m_nextHotkey.alt = true;
        m_nextHotkey.key = DEFAULT_BACKTICK_VK;
    }

    // Default: Shift+Alt+` cycles to the previous window of the focused app.
    if (!m_previousHotkey.key)
    {
        m_previousHotkey.win = false;
        m_previousHotkey.ctrl = false;
        m_previousHotkey.shift = true;
        m_previousHotkey.alt = true;
        m_previousHotkey.key = DEFAULT_BACKTICK_VK;
    }
}

extern "C" __declspec(dllexport) PowertoyModuleIface* __cdecl powertoy_create()
{
    return new AltWindowCycle();
}
