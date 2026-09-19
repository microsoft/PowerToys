// dllmain.cpp : Defines the entry point for the DLL application.
#include "pch.h"

#include <mutex>
#include <common/SettingsAPI/settings_helpers.h>
#include <common/utils/winapi_error.h>
#include <common/utils/logger_helper.h>
#include <common/interop/shared_constants.h>

#include "../interface/powertoy_module_interface.h"
#include "Generated Files/resource.h"
#include <common/SettingsAPI/settings_objects.h>

BOOL APIENTRY DllMain(HMODULE /*hModule*/, DWORD /*ul_reason_for_call*/, LPVOID /*lpReserved*/)
{
    return TRUE;
}

class RobocopyUIModule : public PowertoyModuleIface
{
public:
    RobocopyUIModule()
    {
        app_name = GET_RESOURCE_STRING(IDS_ROBOCOPY_UI);
        app_key = L"RobocopyUI";
        LoggerHelpers::init_logger(app_key, L"ModuleInterface", LogSettings::robocopyUiLoggerName);

        std::filesystem::path oldLogPath(PTSettingsHelper::get_module_save_folder_location(app_key));
        oldLogPath.append("ShortcutGuideLogs");
        LoggerHelpers::delete_old_log_folder(oldLogPath);
    }

    virtual const wchar_t* get_name() override
    {
        return app_name.c_str();
    }

    virtual const wchar_t* get_key() override
    {
        return app_key.c_str();
    }

    // Return the configured status for the gpo policy for the module
    virtual powertoys_gpo::gpo_rule_configured_t gpo_policy_enabled_configuration() override
    {
        return powertoys_gpo::getConfiguredRobocopyUIEnabledValue();
    }

    virtual bool get_config(wchar_t* buffer, int* buffer_size) override
    {
        HINSTANCE hinstance = reinterpret_cast<HINSTANCE>(&__ImageBase);
        PowerToysSettings::Settings settings(hinstance, get_name());
        return settings.serialize_to_buffer(buffer, buffer_size);
    }

    virtual void set_config(const wchar_t* config) override
    {
        (void)config;
        Logger::trace("set_config()");
    }

    virtual void enable() override
    {
        Logger::info("Robocopy UI is enabling");

        if (!_enabled)
        {
            _enabled = true;
        }
        else
        {
            Logger::warn("Robocopy UI is already enabled");
        }
    }

    virtual void disable() override
    {
        Logger::info("RobocopyUI::disable()");
        if (_enabled)
        {
            _enabled = false;
        }
        else
        {
            Logger::warn("Robocopy UI is already disabled");
        }
    }

    virtual bool is_enabled() override
    {
        return _enabled;
    }

    virtual void destroy() override
    {
        this->disable();
        delete this;
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
                StartProcess();
            }
        }
        catch (std::exception&)
        {
            Logger::error(L"Failed to parse action. {}", action);
        }
    }

private:
    std::wstring app_name;
    //contains the non localized key of the powertoy
    std::wstring app_key;
    bool _enabled = false;
    winrt::handle m_process;

    bool StartProcess(std::wstring args = L"")
    {
        const bool trackProcess = args.empty();
        if (trackProcess && IsProcessActive())
        {
            return true;
        }

        unsigned long powertoys_pid = GetCurrentProcessId();
        std::wstring executable_args = L"";
        executable_args.append(std::to_wstring(powertoys_pid));
        if (!args.empty())
        {
            executable_args.append(L" ");
            executable_args.append(args);
        }

        SHELLEXECUTEINFOW sei{ sizeof(sei) };
        sei.fMask = { SEE_MASK_NOCLOSEPROCESS | SEE_MASK_FLAG_NO_UI };
        sei.lpFile = L"WinUI3Apps\\PowerToys.RobocopyUI.exe";
        sei.nShow = SW_SHOWNORMAL;
        sei.lpParameters = executable_args.data();
        if (ShellExecuteExW(&sei) == false)
        {
            Logger::error(L"Failed to start RobocopyUI process. {}", get_last_error_or_default(GetLastError()));
            auto message = get_last_error_message(GetLastError());
            if (message.has_value())
            {
                Logger::error(message.value());
            }

            return false;
        }

        winrt::handle launchedProcess{ sei.hProcess };
        Logger::trace(L"Started RobocopyUI process with pid={}", GetProcessId(launchedProcess.get()));
        if (trackProcess)
        {
            m_process = std::move(launchedProcess);
        }

        return true;
    }

    bool IsProcessActive()
    {
        if (!m_process)
        {
            return false;
        }

        auto result = WaitForSingleObject(m_process.get(), 0);
        if (result == WAIT_FAILED)
        {
            Logger::error("Failed to wait for RobocopyUI process.");
        }

        if (result == WAIT_OBJECT_0)
        {
            m_process = {};
        }

        return result == WAIT_TIMEOUT;
    }
};

extern "C" __declspec(dllexport) PowertoyModuleIface* __cdecl powertoy_create()
{
    return new RobocopyUIModule();
}