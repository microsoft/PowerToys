#include "pch.h"
#include <interface/powertoy_module_interface.h>
#include <common/SettingsAPI/settings_objects.h>
#include "trace.h"
#include "MouseHighlighter.h"
#include "common/utils/color.h"

namespace
{
    const wchar_t JSON_KEY_PROPERTIES[] = L"properties";
    const wchar_t JSON_KEY_VALUE[] = L"value";
    const wchar_t JSON_KEY_ACTIVATION_SHORTCUT[] = L"activation_shortcut";
    const wchar_t JSON_KEY_LEFT_BUTTON_CLICK_COLOR[] = L"left_button_click_color";
    const wchar_t JSON_KEY_RIGHT_BUTTON_CLICK_COLOR[] = L"right_button_click_color";
    const wchar_t JSON_KEY_HIGHLIGHT_OPACITY[] = L"highlight_opacity";
    const wchar_t JSON_KEY_HIGHLIGHT_RADIUS[] = L"highlight_radius";
    const wchar_t JSON_KEY_HIGHLIGHT_FADE_DELAY_MS[] = L"highlight_fade_delay_ms";
    const wchar_t JSON_KEY_HIGHLIGHT_FADE_DURATION_MS[] = L"highlight_fade_duration_ms";
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
const static wchar_t* MODULE_NAME = L"MouseHighlighter";
// Add a description that will we shown in the module settings page.
const static wchar_t* MODULE_DESC = L"<no description>";

// Implement the PowerToy Module Interface and all the required methods.
class MouseHighlighter : public PowertoyModuleIface
{
private:
    // The PowerToy state.
    bool m_enabled = false;

    // Hotkey to invoke the module
    HotkeyEx m_hotkey;

    // Mouse Highlighter specific settings
    MouseHighlighterSettings m_highlightSettings;

public:
    // Constructor
    MouseHighlighter()
    {
        LoggerHelpers::init_logger(MODULE_NAME, L"ModuleInterface", LogSettings::mouseHighlighterLoggerName);
        init_settings();
    };

    // Destroy the powertoy and free memory
    virtual void destroy() override
    {
        delete this;
    }

    // Return the localized display name of the powertoy
    virtual const wchar_t* get_name() override
    {
        return MODULE_NAME;
    }

    // Return the non localized key of the powertoy, this will be cached by the runner
    virtual const wchar_t* get_key() override
    {
        return MODULE_NAME;
    }

    // Return the configured status for the gpo policy for the module
    virtual powertoys_gpo::gpo_rule_configured_t gpo_policy_enabled_configuration() override
    {
        return powertoys_gpo::getConfiguredMouseHighlighterEnabledValue();
    }

    // Return JSON with the configuration options.
    virtual bool get_config(wchar_t* buffer, int* buffer_size) override
    {
        HINSTANCE hinstance = reinterpret_cast<HINSTANCE>(&__ImageBase);
        PowerToysSettings::Settings settings(hinstance, get_name());
        return settings.serialize_to_buffer(buffer, buffer_size);
    }

    // Signal from the Settings editor to call a custom action.
    // This can be used to spawn more complex editors.
    virtual void call_custom_action(const wchar_t* action) override
    {
    }

    // Called by the runner to pass the updated settings values as a serialized JSON.
    virtual void set_config(const wchar_t* config) override
    {
        try
        {
            // Parse the input JSON string.
            PowerToysSettings::PowerToyValues values =
                PowerToysSettings::PowerToyValues::from_json_string(config, get_key());

            parse_settings(values);

            MouseHighlighterApplySettings(m_highlightSettings);
        }
        catch (std::exception&)
        {
            Logger::error("Invalid json when trying to parse Mouse Highlighter settings json.");
        }
    }

    // Enable the powertoy
    virtual void enable()
    {
        m_enabled = true;
        Trace::EnableMouseHighlighter(true);
        std::thread([=]() { MouseHighlighterMain(m_hModule, m_highlightSettings); }).detach();
    }

    // Disable the powertoy
    virtual void disable()
    {
        m_enabled = false;
        Trace::EnableMouseHighlighter(false);
        MouseHighlighterDisable();
    }

    // Returns if the powertoys is enabled
    virtual bool is_enabled() override
    {
        return m_enabled;
    }

    virtual std::optional<HotkeyEx> GetHotkeyEx() override
    {
        return m_hotkey;
    }

    virtual void OnHotkeyEx() override
    {
        MouseHighlighterSwitch();
    }

    // Load the settings file.
    void init_settings()
    {
        try
        {
            // Load and parse the settings file for this PowerToy.
            PowerToysSettings::PowerToyValues settings =
                PowerToysSettings::PowerToyValues::load_from_settings_file(MouseHighlighter::get_key());
            parse_settings(settings);
        }
        catch (std::exception&)
        {
            Logger::error("Invalid json when trying to load the Mouse Highlighter settings json from file.");
        }
    }

    void parse_settings(PowerToysSettings::PowerToyValues& settings)
    {
        // TODO: refactor to use common/utils/json.h instead
        auto settingsObject = settings.get_raw_json();
        MouseHighlighterSettings highlightSettings;
        if (settingsObject.GetView().Size())
        {
            try
            {
                // Parse HotKey
                auto jsonPropertiesObject = settingsObject.GetNamedObject(JSON_KEY_PROPERTIES).GetNamedObject(JSON_KEY_ACTIVATION_SHORTCUT);
                auto hotkey = PowerToysSettings::HotkeyObject::from_json(jsonPropertiesObject);
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

                m_hotkey.vkCode = hotkey.get_code();
            }
            catch (...)
            {
                Logger::warn("Failed to initialize Mouse Highlighter activation shortcut");
            }
            uint8_t opacity = MOUSE_HIGHLIGHTER_DEFAULT_OPACITY;
            try
            {
                // Parse Opacity
                auto jsonPropertiesObject = settingsObject.GetNamedObject(JSON_KEY_PROPERTIES).GetNamedObject(JSON_KEY_HIGHLIGHT_OPACITY);
                int value = static_cast<int>(jsonPropertiesObject.GetNamedNumber(JSON_KEY_VALUE));
                if (value >= 0)
                {
                    opacity = value;
                }
                else
                {
                    throw;
                }
            }
            catch (...)
            {
                Logger::warn("Failed to initialize Opacity from settings. Will use default value");
            }

            // Convert % to uint8_t
            if ((std::wstring)settingsObject.GetNamedString(L"version") != L"1.0")
            {
                opacity = opacity * 255 / 100;
            }

            try
            {
                // Parse left button click color
                auto jsonPropertiesObject = settingsObject.GetNamedObject(JSON_KEY_PROPERTIES).GetNamedObject(JSON_KEY_LEFT_BUTTON_CLICK_COLOR);
                auto leftColor = (std::wstring)jsonPropertiesObject.GetNamedString(JSON_KEY_VALUE);
                uint8_t r, g, b;
                if (!checkValidRGB(leftColor, &r, &g, &b))
                {
                    Logger::error("Left click color RGB value is invalid. Will use default value");
                }
                else
                {
                    highlightSettings.leftButtonColor = winrt::Windows::UI::ColorHelper::FromArgb(opacity, r, g, b);
                }
            }
            catch (...)
            {
                Logger::warn("Failed to initialize left click color from settings. Will use default value");
            }
            try
            {
                // Parse right button click color
                auto jsonPropertiesObject = settingsObject.GetNamedObject(JSON_KEY_PROPERTIES).GetNamedObject(JSON_KEY_RIGHT_BUTTON_CLICK_COLOR);
                auto rightColor = (std::wstring)jsonPropertiesObject.GetNamedString(JSON_KEY_VALUE);
                uint8_t r, g, b;
                if (!checkValidRGB(rightColor, &r, &g, &b))
                {
                    Logger::error("Right click color RGB value is invalid. Will use default value");
                }
                else
                {
                    highlightSettings.rightButtonColor = winrt::Windows::UI::ColorHelper::FromArgb(opacity, r, g, b);
                }
            }
            catch (...)
            {
                Logger::warn("Failed to initialize right click color from settings. Will use default value");
            }
            try
            {
                // Parse Radius
                auto jsonPropertiesObject = settingsObject.GetNamedObject(JSON_KEY_PROPERTIES).GetNamedObject(JSON_KEY_HIGHLIGHT_RADIUS);
                int value = static_cast<int>(jsonPropertiesObject.GetNamedNumber(JSON_KEY_VALUE));
                if (value >= 0)
                {
                    highlightSettings.radius = value;
                }
                else
                {
                    throw;
                }
            }
            catch (...)
            {
                Logger::warn("Failed to initialize Radius from settings. Will use default value");
            }
            try
            {
                // Parse Fade Delay
                auto jsonPropertiesObject = settingsObject.GetNamedObject(JSON_KEY_PROPERTIES).GetNamedObject(JSON_KEY_HIGHLIGHT_FADE_DELAY_MS);
                int value = static_cast<int>(jsonPropertiesObject.GetNamedNumber(JSON_KEY_VALUE));
                if (value >= 0)
                {
                    highlightSettings.fadeDelayMs = value;
                }
                else
                {
                    throw;
                }
            }
            catch (...)
            {
                Logger::warn("Failed to initialize Fade Delay from settings. Will use default value");
            }
            try
            {
                // Parse Fade Duration
                auto jsonPropertiesObject = settingsObject.GetNamedObject(JSON_KEY_PROPERTIES).GetNamedObject(JSON_KEY_HIGHLIGHT_FADE_DURATION_MS);
                int value = static_cast<int>(jsonPropertiesObject.GetNamedNumber(JSON_KEY_VALUE));
                if (value >= 0)
                {
                    highlightSettings.fadeDurationMs = value;
                }
                else
                {
                    throw;
                }
            }
            catch (...)
            {
                Logger::warn("Failed to initialize Fade Duration from settings. Will use default value");
            }
        }
        else
        {
            Logger::info("Mouse Highlighter settings are empty");
        }
        if (!m_hotkey.modifiersMask)
        {
            Logger::info("Mouse Highlighter is going to use default shortcut");
            m_hotkey.modifiersMask = MOD_SHIFT | MOD_WIN;
            m_hotkey.vkCode = 0x48; // H key
        }
        m_highlightSettings = highlightSettings;
    }
};

extern "C" __declspec(dllexport) PowertoyModuleIface* __cdecl powertoy_create()
{
    return new MouseHighlighter();
}