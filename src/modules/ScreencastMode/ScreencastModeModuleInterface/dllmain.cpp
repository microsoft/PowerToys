#include "pch.h"
#include <interface/powertoy_module_interface.h>
#include <common/SettingsAPI/settings_objects.h>
#include "trace.h"
#include <string>
#include <common/utils/process_path.h>
#include <common/utils/winapi_error.h>
#include <common/logger/logger.h>
#include <common/utils/logger_helper.h>
#include <common/interop/shared_constants.h>

extern "C" IMAGE_DOS_HEADER __ImageBase;

namespace
{
    const wchar_t JSON_KEY_PROPERTIES[] = L"properties";
    const wchar_t JSON_KEY_WIN[] = L"win";
    const wchar_t JSON_KEY_ALT[] = L"alt";
    const wchar_t JSON_KEY_CTRL[] = L"ctrl";
    const wchar_t JSON_KEY_SHIFT[] = L"shift";
    const wchar_t JSON_KEY_CODE[] = L"code";
    const wchar_t JSON_KEY_HOTKEY[] = L"ScreencastModeShortcut";
    const wchar_t JSON_KEY_VALUE[] = L"value";
}

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

const static wchar_t* MODULE_NAME = L"Screencast Mode";
const static wchar_t* MODULE_KEY = L"ScreencastMode";
const static wchar_t* MODULE_DESC = L"Visualize keystrokes for recordings and presentations.";

// Implement the PowerToy Module Interface and all the required methods.
class ScreencastMode : public PowertoyModuleIface
{
private:
    bool m_enabled = false;
    bool m_overlayVisible = false;
    bool m_firstEnable = true;
    HANDLE m_hProcess = nullptr;
    DWORD m_processPid = 0;
    Hotkey m_hotkey;

    void parse_hotkey(PowerToysSettings::PowerToyValues& settings)
    {
        auto settingsObject = settings.get_raw_json();
        if (settingsObject.GetView().Size())
        {
            try
            {
                auto jsonPropsObject = settingsObject.GetNamedObject(JSON_KEY_PROPERTIES);
                if (jsonPropsObject.HasKey(JSON_KEY_HOTKEY))
                {
                    // The hotkey object is stored directly, not wrapped in a "value" object
                    auto jsonHotkeyObject = jsonPropsObject.GetNamedObject(JSON_KEY_HOTKEY);
                    m_hotkey.win = jsonHotkeyObject.GetNamedBoolean(JSON_KEY_WIN);
                    m_hotkey.alt = jsonHotkeyObject.GetNamedBoolean(JSON_KEY_ALT);
                    m_hotkey.shift = jsonHotkeyObject.GetNamedBoolean(JSON_KEY_SHIFT);
                    m_hotkey.ctrl = jsonHotkeyObject.GetNamedBoolean(JSON_KEY_CTRL);
                    m_hotkey.key = static_cast<unsigned char>(jsonHotkeyObject.GetNamedNumber(JSON_KEY_CODE));

                    Logger::info("ScreencastMode hotkey loaded: win={}, alt={}, ctrl={}, shift={}, key={}",
                        m_hotkey.win, m_hotkey.alt, m_hotkey.ctrl, m_hotkey.shift, m_hotkey.key);
                }
                else
                {
                    Logger::info("ScreencastMode hotkey not found in settings, using defaults");
                    set_default_hotkey();
                }
            }
            catch (const winrt::hresult_error& e)
            {
                Logger::error(L"Failed to parse ScreencastMode hotkey: {}", e.message().c_str());
                set_default_hotkey();
            }
            catch (...)
            {
                Logger::error("Failed to initialize ScreencastMode hotkey (unknown error)");
                set_default_hotkey();
            }
        }
        else
        {
            Logger::info("ScreencastMode settings are empty, using default hotkey");
            set_default_hotkey();
        }
    }

    void set_default_hotkey()
    {
        // Default: Win + Alt + S
        m_hotkey.win = true;
        m_hotkey.alt = true;
        m_hotkey.shift = false;
        m_hotkey.ctrl = false;
        m_hotkey.key = 0x53;
    }

    void init_settings()
    {
        try
        {
            auto settings = PowerToysSettings::PowerToyValues::load_from_settings_file(get_key());
            parse_hotkey(settings);
            settings.save_to_settings_file();
        }
        catch (std::exception& e)
        {
            Logger::error("Failed to load settings file: {}", e.what());
            set_default_hotkey();
        }
    }

    bool is_viewer_running()
    {
        return m_hProcess && WaitForSingleObject(m_hProcess, 0) == WAIT_TIMEOUT;
    }

    void launch_process()
    {
        if (is_viewer_running())
        {
            return;
        }

        Logger::trace(L"Starting ScreencastMode UI process");
        unsigned long powertoys_pid = GetCurrentProcessId();
        std::wstring executable_args = std::to_wstring(powertoys_pid);

        SHELLEXECUTEINFOW sei{ sizeof(sei) };
        sei.fMask = SEE_MASK_NOCLOSEPROCESS | SEE_MASK_FLAG_NO_UI;
        sei.lpFile = L"WinUI3Apps\\PowerToys.ScreencastModeUI.exe";
        sei.nShow = SW_SHOWNORMAL;
        sei.lpParameters = executable_args.data();
        if (ShellExecuteExW(&sei))
        {
            m_hProcess = sei.hProcess;
            m_processPid = GetProcessId(m_hProcess);
            m_overlayVisible = true;
            Logger::trace("Successfully started ScreencastMode UI process");
        }
        else
        {
            Logger::error(L"ScreencastMode UI failed to start. {}", get_last_error_or_default(GetLastError()));
        }
    }

    void terminate_process()
    {
        if (m_hProcess)
        {
            HANDLE hProcess = OpenProcess(SYNCHRONIZE | PROCESS_TERMINATE, FALSE, m_processPid);
            if (hProcess)
            {
                if (WaitForSingleObject(hProcess, 1000) == WAIT_TIMEOUT)
                {
                    TerminateProcess(hProcess, 1);
                }
                CloseHandle(hProcess);
            }
            CloseHandle(m_hProcess);
            m_hProcess = nullptr;
            m_processPid = 0;
            m_overlayVisible = false;
        }
    }

public:
    ScreencastMode()
    {
        LoggerHelpers::init_logger(MODULE_KEY, L"ModuleInterface", LogSettings::screencastModeLoggerName);
        init_settings();
    };

    virtual void destroy() override
    {
        if (m_enabled)
        {
            terminate_process();
        }
        delete this;
    }

    virtual const wchar_t* get_name() override { return MODULE_NAME; }
    virtual const wchar_t* get_key() override { return MODULE_KEY; }

  /*  virtual powertoys_gpo::gpo_rule_configured_t gpo_policy_enabled_configuration() override
    {
        return powertoys_gpo::getConfiguredScreencastModeEnabledValue();
    }*/

    virtual bool get_config(wchar_t* buffer, int* buffer_size) override
    {
        HINSTANCE hinstance = reinterpret_cast<HINSTANCE>(&__ImageBase);
        PowerToysSettings::Settings settings(hinstance, get_name());
        settings.set_description(MODULE_DESC);
        return settings.serialize_to_buffer(buffer, buffer_size);
    }

    virtual void set_config(const wchar_t* config) override
    {
        try
        {
            auto values = PowerToysSettings::PowerToyValues::from_json_string(config, get_key());
            parse_hotkey(values);
            values.save_to_settings_file();
        }
        catch (std::exception&)
        {
        }
    }

    virtual size_t get_hotkeys(Hotkey* hotkeys, size_t buffer_size) override
    {
        if (m_hotkey.key)
        {
            if (hotkeys && buffer_size >= 1)
            {
                hotkeys[0] = m_hotkey;
            }
            return 1;
        }
        return 0;
    }

    virtual bool on_hotkey(size_t /*hotkeyId*/) override
    {
        // Hotkey only works when the module is enabled via settings
        if (!m_enabled)
        {
            return false;
        }

        // Toggle overlay visibility
        Logger::trace(L"ScreencastMode hotkey pressed, toggling overlay visibility");
        if (m_overlayVisible && is_viewer_running())
        {
            Logger::trace(L"Hiding ScreencastMode overlay");
            terminate_process();
        }
        else
        {
            Logger::trace(L"Showing ScreencastMode overlay");
            launch_process();
        }
        return true;
    }

    virtual void enable() override
    {
        Logger::trace(L"ScreencastMode enabled");
        m_enabled = true;

        // Don't show the overlay on powertoys startup
        if (!m_firstEnable)
        {
            launch_process();
        }
        else
        {
            m_firstEnable = false;
        }

        Trace::ScreencastModeEnabled(true);
    }

    virtual void disable() override
    {
        Logger::trace(L"ScreencastMode disabled");
        m_enabled = false;
        terminate_process();
        Trace::ScreencastModeEnabled(false);
    }

    virtual bool is_enabled() override { return m_enabled; }
};

extern "C" __declspec(dllexport) PowertoyModuleIface* __cdecl powertoy_create()
{
    return new ScreencastMode();
}