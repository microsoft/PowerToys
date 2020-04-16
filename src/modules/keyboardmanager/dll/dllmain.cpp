#include "pch.h"
#include <interface/powertoy_module_interface.h>
#include <interface/lowlevel_keyboard_event_data.h>
#include <interface/win_hook_event_data.h>
#include <common/settings_objects.h>
#include "trace.h"
#include "resource.h"
#include <keyboardmanager/ui/EditKeyboardWindow.h>
#include <keyboardmanager/ui/EditShortcutsWindow.h>
#include <keyboardmanager/common/KeyboardManagerState.h>
#include <keyboardmanager/common/Shortcut.h>
#include <keyboardmanager/common/RemapShortcut.h>

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

// Implement the PowerToy Module Interface and all the required methods.
class KeyboardManager : public PowertoyModuleIface
{
private:
    // The PowerToy state.
    bool m_enabled = false;

    // The PowerToy name that will be shown in the settings.
    const std::wstring app_name = GET_RESOURCE_STRING(IDS_KEYBOARDMANAGER);

    // Flags used for distinguishing key events sent by Keyboard Manager
    static const ULONG_PTR KEYBOARDMANAGER_INJECTED_FLAG = 0x1;
    static const ULONG_PTR KEYBOARDMANAGER_SINGLEKEY_FLAG = 0x11;
    static const ULONG_PTR KEYBOARDMANAGER_SHORTCUT_FLAG = 0x101;

    // Dummy key event used in between key up and down events to prevent certain global events from happening
    static const DWORD DUMMY_KEY = 0xFF;

    // Low level hook handles
    static HHOOK hook_handle;

    // Required for Unhook in old versions of Windows
    static HHOOK hook_handle_copy;

    // Static pointer to the current keyboardmanager object required for accessing the HandleKeyboardHookEvent function in the hook procedure (Only global or static variables can be accessed in a hook procedure CALLBACK)
    static KeyboardManager* keyboardmanager_object_ptr;

    // Variable which stores all the state information to be shared between the UI and back-end
    KeyboardManagerState keyboardManagerState;

public:
    // Constructor
    KeyboardManager()
    {
        init_map();

        // Set the static pointer to the newest object of the class
        keyboardmanager_object_ptr = this;
    };

    // This function is used to add the hardcoded mappings
    void init_map()
    {
        //// If mapped to 0x0 then key is disabled.
        //keyboardManagerState.singleKeyReMap[0x41] = 0x42;
        //keyboardManagerState.singleKeyReMap[0x42] = 0x43;
        //keyboardManagerState.singleKeyReMap[0x43] = 0x41;
        //keyboardManagerState.singleKeyReMap[VK_LWIN] = VK_LCONTROL;
        //keyboardManagerState.singleKeyReMap[VK_LCONTROL] = VK_RWIN;
        //keyboardManagerState.singleKeyReMap[VK_CAPITAL] = 0x0;
        //keyboardManagerState.singleKeyReMap[VK_LSHIFT] = VK_CAPITAL;
        //keyboardManagerState.singleKeyToggleToMod[VK_CAPITAL] = false;

        //// OS-level shortcut remappings
        //Shortcut newShortcut = Shortcut::CreateShortcut(winrt::to_hstring(L"Win 65"));
        //Shortcut originalShortcut = Shortcut::CreateShortcut(winrt::to_hstring(L"Shift 65"));
        //keyboardManagerState.AddOSLevelShortcut(originalShortcut, newShortcut);
        //keyboardManagerState.osLevelShortcutReMap[std::vector<DWORD>({ VK_LMENU, 0x44 })] = std::make_pair(std::vector<WORD>({ VK_LCONTROL, 0x56 }), false);
        //keyboardManagerState.osLevelShortcutReMap[std::vector<DWORD>({ VK_LMENU, 0x45 })] = std::make_pair(std::vector<WORD>({ VK_LCONTROL, 0x58 }), false);
        //keyboardManagerState.osLevelShortcutReMap[std::vector<DWORD>({ VK_LWIN, 0x46 })] = std::make_pair(std::vector<WORD>({ VK_LWIN, 0x53 }), false);
        //keyboardManagerState.osLevelShortcutReMap[std::vector<DWORD>({ VK_LWIN, 0x41 })] = std::make_pair(std::vector<WORD>({ VK_LCONTROL, 0x58 }), false);
        //keyboardManagerState.osLevelShortcutReMap[std::vector<DWORD>({ VK_LCONTROL, 0x58 })] = std::make_pair(std::vector<WORD>({ VK_LWIN, 0x41 }), false);

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
        return app_name.c_str();
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
        settings.set_description(IDS_SETTINGS_DESCRIPTION);

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
            HINSTANCE hInstance = reinterpret_cast<HINSTANCE>(&__ImageBase);

            if (action_object.get_name() == L"RemapKeyboard") 
            {
                if (!CheckEditKeyboardWindowActive())
                {
                    std::thread(createEditKeyboardWindow, hInstance, std::ref(keyboardManagerState)).detach();
                }
            }
            else if (action_object.get_name() == L"EditShortcut")
            {
                if (!CheckEditShortcutsWindowActive())
                {
                    std::thread(createEditShortcutsWindow, hInstance, std::ref(keyboardManagerState)).detach();
                }
            }
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
            if (keyboardmanager_object_ptr->HandleKeyboardHookEvent(&event) == 1)
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
        // Check if there is a registered KeyDelay for this key.
        if (keyboardManagerState.HandleKeyDelayEvent(data))
        {
            return 1;
        }

        // If the Detect Key Window is currently activated, then suppress the keyboard event
        if (keyboardManagerState.DetectSingleRemapKeyUIBackend(data))
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

    void SetKeyEvent(LPINPUT keyEventArray, int index, DWORD inputType, WORD keyCode, DWORD flags, ULONG_PTR extraInfo)
    {
        keyEventArray[index].type = inputType;
        keyEventArray[index].ki.wVk = keyCode;
        keyEventArray[index].ki.dwFlags = flags;
        keyEventArray[index].ki.dwExtraInfo = extraInfo;
    }

    // Function to a handle a single key remap
    intptr_t HandleSingleKeyRemapEvent(LowlevelKeyboardEvent* data) noexcept
    {
        // Check if the key event was generated by KeyboardManager to avoid remapping events generated by us.
        if (!(data->lParam->dwExtraInfo & KEYBOARDMANAGER_INJECTED_FLAG))
        {
            // The mutex should be unlocked before SendInput is called to avoid re-entry into the same mutex. More details can be found at https://github.com/microsoft/PowerToys/pull/1789#issuecomment-607555837
            std::unique_lock<std::mutex> lock(keyboardManagerState.singleKeyReMap_mutex);
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
                if (data->wParam == WM_KEYUP || data->wParam == WM_SYSKEYUP)
                {
                    SetKeyEvent(keyEventList, 0, INPUT_KEYBOARD, (WORD)it->second, KEYEVENTF_KEYUP, KEYBOARDMANAGER_SINGLEKEY_FLAG);
                }
                else
                {
                    SetKeyEvent(keyEventList, 0, INPUT_KEYBOARD, (WORD)it->second, 0, KEYBOARDMANAGER_SINGLEKEY_FLAG);
                }

                lock.unlock();
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
        if (!(data->lParam->dwExtraInfo & KEYBOARDMANAGER_INJECTED_FLAG))
        {
            // The mutex should be unlocked before SendInput is called to avoid re-entry into the same mutex. More details can be found at https://github.com/microsoft/PowerToys/pull/1789#issuecomment-607555837
            std::unique_lock<std::mutex> lock(keyboardManagerState.singleKeyToggleToMod_mutex);
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
                        lock.unlock();
                        return 1;
                    }
                }
                int key_count = 2;
                LPINPUT keyEventList = new INPUT[size_t(key_count)]();
                memset(keyEventList, 0, sizeof(keyEventList));
                SetKeyEvent(keyEventList, 0, INPUT_KEYBOARD, (WORD)data->lParam->vkCode, 0, KEYBOARDMANAGER_SINGLEKEY_FLAG);
                SetKeyEvent(keyEventList, 1, INPUT_KEYBOARD, (WORD)data->lParam->vkCode, KEYEVENTF_KEYUP, KEYBOARDMANAGER_SINGLEKEY_FLAG);

                lock.unlock();
                UINT res = SendInput(key_count, keyEventList, sizeof(INPUT));
                delete[] keyEventList;

                // Reset the long press flag when the key has been lifted.
                if (data->wParam == WM_KEYUP || data->wParam == WM_SYSKEYUP)
                {
                    lock.lock();
                    keyboardManagerState.singleKeyToggleToMod[data->lParam->vkCode] = false;
                    lock.unlock();
                }

                return 1;
            }
        }

        return 0;
    }

    // Function to a handle a shortcut remap
    intptr_t HandleShortcutRemapEvent(LowlevelKeyboardEvent* data, std::map<Shortcut, RemapShortcut>& reMap, std::mutex& map_mutex) noexcept
    {
        // The mutex should be unlocked before SendInput is called to avoid re-entry into the same mutex. More details can be found at https://github.com/microsoft/PowerToys/pull/1789#issuecomment-607555837
        std::unique_lock<std::mutex> lock(map_mutex);
        for (auto& it : reMap)
        {
            const size_t src_size = it.first.Size();
            const size_t dest_size = it.second.targetShortcut.Size();

            // If the shortcut has been pressed down
            if (!it.second.isShortcutInvoked && it.first.CheckModifiersKeyboardState())
            {
                if (data->lParam->vkCode == it.first.GetActionKey() && (data->wParam == WM_KEYDOWN || data->wParam == WM_SYSKEYDOWN))
                {
                    // Check if any other keys have been pressed apart from the shortcut. If true, then check for the next shortcut
                    if (!it.first.IsKeyboardStateClearExceptShortcut())
                    {
                        continue;
                    }

                    size_t key_count;
                    LPINPUT keyEventList;

                    // Remember which win key was pressed initially
                    if (GetAsyncKeyState(VK_RWIN) & 0x8000)
                    {
                        it.second.winKeyInvoked = ModifierKey::Right;
                    }
                    else if (GetAsyncKeyState(VK_LWIN) & 0x8000)
                    {
                        it.second.winKeyInvoked = ModifierKey::Left;
                    }

                    // Get the common keys between the two shortcuts
                    int commonKeys = it.first.GetCommonModifiersCount(it.second.targetShortcut);

                    // If the original shortcut modifiers are a subset of the new shortcut
                    if (commonKeys == src_size - 1)
                    {
                        // key down for all new shortcut keys except the common modifiers
                        key_count = dest_size - commonKeys;
                        keyEventList = new INPUT[key_count]();
                        memset(keyEventList, 0, sizeof(keyEventList));
                        int i = 0;

                        if ((it.second.targetShortcut.GetWinKey(it.second.winKeyInvoked) != it.first.GetWinKey(it.second.winKeyInvoked)) && it.second.targetShortcut.GetWinKey(it.second.winKeyInvoked) != NULL)
                        {
                            SetKeyEvent(keyEventList, i, INPUT_KEYBOARD, (WORD)it.second.targetShortcut.GetWinKey(it.second.winKeyInvoked), 0, KEYBOARDMANAGER_SHORTCUT_FLAG);
                            i++;
                        }
                        if ((it.second.targetShortcut.GetCtrlKey() != it.first.GetCtrlKey()) && it.second.targetShortcut.GetCtrlKey() != NULL)
                        {
                            SetKeyEvent(keyEventList, i, INPUT_KEYBOARD, (WORD)it.second.targetShortcut.GetCtrlKey(), 0, KEYBOARDMANAGER_SHORTCUT_FLAG);
                            i++;
                        }
                        if ((it.second.targetShortcut.GetAltKey() != it.first.GetAltKey()) && it.second.targetShortcut.GetAltKey() != NULL)
                        {
                            SetKeyEvent(keyEventList, i, INPUT_KEYBOARD, (WORD)it.second.targetShortcut.GetAltKey(), 0, KEYBOARDMANAGER_SHORTCUT_FLAG);
                            i++;
                        }
                        if ((it.second.targetShortcut.GetShiftKey() != it.first.GetShiftKey()) && it.second.targetShortcut.GetShiftKey() != NULL)
                        {
                            SetKeyEvent(keyEventList, i, INPUT_KEYBOARD, (WORD)it.second.targetShortcut.GetShiftKey(), 0, KEYBOARDMANAGER_SHORTCUT_FLAG);
                            i++;
                        }
                        SetKeyEvent(keyEventList, i, INPUT_KEYBOARD, (WORD)it.second.targetShortcut.GetActionKey(), 0, KEYBOARDMANAGER_SHORTCUT_FLAG);
                        i++;
                    }
                    else
                    {
                        // Dummy key, key up for all the original shortcut modifier keys and key down for all the new shortcut keys but common keys in each are not repeated
                        key_count = 1 + (src_size - 1) + (dest_size) - (2 * (size_t)commonKeys);
                        keyEventList = new INPUT[key_count]();
                        memset(keyEventList, 0, sizeof(keyEventList));

                        // Send dummy key
                        int i = 0;
                        SetKeyEvent(keyEventList, i, INPUT_KEYBOARD, (WORD)DUMMY_KEY, KEYEVENTF_KEYUP, KEYBOARDMANAGER_SHORTCUT_FLAG);
                        i++;
                        // Release original shortcut state (release in reverse order of shortcut to be accurate)
                        if ((it.second.targetShortcut.GetShiftKey() != it.first.GetShiftKey()) && it.first.GetShiftKey() != NULL)
                        {
                            SetKeyEvent(keyEventList, i, INPUT_KEYBOARD, (WORD)it.first.GetShiftKey(), KEYEVENTF_KEYUP, KEYBOARDMANAGER_SHORTCUT_FLAG);
                            i++;
                        }
                        if ((it.second.targetShortcut.GetAltKey() != it.first.GetAltKey()) && it.first.GetAltKey() != NULL)
                        {
                            SetKeyEvent(keyEventList, i, INPUT_KEYBOARD, (WORD)it.first.GetAltKey(), KEYEVENTF_KEYUP, KEYBOARDMANAGER_SHORTCUT_FLAG);
                            i++;
                        }
                        if ((it.second.targetShortcut.GetCtrlKey() != it.first.GetCtrlKey()) && it.first.GetCtrlKey() != NULL)
                        {
                            SetKeyEvent(keyEventList, i, INPUT_KEYBOARD, (WORD)it.first.GetCtrlKey(), KEYEVENTF_KEYUP, KEYBOARDMANAGER_SHORTCUT_FLAG);
                            i++;
                        }
                        if ((it.second.targetShortcut.GetWinKey(it.second.winKeyInvoked) != it.first.GetWinKey(it.second.winKeyInvoked)) && it.first.GetWinKey(it.second.winKeyInvoked) != NULL)
                        {
                            SetKeyEvent(keyEventList, i, INPUT_KEYBOARD, (WORD)it.first.GetWinKey(it.second.winKeyInvoked), KEYEVENTF_KEYUP, KEYBOARDMANAGER_SHORTCUT_FLAG);
                            i++;
                        }

                        // Set new shortcut key down state
                        if ((it.second.targetShortcut.GetWinKey(it.second.winKeyInvoked) != it.first.GetWinKey(it.second.winKeyInvoked)) && it.second.targetShortcut.GetWinKey(it.second.winKeyInvoked) != NULL)
                        {
                            SetKeyEvent(keyEventList, i, INPUT_KEYBOARD, (WORD)it.second.targetShortcut.GetWinKey(it.second.winKeyInvoked), 0, KEYBOARDMANAGER_SHORTCUT_FLAG);
                            i++;
                        }
                        if ((it.second.targetShortcut.GetCtrlKey() != it.first.GetCtrlKey()) && it.second.targetShortcut.GetCtrlKey() != NULL)
                        {
                            SetKeyEvent(keyEventList, i, INPUT_KEYBOARD, (WORD)it.second.targetShortcut.GetCtrlKey(), 0, KEYBOARDMANAGER_SHORTCUT_FLAG);
                            i++;
                        }
                        if ((it.second.targetShortcut.GetAltKey() != it.first.GetAltKey()) && it.second.targetShortcut.GetAltKey() != NULL)
                        {
                            SetKeyEvent(keyEventList, i, INPUT_KEYBOARD, (WORD)it.second.targetShortcut.GetAltKey(), 0, KEYBOARDMANAGER_SHORTCUT_FLAG);
                            i++;
                        }
                        if ((it.second.targetShortcut.GetShiftKey() != it.first.GetShiftKey()) && it.second.targetShortcut.GetShiftKey() != NULL)
                        {
                            SetKeyEvent(keyEventList, i, INPUT_KEYBOARD, (WORD)it.second.targetShortcut.GetShiftKey(), 0, KEYBOARDMANAGER_SHORTCUT_FLAG);
                            i++;
                        }
                        SetKeyEvent(keyEventList, i, INPUT_KEYBOARD, (WORD)it.second.targetShortcut.GetActionKey(), 0, KEYBOARDMANAGER_SHORTCUT_FLAG);
                        i++;
                    }

                    it.second.isShortcutInvoked = true;
                    lock.unlock();
                    UINT res = SendInput((UINT)key_count, keyEventList, sizeof(INPUT));
                    delete[] keyEventList;
                    return 1;
                }
            }
            // The shortcut has already been pressed down at least once, i.e. the shortcut has been invoked
            // There are 4 cases to be handled if the shortcut has been pressed down
            // 1. The user lets go of one of the modifier keys - reset the keyboard back to the state of the keys actually being pressed down
            // 2. The user keeps the shortcut pressed - the shortcut is repeated (for example you could hold down Ctrl+V and it will keep pasting)
            // 3. The user lets go of the action key - reset the keyboard back to the state of the keys actually being pressed down
            // 4. The user presses another key while holding the shortcut down - the system now sees all the new shortcut keys and this extra key pressed at the end. Not handled as resetting the state would trigger the original shortcut once more
            else if (it.second.isShortcutInvoked)
            {
                // Get the common keys between the two shortcuts
                int commonKeys = it.first.GetCommonModifiersCount(it.second.targetShortcut);

                // Case 1: If any of the modifier keys of the original shortcut are released before the normal key
                if ((it.first.CheckWinKey(data->lParam->vkCode) || it.first.CheckCtrlKey(data->lParam->vkCode) || it.first.CheckAltKey(data->lParam->vkCode) || it.first.CheckShiftKey(data->lParam->vkCode)) && (data->wParam == WM_KEYUP || data->wParam == WM_SYSKEYUP))
                {
                    // Release new shortcut, and set original shortcut keys except the one released
                    size_t key_count;
                    // if the released key is present in both shortcuts' modifiers (i.e part of the common modifiers)
                    if (it.second.targetShortcut.CheckWinKey(data->lParam->vkCode) || it.second.targetShortcut.CheckCtrlKey(data->lParam->vkCode) || it.second.targetShortcut.CheckAltKey(data->lParam->vkCode) || it.second.targetShortcut.CheckShiftKey(data->lParam->vkCode))
                    {
                        // release all new shortcut keys and the common released modifier except the other common modifiers, and add all original shortcut modifiers except the common ones
                        key_count = (dest_size - commonKeys + 1) + (src_size - 1 - commonKeys);
                    }
                    else
                    {
                        // release all new shortcut keys except the common modifiers and add all original shortcut modifiers except the common ones
                        key_count = dest_size + (src_size - 2) - (2 * (size_t)commonKeys);
                    }
                    LPINPUT keyEventList = new INPUT[key_count]();
                    memset(keyEventList, 0, sizeof(keyEventList));

                    // Release new shortcut state (release in reverse order of shortcut to be accurate)
                    int i = 0;
                    SetKeyEvent(keyEventList, i, INPUT_KEYBOARD, (WORD)it.second.targetShortcut.GetActionKey(), KEYEVENTF_KEYUP, KEYBOARDMANAGER_SHORTCUT_FLAG);
                    i++;
                    if (((it.second.targetShortcut.GetShiftKey() != it.first.GetShiftKey()) || (it.second.targetShortcut.CheckShiftKey(data->lParam->vkCode))) && it.second.targetShortcut.GetShiftKey() != NULL)
                    {
                        SetKeyEvent(keyEventList, i, INPUT_KEYBOARD, (WORD)it.second.targetShortcut.GetShiftKey(), KEYEVENTF_KEYUP, KEYBOARDMANAGER_SHORTCUT_FLAG);
                        i++;
                    }
                    if (((it.second.targetShortcut.GetAltKey() != it.first.GetAltKey()) || (it.second.targetShortcut.CheckAltKey(data->lParam->vkCode))) && it.second.targetShortcut.GetAltKey() != NULL)
                    {
                        SetKeyEvent(keyEventList, i, INPUT_KEYBOARD, (WORD)it.second.targetShortcut.GetAltKey(), KEYEVENTF_KEYUP, KEYBOARDMANAGER_SHORTCUT_FLAG);
                        i++;
                    }
                    if (((it.second.targetShortcut.GetCtrlKey() != it.first.GetCtrlKey()) || (it.second.targetShortcut.CheckCtrlKey(data->lParam->vkCode))) && it.second.targetShortcut.GetCtrlKey() != NULL)
                    {
                        SetKeyEvent(keyEventList, i, INPUT_KEYBOARD, (WORD)it.second.targetShortcut.GetCtrlKey(), KEYEVENTF_KEYUP, KEYBOARDMANAGER_SHORTCUT_FLAG);
                        i++;
                    }
                    if (((it.second.targetShortcut.GetWinKey(it.second.winKeyInvoked) != it.first.GetWinKey(it.second.winKeyInvoked)) || (it.second.targetShortcut.CheckWinKey(data->lParam->vkCode))) && it.second.targetShortcut.GetWinKey(it.second.winKeyInvoked) != NULL)
                    {
                        SetKeyEvent(keyEventList, i, INPUT_KEYBOARD, (WORD)it.second.targetShortcut.GetWinKey(it.second.winKeyInvoked), KEYEVENTF_KEYUP, KEYBOARDMANAGER_SHORTCUT_FLAG);
                        i++;
                    }

                    // Set original shortcut key down state except the action key and the released modifier
                    if ((it.second.targetShortcut.GetWinKey(it.second.winKeyInvoked) != it.first.GetWinKey(it.second.winKeyInvoked)) && (!it.first.CheckWinKey(data->lParam->vkCode)) && it.first.GetWinKey(it.second.winKeyInvoked) != NULL)
                    {
                        SetKeyEvent(keyEventList, i, INPUT_KEYBOARD, (WORD)it.first.GetWinKey(it.second.winKeyInvoked), 0, KEYBOARDMANAGER_SHORTCUT_FLAG);
                        i++;
                    }
                    if ((it.second.targetShortcut.GetCtrlKey() != it.first.GetCtrlKey()) && (!it.first.CheckCtrlKey(data->lParam->vkCode)) && it.first.GetCtrlKey() != NULL)
                    {
                        SetKeyEvent(keyEventList, i, INPUT_KEYBOARD, (WORD)it.first.GetCtrlKey(), 0, KEYBOARDMANAGER_SHORTCUT_FLAG);
                        i++;
                    }
                    if ((it.second.targetShortcut.GetAltKey() != it.first.GetAltKey()) && (!it.first.CheckAltKey(data->lParam->vkCode)) && it.first.GetAltKey() != NULL)
                    {
                        SetKeyEvent(keyEventList, i, INPUT_KEYBOARD, (WORD)it.first.GetAltKey(), 0, KEYBOARDMANAGER_SHORTCUT_FLAG);
                        i++;
                    }
                    if ((it.second.targetShortcut.GetShiftKey() != it.first.GetShiftKey()) && (!it.first.CheckShiftKey(data->lParam->vkCode)) && it.first.GetShiftKey() != NULL)
                    {
                        SetKeyEvent(keyEventList, i, INPUT_KEYBOARD, (WORD)it.first.GetShiftKey(), 0, KEYBOARDMANAGER_SHORTCUT_FLAG);
                        i++;
                    }

                    it.second.isShortcutInvoked = false;
                    it.second.winKeyInvoked = ModifierKey::Disabled;
                    lock.unlock();
                    UINT res = SendInput((UINT)key_count, keyEventList, sizeof(INPUT));
                    delete[] keyEventList;
                    return 1;
                }

                // The system will see the modifiers of the new shortcut as being held down because of the shortcut remap
                if (it.second.targetShortcut.CheckModifiersKeyboardState())
                {
                    // Case 2: If the original shortcut is still held down the keyboard will get a key down message of the action key in the original shortcut and the new shortcut's modifiers will be held down (keys held down send repeated keydown messages)
                    if (data->lParam->vkCode == it.first.GetActionKey() && (data->wParam == WM_KEYDOWN || data->wParam == WM_SYSKEYDOWN))
                    {
                        size_t key_count = 1;
                        LPINPUT keyEventList = new INPUT[key_count]();
                        memset(keyEventList, 0, sizeof(keyEventList));
                        SetKeyEvent(keyEventList, 0, INPUT_KEYBOARD, (WORD)it.second.targetShortcut.GetActionKey(), 0, KEYBOARDMANAGER_SHORTCUT_FLAG);

                        it.second.isShortcutInvoked = true;
                        lock.unlock();
                        UINT res = SendInput((UINT)key_count, keyEventList, sizeof(INPUT));
                        delete[] keyEventList;
                        return 1;
                    }

                    // Case 3: If the action key is released from the original shortcut then revert the keyboard state to just the original modifiers being held down
                    if (data->lParam->vkCode == it.first.GetActionKey() && (data->wParam == WM_KEYUP || data->wParam == WM_SYSKEYUP))
                    {
                        size_t key_count;
                        LPINPUT keyEventList;

                        // If the original shortcut is a subset of the new shortcut
                        if (commonKeys == src_size - 1)
                        {
                            key_count = dest_size - commonKeys;
                            keyEventList = new INPUT[key_count]();
                            memset(keyEventList, 0, sizeof(keyEventList));

                            int i = 0;
                            SetKeyEvent(keyEventList, i, INPUT_KEYBOARD, (WORD)it.second.targetShortcut.GetActionKey(), KEYEVENTF_KEYUP, KEYBOARDMANAGER_SHORTCUT_FLAG);
                            i++;
                            if ((it.second.targetShortcut.GetShiftKey() != it.first.GetShiftKey()) && it.second.targetShortcut.GetShiftKey() != NULL)
                            {
                                SetKeyEvent(keyEventList, i, INPUT_KEYBOARD, (WORD)it.second.targetShortcut.GetShiftKey(), KEYEVENTF_KEYUP, KEYBOARDMANAGER_SHORTCUT_FLAG);
                                i++;
                            }
                            if ((it.second.targetShortcut.GetAltKey() != it.first.GetAltKey()) && it.second.targetShortcut.GetAltKey() != NULL)
                            {
                                SetKeyEvent(keyEventList, i, INPUT_KEYBOARD, (WORD)it.second.targetShortcut.GetAltKey(), KEYEVENTF_KEYUP, KEYBOARDMANAGER_SHORTCUT_FLAG);
                                i++;
                            }
                            if ((it.second.targetShortcut.GetCtrlKey() != it.first.GetCtrlKey()) && it.second.targetShortcut.GetCtrlKey() != NULL)
                            {
                                SetKeyEvent(keyEventList, i, INPUT_KEYBOARD, (WORD)it.second.targetShortcut.GetCtrlKey(), KEYEVENTF_KEYUP, KEYBOARDMANAGER_SHORTCUT_FLAG);
                                i++;
                            }
                            if ((it.second.targetShortcut.GetWinKey(it.second.winKeyInvoked) != it.first.GetWinKey(it.second.winKeyInvoked)) && it.second.targetShortcut.GetWinKey(it.second.winKeyInvoked) != NULL)
                            {
                                SetKeyEvent(keyEventList, i, INPUT_KEYBOARD, (WORD)it.second.targetShortcut.GetWinKey(it.second.winKeyInvoked), KEYEVENTF_KEYUP, KEYBOARDMANAGER_SHORTCUT_FLAG);
                                i++;
                            }
                        }
                        else
                        {
                            // Key up for all new shortcut keys, key down for original shortcut modifiers and dummy key but common keys aren't repeated
                            key_count = (dest_size) + (src_size - 1) + 1 - (2 * (size_t)commonKeys);
                            keyEventList = new INPUT[key_count]();
                            memset(keyEventList, 0, sizeof(keyEventList));

                            // Release new shortcut state (release in reverse order of shortcut to be accurate)
                            int i = 0;
                            SetKeyEvent(keyEventList, i, INPUT_KEYBOARD, (WORD)it.second.targetShortcut.GetActionKey(), KEYEVENTF_KEYUP, KEYBOARDMANAGER_SHORTCUT_FLAG);
                            i++;
                            if ((it.second.targetShortcut.GetShiftKey() != it.first.GetShiftKey()) && it.second.targetShortcut.GetShiftKey() != NULL)
                            {
                                SetKeyEvent(keyEventList, i, INPUT_KEYBOARD, (WORD)it.second.targetShortcut.GetShiftKey(), KEYEVENTF_KEYUP, KEYBOARDMANAGER_SHORTCUT_FLAG);
                                i++;
                            }
                            if ((it.second.targetShortcut.GetAltKey() != it.first.GetAltKey()) && it.second.targetShortcut.GetAltKey() != NULL)
                            {
                                SetKeyEvent(keyEventList, i, INPUT_KEYBOARD, (WORD)it.second.targetShortcut.GetAltKey(), KEYEVENTF_KEYUP, KEYBOARDMANAGER_SHORTCUT_FLAG);
                                i++;
                            }
                            if ((it.second.targetShortcut.GetCtrlKey() != it.first.GetCtrlKey()) && it.second.targetShortcut.GetCtrlKey() != NULL)
                            {
                                SetKeyEvent(keyEventList, i, INPUT_KEYBOARD, (WORD)it.second.targetShortcut.GetCtrlKey(), KEYEVENTF_KEYUP, KEYBOARDMANAGER_SHORTCUT_FLAG);
                                i++;
                            }
                            if ((it.second.targetShortcut.GetWinKey(it.second.winKeyInvoked) != it.first.GetWinKey(it.second.winKeyInvoked)) && it.second.targetShortcut.GetWinKey(it.second.winKeyInvoked) != NULL)
                            {
                                SetKeyEvent(keyEventList, i, INPUT_KEYBOARD, (WORD)it.second.targetShortcut.GetWinKey(it.second.winKeyInvoked), KEYEVENTF_KEYUP, KEYBOARDMANAGER_SHORTCUT_FLAG);
                                i++;
                            }

                            // Set old shortcut key down state

                            if ((it.second.targetShortcut.GetWinKey(it.second.winKeyInvoked) != it.first.GetWinKey(it.second.winKeyInvoked)) && it.first.GetWinKey(it.second.winKeyInvoked) != NULL)
                            {
                                SetKeyEvent(keyEventList, i, INPUT_KEYBOARD, (WORD)it.first.GetWinKey(it.second.winKeyInvoked), 0, KEYBOARDMANAGER_SHORTCUT_FLAG);
                                i++;
                            }
                            if ((it.second.targetShortcut.GetCtrlKey() != it.first.GetCtrlKey()) && it.first.GetCtrlKey() != NULL)
                            {
                                SetKeyEvent(keyEventList, i, INPUT_KEYBOARD, (WORD)it.first.GetCtrlKey(), 0, KEYBOARDMANAGER_SHORTCUT_FLAG);
                                i++;
                            }
                            if ((it.second.targetShortcut.GetAltKey() != it.first.GetAltKey()) && it.first.GetAltKey() != NULL)
                            {
                                SetKeyEvent(keyEventList, i, INPUT_KEYBOARD, (WORD)it.first.GetAltKey(), 0, KEYBOARDMANAGER_SHORTCUT_FLAG);
                                i++;
                            }
                            if ((it.second.targetShortcut.GetShiftKey() != it.first.GetShiftKey()) && it.first.GetShiftKey() != NULL)
                            {
                                SetKeyEvent(keyEventList, i, INPUT_KEYBOARD, (WORD)it.first.GetShiftKey(), 0, KEYBOARDMANAGER_SHORTCUT_FLAG);
                                i++;
                            }

                            // Send dummy key
                            SetKeyEvent(keyEventList, i, INPUT_KEYBOARD, (WORD)DUMMY_KEY, KEYEVENTF_KEYUP, KEYBOARDMANAGER_SHORTCUT_FLAG);
                            i++;
                        }

                        it.second.isShortcutInvoked = false;
                        it.second.winKeyInvoked = ModifierKey::Disabled;
                        lock.unlock();
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
        if (data->lParam->dwExtraInfo != KEYBOARDMANAGER_SHORTCUT_FLAG)
        {
            bool result = HandleShortcutRemapEvent(data, keyboardManagerState.osLevelShortcutReMap, keyboardManagerState.osLevelShortcutReMap_mutex);
            return result;
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
        if (data->lParam->dwExtraInfo != KEYBOARDMANAGER_SHORTCUT_FLAG)
        {
            std::wstring process_name = GetCurrentApplication(false);
            if (process_name.empty())
            {
                return 0;
            }

            std::unique_lock<std::mutex> lock(keyboardManagerState.appSpecificShortcutReMap_mutex);
            auto it = keyboardManagerState.appSpecificShortcutReMap.find(process_name);
            if (it != keyboardManagerState.appSpecificShortcutReMap.end())
            {
                lock.unlock();
                bool result = HandleShortcutRemapEvent(data, keyboardManagerState.appSpecificShortcutReMap[process_name], keyboardManagerState.appSpecificShortcutReMap_mutex);
                return result;
            }
        }

        return 0;
    }
};

HHOOK KeyboardManager::hook_handle = nullptr;
HHOOK KeyboardManager::hook_handle_copy = nullptr;
KeyboardManager* KeyboardManager::keyboardmanager_object_ptr = nullptr;

extern "C" __declspec(dllexport) PowertoyModuleIface* __cdecl powertoy_create()
{
    return new KeyboardManager();
}