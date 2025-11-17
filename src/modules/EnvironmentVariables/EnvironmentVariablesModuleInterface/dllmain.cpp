// dllmain.cpp : Defines the entry point for the DLL application.
#include "pch.h"

#include "Generated Files/resource.h"
#include "trace.h"

#include <common/interop/shared_constants.h>
#include <common/logger/logger.h>
#include <common/utils/EventWaiter.h>
#include <common/utils/gpo.h>
#include <common/utils/logger_helper.h>
#include <common/utils/resources.h>
#include <common/utils/winapi_error.h>
#include <interface/powertoy_module_interface.h>

#include <shellapi.h>

extern "C" IMAGE_DOS_HEADER __ImageBase;

BOOL APIENTRY DllMain( HMODULE hModule,
                       DWORD  ul_reason_for_call,
                       LPVOID lpReserved
                     )
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
    inline const std::wstring ModuleKey = L"EnvironmentVariables";
}

class EnvironmentVariablesModuleInterface : public PowertoyModuleIface
{
private:
    bool m_enabled = false;

    std::wstring app_name;

    //contains the non localized key of the powertoy
    std::wstring app_key;

    HANDLE m_hProcess = nullptr;

    HANDLE m_hShowEvent;

    EventWaiter m_showEventWaiter;

    HANDLE m_hShowAdminEvent;

    EventWaiter m_showAdminEventWaiter;

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
        Logger::trace("EnvironmentVariablesModuleInterface::launch_process()");
        unsigned long powertoys_pid = GetCurrentProcessId();

        std::wstring executable_args = L"";
        executable_args.append(std::to_wstring(powertoys_pid));

        SHELLEXECUTEINFOW sei{ sizeof(sei) };
        sei.fMask = { SEE_MASK_NOCLOSEPROCESS | SEE_MASK_FLAG_NO_UI };
        sei.lpFile = L"WinUI3Apps\\PowerToys.EnvironmentVariables.exe";
        sei.nShow = SW_SHOWNORMAL;
        sei.lpParameters = executable_args.data();

        if (runas)
        {
            sei.lpVerb = L"runas";
        }

        if (ShellExecuteExW(&sei))
        {
            Logger::trace("Successfully started the Environment Variables process");
        }
        else
        {
            Logger::error(L"Environment Variables failed to start. {}", get_last_error_or_default(GetLastError()));
        }

        m_hProcess = sei.hProcess;
    }

public:
    EnvironmentVariablesModuleInterface()
    {
        app_name = GET_RESOURCE_STRING(IDS_ENVIRONMENT_VARIABLES_NAME);
        app_key = ModuleKey;
        LoggerHelpers::init_logger(app_key, L"ModuleInterface", LogSettings::environmentVariablesLoggerName);

        m_hShowEvent = CreateDefaultEvent(CommonSharedConstants::SHOW_ENVIRONMENT_VARIABLES_EVENT);
        if (!m_hShowEvent)
        {
            Logger::error(L"Failed to create show Environment Variables event");
            auto message = get_last_error_message(GetLastError());
            if (message.has_value())
            {
                Logger::error(message.value());
            }
        }

        m_hShowAdminEvent = CreateDefaultEvent(CommonSharedConstants::SHOW_ENVIRONMENT_VARIABLES_ADMIN_EVENT);
        if (!m_hShowAdminEvent)
        {
            Logger::error(L"Failed to create show Environment Variables admin event");
            auto message = get_last_error_message(GetLastError());
            if (message.has_value())
            {
                Logger::error(message.value());
            }
        }

        m_showEventWaiter = EventWaiter(CommonSharedConstants::SHOW_ENVIRONMENT_VARIABLES_EVENT, [&](int err) {
            if (m_enabled && err == ERROR_SUCCESS)
            {
                Logger::trace(L"{} event was signaled", CommonSharedConstants::SHOW_ENVIRONMENT_VARIABLES_EVENT);

                if (is_process_running())
                {
                    bring_process_to_front();
                }
                else
                {
                    launch_process(false);
                }

                Trace::ActivateEnvironmentVariables();
            }
        });

        m_showAdminEventWaiter = EventWaiter(CommonSharedConstants::SHOW_ENVIRONMENT_VARIABLES_ADMIN_EVENT, [&](int err) {
            if (m_enabled && err == ERROR_SUCCESS)
            {
                Logger::trace(L"{} event was signaled", CommonSharedConstants::SHOW_ENVIRONMENT_VARIABLES_ADMIN_EVENT);

                if (is_process_running())
                {
                    bring_process_to_front();
                }
                else
                {
                    launch_process(true);
                }

                Trace::ActivateEnvironmentVariables();
            }
        });
    }

    ~EnvironmentVariablesModuleInterface()
    {
        m_enabled = false;
    }

    // Destroy the powertoy and free memory
    virtual void destroy() override
    {
        Logger::trace("EnvironmentVariablesModuleInterface::destroy()");

        if (m_hShowEvent)
        {
            CloseHandle(m_hShowEvent);
            m_hShowEvent = nullptr;
        }

        if (m_hShowAdminEvent)
        {
            CloseHandle(m_hShowAdminEvent);
            m_hShowAdminEvent = nullptr;
        }

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
        return powertoys_gpo::getConfiguredEnvironmentVariablesEnabledValue();
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
        Logger::trace("EnvironmentVariablesModuleInterface::enable()");
        m_enabled = true;
        Trace::EnableEnvironmentVariables(true);
    }

    virtual void disable()
    {
        Logger::trace("EnvironmentVariablesModuleInterface::disable()");
        if (m_enabled)
        {
            if (m_hShowEvent)
            {
                ResetEvent(m_hShowEvent);
            }

            if (m_hShowAdminEvent)
            {
                ResetEvent(m_hShowAdminEvent);
            }

            TerminateProcess(m_hProcess, 1);
        }

        m_enabled = false;
        Trace::EnableEnvironmentVariables(false);
    }
};

extern "C" __declspec(dllexport) PowertoyModuleIface * __cdecl powertoy_create()
{
    return new EnvironmentVariablesModuleInterface();
}