// dllmain.cpp : Defines the entry point for the DLL application.
#include "pch.h"
#include <interface/powertoy_module_interface.h>
#include <common/SettingsAPI/settings_objects.h>
#include "trace.h"
#include <common/interop/shared_constants.h>
#include <common/utils/string_utils.h>
#include <common/utils/winapi_error.h>
#include <common/utils/logger_helper.h>
#include <common/utils/EventWaiter.h>
#include <common/utils/resources.h>
#include <common/utils/modulesRegistry.h>
#include <common/utils/process_path.h>

#include "resource.h"
#include "Constants.h"

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

const static wchar_t* MODULE_NAME = L"RegistryPreview";
const static wchar_t* MODULE_DESC = L"A quick little utility to visualize and edit complex Windows Registry files.";

class RegistryPreviewModule : public PowertoyModuleIface
{

private:
    bool m_enabled = false;

    //Hotkey m_hotkey;
    HANDLE m_hProcess;

    HANDLE triggerEvent;
    EventWaiter triggerEventWaiter;

    bool is_process_running()
    {
        return WaitForSingleObject(m_hProcess, 0) == WAIT_TIMEOUT;
    }

    void launch_process()
    {
        if (m_enabled)
        {
            Logger::trace(L"Starting Registry Preview process");
            unsigned long powertoys_pid = GetCurrentProcessId();

            std::wstring executable_args = L"";
            executable_args.append(std::to_wstring(powertoys_pid));

            SHELLEXECUTEINFOW sei{ sizeof(sei) };
            sei.fMask = { SEE_MASK_NOCLOSEPROCESS | SEE_MASK_FLAG_NO_UI };
            sei.lpFile = L"modules\\RegistryPreview\\PowerToys.RegistryPreview.exe";
            sei.nShow = SW_SHOWNORMAL;
            sei.lpParameters = executable_args.data();
            if (ShellExecuteExW(&sei))
            {
                Logger::trace("Successfully started the Registry Preview process");
            }
            else
            {
                Logger::error(L"Registry Preview failed to start. {}", get_last_error_or_default(GetLastError()));
            }

            m_hProcess = sei.hProcess;
        }
    }

    void terminate_process()
    {
        TerminateProcess(m_hProcess, 1);
    }

public:
    RegistryPreviewModule()
    {
        LoggerHelpers::init_logger(GET_RESOURCE_STRING(IDS_REGISTRYPREVIEW_NAME), L"ModuleInterface", "RegistryPreview");
        Logger::info("Registry Preview object is constructing");

        if (!m_enabled)
        {
            const std::wstring installationDir = get_module_folderpath();

            auto regChanges = getRegistryPreviewChangeSet(installationDir, true);

            if (!regChanges.unApply())
            {
                Logger::error(L"Unapplying registry changes failed");
            }
        }

        triggerEvent = CreateEvent(nullptr, false, false, CommonSharedConstants::REGISTRY_PREVIEW_TRIGGER_EVENT);
        triggerEventWaiter = EventWaiter(CommonSharedConstants::REGISTRY_PREVIEW_TRIGGER_EVENT, [this](int) {
            on_hotkey(0);
        });
    }

    ~RegistryPreviewModule()
    {
        if (m_enabled)
        {
            terminate_process();
        }
        m_enabled = false;
    }

    // Destroy the powertoy and free memory
    virtual void destroy() override
    {
        delete this;
    }

    // Return the localized display name of the powertoy
    virtual const wchar_t* get_name() override
    {
        return MODULE_NAME;
    }

    // Return the non localized key of the powertoy, this will be cached by the runner
    virtual const wchar_t* get_key() override
    {
        return MODULE_NAME;
    }

    // Return the configured status for the gpo policy for the module
    virtual powertoys_gpo::gpo_rule_configured_t gpo_policy_enabled_configuration() override
    {
        return powertoys_gpo::getConfiguredRegistryPreviewEnabledValue();
    }

    // Return JSON with the configuration options.
    virtual bool get_config(wchar_t* buffer, int* buffer_size) override
    {
        HINSTANCE hinstance = reinterpret_cast<HINSTANCE>(&__ImageBase);

        // Create a Settings object.
        PowerToysSettings::Settings settings(hinstance, get_name());
        settings.set_description(MODULE_DESC);

        return settings.serialize_to_buffer(buffer, buffer_size);
    }

    // Pop open the app, if the OOBE page asks it to
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

    // Called by the runner to pass the updated settings values as a serialized JSON.
    virtual void set_config(const wchar_t* config) override
    {
        try
        {
            // Parse the input JSON string.
            PowerToysSettings::PowerToyValues values = PowerToysSettings::PowerToyValues::from_json_string(config, get_key());

            // If you don't need to do any custom processing of the settings, proceed
            // to persists the values.
            values.save_to_settings_file();
        }
        catch (std::exception&)
        {
            // Improper JSON.
        }
    }

    // Enable the powertoy
    virtual void enable()
    {
        const std::wstring installationDir = get_module_folderpath();

        if (!getRegistryPreviewChangeSet(installationDir, true).apply())
        {
            Logger::error(L"Applying registry changes failed");
        }

        // let the DLL enable the app
        m_enabled = true;
        Trace::EnableRegistryPreview(true);
    };

    virtual void disable()
    {
        if (m_enabled)
        {
            // let the DLL disable the app
            terminate_process();

            Trace::EnableRegistryPreview(false);
            Logger::trace(L"Disabling Registry Preview...");

            // Yeet the Registry setting so preview doesn't work anymore
            const std::wstring installationDir = get_module_folderpath();

            if (!getRegistryPreviewChangeSet(installationDir, true).unApply())
            {
                Logger::error(L"Unapplying registry changes failed");
            }
        }

        m_enabled = false;
    }

    // Returns if the powertoys is enabled
    virtual bool is_enabled() override
    {
        return m_enabled;
    }

    // Respond to a "click" from the launcher
    virtual bool on_hotkey(size_t /*hotkeyId*/) override
    {
        if (m_enabled)
        {
            Logger::trace(L"Registry Preview hotkey pressed");
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
};

extern "C" __declspec(dllexport) PowertoyModuleIface* __cdecl powertoy_create()
{
    return new RegistryPreviewModule();
}
