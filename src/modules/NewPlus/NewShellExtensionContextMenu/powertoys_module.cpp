#include "pch.h"

#include <filesystem>
#include <string>

#include <winrt/Windows.Data.Json.h>

#include <common/SettingsAPI/settings_objects.h>
#include <common/utils/gpo.h>
#include <common/utils/process_path.h>
#include <common/utils/resources.h>
#include <interface/powertoy_module_interface.h>

#include "constants.h"
#include "settings.h"
#include "trace.h"
#include "new_utilities.h"
#include "Generated Files/resource.h"
#include "RuntimeRegistration.h"

// Note: Settings are managed via Settings and UI Settings
class NewModule : public PowertoyModuleIface
{
public:
    NewModule()
    {
        init_settings();
    }

    virtual const wchar_t* get_name() override
    {
        static const std::wstring localized_context_menu_item =
            GET_RESOURCE_STRING_FALLBACK(IDS_CONTEXT_MENU_ITEM_NEW, L"New+");

        return localized_context_menu_item.c_str();
    }

    virtual const wchar_t* get_key() override
    {
        // This setting key must match EnabledModules.cs [JsonPropertyName("NewPlus")]
        return newplus::constants::non_localizable::powertoy_key;
    }

    virtual powertoys_gpo::gpo_rule_configured_t gpo_policy_enabled_configuration() override
    {
        return powertoys_gpo::getConfiguredNewPlusEnabledValue();
    }

    virtual bool get_config(_Out_ PWSTR buffer, _Out_ int* buffer_size) override
    {
        // Not implemented as Settings are propagating via json
        return true;
    }

    virtual void set_config(const wchar_t* config) override
    {
        // The following just checks to see if the Template Location was changed for metrics purposes
        // Note: We are not saving the settings here and instead relying on read/write of json in Settings App .cs code paths
        try
        {
            // Parse the input JSON string.
            PowerToysSettings::PowerToyValues values =
                PowerToysSettings::PowerToyValues::from_json_string(config, get_key());

            values.save_to_settings_file();
            NewSettingsInstance().Load();

            auto templateValue = values.get_string_value(newplus::constants::non_localizable::settings_json_key_template_location);
            if (templateValue.has_value())
            {
                const auto latest_location_value = templateValue.value();
                const auto existing_location_value = NewSettingsInstance().GetTemplateLocation();
                if (!newplus::utilities::wstring_same_when_comparing_ignore_case(latest_location_value, existing_location_value))
                {
                    Trace::EventChangedTemplateLocation();
                }
            }

        }
        catch (std::exception& e)
        {
            Logger::error("Configuration parsing failed: {}", std::string{ e.what() });
        }
    }

    virtual bool is_enabled_by_default() const override
    { 
        return false; 
    }

    virtual void enable() override
    {
        Logger::info("New+ enabled via Settings UI");

        // Log telemetry
        Trace::EventToggleOnOff(true);
        if (package::IsWin11OrGreater())
        {
            newplus::utilities::register_msix_package();
        }
        else
        {
#if defined(ENABLE_REGISTRATION) || defined(NDEBUG)
            NewPlusRuntimeRegistration::EnsureRegisteredWin10();
#endif
        }

        powertoy_new_enabled = true;
    }

    virtual void disable() override
    {
        Logger::info("New+ disabled via Settings UI");
        Disable(true);
    }

    virtual bool is_enabled() override
    {
        return powertoy_new_enabled;
    }

    virtual void hide_file_extension(bool hide_file_extension)
    {
        Logger::info("New+ hide file extension {}", hide_file_extension);
    }

    virtual void hide_starting_digits(bool hide_starting_digits)
    {
        Logger::info("New+ hide starting digits {}", hide_starting_digits);
    }

    virtual void template_location(std::wstring path_location)
    {
        Logger::info("New+ template location");
    }

    virtual void destroy() override
    {
        Disable(false);
        delete this;
    }

private:
    bool powertoy_new_enabled = false;

    void Disable(bool const traceEvent)
    {
        // Log telemetry
        if (traceEvent)
        {
            Trace::EventToggleOnOff(false);
        }
        if (!package::IsWin11OrGreater())
        {
#if defined(ENABLE_REGISTRATION) || defined(NDEBUG)
            NewPlusRuntimeRegistration::Unregister();
            Logger::info(L"New+ context menu unregistered (Win10)");
#endif
        }
        powertoy_new_enabled = false;
    }

    void init_settings()
    {
        powertoy_new_enabled = NewSettingsInstance().GetEnabled();
    }
};

extern "C" __declspec(dllexport) PowertoyModuleIface* __cdecl powertoy_create()
{
    return new NewModule();
}
