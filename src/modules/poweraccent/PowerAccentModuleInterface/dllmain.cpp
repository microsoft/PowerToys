#include "pch.h"
#include <interface/powertoy_module_interface.h>
#include <common/SettingsAPI/settings_objects.h>
#include <common/interop/shared_constants.h>
#include "trace.h"
#include "resource.h"
#include "PowerAccentConstants.h"
#include <common/logger/logger.h>
#include <common/SettingsAPI/settings_helpers.h>
#include <mmsystem.h>

#include <common/utils/elevation.h>
#include <common/utils/process_path.h>
#include <common/utils/resources.h>
#include <common/utils/os-detect.h>
#include <common/utils/logger_helper.h>
#include <common/utils/winapi_error.h>

#include <filesystem>
#include <set>

namespace
{
    const wchar_t JSON_KEY_PROPERTIES[] = L"properties";
    const wchar_t JSON_KEY_SOUND[] = L"hotkey_sound";
    const wchar_t JSON_KEY_ENABLED[] = L"enabled";
    const wchar_t JSON_KEY_WIN[] = L"win";
    const wchar_t JSON_KEY_ALT[] = L"alt";
    const wchar_t JSON_KEY_CTRL[] = L"ctrl";
    const wchar_t JSON_KEY_SHIFT[] = L"shift";
    const wchar_t JSON_KEY_CODE[] = L"code";
    const wchar_t JSON_KEY_HOTKEY[] = L"toggle_hotkey";
    const wchar_t JSON_KEY_VALUE[] = L"value";
}

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

const static wchar_t* MODULE_NAME = L"QuickAccent";
const static wchar_t* MODULE_DESC = L"A module that keeps your computer QuickAccent on-demand.";

class PowerAccent : public PowertoyModuleIface
{
    std::wstring app_name;
    std::wstring app_key;

private:
    bool m_enabled = false;
    Hotkey m_hotkey;
    bool m_hotkey_sound = false;
    PROCESS_INFORMATION p_info = {};

    bool is_process_running()
    {
        return WaitForSingleObject(p_info.hProcess, 0) == WAIT_TIMEOUT;
    }

    void launch_process()
    {
        Logger::trace(L"Launching PowerToys QuickAccent process");
        unsigned long powertoys_pid = GetCurrentProcessId();

        std::wstring executable_args = L"" + std::to_wstring(powertoys_pid);
        std::wstring application_path = L"PowerToys.PowerAccent.exe";
        std::wstring full_command_path = application_path + L" " + executable_args.data();
        Logger::trace(L"PowerToys QuickAccent launching: " + full_command_path);

        STARTUPINFO info = { sizeof(info) };

        if (!CreateProcess(application_path.c_str(), full_command_path.data(), NULL, NULL, true, NULL, NULL, NULL, &info, &p_info))
        {
            DWORD error = GetLastError();
            std::wstring message = L"PowerToys QuickAccent failed to start with error: ";
            message += std::to_wstring(error);
            Logger::error(message);
        }
    }

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
            // Error while loading from the settings file. Let default values stay as they are.
        }
    }

    void set_state_json(bool is_enabled)
    {
        try
        {
            // Parse the input JSON string.
            json::JsonObject settings = PTSettingsHelper::load_general_settings();

            settings.GetNamedObject(JSON_KEY_ENABLED).SetNamedValue(get_key(), json::value(is_enabled));
            // If you don't need to do any custom processing of the settings, proceed
            // to persists the values.
            PTSettingsHelper::save_general_settings(settings);
        }
        catch (std::exception&)
        {
            // Improper JSON.
        }
    }

    void parse_hotkey(PowerToysSettings::PowerToyValues& settings)
    {
        auto settingsObject = settings.get_raw_json();
        if (settingsObject.GetView().Size())
        {
            try
            {
                auto jsonHotkeyObject = settingsObject.GetNamedObject(JSON_KEY_PROPERTIES)
                    .GetNamedObject(JSON_KEY_HOTKEY)
                    .GetNamedObject(JSON_KEY_VALUE);
                m_hotkey.win = jsonHotkeyObject.GetNamedBoolean(JSON_KEY_WIN);
                m_hotkey.alt = jsonHotkeyObject.GetNamedBoolean(JSON_KEY_ALT);
                m_hotkey.shift = jsonHotkeyObject.GetNamedBoolean(JSON_KEY_SHIFT);
                m_hotkey.ctrl = jsonHotkeyObject.GetNamedBoolean(JSON_KEY_CTRL);
                m_hotkey.key = static_cast<unsigned char>(jsonHotkeyObject.GetNamedNumber(JSON_KEY_CODE));

                // disabled because of some weird UI problem
                // m_hotkey_sound = settingsObject.GetNamedObject(JSON_KEY_PROPERTIES)
                //                      .GetNamedObject(JSON_KEY_SOUND)
                //                      .GetBoolean();
                m_hotkey_sound = false;
            }
            catch (...)
            {
                Logger::error("Failed to initialize QuickAccent start shortcut");
            }
        }
        else
        {
            Logger::info("QuickAccent settings are empty");
        }

        if (!m_hotkey.key)
        {
            Logger::info("QuickAccent is going to use default shortcut");
            m_hotkey.win = true;
            m_hotkey.alt = false;
            m_hotkey.shift = false;
            m_hotkey.ctrl = false;
            m_hotkey.key = 0x2D; // the Insert key
        }
    }

public:
    PowerAccent()
    {
        app_name = MODULE_NAME;
        app_key = PowerAccentConstants::ModuleKey;
        LoggerHelpers::init_logger(app_key, L"ModuleInterface", "QuickAccent");
        init_settings();

        Logger::info("Launcher object is constructing");
    };

    virtual void destroy() override
    {
        delete this;
    }

    virtual const wchar_t* get_name() override
    {
        return MODULE_NAME;
    }

    virtual bool get_config(wchar_t* buffer, int* buffer_size) override
    {
        HINSTANCE hinstance = reinterpret_cast<HINSTANCE>(&__ImageBase);

        PowerToysSettings::Settings settings(hinstance, get_name());
        settings.set_description(MODULE_DESC);

        return settings.serialize_to_buffer(buffer, buffer_size);
    }

    virtual const wchar_t* get_key() override
    {
        return app_key.c_str();
    }

    // Return the configured status for the gpo policy for the module
    virtual powertoys_gpo::gpo_rule_configured_t gpo_policy_enabled_configuration() override
    {
        return powertoys_gpo::getConfiguredQuickAccentEnabledValue();
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
            // to persists the values.
            values.save_to_settings_file();
        }
        catch (std::exception&)
        {
            // Improper JSON.
        }
    }

    virtual bool on_hotkey(size_t /*hotkeyId*/) override
    {
        if (m_enabled)
        {
            disable();
            if (m_hotkey_sound)
                PlaySound(TEXT("Media\\Windows Notify Messaging.wav"), NULL, SND_FILENAME | SND_ASYNC);
        }
        else
        {
            enable();
            if (m_hotkey_sound)
                PlaySound(TEXT("Media\\Windows Notify Email.wav"), NULL, SND_FILENAME | SND_ASYNC);
        }
        set_state_json(m_enabled);
        return true;
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

    virtual void enable()
    {
        launch_process();
        m_enabled = true;
        Trace::EnablePowerAccent(true);
    };



    virtual void disable()
    {
        if (m_enabled)
        {
            Logger::trace(L"Disabling QuickAccent... {}", m_enabled);

            auto exitEvent = CreateEvent(nullptr, false, false, CommonSharedConstants::POWERACCENT_EXIT_EVENT);
            if (!exitEvent)
            {
                Logger::warn(L"Failed to create exit event for PowerToys QuickAccent. {}", get_last_error_or_default(GetLastError()));
            }
            else
            {
                Logger::trace(L"Signaled exit event for PowerToys QuickAccent.");
                if (!SetEvent(exitEvent))
                {
                    Logger::warn(L"Failed to signal exit event for PowerToys QuickAccent. {}", get_last_error_or_default(GetLastError()));

                    // For some reason, we couldn't process the signal correctly, so we still
                    // need to terminate the PowerAccent process.
                    TerminateProcess(p_info.hProcess, 1);
                }

                ResetEvent(exitEvent);
                CloseHandle(exitEvent);
                CloseHandle(p_info.hProcess);
            }
        }

        m_enabled = false;
        Trace::EnablePowerAccent(false);
    }

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
    return new PowerAccent();
}