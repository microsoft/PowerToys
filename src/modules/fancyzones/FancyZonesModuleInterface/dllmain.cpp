#include "pch.h"

#include <interface/powertoy_module_interface.h>

#include <common/interop/shared_constants.h>
#include <common/logger/logger.h>
#include <common/utils/resources.h>
#include <common/utils/winapi_error.h>

#include <FancyZonesLib/Generated Files/resource.h>
#include <FancyZonesLib/trace.h>
#include <FancyZonesLib/Settings.h>
#include <FancyZonesLib/ModuleConstants.h>

#include <shellapi.h>

// Non-localizable
const std::wstring fancyZonesPath = L"PowerToys.FancyZones.exe";

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

class FancyZonesModuleInterface : public PowertoyModuleIface
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
        return powertoys_gpo::getConfiguredFancyZonesEnabledValue();
    }

    // Return JSON with the configuration options.
    // These are the settings shown on the settings page along with their current values.
    virtual bool get_config(_Out_ PWSTR /*buffer*/, _Out_ int* /*buffer_size*/) override
    {
        return false;
    }

    // Passes JSON with the configuration settings for the powertoy.
    // This is called when the user hits Save on the settings page.
    virtual void set_config(PCWSTR /*config*/) override
    {
    }

    // Signal from the Settings editor to call a custom action.
    // This can be used to spawn more complex editors.
    virtual void call_custom_action(const wchar_t* /*action*/) override
    {
        SetEvent(m_toggleEditorEvent);
    }

    // Enable the powertoy
    virtual void enable()
    {
        Logger::info("FancyZones enabling");

        Enable();
    }

    // Disable the powertoy
    virtual void disable()
    {
        Logger::info("FancyZones disabling");

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

        if (m_toggleEditorEvent)
        {
            CloseHandle(m_toggleEditorEvent);
            m_toggleEditorEvent = nullptr;
        }

        delete this;
    }

    virtual void send_settings_telemetry() override
    {
        Logger::info("Send settings telemetry");
        FancyZonesSettings::instance().LoadSettings();
        Trace::SettingsTelemetry(FancyZonesSettings::settings());
    }

    FancyZonesModuleInterface()
    {
        app_name = GET_RESOURCE_STRING(IDS_FANCYZONES);
        app_key = NonLocalizable::ModuleKey;

        m_toggleEditorEvent = CreateDefaultEvent(CommonSharedConstants::FANCY_ZONES_EDITOR_TOGGLE_EVENT);
        if (!m_toggleEditorEvent)
        {
            Logger::error(L"Failed to create toggle editor event");
            auto message = get_last_error_message(GetLastError());
            if (message.has_value())
            {
                Logger::error(message.value());
            }
        }
    }

private:
    void Enable()
    {
        m_enabled = true;

        // Log telemetry
        Trace::FancyZones::EnableFancyZones(true);

        unsigned long powertoys_pid = GetCurrentProcessId();
        std::wstring executable_args = L"";
        executable_args.append(std::to_wstring(powertoys_pid));

        SHELLEXECUTEINFOW sei{ sizeof(sei) };
        sei.fMask = { SEE_MASK_NOCLOSEPROCESS | SEE_MASK_FLAG_NO_UI };
        sei.lpFile = fancyZonesPath.c_str();
        sei.nShow = SW_SHOWNORMAL;
        sei.lpParameters = executable_args.data();
        if (ShellExecuteExW(&sei) == false)
        {
            Logger::error(L"Failed to start FancyZones");
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

    void SendFZECloseEvent()
    {
        auto exitEvent = CreateEventW(nullptr, false, false, CommonSharedConstants::FZE_EXIT_EVENT);
        if (!exitEvent)
        {
            Logger::warn(L"Failed to create exitEvent. {}", get_last_error_or_default(GetLastError()));
        }
        else
        {
            Logger::trace(L"Signaled exitEvent");
            if (!SetEvent(exitEvent))
            {
                Logger::warn(L"Failed to signal exitEvent. {}", get_last_error_or_default(GetLastError()));
            }

            ResetEvent(exitEvent);
            CloseHandle(exitEvent);
        }
    }

    void Disable(bool const traceEvent)
    {
        m_enabled = false;
        // Log telemetry
        if (traceEvent)
        {
            Trace::FancyZones::EnableFancyZones(false);
        }

        if (m_toggleEditorEvent)
        {
            ResetEvent(m_toggleEditorEvent);
        }

        if (m_hProcess)
        {
            TerminateProcess(m_hProcess, 0);
            SendFZECloseEvent();
            m_hProcess = nullptr;
        }
    }

    std::wstring app_name;
    //contains the non localized key of the powertoy
    std::wstring app_key;

    bool m_enabled = false;
    HANDLE m_hProcess = nullptr;

    // Handle to event used to invoke FancyZones Editor
    HANDLE m_toggleEditorEvent;
};

extern "C" __declspec(dllexport) PowertoyModuleIface* __cdecl powertoy_create()
{
    return new FancyZonesModuleInterface();
}
