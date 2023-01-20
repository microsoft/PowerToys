#include "pch.h"

#include "trace.h"
#include <common/logger/logger.h>
#include <common/utils/logger_helper.h>
#include <interface/powertoy_module_interface.h>
#include "Generated Files/resource.h"

#include <shellapi.h>
#include <common/utils/resources.h>
#include <common/utils/winapi_error.h>
#include <common/SettingsAPI/settings_objects.h>
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
    inline const std::wstring ModuleKey = L"Hosts";
}

class HostsModuleInterface : public PowertoyModuleIface
{
private:
    bool m_enabled = false;

    std::wstring app_name;

    //contains the non localized key of the powertoy
    std::wstring app_key;

    HANDLE m_hProcess;

    bool is_process_running()
    {
        return WaitForSingleObject(m_hProcess, 0) == WAIT_TIMEOUT;
    }

    void bring_process_to_front()
    {
        auto enum_windows = [](HWND hwnd, LPARAM param) -> BOOL {
            HANDLE process_handle = reinterpret_cast<HANDLE>(param);
            DWORD window_process_id = 0;

            GetWindowThreadProcessId(hwnd, &window_process_id);
            if (GetProcessId(process_handle) == window_process_id)
            {
                SetForegroundWindow(hwnd);
                return FALSE;
            }
            return TRUE;
        };

        EnumWindows(enum_windows, (LPARAM)m_hProcess);
    }

    void launch_process(bool runas)
    {
        Logger::trace(L"Starting Hosts process");
        unsigned long powertoys_pid = GetCurrentProcessId();

        std::wstring executable_args = L"";
        executable_args.append(std::to_wstring(powertoys_pid));

        SHELLEXECUTEINFOW sei{ sizeof(sei) };
        sei.fMask = { SEE_MASK_NOCLOSEPROCESS | SEE_MASK_FLAG_NO_UI };
        sei.lpFile = L"modules\\Hosts\\PowerToys.Hosts.exe";
        sei.nShow = SW_SHOWNORMAL;
        sei.lpParameters = executable_args.data();

        if (runas)
        {
            sei.lpVerb = L"runas";
        }

        if (ShellExecuteExW(&sei))
        {
            Logger::trace("Successfully started the Hosts process");
        }
        else
        {
            Logger::error(L"Hosts failed to start. {}", get_last_error_or_default(GetLastError()));
        }

        m_hProcess = sei.hProcess;
    }

public:
    HostsModuleInterface()
    {
        app_name = GET_RESOURCE_STRING(IDS_HOSTS_NAME);
        app_key = ModuleKey;
        LoggerHelpers::init_logger(app_key, L"ModuleInterface", LogSettings::hostsLoggerName);
    }

    ~HostsModuleInterface()
    {
        m_enabled = false;
    }

    // Destroy the powertoy and free memory
    virtual void destroy() override
    {
        Logger::trace("HostsModuleInterface::destroy()");
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

    // Return the configured status for the gpo policy for the module
    virtual powertoys_gpo::gpo_rule_configured_t gpo_policy_enabled_configuration() override
    {
        return powertoys_gpo::getConfiguredHostsFileEditorEnabledValue();
    }

    virtual bool get_config(wchar_t* /*buffer*/, int* /*buffer_size*/) override
    {
        return false;
    }

    virtual void call_custom_action(const wchar_t* action) override
    {
        try
        {
            PowerToysSettings::CustomActionObject action_object =
                PowerToysSettings::CustomActionObject::from_json_string(action);

            if (is_process_running())
            {
                bring_process_to_front();
            }
            else if (action_object.get_name() == L"Launch")
            {
                launch_process(false);
            }
            else if (action_object.get_name() == L"LaunchAdministrator")
            {
                launch_process(true);
            }
            Trace::ActivateEditor();
        }
        catch (std::exception&)
        {
            Logger::error(L"Failed to parse action. {}", action);
        }
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
        Logger::trace("HostsModuleInterface::enable()");
        m_enabled = true;
        Trace::EnableHostsFileEditor(true);
    };

    virtual void disable()
    {
        Logger::trace("HostsModuleInterface::disable()");
        if (m_enabled)
        {
            TerminateProcess(m_hProcess, 1);
        }

        m_enabled = false;
        Trace::EnableHostsFileEditor(false);
    }
};

extern "C" __declspec(dllexport) PowertoyModuleIface* __cdecl powertoy_create()
{
    return new HostsModuleInterface();
}