#include "pch.h"
#include <interface/powertoy_module_interface.h>
#include <common/SettingsAPI/settings_objects.h>
#include <common/interop/shared_constants.h>
#include "trace.h"
#include "resource.h"
#include "EspressoConstants.h"
#include <common/logger/logger.h>
#include <common/SettingsAPI/settings_helpers.h>

#include <common/utils/elevation.h>
#include <common/utils/process_path.h>
#include <common/utils/resources.h>
#include <common/utils/os-detect.h>
#include <common/utils/winapi_error.h>

#include <filesystem>


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



// The PowerToy name that will be shown in the settings.
const static wchar_t* MODULE_NAME = L"Espresso";
// Add a description that will we shown in the module settings page.
const static wchar_t* MODULE_DESC = L"<no description>";

// These are the properties shown in the Settings page.
struct ModuleSettings
{

} g_settings;

// Implement the PowerToy Module Interface and all the required methods.
class Espresso : public PowertoyModuleIface
{
    std::wstring app_name;
    //contains the non localized key of the powertoy
    std::wstring app_key;

private:
    // The PowerToy state.
    bool m_enabled = false;

    HANDLE m_hProcess;

    HANDLE send_telemetry_event;

    // Handle to event used to invoke Espresso
    HANDLE m_hInvokeEvent;

    // Load initial settings from the persisted values.
    void init_settings();

    bool is_process_running()
    {
        return WaitForSingleObject(m_hProcess, 0) == WAIT_TIMEOUT;
    }

    void launch_process()
    {
        Logger::trace(L"Launching Espresso process");
        unsigned long powertoys_pid = GetCurrentProcessId();

        std::wstring executable_args = L"--use-pt-config --pid " + std::to_wstring(powertoys_pid);
        Logger::trace(L"Espresso launching with parameters: " + executable_args);

        SHELLEXECUTEINFOW sei{ sizeof(sei) };
        sei.fMask = { SEE_MASK_NOCLOSEPROCESS | SEE_MASK_FLAG_NO_UI };
        sei.lpFile = L"modules\\Espresso\\PowerToys.Espresso.exe";
        sei.nShow = SW_SHOWNORMAL;
        sei.lpParameters = executable_args.data();
        if (!ShellExecuteExW(&sei))
        {
            DWORD error = GetLastError();
            std::wstring message = L"Espresso failed to start with error = ";
            message += std::to_wstring(error);
            Logger::error(message);
        }

        m_hProcess = sei.hProcess;
    }

public:
    // Constructor
    Espresso()
    {
        app_name = GET_RESOURCE_STRING(IDS_ESPRESSO_NAME);
        app_key = EspressoConstants::ModuleKey;
        std::filesystem::path logFilePath(PTSettingsHelper::get_module_save_folder_location(this->app_key));
        logFilePath.append(LogSettings::espressoLogPath);
        Logger::init(LogSettings::launcherLoggerName, logFilePath.wstring(), PTSettingsHelper::get_log_settings_file_location());
        Logger::info("Launcher object is constructing");
        init_settings();
    };

    // Destroy the powertoy and free memory
    virtual void destroy() override
    {
        delete this;
    }

    // Return the display name of the powertoy, this will be cached by the runner
    virtual const wchar_t* get_name() override
    {
        return MODULE_NAME;
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

    // Return the non localized key of the powertoy, this will be cached by the runner
    virtual const wchar_t* get_key() override
    {
        return app_key.c_str();
    }

    // Signal from the Settings editor to call a custom action.
    // This can be used to spawn more complex editors.
    virtual void call_custom_action(const wchar_t* action) override
    {
        static UINT custom_action_num_calls = 0;
        try
        {
            // Parse the action values, including name.
            PowerToysSettings::CustomActionObject action_object =
                PowerToysSettings::CustomActionObject::from_json_string(action);
        }
        catch (std::exception&)
        {
            // Improper JSON.
        }
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

    virtual void enable()
    {
        ResetEvent(send_telemetry_event);
        ResetEvent(m_hInvokeEvent);
        launch_process();
        m_enabled = true;
    };

    virtual void disable()
    {
        if (m_enabled)
        {
            ResetEvent(send_telemetry_event);
            ResetEvent(m_hInvokeEvent);
            TerminateProcess(m_hProcess, 1);
        }

        m_enabled = false;
    }

    // Returns if the powertoys is enabled
    virtual bool is_enabled() override
    {
        return m_enabled;
    }
};

// Load the settings file.
void Espresso::init_settings()
{
    try
    {
        // Load and parse the settings file for this PowerToy.
        PowerToysSettings::PowerToyValues settings =
            PowerToysSettings::PowerToyValues::load_from_settings_file(get_key());
    }
    catch (std::exception ex)
    {
        Logger::warn(L"An exception occurred while loading the settings file");
        // Error while loading from the settings file. Let default values stay as they are.
    }
}


extern "C" __declspec(dllexport) PowertoyModuleIface* __cdecl powertoy_create()
{
    return new Espresso();
}