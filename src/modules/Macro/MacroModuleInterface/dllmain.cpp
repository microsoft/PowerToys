// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#include "pch.h"
#include <interface/powertoy_module_interface.h>
#include <common/logger/logger.h>
#include <common/utils/logger_helper.h>
#include <common/utils/resources.h>
#include <common/utils/winapi_error.h>
#include "resource.h"

BOOL APIENTRY DllMain(HMODULE, DWORD ul_reason_for_call, LPVOID)
{
    switch (ul_reason_for_call)
    {
    case DLL_PROCESS_ATTACH:
    case DLL_THREAD_ATTACH:
    case DLL_THREAD_DETACH:
    case DLL_PROCESS_DETACH:
        break;
    }
    return TRUE;
}

class MacroModuleInterface : public PowertoyModuleIface
{
public:
    virtual PCWSTR get_name() override { return app_name.c_str(); }
    virtual const wchar_t* get_key() override { return app_key.c_str(); }

    virtual bool get_config(_Out_ PWSTR buffer, _Out_ int* buffer_size) override
    {
        HINSTANCE hinstance = reinterpret_cast<HINSTANCE>(&__ImageBase);
        PowerToysSettings::Settings settings(hinstance, get_name());
        settings.set_description(GET_RESOURCE_STRING(IDS_MACRO_SETTINGS_DESC));
        settings.set_overview_link(L"https://aka.ms/PowerToysOverview_Macro");
        return settings.serialize_to_buffer(buffer, buffer_size);
    }

    virtual void set_config(PCWSTR config) override
    {
        try
        {
            PowerToysSettings::PowerToyValues values =
                PowerToysSettings::PowerToyValues::from_json_string(config, get_key());
            values.save_to_settings_file();
        }
        catch (std::exception&) {}
    }

    virtual void enable() override
    {
        Logger::info("Macro enabling");
        m_enabled = true;
        unsigned long powertoys_pid = GetCurrentProcessId();
        std::wstring args = std::to_wstring(powertoys_pid);

        SHELLEXECUTEINFOW sei{ sizeof(sei) };
        sei.fMask = SEE_MASK_NOCLOSEPROCESS;
        sei.lpFile = L"PowerToys.MacroEngine.exe";
        sei.nShow = SW_HIDE;
        sei.lpParameters = args.data();
        if (ShellExecuteExW(&sei))
        {
            m_hProcess = sei.hProcess;
            Logger::info("MacroEngine started");
        }
        else
        {
            Logger::error(L"MacroEngine failed to start. {}", get_last_error_or_default(GetLastError()));
        }
    }

    virtual void disable() override
    {
        Logger::info("Macro disabling");
        m_enabled = false;
        if (m_hProcess)
        {
            TerminateProcess(m_hProcess, 0);
            CloseHandle(m_hProcess);
            m_hProcess = nullptr;
        }
    }

    virtual bool is_enabled() override { return m_enabled; }

    virtual void destroy() override
    {
        disable();
        delete this;
    }

    virtual void send_settings_telemetry() override {}

    MacroModuleInterface()
    {
        app_name = L"Macro";
        app_key = L"Macro";
        LoggerHelpers::init_logger(app_key, L"ModuleInterface", "Macro");
    }

private:
    bool is_process_running() const
    {
        return m_hProcess && WaitForSingleObject(m_hProcess, 0) == WAIT_TIMEOUT;
    }

    std::wstring app_name;
    std::wstring app_key;
    bool m_enabled = false;
    HANDLE m_hProcess = nullptr;
};

extern "C" __declspec(dllexport) PowertoyModuleIface* __cdecl powertoy_create()
{
    return new MacroModuleInterface();
}
