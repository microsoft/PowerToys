#include "pch.h"
#include <interface/powertoy_module_interface.h>
#include <interface/lowlevel_keyboard_event_data.h>
#include <interface/win_hook_event_data.h>
#include <common/settings_objects.h>
#include "trace.h"

extern "C" IMAGE_DOS_HEADER __ImageBase;

BOOL APIENTRY DllMain(HMODULE hModule, DWORD ul_reason_for_call, LPVOID lpReserved)
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

// The PowerToy name that will be shown in the settings.
const static wchar_t* MODULE_NAME = L"PowerKeys";
// Add a description that will we shown in the module settings page.
const static wchar_t* MODULE_DESC = L"Customize your experience by remapping keys or creating new shortcuts!";

// These are the properties shown in the Settings page.
struct ModuleSettings
{
    // Add the PowerToy module properties with default values.
    // Currently available types:
    // - int
    // - bool
    // - string

    //bool bool_prop = true;
    //int int_prop = 10;
    //std::wstring string_prop = L"The quick brown fox jumps over the lazy dog";
    //std::wstring color_prop = L"#1212FF";

} g_settings;

// Implement the PowerToy Module Interface and all the required methods.
class PowerKeys : public PowertoyModuleIface
{
private:
    // The PowerToy state.
    bool m_enabled = false;
    std::unordered_map<DWORD, WORD> singleKeyReMap;
    std::map<std::vector<DWORD>, std::pair<std::vector<WORD>, bool>> osLevelShortcutReMap;
    // Maps application name to the shortcut map
    std::map<std::wstring, std::map<std::vector<DWORD>, std::pair<std::vector<WORD>, bool>>> appSpecificShortcutReMap;
    std::unordered_map<DWORD, bool> singleKeyToggleToMod;
    // Load initial settings from the persisted values.
    void init_settings();
    static const ULONG_PTR POWERKEYS_INJECTED_FLAG = 0x1;
    static const ULONG_PTR POWERKEYS_SINGLEKEY_FLAG = 0x11;
    static const ULONG_PTR POWERKEYS_SHORTCUT_FLAG = 0x101;
    static const DWORD DUMMY_KEY = 0xCF;
    static HHOOK hook_handle;
    static HHOOK hook_handle_copy; // make sure we do use nullptr in CallNextHookEx call
    static PowerKeys* powerkeys_object_ptr;

public:
    // Constructor
    PowerKeys()
    {
        init_settings();
        init_map();
        powerkeys_object_ptr = this;
    };

    void init_map()
    {
        // If mapped to 0x0 then key is disabled.
        //singleKeyReMap[0x41] = 0x42;
        //singleKeyReMap[0x42] = 0x43;
        //singleKeyReMap[0x43] = 0x41;
        singleKeyReMap[VK_LWIN] = VK_LCONTROL;
        singleKeyReMap[VK_LCONTROL] = VK_LWIN;
        singleKeyReMap[VK_OEM_4] = 0x0;
        singleKeyToggleToMod[VK_CAPITAL] = false;

        // OS-level shortcut remappings
        osLevelShortcutReMap[std::vector<DWORD>({ VK_LMENU, 0x44 })] = std::make_pair(std::vector<WORD>({ VK_LCONTROL, 0x56 }), false);
        osLevelShortcutReMap[std::vector<DWORD>({ VK_LMENU, 0x45 })] = std::make_pair(std::vector<WORD>({ VK_LCONTROL, 0x58 }), false);
        //osLevelShortcutReMap[std::vector<DWORD>({ VK_LWIN, 0x46 })] = std::make_pair(std::vector<WORD>({ VK_LWIN, 0x53 }), false);
        /*osLevelShortcutReMap[std::vector<DWORD>({ VK_LWIN, 0x41 })] = std::make_pair(std::vector<WORD>({ VK_LCONTROL, 0x58 }), false);
        osLevelShortcutReMap[std::vector<DWORD>({ VK_LCONTROL, 0x58 })] = std::make_pair(std::vector<WORD>({ VK_LWIN, 0x41 }), false);*/

        osLevelShortcutReMap[std::vector<DWORD>({ VK_LWIN, 0x41 })] = std::make_pair(std::vector<WORD>({ VK_LCONTROL, 0x58 }), false);
        osLevelShortcutReMap[std::vector<DWORD>({ VK_LCONTROL, 0x58 })] = std::make_pair(std::vector<WORD>({ VK_LMENU, 0x44 }), false);
        osLevelShortcutReMap[std::vector<DWORD>({ VK_LCONTROL, 0x56 })] = std::make_pair(std::vector<WORD>({ VK_LWIN, 0x41 }), false);

        //App-specific shortcut remappings
        appSpecificShortcutReMap[L"msedge.exe"][std::vector<DWORD>({ VK_LCONTROL, 0x43 })] = std::make_pair(std::vector<WORD>({ VK_LCONTROL, 0x56 }), false); // Ctrl+C to Ctrl+V
        appSpecificShortcutReMap[L"msedge.exe"][std::vector<DWORD>({ VK_LMENU, 0x44 })] = std::make_pair(std::vector<WORD>({ VK_LCONTROL, 0x46 }), false); // Alt+D to Ctrl+F
        appSpecificShortcutReMap[L"OUTLOOK.EXE"][std::vector<DWORD>({ VK_LCONTROL, 0x46 })] = std::make_pair(std::vector<WORD>({ VK_LCONTROL, 0x45 }), false); // Ctrl+F to Ctrl+E
        appSpecificShortcutReMap[L"MicrosoftEdge.exe"][std::vector<DWORD>({ VK_LCONTROL, 0x58 })] = std::make_pair(std::vector<WORD>({ VK_LCONTROL, 0x56 }), false); // Ctrl+X to Ctrl+V
        appSpecificShortcutReMap[L"Calculator.exe"][std::vector<DWORD>({ VK_LCONTROL, 0x47 })] = std::make_pair(std::vector<WORD>({ VK_LSHIFT, 0x32 }), false); // Ctrl+G to Shift+2
    }

    // Destroy the powertoy and free memory
    virtual void destroy() override
    {
        stop_lowlevel_keyboard_hook();
        delete this;
    }

    // Return the display name of the powertoy, this will be cached by the runner
    virtual const wchar_t* get_name() override
    {
        return MODULE_NAME;
    }

    // Return array of the names of all events that this powertoy listens for, with
    // nullptr as the last element of the array. Nullptr can also be retured for empty
    // list.
    virtual const wchar_t** get_events() override
    {
        //static const wchar_t* events[] = { nullptr };
        // Available events:
        // - ll_keyboard
        // - win_hook_event
        //
        static const wchar_t* events[] = { ll_keyboard, nullptr };

        return events;
    }

    // Return JSON with the configuration options.
    virtual bool get_config(wchar_t* buffer, int* buffer_size) override
    {
        HINSTANCE hinstance = reinterpret_cast<HINSTANCE>(&__ImageBase);

        // Create a Settings object.
        PowerToysSettings::Settings settings(hinstance, get_name());
        settings.set_description(MODULE_DESC);

        // Show an overview link in the Settings page
        //settings.set_overview_link(L"https://");

        // Show a video link in the Settings page.
        //settings.set_video_link(L"https://");

        // A bool property with a toggle editor.
        //settings.add_bool_toogle(
        //  L"bool_toggle_1", // property name.
        //  L"This is what a BoolToggle property looks like", // description or resource id of the localized string.
        //  g_settings.bool_prop // property value.
        //);

        // An integer property with a spinner editor.
        //settings.add_int_spinner(
        //  L"int_spinner_1", // property name
        //  L"This is what a IntSpinner property looks like", // description or resource id of the localized string.
        //  g_settings.int_prop, // property value.
        //  0, // min value.
        //  100, // max value.
        //  10 // incremental step.
        //);

        // A string property with a textbox editor.
        //settings.add_string(
        //  L"string_text_1", // property name.
        //  L"This is what a String property looks like", // description or resource id of the localized string.
        //  g_settings.string_prop // property value.
        //);

        // A string property with a color picker editor.
        //settings.add_color_picker(
        //  L"color_picker_1", // property name.
        //  L"This is what a ColorPicker property looks like", // description or resource id of the localized string.
        //  g_settings.color_prop // property value.
        //);

        // A custom action property. When using this settings type, the "PowertoyModuleIface::call_custom_action()"
        // method should be overriden as well.
        //settings.add_custom_action(
        //  L"custom_action_id", // action name.
        //  L"This is what a CustomAction property looks like", // label above the field.
        //  L"Call a custom action", // button text.
        //  L"Press the button to call a custom action." // display values / extended info.
        //);

        return settings.serialize_to_buffer(buffer, buffer_size);
    }

    // Signal from the Settings editor to call a custom action.
    // This can be used to spawn more complex editors.
    virtual void call_custom_action(const wchar_t* action) override
    {
        static UINT custom_action_num_calls = 0;
        try
        {
            // Parse the action values, including name.
            PowerToysSettings::CustomActionObject action_object =
                PowerToysSettings::CustomActionObject::from_json_string(action);

            //if (action_object.get_name() == L"custom_action_id") {
            //  // Execute your custom action
            //}
        }
        catch (std::exception&)
        {
            // Improper JSON.
        }
    }

    // Called by the runner to pass the updated settings values as a serialized JSON.
    virtual void set_config(const wchar_t* config) override
    {
        try
        {
            // Parse the input JSON string.
            PowerToysSettings::PowerToyValues values =
                PowerToysSettings::PowerToyValues::from_json_string(config);

            // Update a bool property.
            //if (auto v = values.get_bool_value(L"bool_toggle_1")) {
            //  g_settings.bool_prop = *v;
            //}

            // Update an int property.
            //if (auto v = values.get_int_value(L"int_spinner_1")) {
            //  g_settings.int_prop = *v;
            //}

            // Update a string property.
            //if (auto v = values.get_string_value(L"string_text_1")) {
            //  g_settings.string_prop = *v;
            //}

            // Update a color property.
            //if (auto v = values.get_string_value(L"color_picker_1")) {
            //  g_settings.color_prop = *v;
            //}

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

    // Enable the powertoy
    virtual void enable()
    {
        m_enabled = true;
        start_lowlevel_keyboard_hook();
    }

    // Disable the powertoy
    virtual void disable()
    {
        m_enabled = false;
        stop_lowlevel_keyboard_hook();
    }

    // Returns if the powertoys is enabled
    virtual bool is_enabled() override
    {
        return m_enabled;
    }

    // Handle incoming event, data is event-specific
    virtual intptr_t signal_event(const wchar_t* name, intptr_t data) override
    {
        //if (m_enabled)
        //{
        //    if (wcscmp(name, ll_keyboard) == 0)
        //    {
        //        auto& event = *(reinterpret_cast<LowlevelKeyboardEvent*>(data));
        //        // Return 1 if the keypress is to be suppressed (not forwarded to Windows),
        //        // otherwise return 0.
        //        return HandleKeyboardHookEvent(&event);
        //    }
        //    else if (wcscmp(name, win_hook_event) == 0)
        //    {
        //        auto& event = *(reinterpret_cast<WinHookEvent*>(data));
        //        // Return value is ignored
        //        return 0;
        //    }
        //}
        return 0;
    }

    // This methods are part of an experimental features not fully supported yet
    virtual void register_system_menu_helper(PowertoySystemMenuIface* helper) override {}

    virtual void signal_system_menu_action(const wchar_t* name) override {}

    // Hook procedure definition
    static LRESULT CALLBACK hook_proc(int nCode, WPARAM wParam, LPARAM lParam)
    {
        LowlevelKeyboardEvent event;
        if (nCode == HC_ACTION)
        {
            event.lParam = reinterpret_cast<KBDLLHOOKSTRUCT*>(lParam);
            event.wParam = wParam;
            if (powerkeys_object_ptr->HandleKeyboardHookEvent(&event) == 1)
            {
                return 1;
            }
        }
        return CallNextHookEx(hook_handle_copy, nCode, wParam, lParam);
    }

    // Prevent system-wide input lagging while paused in the debugger
    //#define DISABLE_LOWLEVEL_KBHOOK_WHEN_DEBUGGED
    void start_lowlevel_keyboard_hook()
    {
#if defined(_DEBUG) && defined(DISABLE_LOWLEVEL_KBHOOK_WHEN_DEBUGGED)
        if (IsDebuggerPresent())
        {
            return;
        }
#endif

        if (!hook_handle)
        {
            hook_handle = SetWindowsHookEx(WH_KEYBOARD_LL, hook_proc, GetModuleHandle(NULL), NULL);
            hook_handle_copy = hook_handle;
            if (!hook_handle)
            {
                throw std::runtime_error("Cannot install keyboard listener");
            }
        }
    }

    void stop_lowlevel_keyboard_hook()
    {
        if (hook_handle)
        {
            UnhookWindowsHookEx(hook_handle);
            hook_handle = nullptr;
        }
    }

    intptr_t HandleKeyboardHookEvent(LowlevelKeyboardEvent* data) noexcept
    {
        intptr_t SingleKeyRemapResult = HandleSingleKeyRemapEvent(data);
        // Single key remaps have priority. If a key is remapped, only the remapped version should be visible to the shortcuts
        if (SingleKeyRemapResult == 1)
        {
            return 1;
        }

        intptr_t SingleKeyToggleToModResult = HandleSingleKeyToggleToModEvent(data);
        intptr_t AppSpecificShortcutRemapResult = HandleAppSpecificShortcutRemapEvent(data);
        if (AppSpecificShortcutRemapResult == 1)
        {
            return 1;
        }
        intptr_t OSLevelShortcutRemapResult = HandleOSLevelShortcutRemapEvent(data);
        if ((SingleKeyRemapResult + SingleKeyToggleToModResult + OSLevelShortcutRemapResult + AppSpecificShortcutRemapResult) > 0)
        {
            return 1;
        }
        else
        {
            return 0;
        }
    }

    intptr_t HandleSingleKeyRemapEvent(LowlevelKeyboardEvent* data) noexcept
    {
        if (!(data->lParam->dwExtraInfo & POWERKEYS_INJECTED_FLAG))
        {
            auto it = singleKeyReMap.find(data->lParam->vkCode);
            if (it != singleKeyReMap.end())
            {
                // If mapped to 0x0 then the key is disabled
                if (it->second == 0x0)
                {
                    return 1;
                }

                int key_count = 1;
                LPINPUT keyEventList = new INPUT[size_t(key_count)]();
                memset(keyEventList, 0, sizeof(keyEventList));
                keyEventList[0].type = INPUT_KEYBOARD;
                keyEventList[0].ki.wVk = it->second;
                keyEventList[0].ki.dwFlags = 0;
                keyEventList[0].ki.dwExtraInfo = POWERKEYS_SINGLEKEY_FLAG;
                if (data->wParam == WM_KEYUP || data->wParam == WM_SYSKEYUP)
                {
                    keyEventList[0].ki.dwFlags = KEYEVENTF_KEYUP;
                }

                UINT res = SendInput(key_count, keyEventList, sizeof(INPUT));
                delete[] keyEventList;
                return 1;
            }
        }

        return 0;
    }

    intptr_t HandleSingleKeyToggleToModEvent(LowlevelKeyboardEvent* data) noexcept
    {
        if (!(data->lParam->dwExtraInfo & POWERKEYS_INJECTED_FLAG))
        {
            auto it = singleKeyToggleToMod.find(data->lParam->vkCode);
            if (it != singleKeyToggleToMod.end())
            {
                // To avoid long presses (which leads to continuous keydown messages) from toggling the key on and off
                if (data->wParam == WM_KEYDOWN || data->wParam == WM_SYSKEYDOWN)
                {
                    if (it->second == false)
                    {
                        singleKeyToggleToMod[data->lParam->vkCode] = true;
                    }
                    else
                    {
                        return 1;
                    }
                }
                int key_count = 2;
                LPINPUT keyEventList = new INPUT[size_t(key_count)]();
                memset(keyEventList, 0, sizeof(keyEventList));
                keyEventList[0].type = INPUT_KEYBOARD;
                keyEventList[0].ki.wVk = (WORD)data->lParam->vkCode;
                keyEventList[0].ki.dwFlags = 0;
                keyEventList[0].ki.dwExtraInfo = POWERKEYS_SINGLEKEY_FLAG;
                keyEventList[1].type = INPUT_KEYBOARD;
                keyEventList[1].ki.wVk = (WORD)data->lParam->vkCode;
                keyEventList[1].ki.dwFlags = KEYEVENTF_KEYUP;
                keyEventList[1].ki.dwExtraInfo = POWERKEYS_SINGLEKEY_FLAG;

                UINT res = SendInput(key_count, keyEventList, sizeof(INPUT));
                delete[] keyEventList;

                // Reset the long press flag when the key has been lifted.
                if (data->wParam == WM_KEYUP || data->wParam == WM_SYSKEYUP)
                {
                    singleKeyToggleToMod[data->lParam->vkCode] = false;
                }
                return 1;
            }
        }

        return 0;
    }

    intptr_t HandleShortcutRemapEvent(LowlevelKeyboardEvent* data, std::map<std::vector<DWORD>, std::pair<std::vector<WORD>, bool>>& reMap) noexcept
    {
        for (auto& it : reMap)
        {
            DWORD src_1 = it.first[0];
            DWORD src_2 = it.first[1];
            WORD dest_1 = it.second.first[0];
            WORD dest_2 = it.second.first[1];
            // If the shortcut has been pressed down
            if ((GetAsyncKeyState(src_1) & 0x8000) && !it.second.second)
            {
                if (data->lParam->vkCode == src_2 && (data->wParam == WM_KEYDOWN || data->wParam == WM_SYSKEYDOWN))
                {
                    int key_count = 4;
                    LPINPUT keyEventList = new INPUT[size_t(key_count)]();
                    memset(keyEventList, 0, sizeof(keyEventList));
                    if (src_1 == dest_1)
                    {
                        key_count = 1;
                        keyEventList[0].type = INPUT_KEYBOARD;
                        keyEventList[0].ki.wVk = dest_2;
                        keyEventList[0].ki.dwFlags = 0;
                        keyEventList[0].ki.dwExtraInfo = POWERKEYS_SHORTCUT_FLAG;
                    }
                    else
                    {
                        keyEventList[0].type = INPUT_KEYBOARD;
                        keyEventList[0].ki.wVk = (WORD)DUMMY_KEY;
                        keyEventList[0].ki.dwFlags = KEYEVENTF_KEYUP;
                        keyEventList[0].ki.dwExtraInfo = POWERKEYS_SHORTCUT_FLAG;
                        keyEventList[1].type = INPUT_KEYBOARD;
                        keyEventList[1].ki.wVk = (WORD)src_1;
                        keyEventList[1].ki.dwFlags = KEYEVENTF_KEYUP;
                        keyEventList[1].ki.dwExtraInfo = POWERKEYS_SHORTCUT_FLAG;
                        keyEventList[2].type = INPUT_KEYBOARD;
                        keyEventList[2].ki.wVk = dest_1;
                        keyEventList[2].ki.dwFlags = 0;
                        keyEventList[2].ki.dwExtraInfo = POWERKEYS_SHORTCUT_FLAG;
                        keyEventList[3].type = INPUT_KEYBOARD;
                        keyEventList[3].ki.wVk = dest_2;
                        keyEventList[3].ki.dwFlags = 0;
                        keyEventList[3].ki.dwExtraInfo = POWERKEYS_SHORTCUT_FLAG;
                    }

                    it.second.second = true;
                    UINT res = SendInput(key_count, keyEventList, sizeof(INPUT));
                    delete[] keyEventList;
                    return 1;
                }
            }
            else if (it.second.second)
            {
                // If the modifier key of the original shortcut is released before the normal key
                if (data->lParam->vkCode == src_1 && (data->wParam == WM_KEYUP || data->wParam == WM_SYSKEYUP))
                {
                    int key_count = 2;
                    LPINPUT keyEventList = new INPUT[size_t(key_count)]();
                    memset(keyEventList, 0, sizeof(keyEventList));
                    keyEventList[0].type = INPUT_KEYBOARD;
                    keyEventList[0].ki.wVk = dest_2;
                    keyEventList[0].ki.dwFlags = KEYEVENTF_KEYUP;
                    keyEventList[0].ki.dwExtraInfo = POWERKEYS_SHORTCUT_FLAG;
                    keyEventList[1].type = INPUT_KEYBOARD;
                    keyEventList[1].ki.wVk = dest_1;
                    keyEventList[1].ki.dwFlags = KEYEVENTF_KEYUP;
                    keyEventList[1].ki.dwExtraInfo = POWERKEYS_SHORTCUT_FLAG;
                    it.second.second = false;
                    UINT res = SendInput(key_count, keyEventList, sizeof(INPUT));

                    delete[] keyEventList;
                    return 1;
                }

                if (GetAsyncKeyState(dest_1) & 0x8000)
                {
                    // If the original shortcut is still held down the keyboard will see the original normal key along with the new modifier (keys held down send repeated keydown messages)
                    if (data->lParam->vkCode == src_2 && (data->wParam == WM_KEYDOWN || data->wParam == WM_SYSKEYDOWN))
                    {
                        int key_count = 1;
                        LPINPUT keyEventList = new INPUT[size_t(key_count)]();
                        memset(keyEventList, 0, sizeof(keyEventList));
                        keyEventList[0].type = INPUT_KEYBOARD;
                        keyEventList[0].ki.wVk = dest_2;
                        keyEventList[0].ki.dwFlags = 0;
                        keyEventList[0].ki.dwExtraInfo = POWERKEYS_SHORTCUT_FLAG;

                        it.second.second = true;
                        UINT res = SendInput(key_count, keyEventList, sizeof(INPUT));
                        delete[] keyEventList;
                        return 1;
                    }
                    // If the normal key is released from the original shortcut then revert the keyboard state to just the original modifier being held down
                    if (data->lParam->vkCode == src_2 && (data->wParam == WM_KEYUP || data->wParam == WM_SYSKEYUP))
                    {
                        int key_count = 4;
                        LPINPUT keyEventList = new INPUT[size_t(key_count)]();
                        memset(keyEventList, 0, sizeof(keyEventList));
                        if (src_1 == dest_1)
                        {
                            key_count = 1;
                            keyEventList[0].type = INPUT_KEYBOARD;
                            keyEventList[0].ki.wVk = dest_2;
                            keyEventList[0].ki.dwFlags = KEYEVENTF_KEYUP;
                            keyEventList[0].ki.dwExtraInfo = POWERKEYS_SHORTCUT_FLAG;
                        }
                        else
                        {
                            keyEventList[0].type = INPUT_KEYBOARD;
                            keyEventList[0].ki.wVk = dest_2;
                            keyEventList[0].ki.dwFlags = KEYEVENTF_KEYUP;
                            keyEventList[0].ki.dwExtraInfo = POWERKEYS_SHORTCUT_FLAG;
                            keyEventList[1].type = INPUT_KEYBOARD;
                            keyEventList[1].ki.wVk = dest_1;
                            keyEventList[1].ki.dwFlags = KEYEVENTF_KEYUP;
                            keyEventList[1].ki.dwExtraInfo = POWERKEYS_SHORTCUT_FLAG;
                            keyEventList[2].type = INPUT_KEYBOARD;
                            keyEventList[2].ki.wVk = (WORD)src_1;
                            keyEventList[2].ki.dwFlags = 0;
                            keyEventList[2].ki.dwExtraInfo = POWERKEYS_SHORTCUT_FLAG;
                            keyEventList[3].type = INPUT_KEYBOARD;
                            keyEventList[3].ki.wVk = (WORD)DUMMY_KEY;
                            keyEventList[3].ki.dwFlags = KEYEVENTF_KEYUP;
                            keyEventList[3].ki.dwExtraInfo = POWERKEYS_SHORTCUT_FLAG;
                        }
                        it.second.second = false;
                        UINT res = SendInput(key_count, keyEventList, sizeof(INPUT));
                        delete[] keyEventList;
                        return 1;
                    }
                }
            }
        }

        return 0;
    }

    intptr_t HandleOSLevelShortcutRemapEvent(LowlevelKeyboardEvent* data) noexcept
    {
        if (data->lParam->dwExtraInfo != POWERKEYS_SHORTCUT_FLAG)
        {
            return HandleShortcutRemapEvent(data, osLevelShortcutReMap);
        }

        return 0;
    }

    std::wstring GetCurrentApplication(bool keepPath)
    {
        // Using GetGUIThreadInfo for getting the process of the window in focus. GetForegroundWindow has issues with UWP apps as it returns the Application Frame Host as its linked process
        GUITHREADINFO guiThreadInfo;
        guiThreadInfo.cbSize = sizeof(GUITHREADINFO);
        GetGUIThreadInfo(0, &guiThreadInfo);
        HWND current_window_handle = guiThreadInfo.hwndFocus;

        // If no window in focus, use the active window
        if (current_window_handle == nullptr)
        {
            current_window_handle = guiThreadInfo.hwndActive;
        }
        DWORD process_id;
        DWORD nSize = MAX_PATH;
        WCHAR buffer[MAX_PATH] = { 0 };

        // Get process ID of the focus window
        DWORD thread_id = GetWindowThreadProcessId(current_window_handle, &process_id);
        HANDLE hProc = OpenProcess(PROCESS_QUERY_INFORMATION | PROCESS_VM_READ, FALSE, process_id);

        // Get full path of the executable
        bool res = QueryFullProcessImageName(hProc, 0, buffer, &nSize);
        std::wstring process_name;
        CloseHandle(hProc);

        process_name = buffer;
        if (res)
        {
            PathStripPath(buffer);

            if (!keepPath)
            {
                process_name = buffer;
            }
        }
        return process_name;
    }

    intptr_t HandleAppSpecificShortcutRemapEvent(LowlevelKeyboardEvent* data) noexcept
    {
        if (data->lParam->dwExtraInfo != POWERKEYS_SHORTCUT_FLAG)
        {
            std::wstring process_name = GetCurrentApplication(false);
            if (process_name.empty())
            {
                return 0;
            }

            auto it = appSpecificShortcutReMap.find(process_name);
            if (it != appSpecificShortcutReMap.end())
            {
                return HandleShortcutRemapEvent(data, appSpecificShortcutReMap[process_name]);
            }
        }

        return 0;
    }
};

HHOOK PowerKeys::hook_handle = nullptr;
HHOOK PowerKeys::hook_handle_copy = nullptr;
PowerKeys* PowerKeys::powerkeys_object_ptr = nullptr;

// Load the settings file.
void PowerKeys::init_settings()
{
    try
    {
        //// Load and parse the settings file for this PowerToy.
        //PowerToysSettings::PowerToyValues settings =
        //    PowerToysSettings::PowerToyValues::load_from_settings_file(PowerKeys::get_name());

        // Load a bool property.
        //if (auto v = settings.get_bool_value(L"bool_toggle_1")) {
        //  g_settings.bool_prop = *v;
        //}

        // Load an int property.
        //if (auto v = settings.get_int_value(L"int_spinner_1")) {
        //  g_settings.int_prop = *v;
        //}

        // Load a string property.
        //if (auto v = settings.get_string_value(L"string_text_1")) {
        //  g_settings.string_prop = *v;
        //}

        // Load a color property.
        //if (auto v = settings.get_string_value(L"color_picker_1")) {
        //  g_settings.color_prop = *v;
        //}
    }
    catch (std::exception&)
    {
        // Error while loading from the settings file. Let default values stay as they are.
    }
}

// This method of saving the module settings is only required if you need to do any
// custom processing of the settings before saving them to disk.
//void PowerKeys::save_settings() {
//  try {
//    // Create a PowerToyValues object for this PowerToy
//    PowerToysSettings::PowerToyValues values(get_name());
//
//    // Save a bool property.
//    //values.add_property(
//    //  L"bool_toggle_1", // property name
//    //  g_settings.bool_prop // property value
//    //);
//
//    // Save an int property.
//    //values.add_property(
//    //  L"int_spinner_1", // property name
//    //  g_settings.int_prop // property value
//    //);
//
//    // Save a string property.
//    //values.add_property(
//    //  L"string_text_1", // property name
//    //  g_settings.string_prop // property value
//    );
//
//    // Save a color property.
//    //values.add_property(
//    //  L"color_picker_1", // property name
//    //  g_settings.color_prop // property value
//    //);
//
//    // Save the PowerToyValues JSON to the power toy settings file.
//    values.save_to_settings_file();
//  }
//  catch (std::exception ex) {
//    // Couldn't save the settings.
//  }
//}

extern "C" __declspec(dllexport) PowertoyModuleIface* __cdecl powertoy_create()
{
    return new PowerKeys();
}