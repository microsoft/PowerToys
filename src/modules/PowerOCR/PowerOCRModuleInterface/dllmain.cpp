// dllmain.cpp : Defines the entry point for the DLL application.
#include "pch.h"

#include <interface/powertoy_module_interface.h>
#include "trace.h"
#include "Generated Files/resource.h"
#include <common/logger/logger.h>
#include <common/SettingsAPI/settings_objects.h>
#include <common/utils/resources.h>

#include <PowerOCR/PowerOCRModuleInterface/PowerOcrConstants.h>
#include <common/interop/shared_constants.h>
#include <common/utils/logger_helper.h>
#include <common/utils/winapi_error.h>
#include <common/utils/package.h>

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
        break;
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

struct ModuleSettings
{
} g_settings;

class PowerOCR : public PowertoyModuleIface
{
private:
    bool m_enabled = false;

    std::wstring app_name;

    //contains the non localized key of the powertoy
    std::wstring app_key;

    HANDLE m_hProcess;

    // Time to wait for process to close after sending WM_CLOSE signal
    static const int MAX_WAIT_MILLISEC = 10000;

    Hotkey m_hotkey;

    // Handle to event used to invoke PowerOCR
    HANDLE m_hInvokeEvent;
    // Handle to event used to terminate PowerOCR
    HANDLE m_hTerminateEvent;

    void parse_hotkey(PowerToysSettings::PowerToyValues& settings)
    {
        auto settingsObject = settings.get_raw_json();
        if (settingsObject.GetView().Size())
        {
            try
            {
                auto jsonHotkeyObject = settingsObject.GetNamedObject(JSON_KEY_PROPERTIES).GetNamedObject(JSON_KEY_ACTIVATION_SHORTCUT);
                m_hotkey.win = jsonHotkeyObject.GetNamedBoolean(JSON_KEY_WIN);
                m_hotkey.alt = jsonHotkeyObject.GetNamedBoolean(JSON_KEY_ALT);
                m_hotkey.shift = jsonHotkeyObject.GetNamedBoolean(JSON_KEY_SHIFT);
                m_hotkey.ctrl = jsonHotkeyObject.GetNamedBoolean(JSON_KEY_CTRL);
                m_hotkey.key = static_cast<unsigned char>(jsonHotkeyObject.GetNamedNumber(JSON_KEY_CODE));
            }
            catch (...)
            {
                Logger::error("Failed to initialize TextExtractor start shortcut");
            }
        }
        else
        {
            Logger::info("TextExtractor settings are empty");
        }

        if (!m_hotkey.key)
        {
            Logger::info("TextExtractor is going to use default shortcut");
            m_hotkey.win = true;
            m_hotkey.alt = false;
            m_hotkey.shift = true;
            m_hotkey.ctrl = false;
            m_hotkey.key = 'T';
        }
    }

    bool is_process_running()
    {
        return WaitForSingleObject(m_hProcess, 0) == WAIT_TIMEOUT;
    }

    void launch_process()
    {
        Logger::trace(L"Starting TextExtractor process");
        unsigned long powertoys_pid = GetCurrentProcessId();

        std::wstring executable_args = L"";
        executable_args.append(std::to_wstring(powertoys_pid));

        SHELLEXECUTEINFOW sei{ sizeof(sei) };
        sei.fMask = { SEE_MASK_NOCLOSEPROCESS | SEE_MASK_FLAG_NO_UI };
        sei.lpFile = L"shell:AppsFolder\\Microsoft.PowerToys.SparseApp_djwsxzxb4ksa8!PowerToys.OCR";;
        sei.nShow = SW_SHOWNORMAL;
        sei.lpParameters = executable_args.data();
        if (ShellExecuteExW(&sei))
        {
            Logger::trace("Successfully started the TextExtractor process");
        }
        else
        {
            Logger::error(L"TextExtractor failed to start. {}", get_last_error_or_default(GetLastError()));
        }

        m_hProcess = sei.hProcess;
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
    PowerOCR()
    {
        app_name = GET_RESOURCE_STRING(IDS_TEXTEXTRACTOR_NAME);
        app_key = PowerOcrConstants::ModuleKey;
        LoggerHelpers::init_logger(app_key, L"ModuleInterface", "TextExtractor");
        m_hInvokeEvent = CreateDefaultEvent(CommonSharedConstants::SHOW_POWEROCR_SHARED_EVENT);
        m_hTerminateEvent = CreateDefaultEvent(CommonSharedConstants::TERMINATE_POWEROCR_SHARED_EVENT);
        init_settings();
    }

    ~PowerOCR()
    {
        if (m_enabled)
        {
        }
        m_enabled = false;
    }

    // Destroy the powertoy and free memory
    virtual void destroy() override
    {
        Logger::trace("TextExtractor::destroy()");
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
        return powertoys_gpo::getConfiguredTextExtractorEnabledValue();
    }

    virtual bool get_config(wchar_t* buffer, int* buffer_size) override
    {
        HINSTANCE hinstance = reinterpret_cast<HINSTANCE>(&__ImageBase);

        // Create a Settings object.
        PowerToysSettings::Settings settings(hinstance, get_name());
        settings.set_description(GET_RESOURCE_STRING(IDS_TEXTEXTRACTOR_SETTINGS_DESC));

        settings.set_overview_link(L"https://aka.ms/PowerToysOverview_TextExtractor");

        return settings.serialize_to_buffer(buffer, buffer_size);
    }

    virtual void call_custom_action(const wchar_t* /*action*/) override
    {
    }

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
            // Otherwise call a custom function to process the settings before saving them to disk:
            // save_settings();
        }
        catch (std::exception&)
        {
            // Improper JSON.
        }
    }

    virtual void enable()
    {
        Logger::trace("TextExtractor::enable()");
        ResetEvent(m_hInvokeEvent);
        launch_process();
        m_enabled = true;
        Trace::EnablePowerOCR(true);
    };

    virtual void disable()
    {
        Logger::trace("TextExtractor::disable()");
        if (m_enabled)
        {
            ResetEvent(m_hInvokeEvent);
            SetEvent(m_hTerminateEvent);
            WaitForSingleObject(m_hProcess, 1500);
            TerminateProcess(m_hProcess, 1);
        }

        m_enabled = false;
        Trace::EnablePowerOCR(false);
    }

    virtual bool on_hotkey(size_t /*hotkeyId*/) override
    {
        if (m_enabled)
        {
            Logger::trace(L"TextExtractor hotkey pressed");
            if (!is_process_running())
            {
                launch_process();
            }

            SetEvent(m_hInvokeEvent);
            return true;
        }

        return false;
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

    virtual bool is_enabled() override
    {
        return m_enabled;
    }

    // Returns whether the PowerToys should be enabled by default
    virtual bool is_enabled_by_default() const override
    {
        // disabled by default for Windows 11 and enabled by default on Windows 10
        return !package::IsWin11OrGreater();
    }
};

extern "C" __declspec(dllexport) PowertoyModuleIface* __cdecl powertoy_create()
{
    return new PowerOCR();
}
