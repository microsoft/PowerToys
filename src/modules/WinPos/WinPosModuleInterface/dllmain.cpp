#include "pch.h"
#include <interface/powertoy_module_interface.h>
#include "trace.h"
#include <common/logger/logger.h>
#include <common/SettingsAPI/settings_objects.h>
#include <common/SettingsAPI/settings_helpers.h>
#include <common/utils/logger_helper.h>
#include <common/interop/shared_constants.h>

extern "C" IMAGE_DOS_HEADER __ImageBase;

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
const static wchar_t* MODULE_NAME = L"WinPos";
// Add a description that will be shown in the module settings page.
const static wchar_t* MODULE_DESC = L"Move and resize windows with Alt+Drag (left button to move, right button to resize).";

class WinPosInterface : public PowertoyModuleIface
{
private:
    bool m_enabled = false;
    HANDLE m_process{ nullptr };
    HANDLE m_reload_settings_event_handle{ nullptr };

public:
    WinPosInterface()
    {
        LoggerHelpers::init_logger(L"WinPos", L"ModuleInterface", LogSettings::winPosLoggerName);
        m_reload_settings_event_handle = CreateDefaultEvent(CommonSharedConstants::WINPOS_REFRESH_SETTINGS_EVENT);
    }

    virtual const wchar_t* get_key() override
    {
        return L"WinPos";
    }

    virtual void destroy() override
    {
        disable();
        delete this;
    }

    virtual const wchar_t* get_name() override
    {
        return MODULE_NAME;
    }

    virtual powertoys_gpo::gpo_rule_configured_t gpo_policy_enabled_configuration() override
    {
        return powertoys_gpo::getConfiguredWinPosEnabledValue();
    }

    virtual bool get_config(wchar_t* buffer, int* buffer_size) override
    {
        HINSTANCE hinstance = reinterpret_cast<HINSTANCE>(&__ImageBase);

        PowerToysSettings::Settings settings(hinstance, get_name());
        settings.set_description(MODULE_DESC);
        settings.set_overview_link(L"https://aka.ms/powertoys");

        return settings.serialize_to_buffer(buffer, buffer_size);
    }

    virtual void set_config(const wchar_t* config) override
    {
        try
        {
            auto values = PowerToysSettings::PowerToyValues::from_json_string(config, get_key());
            values.save_to_settings_file();

            // Signal the WinPos process to reload settings
            if (m_reload_settings_event_handle)
            {
                SetEvent(m_reload_settings_event_handle);
            }
        }
        catch (const std::exception&)
        {
            Logger::error("[WinPos] set_config: Failed to parse or apply config.");
        }
    }

    virtual void enable()
    {
        m_enabled = true;
        Logger::info(L"Enabling WinPos module...");
        Trace::Enable(true);

        unsigned long powertoys_pid = GetCurrentProcessId();
        std::wstring args = L"--pid " + std::to_wstring(powertoys_pid);
        std::wstring exe_name = L"WinPos\\PowerToys.WinPos.exe";

        std::wstring resolved_path(MAX_PATH, L'\0');
        DWORD result = SearchPathW(
            nullptr,
            exe_name.c_str(),
            nullptr,
            static_cast<DWORD>(resolved_path.size()),
            resolved_path.data(),
            nullptr);

        if (result == 0 || result >= resolved_path.size())
        {
            Logger::error(
                L"Failed to locate WinPos executable named '{}' at location '{}'",
                exe_name,
                resolved_path.c_str());
            return;
        }

        resolved_path.resize(result);
        Logger::debug(L"Resolved executable path: {}", resolved_path);

        std::wstring command_line = L"\"" + resolved_path + L"\" " + args;

        STARTUPINFO si = { sizeof(si) };
        PROCESS_INFORMATION pi;

        if (!CreateProcessW(
                resolved_path.c_str(),
                command_line.data(),
                nullptr,
                nullptr,
                TRUE,
                0,
                nullptr,
                nullptr,
                &si,
                &pi))
        {
            Logger::error(L"Failed to launch WinPos process. {}", get_last_error_or_default(GetLastError()));
            return;
        }

        Logger::info(L"WinPos process launched successfully (PID: {}).", pi.dwProcessId);
        m_process = pi.hProcess;
        CloseHandle(pi.hThread);
    }

    virtual void disable()
    {
        Logger::info("WinPos disabling");
        m_enabled = false;

        if (m_process)
        {
            constexpr DWORD timeout_ms = 1500;
            DWORD result = WaitForSingleObject(m_process, timeout_ms);

            if (result == WAIT_TIMEOUT)
            {
                Logger::warn("WinPos: Process didn't exit in time. Forcing termination.");
                TerminateProcess(m_process, 0);
            }

            CloseHandle(m_process);
            m_process = nullptr;
        }

        Trace::Enable(false);
    }

    virtual bool is_enabled() override
    {
        return m_enabled;
    }

    virtual bool is_enabled_by_default() const override
    {
        return false;
    }
};

extern "C" __declspec(dllexport) PowertoyModuleIface* __cdecl powertoy_create()
{
    return new WinPosInterface();
}
