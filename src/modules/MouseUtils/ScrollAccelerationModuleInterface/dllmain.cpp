#include "pch.h"
#include <interface/powertoy_module_interface.h>
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
        break;
    case DLL_THREAD_ATTACH:
    case DLL_THREAD_DETACH:
        break;
    case DLL_PROCESS_DETACH:
        break;
    }
    return TRUE;
}

// The PowerToy name that will be shown in the settings.
const static wchar_t* MODULE_NAME = L"ScrollAcceleration";
// Add a description that will be shown in the module settings page.
const static wchar_t* MODULE_DESC = L"Accelerate mouse wheel scrolling based on how fast you spin the wheel.";

class ScrollAccelerationInterface : public PowertoyModuleIface
{
private:
    bool m_enabled = false;
    HANDLE m_process{ nullptr };
    HANDLE m_reload_settings_event_handle{ nullptr };
    HANDLE m_exit_event_handle{ nullptr };
    HANDLE m_toggle_event_handle{ nullptr };
    HANDLE m_exclude_event_handle{ nullptr };

    // Activation shortcut: pauses / resumes the acceleration without stopping the module.
    // Default Win + Ctrl + Alt + W: three modifiers keep it clear of the Windows system shortcuts and of the
    // other PowerToys modules (the only other Win + Ctrl + Alt shortcuts are V, R, L and E).
    Hotkey m_hotkey{ .win = true, .ctrl = true, .shift = false, .alt = true, .key = 'W' };

    // Second shortcut: adds / removes the foreground app in the exclusion list. Win + Ctrl + Alt + X.
    Hotkey m_excludeHotkey{ .win = true, .ctrl = true, .shift = false, .alt = true, .key = 'X' };

    static constexpr wchar_t JSON_KEY_PROPERTIES[] = L"properties";
    static constexpr wchar_t JSON_KEY_EXCLUDE_APP_SHORTCUT[] = L"exclude_app_shortcut";
    static constexpr wchar_t JSON_KEY_ACTIVATION_SHORTCUT[] = L"activation_shortcut";

    void parse_hotkey(PowerToysSettings::PowerToyValues& values)
    {
        try
        {
            auto object = values.get_raw_json().GetNamedObject(JSON_KEY_PROPERTIES).GetNamedObject(JSON_KEY_ACTIVATION_SHORTCUT);
            auto hotkey = PowerToysSettings::HotkeyObject::from_json(object);
            m_hotkey.win = hotkey.win_pressed();
            m_hotkey.ctrl = hotkey.ctrl_pressed();
            m_hotkey.shift = hotkey.shift_pressed();
            m_hotkey.alt = hotkey.alt_pressed();
            m_hotkey.key = static_cast<unsigned char>(hotkey.get_code());
        }
        catch (...)
        {
            Logger::warn("[ScrollAcceleration] Failed to read the activation shortcut, using the current one.");
        }

        try
        {
            auto object = values.get_raw_json().GetNamedObject(JSON_KEY_PROPERTIES).GetNamedObject(JSON_KEY_EXCLUDE_APP_SHORTCUT);
            auto hotkey = PowerToysSettings::HotkeyObject::from_json(object);
            m_excludeHotkey.win = hotkey.win_pressed();
            m_excludeHotkey.ctrl = hotkey.ctrl_pressed();
            m_excludeHotkey.shift = hotkey.shift_pressed();
            m_excludeHotkey.alt = hotkey.alt_pressed();
            m_excludeHotkey.key = static_cast<unsigned char>(hotkey.get_code());
        }
        catch (...)
        {
            Logger::warn("[ScrollAcceleration] Failed to read the exclude app shortcut, using the current one.");
        }
    }

    void init_settings()
    {
        try
        {
            auto values = PowerToysSettings::PowerToyValues::load_from_settings_file(L"ScrollAcceleration");
            parse_hotkey(values);
        }
        catch (...)
        {
            Logger::warn("[ScrollAcceleration] No settings file yet, using the default activation shortcut.");
        }
    }

public:
    ScrollAccelerationInterface()
    {
        LoggerHelpers::init_logger(L"ScrollAcceleration", L"ModuleInterface", LogSettings::scrollAccelerationLoggerName);
        m_reload_settings_event_handle = CreateDefaultEvent(CommonSharedConstants::SCROLLACCELERATION_REFRESH_SETTINGS_EVENT);
        m_exit_event_handle = CreateDefaultEvent(CommonSharedConstants::SCROLLACCELERATION_EXIT_EVENT);
        m_toggle_event_handle = CreateDefaultEvent(CommonSharedConstants::SCROLLACCELERATION_TOGGLE_EVENT);
        m_exclude_event_handle = CreateDefaultEvent(CommonSharedConstants::SCROLLACCELERATION_EXCLUDE_APP_EVENT);
        init_settings();
    }

    virtual const wchar_t* get_key() override
    {
        return L"ScrollAcceleration";
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
        return powertoys_gpo::getConfiguredScrollAccelerationEnabledValue();
    }

    virtual bool get_config(wchar_t* buffer, int* buffer_size) override
    {
        HINSTANCE hinstance = reinterpret_cast<HINSTANCE>(&__ImageBase);

        PowerToysSettings::Settings settings(hinstance, get_name());
        settings.set_description(MODULE_DESC);
        settings.set_overview_link(L"https://aka.ms/powertoys");

        PowerToysSettings::HotkeyObject hotkey_object = PowerToysSettings::HotkeyObject::from_settings(
            m_hotkey.win, m_hotkey.ctrl, m_hotkey.alt, m_hotkey.shift, m_hotkey.key);
        settings.add_hotkey(JSON_KEY_ACTIVATION_SHORTCUT, L"Activation shortcut", hotkey_object);

        PowerToysSettings::HotkeyObject exclude_object = PowerToysSettings::HotkeyObject::from_settings(
            m_excludeHotkey.win, m_excludeHotkey.ctrl, m_excludeHotkey.alt, m_excludeHotkey.shift, m_excludeHotkey.key);
        settings.add_hotkey(JSON_KEY_EXCLUDE_APP_SHORTCUT, L"Exclude current app shortcut", exclude_object);

        return settings.serialize_to_buffer(buffer, buffer_size);
    }

    virtual void set_config(const wchar_t* config) override
    {
        try
        {
            auto values = PowerToysSettings::PowerToyValues::from_json_string(config, get_key());
            values.save_to_settings_file();
            parse_hotkey(values);

            // Signal the ScrollAcceleration process to reload settings
            if (m_reload_settings_event_handle)
            {
                SetEvent(m_reload_settings_event_handle);
            }
        }
        catch (const std::exception&)
        {
            Logger::error("[ScrollAcceleration] set_config: Failed to parse or apply config.");
        }
    }

    virtual void enable()
    {
        Logger::info(L"Enabling ScrollAcceleration module...");

        if (m_process && WaitForSingleObject(m_process, 0) == WAIT_TIMEOUT)
        {
            m_enabled = true;
            Logger::debug(L"ScrollAcceleration process already running.");
            return;
        }

        if (m_process)
        {
            CloseHandle(m_process);
            m_process = nullptr;
        }

        m_enabled = false;

        unsigned long powertoys_pid = GetCurrentProcessId();
        std::wstring args = std::to_wstring(powertoys_pid);
        std::wstring exe_name = L"PowerToys.ScrollAcceleration.exe";

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
                L"Failed to locate ScrollAcceleration executable named '{}' at location '{}'",
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
            Logger::error(L"Failed to launch ScrollAcceleration process. {}", get_last_error_or_default(GetLastError()));
            return;
        }

        Logger::info(L"ScrollAcceleration process launched successfully (PID: {}).", pi.dwProcessId);
        m_process = pi.hProcess;
        m_enabled = true;
        CloseHandle(pi.hThread);
    }

    virtual void disable()
    {
        Logger::info("ScrollAcceleration disabling");
        m_enabled = false;

        if (m_exit_event_handle)
        {
            SetEvent(m_exit_event_handle);
        }

        if (m_process)
        {
            constexpr DWORD timeout_ms = 1500;
            DWORD result = WaitForSingleObject(m_process, timeout_ms);

            if (result == WAIT_TIMEOUT)
            {
                Logger::warn("ScrollAcceleration: Process didn't exit in time. Forcing termination.");
                TerminateProcess(m_process, 0);
            }

            CloseHandle(m_process);
            m_process = nullptr;
        }

    }

    virtual bool is_enabled() override
    {
        return m_enabled;
    }

    virtual bool is_enabled_by_default() const override
    {
        return false;
    }

    virtual size_t get_hotkeys(Hotkey* buffer, size_t buffer_size) override
    {
        if (buffer && buffer_size >= 2)
        {
            buffer[0] = m_hotkey;
            buffer[1] = m_excludeHotkey;
        }
        return 2;
    }

    virtual bool on_hotkey(size_t hotkeyId) override
    {
        if (!m_enabled)
        {
            return false;
        }

        // The process that owns the hook does the actual work.
        if (hotkeyId == 0 && m_toggle_event_handle)
        {
            return SetEvent(m_toggle_event_handle) != FALSE; // pause / resume
        }

        if (hotkeyId == 1 && m_exclude_event_handle)
        {
            return SetEvent(m_exclude_event_handle) != FALSE; // exclude / include the foreground app
        }

        return false;
    }
};

extern "C" __declspec(dllexport) PowertoyModuleIface* __cdecl powertoy_create()
{
    return new ScrollAccelerationInterface();
}
