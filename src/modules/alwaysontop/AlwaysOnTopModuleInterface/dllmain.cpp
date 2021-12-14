#include "pch.h"

#include <interface/powertoy_module_interface.h>

#include <common/logger/logger.h>
#include <common/utils/resources.h>
#include <common/utils/winapi_error.h>

#include <AlwaysOnTop/trace.h>
#include <AlwaysOnTop/ModuleConstants.h>

#include <shellapi.h>

namespace NonLocalizable
{
    const wchar_t ModulePath[] = L"modules\\AlwaysOnTop\\PowerToys.AlwaysOnTop.exe";
}

BOOL APIENTRY DllMain(HMODULE hModule, DWORD ul_reason_for_call, LPVOID lpReserved)
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

class AlwaysOnTopModuleInterface : public PowertoyModuleIface
{
public:
    // Return the localized display name of the powertoy
    virtual PCWSTR get_name() override
    {
        return app_name.c_str();
    }

    // Return the non localized key of the powertoy, this will be cached by the runner
    virtual const wchar_t* get_key() override
    {
        return app_key.c_str();
    }

    // Return JSON with the configuration options.
    // These are the settings shown on the settings page along with their current values.
    virtual bool get_config(_Out_ PWSTR buffer, _Out_ int* buffer_size) override
    {
        return false;
    }

    // Passes JSON with the configuration settings for the powertoy.
    // This is called when the user hits Save on the settings page.
    virtual void set_config(PCWSTR config) override
    {
    }

    // Enable the powertoy
    virtual void enable()
    {
        Logger::info("AlwaysOnTop enabling");

        Enable();
    }

    // Disable the powertoy
    virtual void disable()
    {
        Logger::info("AlwaysOnTop disabling");

        Disable(true);
    }

    // Returns if the powertoy is enabled
    virtual bool is_enabled() override
    {
        return m_enabled;
    }

    // Destroy the powertoy and free memory
    virtual void destroy() override
    {
        Disable(false);
        delete this;
    }

    AlwaysOnTopModuleInterface()
    {
        app_name = L"AlwaysOnTop"; //TODO: localize
        app_key = NonLocalizable::ModuleKey;
    }

private:
    void Enable()
    {
        m_enabled = true;

        // Log telemetry
        Trace::AlwaysOnTop::Enable(true);

        unsigned long powertoys_pid = GetCurrentProcessId();
        std::wstring executable_args = L"";
        executable_args.append(std::to_wstring(powertoys_pid));

        SHELLEXECUTEINFOW sei{ sizeof(sei) };
        sei.fMask = { SEE_MASK_NOCLOSEPROCESS | SEE_MASK_FLAG_NO_UI };
        sei.lpFile = NonLocalizable::ModulePath;
        sei.nShow = SW_SHOWNORMAL;
        sei.lpParameters = executable_args.data();
        if (ShellExecuteExW(&sei) == false)
        {
            Logger::error(L"Failed to start AlwaysOnTop");
            auto message = get_last_error_message(GetLastError());
            if (message.has_value())
            {
                Logger::error(message.value());
            }
        }
        else
        {
            m_hProcess = sei.hProcess;
        }
    }

    void Disable(bool const traceEvent)
    {
        m_enabled = false;
        // Log telemetry
        if (traceEvent)
        {
            Trace::AlwaysOnTop::Enable(false);
        }

        if (m_hProcess)
        {
            TerminateProcess(m_hProcess, 0);
            m_hProcess = nullptr;
        }
    }

    std::wstring app_name;
    std::wstring app_key; //contains the non localized key of the powertoy

    bool m_enabled = false;
    HANDLE m_hProcess = nullptr;
};

extern "C" __declspec(dllexport) PowertoyModuleIface* __cdecl powertoy_create()
{
    return new AlwaysOnTopModuleInterface();
}
