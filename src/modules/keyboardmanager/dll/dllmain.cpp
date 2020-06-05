#include "pch.h"
#include <interface/powertoy_module_interface.h>
#include <interface/lowlevel_keyboard_event_data.h>
#include <interface/win_hook_event_data.h>
#include <common/settings_objects.h>
#include <common/shared_constants.h>
#include "resource.h"
#include <keyboardmanager/ui/EditKeyboardWindow.h>
#include <keyboardmanager/ui/EditShortcutsWindow.h>
#include <keyboardmanager/common/KeyboardManagerState.h>
#include <keyboardmanager/common/Shortcut.h>
#include <keyboardmanager/common/RemapShortcut.h>
#include <keyboardmanager/common/KeyboardManagerConstants.h>
#include <common/settings_helpers.h>
#include <keyboardmanager/common/trace.h>
#include "KeyboardEventHandlers.h"

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
        // Load the initial configuration.
        load_config();

        // Set the static pointer to the newest object of the class
        keyboardmanager_object_ptr = this;
    };

    // Load config from the saved settings.
    void load_config()
    {
        try
        {
            PowerToysSettings::PowerToyValues settings =
                PowerToysSettings::PowerToyValues::load_from_settings_file(get_name());
            auto current_config = settings.get_string_value(KeyboardManagerConstants::ActiveConfigurationSettingName);

            if (current_config)
            {
                keyboardManagerState.SetCurrentConfigName(*current_config);
                // Read the config file and load the remaps.
                auto configFile = json::from_file(PTSettingsHelper::get_module_save_folder_location(KeyboardManagerConstants::ModuleName) + L"\\" + *current_config + L".json");
                if (configFile)
                {
                    auto jsonData = *configFile;
                    auto remapKeysData = jsonData.GetNamedObject(KeyboardManagerConstants::RemapKeysSettingName);
                    auto remapShortcutsData = jsonData.GetNamedObject(KeyboardManagerConstants::RemapShortcutsSettingName);
                    keyboardManagerState.ClearSingleKeyRemaps();

                    if (remapKeysData)
                    {
                        auto inProcessRemapKeys = remapKeysData.GetNamedArray(KeyboardManagerConstants::InProcessRemapKeysSettingName);
                        for (const auto& it : inProcessRemapKeys)
                        {
                            try
                            {
                                auto originalKey = it.GetObjectW().GetNamedString(KeyboardManagerConstants::OriginalKeysSettingName);
                                auto newRemapKey = it.GetObjectW().GetNamedString(KeyboardManagerConstants::NewRemapKeysSettingName);
                                keyboardManagerState.AddSingleKeyRemap(std::stoul(originalKey.c_str()), std::stoul(newRemapKey.c_str()));
                            }
                            catch (...)
                            {
                                // Improper Key Data JSON. Try the next remap.
                            }
                        }
                    }

                    keyboardManagerState.ClearOSLevelShortcuts();
                    if (remapShortcutsData)
                    {
                        auto globalRemapShortcuts = remapShortcutsData.GetNamedArray(KeyboardManagerConstants::GlobalRemapShortcutsSettingName);
                        for (const auto& it : globalRemapShortcuts)
                        {
                            try
                            {
                                auto originalKeys = it.GetObjectW().GetNamedString(KeyboardManagerConstants::OriginalKeysSettingName);
                                auto newRemapKeys = it.GetObjectW().GetNamedString(KeyboardManagerConstants::NewRemapKeysSettingName);
                                Shortcut originalSC(originalKeys.c_str());
                                Shortcut newRemapSC(newRemapKeys.c_str());
                                keyboardManagerState.AddOSLevelShortcut(originalSC, newRemapSC);
                            }
                            catch (...)
                            {
                                // Improper Key Data JSON. Try the next shortcut.
                            }
                        }
                    }
                }
            }
        }
        catch (...)
        {
            // Unable to load inital config.
        }
    }

    // This function is used to add the hardcoded mappings
    void init_map()
    {
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
    // nullptr as the last element of the array. Nullptr can also be returned for empty
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
        settings.set_overview_link(L"https://github.com/microsoft/PowerToys/blob/master/src/modules/keyboardmanager/README.md");

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
                if (!CheckEditKeyboardWindowActive() && !CheckEditShortcutsWindowActive())
                {
                    std::thread(createEditKeyboardWindow, hInstance, std::ref(keyboardManagerState)).detach();
                }
            }
            else if (action_object.get_name() == L"EditShortcut")
            {
                if (!CheckEditKeyboardWindowActive() && !CheckEditShortcutsWindowActive())
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
        // Log telemetry
        Trace::EnableKeyboardManager(true);
        // Start keyboard hook
        start_lowlevel_keyboard_hook();
    }

    // Disable the powertoy
    virtual void disable()
    {
        m_enabled = false;
        // Log telemetry
        Trace::EnableKeyboardManager(false);
        // Close active windows
        CloseActiveEditKeyboardWindow();
        CloseActiveEditShortcutsWindow();
        // Stop keyboard hook
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
                // Reset Num Lock whenever a NumLock key down event is suppressed since Num Lock key state change occurs before it is intercepted by low level hooks
                if (event.lParam->vkCode == VK_NUMLOCK && (event.wParam == WM_KEYDOWN || event.wParam == WM_SYSKEYDOWN) && event.lParam->dwExtraInfo != KeyboardManagerConstants::KEYBOARDMANAGER_SUPPRESS_FLAG)
                {
                    KeyboardEventHandlers::SetNumLockToPreviousState();
                }
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
        // If key has suppress flag, then suppress it
        if (data->lParam->dwExtraInfo == KeyboardManagerConstants::KEYBOARDMANAGER_SUPPRESS_FLAG)
        {
            return 1;
        }

        // If the Detect Key Window is currently activated, then suppress the keyboard event
        KeyboardManagerHelper::KeyboardHookDecision singleKeyRemapUIDetected = keyboardManagerState.DetectSingleRemapKeyUIBackend(data);
        if (singleKeyRemapUIDetected == KeyboardManagerHelper::KeyboardHookDecision::Suppress)
        {
            return 1;
        }
        else if (singleKeyRemapUIDetected == KeyboardManagerHelper::KeyboardHookDecision::SkipHook)
        {
            return 0;
        }

        // Remap a key
        intptr_t SingleKeyRemapResult = KeyboardEventHandlers::HandleSingleKeyRemapEvent(data, keyboardManagerState);

        // Single key remaps have priority. If a key is remapped, only the remapped version should be visible to the shortcuts and hence the event should be suppressed here.
        if (SingleKeyRemapResult == 1)
        {
            return 1;
        }

        // If the Detect Shortcut Window is currently activated, then suppress the keyboard event
        KeyboardManagerHelper::KeyboardHookDecision shortcutUIDetected = keyboardManagerState.DetectShortcutUIBackend(data);
        if (shortcutUIDetected == KeyboardManagerHelper::KeyboardHookDecision::Suppress)
        {
            return 1;
        }
        else if (shortcutUIDetected == KeyboardManagerHelper::KeyboardHookDecision::SkipHook)
        {
            return 0;
        }

        //// Remap a key to behave like a modifier instead of a toggle
        //intptr_t SingleKeyToggleToModResult = KeyboardEventHandlers::HandleSingleKeyToggleToModEvent(data, keyboardManagerState);

        //// Handle an app-specific shortcut remapping
        //intptr_t AppSpecificShortcutRemapResult = KeyboardEventHandlers::HandleAppSpecificShortcutRemapEvent(data, keyboardManagerState);

        //// If an app-specific shortcut is remapped then the os-level shortcut remapping should be suppressed.
        //if (AppSpecificShortcutRemapResult == 1)
        //{
        //    return 1;
        //}

        // Handle an os-level shortcut remapping
        intptr_t OSLevelShortcutRemapResult = KeyboardEventHandlers::HandleOSLevelShortcutRemapEvent(data, keyboardManagerState);

        // If any of the supported types of remappings took place, then suppress the key event
        if ((SingleKeyRemapResult + OSLevelShortcutRemapResult) > 0)
        {
            return 1;
        }
        else
        {
            return 0;
        }
    }
};

HHOOK KeyboardManager::hook_handle = nullptr;
HHOOK KeyboardManager::hook_handle_copy = nullptr;
KeyboardManager* KeyboardManager::keyboardmanager_object_ptr = nullptr;

extern "C" __declspec(dllexport) PowertoyModuleIface* __cdecl powertoy_create()
{
    return new KeyboardManager();
}