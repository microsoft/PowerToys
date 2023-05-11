#include "pch.h"
#include <interface/powertoy_module_interface.h>
#include <common/logger/logger.h>
#include <common/SettingsAPI/settings_objects.h>
#include "trace.h"
#include <common/utils/winapi_error.h>
#include <filesystem>
#include <common/interop/shared_constants.h>

extern "C" IMAGE_DOS_HEADER __ImageBase;

BOOL APIENTRY DllMain(HMODULE /*hModule*/, 
                      DWORD ul_reason_for_call, 
                      LPVOID /*lpReserved*/)
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
    const wchar_t JSON_KEY_ACTIVATION_SHORTCUT[] = L"ActivationShortcut";
}

// The PowerToy name that will be shown in the settings.
const static wchar_t* MODULE_NAME = L"Peek";
// Add a description that will we shown in the module settings page.
const static wchar_t* MODULE_DESC = L"A module that previews an image file.";

// Implement the PowerToy Module Interface and all the required methods.
class Peek : public PowertoyModuleIface
{
private:
    // The PowerToy state.
    bool m_enabled = false;

    Hotkey m_hotkey;

    HANDLE m_hProcess;

    HANDLE m_hInvokeEvent;

    // Load the settings file.
    void init_settings()
    {
        try
        {
            // Load and parse the settings file for this PowerToy.
            PowerToysSettings::PowerToyValues settings =
                PowerToysSettings::PowerToyValues::load_from_settings_file(Peek::get_name());

            parse_settings(settings);
        }
        catch (std::exception&)
        {
            // Error while loading from the settings file. Let default values stay as they are.
        }
    }

    void parse_settings(PowerToysSettings::PowerToyValues& settings)
    {
        auto settingsObject = settings.get_raw_json();
        if (settingsObject.GetView().Size())
        {
            try
            {
                auto jsonHotkeyObject = settingsObject.GetNamedObject(JSON_KEY_PROPERTIES).GetNamedObject(JSON_KEY_ACTIVATION_SHORTCUT);
                parse_hotkey(jsonHotkeyObject);
            }
            catch (...)
            {
                Logger::error("Failed to initialize Peek start settings");

                set_default_settings();
            }
        }
        else
        {
            Logger::info("Peek settings are empty");

            set_default_settings();
        }
    }

    void set_default_settings()
    {
        Logger::info("Peek is going to use default settings");
        m_hotkey.win = false;
        m_hotkey.alt = false;
        m_hotkey.shift = false;
        m_hotkey.ctrl = true;
        m_hotkey.key = ' ';
    }

    void parse_hotkey(winrt::Windows::Data::Json::JsonObject& jsonHotkeyObject)
    {
        try
        {
            m_hotkey.win = jsonHotkeyObject.GetNamedBoolean(JSON_KEY_WIN);
            m_hotkey.alt = jsonHotkeyObject.GetNamedBoolean(JSON_KEY_ALT);
            m_hotkey.shift = jsonHotkeyObject.GetNamedBoolean(JSON_KEY_SHIFT);
            m_hotkey.ctrl = jsonHotkeyObject.GetNamedBoolean(JSON_KEY_CTRL);
            m_hotkey.key = static_cast<unsigned char>(jsonHotkeyObject.GetNamedNumber(JSON_KEY_CODE));
        }
        catch (...)
        {
            Logger::error("Failed to initialize Peek start shortcut");
        }

        if (!m_hotkey.key)
        {
            Logger::info("Peek is going to use default shortcut");
            m_hotkey.win = false;
            m_hotkey.alt = false;
            m_hotkey.shift = false;
            m_hotkey.ctrl = true;
            m_hotkey.key = ' ';
        }
    }

    bool is_viewer_running()
    {
        return WaitForSingleObject(m_hProcess, 0) == WAIT_TIMEOUT;
    }

    void launch_process()
    {
        Logger::trace(L"Starting Peek.UI process");

        unsigned long powertoys_pid = GetCurrentProcessId();

        std::wstring executable_args = L"";
        executable_args.append(std::to_wstring(powertoys_pid));

        SHELLEXECUTEINFOW sei{ sizeof(sei) };

        sei.fMask = { SEE_MASK_NOCLOSEPROCESS };
        sei.lpVerb = L"open";
        sei.lpFile = L"modules\\Peek\\Powertoys.Peek.UI.exe";
        sei.nShow = SW_SHOWNORMAL;
        sei.lpParameters = executable_args.data();

        if (ShellExecuteExW(&sei))
        {
            Logger::trace("Successfully started the PeekViewer process");
        }
        else
        {
            Logger::error(L"PeekViewer failed to start. {}", get_last_error_or_default(GetLastError()));
        }

        m_hProcess = sei.hProcess;
    }

public:
    Peek()
    {
        init_settings();

        m_hInvokeEvent = CreateDefaultEvent(CommonSharedConstants::SHOW_PEEK_SHARED_EVENT);
    };

    ~Peek()
    {
        if (m_enabled)
        {
        }
        m_enabled = false;
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

    virtual const wchar_t* get_key() override
    {
        return MODULE_NAME;
    }

    // Return the configured status for the gpo policy for the module
    virtual powertoys_gpo::gpo_rule_configured_t gpo_policy_enabled_configuration() override
    {
        return powertoys_gpo::getConfiguredPeekEnabledValue();
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

    // Called by the runner to pass the updated settings values as a serialized JSON.
    virtual void set_config(const wchar_t* config) override
    {
        try
        {
            // Parse the input JSON string.
            PowerToysSettings::PowerToyValues values =
                PowerToysSettings::PowerToyValues::from_json_string(config, get_key());

            parse_settings(values);

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
        Logger::trace("Peek::enable()");
        ResetEvent(m_hInvokeEvent);
        launch_process();
        m_enabled = true;
        Trace::EnablePeek(true);
    }

    // Disable the powertoy
    virtual void disable()
    {
        Logger::trace("Peek::disable()");
        if (m_enabled)
        {
            ResetEvent(m_hInvokeEvent);
            TerminateProcess(m_hProcess, 1);
        }

        m_enabled = false;
        Trace::EnablePeek(false);
    }

    // Returns if the powertoys is enabled
    virtual bool is_enabled() override
    {
        return m_enabled;
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
        else
        {
            return 0;
        }
    }

    virtual bool on_hotkey(size_t /*hotkeyId*/) override
    {
        if (m_enabled)
        {
            Logger::trace(L"Peek hotkey pressed");
            
            // TODO: fix VK_SPACE DestroyWindow in viewer app
            if (!is_viewer_running())
            {
                launch_process();
            }

            SetEvent(m_hInvokeEvent);

            Trace::PeekInvoked();

            return true;
        }

        return false;
    }

    virtual void send_settings_telemetry() override
    {
        Logger::info("Send settings telemetry");
        Trace::SettingsTelemetry(m_hotkey);
    }
};

extern "C" __declspec(dllexport) PowertoyModuleIface* __cdecl powertoy_create()
{
    return new Peek();
}