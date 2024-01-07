// dllmain.cpp : Defines the entry point for the DLL application.
#include "pch.h"

#include <filesystem>
#include <string>

#include <common/logger/logger.h>
#include <common/logger/logger_settings.h>
#include <common/SettingsAPI/settings_objects.h>
#include <common/utils/gpo.h>
#include <common/utils/logger_helper.h>
#include <common/utils/process_path.h>
#include <common/utils/resources.h>
#include <interface/powertoy_module_interface.h>

#include "resource.h"
#include "trace.h"

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

const static wchar_t* MODULE_NAME = L"Command Not Found";
const static wchar_t* MODULE_DESC = L"A module that detects an error thrown by a command in PowerShell and suggests a relevant WinGet package to install, if available.";

inline const std::wstring ModuleKey = L"CmdNotFound";

class CmdNotFound : public PowertoyModuleIface
{
    std::wstring app_name;
    std::wstring app_key;

private:
    bool m_enabled = false;

    void install_module()
    {
        auto module_path = get_module_folderpath();

        std::string command = "pwsh.exe";
        command += " ";
        command += "-NoProfile -NonInteractive -NoLogo -WindowStyle Hidden -ExecutionPolicy Unrestricted -File \"" + winrt::to_string(module_path) + "\\WinUI3Apps\\Assets\\Settings\\Scripts\\EnableModule.ps1" + "\"" + " -scriptPath \"" + winrt::to_string(module_path) + "\"";

        int ret = system(command.c_str());

        if (ret != 0)
        {
            Logger::error("Running EnableModule.ps1 script failed.");
        }
        else
        {
            Logger::info("Module installed successfully.");
            Trace::EnableCmdNotFoundGpo(true);
        }
    }

    void uninstall_module()
    {
        auto module_path = get_module_folderpath();

        std::string command = "pwsh.exe";
        command += " ";
        command += "-NoProfile -NonInteractive -NoLogo -WindowStyle Hidden -ExecutionPolicy Unrestricted -File \"" + winrt::to_string(module_path) + "\\WinUI3Apps\\Assets\\Settings\\Scripts\\DisableModule.ps1" + "\"";

        int ret = system(command.c_str());

        if (ret != 0)
        {
            Logger::error("Running EnableModule.ps1 script failed.");
        }
        else
        {
            Logger::info("Module uninstalled successfully.");
            Trace::EnableCmdNotFoundGpo(false);
        }
    }

public:
    CmdNotFound()
    {
        app_name = GET_RESOURCE_STRING(IDS_CMD_NOT_FOUND_NAME);
        app_key = ModuleKey;
        LoggerHelpers::init_logger(app_key, L"ModuleInterface", LogSettings::cmdNotFoundLoggerName);
        Logger::info("CmdNotFound object is constructing");

        powertoys_gpo::gpo_rule_configured_t gpo_rule_configured_value = gpo_policy_enabled_configuration();
        if (gpo_rule_configured_value == powertoys_gpo::gpo_rule_configured_t::gpo_rule_configured_enabled)
        {
            install_module();
            m_enabled = true;
        }
        else if (gpo_rule_configured_value == powertoys_gpo::gpo_rule_configured_t::gpo_rule_configured_disabled)
        {
            uninstall_module();
            m_enabled = false;
        }
    }

    virtual powertoys_gpo::gpo_rule_configured_t gpo_policy_enabled_configuration() override
    {
        return powertoys_gpo::getConfiguredCmdNotFoundEnabledValue();
    }

    virtual void destroy() override
    {
        delete this;
    }

    virtual const wchar_t* get_name() override
    {
        return MODULE_NAME;
    }

    virtual const wchar_t* get_key() override
    {
        return app_key.c_str();
    }

    virtual bool get_config(wchar_t* /*buffer*/, int* /*buffer_size*/) override
    {
        return false;
    }

    virtual void set_config(const wchar_t* config) override
    {
    }

    virtual void enable()
    {
    }

    virtual void disable()
    {
    }

    virtual bool is_enabled() override
    {
        return m_enabled;
    }
};

extern "C" __declspec(dllexport) PowertoyModuleIface* __cdecl powertoy_create()
{
    return new CmdNotFound();
}
