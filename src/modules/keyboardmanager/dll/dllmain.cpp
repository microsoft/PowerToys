#include "pch.h"
#include <interface/powertoy_module_interface.h>
#include <common/SettingsAPI/settings_objects.h>
#include <common/utils/resources.h>
#include "Generated Files/resource.h"
#include <keyboardmanager/common/KeyboardManagerConstants.h>
#include <common/utils/winapi_error.h>
#include <keyboardmanager/dll/trace.h>
#include <shellapi.h>
#include <common/utils/logger_helper.h>

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

// Implement the PowerToy Module Interface and all the required methods.
class KeyboardManager : public PowertoyModuleIface
{
private:
    // The PowerToy state.
    bool m_enabled = false;

    // The PowerToy name that will be shown in the settings.
    const std::wstring app_name = GET_RESOURCE_STRING(IDS_KEYBOARDMANAGER);

    //contains the non localized key of the powertoy
    std::wstring app_key = KeyboardManagerConstants::ModuleName;

    HANDLE m_hProcess = nullptr;

public:
    // Constructor
    KeyboardManager()
    {
        LoggerHelpers::init_logger(KeyboardManagerConstants::ModuleName, L"ModuleInterface", LogSettings::keyboardManagerLoggerName);

        std::filesystem::path oldLogPath(PTSettingsHelper::get_module_save_folder_location(app_key));
        oldLogPath.append("Logs");
        LoggerHelpers::delete_old_log_folder(oldLogPath);
    };

    // Destroy the powertoy and free memory
    virtual void destroy() override
    {
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
        return powertoys_gpo::getConfiguredKeyboardManagerEnabledValue();
    }

    // Return JSON with the configuration options.
    virtual bool get_config(wchar_t* buffer, int* buffer_size) override
    {
        HINSTANCE hinstance = reinterpret_cast<HINSTANCE>(&__ImageBase);

        // Create a Settings object.
        PowerToysSettings::Settings settings(hinstance, get_name());
        settings.set_description(IDS_SETTINGS_DESCRIPTION);
        settings.set_overview_link(L"https://aka.ms/PowerToysOverview_KeyboardManager");

        return settings.serialize_to_buffer(buffer, buffer_size);
    }

    // Signal from the Settings editor to call a custom action.
    virtual void call_custom_action(const wchar_t* /*action*/) override
    {
    }

    // Called by the runner to pass the updated settings values as a serialized JSON.
    virtual void set_config(const wchar_t* config) override
    {
        try
        {
            // Parse the input JSON string.
            PowerToysSettings::PowerToyValues values =
                PowerToysSettings::PowerToyValues::from_json_string(config, get_key());

            // If you don't need to do any custom processing of the settings, proceed
            // to persists the values calling:
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
        m_enabled = true;
        // Log telemetry
        Trace::EnableKeyboardManager(true);

        unsigned long powertoys_pid = GetCurrentProcessId();
        std::wstring executable_args = L"";
        executable_args.append(std::to_wstring(powertoys_pid));

        SHELLEXECUTEINFOW sei{ sizeof(sei) };
        sei.fMask = { SEE_MASK_NOCLOSEPROCESS | SEE_MASK_FLAG_NO_UI };
        sei.lpFile = L"modules\\KeyboardManager\\KeyboardManagerEngine\\PowerToys.KeyboardManagerEngine.exe";
        sei.nShow = SW_SHOWNORMAL;
        sei.lpParameters = executable_args.data();
        if (ShellExecuteExW(&sei) == false)
        {
            Logger::error(L"Failed to start keyboard manager engine");
            auto message = get_last_error_message(GetLastError());
            if (message.has_value())
            {
                Logger::error(message.value());
            }
        }
        else
        {
            m_hProcess = sei.hProcess;
            if (m_hProcess)
            {
                SetPriorityClass(m_hProcess, REALTIME_PRIORITY_CLASS);
            }
        }
    }

    // Disable the powertoy
    virtual void disable()
    {
        m_enabled = false;
        // Log telemetry
        Trace::EnableKeyboardManager(false);

        if (m_hProcess)
        {
            TerminateProcess(m_hProcess, 0);
            m_hProcess = nullptr;
        }
    }

    // Returns if the powertoys is enabled
    virtual bool is_enabled() override
    {
        return m_enabled;
    }
};

extern "C" __declspec(dllexport) PowertoyModuleIface* __cdecl powertoy_create()
{
    return new KeyboardManager();
}