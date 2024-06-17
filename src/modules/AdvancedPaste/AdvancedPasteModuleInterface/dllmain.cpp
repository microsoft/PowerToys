// dllmain.cpp : Defines the entry point for the DLL application.
#include "pch.h"

#include "AdvancedPasteConstants.h"
#include <interface/powertoy_module_interface.h>
#include "trace.h"
#include "Generated Files/resource.h"
#include <common/logger/logger.h>
#include <common/SettingsAPI/settings_helpers.h>
#include <common/SettingsAPI/settings_objects.h>
#include <common/utils/resources.h>

#include <common/interop/shared_constants.h>
#include <common/utils/logger_helper.h>
#include <common/utils/winapi_error.h>

BOOL APIENTRY DllMain(HMODULE /*hModule*/, DWORD ul_reason_for_call, LPVOID /*lpReserved*/)
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
    const wchar_t JSON_KEY_PASTE_AS_PLAIN_HOTKEY[] = L"paste-as-plain-hotkey";
    const wchar_t JSON_KEY_ADVANCED_PASTE_UI_HOTKEY[] = L"advanced-paste-ui-hotkey";
    const wchar_t JSON_KEY_PASTE_AS_MARKDOWN_HOTKEY[] = L"paste-as-markdown-hotkey";
    const wchar_t JSON_KEY_PASTE_AS_JSON_HOTKEY[] = L"paste-as-json-hotkey";
    const wchar_t JSON_KEY_SHOW_CUSTOM_PREVIEW[] = L"ShowCustomPreview";
    const wchar_t JSON_KEY_VALUE[] = L"value";
}

class AdvancedPaste : public PowertoyModuleIface
{
private:
    bool m_enabled = false;

    std::wstring app_name;

    //contains the non localized key of the powertoy
    std::wstring app_key;

    HANDLE m_hProcess;

    // Time to wait for process to close after sending WM_CLOSE signal
    static const int MAX_WAIT_MILLISEC = 10000;

    Hotkey m_paste_as_plain_hotkey = { .win = true, .ctrl = true, .shift = false, .alt = true, .key = 'V' };
    Hotkey m_advanced_paste_ui_hotkey = { .win = true, .ctrl = false, .shift = true, .alt = false, .key = 'V' };
    Hotkey m_paste_as_markdown_hotkey{};
    Hotkey m_paste_as_json_hotkey{};

    bool m_preview_custom_format_output = true;

    // Handle to event used to invoke AdvancedPaste
    HANDLE m_hShowUIEvent;
    HANDLE m_hPasteMarkdownEvent;
    HANDLE m_hPasteJsonEvent;

    Hotkey parse_single_hotkey(const wchar_t* hotkey, const winrt::Windows::Data::Json::JsonObject& settingsObject)
    {
        try
        {
            Hotkey _temp_paste_as_plain;
            auto jsonHotkeyObject = settingsObject.GetNamedObject(JSON_KEY_PROPERTIES).GetNamedObject(hotkey);
            _temp_paste_as_plain.win = jsonHotkeyObject.GetNamedBoolean(JSON_KEY_WIN);
            _temp_paste_as_plain.alt = jsonHotkeyObject.GetNamedBoolean(JSON_KEY_ALT);
            _temp_paste_as_plain.shift = jsonHotkeyObject.GetNamedBoolean(JSON_KEY_SHIFT);
            _temp_paste_as_plain.ctrl = jsonHotkeyObject.GetNamedBoolean(JSON_KEY_CTRL);
            _temp_paste_as_plain.key = static_cast<unsigned char>(jsonHotkeyObject.GetNamedNumber(JSON_KEY_CODE));
            return _temp_paste_as_plain;
        }
        catch (...)
        {
            Logger::error("Failed to initialize AdvancedPaste shortcut from settings. Value will keep unchanged.");
        }

        return {};
    }

    bool migrate_data_and_remove_data_file(Hotkey& old_paste_as_plain_hotkey)
    {
        const wchar_t OLD_JSON_KEY_ACTIVATION_SHORTCUT[] = L"ActivationShortcut";
        const wchar_t OLD_MODULE_KEY[] = L"PastePlain";

        try
        {
            const std::wstring save_file_location = PTSettingsHelper::get_module_save_file_location(OLD_MODULE_KEY);
            if (std::filesystem::exists(save_file_location))
            {
                PowerToysSettings::PowerToyValues settings =
                    PowerToysSettings::PowerToyValues::load_from_settings_file(OLD_MODULE_KEY);

                auto settingsObject = settings.get_raw_json();
                if (settingsObject.GetView().Size())
                {
                    old_paste_as_plain_hotkey = parse_single_hotkey(OLD_JSON_KEY_ACTIVATION_SHORTCUT, settingsObject);

                    std::filesystem::remove(save_file_location);

                    return true;
                }
            }
        }
        catch (std::exception)
        {
        }

        return false;
    }

    void parse_hotkeys(PowerToysSettings::PowerToyValues& settings)
    {
        auto settingsObject = settings.get_raw_json();

        // Migrate Paste As PLain text shortcut
        Hotkey old_paste_as_plain_hotkey;
        bool old_data_migrated = migrate_data_and_remove_data_file(old_paste_as_plain_hotkey);
        if (old_data_migrated)
        {
            m_paste_as_plain_hotkey = old_paste_as_plain_hotkey;

            // override settings file
            json::JsonObject new_hotkey_value;
            new_hotkey_value.SetNamedValue(JSON_KEY_WIN, json::value(old_paste_as_plain_hotkey.win));
            new_hotkey_value.SetNamedValue(JSON_KEY_ALT, json::value(old_paste_as_plain_hotkey.alt));
            new_hotkey_value.SetNamedValue(JSON_KEY_SHIFT, json::value(old_paste_as_plain_hotkey.shift));
            new_hotkey_value.SetNamedValue(JSON_KEY_CTRL, json::value(old_paste_as_plain_hotkey.ctrl));
            new_hotkey_value.SetNamedValue(JSON_KEY_CODE, json::value(old_paste_as_plain_hotkey.key));

            if (!settingsObject.HasKey(JSON_KEY_PROPERTIES))
            {
                settingsObject.SetNamedValue(JSON_KEY_PROPERTIES, json::JsonObject{});
            }

            settingsObject.GetNamedObject(JSON_KEY_PROPERTIES).SetNamedValue(JSON_KEY_PASTE_AS_PLAIN_HOTKEY, new_hotkey_value);


            json::JsonObject ui_hotkey;
            ui_hotkey.SetNamedValue(JSON_KEY_WIN, json::value(m_advanced_paste_ui_hotkey.win));
            ui_hotkey.SetNamedValue(JSON_KEY_ALT, json::value(m_advanced_paste_ui_hotkey.alt));
            ui_hotkey.SetNamedValue(JSON_KEY_SHIFT, json::value(m_advanced_paste_ui_hotkey.shift));
            ui_hotkey.SetNamedValue(JSON_KEY_CTRL, json::value(m_advanced_paste_ui_hotkey.ctrl));
            ui_hotkey.SetNamedValue(JSON_KEY_CODE, json::value(m_advanced_paste_ui_hotkey.key));
            settingsObject.GetNamedObject(JSON_KEY_PROPERTIES).SetNamedValue(JSON_KEY_ADVANCED_PASTE_UI_HOTKEY, ui_hotkey);

            settings.save_to_settings_file();
        }
        else
        {
            if (settingsObject.GetView().Size())
            {
                if (settingsObject.GetNamedObject(JSON_KEY_PROPERTIES).HasKey(JSON_KEY_PASTE_AS_PLAIN_HOTKEY))
                {
                    m_paste_as_plain_hotkey = parse_single_hotkey(JSON_KEY_PASTE_AS_PLAIN_HOTKEY, settingsObject);
                }
                if (settingsObject.GetNamedObject(JSON_KEY_PROPERTIES).HasKey(JSON_KEY_ADVANCED_PASTE_UI_HOTKEY))
                {
                    m_advanced_paste_ui_hotkey = parse_single_hotkey(JSON_KEY_ADVANCED_PASTE_UI_HOTKEY, settingsObject);
                }
                if (settingsObject.GetNamedObject(JSON_KEY_PROPERTIES).HasKey(JSON_KEY_PASTE_AS_MARKDOWN_HOTKEY))
                {
                    m_paste_as_markdown_hotkey = parse_single_hotkey(JSON_KEY_PASTE_AS_MARKDOWN_HOTKEY, settingsObject);
                }
                if (settingsObject.GetNamedObject(JSON_KEY_PROPERTIES).HasKey(JSON_KEY_PASTE_AS_JSON_HOTKEY))
                {
                    m_paste_as_json_hotkey = parse_single_hotkey(JSON_KEY_PASTE_AS_JSON_HOTKEY, settingsObject);
                }
            }
        }
    }

    bool is_process_running()
    {
        return WaitForSingleObject(m_hProcess, 0) == WAIT_TIMEOUT;
    }

    void launch_process(const std::wstring& arg = L"")
    {
        Logger::trace(L"Starting AdvancedPaste process");
        unsigned long powertoys_pid = GetCurrentProcessId();

        std::wstring executable_args = L"";
        executable_args.append(std::to_wstring(powertoys_pid));

        executable_args += L" " + arg;

        SHELLEXECUTEINFOW sei{ sizeof(sei) };
        sei.fMask = { SEE_MASK_NOCLOSEPROCESS | SEE_MASK_FLAG_NO_UI };
        sei.lpFile = L"WinUI3Apps\\PowerToys.AdvancedPaste.exe";
        sei.nShow = SW_SHOWNORMAL;
        sei.lpParameters = executable_args.data();
        if (ShellExecuteExW(&sei))
        {
            Logger::trace("Successfully started the Advanced Paste process");
        }
        else
        {
            Logger::error(L"AdvancedPaste failed to start. {}", get_last_error_or_default(GetLastError()));
        }

        TerminateProcess(m_hProcess, 1);
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

            parse_hotkeys(settings);

            auto settingsObject = settings.get_raw_json();
            if (settingsObject.GetView().Size() && settingsObject.GetNamedObject(JSON_KEY_PROPERTIES).HasKey(JSON_KEY_SHOW_CUSTOM_PREVIEW))
            {
                m_preview_custom_format_output = settingsObject.GetNamedObject(JSON_KEY_PROPERTIES).GetNamedObject(JSON_KEY_SHOW_CUSTOM_PREVIEW).GetNamedBoolean(JSON_KEY_VALUE);
            }
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
                Trace::AdvancedPaste_Error(errorCode, errorMessage.has_value() ? errorMessage.value() : L"", L"read.OpenClipboard");
                return;
            }
            HANDLE h_clipboard_data = GetClipboardData(CF_UNICODETEXT);

            if (h_clipboard_data == NULL)
            {
                DWORD errorCode = GetLastError();
                auto errorMessage = get_last_error_message(errorCode);
                Logger::error(L"Failed to get clipboard data. {}", errorMessage.has_value() ? errorMessage.value() : L"");
                Trace::AdvancedPaste_Error(errorCode, errorMessage.has_value() ? errorMessage.value() : L"", L"read.GetClipboardData");
                CloseClipboard();
                return;
            }

            wchar_t* pch_data = static_cast<wchar_t*>(GlobalLock(h_clipboard_data));

            if (NULL == pch_data)
            {
                DWORD errorCode = GetLastError();
                auto errorMessage = get_last_error_message(errorCode);
                Logger::error(L"Couldn't lock the buffer to get the unformatted text from the clipboard. {}", errorMessage.has_value() ? errorMessage.value() : L"");
                Trace::AdvancedPaste_Error(errorCode, errorMessage.has_value() ? errorMessage.value() : L"", L"read.GlobalLock");
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
                Trace::AdvancedPaste_Error(errorCode, errorMessage.has_value() ? errorMessage.value() : L"", L"write.RegisterClipboardFormat");
                return;
            }

            if (!OpenClipboard(NULL))
            {
                DWORD errorCode = GetLastError();
                auto errorMessage = get_last_error_message(errorCode);
                Logger::error(L"Couldn't open the clipboard to copy the unformatted text. {}", errorMessage.has_value() ? errorMessage.value() : L"");
                Trace::AdvancedPaste_Error(errorCode, errorMessage.has_value() ? errorMessage.value() : L"", L"write.OpenClipboard");
                return;
            }

            HGLOBAL h_clipboard_data;

            if (NULL == (h_clipboard_data = GlobalAlloc(GMEM_MOVEABLE, (clipboard_text.length() + 1) * sizeof(wchar_t))))
            {
                DWORD errorCode = GetLastError();
                auto errorMessage = get_last_error_message(errorCode);
                Logger::error(L"Couldn't allocate a buffer for the unformatted text. {}", errorMessage.has_value() ? errorMessage.value() : L"");
                Trace::AdvancedPaste_Error(errorCode, errorMessage.has_value() ? errorMessage.value() : L"", L"write.GlobalAlloc");
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
                Trace::AdvancedPaste_Error(errorCode, errorMessage.has_value() ? errorMessage.value() : L"", L"write.GlobalLock");
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
                Trace::AdvancedPaste_Error(errorCode, errorMessage.has_value() ? errorMessage.value() : L"", L"write.SetClipboardData");
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

            // After restoring the modifier keys send a dummy key to prevent Start Menu from activating
            INPUT dummyEvent = {};
            dummyEvent.type = INPUT_KEYBOARD;
            dummyEvent.ki.wVk = 0xFF;
            dummyEvent.ki.dwFlags = KEYEVENTF_KEYUP;
            inputs.push_back(dummyEvent);

            auto uSent = SendInput(static_cast<UINT>(inputs.size()), inputs.data(), sizeof(INPUT));
            if (uSent != inputs.size())
            {
                DWORD errorCode = GetLastError();
                auto errorMessage = get_last_error_message(errorCode);
                Logger::error(L"SendInput failed. Expected to send {} inputs and sent only {}. {}", inputs.size(), uSent, errorMessage.has_value() ? errorMessage.value() : L"");
                Trace::AdvancedPaste_Error(errorCode, errorMessage.has_value() ? errorMessage.value() : L"", L"input.SendInput");
                return;
            }

            // Clear kb state and send Ctrl+V end
        }
    }

    void bring_process_to_front()
    {
        auto enum_windows = [](HWND hwnd, LPARAM param) -> BOOL {
            HANDLE process_handle = reinterpret_cast<HANDLE>(param);
            DWORD window_process_id = 0;

            GetWindowThreadProcessId(hwnd, &window_process_id);
            if (GetProcessId(process_handle) == window_process_id)
            {
                SetForegroundWindow(hwnd);
                return FALSE;
            }
            return TRUE;
        };

        EnumWindows(enum_windows, (LPARAM)m_hProcess);
    }

public:
    AdvancedPaste()
    {
        app_name = GET_RESOURCE_STRING(IDS_ADVANCED_PASTE_NAME);
        app_key = AdvancedPasteConstants::ModuleKey;
        LoggerHelpers::init_logger(app_key, L"ModuleInterface", "AdvancedPaste");
        m_hShowUIEvent = CreateDefaultEvent(CommonSharedConstants::SHOW_ADVANCED_PASTE_SHARED_EVENT);
        m_hPasteMarkdownEvent = CreateDefaultEvent(CommonSharedConstants::ADVANCED_PASTE_MARKDOWN_EVENT);
        m_hPasteJsonEvent = CreateDefaultEvent(CommonSharedConstants::ADVANCED_PASTE_JSON_EVENT);
        init_settings();
    }

    ~AdvancedPaste()
    {
        if (m_enabled)
        {
        }
        m_enabled = false;
    }

    // Destroy the powertoy and free memory
    virtual void destroy() override
    {
        Logger::trace("AdvancedPaste::destroy()");
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
        return powertoys_gpo::getConfiguredAdvancedPasteEnabledValue();
    }

    virtual bool get_config(wchar_t* buffer, int* buffer_size) override
    {
        HINSTANCE hinstance = reinterpret_cast<HINSTANCE>(&__ImageBase);

        // Create a Settings object.
        PowerToysSettings::Settings settings(hinstance, get_name());
        settings.set_description(GET_RESOURCE_STRING(IDS_ADVANCED_PASTE_SETTINGS_DESC));

        settings.set_overview_link(L"https://aka.ms/PowerToysOverview_AdvancedPaste");

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

            parse_hotkeys(values);

            auto settingsObject = values.get_raw_json();
            if (settingsObject.GetView().Size() && settingsObject.GetNamedObject(JSON_KEY_PROPERTIES).HasKey(JSON_KEY_SHOW_CUSTOM_PREVIEW))
            {
                m_preview_custom_format_output = settingsObject.GetNamedObject(JSON_KEY_PROPERTIES).GetNamedObject(JSON_KEY_SHOW_CUSTOM_PREVIEW).GetNamedBoolean(JSON_KEY_VALUE);
            }

            // order of args matter
            Trace::AdvancedPaste_SettingsTelemetry(m_paste_as_plain_hotkey,
                                     m_advanced_paste_ui_hotkey,
                                     m_paste_as_markdown_hotkey,
                                     m_paste_as_json_hotkey,
                                     m_preview_custom_format_output);

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
        Logger::trace("AdvancedPaste::enable()");
        Trace::AdvancedPaste_Enable(true);
        ResetEvent(m_hShowUIEvent);
        ResetEvent(m_hPasteMarkdownEvent);
        ResetEvent(m_hPasteJsonEvent);
        m_enabled = true;

        launch_process();
    };

    virtual void disable()
    {
        Logger::trace("AdvancedPaste::disable()");
        if (m_enabled)
        {
            ResetEvent(m_hShowUIEvent);
            ResetEvent(m_hPasteMarkdownEvent);
            ResetEvent(m_hPasteJsonEvent);
            TerminateProcess(m_hProcess, 1);
            Trace::AdvancedPaste_Enable(false);

            CloseHandle(m_hProcess);
            m_hProcess = 0;
        }

        m_enabled = false;
    }

    virtual bool on_hotkey(size_t hotkeyId) override
    {
        Logger::trace(L"AdvancedPaste hotkey pressed");
        if (m_enabled)
        {
            if (!is_process_running())
            {
                Logger::trace(L"Launching new process");
                launch_process();

                Trace::AdvancedPaste_Invoked(L"AdvancedPasteUI");
            }

            // hotkeyId in same order as set by get_hotkeys
            if (hotkeyId == 0) { // m_paste_as_plain_hotkey
                Logger::trace(L"Paste as plain text hotkey pressed");

                std::thread([=]() {
                    // hotkey work should be kept to a minimum, or Windows might deregister our low level keyboard hook.
                    // Move work to another thread.
                    try_to_paste_as_plain_text();
                }).detach();

                Trace::AdvancedPaste_Invoked(L"PastePlainTextDirect");
                return true;
            }

            if (hotkeyId == 1) { // m_advanced_paste_ui_hotkey
                Logger::trace(L"Setting start up event");

                bring_process_to_front();
                SetEvent(m_hShowUIEvent);
                return true;
            }
            if (hotkeyId == 2) { // m_paste_as_markdown_hotkey
                Logger::trace(L"Starting paste as markdown directly");
                SetEvent(m_hPasteMarkdownEvent);
                return true;
            }
            if (hotkeyId == 3) { // m_paste_as_json_hotkey
                Logger::trace(L"Starting paste as json directly");
                SetEvent(m_hPasteJsonEvent);
                return true;
            }
        }

        return false;
    }

    virtual size_t get_hotkeys(Hotkey* hotkeys, size_t buffer_size) override
    {
        if (hotkeys && buffer_size >= 4)
        {
            hotkeys[0] = m_paste_as_plain_hotkey;
            hotkeys[1] = m_advanced_paste_ui_hotkey;
            hotkeys[2] = m_paste_as_markdown_hotkey;
            hotkeys[3] = m_paste_as_json_hotkey;
        }
        return 4;
    }

    virtual bool is_enabled() override
    {
        return m_enabled;
    }
};

extern "C" __declspec(dllexport) PowertoyModuleIface* __cdecl powertoy_create()
{
    return new AdvancedPaste();
}
