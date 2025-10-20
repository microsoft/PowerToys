#include "pch.h"
#include <interface/powertoy_module_interface.h>
#include <common/SettingsAPI/settings_objects.h>
#include <common/interop/shared_constants.h>
#include "trace.h"
#include "resource.h"
#include "McpConstants.h"
#include <common/logger/logger.h>
#include <common/SettingsAPI/settings_helpers.h>

#include <common/utils/process_path.h>
#include <common/utils/resources.h>
#include <common/utils/os-detect.h>
#include <common/utils/winapi_error.h>

#include <filesystem>

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

const static wchar_t* MODULE_NAME = L"Model Context Protocol";
const static wchar_t* MODULE_DESC = L"Exposes PowerToys functionality via MCP protocol for AI assistants.";

class McpServer : public PowertoyModuleIface
{
    std::wstring app_name;
    std::wstring app_key;

private:
    bool m_enabled = false;
    PROCESS_INFORMATION p_info = {};

    bool is_process_running()
    {
        return WaitForSingleObject(p_info.hProcess, 0) == WAIT_TIMEOUT;
    }

    void launch_process()
    {
        Logger::trace(L"Launching PowerToys MCP Server process");
        unsigned long powertoys_pid = GetCurrentProcessId();

        std::wstring executable_args = L"--pid " + std::to_wstring(powertoys_pid);
        std::wstring application_path = L"PowerToys.McpServer.exe";
        std::wstring full_command_path = application_path + L" " + executable_args.data();
        Logger::trace(L"PowerToys MCP Server launching with parameters: " + executable_args);

        STARTUPINFO info = { sizeof(info) };

        if (!CreateProcess(application_path.c_str(), full_command_path.data(), NULL, NULL, true, NULL, NULL, NULL, &info, &p_info))
        {
            DWORD error = GetLastError();
            std::wstring message = L"PowerToys MCP Server failed to start with error: ";
            message += std::to_wstring(error);
            Logger::error(message);
        }
    }

public:
    McpServer()
    {
        app_name = GET_RESOURCE_STRING(IDS_MCP_NAME);
        app_key = McpConstants::ModuleKey;
        std::filesystem::path logFilePath(PTSettingsHelper::get_module_save_folder_location(this->app_key));
        logFilePath.append(L"Logs");
        Logger::init("McpModuleInterface", logFilePath.wstring(), PTSettingsHelper::get_log_settings_file_location());
        Logger::info("McpServer module interface is constructing");
    };

    // Return the configured status for the gpo policy for the module
    virtual powertoys_gpo::gpo_rule_configured_t gpo_policy_enabled_configuration() override
    {
        return powertoys_gpo::gpo_rule_configured_not_configured;
    }

    virtual void destroy() override
    {
        delete this;
    }

    virtual const wchar_t* get_name() override
    {
        return MODULE_NAME;
    }

    virtual bool get_config(wchar_t* buffer, int* buffer_size) override
    {
        HINSTANCE hinstance = reinterpret_cast<HINSTANCE>(&__ImageBase);

        PowerToysSettings::Settings settings(hinstance, get_name());
        settings.set_description(MODULE_DESC);

        return settings.serialize_to_buffer(buffer, buffer_size);
    }

    virtual const wchar_t* get_key() override
    {
        return app_key.c_str();
    }

    virtual void set_config(const wchar_t* config) override
    {
        try
        {
            // Parse the input JSON string.
            PowerToysSettings::PowerToyValues values =
                PowerToysSettings::PowerToyValues::from_json_string(config, get_key());

            // Persist the values.
            values.save_to_settings_file();
        }
        catch (std::exception&)
        {
            // Improper JSON.
        }
    }

    virtual void enable()
    {
        Trace::EnableMCP(true);
        launch_process();
        m_enabled = true;
    };

    virtual void disable()
    {
        if (m_enabled)
        {
            Trace::EnableMCP(false);
            Logger::trace(L"Disabling MCP Server...");

            // Terminate the MCP server process
            if (p_info.hProcess)
            {
                TerminateProcess(p_info.hProcess, 0);
                CloseHandle(p_info.hProcess);
                CloseHandle(p_info.hThread);
            }
        }

        m_enabled = false;
    }

    virtual bool is_enabled() override
    {
        return m_enabled;
    }
};

extern "C" __declspec(dllexport) PowertoyModuleIface* __cdecl powertoy_create()
{
    return new McpServer();
}
