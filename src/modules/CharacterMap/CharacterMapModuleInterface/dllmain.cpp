#include "pch.h"

#include <modules/interface/powertoy_module_interface.h>
#include <common/SettingsAPI/settings_objects.h>

#include "trace.h"
#include <common/logger/logger.h>
#include <common/utils/logger_helper.h>
#include <common/utils/EventWaiter.h>
#include <common/utils/resources.h>
#include <common/utils/winapi_error.h>

#include <shellapi.h>
#include <common/interop/shared_constants.h>

namespace NonLocalizable
{
    //const wchar_t ModulePath[] = L"PowerToys.CharacterMap.exe";
    const inline wchar_t ModuleKey[] = L"CharacterMap";
}

BOOL APIENTRY DllMain( HMODULE /*hModule*/,
                       DWORD  ul_reason_for_call,
                       LPVOID /*lpReserved*/
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

class CharacterMapModuleInterface : public PowertoyModuleIface
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
        return powertoys_gpo::getConfiguredCharacterMapEnabledValue();
    }

    // Return JSON with the configuration options.
    // These are the settings shown on the settings page along with their current values.
    virtual bool get_config(wchar_t* buffer, int* buffer_size) override
    {
        HINSTANCE hinstance = reinterpret_cast<HINSTANCE>(&__ImageBase);

        // TODO: Read settings from Registry.

        // Create a Settings object.
        PowerToysSettings::Settings settings(hinstance, get_name());

        return settings.serialize_to_buffer(buffer, buffer_size);
    }

    // Passes JSON with the configuration settings for the powertoy.
    // This is called when the user hits Save on the settings page.
    virtual void set_config(const wchar_t*) override
    {
        try
        {
            // Parse the input JSON string.
            // TODO: Save settings to registry.
        }
        catch (std::exception&)
        {
            // Improper JSON.
        }
    }

    // Enable the powertoy
    virtual void enable()
    {
        Logger::info("CharacterMap enabling");
        Enable();
    }

    // Disable the powertoy
    virtual void disable()
    {
        Logger::info("CharacterMap disabling");
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

    CharacterMapModuleInterface()
    {
        app_name = L"CharacterMap";
        app_key = NonLocalizable::ModuleKey;
        LoggerHelpers::init_logger(app_key, L"ModuleInterface", LogSettings::characterMapLoggerName);
        //m_reload_settings_event_handle = CreateDefaultEvent(CommonSharedConstants::ZOOMIT_REFRESH_SETTINGS_EVENT);
        //m_exit_event_handle = CreateDefaultEvent(CommonSharedConstants::ZOOMIT_EXIT_EVENT);
        triggerEvent = CreateEvent(nullptr, false, false, CommonSharedConstants::CHARACTER_MAP_TRIGGER_EVENT);
        triggerEventWaiter = EventWaiter(CommonSharedConstants::CHARACTER_MAP_TRIGGER_EVENT, [this](int) {
            on_hotkey(0);
        });
    }
    ~CharacterMapModuleInterface()
    {
        if (m_enabled)
        {
            terminate_process();
        }
        m_enabled = false;
    }

private:  
    bool is_process_running()
    {
        return WaitForSingleObject(m_hProcess, 0) == WAIT_TIMEOUT;
    }
    void launch_process()
    {
        if (m_enabled)
        {
            Logger::trace(L"Starting Registry Preview process");
            //Get current process id
            unsigned long powertoys_pid = GetCurrentProcessId();
            std::wstring executable_args = std::to_wstring(powertoys_pid);

            // Initiate SHELLEXECUTEINFOW structure
            SHELLEXECUTEINFOW sei{ sizeof(sei) };
            sei.fMask = SEE_MASK_NOCLOSEPROCESS | SEE_MASK_FLAG_NO_UI;
            sei.lpFile = L"shell:AppsFolder\\PowerToys.CharacterMap_8wekyb3d8bbwe!App"; // UWP App ID
            sei.nShow = SW_SHOWNORMAL;
            sei.lpParameters = executable_args.c_str(); 

            // start the app
            if (ShellExecuteExW(&sei) == FALSE)
            {
              
                Logger::error(L"Registry Preview failed to start. {}", get_last_error_or_default(GetLastError()));
                 
                
            }
            else
            {
                Logger::trace("PowerToys.CharacterMap started successfully!");
                m_hProcess = sei.hProcess;
            }
        }
    }
    virtual void call_custom_action(const wchar_t* action) override
    {
        try
        {
            PowerToysSettings::CustomActionObject action_object =
                PowerToysSettings::CustomActionObject::from_json_string(action);

            if (action_object.get_name() == L"Launch")
            {
                launch_process();
                Trace::ActivateEditor();
            }
        }
        catch (std::exception&)
        {
            Logger::error(L"Failed to parse action. {}", action);
        }
    }
    void terminate_process()
    {
        TerminateProcess(m_hProcess, 1);
    }


    bool is_enabled_by_default() const override
    {
        return false;
    }

    void Enable()
    {
        m_enabled = true;

        // Log telemetry
        Trace::EnableCharacterMap(true);
    }

    void Disable(bool const traceEvent)
    {
        m_enabled = false;
        // Log telemetry
        if (traceEvent)
        {
            Trace::EnableCharacterMap(false);
        }
        if (m_enabled)
        {
            // let the DLL disable the app
            terminate_process();

            Logger::trace(L"Disabling Registry Preview...");
        }

        m_enabled = false;

    }

    virtual bool on_hotkey(size_t /*hotkeyId*/) override
    {
        if (m_enabled)
        {
            Logger::trace(L"Character Map hotkey pressed");
            if (is_process_running())
            {
                terminate_process();
            }
            else
            {
                launch_process();
            }

            return true;
        }

        return false;
    }
    

    std::wstring app_name;
    std::wstring app_key; //contains the non localized key of the powertoy

    bool m_enabled = false;
    HANDLE m_hProcess = nullptr;

    HANDLE triggerEvent;
    EventWaiter triggerEventWaiter;

    //HANDLE m_reload_settings_event_handle = NULL;
    //HANDLE m_exit_event_handle = NULL;
};

extern "C" __declspec(dllexport) PowertoyModuleIface* __cdecl powertoy_create()
{
    return new CharacterMapModuleInterface();
}
