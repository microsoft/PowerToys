// dllmain.cpp : Defines the entry point for the DLL application.
#include "pch.h"

#include <mutex>
#include <common/SettingsAPI/settings_helpers.h>
#include <common/utils/winapi_error.h>
#include <common/utils/logger_helper.h>
#include <common/interop/shared_constants.h>

#include "trace.h"
#include "../interface/powertoy_module_interface.h"
#include "Generated Files/resource.h"

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

class ShortcutGuideModule : public PowertoyModuleIface
{
public:
    ShortcutGuideModule()
    {
        app_name = GET_RESOURCE_STRING(IDS_SHORTCUT_GUIDE);
        app_key = L"Shortcut Guide";
        LoggerHelpers::init_logger(app_key, L"ModuleInterface", LogSettings::shortcutGuideLoggerName);

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

    virtual bool get_config(wchar_t* buffer, int* buffer_size) override
    {
        return true;
    }

    virtual void set_config(const wchar_t* config) override
    {
    }

    virtual void enable() override
    {
        Logger::info("Shortcut Guide is enabling");

        if (!_enabled)
        {
            Trace::EnableShortcutGuide(true);
            _enabled = true;
        }
        else
        {
            Logger::warn("Shortcut guide is already enabled");
        }
    }

    virtual void disable() override
    {
        this->disable(true);
    }

    virtual bool is_enabled() override
    {
        return _enabled;
    }

    virtual void destroy() override
    {
        this->disable(false);
        delete this;
    }

    virtual size_t get_hotkeys(Hotkey* buffer, size_t buffer_size) override
    {
        if (buffer_size < 1)
        {
            return 1;
        }

        buffer[0].win = true;
        buffer[0].alt = false;
        buffer[0].shift = true;
        buffer[0].ctrl = false;
        buffer[0].key = VK_OEM_2;
        return 1;
    }

    virtual bool on_hotkey(size_t hotkeyId) override
    {
        if (!_enabled)
        {
            return false;
        }

        if (hotkeyId == 0)
        {
            Logger::trace("On hotkey");
            if (IsProcessActive())
            {
                TerminateProcess();
                return true;
            }

            StartProcess();
            return true;
        }

        return false;
    }

private:
    const int alternative_switch_hotkey_id = 0x2;
    const UINT alternative_switch_modifier_mask = MOD_WIN | MOD_SHIFT;
    const UINT alternative_switch_vk_code = VK_OEM_2;

    std::wstring app_name;
    //contains the non localized key of the powertoy
    std::wstring app_key;
    bool _enabled = false;
    HANDLE m_hProcess = nullptr;

    void disable(bool trace_event)
    {
        Logger::info("OverlayWindow::disable");
        if (_enabled)
        {
            _enabled = false;
            if (trace_event)
            {
                Trace::EnableShortcutGuide(false);
            }

            TerminateProcess();
        }
        else
        {
            Logger::warn("Shortcut Guide is already disabled");
        }
    }

    bool StartProcess()
    {
        unsigned long powertoys_pid = GetCurrentProcessId();
        std::wstring executable_args = L"";
        executable_args.append(std::to_wstring(powertoys_pid));

        SHELLEXECUTEINFOW sei{ sizeof(sei) };
        sei.fMask = { SEE_MASK_NOCLOSEPROCESS | SEE_MASK_FLAG_NO_UI };
        sei.lpFile = L"modules\\ShortcutGuide\\ShortcutGuide\\PowerToys.ShortcutGuide.exe";
        sei.nShow = SW_SHOWNORMAL;
        sei.lpParameters = executable_args.data();
        if (ShellExecuteExW(&sei) == false)
        {
            Logger::error(L"Failed to start SG process. {}", get_last_error_or_default(GetLastError()));
            auto message = get_last_error_message(GetLastError());
            if (message.has_value())
            {
                Logger::error(message.value());
            }

            return false;
        }

        Logger::trace(L"Started SG process with pid={}", GetProcessId(sei.hProcess));
        m_hProcess = sei.hProcess;
        return true;    
    }

    void TerminateProcess()
    {
        if (m_hProcess)
        {
            if (WaitForSingleObject(m_hProcess, 0) != WAIT_OBJECT_0)
            {
                if (!::TerminateProcess(m_hProcess, 0))
                {
                    Logger::warn(L"Failed to terminate the process");
                }
                else
                {
                    CloseHandle(m_hProcess);
                    m_hProcess = nullptr;
                    Logger::trace("Terminated the process successfully");
                }
            }
            else
            {
                CloseHandle(m_hProcess);
                m_hProcess = nullptr;
                Logger::trace("SG process was already terminated");
            }
        }
    }

    bool IsProcessActive()
    {
        return m_hProcess && WaitForSingleObject(m_hProcess, 0) != WAIT_OBJECT_0;
    }
};

extern "C" __declspec(dllexport) PowertoyModuleIface* __cdecl powertoy_create()
{
    return new ShortcutGuideModule();
}