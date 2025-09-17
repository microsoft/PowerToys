#include "pch.h"
#include <interface/powertoy_module_interface.h>
#include <common/logger/logger.h>
#include <common/utils/resources.h>
#include <common/SettingsAPI/settings_objects.h>
#include <common/utils/winapi_error.h>
#include <shellapi.h>

namespace NonLocalizable
{
    const wchar_t ModulePath[] = L"PowerToys.MCPServer.exe";
    const wchar_t ModuleKey[] = L"MCPServer";
}

BOOL APIENTRY DllMain(HMODULE /*hModule*/, DWORD ul_reason_for_call, LPVOID /*lpReserved*/)
{
    switch (ul_reason_for_call)
    {
    case DLL_PROCESS_ATTACH:
        break;
    case DLL_THREAD_ATTACH:
    case DLL_THREAD_DETACH:
    case DLL_PROCESS_DETACH:
        break;
    }
    return TRUE;
}

class MCPServerModuleInterface : public PowertoyModuleIface
{
public:
    virtual PCWSTR get_name() override
    {
        return app_name.c_str();
    }

    virtual const wchar_t* get_key() override
    {
        return app_key.c_str();
    }

    virtual powertoys_gpo::gpo_rule_configured_t gpo_policy_enabled_configuration() override
    {
        return powertoys_gpo::gpo_rule_configured_t::gpo_rule_configured_not_configured;
    }

    virtual bool get_config(wchar_t* buffer, int* buffer_size) override
    {
        HINSTANCE hinstance = reinterpret_cast<HINSTANCE>(&__ImageBase);
        PowerToysSettings::Settings settings(hinstance, get_name());
        
        settings.set_description(L"MCP Server provides Model Context Protocol access to PowerToys functionality for AI assistants and tools");
        settings.set_icon_key(L"pt-mcp-server");

        // Port configuration
        settings.add_int_spinner(
            L"port",
            L"Server Port",
            m_port,
            1024,
            65535,
            1);

        // Auto start option
        settings.add_bool_toggle(
            L"auto_start",
            L"Auto Start Server",
            m_auto_start);

        // Enable tools API
        settings.add_bool_toggle(
            L"enable_tools",
            L"Enable Tools API",
            m_enable_tools);

        // Enable resources API
        settings.add_bool_toggle(
            L"enable_resources",
            L"Enable Resources API", 
            m_enable_resources);

        // Transport protocol
        settings.add_dropdown(
            L"transport",
            L"Transport Protocol",
            m_transport,
            std::vector<std::pair<std::wstring, std::wstring>>{
                { L"http", L"HTTP" },
                { L"stdio", L"Standard I/O" },
                { L"tcp", L"TCP Socket" }
            });

        return settings.serialize_to_buffer(buffer, buffer_size);
    }

    virtual void set_config(const wchar_t* config) override
    {
        try
        {
            PowerToysSettings::PowerToyValues values =
                PowerToysSettings::PowerToyValues::from_json_string(config, get_key());

            if (auto port = values.get_int_value(L"port"))
            {
                m_port = port.value();
            }

            if (auto auto_start = values.get_bool_value(L"auto_start"))
            {
                m_auto_start = auto_start.value();
            }

            if (auto enable_tools = values.get_bool_value(L"enable_tools"))
            {
                m_enable_tools = enable_tools.value();
            }

            if (auto enable_resources = values.get_bool_value(L"enable_resources"))
            {
                m_enable_resources = enable_resources.value();
            }

            if (auto transport = values.get_string_value(L"transport"))
            {
                m_transport = transport.value();
            }

            values.save_to_settings_file();
            
            // If service is running, restart to apply new configuration
            if (m_enabled && is_process_running())
            {
                StopMCPServer();
                StartMCPServer();
            }
        }
        catch (std::exception& e)
        {
            Logger::error("MCPServer configuration parsing failed: {}", std::string{ e.what() });
        }
    }

    virtual void enable() override
    {
        Logger::info("MCPServer enabling");
        m_enabled = true;
        if (m_auto_start)
        {
            StartMCPServer();
        }
    }

    virtual void disable() override
    {
        Logger::info("MCPServer disabling");
        m_enabled = false;
        StopMCPServer();
    }

    virtual bool is_enabled() override
    {
        return m_enabled;
    }

    virtual void destroy() override
    {
        StopMCPServer();
        delete this;
    }

    MCPServerModuleInterface()
    {
        app_name = L"MCP Server";
        app_key = NonLocalizable::ModuleKey;
        m_port = 8080;
        m_auto_start = true;
        m_enable_tools = true;
        m_enable_resources = true;
        m_transport = L"http";
        init_settings();
    }

private:
    void StartMCPServer()
    {
        if (m_hProcess && WaitForSingleObject(m_hProcess, 0) == WAIT_TIMEOUT)
        {
            return; // Already running
        }

        std::wstring executable_args = L"--port=" + std::to_wstring(m_port);
        
        if (!m_enable_tools)
        {
            executable_args += L" --disable-tools";
        }
        
        if (!m_enable_resources)
        {
            executable_args += L" --disable-resources";
        }

        if (!m_transport.empty())
        {
            executable_args += L" --transport=" + m_transport;
        }

        SHELLEXECUTEINFOW sei{ sizeof(sei) };
        sei.fMask = { SEE_MASK_NOCLOSEPROCESS | SEE_MASK_FLAG_NO_UI };
        sei.lpFile = NonLocalizable::ModulePath;
        sei.nShow = SW_HIDE;
        sei.lpParameters = executable_args.data();
        
        if (ShellExecuteExW(&sei))
        {
            m_hProcess = sei.hProcess;
            Logger::info("MCPServer started successfully on port {} with transport {}", m_port, std::string(m_transport.begin(), m_transport.end()));
        }
        else
        {
            Logger::error("Failed to start MCPServer");
            auto message = get_last_error_message(GetLastError());
            if (message.has_value())
            {
                Logger::error(message.value());
            }
        }
    }

    void StopMCPServer()
    {
        if (m_hProcess)
        {
            TerminateProcess(m_hProcess, 0);
            CloseHandle(m_hProcess);
            m_hProcess = nullptr;
            Logger::info("MCPServer stopped");
        }
    }

    bool is_process_running()
    {
        return m_hProcess && WaitForSingleObject(m_hProcess, 0) == WAIT_TIMEOUT;
    }

    void init_settings()
    {
        try
        {
            PowerToysSettings::PowerToyValues settings =
                PowerToysSettings::PowerToyValues::load_from_settings_file(get_key());

            if (auto port = settings.get_int_value(L"port"))
            {
                m_port = port.value();
            }

            if (auto auto_start = settings.get_bool_value(L"auto_start"))
            {
                m_auto_start = auto_start.value();
            }

            if (auto enable_tools = settings.get_bool_value(L"enable_tools"))
            {
                m_enable_tools = enable_tools.value();
            }

            if (auto enable_resources = settings.get_bool_value(L"enable_resources"))
            {
                m_enable_resources = enable_resources.value();
            }

            if (auto transport = settings.get_string_value(L"transport"))
            {
                m_transport = transport.value();
            }
        }
        catch (std::exception&)
        {
            Logger::warn(L"MCPServer settings file not found, using defaults");
        }
    }

    std::wstring app_name;
    std::wstring app_key;
    bool m_enabled = false;
    HANDLE m_hProcess = nullptr;
    int m_port = 8080;
    bool m_auto_start = true;
    bool m_enable_tools = true;
    bool m_enable_resources = true;
    std::wstring m_transport = L"http";
};

extern "C" __declspec(dllexport) PowertoyModuleIface* __cdecl powertoy_create()
{
    return new MCPServerModuleInterface();
}