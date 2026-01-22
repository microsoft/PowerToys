#include "pch.h"
#include <interface/powertoy_module_interface.h>
#include <common/SettingsAPI/settings_objects.h>
#include "trace.h"
#include "MouseHighlighter.h"
#include "common/utils/color.h"
#include <common/utils/EventWaiter.h>
#include <common/interop/shared_constants.h>

namespace
{
    const wchar_t JSON_KEY_PROPERTIES[] = L"properties";
    const wchar_t JSON_KEY_VALUE[] = L"value";
    const wchar_t JSON_KEY_ACTIVATION_SHORTCUT[] = L"activation_shortcut";
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

class MouseHighlighter
{
private:
    // The PowerToy state.
    bool m_enabled = false;

    // Event-driven trigger support
    EventWaiter m_triggerEventWaiter;

public:
    // Mouse Highlighter specific settings
    MouseHighlighterSettings m_highlightSettings;

    // Constructor
    MouseHighlighter()
    {
        LoggerHelpers::init_logger(L"MouseHighlighter", L"ModuleInterface", LogSettings::mouseHighlighterLoggerName);
        init_settings();
    };

    // Enable the powertoy
    virtual void enable()
    {
        m_enabled = true;
        Trace::EnableMouseHighlighter(true);
        std::thread([=]() { MouseHighlighterMain(GetModuleHandle(nullptr), m_highlightSettings); }).detach();
    }

    // Disable the powertoy
    virtual void disable()
    {
        m_enabled = false;
        Trace::EnableMouseHighlighter(false);
        MouseHighlighterDisable();

        m_triggerEventWaiter.stop();
    }

    // Load the settings file.
    void init_settings()
    {
        try
        {
            // Load and parse the settings file for this PowerToy.
            PowerToysSettings::PowerToyValues settings =
            PowerToysSettings::PowerToyValues::load_from_settings_file(L"MouseHighlighter");
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
    m_highlightSettings = highlightSettings;
}
};

MouseHighlighter* g_mouseHighlighter = nullptr;

EXTERN_C __declspec(dllexport) void MouseHighlighterSettingsChanged()
{
    if (!g_mouseHighlighter)
    {
        g_mouseHighlighter = new MouseHighlighter();
    }

    g_mouseHighlighter->init_settings();

    MouseHighlighterApplySettings(g_mouseHighlighter->m_highlightSettings);
}

EXTERN_C __declspec(dllexport) void ToggleMouseHighlighter()
{
    if (!g_mouseHighlighter)
    {
        g_mouseHighlighter = new MouseHighlighter();
    }
    MouseHighlighterSwitch();
}

EXTERN_C __declspec(dllexport) void EnableMouseHighlighter()
{
    if (!g_mouseHighlighter)
    {
        g_mouseHighlighter = new MouseHighlighter();
    }
    g_mouseHighlighter->enable();
}

EXTERN_C __declspec(dllexport) void DisableMouseHighlighter()
{
    if (!g_mouseHighlighter)
    {
        g_mouseHighlighter = new MouseHighlighter();
    }
    g_mouseHighlighter->disable();
}

