#include "pch.h"

#include <interface/powertoy_module_interface.h>

#include <common/logger/logger.h>
#include <common/utils/resources.h>
#include <common/utils/winapi_error.h>

#include <shellapi.h>
#include <common/SettingsAPI/settings_objects.h>
#include <common/interop/shared_constants.h>

namespace NonLocalizable
{
    const wchar_t ModulePath[] = L"PowerToys.ShowDesktop.exe";
    const wchar_t ModuleKey[] = L"ShowDesktop";
}

BOOL APIENTRY DllMain(HMODULE /*hModule*/, DWORD ul_reason_for_call, LPVOID /*lpReserved*/)
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

class ShowDesktopModuleInterface : public PowertoyModuleIface
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

    // Return the configured status for the gpo policy for the module
    virtual powertoys_gpo::gpo_rule_configured_t gpo_policy_enabled_configuration() override
    {
        return powertoys_gpo::getConfiguredShowDesktopEnabledValue();
    }

    // Return JSON with the configuration options.
    virtual bool get_config(wchar_t* buffer, int* buffer_size) override
    {
        HINSTANCE hinstance = reinterpret_cast<HINSTANCE>(&__ImageBase);

        PowerToysSettings::Settings settings(hinstance, get_name());

        return settings.serialize_to_buffer(buffer, buffer_size);
    }

    // Passes JSON with the configuration settings for the powertoy.
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
            // Improper JSON.
        }
    }

    virtual bool on_hotkey(size_t /*hotkeyId*/) override
    {
        return false;
    }

    virtual size_t get_hotkeys(Hotkey* /*hotkeys*/, size_t /*buffer_size*/) override
    {
        return 0;
    }

    // Enable the powertoy
    virtual void enable()
    {
        Logger::info("ShowDesktop enabling");

        Enable();
    }

    // Disable the powertoy
    virtual void disable()
    {
        Logger::info("ShowDesktop disabling");

        Disable();
    }

    // Returns if the powertoy is enabled
    virtual bool is_enabled() override
    {
        return m_enabled;
    }

    // Destroy the powertoy and free memory
    virtual void destroy() override
    {
        Disable();
        delete this;
    }

    ShowDesktopModuleInterface()
    {
        app_name = L"ShowDesktop";
        app_key = NonLocalizable::ModuleKey;
        m_hTerminateEvent = CreateDefaultEvent(CommonSharedConstants::SHOW_DESKTOP_TERMINATE_EVENT);
    }

private:
    void Enable()
    {
        m_enabled = true;

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
            Logger::error(L"Failed to start ShowDesktop");
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

    void Disable()
    {
        m_enabled = false;

        SetEvent(m_hTerminateEvent);

        // Wait for 1.5 seconds for the process to end correctly
        WaitForSingleObject(m_hProcess, 1500);

        // If process is still running, terminate it
        if (m_hProcess)
        {
            TerminateProcess(m_hProcess, 0);
            m_hProcess = nullptr;
        }
    }

    bool is_process_running()
    {
        return WaitForSingleObject(m_hProcess, 0) == WAIT_TIMEOUT;
    }

    std::wstring app_name;
    std::wstring app_key;

    bool m_enabled = false;
    HANDLE m_hProcess = nullptr;
    HANDLE m_hTerminateEvent;
};

extern "C" __declspec(dllexport) PowertoyModuleIface* __cdecl powertoy_create()
{
    return new ShowDesktopModuleInterface();
}
