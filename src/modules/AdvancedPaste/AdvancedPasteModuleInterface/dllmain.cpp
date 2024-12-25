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
#include <common/utils/gpo.h>

#include <winrt/Windows.Security.Credentials.h>
#include <atlfile.h>
#include <atlstr.h>
#include <vector>

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
    const wchar_t JSON_KEY_CUSTOM_ACTIONS[] = L"custom-actions";
    const wchar_t JSON_KEY_ADDITIONAL_ACTIONS[] = L"additional-actions";
    const wchar_t JSON_KEY_SHORTCUT[] = L"shortcut";
    const wchar_t JSON_KEY_IS_SHOWN[] = L"isShown";
    const wchar_t JSON_KEY_ID[] = L"id";
    const wchar_t JSON_KEY_WIN[] = L"win";
    const wchar_t JSON_KEY_ALT[] = L"alt";
    const wchar_t JSON_KEY_CTRL[] = L"ctrl";
    const wchar_t JSON_KEY_SHIFT[] = L"shift";
    const wchar_t JSON_KEY_CODE[] = L"code";
    const wchar_t JSON_KEY_PASTE_AS_PLAIN_HOTKEY[] = L"paste-as-plain-hotkey";
    const wchar_t JSON_KEY_ADVANCED_PASTE_UI_HOTKEY[] = L"advanced-paste-ui-hotkey";
    const wchar_t JSON_KEY_PASTE_AS_MARKDOWN_HOTKEY[] = L"paste-as-markdown-hotkey";
    const wchar_t JSON_KEY_PASTE_AS_JSON_HOTKEY[] = L"paste-as-json-hotkey";
    const wchar_t JSON_KEY_IS_ADVANCED_AI_ENABLED[] = L"IsAdvancedAIEnabled";
    const wchar_t JSON_KEY_SHOW_CUSTOM_PREVIEW[] = L"ShowCustomPreview";
    const wchar_t JSON_KEY_VALUE[] = L"value";

    const wchar_t OPENAI_VAULT_RESOURCE[] = L"https://platform.openai.com/api-keys";
    const wchar_t OPENAI_VAULT_USERNAME[] = L"PowerToys_AdvancedPaste_OpenAIKey";
}

class AdvancedPaste : public PowertoyModuleIface
{
private:
    bool m_enabled = false;

    std::wstring app_name;

    //contains the non localized key of the powertoy
    std::wstring app_key;

    HANDLE m_hProcess;

    std::unique_ptr<CAtlFile> m_write_pipe;

    // Time to wait for process to close after sending WM_CLOSE signal
    static const constexpr int MAX_WAIT_MILLISEC = 10000;

    static const constexpr int NUM_DEFAULT_HOTKEYS = 4;

    Hotkey m_paste_as_plain_hotkey = { .win = true, .ctrl = true, .shift = false, .alt = true, .key = 'V' };
    Hotkey m_advanced_paste_ui_hotkey = { .win = true, .ctrl = false, .shift = true, .alt = false, .key = 'V' };
    Hotkey m_paste_as_markdown_hotkey{};
    Hotkey m_paste_as_json_hotkey{};

    template<class Id>
    struct ActionData
    {
        Id id;
        Hotkey hotkey;
    };

    using AdditionalAction = ActionData<std::wstring>;
    std::vector<AdditionalAction> m_additional_actions;

    using CustomAction = ActionData<int>;
    std::vector<CustomAction> m_custom_actions;

    bool m_is_advanced_ai_enabled = false;
    bool m_preview_custom_format_output = true;

    Hotkey parse_single_hotkey(const wchar_t* keyName, const winrt::Windows::Data::Json::JsonObject& settingsObject)
    {
        try
        {
            const auto jsonHotkeyObject = settingsObject.GetNamedObject(JSON_KEY_PROPERTIES).GetNamedObject(keyName);
            return parse_single_hotkey(jsonHotkeyObject);
        }
        catch (...)
        {
            Logger::error("Failed to initialize AdvancedPaste shortcut from settings. Value will keep unchanged.");
        }

        return {};
    }

    static Hotkey parse_single_hotkey(const winrt::Windows::Data::Json::JsonObject& jsonHotkeyObject)
    {
        try
        {
            Hotkey hotkey;
            hotkey.win = jsonHotkeyObject.GetNamedBoolean(JSON_KEY_WIN);
            hotkey.alt = jsonHotkeyObject.GetNamedBoolean(JSON_KEY_ALT);
            hotkey.shift = jsonHotkeyObject.GetNamedBoolean(JSON_KEY_SHIFT);
            hotkey.ctrl = jsonHotkeyObject.GetNamedBoolean(JSON_KEY_CTRL);
            hotkey.key = static_cast<unsigned char>(jsonHotkeyObject.GetNamedNumber(JSON_KEY_CODE));
            return hotkey;
        }
        catch (...)
        {
            Logger::error("Failed to initialize AdvancedPaste shortcut from settings. Value will keep unchanged.");
        }

        return {};
    }

    static json::JsonObject to_json_object(const Hotkey& hotkey)
    {
        json::JsonObject jsonObject;
        jsonObject.SetNamedValue(JSON_KEY_WIN, json::value(hotkey.win));
        jsonObject.SetNamedValue(JSON_KEY_ALT, json::value(hotkey.alt));
        jsonObject.SetNamedValue(JSON_KEY_SHIFT, json::value(hotkey.shift));
        jsonObject.SetNamedValue(JSON_KEY_CTRL, json::value(hotkey.ctrl));
        jsonObject.SetNamedValue(JSON_KEY_CODE, json::value(hotkey.key));

        return jsonObject;
    }

    static bool open_ai_key_exists()
    {
        try
        {
            winrt::Windows::Security::Credentials::PasswordVault().Retrieve(OPENAI_VAULT_RESOURCE, OPENAI_VAULT_USERNAME);
            return true;
        }
        catch (const winrt::hresult_error& ex)
        {
            // Looks like the only way to access the PasswordVault is through an API that throws an exception in case the resource doesn't exist.
            // If the debugger breaks here, just continue.
            // If you want to disable breaking here in a more permanent way, just add a condition in Visual Studio's Exception Settings to not break on win::hresult_error, but that might make you not hit other exceptions you might want to catch.
            if (ex.code() == HRESULT_FROM_WIN32(ERROR_NOT_FOUND))
            {
                return false; // Credential doesn't exist.
            }
            Logger::error("Unexpected error while retrieving OpenAI key from vault: {}", winrt::to_string(ex.message()));
            return false;
        }
    }

    bool is_open_ai_enabled()
    {
        return gpo_policy_enabled_configuration() != powertoys_gpo::gpo_rule_configured_disabled &&
               powertoys_gpo::getAllowedAdvancedPasteOnlineAIModelsValue() != powertoys_gpo::gpo_rule_configured_disabled &&
               open_ai_key_exists();
    }

    static std::wstring kebab_to_pascal_case(const std::wstring& kebab_str)
    {
        std::wstring result;
        bool capitalize_next = true;

        for (const auto ch : kebab_str)
        {
            if (ch == L'-')
            {
                capitalize_next = true;
            }
            else
            {
                if (capitalize_next)
                {
                    result += std::towupper(ch);
                    capitalize_next = false;
                }
                else
                {
                    result += ch;
                }
            }
        }

        return result;
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

    void process_additional_action(const winrt::hstring& actionName, const winrt::Windows::Data::Json::IJsonValue& actionValue)
    {
        if (actionValue.ValueType() != winrt::Windows::Data::Json::JsonValueType::Object)
        {
            return;
        }

        const auto action = actionValue.GetObjectW();

        if (!action.GetNamedBoolean(JSON_KEY_IS_SHOWN, false))
        {
            return;
        }

        if (action.HasKey(JSON_KEY_SHORTCUT))
        {
            const AdditionalAction additionalAction
            {
                actionName.c_str(),
                parse_single_hotkey(action.GetNamedObject(JSON_KEY_SHORTCUT))
            };

            m_additional_actions.push_back(additionalAction);
        }
        else
        {
            for (const auto& [subActionName, subAction] : action)
            {
                process_additional_action(subActionName, subAction);
            }
        }
    }

    void read_settings(PowerToysSettings::PowerToyValues& settings)
    {
        const auto settingsObject = settings.get_raw_json();

        // Migrate Paste As Plain text shortcut
        Hotkey old_paste_as_plain_hotkey;
        bool old_data_migrated = migrate_data_and_remove_data_file(old_paste_as_plain_hotkey);
        if (old_data_migrated)
        {
            m_paste_as_plain_hotkey = old_paste_as_plain_hotkey;

            // override settings file
            const auto new_hotkey_value = to_json_object(old_paste_as_plain_hotkey);

            if (!settingsObject.HasKey(JSON_KEY_PROPERTIES))
            {
                settingsObject.SetNamedValue(JSON_KEY_PROPERTIES, json::JsonObject{});
            }

            settingsObject.GetNamedObject(JSON_KEY_PROPERTIES).SetNamedValue(JSON_KEY_PASTE_AS_PLAIN_HOTKEY, new_hotkey_value);

            const auto ui_hotkey = to_json_object(m_advanced_paste_ui_hotkey);
            settingsObject.GetNamedObject(JSON_KEY_PROPERTIES).SetNamedValue(JSON_KEY_ADVANCED_PASTE_UI_HOTKEY, ui_hotkey);

            settings.save_to_settings_file();
        }
        else
        {
            if (settingsObject.GetView().Size())
            {
                const std::array<std::pair<Hotkey*, LPCWSTR>, NUM_DEFAULT_HOTKEYS> defaultHotkeys{
                    { { &m_paste_as_plain_hotkey, JSON_KEY_PASTE_AS_PLAIN_HOTKEY },
                      { &m_advanced_paste_ui_hotkey, JSON_KEY_ADVANCED_PASTE_UI_HOTKEY },
                      { &m_paste_as_markdown_hotkey, JSON_KEY_PASTE_AS_MARKDOWN_HOTKEY },
                      { &m_paste_as_json_hotkey, JSON_KEY_PASTE_AS_JSON_HOTKEY } }
                };

                for (auto& [hotkey, keyName] : defaultHotkeys)
                {
                    *hotkey = parse_single_hotkey(keyName, settingsObject);
                }

                m_additional_actions.clear();
                m_custom_actions.clear();

                if (settingsObject.HasKey(JSON_KEY_PROPERTIES))
                {
                    const auto propertiesObject = settingsObject.GetNamedObject(JSON_KEY_PROPERTIES);

                    if (propertiesObject.HasKey(JSON_KEY_ADDITIONAL_ACTIONS))
                    {
                        const auto additionalActions = propertiesObject.GetNamedObject(JSON_KEY_ADDITIONAL_ACTIONS);

                        for (const auto& [actionName, additionalAction] : additionalActions)
                        {
                            process_additional_action(actionName, additionalAction);
                        }
                    }

                    if (propertiesObject.HasKey(JSON_KEY_CUSTOM_ACTIONS))
                    {
                        const auto customActions = propertiesObject.GetNamedObject(JSON_KEY_CUSTOM_ACTIONS).GetNamedArray(JSON_KEY_VALUE);
                        if (customActions.Size() > 0 && is_open_ai_enabled())
                        {
                            for (const auto& customAction : customActions)
                            {
                                const auto object = customAction.GetObjectW();

                                if (object.GetNamedBoolean(JSON_KEY_IS_SHOWN, false))
                                {
                                    const CustomAction customActionData
                                    {
                                        static_cast<int>(object.GetNamedNumber(JSON_KEY_ID)),
                                        parse_single_hotkey(object.GetNamedObject(JSON_KEY_SHORTCUT))
                                    };

                                    m_custom_actions.push_back(customActionData);
                                }
                            }
                        }
                    }
                }
            }
        }

        if (settingsObject.GetView().Size())
        {
            const auto propertiesObject = settingsObject.GetNamedObject(JSON_KEY_PROPERTIES);

            if (propertiesObject.HasKey(JSON_KEY_IS_ADVANCED_AI_ENABLED))
            {
                m_is_advanced_ai_enabled = propertiesObject.GetNamedObject(JSON_KEY_IS_ADVANCED_AI_ENABLED).GetNamedBoolean(JSON_KEY_VALUE);
            }

            if (propertiesObject.HasKey(JSON_KEY_SHOW_CUSTOM_PREVIEW))
            {
                m_preview_custom_format_output = propertiesObject.GetNamedObject(JSON_KEY_SHOW_CUSTOM_PREVIEW).GetNamedBoolean(JSON_KEY_VALUE);
            }
        }
    }

    bool is_process_running() const
    {
        return WaitForSingleObject(m_hProcess, 0) == WAIT_TIMEOUT;
    }

    void launch_process(const std::wstring& pipe_name)
    {
        Logger::trace(L"Starting AdvancedPaste process");
        const unsigned long powertoys_pid = GetCurrentProcessId();

        const auto executable_args = std::format(L"{} {}", std::to_wstring(powertoys_pid), pipe_name);

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

    std::optional<std::wstring> get_pipe_name(const std::wstring& prefix) const
    {
        UUID temp_uuid;
        wchar_t* uuid_chars = nullptr;
        if (UuidCreate(&temp_uuid) == RPC_S_UUID_NO_ADDRESS)
        {
            const auto val = get_last_error_message(GetLastError());
            Logger::error(L"UuidCreate cannot create guid. {}", val.has_value() ? val.value() : L"");
            return std::nullopt;
        }
        else if (UuidToString(&temp_uuid, reinterpret_cast<RPC_WSTR*>(&uuid_chars)) != RPC_S_OK)
        {
            const auto val = get_last_error_message(GetLastError());
            Logger::error(L"UuidToString cannot convert to string. {}", val.has_value() ? val.value() : L"");
            return std::nullopt;
        }

        const auto pipe_name = std::format(L"{}{}", prefix, std::wstring(uuid_chars));
        RpcStringFree(reinterpret_cast<RPC_WSTR*>(&uuid_chars));

        return pipe_name;
    }

    void launch_process_and_named_pipe()
    {
        const auto pipe_name = get_pipe_name(L"powertoys_advanced_paste_");

        if (!pipe_name)
        {
            return;
        }

        std::thread create_pipe_thread ([&]{ start_named_pipe_server(pipe_name.value()); });
        launch_process(pipe_name.value());
        create_pipe_thread.join();
    }

    void send_named_pipe_message(const std::wstring& message_type, const std::wstring& message_arg = L"")
    {
        if (m_write_pipe)
        {
            const auto message = message_arg.empty() ? std::format(L"{}\r\n", message_type) : std::format(L"{} {}\r\n", message_type, message_arg);

            const CString file_name(message.c_str());
            m_write_pipe->Write(file_name, file_name.GetLength() * sizeof(TCHAR));
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

            read_settings(settings);
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

    void try_inject_modifier_key_restore(std::vector<INPUT>& inputs, short modifier)
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

    HRESULT start_named_pipe_server(const std::wstring& pipe_name)
    {
        const constexpr DWORD BUFSIZE = 4096 * 4;

        const auto full_pipe_name = std::format(L"\\\\.\\pipe\\{}", pipe_name);

        const auto hPipe = CreateNamedPipe(
                full_pipe_name.c_str(),     // pipe name
                PIPE_ACCESS_OUTBOUND,       // write access
                PIPE_TYPE_MESSAGE |         // message type pipe
                    PIPE_READMODE_MESSAGE | // message-read mode
                    PIPE_WAIT,              // blocking mode
                1,                          // max. instances
                BUFSIZE,                    // output buffer size
                0,                          // input buffer size
                0,                          // client time-out
                NULL);                      // default security attribute

        if (hPipe == NULL || hPipe == INVALID_HANDLE_VALUE)
        {
            return E_FAIL;
        }

        // This call blocks until a client process connects to the pipe
        BOOL connected = ConnectNamedPipe(hPipe, NULL);
        if (!connected)
        {
            if (GetLastError() == ERROR_PIPE_CONNECTED)
            {
                return S_OK;
            }
            else
            {
                CloseHandle(hPipe);
            }
            return E_FAIL;
        }

        m_write_pipe = std::make_unique<CAtlFile>(hPipe);
        return S_OK;
    }

public:
    AdvancedPaste()
    {
        app_name = GET_RESOURCE_STRING(IDS_ADVANCED_PASTE_NAME);
        app_key = AdvancedPasteConstants::ModuleKey;
        LoggerHelpers::init_logger(app_key, L"ModuleInterface", "AdvancedPaste");
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
        Disable(false);

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

            read_settings(values);

            std::unordered_map<std::wstring, Hotkey> additionalActionMap;
            for (const auto& action : m_additional_actions)
            {
                additionalActionMap[kebab_to_pascal_case(action.id)] = action.hotkey;
            }

            // order of args matter
            Trace::AdvancedPaste_SettingsTelemetry(m_paste_as_plain_hotkey,
                                                   m_advanced_paste_ui_hotkey,
                                                   m_paste_as_markdown_hotkey,
                                                   m_paste_as_json_hotkey,
                                                   m_is_advanced_ai_enabled,
                                                   m_preview_custom_format_output,
                                                   additionalActionMap);

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
        m_enabled = true;

        launch_process_and_named_pipe();
    };

    void Disable(bool traceEvent)
    {
        if (m_enabled)
        {
            send_named_pipe_message(CommonSharedConstants::ADVANCED_PASTE_TERMINATE_APP_MESSAGE);
            WaitForSingleObject(m_hProcess, 1500);

            m_write_pipe = nullptr;

            TerminateProcess(m_hProcess, 1);

            if (traceEvent)
            {
                Trace::AdvancedPaste_Enable(false);
            }

            CloseHandle(m_hProcess);
            m_hProcess = 0;
        }

        m_enabled = false;
    }

    virtual void disable()
    {
        Logger::trace("AdvancedPaste::disable()");
        Disable(true);
    }

    virtual bool on_hotkey(size_t hotkeyId) override
    {
        Logger::trace(L"AdvancedPaste hotkey pressed");
        if (m_enabled)
        {
            if (!is_process_running())
            {
                Logger::trace(L"Launching new process");
                launch_process_and_named_pipe();
            }

            // hotkeyId in same order as set by get_hotkeys
            if (hotkeyId == 0)
            { // m_paste_as_plain_hotkey
                Logger::trace(L"Paste as plain text hotkey pressed");

                std::thread([=]() {
                    // hotkey work should be kept to a minimum, or Windows might deregister our low level keyboard hook.
                    // Move work to another thread.
                    try_to_paste_as_plain_text();
                }).detach();

                Trace::AdvancedPaste_Invoked(L"PastePlainTextDirect");
                return true;
            }

            if (hotkeyId == 1)
            { // m_advanced_paste_ui_hotkey
                Logger::trace(L"Setting start up event");

                bring_process_to_front();
                send_named_pipe_message(CommonSharedConstants::ADVANCED_PASTE_SHOW_UI_MESSAGE);
                Trace::AdvancedPaste_Invoked(L"AdvancedPasteUI");
                return true;
            }
            if (hotkeyId == 2)
            { // m_paste_as_markdown_hotkey
                Logger::trace(L"Starting paste as markdown directly");
                send_named_pipe_message(CommonSharedConstants::ADVANCED_PASTE_MARKDOWN_MESSAGE);
                Trace::AdvancedPaste_Invoked(L"MarkdownDirect");
                return true;
            }
            if (hotkeyId == 3)
            { // m_paste_as_json_hotkey
                Logger::trace(L"Starting paste as json directly");
                send_named_pipe_message(CommonSharedConstants::ADVANCED_PASTE_JSON_MESSAGE);
                Trace::AdvancedPaste_Invoked(L"JsonDirect");
                return true;
            }


            const auto additional_action_index = hotkeyId - NUM_DEFAULT_HOTKEYS;
            if (additional_action_index < m_additional_actions.size())
            {
                const auto& id = m_additional_actions.at(additional_action_index).id;

                Logger::trace(L"Starting additional action id={}", id);

                Trace::AdvancedPaste_Invoked(std::format(L"{}Direct", kebab_to_pascal_case(id)));

                send_named_pipe_message(CommonSharedConstants::ADVANCED_PASTE_ADDITIONAL_ACTION_MESSAGE, id);
                return true;
            }

            const auto custom_action_index = additional_action_index - m_additional_actions.size();
            if (custom_action_index < m_custom_actions.size())
            {
                const auto id = m_custom_actions.at(custom_action_index).id;

                Logger::trace(L"Starting custom action id={}", id);

                send_named_pipe_message(CommonSharedConstants::ADVANCED_PASTE_CUSTOM_ACTION_MESSAGE, std::to_wstring(id));
                Trace::AdvancedPaste_Invoked(L"CustomActionDirect");
                return true;
            }
        }

        return false;
    }

    virtual size_t get_hotkeys(Hotkey* hotkeys, size_t buffer_size) override
    {
        const size_t num_hotkeys = NUM_DEFAULT_HOTKEYS + m_additional_actions.size() + m_custom_actions.size();

        if (hotkeys && buffer_size >= num_hotkeys)
        {
            const std::array default_hotkeys = { m_paste_as_plain_hotkey,
                                                 m_advanced_paste_ui_hotkey,
                                                 m_paste_as_markdown_hotkey,
                                                 m_paste_as_json_hotkey };
            std::copy(default_hotkeys.begin(), default_hotkeys.end(), hotkeys);

            const auto get_action_hotkey = [](const auto& action) { return action.hotkey; };
            std::transform(m_additional_actions.begin(), m_additional_actions.end(), hotkeys + NUM_DEFAULT_HOTKEYS, get_action_hotkey);
            std::transform(m_custom_actions.begin(), m_custom_actions.end(), hotkeys + NUM_DEFAULT_HOTKEYS + m_additional_actions.size(), get_action_hotkey);
        }

        return num_hotkeys;
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
