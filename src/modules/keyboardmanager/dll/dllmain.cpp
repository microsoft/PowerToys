#include "pch.h"
#include <interface/powertoy_module_interface.h>
#include <interface/lowlevel_keyboard_event_data.h>
#include <interface/win_hook_event_data.h>
#include <common/settings_objects.h>
#include "trace.h"
#include <keyboardmanager/ui/MainWindow.h>
#include <keyboardmanager/common/KeyboardManagerState.h>

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

// Implement the PowerToy Module Interface and all the required methods.
class PowerKeys : public PowertoyModuleIface
{
private:
    // The PowerToy state.
    bool m_enabled = false;

    // Flags used for distinguishing key events sent by PowerKeys
    static const ULONG_PTR POWERKEYS_INJECTED_FLAG = 0x1;
    static const ULONG_PTR POWERKEYS_SINGLEKEY_FLAG = 0x11;
    static const ULONG_PTR POWERKEYS_SHORTCUT_FLAG = 0x101;

    // Dummy key event used in between key up and down events to prevent certain global events from happening
    static const DWORD DUMMY_KEY = 0xFF;

    // Low level hook handles
    static HHOOK hook_handle;

    // Required for Unhook in old versions of Windows
    static HHOOK hook_handle_copy;

    // Static pointer to the current powerkeys object required for accessing the HandleKeyboardHookEvent function in the hook procedure (Only global or static variables can be accessed in a hook procedure CALLBACK)
    static PowerKeys* powerkeys_object_ptr;

    // Variable which stores all the state information to be shared between the UI and back-end
    KeyboardManagerState keyboardManagerState;

    // Vector to store the detected shortcut in the detect shortcut UI. Acts as a shortcut buffer while detecting the shortcuts in the UI.
    std::vector<DWORD> detectedShortcutKeys;

public:
    // Constructor
    PowerKeys()
    {
        init_map();

        // Set the static pointer to the newest object of the class
        powerkeys_object_ptr = this;
    };

    // This function is used to add the hardcoded mappings
    void init_map()
    {
        //// If mapped to 0x0 then key is disabled.
        //keyboardManagerState.singleKeyReMap[0x41] = 0x42;
        //keyboardManagerState.singleKeyReMap[0x42] = 0x41;
        //keyboardManagerState.singleKeyReMap[0x43] = 0x41;
        //keyboardManagerState.singleKeyReMap[VK_LWIN] = VK_LCONTROL;
        //keyboardManagerState.singleKeyReMap[VK_LCONTROL] = VK_LWIN;
        //keyboardManagerState.singleKeyReMap[VK_CAPITAL] = 0x41;
        //keyboardManagerState.singleKeyReMap[0x41] = VK_CAPITAL;
        //keyboardManagerState.singleKeyToggleToMod[VK_CAPITAL] = false;

        //// OS-level shortcut remappings
        //keyboardManagerState.osLevelShortcutReMap[std::vector<DWORD>({ VK_MENU, 0x56 })] = std::make_pair(std::vector<WORD>({ VK_LCONTROL, 0x56 }), false);
        //keyboardManagerState.osLevelShortcutReMap[std::vector<DWORD>({ VK_LMENU, 0x45 })] = std::make_pair(std::vector<WORD>({ VK_LCONTROL, 0x58 }), false);
        //keyboardManagerState.osLevelShortcutReMap[std::vector<DWORD>({ VK_LWIN, 0x46 })] = std::make_pair(std::vector<WORD>({ VK_LWIN, 0x53 }), false);
        //keyboardManagerState.osLevelShortcutReMap[std::vector<DWORD>({ VK_LWIN, 0x41 })] = std::make_pair(std::vector<WORD>({ VK_LCONTROL, 0x58 }), false);
        //keyboardManagerState.osLevelShortcutReMap[std::vector<DWORD>({ VK_LCONTROL, 0x56 })] = std::make_pair(std::vector<WORD>({ VK_MENU, 0x56 }), false);

        //keyboardManagerState.osLevelShortcutReMap[std::vector<DWORD>({ VK_LWIN, 0x41 })] = std::make_pair(std::vector<WORD>({ VK_LCONTROL, 0x58 }), false);
        //keyboardManagerState.osLevelShortcutReMap[std::vector<DWORD>({ VK_LCONTROL, 0x58 })] = std::make_pair(std::vector<WORD>({ VK_LMENU, 0x44 }), false);
        //keyboardManagerState.osLevelShortcutReMap[std::vector<DWORD>({ VK_LCONTROL, 0x56 })] = std::make_pair(std::vector<WORD>({ VK_LWIN, 0x41 }), false);

        ////App-specific shortcut remappings
        //keyboardManagerState.appSpecificShortcutReMap[L"msedge.exe"][std::vector<DWORD>({ VK_LCONTROL, 0x43 })] = std::make_pair(std::vector<WORD>({ VK_LCONTROL, 0x56 }), false); // Ctrl+C to Ctrl+V
        //keyboardManagerState.appSpecificShortcutReMap[L"msedge.exe"][std::vector<DWORD>({ VK_LMENU, 0x44 })] = std::make_pair(std::vector<WORD>({ VK_LCONTROL, 0x46 }), false); // Alt+D to Ctrl+F
        //keyboardManagerState.appSpecificShortcutReMap[L"OUTLOOK.EXE"][std::vector<DWORD>({ VK_LCONTROL, 0x46 })] = std::make_pair(std::vector<WORD>({ VK_LCONTROL, 0x45 }), false); // Ctrl+F to Ctrl+E
        //keyboardManagerState.appSpecificShortcutReMap[L"MicrosoftEdge.exe"][std::vector<DWORD>({ VK_LCONTROL, 0x58 })] = std::make_pair(std::vector<WORD>({ VK_LCONTROL, 0x56 }), false); // Ctrl+X to Ctrl+V
        //keyboardManagerState.appSpecificShortcutReMap[L"Calculator.exe"][std::vector<DWORD>({ VK_LCONTROL, 0x47 })] = std::make_pair(std::vector<WORD>({ VK_LSHIFT, 0x32 }), false); // Ctrl+G to Shift+2
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

            // If you don't need to do any custom processing of the settings, proceed
            // to persists the values calling:
            values.save_to_settings_file();
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
        HINSTANCE hInstance = reinterpret_cast<HINSTANCE>(&__ImageBase);
        std::thread(createMainWindow, hInstance, std::ref(keyboardManagerState)).detach();
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
        return 0;
    }

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

    // Function to terminate the low level hook
    void stop_lowlevel_keyboard_hook()
    {
        if (hook_handle)
        {
            UnhookWindowsHookEx(hook_handle);
            hook_handle = nullptr;
        }
    }

    // Function called by the hook procedure to handle the events. This is the starting point function for remapping
    intptr_t HandleKeyboardHookEvent(LowlevelKeyboardEvent* data) noexcept
    {
        // If the Detect Key Window is currently activated, then suppress the keyboard event
        if (keyboardManagerState.DetectKeyUIBackend(data))
        {
            return 1;
        }

        // Remap a key
        intptr_t SingleKeyRemapResult = HandleSingleKeyRemapEvent(data);

        // Single key remaps have priority. If a key is remapped, only the remapped version should be visible to the shortcuts and hence the event should be suppressed here.
        if (SingleKeyRemapResult == 1)
        {
            return 1;
        }

        // If the Detect Shortcut Window is currently activated, then suppress the keyboard event
        if (keyboardManagerState.DetectShortcutUIBackend(data))
        {
            return 1;
        }

        // Remap a key to behave like a modifier instead of a toggle
        intptr_t SingleKeyToggleToModResult = HandleSingleKeyToggleToModEvent(data);

        // Handle an app-specific shortcut remapping
        intptr_t AppSpecificShortcutRemapResult = HandleAppSpecificShortcutRemapEvent(data);

        // If an app-specific shortcut is remapped then the os-level shortcut remapping should be suppressed.
        if (AppSpecificShortcutRemapResult == 1)
        {
            return 1;
        }

        // Handle an os-level shortcut remapping
        intptr_t OSLevelShortcutRemapResult = HandleOSLevelShortcutRemapEvent(data);

        // If any of the supported types of remappings took place, then suppress the key event
        if ((SingleKeyRemapResult + SingleKeyToggleToModResult + OSLevelShortcutRemapResult + AppSpecificShortcutRemapResult) > 0)
        {
            return 1;
        }
        else
        {
            return 0;
        }
    }

    // Function to a handle a single key remap
    intptr_t HandleSingleKeyRemapEvent(LowlevelKeyboardEvent* data) noexcept
    {
        // Check if the key event was generated by KeyboardManager to avoid remapping events generated by us.
        if (!(data->lParam->dwExtraInfo & POWERKEYS_INJECTED_FLAG))
        {
            auto it = keyboardManagerState.singleKeyReMap.find(data->lParam->vkCode);
            if (it != keyboardManagerState.singleKeyReMap.end())
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

    // Function to a change a key's behaviour from toggle to modifier
    intptr_t HandleSingleKeyToggleToModEvent(LowlevelKeyboardEvent* data) noexcept
    {
        // Check if the key event was generated by KeyboardManager to avoid remapping events generated by us.
        if (!(data->lParam->dwExtraInfo & POWERKEYS_INJECTED_FLAG))
        {
            auto it = keyboardManagerState.singleKeyToggleToMod.find(data->lParam->vkCode);
            if (it != keyboardManagerState.singleKeyToggleToMod.end())
            {
                // To avoid long presses (which leads to continuous keydown messages) from toggling the key on and off
                if (data->wParam == WM_KEYDOWN || data->wParam == WM_SYSKEYDOWN)
                {
                    if (it->second == false)
                    {
                        keyboardManagerState.singleKeyToggleToMod[data->lParam->vkCode] = true;
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
                    keyboardManagerState.singleKeyToggleToMod[data->lParam->vkCode] = false;
                }
                return 1;
            }
        }

        return 0;
    }

    // Function to check if any keys are pressed down except those passed in the argument
    bool IsKeyboardStateClearExceptArgs(const std::vector<DWORD>& args)
    {
        bool isIgnore = false;
        for (int keyVal = 0; keyVal < 0x100; keyVal++)
        {
            // Skip mouse buttons. Keeping this could cause a remapping to fail if a mouse button is also pressed at the same time
            if (keyVal == VK_LBUTTON || keyVal == VK_RBUTTON || keyVal == VK_MBUTTON || keyVal == VK_XBUTTON1 || keyVal == VK_XBUTTON2)
            {
                continue;
            }
            // Check state of the key
            if (GetAsyncKeyState(keyVal) & 0x8000)
            {
                isIgnore = false;
                // If the key is not part of the argument then the keyboard state is not clear
                for (int i = 0; i < args.size(); i++)
                {
                    // If the key matches one of the args, ignore
                    if (args[i] == keyVal)
                    {
                        isIgnore = true;
                        break;
                    }
                    // If the key is Control and either of the args is L/R Control, ignore
                    else if ((args[i] == VK_LCONTROL || args[i] == VK_RCONTROL) && keyVal == VK_CONTROL)
                    {
                        isIgnore = true;
                        break;
                    }
                    // If the key is Alt and either of the args is L/R Alt, ignore
                    else if ((args[i] == VK_LMENU || args[i] == VK_RMENU) && keyVal == VK_MENU)
                    {
                        isIgnore = true;
                        break;
                    }
                    // If the key is Shift and either of the args is L/R Shift, ignore
                    else if ((args[i] == VK_LSHIFT || args[i] == VK_RSHIFT) && keyVal == VK_SHIFT)
                    {
                        isIgnore = true;
                        break;
                    }
                    // If the key is L/R Control and either of the args is Control, ignore
                    else if ((keyVal == VK_LCONTROL || keyVal == VK_RCONTROL) && args[i] == VK_CONTROL)
                    {
                        isIgnore = true;
                        break;
                    }
                    // If the key is L/R Alt and either of the args is Alt, ignore
                    else if ((keyVal == VK_LMENU || keyVal == VK_RMENU) && args[i] == VK_MENU)
                    {
                        isIgnore = true;
                        break;
                    }
                    // If the key is L/R Shift and either of the args is Shift, ignore
                    else if ((keyVal == VK_LSHIFT || keyVal == VK_RSHIFT) && args[i] == VK_SHIFT)
                    {
                        isIgnore = true;
                        break;
                    }
                }

                if (!isIgnore)
                {
                    return false;
                }
            }
        }

        return true;
    }

    // Function to check if the modifiers in the shortcut have been pressed down
    template<typename T>
    bool CheckModifiersKeyboardState(const std::vector<T>& args)
    {
        // Check all keys except last
        for (int i = 0; i < args.size() - 1; i++)
        {
            if (!(GetAsyncKeyState(args[i]) & 0x8000))
            {
                return false;
            }
        }

        return true;
    }

    // Function to check if all the modifiers in the first shorcut are present in the second shortcut, i.e. Modifiers(src) are a subset of Modifiers(dest)
    std::vector<DWORD> GetCommonModifiers(const std::vector<DWORD>& src, const std::vector<WORD>& dest)
    {
        std::vector<DWORD> commonElements;
        for (auto it = src.begin(); it != src.end() - 1; it++)
        {
            if (std::find(dest.begin(), dest.end() - 1, *it) != dest.end() - 1)
            {
                commonElements.push_back(*it);
            }
        }

        return commonElements;
    }

    // Function to a handle a shortcut remap
    intptr_t HandleShortcutRemapEvent(LowlevelKeyboardEvent* data, std::map<std::vector<DWORD>, std::pair<std::vector<WORD>, bool>>& reMap) noexcept
    {
        for (auto& it : reMap)
        {
            const size_t src_size = it.first.size();
            const size_t dest_size = it.second.first.size();
            // If the shortcut has been pressed down
            if (!it.second.second && CheckModifiersKeyboardState<DWORD>(it.first))
            {
                if (data->lParam->vkCode == it.first[src_size - 1] && (data->wParam == WM_KEYDOWN || data->wParam == WM_SYSKEYDOWN))
                {
                    // Check if any other keys have been pressed apart from the shortcut. If true, then check for the next shortcut
                    if (!IsKeyboardStateClearExceptArgs(it.first))
                    {
                        continue;
                    }

                    size_t key_count;
                    LPINPUT keyEventList;
                    // Get the common keys between the two shortcuts
                    std::vector<DWORD> commonKeys = GetCommonModifiers(it.first, it.second.first);

                    // If the original shortcut modifiers are a subset of the new shortcut
                    if (commonKeys.size() == src_size - 1)
                    {
                        // key down for all new shortcut keys except the common modifiers
                        key_count = dest_size - commonKeys.size();
                        keyEventList = new INPUT[key_count]();
                        memset(keyEventList, 0, sizeof(keyEventList));
                        long long i = 0;
                        long long j = 0;
                        // Add a key down only for the non-common keys in the new shortcut
                        while (i < (long long)key_count)
                        {
                            if (std::find(commonKeys.begin(), commonKeys.end(), it.second.first[j]) == commonKeys.end())
                            {
                                keyEventList[i].type = INPUT_KEYBOARD;
                                keyEventList[i].ki.wVk = it.second.first[j];
                                keyEventList[i].ki.dwFlags = 0;
                                keyEventList[i].ki.dwExtraInfo = POWERKEYS_SHORTCUT_FLAG;
                                i++;
                            }
                            j++;
                        }
                    }
                    else
                    {
                        // Dummy key, key up for all the original shortcut modifier keys and key down for all the new shortcut keys but common keys in each are not repeated
                        key_count = 1 + (src_size - 1) + (dest_size) - (2 * commonKeys.size());
                        keyEventList = new INPUT[key_count]();
                        memset(keyEventList, 0, sizeof(keyEventList));

                        // Send dummy key
                        keyEventList[0].type = INPUT_KEYBOARD;
                        keyEventList[0].ki.wVk = (WORD)DUMMY_KEY;
                        keyEventList[0].ki.dwFlags = KEYEVENTF_KEYUP;
                        keyEventList[0].ki.dwExtraInfo = POWERKEYS_SHORTCUT_FLAG;

                        // Release original shortcut state (release in reverse order of shortcut to be accurate)
                        long long i = 1;
                        long long j = (long long)src_size - 2;
                        while (j >= 0)
                        {
                            // Release only those keys which are not common
                            if (std::find(commonKeys.begin(), commonKeys.end(), it.first[j]) == commonKeys.end())
                            {
                                keyEventList[i].type = INPUT_KEYBOARD;
                                keyEventList[i].ki.wVk = (WORD)it.first[j];
                                keyEventList[i].ki.dwFlags = KEYEVENTF_KEYUP;
                                keyEventList[i].ki.dwExtraInfo = POWERKEYS_SHORTCUT_FLAG;
                                i++;
                            }
                            j--;
                        }

                        // Set new shortcut key down state
                        j = 0;
                        while (i < (long long)key_count)
                        {
                            // Key down only those keys which are not common
                            if (std::find(commonKeys.begin(), commonKeys.end(), it.second.first[j]) == commonKeys.end())
                            {
                                keyEventList[i].type = INPUT_KEYBOARD;
                                keyEventList[i].ki.wVk = it.second.first[j];
                                keyEventList[i].ki.dwFlags = 0;
                                keyEventList[i].ki.dwExtraInfo = POWERKEYS_SHORTCUT_FLAG;
                                i++;
                            }
                            j++;
                        }
                    }

                    it.second.second = true;
                    UINT res = SendInput((UINT)key_count, keyEventList, sizeof(INPUT));
                    delete[] keyEventList;
                    return 1;
                }
            }
            // The shortcut has already been pressed down at least once, i.e. the shortcut has been invoked
            // There are 4 cases to be handled if the shortcut has been pressed down
            // 1. The user lets go of one of the modifier keys - reset the keyboard back to the state of the keys actually being pressed down
            // 2. The user keeps the shortcut pressed - the shortcut is repeated (for example you could hold down Ctrl+V and it will keep pasting)
            // 3. The user lets go of the last key - reset the keyboard back to the state of the keys actually being pressed down
            // 4. The user presses another key while holding the shortcut down - the system now sees all the new shortcut keys and this extra key pressed at the end. Not handled as resetting the state would trigger the original shortcut once more
            else if (it.second.second)
            {
                // Get the common keys between the two shortcuts
                std::vector<DWORD> commonKeys = GetCommonModifiers(it.first, it.second.first);

                // Case 1: If any of the modifier keys of the original shortcut are released before the normal key
                auto keyIt = std::find(it.first.begin(), it.first.end() - 1, data->lParam->vkCode);
                if (keyIt != (it.first.end() - 1) && (data->wParam == WM_KEYUP || data->wParam == WM_SYSKEYUP))
                {
                    // Release new shortcut, and set original shortcut keys except the one released
                    size_t key_count;
                    if (std::find(commonKeys.begin(), commonKeys.end(), data->lParam->vkCode) != commonKeys.end())
                    {
                        // release all new shortcut keys and the common released modifier except the other common modifiers, and add all original shortcut modifiers except the common ones
                        key_count = (dest_size - commonKeys.size() + 1) + (src_size - 1 - commonKeys.size());
                    }
                    else
                    {
                        // release all new shortcut keys except the common modifiers and add all original shortcut modifiers except the common ones
                        key_count = dest_size + (src_size - 2) - (2 * commonKeys.size());
                    }
                    LPINPUT keyEventList = new INPUT[key_count]();
                    memset(keyEventList, 0, sizeof(keyEventList));

                    // Release new shortcut state (release in reverse order of shortcut to be accurate)
                    long long i = 0;
                    long long j = (long long)dest_size - 1;
                    while (j >= 0)
                    {
                        // Do not release if it is a common modifier, except the case where a common modifier is released (second part of the if condition))
                        if ((std::find(commonKeys.begin(), commonKeys.end(), it.second.first[j]) == commonKeys.end()) || it.second.first[j] == data->lParam->vkCode)
                        {
                            keyEventList[i].type = INPUT_KEYBOARD;
                            keyEventList[i].ki.wVk = it.second.first[j];
                            keyEventList[i].ki.dwFlags = KEYEVENTF_KEYUP;
                            keyEventList[i].ki.dwExtraInfo = POWERKEYS_SHORTCUT_FLAG;
                            i++;
                        }
                        j--;
                    }

                    // Set original shortcut key down state except the last key and the released modifier
                    j = 0;
                    while (i < (long long)key_count)
                    {
                        // Do not set key down for the released modifier and for the common modifiers
                        if (it.first[j] != data->lParam->vkCode && (std::find(commonKeys.begin(), commonKeys.end(), it.first[j]) == commonKeys.end()))
                        {
                            keyEventList[i].type = INPUT_KEYBOARD;
                            keyEventList[i].ki.wVk = (WORD)it.first[j];
                            keyEventList[i].ki.dwFlags = 0;
                            keyEventList[i].ki.dwExtraInfo = POWERKEYS_SHORTCUT_FLAG;
                            i++;
                        }
                        j++;
                    }

                    it.second.second = false;
                    UINT res = SendInput((UINT)key_count, keyEventList, sizeof(INPUT));

                    delete[] keyEventList;
                    return 1;
                }

                // The system will see the modifiers of the new shortcut as being held down because of the shortcut remap
                if (CheckModifiersKeyboardState<WORD>(it.second.first))
                {
                    // Case 2: If the original shortcut is still held down the keyboard will get a key down message of the last key in the original shortcut and the new shortcut's modifiers will be held down (keys held down send repeated keydown messages)
                    if (data->lParam->vkCode == it.first[src_size - 1] && (data->wParam == WM_KEYDOWN || data->wParam == WM_SYSKEYDOWN))
                    {
                        size_t key_count = 1;
                        LPINPUT keyEventList = new INPUT[key_count]();
                        memset(keyEventList, 0, sizeof(keyEventList));
                        keyEventList[0].type = INPUT_KEYBOARD;
                        keyEventList[0].ki.wVk = it.second.first[dest_size - 1];
                        keyEventList[0].ki.dwFlags = 0;
                        keyEventList[0].ki.dwExtraInfo = POWERKEYS_SHORTCUT_FLAG;

                        it.second.second = true;
                        UINT res = SendInput((UINT)key_count, keyEventList, sizeof(INPUT));
                        delete[] keyEventList;
                        return 1;
                    }

                    // Case 3: If the last key is released from the original shortcut then revert the keyboard state to just the original modifiers being held down
                    if (data->lParam->vkCode == it.first[src_size - 1] && (data->wParam == WM_KEYUP || data->wParam == WM_SYSKEYUP))
                    {
                        size_t key_count;
                        LPINPUT keyEventList;

                        // If the original shortcut is a subset of the new shortcut
                        if (commonKeys.size() == src_size - 1)
                        {
                            key_count = dest_size - commonKeys.size();
                            keyEventList = new INPUT[key_count]();
                            memset(keyEventList, 0, sizeof(keyEventList));
                            long long i = 0;
                            long long j = (long long)dest_size - 1;
                            while (i < (long long)key_count)
                            {
                                if (std::find(commonKeys.begin(), commonKeys.end(), it.second.first[j]) == commonKeys.end())
                                {
                                    keyEventList[i].type = INPUT_KEYBOARD;
                                    keyEventList[i].ki.wVk = it.second.first[j];
                                    keyEventList[i].ki.dwFlags = KEYEVENTF_KEYUP;
                                    keyEventList[i].ki.dwExtraInfo = POWERKEYS_SHORTCUT_FLAG;
                                    i++;
                                }
                                j--;
                            }
                        }
                        else
                        {
                            // Key up for all new shortcut keys, key down for original shortcut modifiers and dummy key but common keys aren't repeated
                            key_count = (dest_size) + (src_size - 1) + 1 - (2 * commonKeys.size());
                            keyEventList = new INPUT[key_count]();
                            memset(keyEventList, 0, sizeof(keyEventList));

                            // Release new shortcut state (release in reverse order of shortcut to be accurate)
                            long long i = 0;
                            long long j = (long long)dest_size - 1;
                            while (j >= 0 && i < (long long)(dest_size - commonKeys.size()))
                            {
                                // Release only those keys which are not common
                                if (std::find(commonKeys.begin(), commonKeys.end(), it.second.first[j]) == commonKeys.end())
                                {
                                    keyEventList[i].type = INPUT_KEYBOARD;
                                    keyEventList[i].ki.wVk = it.second.first[j];
                                    keyEventList[i].ki.dwFlags = KEYEVENTF_KEYUP;
                                    keyEventList[i].ki.dwExtraInfo = POWERKEYS_SHORTCUT_FLAG;
                                    i++;
                                }
                                j--;
                            }

                            // Set old shortcut key down state
                            j = 0;
                            while (i < (long long)key_count)
                            {
                                // Key down only those keys which are not common
                                if (std::find(commonKeys.begin(), commonKeys.end(), it.first[j]) == commonKeys.end())
                                {
                                    keyEventList[i].type = INPUT_KEYBOARD;
                                    keyEventList[i].ki.wVk = (WORD)it.first[j];
                                    keyEventList[i].ki.dwFlags = 0;
                                    keyEventList[i].ki.dwExtraInfo = POWERKEYS_SHORTCUT_FLAG;
                                    i++;
                                }
                                j++;
                            }

                            // Send dummy key
                            keyEventList[key_count - 1].type = INPUT_KEYBOARD;
                            keyEventList[key_count - 1].ki.wVk = (WORD)DUMMY_KEY;
                            keyEventList[key_count - 1].ki.dwFlags = KEYEVENTF_KEYUP;
                            keyEventList[key_count - 1].ki.dwExtraInfo = POWERKEYS_SHORTCUT_FLAG;
                        }

                        it.second.second = false;
                        UINT res = SendInput((UINT)key_count, keyEventList, sizeof(INPUT));
                        delete[] keyEventList;
                        return 1;
                    }
                }
            }
        }

        return 0;
    }

    // Function to a handle an os-level shortcut remap
    intptr_t HandleOSLevelShortcutRemapEvent(LowlevelKeyboardEvent* data) noexcept
    {
        // Check if the key event was generated by KeyboardManager to avoid remapping events generated by us.
        if (data->lParam->dwExtraInfo != POWERKEYS_SHORTCUT_FLAG)
        {
            return HandleShortcutRemapEvent(data, keyboardManagerState.osLevelShortcutReMap);
        }

        return 0;
    }

    // Function to return the window in focus
    HWND GetFocusWindowHandle()
    {
        // Using GetGUIThreadInfo for getting the process of the window in focus. GetForegroundWindow has issues with UWP apps as it returns the Application Frame Host as its linked process
        GUITHREADINFO guiThreadInfo;
        guiThreadInfo.cbSize = sizeof(GUITHREADINFO);
        GetGUIThreadInfo(0, &guiThreadInfo);

        // If no window in focus, use the active window
        if (guiThreadInfo.hwndFocus == nullptr)
        {
            return guiThreadInfo.hwndActive;
        }
        return guiThreadInfo.hwndFocus;
    }

    // Function to return the executable name of the application in focus
    std::wstring GetCurrentApplication(bool keepPath)
    {
        HWND current_window_handle = GetFocusWindowHandle();
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

    // Function to a handle an app-specific shortcut remap
    intptr_t HandleAppSpecificShortcutRemapEvent(LowlevelKeyboardEvent* data) noexcept
    {
        // Check if the key event was generated by KeyboardManager to avoid remapping events generated by us.
        if (data->lParam->dwExtraInfo != POWERKEYS_SHORTCUT_FLAG)
        {
            std::wstring process_name = GetCurrentApplication(false);
            if (process_name.empty())
            {
                return 0;
            }

            auto it = keyboardManagerState.appSpecificShortcutReMap.find(process_name);
            if (it != keyboardManagerState.appSpecificShortcutReMap.end())
            {
                return HandleShortcutRemapEvent(data, keyboardManagerState.appSpecificShortcutReMap[process_name]);
            }
        }

        return 0;
    }
};

HHOOK PowerKeys::hook_handle = nullptr;
HHOOK PowerKeys::hook_handle_copy = nullptr;
PowerKeys* PowerKeys::powerkeys_object_ptr = nullptr;

extern "C" __declspec(dllexport) PowertoyModuleIface* __cdecl powertoy_create()
{
    return new PowerKeys();
}