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
#include <common/interop/shared_constants.h>

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

namespace
{
    const wchar_t JSON_KEY_PROPERTIES[] = L"properties";
    const wchar_t JSON_KEY_WIN[] = L"win";
    const wchar_t JSON_KEY_ALT[] = L"alt";
    const wchar_t JSON_KEY_CTRL[] = L"ctrl";
    const wchar_t JSON_KEY_SHIFT[] = L"shift";
    const wchar_t JSON_KEY_CODE[] = L"code";
    const wchar_t JSON_KEY_ACTIVATION_SHORTCUT[] = L"ToggleShortcut";
    const wchar_t JSON_KEY_EDITOR_SHORTCUT[] = L"EditorShortcut";
    const wchar_t JSON_KEY_USE_NEW_EDITOR[] = L"useNewEditor";
}

// Implement the PowerToy Module Interface and all the required methods.
class KeyboardManager : public PowertoyModuleIface
{
private:
    // The PowerToy state.
    bool m_enabled = false;
    bool m_active = false;

    // The PowerToy name that will be shown in the settings.
    const std::wstring app_name = GET_RESOURCE_STRING(IDS_KEYBOARDMANAGER);

    //contains the non localized key of the powertoy
    std::wstring app_key = KeyboardManagerConstants::ModuleName;

    // Hotkey for toggling the module
    Hotkey m_hotkey = { .key = 0 };

    // Hotkey for opening the editor
    Hotkey m_editorHotkey = { .key = 0 };

    // Whether to use the new WinUI3 editor
    bool m_useNewEditor = false;

    ULONGLONG m_lastHotkeyToggleTime = 0;

    HANDLE m_hProcess = nullptr;
    HANDLE m_hEditorProcess = nullptr;

    HANDLE m_hTerminateEngineEvent = nullptr;

    void refresh_process_state()
    {
        if (m_hProcess && WaitForSingleObject(m_hProcess, 0) != WAIT_TIMEOUT)
        {
            CloseHandle(m_hProcess);
            m_hProcess = nullptr;
            m_active = false;
        }
    }

    bool start_engine()
    {
        refresh_process_state();
        if (m_hProcess)
        {
            m_active = true;
            return true;
        }

        if (!m_hTerminateEngineEvent)
        {
            Logger::error(L"Cannot start keyboard manager engine because terminate event is not available");
            m_active = false;
            return false;
        }

        unsigned long powertoys_pid = GetCurrentProcessId();
        std::wstring executable_args = std::to_wstring(powertoys_pid);

        SHELLEXECUTEINFOW sei{ sizeof(sei) };
        sei.fMask = { SEE_MASK_NOCLOSEPROCESS | SEE_MASK_FLAG_NO_UI };
        sei.lpFile = L"KeyboardManagerEngine\\PowerToys.KeyboardManagerEngine.exe";
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

            m_active = false;
            return false;
        }

        m_hProcess = sei.hProcess;
        if (m_hProcess)
        {
            SetPriorityClass(m_hProcess, REALTIME_PRIORITY_CLASS);
            m_active = true;
            return true;
        }

        m_active = false;
        return false;
    }

    void stop_engine()
    {
        refresh_process_state();
        if (!m_hProcess)
        {
            m_active = false;
            return;
        }

        SetEvent(m_hTerminateEngineEvent);
        auto waitResult = WaitForSingleObject(m_hProcess, 1500);
        if (waitResult == WAIT_TIMEOUT)
        {
            TerminateProcess(m_hProcess, 0);
            WaitForSingleObject(m_hProcess, 500);
        }

        CloseHandle(m_hProcess);
        m_hProcess = nullptr;
        ResetEvent(m_hTerminateEngineEvent);
        m_active = false;
    }

    void parse_hotkey(PowerToysSettings::PowerToyValues& settings)
    {
        auto settingsObject = settings.get_raw_json();
        if (settingsObject.GetView().Size())
        {
            try
            {
                auto jsonHotkeyObject = settingsObject.GetNamedObject(JSON_KEY_PROPERTIES)
                                            .GetNamedObject(JSON_KEY_ACTIVATION_SHORTCUT);
                m_hotkey.win = jsonHotkeyObject.GetNamedBoolean(JSON_KEY_WIN);
                m_hotkey.alt = jsonHotkeyObject.GetNamedBoolean(JSON_KEY_ALT);
                m_hotkey.shift = jsonHotkeyObject.GetNamedBoolean(JSON_KEY_SHIFT);
                m_hotkey.ctrl = jsonHotkeyObject.GetNamedBoolean(JSON_KEY_CTRL);
                m_hotkey.key = static_cast<unsigned char>(jsonHotkeyObject.GetNamedNumber(JSON_KEY_CODE));
            }
            catch (...)
            {
                Logger::error("Failed to initialize Keyboard Manager toggle shortcut");
            }
        }

        if (!m_hotkey.key)
        {
            // Set default: Win+Shift+K
            m_hotkey.win = true;
            m_hotkey.shift = true;
            m_hotkey.ctrl = false;
            m_hotkey.alt = false;
            m_hotkey.key = 'K';
        }

        // Parse editor shortcut
        if (settingsObject.GetView().Size())
        {
            try
            {
                auto jsonEditorHotkeyObject = settingsObject.GetNamedObject(JSON_KEY_PROPERTIES)
                                                  .GetNamedObject(JSON_KEY_EDITOR_SHORTCUT);
                m_editorHotkey.win = jsonEditorHotkeyObject.GetNamedBoolean(JSON_KEY_WIN);
                m_editorHotkey.alt = jsonEditorHotkeyObject.GetNamedBoolean(JSON_KEY_ALT);
                m_editorHotkey.shift = jsonEditorHotkeyObject.GetNamedBoolean(JSON_KEY_SHIFT);
                m_editorHotkey.ctrl = jsonEditorHotkeyObject.GetNamedBoolean(JSON_KEY_CTRL);
                m_editorHotkey.key = static_cast<unsigned char>(jsonEditorHotkeyObject.GetNamedNumber(JSON_KEY_CODE));
            }
            catch (...)
            {
                Logger::error("Failed to initialize Keyboard Manager editor shortcut");
            }
        }

        if (!m_editorHotkey.key)
        {
            // Set default: Win+Shift+Q
            m_editorHotkey.win = true;
            m_editorHotkey.shift = true;
            m_editorHotkey.ctrl = false;
            m_editorHotkey.alt = false;
            m_editorHotkey.key = 'Q';
        }

        // Parse useNewEditor setting
        if (settingsObject.GetView().Size())
        {
            try
            {
                auto propertiesObject = settingsObject.GetNamedObject(JSON_KEY_PROPERTIES);
                m_useNewEditor = propertiesObject.GetNamedBoolean(JSON_KEY_USE_NEW_EDITOR, false);
            }
            catch (...)
            {
                Logger::warn("Failed to parse useNewEditor setting, defaulting to false");
            }
        }
    }

    // Load the settings file.
    void init_settings()
    {
        try
        {
            // Load and parse the settings file for this PowerToy.
            PowerToysSettings::PowerToyValues settings =
                PowerToysSettings::PowerToyValues::load_from_settings_file(get_key());
            parse_hotkey(settings);
        }
        catch (std::exception&)
        {
            Logger::warn(L"An exception occurred while loading the settings file");
            // Error while loading from the settings file. Let default values stay as they are.
        }
    }

public:
    // Constructor
    KeyboardManager()
    {
        LoggerHelpers::init_logger(KeyboardManagerConstants::ModuleName, L"ModuleInterface", LogSettings::keyboardManagerLoggerName);

        std::filesystem::path oldLogPath(PTSettingsHelper::get_module_save_folder_location(app_key));
        oldLogPath.append("Logs");
        LoggerHelpers::delete_old_log_folder(oldLogPath);

        m_hTerminateEngineEvent = CreateDefaultEvent(CommonSharedConstants::TERMINATE_KBM_SHARED_EVENT);
        if (!m_hTerminateEngineEvent)
        {
            Logger::error(L"Failed to create terminate Engine event");
            auto message = get_last_error_message(GetLastError());
            if (message.has_value())
            {
                Logger::error(message.value());
            }
        }

        init_settings();
    };

    ~KeyboardManager()
    {
        stop_engine();
        if (m_hTerminateEngineEvent)
        {
            CloseHandle(m_hTerminateEngineEvent);
            m_hTerminateEngineEvent = nullptr;
        }
        if (m_hEditorProcess)
        {
            CloseHandle(m_hEditorProcess);
            m_hEditorProcess = nullptr;
        }
    }

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
            parse_hotkey(values);

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
        start_engine();
    }

    // Disable the powertoy
    virtual void disable()
    {
        m_enabled = false;
        // Log telemetry
        Trace::EnableKeyboardManager(false);
        stop_engine();
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

    // Return the invocation hotkeys for toggling and opening the editor
    virtual size_t get_hotkeys(Hotkey* hotkeys, size_t buffer_size) override
    {
        size_t count = 0;

        // Hotkey 0: toggle engine
        if (m_hotkey.key)
        {
            if (hotkeys && buffer_size > count)
            {
                hotkeys[count] = m_hotkey;
            }
            count++;
        }

        // Hotkey 1: open editor (only when using new editor)
        if (m_useNewEditor && m_editorHotkey.key)
        {
            if (hotkeys && buffer_size > count)
            {
                hotkeys[count] = m_editorHotkey;
            }
            count++;
        }

        return count;
    }

    bool launch_editor()
    {
        // Check if editor is already running
        if (m_hEditorProcess)
        {
            if (WaitForSingleObject(m_hEditorProcess, 0) == WAIT_TIMEOUT)
            {
                // Editor still running, bring it to front
                DWORD editorPid = GetProcessId(m_hEditorProcess);
                if (editorPid)
                {
                    AllowSetForegroundWindow(editorPid);
                }
                return true;
            }
            else
            {
                CloseHandle(m_hEditorProcess);
                m_hEditorProcess = nullptr;
            }
        }

        unsigned long powertoys_pid = GetCurrentProcessId();
        std::wstring executable_args = std::to_wstring(powertoys_pid);

        SHELLEXECUTEINFOW sei{ sizeof(sei) };
        sei.fMask = { SEE_MASK_NOCLOSEPROCESS | SEE_MASK_FLAG_NO_UI };
        sei.lpFile = L"WinUI3Apps\\PowerToys.KeyboardManagerEditorUI.exe";
        sei.nShow = SW_SHOWNORMAL;
        sei.lpParameters = executable_args.data();
        if (ShellExecuteExW(&sei) == false)
        {
            Logger::error(L"Failed to start new keyboard manager editor");
            auto message = get_last_error_message(GetLastError());
            if (message.has_value())
            {
                Logger::error(message.value());
            }
            return false;
        }

        m_hEditorProcess = sei.hProcess;
        return m_hEditorProcess != nullptr;
    }

    // Process the hotkey event
    virtual bool on_hotkey(size_t hotkeyId) override
    {
        if (!m_enabled)
        {
            return false;
        }

        constexpr ULONGLONG hotkeyToggleDebounceMs = 500;
        const auto now = GetTickCount64();
        if (now - m_lastHotkeyToggleTime < hotkeyToggleDebounceMs)
        {
            return true;
        }
        m_lastHotkeyToggleTime = now;

        if (hotkeyId == 0)
        {
            // Toggle engine on/off
            refresh_process_state();
            if (m_active)
            {
                stop_engine();
            }
            else
            {
                start_engine();
            }
        }
        else if (hotkeyId == 1)
        {
            // Open the new editor (only in new editor mode)
            launch_editor();
        }

        return true;
    }
};

extern "C" __declspec(dllexport) PowertoyModuleIface* __cdecl powertoy_create()
{
    return new KeyboardManager();
}
