#include "pch.h"

#include <interface/powertoy_module_interface.h>
#include <common/SettingsAPI/settings_objects.h>
#include <common/logger/logger.h>
#include <common/logger/logger_settings.h>
#include <common/utils/logger_helper.h>
#include <common/utils/package.h>
#include <common/utils/process_path.h>
#include <optional>

#include "FileLocksmithLib/Constants.h"
#include "FileLocksmithLib/Settings.h"
#include "FileLocksmithLib/Trace.h"

#include "dllmain.h"
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

            toggle_extended_only(values.get_bool_value(L"bool_show_extended_menu").value());
            save_settings();
        }
        catch (std::exception& e)
        {
            Logger::error("Configuration parsing failed: {}", std::string{ e.what() });
        }
    }

    virtual void enable() override
    {
        Logger::info(L"File Locksmith enabled");

        if (package::IsWin11OrGreater())
        {
            std::wstring path = get_module_folderpath(globals::instance);
            std::wstring packageUri = path + L"\\FileLocksmithContextMenuPackage.msix";

            if (!package::IsPackageRegisteredWithPowerToysVersion(constants::nonlocalizable::ContextMenuPackageName))
            {
                package::RegisterSparsePackage(path, packageUri);
            }
        }

        m_enabled = true;
    }

    virtual void disable() override
    {
        Logger::info(L"File Locksmith disabled");
        m_enabled = false;
    }

    virtual bool is_enabled() override
    {
        return m_enabled;
    }

    virtual void toggle_extended_only(bool extended_only)
    {
        Logger::info(L"File Locksmith toggle extended only");
        m_extended_only = extended_only;
        save_settings();
    }

    virtual bool is_extended_only()
    {
        return m_extended_only;
    }

    virtual void destroy() override
    {
        delete this;
    }

private:
    bool m_enabled = false;
    bool m_extended_only;

    void init_settings()
    {
        m_enabled = FileLocksmithSettingsInstance().GetEnabled();
        m_extended_only = FileLocksmithSettingsInstance().GetShowInExtendedContextMenu();
        Trace::EnableFileLocksmith(m_enabled);
    }

    void save_settings()
    {
        auto& settings = FileLocksmithSettingsInstance();
        m_enabled = FileLocksmithSettingsInstance().GetEnabled();
        settings.SetExtendedContextMenuOnly(m_extended_only);

        settings.Save();
        Trace::EnableFileLocksmith(m_enabled);
    }
};

extern "C" __declspec(dllexport) PowertoyModuleIface* __cdecl powertoy_create()
{
    return new FileLocksmithModule();
}
