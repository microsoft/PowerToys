// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#include "pch.h"
#include "../../../interface/powertoy_module_interface.h"
#include "../../../common/SettingsAPI/settings_objects.h"
#include "trace.h"
#include "../../../common/utils/process_path.h"
#include "../../../common/utils/resources.h"
#include "../../../common/logger/logger.h"
#include "../../../common/utils/logger_helper.h"
#include "../../../common/interop/shared_constants.h"
#include <atomic>
#include <thread>
#include <string>
#include <windows.h>
#include "resource.h"
#include "CursorFocusCore.h"

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

// Non-Localizable strings
namespace
{
    const wchar_t JSON_KEY_PROPERTIES[] = L"properties";
    const wchar_t JSON_KEY_VALUE[] = L"value";
    const wchar_t JSON_KEY_ACTIVATION_SHORTCUT[] = L"activation_shortcut";
    const wchar_t JSON_KEY_AUTO_ACTIVATE[] = L"auto_activate";
    const wchar_t JSON_KEY_FOCUS_CHANGE_DELAY_MS[] = L"focus_change_delay_ms";
    const wchar_t JSON_KEY_TARGET_POSITION[] = L"target_position";
    const wchar_t JSON_KEY_DISABLE_ON_FULLSCREEN[] = L"disable_on_fullscreen";
    const wchar_t JSON_KEY_DISABLE_ON_GAME_MODE[] = L"disable_on_game_mode";
}

// The PowerToy name that will be shown in the settings.
const static wchar_t* MODULE_NAME = L"CursorFocus";
// Add a description that will we shown in the module settings page.
const static wchar_t* MODULE_DESC = L"<no description>";

// Forward declaration
class CursorFocus;

// Global instance pointer
static CursorFocus* g_cursorFocusInstance = nullptr;

// Implement the PowerToy Module Interface and all the required methods.
class CursorFocus : public PowertoyModuleIface
{
private:
    // The PowerToy state.
    bool m_enabled = false;
    bool m_autoActivate = false;
    int m_focusChangeDelayMs = 200;
    int m_targetPosition = 0; // 0=Center of window, 1=Center of title bar
    bool m_disableOnFullScreen = false;
    bool m_disableOnGameMode = false;

    // Core focus engine
    CursorFocusCore m_core;

    // Hotkey
    Hotkey m_activationHotkey{};

    // Event-driven trigger support (for CmdPal/automation)
    HANDLE m_triggerEventHandle = nullptr;
    HANDLE m_terminateEventHandle = nullptr;
    std::thread m_eventThread;
    std::atomic_bool m_listening{ false };

    // Active state (can be toggled by hotkey)
    std::atomic_bool m_active{ false };

public:
    // Constructor
    CursorFocus()
    {
        LoggerHelpers::init_logger(MODULE_NAME, L"ModuleInterface", LogSettings::cursorFocusLoggerName);
        init_settings();
        g_cursorFocusInstance = this;
    }

    // Destroy the powertoy and free memory
    virtual void destroy() override
    {
        disable();
        g_cursorFocusInstance = nullptr;
        delete this;
    }

    // Return the localized display name of the powertoy
    virtual const wchar_t* get_name() override
    {
        return MODULE_NAME;
    }

    // Return the non localized key of the powertoy, this will be cached by the runner
    virtual const wchar_t* get_key() override
    {
        return MODULE_NAME;
    }

    // Return the configured status for the gpo policy for the module
    virtual powertoys_gpo::gpo_rule_configured_t gpo_policy_enabled_configuration() override
    {
        return powertoys_gpo::getConfiguredCursorFocusEnabledValue();
    }

    // Return JSON with the configuration options.
    virtual bool get_config(wchar_t* buffer, int* buffer_size) override
    {
        HINSTANCE hinstance = reinterpret_cast<HINSTANCE>(&__ImageBase);

        PowerToysSettings::Settings settings(hinstance, get_name());

        settings.set_description(IDS_CURSORFOCUS_NAME);
        settings.set_icon_key(L"pt-cursor-focus");

        // Create HotkeyObject from the Hotkey struct for the settings
        auto hotkey_object = PowerToysSettings::HotkeyObject::from_settings(
            m_activationHotkey.win,
            m_activationHotkey.ctrl,
            m_activationHotkey.alt,
            m_activationHotkey.shift,
            m_activationHotkey.key);

        settings.add_hotkey(JSON_KEY_ACTIVATION_SHORTCUT, IDS_CURSORFOCUS_NAME, hotkey_object);
        settings.add_bool_toggle(JSON_KEY_AUTO_ACTIVATE, IDS_CURSORFOCUS_NAME, m_autoActivate);

        return settings.serialize_to_buffer(buffer, buffer_size);
    }

    // Signal from the Settings editor to call a custom action.
    virtual void call_custom_action(const wchar_t* /*action*/) override {}

    // Called by the runner to pass the updated settings values as a serialized JSON.
    virtual void set_config(const wchar_t* config) override
    {
        try
        {
            // Parse the input JSON string.
            PowerToysSettings::PowerToyValues values =
                PowerToysSettings::PowerToyValues::from_json_string(config, get_key());

            parse_settings(values);
        }
        catch (std::exception&)
        {
            Logger::error("Invalid json when trying to parse CursorFocus settings json.");
        }
    }

    // Enable the powertoy
    virtual void enable()
    {
        m_enabled = true;
        Trace::EnableCursorFocus(true);

        Logger::info(L"CursorFocus: enable() called");

        // Start listening for external trigger event
        m_triggerEventHandle = CreateEventW(nullptr, false, false, CommonSharedConstants::CURSOR_FOCUS_TRIGGER_EVENT);
        m_terminateEventHandle = CreateEventW(nullptr, false, false, nullptr);

        if (m_triggerEventHandle && m_terminateEventHandle)
        {
            m_listening = true;
            m_eventThread = std::thread([this]() {
                HANDLE events[] = { m_triggerEventHandle, m_terminateEventHandle };
                while (m_listening)
                {
                    DWORD result = WaitForMultipleObjects(2, events, FALSE, INFINITE);
                    if (result == WAIT_OBJECT_0)
                    {
                        // Toggle active state
                        toggle_active();
                    }
                    else if (result == WAIT_OBJECT_0 + 1)
                    {
                        // Terminate event
                        break;
                    }
                }
            });
        }

        // Apply settings to core
        m_core.SetFocusChangeDelayMs(m_focusChangeDelayMs);
        m_core.SetTargetPosition(m_targetPosition);
        m_core.SetDisableOnFullScreen(m_disableOnFullScreen);
        m_core.SetDisableOnGameMode(m_disableOnGameMode);

        Logger::info(L"CursorFocus: m_autoActivate={}", m_autoActivate);

        // Auto-activate if configured
        if (m_autoActivate)
        {
            m_active = true;
            m_core.Start();
            Logger::info(L"CursorFocus: m_core.Start() called from enable()");
        }

        Logger::info("CursorFocus module enabled");
    }

    // Disable the powertoy
    virtual void disable()
    {
        if (!m_enabled)
        {
            return;
        }

        m_enabled = false;
        m_active = false;
        Trace::EnableCursorFocus(false);

        // Stop the core
        m_core.Stop();

        // Stop listening for trigger events
        m_listening = false;
        if (m_terminateEventHandle)
        {
            SetEvent(m_terminateEventHandle);
        }

        if (m_eventThread.joinable())
        {
            m_eventThread.join();
        }

        if (m_triggerEventHandle)
        {
            CloseHandle(m_triggerEventHandle);
            m_triggerEventHandle = nullptr;
        }
        if (m_terminateEventHandle)
        {
            CloseHandle(m_terminateEventHandle);
            m_terminateEventHandle = nullptr;
        }

        Logger::info("CursorFocus module disabled");
    }

    // Returns if the powertoy is enabled
    virtual bool is_enabled() override
    {
        return m_enabled;
    }

    // Toggle the active state
    void toggle_active()
    {
        if (!m_enabled)
        {
            return;
        }

        bool currentState = m_active.load();
        m_active = !currentState;

        if (m_active)
        {
            m_core.Start();
            Logger::info("CursorFocus activated");
        }
        else
        {
            m_core.Stop();
            Logger::info("CursorFocus deactivated");
        }
    }

private:
void init_settings()
{
    try
    {
        Logger::info(L"CursorFocus: init_settings() loading settings from file");
        // Load settings from file
        PowerToysSettings::PowerToyValues settings =
            PowerToysSettings::PowerToyValues::load_from_settings_file(MODULE_NAME);

        parse_settings(settings);
        Logger::info(L"CursorFocus: init_settings() completed, m_autoActivate = {}", m_autoActivate);
    }
    catch (std::exception& ex)
    {
        Logger::warn("CursorFocus settings could not be loaded: {}. Using defaults.", ex.what());
        // Use default values
        m_activationHotkey.win = true;
        m_activationHotkey.ctrl = false;
        m_activationHotkey.alt = true;
        m_activationHotkey.shift = false;
        m_activationHotkey.key = 0x46; // 'F' key
    }
}

    void parse_settings(const PowerToysSettings::PowerToyValues& values)
    {
        Logger::info(L"CursorFocus: parse_settings() called");
        
        try
        {
            // Parse hotkey - this one correctly uses get_json because it's an object
            if (auto hotkey = values.get_json(JSON_KEY_ACTIVATION_SHORTCUT))
            {
                auto hk = PowerToysSettings::HotkeyObject::from_json(*hotkey);
                m_activationHotkey.win = hk.win_pressed();
                m_activationHotkey.ctrl = hk.ctrl_pressed();
                m_activationHotkey.alt = hk.alt_pressed();
                m_activationHotkey.shift = hk.shift_pressed();
                m_activationHotkey.key = hk.get_code();
                Logger::info(L"CursorFocus: Parsed hotkey successfully");
            }
        }
        catch (...)
        {
            Logger::warn("Failed to parse activation shortcut");
        }

        try
        {
            // Use get_bool_value for boolean settings
            if (auto val = values.get_bool_value(JSON_KEY_AUTO_ACTIVATE))
            {
                m_autoActivate = *val;
                Logger::info(L"CursorFocus: Parsed auto_activate = {}", m_autoActivate);
            }
            else
            {
                Logger::warn(L"CursorFocus: auto_activate key not found in settings");
            }
        }
        catch (...)
        {
            Logger::warn("Failed to parse auto_activate");
        }

        try
        {
            // Use get_int_value for integer settings
            if (auto val = values.get_int_value(JSON_KEY_FOCUS_CHANGE_DELAY_MS))
            {
                m_focusChangeDelayMs = *val;
                m_core.SetFocusChangeDelayMs(m_focusChangeDelayMs);
                Logger::info(L"CursorFocus: Parsed focus_change_delay_ms = {}", m_focusChangeDelayMs);
            }
        }
        catch (...)
        {
            Logger::warn("Failed to parse focus_change_delay_ms");
        }

        try
        {
            if (auto val = values.get_int_value(JSON_KEY_TARGET_POSITION))
            {
                m_targetPosition = *val;
                m_core.SetTargetPosition(m_targetPosition);
            }
        }
        catch (...)
        {
            Logger::warn("Failed to parse target_position");
        }

        try
        {
            if (auto val = values.get_bool_value(JSON_KEY_DISABLE_ON_FULLSCREEN))
            {
                m_disableOnFullScreen = *val;
                m_core.SetDisableOnFullScreen(m_disableOnFullScreen);
            }
        }
        catch (...)
        {
            Logger::warn("Failed to parse disable_on_fullscreen");
        }

        try
        {
            if (auto val = values.get_bool_value(JSON_KEY_DISABLE_ON_GAME_MODE))
            {
                m_disableOnGameMode = *val;
                m_core.SetDisableOnGameMode(m_disableOnGameMode);
            }
        }
        catch (...)
        {
            Logger::warn("Failed to parse disable_on_game_mode");
        }
    }
};

// Load the settings file.
extern "C" __declspec(dllexport) PowertoyModuleIface* __cdecl powertoy_create()
{
    return new CursorFocus();
}
