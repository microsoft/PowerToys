#include "pch.h"

#include <interface/powertoy_module_interface.h>
#include <common/SettingsAPI/settings_objects.h>

#include <common/logger/logger.h>
#include <common/utils/logger_helper.h>
#include <common/utils/resources.h>
#include <common/utils/winapi_error.h>

#include <shellapi.h>

#include "trace.h"
#include "common/interop/shared_constants.h"

extern "C" IMAGE_DOS_HEADER __ImageBase;

BOOL APIENTRY DllMain(HMODULE /* hModule*/, DWORD ul_reason_for_call, LPVOID /*lpReserved*/)
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
const static wchar_t* MODULE_NAME = L"ClipPing";

// Implement the PowerToy Module Interface and all the required methods.
class ClipPingModuleInterface : public PowertoyModuleIface
{
private:
    // The PowerToy state.
    bool m_enabled = false;

    // Load initial settings from the persisted values.
    void init_settings();

    std::wstring app_name;
    std::wstring app_key; //contains the non localized key of the powertoy

    HANDLE m_hProcess = nullptr;
    HANDLE m_exit_event_handle = nullptr;
    HANDLE m_show_overlay_event_handle = nullptr;

public:
    ClipPingModuleInterface()
    {
        app_name = L"ClipPing";
        app_key = L"ClipPing";
        LoggerHelpers::init_logger(app_key, L"ModuleInterface", "ClipPing");
        m_exit_event_handle = CreateDefaultEvent(CommonSharedConstants::CLIPPING_EXIT_EVENT);
        m_show_overlay_event_handle = CreateDefaultEvent(CommonSharedConstants::CLIPPING_SHOW_OVERLAY_EVENT);
        init_settings();
    };

    // Destroy the powertoy and free memory
    void destroy() override
    {
        Disable(false);
        delete this;
    }

    // Return the display name of the powertoy, this will be cached by the runner
    const wchar_t* get_name() override
    {
        return MODULE_NAME;
    }

    // Return the non localized key of the powertoy, this will be cached by the runner
    const wchar_t* get_key() override
    {
        return app_key.c_str();
    }

    // Return JSON with the configuration options.
    bool get_config(wchar_t* buffer, int* buffer_size) override
    {
        HINSTANCE hinstance = reinterpret_cast<HINSTANCE>(&__ImageBase);

        // Create a Settings object.
        PowerToysSettings::Settings settings(hinstance, get_name());

        return settings.serialize_to_buffer(buffer, buffer_size);
    }

    // Called by the runner to pass the updated settings values as a serialized JSON.
    void set_config(const wchar_t* config) override
    {
        try
        {
            // Parse the input JSON string.
            PowerToysSettings::PowerToyValues values =
                PowerToysSettings::PowerToyValues::from_json_string(config, get_key());

            values.save_to_settings_file();
        }
        catch (std::exception&)
        {
            // Improper JSON.
        }
    }

    // Enable the powertoy
    void enable()
    {
        m_enabled = true;

        // Log telemetry
        Trace::Enable(true);

        ResetEvent(m_exit_event_handle);

        unsigned long powertoys_pid = GetCurrentProcessId();
        std::wstring executable_args;
        executable_args.append(std::to_wstring(powertoys_pid));

        SHELLEXECUTEINFOW sei{ sizeof(sei) };
        sei.fMask = { SEE_MASK_NOCLOSEPROCESS | SEE_MASK_FLAG_NO_UI };
        sei.lpFile = L"WinUI3Apps\\Powertoys.ClipPing.exe";
        sei.nShow = SW_SHOWNORMAL;
        sei.lpParameters = executable_args.data();
        if (ShellExecuteExW(&sei) == false)
        {
            Logger::error(L"Failed to start ClipPing");
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

    // Disable the powertoy
    void disable()
    {
        m_enabled = false;
        Disable(true);
    }

    // Returns if the powertoys is enabled
    bool is_enabled() override
    {
        return m_enabled;
    }

    void Disable(bool const traceEvent)
    {
        m_enabled = false;

        // Log telemetry
        if (traceEvent)
        {
            Trace::Enable(false);
        }

        // Tell the ClipPing process to exit.
        SetEvent(m_exit_event_handle);

        // Wait for 1.5 seconds for the process to end correctly and stop etw tracer
        WaitForSingleObject(m_hProcess, 1500);

        // If process is still running, terminate it
        if (m_hProcess)
        {
            TerminateProcess(m_hProcess, 0);
            m_hProcess = nullptr;
        }
    }
};

// Load the settings file.
void ClipPingModuleInterface::init_settings()
{
    try
    {
        // Load and parse the settings file for this PowerToy.
        PowerToysSettings::PowerToyValues settings =
            PowerToysSettings::PowerToyValues::load_from_settings_file(ClipPingModuleInterface::get_name());
    }
    catch (std::exception&)
    {
        // Error while loading from the settings file. Let default values stay as they are.
    }
}

extern "C" __declspec(dllexport) PowertoyModuleIface* __cdecl powertoy_create()
{
    return new ClipPingModuleInterface();
}