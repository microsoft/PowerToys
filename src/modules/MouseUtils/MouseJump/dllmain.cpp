#include "pch.h"

#include <interface/powertoy_module_interface.h>
//#include <interface/lowlevel_keyboard_event_data.h>
//#include <interface/win_hook_event_data.h>
#include <common/SettingsAPI/settings_objects.h>
#include "trace.h"
#include <common/utils/winapi_error.h>
#include <common/utils/logger_helper.h>

extern "C" IMAGE_DOS_HEADER __ImageBase;

HMODULE m_hModule;

BOOL APIENTRY DllMain(HMODULE hModule, DWORD ul_reason_for_call, LPVOID /*lpReserved*/)
{
    m_hModule = hModule;
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
const static wchar_t* MODULE_NAME = L"MouseJump";
// Add a description that will we shown in the module settings page.
const static wchar_t* MODULE_DESC = L"Quickly move the mouse long distances";

namespace
{
    const wchar_t JSON_KEY_PROPERTIES[] = L"properties";
    const wchar_t JSON_KEY_WIN[] = L"win";
    const wchar_t JSON_KEY_ALT[] = L"alt";
    const wchar_t JSON_KEY_CTRL[] = L"ctrl";
    const wchar_t JSON_KEY_SHIFT[] = L"shift";
    const wchar_t JSON_KEY_CODE[] = L"code";
    const wchar_t JSON_KEY_ACTIVATION_SHORTCUT[] = L"activation_shortcut";
}

// Implement the PowerToy Module Interface and all the required methods.
class MouseJump : public PowertoyModuleIface
{
private:
    // The PowerToy state.
    bool m_enabled = false;
    HANDLE m_hProcess = nullptr;

    bool is_process_running()
    {
        return WaitForSingleObject(m_hProcess, 0) == WAIT_TIMEOUT;
    }

    void launch_process()
    {
        Logger::info("calling launch_process()");
        Logger::trace(L"Starting MouseJump process");
        unsigned long powertoys_pid = GetCurrentProcessId();

        std::wstring executable_args = L"";
        executable_args.append(std::to_wstring(powertoys_pid));

        SHELLEXECUTEINFOW sei{ sizeof(sei) };
        sei.fMask = { SEE_MASK_NOCLOSEPROCESS | SEE_MASK_FLAG_NO_UI };
        sei.lpFile = L"PowerToys.MouseJumpUI.exe";
        sei.nShow = SW_SHOWNORMAL;
        sei.lpParameters = executable_args.data();
        if (ShellExecuteExW(&sei))
        {
            Logger::trace("Successfully started the Mouse Jump process");
        }
        else
        {
            Logger::error(L"Mouse Jump failed to start. {}", get_last_error_or_default(GetLastError()));
        }

        m_hProcess = sei.hProcess;
    }

    void terminate_process()
    {
        Logger::info("calling terminate_process()");
        TerminateProcess(m_hProcess, 1);
    }

public:
    // Constructor
    MouseJump()
    {
        LoggerHelpers::init_logger(MODULE_NAME, L"ModuleInterface", LogSettings::mouseJumpLoggerName);
        Logger::info(L"calling MouseJump()");
    };

    ~MouseJump()
    {
        disable();
    }

    // Destroy the powertoy and free memory
    virtual void destroy() override
    {
        disable();
        delete this;
    }

    // Return the display name of the powertoy, this will be cached by the runner
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
        return powertoys_gpo::getConfiguredMouseJumpEnabledValue();
    }

    // Return JSON with the configuration options.
    virtual bool get_config(wchar_t* buffer, int* buffer_size) override
    {
        return false;
    }

    // Signal from the Settings editor to call a custom action.
    // This can be used to spawn more complex editors.
    virtual void call_custom_action(const wchar_t* action) override
    {
    }

    // Called by the runner to pass the updated settings values as a serialized JSON.
    virtual void set_config(const wchar_t* config) override
    {
    }

    // Enable the powertoy
    virtual void enable()
    {
        Logger::info("calling enable()");
        m_enabled = true;
        launch_process();
        Trace::EnableJumpTool(true);
    }

    // Disable the powertoy
    virtual void disable()
    {
        Logger::info("calling disable()");
        if (m_enabled)
        {
            terminate_process();
        }
        m_enabled = false;
        Trace::EnableJumpTool(false);
    }

    // Returns if the powertoys is enabled
    virtual bool is_enabled() override
    {
        return m_enabled;
    }

    // Returns whether the PowerToys should be enabled by default
    virtual bool is_enabled_by_default() const override
    {
        return false;
    }

};

extern "C" __declspec(dllexport) PowertoyModuleIface* __cdecl powertoy_create()
{
    return new MouseJump();
}