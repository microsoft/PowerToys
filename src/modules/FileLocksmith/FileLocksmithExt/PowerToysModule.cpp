#include "pch.h"

#include <interface/powertoy_module_interface.h>
#include <common/SettingsAPI/settings_objects.h>
#include <common/logger/logger.h>
#include <common/logger/logger_settings.h>
#include <common/utils/logger_helper.h>
#include <optional>

#include "Constants.h"
#include "dllmain.h"
#include "Settings.h"
#include "Trace.h"
#include "Generated Files/resource.h"

class FileLocksmithModule : public PowertoyModuleIface
{
public:
    FileLocksmithModule()
    {
        LoggerHelpers::init_logger(constants::nonlocalizable::PowerToyName, L"ModuleInterface", LogSettings::fileLocksmithLoggerName);
        init_settings();
    }

    virtual const wchar_t* get_name() override
    {
        static WCHAR buffer[128];
        LoadStringW(globals::instance, IDS_FILELOCKSMITH_POWERTOYNAME, buffer, ARRAYSIZE(buffer));
        return buffer;
    }

    virtual const wchar_t* get_key() override
    {
        return constants::nonlocalizable::PowerToyKey;
    }

    // Return the configured status for the gpo policy for the module
    virtual powertoys_gpo::gpo_rule_configured_t gpo_policy_enabled_configuration() override
    {
        return powertoys_gpo::getConfiguredFileLocksmithEnabledValue();
    }

    // Return JSON with the configuration options.
    // These are the settings shown on the settings page along with their current values.
    virtual bool get_config(_Out_ PWSTR buffer, _Out_ int* buffer_size) override
    {
        HINSTANCE hinstance = reinterpret_cast<HINSTANCE>(&__ImageBase);

        // Create a Settings object.
        PowerToysSettings::Settings settings(hinstance, get_name());

        return settings.serialize_to_buffer(buffer, buffer_size);
    }

    // Passes JSON with the configuration settings for the powertoy.
    // This is called when the user hits Save on the settings page.
    virtual void set_config(PCWSTR config) override
    {
        try
        {
            // Parse the input JSON string.
            PowerToysSettings::PowerToyValues values =
                PowerToysSettings::PowerToyValues::from_json_string(config, get_key());

            // Currently, there are no settings, so we don't do anything.
        }
        catch (std::exception& e)
        {
            Logger::error("Configuration parsing failed: {}", std::string{ e.what() });
        }
    }

    virtual void enable() override
    {
        Logger::info(L"File Locksmith enabled");
        m_enabled = true;
        save_settings();
    }

    virtual void disable() override
    {
        Logger::info(L"File Locksmith disabled");
        m_enabled = false;
        save_settings();
    }

    virtual bool is_enabled() override
    {
        return m_enabled;
    }

    virtual void destroy() override
    {
        delete this;
    }

private:
    bool m_enabled;

    void init_settings()
    {
        m_enabled = FileLocksmithSettingsInstance().GetEnabled();
        Trace::EnableFileLocksmith(m_enabled);
    }

    void save_settings()
    {
        auto& settings = FileLocksmithSettingsInstance();
        settings.SetEnabled(m_enabled);
        settings.Save();
        Trace::EnableFileLocksmith(m_enabled);
    }
};

extern "C" __declspec(dllexport) PowertoyModuleIface* __cdecl powertoy_create()
{
    return new FileLocksmithModule();
}
