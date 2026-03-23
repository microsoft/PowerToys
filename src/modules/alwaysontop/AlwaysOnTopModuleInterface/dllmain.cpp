#include "pch.h"

#include <interface/powertoy_module_interface.h>

#include <common/logger/logger.h>
#include <common/utils/resources.h>
#include <common/utils/winapi_error.h>

#include <AlwaysOnTop/trace.h>
#include <AlwaysOnTop/ModuleConstants.h>

#include <shellapi.h>
#include <common/SettingsAPI/settings_objects.h>
#include <common/interop/shared_constants.h>

namespace NonLocalizable
{
    const wchar_t ModulePath[] = L"PowerToys.AlwaysOnTop.exe";
}

namespace
{
    const wchar_t JSON_KEY_PROPERTIES[] = L"properties";
    const wchar_t JSON_KEY_WIN[] = L"win";
    const wchar_t JSON_KEY_ALT[] = L"alt";
    const wchar_t JSON_KEY_CTRL[] = L"ctrl";
    const wchar_t JSON_KEY_SHIFT[] = L"shift";
    const wchar_t JSON_KEY_CODE[] = L"code";
    const wchar_t JSON_KEY_HOTKEY[] = L"hotkey";
    const wchar_t JSON_KEY_INCREASE_OPACITY_HOTKEY[] = L"increase-opacity-hotkey";
    const wchar_t JSON_KEY_DECREASE_OPACITY_HOTKEY[] = L"decrease-opacity-hotkey";
    const wchar_t JSON_KEY_VALUE[] = L"value";
}

BOOL APIENTRY DllMain(HMODULE /*hModule*/, DWORD ul_reason_for_call, LPVOID /*lpReserved*/)
{
    switch (ul_reason_for_call)
    {
    case DLL_PROCESS_ATTACH:
        Trace::AlwaysOnTop::RegisterProvider();
        break;

    case DLL_THREAD_ATTACH:
    case DLL_THREAD_DETACH:
        break;

    case DLL_PROCESS_DETACH:
        Trace::AlwaysOnTop::UnregisterProvider();
        break;
    }
    return TRUE;
}

class AlwaysOnTopModuleInterface : public PowertoyModuleIface
{
public:
    // Return the localized display name of the powertoy
    virtual PCWSTR get_name() override
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
        return powertoys_gpo::getConfiguredAlwaysOnTopEnabledValue();
    }

    // Return JSON with the configuration options.
    // These are the settings shown on the settings page along with their current values.
    virtual bool get_config(wchar_t* buffer, int* buffer_size) override
    {
        HINSTANCE hinstance = reinterpret_cast<HINSTANCE>(&__ImageBase);

        // Create a Settings object.
        PowerToysSettings::Settings settings(hinstance, get_name());

        return settings.serialize_to_buffer(buffer, buffer_size);
    }

    // Passes JSON with the configuration settings for the powertoy.
    // This is called when the user hits Save on the settings page.
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

    virtual bool on_hotkey(size_t hotkeyId) override
    {
        if (m_enabled)
        {
            Logger::trace(L"AlwaysOnTop hotkey pressed, id={}", hotkeyId);
            if (!is_process_running())
            {
                Enable();
            }

            if (hotkeyId == 0)
            {
                SetEvent(m_hPinEvent);
            }
            else if (hotkeyId == 1)
            {
                SetEvent(m_hIncreaseOpacityEvent);
            }
            else if (hotkeyId == 2)
            {
                SetEvent(m_hDecreaseOpacityEvent);
            }

            return true;
        }

        return false;
    }

    virtual size_t get_hotkeys(Hotkey* hotkeys, size_t buffer_size) override
    {
        constexpr size_t hotkeyCount = 3;

        if (hotkeys && buffer_size >= hotkeyCount)
        {
            hotkeys[0] = m_hotkey;
            hotkeys[1] = m_increaseOpacityHotkey;
            hotkeys[2] = m_decreaseOpacityHotkey;

            Logger::trace(L"AlwaysOnTop hotkey[0]: win={}, ctrl={}, shift={}, alt={}, key={}",
                          hotkeys[0].win,
                          hotkeys[0].ctrl,
                          hotkeys[0].shift,
                          hotkeys[0].alt,
                          hotkeys[0].key);
            Logger::trace(L"AlwaysOnTop hotkey[1] (increase opacity): win={}, ctrl={}, shift={}, alt={}, key={}",
                          hotkeys[1].win,
                          hotkeys[1].ctrl,
                          hotkeys[1].shift,
                          hotkeys[1].alt,
                          hotkeys[1].key);
            Logger::trace(L"AlwaysOnTop hotkey[2] (decrease opacity): win={}, ctrl={}, shift={}, alt={}, key={}",
                          hotkeys[2].win,
                          hotkeys[2].ctrl,
                          hotkeys[2].shift,
                          hotkeys[2].alt,
                          hotkeys[2].key);
        }

        Logger::trace(L"AlwaysOnTop get_hotkeys returning count={}", hotkeyCount);
        return hotkeyCount;
    }

    // Enable the powertoy
    virtual void enable()
    {
        Logger::info("AlwaysOnTop enabling");

        Enable();
    }

    // Disable the powertoy
    virtual void disable()
    {
        Logger::info("AlwaysOnTop disabling");

        Disable(true);
    }

    // Returns if the powertoy is enabled
    virtual bool is_enabled() override
    {
        return m_enabled;
    }

    // Destroy the powertoy and free memory
    virtual void destroy() override
    {
        Disable(false);
        delete this;
    }

    AlwaysOnTopModuleInterface()
    {
        app_name = L"AlwaysOnTop"; //TODO: localize
        app_key = NonLocalizable::ModuleKey;
        m_hPinEvent = CreateDefaultEvent(CommonSharedConstants::ALWAYS_ON_TOP_PIN_EVENT);
        m_hTerminateEvent = CreateDefaultEvent(CommonSharedConstants::ALWAYS_ON_TOP_TERMINATE_EVENT);
        m_hIncreaseOpacityEvent = CreateDefaultEvent(CommonSharedConstants::ALWAYS_ON_TOP_INCREASE_OPACITY_EVENT);
        m_hDecreaseOpacityEvent = CreateDefaultEvent(CommonSharedConstants::ALWAYS_ON_TOP_DECREASE_OPACITY_EVENT);
        init_settings();
    }

private:
    void Enable()
    {
        m_enabled = true;

        // Log telemetry
        Trace::AlwaysOnTop::Enable(true);

        unsigned long powertoys_pid = GetCurrentProcessId();
        std::wstring executable_args = L"";
        executable_args.append(std::to_wstring(powertoys_pid));
        ResetEvent(m_hPinEvent);

        SHELLEXECUTEINFOW sei{ sizeof(sei) };
        sei.fMask = { SEE_MASK_NOCLOSEPROCESS | SEE_MASK_FLAG_NO_UI };
        sei.lpFile = NonLocalizable::ModulePath;
        sei.nShow = SW_SHOWNORMAL;
        sei.lpParameters = executable_args.data();
        if (ShellExecuteExW(&sei) == false)
        {
            Logger::error(L"Failed to start AlwaysOnTop");
            auto message = get_last_error_message(GetLastError());
            if (message.has_value())
            {
                Logger::error(message.value());
            }
        }
        else
        {
            m_hProcess = sei.hProcess;
        }
    }

    void Disable(bool const traceEvent)
    {
        m_enabled = false;
        ResetEvent(m_hPinEvent);

        // Log telemetry
        if (traceEvent)
        {
            Trace::AlwaysOnTop::Enable(false);
        }

        SetEvent(m_hTerminateEvent);

        // Wait for 1.5 seconds for the process to end correctly and stop etw tracer
        WaitForSingleObject(m_hProcess, 1500);

        // If process is still running, terminate it
        if (m_hProcess)
        {
            TerminateProcess(m_hProcess, 0);
            m_hProcess = nullptr;
        }
    }

    void parse_hotkey(PowerToysSettings::PowerToyValues& settings)
    {
        const auto parseSingleHotkey = [](const winrt::Windows::Data::Json::JsonObject& propertiesObject, const wchar_t* hotkeyName, Hotkey& hotkey) {
            try
            {
                auto jsonHotkeyObject = propertiesObject.GetNamedObject(hotkeyName).GetNamedObject(JSON_KEY_VALUE);
                hotkey.win = jsonHotkeyObject.GetNamedBoolean(JSON_KEY_WIN);
                hotkey.alt = jsonHotkeyObject.GetNamedBoolean(JSON_KEY_ALT);
                hotkey.shift = jsonHotkeyObject.GetNamedBoolean(JSON_KEY_SHIFT);
                hotkey.ctrl = jsonHotkeyObject.GetNamedBoolean(JSON_KEY_CTRL);
                hotkey.key = static_cast<unsigned char>(jsonHotkeyObject.GetNamedNumber(JSON_KEY_CODE));
            }
            catch (...)
            {
            }
        };

        auto settingsObject = settings.get_raw_json();
        if (settingsObject.GetView().Size())
        {
            try
            {
                auto propertiesObject = settingsObject.GetNamedObject(JSON_KEY_PROPERTIES);
                parseSingleHotkey(propertiesObject, JSON_KEY_HOTKEY, m_hotkey);
                parseSingleHotkey(propertiesObject, JSON_KEY_INCREASE_OPACITY_HOTKEY, m_increaseOpacityHotkey);
                parseSingleHotkey(propertiesObject, JSON_KEY_DECREASE_OPACITY_HOTKEY, m_decreaseOpacityHotkey);
            }
            catch (...)
            {
                Logger::error("Failed to initialize AlwaysOnTop shortcuts");
            }
        }
        else
        {
            Logger::info("AlwaysOnTop settings are empty");
        }
    }

    bool is_process_running()
    {
        return WaitForSingleObject(m_hProcess, 0) == WAIT_TIMEOUT;
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
            Logger::warn(L"An exception occurred while loading the settings file");
            // Error while loading from the settings file. Let default values stay as they are.
        }
    }

    std::wstring app_name;
    std::wstring app_key; //contains the non localized key of the powertoy

    bool m_enabled = false;
    HANDLE m_hProcess = nullptr;
    Hotkey m_hotkey{ .win = true, .ctrl = true, .shift = false, .alt = false, .key = 'T' };
    Hotkey m_increaseOpacityHotkey{ .win = true, .ctrl = true, .shift = false, .alt = false, .key = VK_OEM_PLUS };
    Hotkey m_decreaseOpacityHotkey{ .win = true, .ctrl = true, .shift = false, .alt = false, .key = VK_OEM_MINUS };

    // Handle to event used to pin/unpin windows
    HANDLE m_hPinEvent;
    HANDLE m_hTerminateEvent;
    HANDLE m_hIncreaseOpacityEvent;
    HANDLE m_hDecreaseOpacityEvent;
};

extern "C" __declspec(dllexport) PowertoyModuleIface* __cdecl powertoy_create()
{
    return new AlwaysOnTopModuleInterface();
}
