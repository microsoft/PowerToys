#include "pch.h"

#include <interface/powertoy_module_interface.h>
#include <common/SettingsAPI/settings_objects.h>
#include <common/logger/logger.h>
#include <common/logger/logger_settings.h>
#include <common/utils/logger_helper.h>
#include <common/utils/package.h>
#include <common/utils/process_path.h>

#include "CopyAsUNCLib/Constants.h"
#include "CopyAsUNCLib/Settings.h"

#include "dllmain.h"
#include "Generated Files/resource.h"

class CopyAsUNCModule : public PowertoyModuleIface
{
public:
    CopyAsUNCModule()
    {
        LoggerHelpers::init_logger(constants::nonlocalizable::PowerToyName, L"ModuleInterface", "CopyAsUNC");
        init_settings();
    }

    virtual const wchar_t* get_name() override
    {
        static WCHAR buffer[128];
        LoadStringW(globals::instance, IDS_COPY_AS_UNC_POWERTOYNAME, buffer, ARRAYSIZE(buffer));
        return buffer;
    }

    virtual const wchar_t* get_key() override
    {
        return constants::nonlocalizable::PowerToyKey;
    }

    virtual powertoys_gpo::gpo_rule_configured_t gpo_policy_enabled_configuration() override
    {
        return powertoys_gpo::getConfiguredCopyAsUNCEnabledValue();
    }

    virtual bool get_config(_Out_ PWSTR buffer, _Out_ int* buffer_size) override
    {
        HINSTANCE hinstance = reinterpret_cast<HINSTANCE>(&__ImageBase);
        PowerToysSettings::Settings settings(hinstance, get_name());
        settings.add_bool_toggle(L"bool_show_extended_menu",
                                 L"",
                                 CopyAsUNCSettingsInstance().GetShowInExtendedContextMenu());
        return settings.serialize_to_buffer(buffer, buffer_size);
    }

    virtual void set_config(PCWSTR config) override
    {
        try
        {
            PowerToysSettings::PowerToyValues values =
                PowerToysSettings::PowerToyValues::from_json_string(config, get_key());

            auto extendedMenu = values.get_bool_value(L"bool_show_extended_menu");
            if (extendedMenu.has_value())
            {
                CopyAsUNCSettingsInstance().SetExtendedContextMenuOnly(extendedMenu.value());
                CopyAsUNCSettingsInstance().Save();
            }
        }
        catch (std::exception& e)
        {
            Logger::error("Configuration parsing failed: {}", std::string{ e.what() });
        }
    }

    virtual void enable() override
    {
        Logger::info(L"Copy as UNC enabled");

        if (package::IsWin11OrGreater())
        {
            std::wstring path = get_module_folderpath(globals::instance);
            std::wstring packageUri = path + L"\\CopyAsUNCContextMenuPackage.msix";
            if (!package::IsPackageRegisteredWithPowerToysVersion(constants::nonlocalizable::ContextMenuPackageName))
            {
                package::RegisterSparsePackage(path, packageUri);
            }
        }

        m_enabled = true;
    }

    virtual void disable() override
    {
        Logger::info(L"Copy as UNC disabled");
        m_enabled = false;
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
    bool m_enabled = false;

    void init_settings()
    {
        m_enabled = CopyAsUNCSettingsInstance().GetEnabled();
    }
};

extern "C" __declspec(dllexport) PowertoyModuleIface* __cdecl powertoy_create()
{
    return new CopyAsUNCModule();
}
