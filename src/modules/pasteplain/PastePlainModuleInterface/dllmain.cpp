// dllmain.cpp : Defines the entry point for the DLL application.
#include "pch.h"

#include <interface/powertoy_module_interface.h>
#include "trace.h"
#include "Generated Files/resource.h"
#include <common/logger/logger.h>
#include <common/SettingsAPI/settings_objects.h>
#include <common/utils/resources.h>

#include "PastePlainConstants.h"
#include <common/interop/shared_constants.h>
#include <common/utils/logger_helper.h>
#include <common/utils/winapi_error.h>

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

class PastePlain : public PowertoyModuleIface
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

    // Handle to event used to invoke PastePlain
    HANDLE m_hInvokeEvent;

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
                Logger::error("Failed to initialize PastePlain start shortcut");
            }
        }
        else
        {
            Logger::info("PastePlain settings are empty");
        }

        if (!m_hotkey.key)
        {
            Logger::info("PastePlain is going to use default shortcut");
            m_hotkey.win = true;
            m_hotkey.alt = true;
            m_hotkey.shift = false;
            m_hotkey.ctrl = true;
            m_hotkey.key = 'V';
        }
    }

    bool is_process_running()
    {
        return WaitForSingleObject(m_hProcess, 0) == WAIT_TIMEOUT;
    }

    void launch_process()
    {
        Logger::trace(L"Starting PastePlain process");
        unsigned long powertoys_pid = GetCurrentProcessId();

        std::wstring executable_args = L"";
        executable_args.append(std::to_wstring(powertoys_pid));

        SHELLEXECUTEINFOW sei{ sizeof(sei) };
        sei.fMask = { SEE_MASK_NOCLOSEPROCESS | SEE_MASK_FLAG_NO_UI };
        sei.lpFile = L"PowerToys.PastePlain.exe";
        sei.nShow = SW_SHOWNORMAL;
        sei.lpParameters = executable_args.data();
        if (ShellExecuteExW(&sei))
        {
            Logger::trace("Successfully started the PastePlain process");
        }
        else
        {
            Logger::error(L"PastePlain failed to start. {}", get_last_error_or_default(GetLastError()));
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

    void try_inject_modifier_key_up(std::vector<INPUT>& inputs, short modifier)
    {
        // Most significant bit is set if key is down
        if ((GetAsyncKeyState(static_cast<int>(modifier)) & 0x8000) != 0)
        {
            INPUT input_event = {};
            input_event.type = INPUT_KEYBOARD;
            input_event.ki.wVk = modifier;
            input_event.ki.dwFlags = KEYEVENTF_KEYUP;
            inputs.push_back(input_event);
        }
    }

    void try_inject_modifier_key_restore(std::vector<INPUT> &inputs, short modifier)
    {
        // Most significant bit is set if key is down
        if ((GetAsyncKeyState(static_cast<int>(modifier)) & 0x8000) != 0)
        {
            INPUT input_event = {};
            input_event.type = INPUT_KEYBOARD;
            input_event.ki.wVk = modifier;
            inputs.push_back(input_event);
        }
    }

    void try_to_paste_as_plain_text()
    {
        std::wstring clipboard_text;

        {
            // Read clipboard data begin

            if (!OpenClipboard(NULL))
            {
                DWORD errorCode = GetLastError();
                auto errorMessage = get_last_error_message(errorCode);
                Logger::error(L"Couldn't open the clipboard to get the text. {}", errorMessage.has_value() ? errorMessage.value() : L"");
                Trace::PastePlainError(errorCode, errorMessage.has_value() ? errorMessage.value() : L"", L"read.OpenClipboard");
                return;
            }
            HANDLE h_clipboard_data = GetClipboardData(CF_UNICODETEXT);

            if (h_clipboard_data == NULL)
            {
                DWORD errorCode = GetLastError();
                auto errorMessage = get_last_error_message(errorCode);
                Logger::error(L"Failed to get clipboard data. {}", errorMessage.has_value() ? errorMessage.value() : L"");
                Trace::PastePlainError(errorCode, errorMessage.has_value() ? errorMessage.value() : L"", L"read.GetClipboardData");
                CloseClipboard();
                return;
            }

            wchar_t* pch_data = static_cast<wchar_t*>(GlobalLock(h_clipboard_data));

            if (NULL == pch_data)
            {
                DWORD errorCode = GetLastError();
                auto errorMessage = get_last_error_message(errorCode);
                Logger::error(L"Couldn't lock the buffer to get the unformatted text from the clipboard. {}", errorMessage.has_value() ? errorMessage.value() : L"");
                Trace::PastePlainError(errorCode, errorMessage.has_value() ? errorMessage.value() : L"", L"read.GlobalLock");
                CloseClipboard();
                return;
            }

            clipboard_text = pch_data;
            GlobalUnlock(h_clipboard_data);

            CloseClipboard();
            // Read clipboard data end
        }

        {
            // Copy text to clipboard begin
            UINT no_clipboard_history_or_roaming_format = 0;

            // Get the format identifier for not adding the data to the clipboard history or roaming.
            // https://learn.microsoft.com/windows/win32/dataxchg/clipboard-formats#cloud-clipboard-and-clipboard-history-formats
            if (0 == (no_clipboard_history_or_roaming_format = RegisterClipboardFormat(L"ExcludeClipboardContentFromMonitorProcessing")))
            {
                DWORD errorCode = GetLastError();
                auto errorMessage = get_last_error_message(errorCode);
                Logger::error(L"Couldn't get the clipboard data format type that would allow excluding the data from the clipboard history / roaming. {}", errorMessage.has_value() ? errorMessage.value() : L"");
                Trace::PastePlainError(errorCode, errorMessage.has_value() ? errorMessage.value() : L"", L"write.RegisterClipboardFormat");
                return;
            }

            if (!OpenClipboard(NULL))
            {
                DWORD errorCode = GetLastError();
                auto errorMessage = get_last_error_message(errorCode);
                Logger::error(L"Couldn't open the clipboard to copy the unformatted text. {}", errorMessage.has_value() ? errorMessage.value() : L"");
                Trace::PastePlainError(errorCode, errorMessage.has_value() ? errorMessage.value() : L"", L"write.OpenClipboard");
                return;
            }

            HGLOBAL h_clipboard_data;

            if (NULL == (h_clipboard_data = GlobalAlloc(GMEM_MOVEABLE, (clipboard_text.length() + 1) * sizeof(wchar_t))))
            {
                DWORD errorCode = GetLastError();
                auto errorMessage = get_last_error_message(errorCode);
                Logger::error(L"Couldn't allocate a buffer for the unformatted text. {}", errorMessage.has_value() ? errorMessage.value() : L"");
                Trace::PastePlainError(errorCode, errorMessage.has_value() ? errorMessage.value() : L"", L"write.GlobalAlloc");
                CloseClipboard();
                return;
            }
            wchar_t* pch_data = static_cast<wchar_t*>(GlobalLock(h_clipboard_data));

            if (NULL == pch_data)
            {
                DWORD errorCode = GetLastError();
                auto errorMessage = get_last_error_message(errorCode);
                Logger::error(L"Couldn't lock the buffer to send the unformatted text to the clipboard. {}", errorMessage.has_value() ? errorMessage.value() : L"");
                GlobalFree(h_clipboard_data);
                CloseClipboard();
                Trace::PastePlainError(errorCode, errorMessage.has_value() ? errorMessage.value() : L"", L"write.GlobalLock");
                return;
            }

            wcscpy_s(pch_data, clipboard_text.length() + 1, clipboard_text.c_str());

            EmptyClipboard();

            if (NULL == SetClipboardData(CF_UNICODETEXT, h_clipboard_data))
            {
                DWORD errorCode = GetLastError();
                auto errorMessage = get_last_error_message(errorCode);
                Logger::error(L"Couldn't set the clipboard data to the unformatted text. {}", errorMessage.has_value() ? errorMessage.value() : L"");
                GlobalUnlock(h_clipboard_data);
                GlobalFree(h_clipboard_data);
                CloseClipboard();
                Trace::PastePlainError(errorCode, errorMessage.has_value() ? errorMessage.value() : L"", L"write.SetClipboardData");
                return;
            }

            // Don't show in history or allow data roaming.
            SetClipboardData(no_clipboard_history_or_roaming_format, 0);

            CloseClipboard();
            // Copy text to clipboard end
        }
        {
            // Clear kb state and send Ctrl+V begin

            // we can assume that the last pressed key is...
            //  (1) not a modifier key and
            //  (2) marked as handled (so it never gets a key down input event).
            // So, let's check which modifiers were pressed,
            // and, if they were, inject a key up event for each of them
            std::vector<INPUT> inputs;
            try_inject_modifier_key_up(inputs, VK_LCONTROL);
            try_inject_modifier_key_up(inputs, VK_RCONTROL);
            try_inject_modifier_key_up(inputs, VK_LWIN);
            try_inject_modifier_key_up(inputs, VK_RWIN);
            try_inject_modifier_key_up(inputs, VK_LSHIFT);
            try_inject_modifier_key_up(inputs, VK_RSHIFT);
            try_inject_modifier_key_up(inputs, VK_LMENU);
            try_inject_modifier_key_up(inputs, VK_RMENU);

            // send Ctrl+V (key downs and key ups)
            {
                INPUT input_event = {};
                input_event.type = INPUT_KEYBOARD;
                input_event.ki.wVk = VK_CONTROL;
                inputs.push_back(input_event);
            }

            {
                INPUT input_event = {};
                input_event.type = INPUT_KEYBOARD;
                input_event.ki.wVk = 0x56; // V
                // Avoid triggering detection by the centralized keyboard hook. Allows using Control+V as activation.
                input_event.ki.dwExtraInfo = CENTRALIZED_KEYBOARD_HOOK_DONT_TRIGGER_FLAG;
                inputs.push_back(input_event);
            }

            {
                INPUT input_event = {};
                input_event.type = INPUT_KEYBOARD;
                input_event.ki.wVk = 0x56; // V
                input_event.ki.dwFlags = KEYEVENTF_KEYUP;
                // Avoid triggering detection by the centralized keyboard hook. Allows using Control+V as activation.
                input_event.ki.dwExtraInfo = CENTRALIZED_KEYBOARD_HOOK_DONT_TRIGGER_FLAG;
                inputs.push_back(input_event);
            }

            {
                INPUT input_event = {};
                input_event.type = INPUT_KEYBOARD;
                input_event.ki.wVk = VK_CONTROL;
                input_event.ki.dwFlags = KEYEVENTF_KEYUP;
                inputs.push_back(input_event);
            }

            try_inject_modifier_key_restore(inputs, VK_LCONTROL);
            try_inject_modifier_key_restore(inputs, VK_RCONTROL);
            try_inject_modifier_key_restore(inputs, VK_LWIN);
            try_inject_modifier_key_restore(inputs, VK_RWIN);
            try_inject_modifier_key_restore(inputs, VK_LSHIFT);
            try_inject_modifier_key_restore(inputs, VK_RSHIFT);
            try_inject_modifier_key_restore(inputs, VK_LMENU);
            try_inject_modifier_key_restore(inputs, VK_RMENU);

            auto uSent = SendInput(static_cast<UINT>(inputs.size()), inputs.data(), sizeof(INPUT));
            if (uSent != inputs.size())
            {
                DWORD errorCode = GetLastError();
                auto errorMessage = get_last_error_message(errorCode);
                Logger::error(L"SendInput failed. Expected to send {} inputs and sent only {}. {}", inputs.size(), uSent, errorMessage.has_value() ? errorMessage.value() : L"");
                Trace::PastePlainError(errorCode, errorMessage.has_value() ? errorMessage.value() : L"", L"input.SendInput");
                return;
            }

            // Clear kb state and send Ctrl+V end
        }
        Trace::PastePlainSuccess();
    }

public:
    PastePlain()
    {
        app_name = GET_RESOURCE_STRING(IDS_PASTEPLAIN_NAME);
        app_key = PastePlainConstants::ModuleKey;
        LoggerHelpers::init_logger(app_key, L"ModuleInterface", "PastePlain");
        init_settings();
    }

    ~PastePlain()
    {
        if (m_enabled)
        {
        }
        m_enabled = false;
    }

    // Destroy the powertoy and free memory
    virtual void destroy() override
    {
        Logger::trace("PastePlain::destroy()");
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
        return powertoys_gpo::getConfiguredPastePlainEnabledValue();
    }

    virtual bool get_config(wchar_t* buffer, int* buffer_size) override
    {
        HINSTANCE hinstance = reinterpret_cast<HINSTANCE>(&__ImageBase);

        // Create a Settings object.
        PowerToysSettings::Settings settings(hinstance, get_name());
        settings.set_description(GET_RESOURCE_STRING(IDS_PASTEPLAIN_SETTINGS_DESC));

        settings.set_overview_link(L"https://aka.ms/PowerToysOverview_PastePlain");

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
        Logger::trace("PastePlain::enable()");
        m_enabled = true;
        Trace::EnablePastePlain(true);
    };

    virtual void disable()
    {
        Logger::trace("PastePlain::disable()");
        m_enabled = false;
        Trace::EnablePastePlain(false);
    }

    virtual bool on_hotkey(size_t /*hotkeyId*/) override
    {
        if (m_enabled)
        {
            Logger::trace(L"PastePlain hotkey pressed");

            std::thread([=]() {
                // hotkey work should be kept to a minimum, or Windows might deregister our low level keyboard hook.
                // Move work to another thread.
                try_to_paste_as_plain_text();
            }).detach();

            Trace::PastePlainInvoked();
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

    virtual void send_settings_telemetry() override
    {
        Logger::info("Send settings telemetry");
        Trace::SettingsTelemetry(m_hotkey);
    }
};

extern "C" __declspec(dllexport) PowertoyModuleIface* __cdecl powertoy_create()
{
    return new PastePlain();
}
