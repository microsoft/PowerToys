#include "pch.h"
#include <interface/powertoy_module_interface.h>
#include <common/SettingsAPI/settings_objects.h>
#include "trace.h"
#include "LaserPointer.h"
#include "common/utils/color.h"
#include <common/utils/EventWaiter.h>
#include <common/interop/shared_constants.h>

#include <algorithm>

namespace
{
    const wchar_t JSON_KEY_PROPERTIES[] = L"properties";
    const wchar_t JSON_KEY_VALUE[] = L"value";
    const wchar_t JSON_KEY_ACTIVATION_SHORTCUT[] = L"activation_shortcut";
    const wchar_t JSON_KEY_PEN_ACTIVATION_SHORTCUT[] = L"pen_activation_shortcut";
    const wchar_t JSON_KEY_PRESENTER_ACTIVATION_SHORTCUT[] = L"presenter_activation_shortcut";
    const wchar_t JSON_KEY_PRESENTER_STOP_SHORTCUT[] = L"presenter_stop_shortcut";
    const wchar_t JSON_KEY_ACTIVATION_BUTTON[] = L"activation_button";
    const wchar_t JSON_KEY_ALWAYS_ON_BUTTON[] = L"always_on_button";
    const wchar_t JSON_KEY_SUPPRESS_ACTIVATION_BUTTON[] = L"suppress_activation_button";
    const wchar_t JSON_KEY_AUTO_ACTIVATE[] = L"auto_activate";
    const wchar_t JSON_KEY_GLOW_ENABLED[] = L"glow_enabled";
    const wchar_t JSON_KEY_PEN_RENDER_WHEN_CLOSE[] = L"pen_render_when_close";
    const wchar_t JSON_KEY_LASER_COLOR[] = L"laser_color";
    const wchar_t JSON_KEY_LASER_SIZE[] = L"laser_size";
    const wchar_t JSON_KEY_DECAY_TIME_MS[] = L"decay_time_ms";
    const wchar_t JSON_KEY_DECAY_LENGTH[] = L"decay_length";
    const wchar_t JSON_KEY_STREAMLINE[] = L"streamline";
}

extern "C" IMAGE_DOS_HEADER __ImageBase;

HMODULE m_hModule;

BOOL APIENTRY DllMain(HMODULE hModule, DWORD ul_reason_for_call, LPVOID lpReserved)
{
    m_hModule = hModule;
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
const static wchar_t* MODULE_NAME = L"LaserPointer";
// Add a description that will we shown in the module settings page.
const static wchar_t* MODULE_DESC = L"<no description>";

// Implement the PowerToy Module Interface and all the required methods.
class LaserPointer : public PowertoyModuleIface
{
private:
    // The PowerToy state.
    bool m_enabled = false;

    // Two independent shortcuts: one arms the mouse, one arms the pen. The order here
    // must match GetAllHotkeyAccessors() on the settings side, because the runner
    // identifies them by index.
    enum HotkeyId : size_t
    {
        HotkeyMouse = 0,
        HotkeyPen = 1,
        // S shares: it starts sharing, or moves the share to the next window. X stops.
        // Two one-way shortcuts rather than a toggle, so neither ever does the opposite
        // of what was intended.
        HotkeyPresenterShare = 2,
        HotkeyPresenterStop = 3,
        HotkeyCount = 4,
    };

    Hotkey m_hotkeys[HotkeyCount]{};

    // Laser Pointer specific settings
    LaserPointerSettings m_laserPointerSettings;

    // Event-driven trigger support
    EventWaiter m_triggerEventWaiter;

    // Quick Access drives sharing through events of its own, one per button.
    EventWaiter m_presenterEventWaiter;
    EventWaiter m_presenterStopEventWaiter;

public:
    LaserPointer()
    {
        LoggerHelpers::init_logger(MODULE_NAME, L"ModuleInterface", LogSettings::laserPointerLoggerName);
        init_settings();
    };

    virtual void destroy() override
    {
        // Tear down threads/handles before deletion to avoid abort() on joinable threads during shutdown
        disable();
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
        return powertoys_gpo::getConfiguredLaserPointerEnabledValue();
    }

    virtual bool get_config(wchar_t* buffer, int* buffer_size) override
    {
        HINSTANCE hinstance = reinterpret_cast<HINSTANCE>(&__ImageBase);
        PowerToysSettings::Settings settings(hinstance, get_name());
        return settings.serialize_to_buffer(buffer, buffer_size);
    }

    virtual void call_custom_action(const wchar_t* action) override
    {
    }

    virtual void set_config(const wchar_t* config) override
    {
        try
        {
            PowerToysSettings::PowerToyValues values =
                PowerToysSettings::PowerToyValues::from_json_string(config, get_key());

            parse_settings(values);

            LaserPointerApplySettings(m_laserPointerSettings);
        }
        catch (std::exception&)
        {
            Logger::error("Invalid json when trying to parse Laser Pointer settings json.");
        }
    }

    virtual void enable()
    {
        m_enabled = true;
        Trace::EnableLaserPointer(true);
        std::thread([=]() { LaserPointerMain(m_hModule, m_laserPointerSettings); }).detach();

        // Start listening for external trigger event so we can invoke the same logic as the hotkey.
        m_triggerEventWaiter.start(CommonSharedConstants::LASER_POINTER_TRIGGER_EVENT, [this](DWORD) {
            LaserPointerSwitch();
        });

        m_presenterEventWaiter.start(CommonSharedConstants::LASER_POINTER_PRESENTER_EVENT, [this](DWORD) {
            LaserPointerShareWindowExternal();
        });

        m_presenterStopEventWaiter.start(CommonSharedConstants::LASER_POINTER_PRESENTER_STOP_EVENT, [this](DWORD) {
            LaserPointerStopSharing();
        });
    }

    virtual void disable()
    {
        m_enabled = false;
        Trace::EnableLaserPointer(false);
        LaserPointerDisable();

        m_triggerEventWaiter.stop();
        m_presenterEventWaiter.stop();
        m_presenterStopEventWaiter.stop();
    }

    virtual bool is_enabled() override
    {
        return m_enabled;
    }

    virtual size_t get_hotkeys(Hotkey* hotkeys, size_t buffer_size) override
    {
        if (hotkeys && buffer_size >= HotkeyCount)
        {
            std::copy(std::begin(m_hotkeys), std::end(m_hotkeys), hotkeys);

            // The pen shortcut has no default, so it is usually unset. The runner
            // registers every hotkey whose isShown is true, so an unset one would be
            // registered with no key at all and would show up in conflict detection.
            // The count has to stay constant either way, because on_hotkey identifies
            // hotkeys by index.
            for (size_t i = 0; i < HotkeyCount; i++)
            {
                if (hotkeys[i].key == 0)
                {
                    hotkeys[i].isShown = false;
                }
            }
        }

        return HotkeyCount;
    }

    virtual bool on_hotkey(size_t hotkeyId) override
    {
        if (!m_enabled)
        {
            return false;
        }

        switch (hotkeyId)
        {
        case HotkeyPen:
            LaserPointerSwitchPen();
            break;
        case HotkeyPresenterShare:
            LaserPointerShareWindow();
            break;
        case HotkeyPresenterStop:
            LaserPointerStopSharing();
            break;
        default:
            LaserPointerSwitch();
            break;
        }

        return true;
    }

    void init_settings()
    {
        try
        {
            PowerToysSettings::PowerToyValues settings =
                PowerToysSettings::PowerToyValues::load_from_settings_file(LaserPointer::get_key());
            parse_settings(settings);
        }
        catch (std::exception&)
        {
            Logger::error("Invalid json when trying to load the Laser Pointer settings json from file.");
        }
    }

    // Reads one shortcut into m_hotkeys[id]. A missing or malformed entry leaves the
    // slot cleared, which the runner treats as "not registered".
    void parse_hotkey(const winrt::Windows::Data::Json::JsonObject& settingsObject,
                      const wchar_t* jsonKey,
                      HotkeyId id,
                      const wchar_t* label)
    {
        try
        {
            auto jsonPropertiesObject = settingsObject.GetNamedObject(JSON_KEY_PROPERTIES).GetNamedObject(jsonKey);
            auto hotkey = PowerToysSettings::HotkeyObject::from_json(jsonPropertiesObject);

            m_hotkeys[id] = Hotkey{
                .win = hotkey.win_pressed(),
                .ctrl = hotkey.ctrl_pressed(),
                .shift = hotkey.shift_pressed(),
                .alt = hotkey.alt_pressed(),
                .key = static_cast<unsigned char>(hotkey.get_code()),
            };
        }
        catch (...)
        {
            m_hotkeys[id] = Hotkey{};
            Logger::warn(L"Failed to initialize Laser Pointer {} shortcut", label);
        }
    }

    void parse_settings(PowerToysSettings::PowerToyValues& settings)
    {
        auto settingsObject = settings.get_raw_json();
        LaserPointerSettings laserPointerSettings;
        if (settingsObject.GetView().Size())
        {
            parse_hotkey(settingsObject, JSON_KEY_ACTIVATION_SHORTCUT, HotkeyMouse, L"activation");
            parse_hotkey(settingsObject, JSON_KEY_PEN_ACTIVATION_SHORTCUT, HotkeyPen, L"pen activation");
            parse_hotkey(settingsObject, JSON_KEY_PRESENTER_ACTIVATION_SHORTCUT, HotkeyPresenterShare, L"share window");
            parse_hotkey(settingsObject, JSON_KEY_PRESENTER_STOP_SHORTCUT, HotkeyPresenterStop, L"stop sharing");
            try
            {
                // Parse activation button
                auto jsonPropertiesObject = settingsObject.GetNamedObject(JSON_KEY_PROPERTIES).GetNamedObject(JSON_KEY_ACTIVATION_BUTTON);
                int value = static_cast<int>(jsonPropertiesObject.GetNamedNumber(JSON_KEY_VALUE));
                if (value >= static_cast<int>(LaserPointerButton::Left) && value <= static_cast<int>(LaserPointerButton::X2))
                {
                    laserPointerSettings.activationButton = static_cast<LaserPointerButton>(value);
                }
                else
                {
                    throw std::runtime_error("Invalid activation button value");
                }
            }
            catch (...)
            {
                Logger::warn("Failed to initialize activation button from settings. Will use default value");
            }
            try
            {
                // Parse always-on button. Unlike the activation button this one is not
                // gated on the shortcut, so Left and Right are rejected outright: a
                // permanently swallowed primary button would make the machine unusable.
                auto jsonPropertiesObject = settingsObject.GetNamedObject(JSON_KEY_PROPERTIES).GetNamedObject(JSON_KEY_ALWAYS_ON_BUTTON);
                int value = static_cast<int>(jsonPropertiesObject.GetNamedNumber(JSON_KEY_VALUE));
                if (value == static_cast<int>(LaserPointerButton::None) ||
                    value == static_cast<int>(LaserPointerButton::Middle) ||
                    value == static_cast<int>(LaserPointerButton::X1) ||
                    value == static_cast<int>(LaserPointerButton::X2))
                {
                    laserPointerSettings.alwaysOnButton = static_cast<LaserPointerButton>(value);
                }
                else
                {
                    throw std::runtime_error("Invalid always-on button value");
                }
            }
            catch (...)
            {
                Logger::warn("Failed to initialize always-on button from settings. Will use default value");
            }
            try
            {
                // Parse suppress activation button
                auto jsonPropertiesObject = settingsObject.GetNamedObject(JSON_KEY_PROPERTIES).GetNamedObject(JSON_KEY_SUPPRESS_ACTIVATION_BUTTON);
                laserPointerSettings.suppressActivationButton = jsonPropertiesObject.GetNamedBoolean(JSON_KEY_VALUE);
            }
            catch (...)
            {
                Logger::warn("Failed to initialize suppress activation button from settings. Will use default value");
            }
            try
            {
                // Parse auto activate
                auto jsonPropertiesObject = settingsObject.GetNamedObject(JSON_KEY_PROPERTIES).GetNamedObject(JSON_KEY_AUTO_ACTIVATE);
                laserPointerSettings.autoActivate = jsonPropertiesObject.GetNamedBoolean(JSON_KEY_VALUE);
            }
            catch (...)
            {
                Logger::warn("Failed to initialize auto activate from settings. Will use default value");
            }
            try
            {
                // Parse pen proximity rendering
                auto jsonPropertiesObject = settingsObject.GetNamedObject(JSON_KEY_PROPERTIES).GetNamedObject(JSON_KEY_PEN_RENDER_WHEN_CLOSE);
                laserPointerSettings.penRenderWhenClose = jsonPropertiesObject.GetNamedBoolean(JSON_KEY_VALUE);
            }
            catch (...)
            {
                Logger::warn("Failed to initialize pen proximity rendering from settings. Will use default value");
            }
            try
            {
                // Parse glow enabled
                auto jsonPropertiesObject = settingsObject.GetNamedObject(JSON_KEY_PROPERTIES).GetNamedObject(JSON_KEY_GLOW_ENABLED);
                laserPointerSettings.glowEnabled = jsonPropertiesObject.GetNamedBoolean(JSON_KEY_VALUE);
            }
            catch (...)
            {
                Logger::warn("Failed to initialize glow from settings. Will use default value");
            }
            try
            {
                // Parse laser color
                auto jsonPropertiesObject = settingsObject.GetNamedObject(JSON_KEY_PROPERTIES).GetNamedObject(JSON_KEY_LASER_COLOR);
                auto color = static_cast<std::wstring>(jsonPropertiesObject.GetNamedString(JSON_KEY_VALUE));
                uint8_t a, r, g, b;
                if (!checkValidARGB(color, &a, &r, &g, &b))
                {
                    Logger::error("Laser color ARGB value is invalid. Will use default value");
                }
                else
                {
                    laserPointerSettings.laserColor = winrt::Windows::UI::ColorHelper::FromArgb(a, r, g, b);
                }
            }
            catch (...)
            {
                Logger::warn("Failed to initialize laser color from settings. Will use default value");
            }
            try
            {
                // Parse laser size
                auto jsonPropertiesObject = settingsObject.GetNamedObject(JSON_KEY_PROPERTIES).GetNamedObject(JSON_KEY_LASER_SIZE);
                int value = static_cast<int>(jsonPropertiesObject.GetNamedNumber(JSON_KEY_VALUE));
                if (value > 0)
                {
                    laserPointerSettings.size = value;
                }
                else
                {
                    throw std::runtime_error("Invalid laser size value");
                }
            }
            catch (...)
            {
                Logger::warn("Failed to initialize laser size from settings. Will use default value");
            }
            try
            {
                // Parse decay time
                auto jsonPropertiesObject = settingsObject.GetNamedObject(JSON_KEY_PROPERTIES).GetNamedObject(JSON_KEY_DECAY_TIME_MS);
                int value = static_cast<int>(jsonPropertiesObject.GetNamedNumber(JSON_KEY_VALUE));
                if (value > 0)
                {
                    laserPointerSettings.decayTimeMs = value;
                }
                else
                {
                    throw std::runtime_error("Invalid decay time value");
                }
            }
            catch (...)
            {
                Logger::warn("Failed to initialize decay time from settings. Will use default value");
            }
            try
            {
                // Parse decay length
                auto jsonPropertiesObject = settingsObject.GetNamedObject(JSON_KEY_PROPERTIES).GetNamedObject(JSON_KEY_DECAY_LENGTH);
                int value = static_cast<int>(jsonPropertiesObject.GetNamedNumber(JSON_KEY_VALUE));
                if (value > 1)
                {
                    laserPointerSettings.decayLength = value;
                }
                else
                {
                    throw std::runtime_error("Invalid decay length value");
                }
            }
            catch (...)
            {
                Logger::warn("Failed to initialize decay length from settings. Will use default value");
            }
            try
            {
                // Parse streamline
                auto jsonPropertiesObject = settingsObject.GetNamedObject(JSON_KEY_PROPERTIES).GetNamedObject(JSON_KEY_STREAMLINE);
                int value = static_cast<int>(jsonPropertiesObject.GetNamedNumber(JSON_KEY_VALUE));
                if (value >= 0 && value <= 95)
                {
                    laserPointerSettings.streamlinePercent = value;
                }
                else
                {
                    throw std::runtime_error("Invalid streamline value");
                }
            }
            catch (...)
            {
                Logger::warn("Failed to initialize streamline from settings. Will use default value");
            }
        }
        else
        {
            Logger::info("Laser Pointer settings are empty");
        }

        if (!m_hotkeys[HotkeyMouse].key)
        {
            Logger::info("Laser Pointer is going to use the default activation shortcut");
            m_hotkeys[HotkeyMouse] = Hotkey{ .win = true, .shift = true, .key = 0x4C }; // Win+Shift+L
        }
        m_hotkeys[HotkeyMouse].id = static_cast<int>(HotkeyMouse);

        // The pen shortcut has no default: leaving it unset is how that half of the
        // module stays switched off.
        m_hotkeys[HotkeyPen].id = static_cast<int>(HotkeyPen);

        if (!m_hotkeys[HotkeyPresenterShare].key)
        {
            m_hotkeys[HotkeyPresenterShare] = Hotkey{ .win = true, .ctrl = true, .shift = true, .key = 0x57 }; // Ctrl+Shift+Win+W
        }
        m_hotkeys[HotkeyPresenterShare].id = static_cast<int>(HotkeyPresenterShare);

        if (!m_hotkeys[HotkeyPresenterStop].key)
        {
            m_hotkeys[HotkeyPresenterStop] = Hotkey{ .win = true, .ctrl = true, .shift = true, .key = 0x51 }; // Ctrl+Shift+Win+Q
        }
        m_hotkeys[HotkeyPresenterStop].id = static_cast<int>(HotkeyPresenterStop);

        m_laserPointerSettings = laserPointerSettings;

        Logger::info("Laser Pointer settings resolved: button={} alwaysOn={} suppress={} penClose={} size={} decayMs={}",
                     static_cast<int>(m_laserPointerSettings.activationButton),
                     static_cast<int>(m_laserPointerSettings.alwaysOnButton),
                     m_laserPointerSettings.suppressActivationButton,
                     m_laserPointerSettings.penRenderWhenClose,
                     m_laserPointerSettings.size,
                     m_laserPointerSettings.decayTimeMs);
    }
};

extern "C" __declspec(dllexport) PowertoyModuleIface* __cdecl powertoy_create()
{
    return new LaserPointer();
}
