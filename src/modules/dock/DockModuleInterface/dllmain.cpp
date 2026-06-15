#include "pch.h"

#include <interface/powertoy_module_interface.h>

#include <common/logger/logger.h>
#include <common/utils/resources.h>
#include <common/utils/winapi_error.h>

#include <Dock/trace.h>
#include <Dock/ModuleConstants.h>

#include <shellapi.h>
#include <common/SettingsAPI/settings_objects.h>

BOOL APIENTRY DllMain(HMODULE /*hModule*/, DWORD ul_reason_for_call, LPVOID /*lpReserved*/)
{
    switch (ul_reason_for_call)
    {
    case DLL_PROCESS_ATTACH:
        Trace::Dock::RegisterProvider();
        break;
    case DLL_THREAD_ATTACH:
    case DLL_THREAD_DETACH:
        break;
    case DLL_PROCESS_DETACH:
        Trace::Dock::UnregisterProvider();
        break;
    }
    return TRUE;
}

class DockModuleInterface : public PowertoyModuleIface
{
public:
    virtual PCWSTR get_name() override
    {
        return L"Dock";
    }

    virtual const wchar_t* get_key() override
    {
        return NonLocalizable::ModuleKey;
    }

    virtual bool get_config(wchar_t* buffer, int* buffer_size) override
    {
        HINSTANCE hinstance = reinterpret_cast<HINSTANCE>(&__ImageBase);
        PowerToysSettings::Settings settings(hinstance, get_name());
        return settings.serialize_to_buffer(buffer, buffer_size);
    }

    virtual void set_config(const wchar_t* config) override
    {
        try
        {
            PowerToysSettings::PowerToyValues values =
                PowerToysSettings::PowerToyValues::from_json_string(config, get_key());
            values.save_to_settings_file();
        }
        catch (std::exception&)
        {
        }
    }

    virtual void enable()
    {
        Logger::info("Dock enabling");
        m_enabled = true;

        Trace::Dock::Enable(true);

        unsigned long powertoys_pid = GetCurrentProcessId();
        std::wstring executable_args = std::to_wstring(powertoys_pid);

        SHELLEXECUTEINFOW sei{ sizeof(sei) };
        sei.fMask = SEE_MASK_NOCLOSEPROCESS | SEE_MASK_FLAG_NO_UI;
        sei.lpFile = NonLocalizable::ModulePath;
        sei.nShow = SW_SHOWNORMAL;
        sei.lpParameters = executable_args.data();
        if (!ShellExecuteExW(&sei))
        {
            Logger::error(L"Failed to start Dock");
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

    virtual void disable()
    {
        Logger::info("Dock disabling");
        m_enabled = false;

        Trace::Dock::Enable(false);

        SetEvent(m_hStopEvent);

        if (WaitForSingleObject(m_hProcess, 1500) == WAIT_TIMEOUT)
        {
            if (m_hProcess)
            {
                TerminateProcess(m_hProcess, 0);
            }
        }

        if (m_hProcess)
        {
            CloseHandle(m_hProcess);
            m_hProcess = nullptr;
        }
    }

    virtual bool is_enabled() override
    {
        return m_enabled;
    }

    virtual void destroy() override
    {
        disable();
        if (m_hStopEvent)
        {
            CloseHandle(m_hStopEvent);
            m_hStopEvent = nullptr;
        }
        delete this;
    }

    DockModuleInterface()
    {
        m_hStopEvent = CreateEventW(nullptr, TRUE, FALSE, NonLocalizable::StopEventName);
    }

private:
    bool m_enabled = false;
    HANDLE m_hProcess = nullptr;
    HANDLE m_hStopEvent = nullptr;
};

extern "C" __declspec(dllexport) PowertoyModuleIface* __cdecl powertoy_create()
{
    return new DockModuleInterface();
}
