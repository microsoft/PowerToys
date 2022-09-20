#include "pch.h"

#include <interface/powertoy_module_interface.h>
#include <common/SettingsAPI/settings_objects.h>
#include <common/logger/logger.h>
#include <common/logger/logger_settings.h>
#include <common/utils/logger_helper.h>

#include "Constants.h"
#include "dllmain.h"
#include "Settings.h"
#include "Trace.h"

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
        return constants::localizable::PowerToyName;
    }

    virtual const wchar_t* get_key() override
    {
        return constants::nonlocalizable::PowerToyKey;
    }

    // Return JSON with the configuration options.
    // These are the settings shown on the settings page along with their current values.
    virtual bool get_config(_Out_ PWSTR buffer, _Out_ int* buffer_size) override
    {
        HINSTANCE hinstance = reinterpret_cast<HINSTANCE>(&__ImageBase);

        // Create a Settings object.
        PowerToysSettings::Settings settings(hinstance, get_name());
        settings.set_description(L"TODO: GET_RESOURCE_STRING(IDS_SETTINGS_DESCRIPTION");
        settings.set_icon_key(L"TODO: pt-file-locksmith");

        // Link to the GitHub FileLocksmith sub-page
        settings.set_overview_link(L"TODO: GET_RESOURCE_STRING(IDS_OVERVIEW_LINK)");

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

            // TODO: Trace
            // Trace::SettingsChanged();
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
        Trace::EnableFileLocksmith(m_enabled);
    }

    virtual void disable() override
    {
        Logger::info(L"File Locksmith disabled");
        m_enabled = false;
        save_settings();
        Trace::EnableFileLocksmith(m_enabled);
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
        // TODO trace
        // Trace::EnablePowerRename(m_enabled);
    }

    void save_settings()
    {
        auto& settings = FileLocksmithSettingsInstance();
        settings.SetEnabled(m_enabled);
        settings.Save();
        // TODO trace
        // Trace::EnablePowerRename(m_enabled);
    }
};

extern "C" __declspec(dllexport) PowertoyModuleIface* __cdecl powertoy_create()
{
    return new FileLocksmithModule();
}
