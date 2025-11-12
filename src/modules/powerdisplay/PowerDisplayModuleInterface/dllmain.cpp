// dllmain.cpp : Defines the entry point for the DLL Application.
#include "pch.h"
#include <interface/powertoy_module_interface.h>
#include <common/SettingsAPI/settings_objects.h>
#include "trace.h"
#include <common/interop/shared_constants.h>
#include <common/utils/string_utils.h>
#include <common/utils/winapi_error.h>
#include <common/utils/logger_helper.h>
#include <common/utils/resources.h>

#include "resource.h"
#include "Constants.h"
#include "PowerDisplayProcessManager.h"

extern "C" IMAGE_DOS_HEADER __ImageBase;

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

const static wchar_t* MODULE_NAME = L"PowerDisplay";
const static wchar_t* MODULE_DESC = L"A utility to manage display brightness and color temperature across multiple monitors.";

namespace
{
    const wchar_t JSON_KEY_PROPERTIES[] = L"properties";
    const wchar_t JSON_KEY_ENABLED[] = L"enabled";
    const wchar_t JSON_KEY_HOTKEY_ENABLED[] = L"hotkey_enabled";
    const wchar_t JSON_KEY_ACTIVATION_SHORTCUT[] = L"ActivationShortcut";
    const wchar_t JSON_KEY_WIN[] = L"win";
    const wchar_t JSON_KEY_ALT[] = L"alt";
    const wchar_t JSON_KEY_CTRL[] = L"ctrl";
    const wchar_t JSON_KEY_SHIFT[] = L"shift";
    const wchar_t JSON_KEY_CODE[] = L"code";
}

class PowerDisplayModule : public PowertoyModuleIface
{
private:
    bool m_enabled = false;
    bool m_hotkey_enabled = false;
    Hotkey m_activation_hotkey = { .win = true, .ctrl = false, .shift = false, .alt = true, .key = 'M' };

    // Process manager for handling PowerDisplay.exe lifecycle and IPC
    PowerDisplayProcessManager m_process_manager;

    void parse_hotkey_settings(PowerToysSettings::PowerToyValues settings)
    {
        auto settingsObject = settings.get_raw_json();
        if (settingsObject.GetView().Size())
        {
            try
            {
                if (settingsObject.HasKey(JSON_KEY_PROPERTIES))
                {
                    auto properties = settingsObject.GetNamedObject(JSON_KEY_PROPERTIES);
                    m_hotkey_enabled = properties.GetNamedBoolean(JSON_KEY_HOTKEY_ENABLED, false);
                }
                else
                {
                    Logger::info("Properties object not found in settings, using defaults");
                    m_hotkey_enabled = false;
                }
            }
            catch (...)
            {
                Logger::info("Failed to parse hotkey settings, using defaults");
                m_hotkey_enabled = false;
            }
        }
        else
        {
            Logger::info("Power Display settings are empty");
            m_hotkey_enabled = false;
        }
    }

    void parse_activation_hotkey(PowerToysSettings::PowerToyValues& settings)
    {
        auto settingsObject = settings.get_raw_json();
        if (settingsObject.GetView().Size())
        {
            try
            {
                if (settingsObject.HasKey(JSON_KEY_PROPERTIES))
                {
                    auto properties = settingsObject.GetNamedObject(JSON_KEY_PROPERTIES);
                    if (properties.HasKey(JSON_KEY_ACTIVATION_SHORTCUT))
                    {
                        auto jsonHotkeyObject = properties.GetNamedObject(JSON_KEY_ACTIVATION_SHORTCUT);
                        m_activation_hotkey.win = jsonHotkeyObject.GetNamedBoolean(JSON_KEY_WIN);
                        m_activation_hotkey.alt = jsonHotkeyObject.GetNamedBoolean(JSON_KEY_ALT);
                        m_activation_hotkey.shift = jsonHotkeyObject.GetNamedBoolean(JSON_KEY_SHIFT);
                        m_activation_hotkey.ctrl = jsonHotkeyObject.GetNamedBoolean(JSON_KEY_CTRL);
                        m_activation_hotkey.key = static_cast<unsigned char>(jsonHotkeyObject.GetNamedNumber(JSON_KEY_CODE));
                        Logger::trace(L"Parsed activation hotkey: Win={} Ctrl={} Alt={} Shift={} Key={}",
                                     m_activation_hotkey.win, m_activation_hotkey.ctrl, m_activation_hotkey.alt,
                                     m_activation_hotkey.shift, m_activation_hotkey.key);
                    }
                    else
                    {
                        Logger::info("ActivationShortcut not found in settings, using default Win+Alt+M");
                    }
                }
            }
            catch (...)
            {
                Logger::error("Failed to parse PowerDisplay activation shortcut, using default Win+Alt+M");
            }
        }
    }

    void init_settings()
    {
        try
        {
            PowerToysSettings::PowerToyValues settings =
                PowerToysSettings::PowerToyValues::load_from_settings_file(get_key());

            parse_hotkey_settings(settings);
            parse_activation_hotkey(settings);
        }
        catch (std::exception&)
        {
            Logger::error("Invalid json when trying to load the Power Display settings json from file.");
        }
    }

public:
    PowerDisplayModule()
    {
        LoggerHelpers::init_logger(MODULE_NAME, L"ModuleInterface", "PowerDisplay");
        Logger::info("Power Display object is constructing");

        init_settings();

        // Note: PowerDisplay.exe will send messages directly to runner via named pipes
        // The runner's message_receiver_thread will handle routing to Settings UI
        // No need to set a callback here - the process manager just manages lifecycle
    }

    ~PowerDisplayModule()
    {
        if (m_enabled)
        {
            m_process_manager.stop();
        }
        m_enabled = false;
    }

    virtual void destroy() override
    {
        Logger::trace("PowerDisplay::destroy()");
        if (m_enabled)
        {
            m_process_manager.stop();
        }
        delete this;
    }

    virtual const wchar_t* get_name() override
    {
        return MODULE_NAME;
    }

    virtual const wchar_t* get_key() override
    {
        return MODULE_NAME;
    }

    virtual powertoys_gpo::gpo_rule_configured_t gpo_policy_enabled_configuration() override
    {
        return powertoys_gpo::gpo_rule_configured_not_configured;
    }

    virtual bool get_config(wchar_t* buffer, int* buffer_size) override
    {
        HINSTANCE hinstance = reinterpret_cast<HINSTANCE>(&__ImageBase);

        PowerToysSettings::Settings settings(hinstance, get_name());
        settings.set_description(MODULE_DESC);

        return settings.serialize_to_buffer(buffer, buffer_size);
    }

    virtual void call_custom_action(const wchar_t* action) override
    {
        try
        {
            PowerToysSettings::CustomActionObject action_object =
                PowerToysSettings::CustomActionObject::from_json_string(action);

            if (action_object.get_name() == L"Launch")
            {
                Logger::trace(L"Launch action received, sending show_window command");
                m_process_manager.send_message_to_powerdisplay(L"{\"action\":\"show_window\"}");
                Trace::ActivatePowerDisplay();
            }
            else if (action_object.get_name() == L"RefreshMonitors")
            {
                Logger::trace(L"RefreshMonitors action received");
                m_process_manager.send_message_to_powerdisplay(L"{\"action\":\"refresh_monitors\"}");
            }
        }
        catch (std::exception&)
        {
            Logger::error(L"Failed to parse action. {}", action);
        }
    }

    virtual void set_config(const wchar_t* config) override
    {
        try
        {
            PowerToysSettings::PowerToyValues values =
                PowerToysSettings::PowerToyValues::from_json_string(config, get_key());

            parse_hotkey_settings(values);
            parse_activation_hotkey(values);
            values.save_to_settings_file();

            // Notify PowerDisplay.exe that settings have been updated (signal only, no config data)
            // PowerDisplay will read the updated settings.json file itself
            m_process_manager.send_message_to_powerdisplay(L"{\"action\":\"settings_updated\"}");
        }
        catch (std::exception&)
        {
            Logger::error(L"Invalid json when trying to parse Power Display settings json.");
        }
    }

    virtual void enable() override
    {
        m_enabled = true;
        Trace::EnablePowerDisplay(true);

        Logger::trace(L"PowerDisplay enabled, starting process manager");
        m_process_manager.start();
    }

    virtual void disable() override
    {
        if (m_enabled)
        {
            Logger::trace(L"Disabling Power Display...");
            m_process_manager.stop();
        }

        m_enabled = false;
        Trace::EnablePowerDisplay(false);
    }

    virtual bool is_enabled() override
    {
        return m_enabled;
    }

    virtual bool on_hotkey(size_t /*hotkeyId*/) override
    {
        if (m_enabled)
        {
            Logger::trace(L"Power Display hotkey pressed");
            // Send toggle window command
            m_process_manager.send_message_to_powerdisplay(L"{\"action\":\"toggle_window\"}");
            return true;
        }

        return false;
    }

    virtual size_t get_hotkeys(Hotkey* hotkeys, size_t buffer_size) override
    {
        if (m_activation_hotkey.key != 0)
        {
            if (hotkeys && buffer_size >= 1)
            {
                hotkeys[0] = m_activation_hotkey;
            }
            return 1;
        }
        return 0;
    }
};

extern "C" __declspec(dllexport) PowertoyModuleIface* __cdecl powertoy_create()
{
    return new PowerDisplayModule();
}
