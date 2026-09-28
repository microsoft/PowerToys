#include "pch.h"

#include "trace.h"
#include <common/logger/logger.h>
#include <common/utils/logger_helper.h>
#include <common/utils/gpo.h>
#include <interface/powertoy_module_interface.h>
#include "Generated Files/resource.h"

#include <common/utils/resources.h>
#include <string>

extern "C" IMAGE_DOS_HEADER __ImageBase;

BOOL APIENTRY DllMain(HMODULE /*hModule*/, DWORD ul_reason_for_call, LPVOID /*lpReserved*/)
{
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

namespace
{
    // Name of the powertoy module.
    inline const std::wstring ModuleKey = L"ContextMenuManager";
}

// Context Menu Manager does all of its work (registry enumeration and toggling) inside the Settings
// UI process - see src/settings-ui/Settings.UI/ViewModels/ContextMenuManagerViewModel.cs. This
// native module exists only to satisfy the runner's EnabledModules/GPO/tray architecture, which
// requires every module to have a PowertoyModuleIface. There is no hotkey, no background lifecycle,
// and nothing to launch.
class ContextMenuManagerModuleInterface : public PowertoyModuleIface
{
private:
    bool m_enabled = false;

    std::wstring app_name;

    // contains the non localized key of the powertoy
    std::wstring app_key;

public:
    ContextMenuManagerModuleInterface()
    {
        app_name = GET_RESOURCE_STRING(IDS_CONTEXTMENUMANAGER_NAME);
        app_key = ModuleKey;
        LoggerHelpers::init_logger(app_key, L"ModuleInterface", LogSettings::contextMenuManagerLoggerName);
    }

    // Destroy the powertoy and free memory
    virtual void destroy() override
    {
        Logger::trace("ContextMenuManagerModuleInterface::destroy()");
        delete this;
    }

    // Return the localized display name of the powertoy
    virtual const wchar_t* get_name() override
    {
        return app_name.c_str();
    }

    // Return the non localized key of the powertoy, this will be cached by the runner
    virtual const wchar_t* get_key() override
    {
        return app_key.c_str();
    }

    // Return the configured status for the gpo policy for the module.
    // No dedicated GPO policy for v1 - module-enable via EnabledModules only.
    virtual powertoys_gpo::gpo_rule_configured_t gpo_policy_enabled_configuration() override
    {
        return powertoys_gpo::gpo_rule_configured_not_configured;
    }

    // Returns whether the PowerToys should be enabled by default
    virtual bool is_enabled_by_default() const override
    {
        return false;
    }

    virtual bool get_config(wchar_t* /*buffer*/, int* /*buffer_size*/) override
    {
        return false;
    }

    virtual void call_custom_action(const wchar_t* /*action*/) override
    {
    }

    virtual void set_config(const wchar_t* /*config*/) override
    {
    }

    virtual bool is_enabled() override
    {
        return m_enabled;
    }

    virtual void enable()
    {
        Logger::trace("ContextMenuManagerModuleInterface::enable()");
        m_enabled = true;
        Trace::EnableContextMenuManager(true);
    };

    virtual void disable()
    {
        Logger::trace("ContextMenuManagerModuleInterface::disable()");
        m_enabled = false;
        Trace::EnableContextMenuManager(false);
    }
};

extern "C" __declspec(dllexport) PowertoyModuleIface* __cdecl powertoy_create()
{
    return new ContextMenuManagerModuleInterface();
}
