#include "pch.h"
#include <interface/powertoy_module_interface.h>
#include <common/SettingsAPI/settings_objects.h>
#include "trace.h"
#include "InclusiveCrosshair.h"
#include "common/utils/color.h"

// Non-Localizable strings
namespace
{
    const wchar_t JSON_KEY_PROPERTIES[] = L"properties";
    const wchar_t JSON_KEY_VALUE[] = L"value";
    const wchar_t JSON_KEY_ACTIVATION_SHORTCUT[] = L"activation_shortcut";
    const wchar_t JSON_KEY_CROSSHAIR_COLOR[] = L"crosshair_color";
    const wchar_t JSON_KEY_CROSSHAIR_OPACITY[] = L"crosshair_opacity";
    const wchar_t JSON_KEY_CROSSHAIR_RADIUS[] = L"crosshair_radius";
    const wchar_t JSON_KEY_CROSSHAIR_THICKNESS[] = L"crosshair_thickness";
    const wchar_t JSON_KEY_CROSSHAIR_BORDER_COLOR[] = L"crosshair_border_color";
    const wchar_t JSON_KEY_CROSSHAIR_BORDER_SIZE[] = L"crosshair_border_size";
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
const static wchar_t* MODULE_NAME = L"MousePointerCrosshair";
// Add a description that will we shown in the module settings page.
const static wchar_t* MODULE_DESC = L"<no description>";

// Implement the PowerToy Module Interface and all the required methods.
class MousePointerCrosshair : public PowertoyModuleIface
{
private:
    // The PowerToy state.
    bool m_enabled = false;

    // Hotkey to invoke the module
    HotkeyEx m_hotkey;

    // Mouse Pointer Crosshair specific settings
    InclusiveCrosshairSettings m_inclusiveCrosshairSettings;

public:
    // Constructor
    MousePointerCrosshair()
    {
        LoggerHelpers::init_logger(MODULE_NAME, L"ModuleInterface", LogSettings::mousePointerCrosshairLoggerName);
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

            InclusiveCrosshairApplySettings(m_inclusiveCrosshairSettings);
        }
        catch (std::exception&)
        {
            Logger::error("Invalid json when trying to parse Mouse Pointer Crosshair settings json.");
        }
    }

    // Enable the powertoy
    virtual void enable()
    {
        m_enabled = true;
        Trace::EnableMousePointerCrosshair(true);
        std::thread([=]() { InclusiveCrosshairMain(m_hModule, m_inclusiveCrosshairSettings); }).detach();
    }

    // Disable the powertoy
    virtual void disable()
    {
        m_enabled = false;
        Trace::EnableMousePointerCrosshair(false);
        InclusiveCrosshairDisable();
    }

    // Returns if the powertoys is enabled
    virtual bool is_enabled() override
    {
        return m_enabled;
    }

    // Returns whether the PowerToys should be enabled by default
    virtual bool is_enabled_by_default() const override
    {
        return false;
    }

    virtual std::optional<HotkeyEx> GetHotkeyEx() override
    {
        return m_hotkey;
    }

    virtual void OnHotkeyEx() override
    {
        InclusiveCrosshairSwitch();
    }
    // Load the settings file.
    void init_settings()
    {
        try
        {
            // Load and parse the settings file for this PowerToy.
            PowerToysSettings::PowerToyValues settings =
                PowerToysSettings::PowerToyValues::load_from_settings_file(MousePointerCrosshair::get_key());
            parse_settings(settings);
        }
        catch (std::exception&)
        {
            Logger::error("Invalid json when trying to load the Mouse Pointer Crosshair settings json from file.");
        }
    }

    void parse_settings(PowerToysSettings::PowerToyValues& settings)
    {
        // TODO: refactor to use common/utils/json.h instead
        auto settingsObject = settings.get_raw_json();
        InclusiveCrosshairSettings inclusiveCrosshairSettings;
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
                Logger::warn("Failed to initialize Mouse Pointer Crosshair activation shortcut");
            }
            try
            {
                // Parse Opacity
                auto jsonPropertiesObject = settingsObject.GetNamedObject(JSON_KEY_PROPERTIES).GetNamedObject(JSON_KEY_CROSSHAIR_OPACITY);
                inclusiveCrosshairSettings.crosshairOpacity = (uint8_t)jsonPropertiesObject.GetNamedNumber(JSON_KEY_VALUE);
            }
            catch (...)
            {
                Logger::warn("Failed to initialize Opacity from settings. Will use default value");
            }
            try
            {
                // Parse crosshair color
                auto jsonPropertiesObject = settingsObject.GetNamedObject(JSON_KEY_PROPERTIES).GetNamedObject(JSON_KEY_CROSSHAIR_COLOR);
                auto crosshairColor = (std::wstring)jsonPropertiesObject.GetNamedString(JSON_KEY_VALUE);
                uint8_t r, g, b;
                if (!checkValidRGB(crosshairColor, &r, &g, &b))
                {
                    Logger::error("Crosshair color RGB value is invalid. Will use default value");
                }
                else
                {
                    inclusiveCrosshairSettings.crosshairColor = winrt::Windows::UI::ColorHelper::FromArgb(255, r, g, b);
                }
            }
            catch (...)
            {
                Logger::warn("Failed to initialize crosshair color from settings. Will use default value");
            }
            try
            {
                // Parse Radius
                auto jsonPropertiesObject = settingsObject.GetNamedObject(JSON_KEY_PROPERTIES).GetNamedObject(JSON_KEY_CROSSHAIR_RADIUS);
                inclusiveCrosshairSettings.crosshairRadius = (UINT)jsonPropertiesObject.GetNamedNumber(JSON_KEY_VALUE);
            }
            catch (...)
            {
                Logger::warn("Failed to initialize Radius from settings. Will use default value");
            }
            try
            {
                // Parse Thickness
                auto jsonPropertiesObject = settingsObject.GetNamedObject(JSON_KEY_PROPERTIES).GetNamedObject(JSON_KEY_CROSSHAIR_THICKNESS);
                inclusiveCrosshairSettings.crosshairThickness = (UINT)jsonPropertiesObject.GetNamedNumber(JSON_KEY_VALUE);
            }
            catch (...)
            {
                Logger::warn("Failed to initialize Thickness from settings. Will use default value");
            }
            try
            {
                // Parse crosshair border color
                auto jsonPropertiesObject = settingsObject.GetNamedObject(JSON_KEY_PROPERTIES).GetNamedObject(JSON_KEY_CROSSHAIR_BORDER_COLOR);
                auto crosshairBorderColor = (std::wstring)jsonPropertiesObject.GetNamedString(JSON_KEY_VALUE);
                uint8_t r, g, b;
                if (!checkValidRGB(crosshairBorderColor, &r, &g, &b))
                {
                    Logger::error("Crosshair border color RGB value is invalid. Will use default value");
                }
                else
                {
                    inclusiveCrosshairSettings.crosshairBorderColor = winrt::Windows::UI::ColorHelper::FromArgb(255, r, g, b);
                }
            }
            catch (...)
            {
                Logger::warn("Failed to initialize crosshair border color from settings. Will use default value");
            }
            try
            {
                // Parse border size
                auto jsonPropertiesObject = settingsObject.GetNamedObject(JSON_KEY_PROPERTIES).GetNamedObject(JSON_KEY_CROSSHAIR_BORDER_SIZE);
                inclusiveCrosshairSettings.crosshairBorderSize = (UINT)jsonPropertiesObject.GetNamedNumber(JSON_KEY_VALUE);
            }
            catch (...)
            {
                Logger::warn("Failed to initialize border color from settings. Will use default value");
            }
        }
        else
        {
            Logger::info("Mouse Pointer Crosshair settings are empty");
        }
        if (!m_hotkey.modifiersMask)
        {
            Logger::info("Mouse Pointer Crosshair  is going to use default shortcut");
            m_hotkey.modifiersMask = MOD_CONTROL | MOD_ALT;
            m_hotkey.vkCode = 0x50; // P key
        }
        m_inclusiveCrosshairSettings = inclusiveCrosshairSettings;
    }

};

extern "C" __declspec(dllexport) PowertoyModuleIface* __cdecl powertoy_create()
{
    return new MousePointerCrosshair();
}