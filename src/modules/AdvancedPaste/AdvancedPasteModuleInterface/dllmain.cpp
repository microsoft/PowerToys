// dllmain.cpp : Defines the entry point for the DLL application.
#include "pch.h"

#include "AdvancedPasteConstants.h"
#include "AdvancedPasteProcessManager.h"
#include "HotkeyWork.h"
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
#include <common/utils/EventWaiter.h>

#include <algorithm>
#include <array>
#include <atomic>
#include <cwctype>
#include <chrono>
#include <functional>
#include <string_view>
#include <thread>
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
    const wchar_t JSON_KEY_PASTE_AS_FILE_ACTION[] = L"paste-as-file";
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
    const wchar_t JSON_KEY_IS_AI_ENABLED[] = L"IsAIEnabled";
    const wchar_t JSON_KEY_IS_OPEN_AI_ENABLED[] = L"IsOpenAIEnabled";
    const wchar_t JSON_KEY_SHOW_CUSTOM_PREVIEW[] = L"ShowCustomPreview";
    const wchar_t JSON_KEY_AUTO_COPY_SELECTION_CUSTOM_ACTION[] = L"AutoCopySelectionForCustomActionHotkey";
    const wchar_t JSON_KEY_PASTE_AI_CONFIGURATION[] = L"paste-ai-configuration";
    const wchar_t JSON_KEY_PROVIDERS[] = L"providers";
    const wchar_t JSON_KEY_SERVICE_TYPE[] = L"service-type";
    const wchar_t JSON_KEY_ENABLE_ADVANCED_AI[] = L"enable-advanced-ai";
    const wchar_t JSON_KEY_COACHING_SHORTCUT[] = L"coaching-shortcut";
    const wchar_t JSON_KEY_COACHING_ENABLED[] = L"coaching-enabled";
    const wchar_t JSON_KEY_VALUE[] = L"value";
}

class AdvancedPaste : public PowertoyModuleIface
{
private:
    
    AdvancedPasteProcessManager m_process_manager;
    std::atomic_bool m_enabled = false;

    std::wstring app_name;

    //contains the non localized key of the powertoy
    std::wstring app_key;

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

    bool m_is_ai_enabled = false;
    bool m_is_advanced_ai_enabled = false;
    bool m_preview_custom_format_output = true;
    std::atomic_bool m_auto_copy_selection_custom_action = false;
    advanced_paste::HotkeyWork m_hotkey_work;

    // Event listening for external triggers (e.g., from CmdPal extension)
    EventWaiter m_triggerEventWaiter;

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

    static Hotkey parse_single_hotkey(const winrt::Windows::Data::Json::JsonObject& jsonHotkeyObject, bool isShown = true)
    {
        try
        {
            Hotkey hotkey;
            hotkey.win = jsonHotkeyObject.GetNamedBoolean(JSON_KEY_WIN);
            hotkey.alt = jsonHotkeyObject.GetNamedBoolean(JSON_KEY_ALT);
            hotkey.shift = jsonHotkeyObject.GetNamedBoolean(JSON_KEY_SHIFT);
            hotkey.ctrl = jsonHotkeyObject.GetNamedBoolean(JSON_KEY_CTRL);
            hotkey.key = static_cast<unsigned char>(jsonHotkeyObject.GetNamedNumber(JSON_KEY_CODE));
            hotkey.isShown = isShown;
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

    bool is_ai_enabled()
    {
        return gpo_policy_enabled_configuration() != powertoys_gpo::gpo_rule_configured_disabled &&
               powertoys_gpo::getAllowedAdvancedPasteOnlineAIModelsValue() != powertoys_gpo::gpo_rule_configured_disabled &&
               m_is_ai_enabled;
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

    static std::wstring to_lower_case(const std::wstring& value)
    {
        std::wstring result = value;
        std::transform(result.begin(), result.end(), result.begin(), [](wchar_t ch) { return std::towlower(ch); });
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

    void process_additional_action(const winrt::hstring& actionName, const winrt::Windows::Data::Json::IJsonValue& actionValue, bool actionsGroupIsShown = true)
    {
        bool actionIsShown = true;

        if (actionValue.ValueType() != winrt::Windows::Data::Json::JsonValueType::Object)
        {
            return;
        }

        const auto action = actionValue.GetObjectW();

        if (!action.GetNamedBoolean(JSON_KEY_IS_SHOWN, false) || !actionsGroupIsShown)
        {
            actionIsShown = false;
        }

        if (action.HasKey(JSON_KEY_SHORTCUT))
        {
            const AdditionalAction additionalAction
            {
                actionName.c_str(),
                parse_single_hotkey(action.GetNamedObject(JSON_KEY_SHORTCUT), actionIsShown)
            };

            m_additional_actions.push_back(additionalAction);

            // Register coaching shortcut as a separate hotkey with a "-coaching" suffix ID
            if (action.HasKey(JSON_KEY_COACHING_SHORTCUT) && action.GetNamedBoolean(JSON_KEY_COACHING_ENABLED, false))
            {
                auto coachingHotkey = parse_single_hotkey(action.GetNamedObject(JSON_KEY_COACHING_SHORTCUT), actionIsShown);
                if (coachingHotkey.key != 0)
                {
                    const AdditionalAction coachingAction
                    {
                        std::wstring(actionName.c_str()) + L"-coaching",
                        coachingHotkey
                    };
                    m_additional_actions.push_back(coachingAction);
                }
            }
        }
        else if (actionName == JSON_KEY_PASTE_AS_FILE_ACTION)
        {
            // Hotkey IDs are positional and must match the order of AdvancedPastePasteAsFileAction.SubActions
            // in Settings, so walk the children in that order instead of the JSON object's iteration order.
            // Settings files written before the JPEG action existed have no entry for it, while Settings
            // deserializes a default one; reserve an inactive slot so the following IDs stay aligned.
            static const std::array<std::wstring_view, 4> pasteAsFileOrder{
                L"paste-as-txt-file",
                L"paste-as-png-file",
                L"paste-as-jpg-file",
                L"paste-as-html-file"
            };

            for (const std::wstring_view subActionName : pasteAsFileOrder)
            {
                const winrt::hstring subActionKey{ subActionName };
                if (action.HasKey(subActionKey))
                {
                    process_additional_action(subActionKey, action.GetNamedValue(subActionKey), actionIsShown);
                }
                else if (subActionName == L"paste-as-jpg-file")
                {
                    Hotkey inactiveHotkey{};
                    inactiveHotkey.isShown = false;
                    m_additional_actions.push_back({ std::wstring{ subActionName }, inactiveHotkey });
                }
            }
        }
        else
        {
            for (const auto& [subActionName, subAction] : action)
            {
                process_additional_action(subActionName, subAction, actionIsShown);
            }
        }
    }

    bool has_advanced_ai_provider(const winrt::Windows::Data::Json::JsonObject& propertiesObject)
    {
        if (!propertiesObject.HasKey(JSON_KEY_PASTE_AI_CONFIGURATION))
        {
            return false;
        }

        const auto configValue = propertiesObject.GetNamedValue(JSON_KEY_PASTE_AI_CONFIGURATION);
        if (configValue.ValueType() != winrt::Windows::Data::Json::JsonValueType::Object)
        {
            return false;
        }

        const auto configObject = configValue.GetObjectW();
        if (!configObject.HasKey(JSON_KEY_PROVIDERS))
        {
            return false;
        }

        const auto providersValue = configObject.GetNamedValue(JSON_KEY_PROVIDERS);
        if (providersValue.ValueType() != winrt::Windows::Data::Json::JsonValueType::Array)
        {
            return false;
        }

        const auto providers = providersValue.GetArray();
        for (const auto providerValue : providers)
        {
            if (providerValue.ValueType() != winrt::Windows::Data::Json::JsonValueType::Object)
            {
                continue;
            }

            const auto providerObject = providerValue.GetObjectW();
            if (!providerObject.GetNamedBoolean(JSON_KEY_ENABLE_ADVANCED_AI, false))
            {
                continue;
            }

            if (!providerObject.HasKey(JSON_KEY_SERVICE_TYPE))
            {
                continue;
            }

            const std::wstring serviceType = providerObject.GetNamedString(JSON_KEY_SERVICE_TYPE, L"").c_str();
            const auto normalizedServiceType = to_lower_case(serviceType);
            if (normalizedServiceType == L"openai" || normalizedServiceType == L"azureopenai")
            {
                return true;
            }
        }

        return false;
    }

    void read_settings(PowerToysSettings::PowerToyValues& settings)
    {
        const auto settingsObject = settings.get_raw_json();

        // Migrate Paste As Plain text shortcut
        Hotkey old_paste_as_plain_hotkey;
        bool old_data_migrated = migrate_data_and_remove_data_file(old_paste_as_plain_hotkey);

        if (settingsObject.GetView().Size())
        {
            const auto propertiesObject = settingsObject.GetNamedObject(JSON_KEY_PROPERTIES);

            m_is_advanced_ai_enabled = has_advanced_ai_provider(propertiesObject);

            if (propertiesObject.HasKey(JSON_KEY_IS_AI_ENABLED))
            {
                m_is_ai_enabled = propertiesObject.GetNamedObject(JSON_KEY_IS_AI_ENABLED).GetNamedBoolean(JSON_KEY_VALUE, false);
            }
            else if (propertiesObject.HasKey(JSON_KEY_IS_OPEN_AI_ENABLED))
            {
                m_is_ai_enabled = propertiesObject.GetNamedObject(JSON_KEY_IS_OPEN_AI_ENABLED).GetNamedBoolean(JSON_KEY_VALUE, false);
            }
            else
            {
                m_is_ai_enabled = false;
            }

            if (propertiesObject.HasKey(JSON_KEY_SHOW_CUSTOM_PREVIEW))
            {
                m_preview_custom_format_output = propertiesObject.GetNamedObject(JSON_KEY_SHOW_CUSTOM_PREVIEW).GetNamedBoolean(JSON_KEY_VALUE);
            }

            if (propertiesObject.HasKey(JSON_KEY_AUTO_COPY_SELECTION_CUSTOM_ACTION))
            {
                m_auto_copy_selection_custom_action = propertiesObject.GetNamedObject(JSON_KEY_AUTO_COPY_SELECTION_CUSTOM_ACTION).GetNamedBoolean(JSON_KEY_VALUE, false);
            }
        }

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

                        // Define the expected order to ensure consistent hotkey ID assignment
                        const std::vector<winrt::hstring> expectedOrder = {
                            L"image-to-text",
                            L"fix-spelling-and-grammar",
                            L"paste-as-file",
                            L"transcode"
                        };

                        // Process actions in the predefined order
                        for (auto& actionKey : expectedOrder)
                        {
                            if (additionalActions.HasKey(actionKey))
                            {
                                const auto actionValue = additionalActions.GetNamedValue(actionKey);
                                process_additional_action(actionKey, actionValue);
                            }
                        }
                    }

                    if (propertiesObject.HasKey(JSON_KEY_CUSTOM_ACTIONS))
                    {
                        const auto customActions = propertiesObject.GetNamedObject(JSON_KEY_CUSTOM_ACTIONS).GetNamedArray(JSON_KEY_VALUE);
                        if (customActions.Size() > 0 && is_ai_enabled())
                        {
                            for (const auto& customAction : customActions)
                            {
                                const auto object = customAction.GetObjectW();
                                bool actionIsShown = object.GetNamedBoolean(JSON_KEY_IS_SHOWN, false);

                                const CustomAction customActionData{
                                    static_cast<int>(object.GetNamedNumber(JSON_KEY_ID)),
                                    parse_single_hotkey(object.GetNamedObject(JSON_KEY_SHORTCUT), actionIsShown)
                                };

                                m_custom_actions.push_back(customActionData);
                            }
                        }
                    }
                }
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

            read_settings(settings);
        }
        catch (std::exception&)
        {
            Logger::warn(L"An exception occurred while loading the settings file");
            // Error while loading from the settings file. Let default values stay as they are.
        }
    }

    static constexpr std::array<short, 8> modifier_keys = { VK_LCONTROL, VK_RCONTROL, VK_LWIN, VK_RWIN, VK_LSHIFT, VK_RSHIFT, VK_LMENU, VK_RMENU };

    static bool is_key_down(short vk)
    {
        // Most significant bit is set if key is down
        return (GetAsyncKeyState(static_cast<int>(vk)) & 0x8000) != 0;
    }

    struct HotkeyTarget
    {
        HWND window = nullptr;
        HWND control = nullptr;
        DWORD thread_id = 0;
        DWORD process_id = 0;

        static HotkeyTarget capture()
        {
            HotkeyTarget target;
            target.window = GetForegroundWindow();
            target.thread_id = GetWindowThreadProcessId(target.window, &target.process_id);
            GUITHREADINFO info{ sizeof(GUITHREADINFO) };
            if (target.thread_id && GetGUIThreadInfo(target.thread_id, &info))
            {
                target.control = info.hwndFocus ? info.hwndFocus : info.hwndActive;
            }
            return target;
        }

        bool is_current() const
        {
            const auto current = capture();
            return window && control && window == current.window && control == current.control &&
                   thread_id == current.thread_id && process_id == current.process_id;
        }
    };

    static bool request_is_current(const HotkeyTarget& target, std::stop_token token)
    {
        return !token.stop_requested() && target.is_current();
    }

    // Never wait on the keyboard hook thread, or synthesize releases for physically held keys.
    static bool wait_for_modifiers_release(const HotkeyTarget& target, std::stop_token token)
    {
        const bool released = advanced_paste::wait_for_modifiers_release(
            token, [&] { return target.is_current(); }, [] { return std::any_of(modifier_keys.begin(), modifier_keys.end(), is_key_down); });
        if (!released)
        {
            Logger::trace(L"Advanced Paste request cancelled while waiting for modifiers");
        }
        return released;
    }

    bool try_send_copy_message(const HotkeyTarget& target, std::stop_token token)
    {
        if (!request_is_current(target, token))
        {
            return false;
        }

        DWORD_PTR result = 0;
        auto sendResult = SendMessageTimeout(target.control, WM_COPY, 0, 0, SMTO_ABORTIFHUNG | SMTO_BLOCK, 50, &result);
        return sendResult != 0;
    }

    // Helper: poll clipboard sequence number for a change from initial_sequence.
    // Returns true if the sequence number changed within the given number of polls.
    bool poll_clipboard_sequence(DWORD initial_sequence, int poll_attempts, std::chrono::milliseconds poll_delay, const HotkeyTarget& target, std::stop_token token)
    {
        for (int poll = 0; poll < poll_attempts; ++poll)
        {
            if (!request_is_current(target, token))
            {
                return false;
            }
            if (GetClipboardSequenceNumber() != initial_sequence)
            {
                return true;
            }
            std::this_thread::sleep_for(poll_delay);
        }
        return false;
    }

    bool send_ctrl_c_input(const HotkeyTarget& target, std::stop_token token)
    {
        if (!wait_for_modifiers_release(target, token))
        {
            return false;
        }

        std::vector<INPUT> inputs;

        // Ctrl down
        {
            INPUT input_event = {};
            input_event.type = INPUT_KEYBOARD;
            input_event.ki.wVk = VK_CONTROL;
            input_event.ki.dwExtraInfo = CENTRALIZED_KEYBOARD_HOOK_DONT_TRIGGER_FLAG;
            inputs.push_back(input_event);
        }

        // C down
        {
            INPUT input_event = {};
            input_event.type = INPUT_KEYBOARD;
            input_event.ki.wVk = 0x43; // C
            input_event.ki.dwExtraInfo = CENTRALIZED_KEYBOARD_HOOK_DONT_TRIGGER_FLAG;
            inputs.push_back(input_event);
        }

        // C up
        {
            INPUT input_event = {};
            input_event.type = INPUT_KEYBOARD;
            input_event.ki.wVk = 0x43; // C
            input_event.ki.dwFlags = KEYEVENTF_KEYUP;
            input_event.ki.dwExtraInfo = CENTRALIZED_KEYBOARD_HOOK_DONT_TRIGGER_FLAG;
            inputs.push_back(input_event);
        }

        // Ctrl up
        {
            INPUT input_event = {};
            input_event.type = INPUT_KEYBOARD;
            input_event.ki.wVk = VK_CONTROL;
            input_event.ki.dwFlags = KEYEVENTF_KEYUP;
            input_event.ki.dwExtraInfo = CENTRALIZED_KEYBOARD_HOOK_DONT_TRIGGER_FLAG;
            inputs.push_back(input_event);
        }

        // Prevent Start Menu from activating after Win key release
        INPUT dummyEvent = {};
        dummyEvent.type = INPUT_KEYBOARD;
        dummyEvent.ki.wVk = 0xFF;
        dummyEvent.ki.dwFlags = KEYEVENTF_KEYUP;
        inputs.push_back(dummyEvent);

        if (!request_is_current(target, token) ||
            std::any_of(modifier_keys.begin(), modifier_keys.end(), is_key_down))
        {
            return false;
        }
        auto uSent = SendInput(static_cast<UINT>(inputs.size()), inputs.data(), sizeof(INPUT));
        if (uSent != inputs.size())
        {
            DWORD errorCode = GetLastError();
            auto errorMessage = get_last_error_message(errorCode);
            Logger::error(L"SendInput failed for Ctrl+C. Expected to send {} inputs and sent only {}. {}", inputs.size(), uSent, errorMessage.has_value() ? errorMessage.value() : L"");
            Trace::AdvancedPaste_Error(errorCode, errorMessage.has_value() ? errorMessage.value() : L"", L"input.SendInput");
            return false;
        }
        return true;
    }

    // False cancels the request; an unchanged clipboard is still a valid best-effort outcome.
    bool send_copy_selection(const HotkeyTarget& target, std::stop_token token)
    {
        constexpr int copy_attempts = 2;
        constexpr auto copy_retry_delay = std::chrono::milliseconds(100);
        constexpr int clipboard_poll_attempts = 5;
        constexpr auto clipboard_poll_delay = std::chrono::milliseconds(30);

        bool copy_succeeded = false;
        for (int attempt = 0; attempt < copy_attempts; ++attempt)
        {
            if (!request_is_current(target, token))
            {
                return false;
            }
            const auto initial_sequence = GetClipboardSequenceNumber();

            // Strategy 1: Try WM_COPY message (works for standard Win32 controls)
            bool wm_copy_sent = try_send_copy_message(target, token);

            if (wm_copy_sent)
            {
                if (poll_clipboard_sequence(initial_sequence, clipboard_poll_attempts, clipboard_poll_delay, target, token))
                {
                    copy_succeeded = true;
                }
            }

            // Strategy 2: If WM_COPY didn't work, try SendInput Ctrl+C (works for Electron, browsers, etc.)
            if (!copy_succeeded)
            {
                const auto sequence_before_ctrl_c = GetClipboardSequenceNumber();

                if (!send_ctrl_c_input(target, token))
                {
                    return false;
                }
                if (poll_clipboard_sequence(sequence_before_ctrl_c, clipboard_poll_attempts, clipboard_poll_delay, target, token))
                {
                    copy_succeeded = true;
                }
            }

            if (copy_succeeded)
            {
                break;
            }

            if (attempt + 1 < copy_attempts)
            {
                std::this_thread::sleep_for(copy_retry_delay);
            }
        }

        if (!copy_succeeded)
        {
            Logger::warn(L"Auto-copy: all {} copy attempts failed — the target application did not update the clipboard after WM_COPY and Ctrl+C", copy_attempts);
        }

        return request_is_current(target, token);
    }

    void try_to_paste_as_plain_text(const HotkeyTarget& target, std::stop_token token)
    {
        if (!wait_for_modifiers_release(target, token))
        {
            return;
        }
        std::wstring clipboard_text;
        DWORD clipboard_sequence = 0;

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
            clipboard_sequence = GetClipboardSequenceNumber();

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

            if (!request_is_current(target, token) || GetClipboardSequenceNumber() != clipboard_sequence)
            {
                CloseClipboard();
                Logger::trace(L"Plain text paste cancelled: target or clipboard changed");
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

            clipboard_sequence = GetClipboardSequenceNumber();
            CloseClipboard();
            // Copy text to clipboard end
        }
        {
            if (!wait_for_modifiers_release(target, token))
            {
                return;
            }

            std::vector<INPUT> inputs;

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

            // Send a dummy key to prevent Start Menu from activating after the Win key release
            INPUT dummyEvent = {};
            dummyEvent.type = INPUT_KEYBOARD;
            dummyEvent.ki.wVk = 0xFF;
            dummyEvent.ki.dwFlags = KEYEVENTF_KEYUP;
            inputs.push_back(dummyEvent);

            if (!request_is_current(target, token) ||
                std::any_of(modifier_keys.begin(), modifier_keys.end(), is_key_down) ||
                GetClipboardSequenceNumber() != clipboard_sequence)
            {
                Logger::trace(L"Plain text paste cancelled: target, modifiers, or clipboard changed");
                return;
            }
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

    void queue_hotkey_work(std::function<void(const HotkeyTarget&, std::stop_token)> action, bool copy_selection, unsigned char key = 0, size_t hotkey_id = 1)
    {
        const auto target = HotkeyTarget::capture();
        m_hotkey_work.submit(hotkey_id, [this, target, copy_selection, key, action = std::move(action)](std::stop_token token) {
            if (request_is_current(target, token) && (!copy_selection || wait_for_modifiers_release(target, token)))
            {
                const bool copy_ready = !copy_selection || send_copy_selection(target, token);
                if (copy_ready && request_is_current(target, token))
                {
                    m_process_manager.start();
                    action(target, token);
                }
            }

            // Swallowed hotkey key-downs may not appear in GetAsyncKeyState. Also wait for
            // modifiers so auto-repeat cannot rearm a request that timed out while held.
            while (key && !token.stop_requested() &&
                   (is_key_down(key) || std::any_of(modifier_keys.begin(), modifier_keys.end(), is_key_down)))
            {
                std::this_thread::sleep_for(std::chrono::milliseconds(10));
            }
        });
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
        Disable(false);
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

    // Returns whether the PowerToys should be enabled by default
    virtual bool is_enabled_by_default() const override
    {
        return false;
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
        m_hotkey_work.start();
        m_enabled = true;
        m_process_manager.start();

        // Start listening for external trigger event so we can invoke the same logic as the hotkey.
        // Note: Use start() directly instead of constructor + move assignment to avoid dangling this pointer in the thread.
        m_triggerEventWaiter.start(CommonSharedConstants::ADVANCED_PASTE_SHOW_UI_EVENT, [this](DWORD error) {
            if (error != ERROR_SUCCESS)
            {
                Logger::error(L"Advanced Paste ShowUI event wait failed: {}", error);
                return;
            }
            // Same logic as hotkeyId == 1 (m_advanced_paste_ui_hotkey)
            Logger::trace(L"AdvancedPaste ShowUI event triggered");

            queue_hotkey_work([this](const HotkeyTarget&, std::stop_token) {
                m_process_manager.bring_to_front();
                m_process_manager.send_message(CommonSharedConstants::ADVANCED_PASTE_SHOW_UI_MESSAGE);
            },
                              m_auto_copy_selection_custom_action);
            Trace::AdvancedPaste_Invoked(L"AdvancedPasteUIEvent");
        });
    };

    void Disable(bool traceEvent)
    {
        if (m_enabled.exchange(false))
        {
            // Reject submissions and join work before stopping the process manager or unloading the DLL.
            m_hotkey_work.stop();
            m_triggerEventWaiter.stop();
            m_process_manager.stop();

            if (traceEvent)
            {
                Trace::AdvancedPaste_Enable(false);
            }
        }

    }

    virtual void disable()
    {
        Logger::trace("AdvancedPaste::disable()");
        Disable(true);
    }

    virtual bool on_hotkey(size_t hotkeyId) override
    {
        Logger::trace(L"AdvancedPaste hotkey pressed");
        if (!m_enabled)
        {
            return false;
        }

        // This runs synchronously inside the runner's low-level keyboard hook. Anything slow here
        // stalls all keyboard input and, with modifier injection, can leave modifiers stuck (#27612).
        // Resolve the action here, and run copy/paste work on a separate thread.
        std::function<void(const HotkeyTarget&, std::stop_token)> action;
        const wchar_t* trace_name = nullptr;
        unsigned char key = 0;

        // hotkeyId in same order as set by get_hotkeys
        if (hotkeyId == 0)
        { // m_paste_as_plain_hotkey
            Logger::trace(L"Paste as plain text hotkey pressed");
            action = [this](const HotkeyTarget& target, std::stop_token token) { try_to_paste_as_plain_text(target, token); };
            key = m_paste_as_plain_hotkey.key;
            trace_name = L"PastePlainTextDirect";
        }
        else if (hotkeyId == 1)
        { // m_advanced_paste_ui_hotkey
            key = m_advanced_paste_ui_hotkey.key;
            action = [this](const HotkeyTarget&, std::stop_token) {
                Logger::trace(L"Setting start up event");
                m_process_manager.bring_to_front();
                m_process_manager.send_message(CommonSharedConstants::ADVANCED_PASTE_SHOW_UI_MESSAGE);
            };
            trace_name = L"AdvancedPasteUI";
        }
        else if (hotkeyId == 2)
        { // m_paste_as_markdown_hotkey
            key = m_paste_as_markdown_hotkey.key;
            action = [this](const HotkeyTarget&, std::stop_token) {
                Logger::trace(L"Starting paste as markdown directly");
                m_process_manager.send_message(CommonSharedConstants::ADVANCED_PASTE_MARKDOWN_MESSAGE);
            };
            trace_name = L"MarkdownDirect";
        }
        else if (hotkeyId == 3)
        { // m_paste_as_json_hotkey
            key = m_paste_as_json_hotkey.key;
            action = [this](const HotkeyTarget&, std::stop_token) {
                Logger::trace(L"Starting paste as json directly");
                m_process_manager.send_message(CommonSharedConstants::ADVANCED_PASTE_JSON_MESSAGE);
            };
            trace_name = L"JsonDirect";
        }
        else
        {
            const size_t additional_action_index = hotkeyId - NUM_DEFAULT_HOTKEYS;
            if (additional_action_index < m_additional_actions.size())
            {
                const auto id = m_additional_actions.at(additional_action_index).id;
                key = m_additional_actions.at(additional_action_index).hotkey.key;
                Trace::AdvancedPaste_Invoked(std::format(L"{}Direct", kebab_to_pascal_case(id)));
                action = [this, id](const HotkeyTarget&, std::stop_token) {
                    Logger::trace(L"Starting additional action id={}", id);
                    m_process_manager.send_message(CommonSharedConstants::ADVANCED_PASTE_ADDITIONAL_ACTION_MESSAGE, id);
                };
            }
            else
            {
                const size_t custom_action_index = additional_action_index - m_additional_actions.size();
                if (custom_action_index >= m_custom_actions.size())
                {
                    return false;
                }

                const auto id = m_custom_actions.at(custom_action_index).id;
                key = m_custom_actions.at(custom_action_index).hotkey.key;
                action = [this, id](const HotkeyTarget&, std::stop_token) {
                    Logger::trace(L"Starting custom action id={}", id);
                    m_process_manager.send_message(CommonSharedConstants::ADVANCED_PASTE_CUSTOM_ACTION_MESSAGE, std::to_wstring(id));
                };
                trace_name = L"CustomActionDirect";
            }
        }

        // Try to capture selected text for all hotkey actions when the setting is enabled.
        // If nothing is selected (clipboard unchanged), fall through to use existing clipboard content.
        const bool copy_selection = m_auto_copy_selection_custom_action;

        queue_hotkey_work(std::move(action), copy_selection, key, hotkeyId);

        if (trace_name != nullptr)
        {
            Trace::AdvancedPaste_Invoked(trace_name);
        }

        return true;
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
