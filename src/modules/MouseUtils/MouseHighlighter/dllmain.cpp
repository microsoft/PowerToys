#include "pch.h"
#include <interface/powertoy_module_interface.h>
#include <common/SettingsAPI/settings_objects.h>
#include "trace.h"
#include "MouseHighlighter.h"
#include "common/utils/color.h"
#include <algorithm>
#include <vector>

namespace
{
    const wchar_t JSON_KEY_PROPERTIES[] = L"properties";
    const wchar_t JSON_KEY_VALUE[] = L"value";
    const wchar_t JSON_KEY_ACTIVATION_SHORTCUT[] = L"activation_shortcut";
    const wchar_t JSON_KEY_MODE_SWITCH_SHORTCUT[] = L"mode_switch_shortcut";
    const wchar_t JSON_KEY_LEFT_BUTTON_CLICK_COLOR[] = L"left_button_click_color";
    const wchar_t JSON_KEY_RIGHT_BUTTON_CLICK_COLOR[] = L"right_button_click_color";
    const wchar_t JSON_KEY_HIGHLIGHT_OPACITY[] = L"highlight_opacity";
    const wchar_t JSON_KEY_ALWAYS_COLOR[] = L"always_color";
    const wchar_t JSON_KEY_HIGHLIGHT_RADIUS[] = L"highlight_radius";
    const wchar_t JSON_KEY_HIGHLIGHT_FADE_DELAY_MS[] = L"highlight_fade_delay_ms";
    const wchar_t JSON_KEY_HIGHLIGHT_FADE_DURATION_MS[] = L"highlight_fade_duration_ms";
    const wchar_t JSON_KEY_AUTO_ACTIVATE[] = L"auto_activate";
    const wchar_t JSON_KEY_SPOTLIGHT_MODE[] = L"spotlight_mode";
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

    // Hotkeys to invoke the module
    std::vector<PowertoyModuleIface::Hotkey> m_hotkeys;

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

    virtual size_t get_hotkeys(Hotkey* buffer, size_t buffer_size) override
    {
        if (buffer == nullptr || buffer_size == 0)
        {
            return m_hotkeys.size();
        }

        size_t copied = std::min(buffer_size, m_hotkeys.size());
        std::copy_n(m_hotkeys.begin(), copied, buffer);
        return copied;
    }

    virtual bool on_hotkey(size_t hotkeyId) override
    {
        if (hotkeyId == 0)
        {
            MouseHighlighterSwitch();
        }
        else if (hotkeyId == 1)
        {
            MouseHighlighterSwitchMode();
        }
        return true;
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
                // Initialize hotkeys vector
                m_hotkeys.clear();
                m_hotkeys.resize(2); // Activation and Mode Switch hotkeys

                // Parse Activation HotKey
                auto jsonPropertiesObject = settingsObject.GetNamedObject(JSON_KEY_PROPERTIES).GetNamedObject(JSON_KEY_ACTIVATION_SHORTCUT);
                auto hotkey = PowerToysSettings::HotkeyObject::from_json(jsonPropertiesObject);
                
                m_hotkeys[0].win = hotkey.win_pressed();
                m_hotkeys[0].ctrl = hotkey.ctrl_pressed();
                m_hotkeys[0].shift = hotkey.shift_pressed();
                m_hotkeys[0].alt = hotkey.alt_pressed();
                m_hotkeys[0].key = hotkey.get_code();
                m_hotkeys[0].id = 0;
            }
            catch (...)
            {
                Logger::warn("Failed to initialize Mouse Highlighter activation shortcut");
            }

            try
            {
                // Parse Mode Switch HotKey
                auto jsonPropertiesObject = settingsObject.GetNamedObject(JSON_KEY_PROPERTIES).GetNamedObject(JSON_KEY_MODE_SWITCH_SHORTCUT);
                auto hotkey = PowerToysSettings::HotkeyObject::from_json(jsonPropertiesObject);
                
                m_hotkeys[1].win = hotkey.win_pressed();
                m_hotkeys[1].ctrl = hotkey.ctrl_pressed();
                m_hotkeys[1].shift = hotkey.shift_pressed();
                m_hotkeys[1].alt = hotkey.alt_pressed();
                m_hotkeys[1].key = hotkey.get_code();
                m_hotkeys[1].id = 1;
            }
            catch (...)
            {
                Logger::warn("Failed to initialize Mouse Highlighter mode switch shortcut");
            }
            // Migration from <=1.1
            auto version = (std::wstring)settingsObject.GetNamedString(L"version");
            auto migration = false;
            uint8_t opacity = 166;
            if (version == L"1.0" || version == L"1.1")
            {
                migration = true;
                try
                {
                    // Parse Opacity
                    auto jsonPropertiesObject = settingsObject.GetNamedObject(JSON_KEY_PROPERTIES).GetNamedObject(JSON_KEY_HIGHLIGHT_OPACITY);
                    int value = static_cast<int>(jsonPropertiesObject.GetNamedNumber(JSON_KEY_VALUE));
                    if (value >= 0)
                    {
                        if (version == L"1.0")
                        {
                            opacity = value;
                        }
                        else
                        {
                            // 1.1
                            opacity = value * 255 / 100;
                        }
                    }
                    else
                    {
                        throw std::runtime_error("Invalid Opacity value");
                    }
                }
                catch (...)
                {
                    Logger::warn("Failed to initialize Opacity from settings. Will use default value");
                }
            }
            try
            {
                // Parse left button click color
                auto jsonPropertiesObject = settingsObject.GetNamedObject(JSON_KEY_PROPERTIES).GetNamedObject(JSON_KEY_LEFT_BUTTON_CLICK_COLOR);
                auto leftColor = (std::wstring)jsonPropertiesObject.GetNamedString(JSON_KEY_VALUE);
                uint8_t a = opacity, r, g, b;
                if (!migration && !checkValidARGB(leftColor, &a, &r, &g, &b) || migration && !checkValidRGB(leftColor, &r, &g, &b))
                {
                    Logger::error("Left click color ARGB value is invalid. Will use default value");
                }
                else
                {
                    highlightSettings.leftButtonColor = winrt::Windows::UI::ColorHelper::FromArgb(a, r, g, b);
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
                uint8_t a = opacity, r, g, b;
                if (!migration && !checkValidARGB(rightColor, &a, &r, &g, &b) || migration && !checkValidRGB(rightColor, &r, &g, &b))
                {
                    Logger::error("Right click color ARGB value is invalid. Will use default value");
                }
                else
                {
                    highlightSettings.rightButtonColor = winrt::Windows::UI::ColorHelper::FromArgb(a, r, g, b);
                }
            }
            catch (...)
            {
                Logger::warn("Failed to initialize right click color from settings. Will use default value");
            }
            try
            {
                // Parse always color
                auto jsonPropertiesObject = settingsObject.GetNamedObject(JSON_KEY_PROPERTIES).GetNamedObject(JSON_KEY_ALWAYS_COLOR);
                auto alwaysColor = (std::wstring)jsonPropertiesObject.GetNamedString(JSON_KEY_VALUE);
                uint8_t a, r, g, b;
                if (!migration && !checkValidARGB(alwaysColor, &a, &r, &g, &b))
                {
                    Logger::error("Always color ARGB value is invalid. Will use default value");
                }
                else
                {
                    highlightSettings.alwaysColor = winrt::Windows::UI::ColorHelper::FromArgb(a, r, g, b);
                }
            }
            catch (...)
            {
                Logger::warn("Failed to initialize always color from settings. Will use default value");
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
                    throw std::runtime_error("Invalid Radius value");
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
                    throw std::runtime_error("Invalid Fade Delay value");
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
                    throw std::runtime_error("Invalid Fade Duration value");
                }
            }
            catch (...)
            {
                Logger::warn("Failed to initialize Fade Duration from settings. Will use default value");
            }
            try
            {
                // Parse auto activate
                auto jsonPropertiesObject = settingsObject.GetNamedObject(JSON_KEY_PROPERTIES).GetNamedObject(JSON_KEY_AUTO_ACTIVATE);
                highlightSettings.autoActivate = jsonPropertiesObject.GetNamedBoolean(JSON_KEY_VALUE);
            }
            catch (...)
            {
                Logger::warn("Failed to initialize auto activate from settings. Will use default value");
            }
            try
            {
                // Parse spotlight mode
                auto jsonPropertiesObject = settingsObject.GetNamedObject(JSON_KEY_PROPERTIES).GetNamedObject(JSON_KEY_SPOTLIGHT_MODE);
                highlightSettings.spotlightMode = jsonPropertiesObject.GetNamedBoolean(JSON_KEY_VALUE);
            }
            catch (...)
            {
                Logger::warn("Failed to initialize spotlight mode settings. Will use default value");
            }
        }
        else
        {
            Logger::info("Mouse Highlighter settings are empty");
        }
        
        // Set default hotkeys if not configured
        if (m_hotkeys.empty() || m_hotkeys.size() < 2)
        {
            Logger::info("Mouse Highlighter is going to use default shortcuts");
            m_hotkeys.clear();
            m_hotkeys.resize(2);
            
            // Default activation shortcut: Win + Shift + H
            m_hotkeys[0].win = true;
            m_hotkeys[0].shift = true;
            m_hotkeys[0].key = 0x48; // H key
            m_hotkeys[0].id = 0;
            
            // Default mode switch shortcut: Win + Shift + M
            m_hotkeys[1].win = true;
            m_hotkeys[1].shift = true;
            m_hotkeys[1].key = 0x4D; // M key
            m_hotkeys[1].id = 1;
        }
        else
        {
            // Check if activation hotkey is empty and set default
            if (!m_hotkeys[0].win && !m_hotkeys[0].ctrl && !m_hotkeys[0].shift && !m_hotkeys[0].alt && m_hotkeys[0].key == 0)
            {
                m_hotkeys[0].win = true;
                m_hotkeys[0].shift = true;
                m_hotkeys[0].key = 0x48; // H key
                m_hotkeys[0].id = 0;
            }
            
            // Check if mode switch hotkey is empty and set default
            if (!m_hotkeys[1].win && !m_hotkeys[1].ctrl && !m_hotkeys[1].shift && !m_hotkeys[1].alt && m_hotkeys[1].key == 0)
            {
                m_hotkeys[1].win = true;
                m_hotkeys[1].shift = true;
                m_hotkeys[1].key = 0x4D; // M key
                m_hotkeys[1].id = 1;
            }
        }
        
        m_highlightSettings = highlightSettings;
    }
};

extern "C" __declspec(dllexport) PowertoyModuleIface* __cdecl powertoy_create()
{
    return new MouseHighlighter();
}
