#pragma once
#include "pch.h"
#include <interface/powertoy_module_interface.h>
#include <common/SettingsAPI/settings_objects.h>
#include <common/interop/shared_constants.h>
#include <keyboardmanager/ui/EditKeyboardWindow.h>
#include <keyboardmanager/ui/EditShortcutsWindow.h>
#include <keyboardmanager/common/KeyboardManagerState.h>
#include <keyboardmanager/common/Shortcut.h>
#include <keyboardmanager/common/RemapShortcut.h>
#include <keyboardmanager/common/KeyboardManagerConstants.h>
#include <common/debug_control.h>
#include <common/utils/winapi_error.h>
#include <common/logger/logger_settings.h>
#include <keyboardmanager/common/trace.h>
#include <keyboardmanager/common/Helpers.h>
#include "KeyboardEventHandlers.h"
#include "Input.h"
#include <common/utils/file_watcher.h>

class KeyboardManager
{
private:
    //contains the non localized key of the powertoy
    std::wstring app_key = KeyboardManagerConstants::ModuleName;

    // Low level hook handles
    static HHOOK hook_handle;

    // Required for Unhook in old versions of Windows
    static HHOOK hook_handle_copy;

    // Static pointer to the current keyboardmanager object required for accessing the HandleKeyboardHookEvent function in the hook procedure (Only global or static variables can be accessed in a hook procedure CALLBACK)
    static KeyboardManager* keyboardmanager_object_ptr;

    // Variable which stores all the state information to be shared between the UI and back-end
    KeyboardManagerState keyboardManagerState;

    // Object of class which implements InputInterface. Required for calling library functions while enabling testing
    Input inputHandler;

    file_watcher watcher;

    std::mutex configAccessMutex;
public:
    // Constructor
    KeyboardManager()
    {
        // Load the initial configuration.
        load_config();

        // Set the static pointer to the newest object of the class
        keyboardmanager_object_ptr = this;

        std::filesystem::path modulePath(PTSettingsHelper::get_module_save_folder_location(app_key));
        auto changeConfigCallback = [this](DWORD err) {
            if (err != ERROR_SUCCESS)
            {
                Logger::error(L"Failed to watch settings changes. {}", get_last_error_or_default(err));
            }

            Logger::trace(L"Loading new config.");
            load_config();
        };

        watcher = std::move(file_watcher(changeConfigCallback, modulePath.c_str(), { L"settings.json", L"default.json" }));
    };

    // Load config from the saved settings.
    void load_config()
    {
        std::lock_guard configLock{ configAccessMutex };
        try
        {
            PowerToysSettings::PowerToyValues settings =
                PowerToysSettings::PowerToyValues::load_from_settings_file(app_key.c_str());
            auto current_config = settings.get_string_value(KeyboardManagerConstants::ActiveConfigurationSettingName);

            if (current_config)
            {
                keyboardManagerState.SetCurrentConfigName(*current_config);
                // Read the config file and load the remaps.
                auto configFile = json::from_file(PTSettingsHelper::get_module_save_folder_location(KeyboardManagerConstants::ModuleName) + L"\\" + *current_config + L".json");
                if (configFile)
                {
                    auto jsonData = *configFile;

                    // Load single key remaps
                    try
                    {
                        auto remapKeysData = jsonData.GetNamedObject(KeyboardManagerConstants::RemapKeysSettingName);
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

                                    // If remapped to a shortcut
                                    if (std::wstring(newRemapKey).find(L";") != std::string::npos)
                                    {
                                        keyboardManagerState.AddSingleKeyRemap(std::stoul(originalKey.c_str()), Shortcut(newRemapKey.c_str()));
                                    }

                                    // If remapped to a key
                                    else
                                    {
                                        keyboardManagerState.AddSingleKeyRemap(std::stoul(originalKey.c_str()), std::stoul(newRemapKey.c_str()));
                                    }
                                }
                                catch (...)
                                {
                                    // Improper Key Data JSON. Try the next remap.
                                }
                            }
                        }
                    }
                    catch (...)
                    {
                        // Improper JSON format for single key remaps. Skip to next remap type
                    }

                    // Load shortcut remaps
                    try
                    {
                        auto remapShortcutsData = jsonData.GetNamedObject(KeyboardManagerConstants::RemapShortcutsSettingName);
                        keyboardManagerState.ClearOSLevelShortcuts();
                        keyboardManagerState.ClearAppSpecificShortcuts();
                        if (remapShortcutsData)
                        {
                            // Load os level shortcut remaps
                            try
                            {
                                auto globalRemapShortcuts = remapShortcutsData.GetNamedArray(KeyboardManagerConstants::GlobalRemapShortcutsSettingName);
                                for (const auto& it : globalRemapShortcuts)
                                {
                                    try
                                    {
                                        auto originalKeys = it.GetObjectW().GetNamedString(KeyboardManagerConstants::OriginalKeysSettingName);
                                        auto newRemapKeys = it.GetObjectW().GetNamedString(KeyboardManagerConstants::NewRemapKeysSettingName);

                                        // If remapped to a shortcut
                                        if (std::wstring(newRemapKeys).find(L";") != std::string::npos)
                                        {
                                            keyboardManagerState.AddOSLevelShortcut(Shortcut(originalKeys.c_str()), Shortcut(newRemapKeys.c_str()));
                                        }

                                        // If remapped to a key
                                        else
                                        {
                                            keyboardManagerState.AddOSLevelShortcut(Shortcut(originalKeys.c_str()), std::stoul(newRemapKeys.c_str()));
                                        }
                                    }
                                    catch (...)
                                    {
                                        // Improper Key Data JSON. Try the next shortcut.
                                    }
                                }
                            }
                            catch (...)
                            {
                                // Improper JSON format for os level shortcut remaps. Skip to next remap type
                            }

                            // Load app specific shortcut remaps
                            try
                            {
                                auto appSpecificRemapShortcuts = remapShortcutsData.GetNamedArray(KeyboardManagerConstants::AppSpecificRemapShortcutsSettingName);
                                for (const auto& it : appSpecificRemapShortcuts)
                                {
                                    try
                                    {
                                        auto originalKeys = it.GetObjectW().GetNamedString(KeyboardManagerConstants::OriginalKeysSettingName);
                                        auto newRemapKeys = it.GetObjectW().GetNamedString(KeyboardManagerConstants::NewRemapKeysSettingName);
                                        auto targetApp = it.GetObjectW().GetNamedString(KeyboardManagerConstants::TargetAppSettingName);

                                        // If remapped to a shortcut
                                        if (std::wstring(newRemapKeys).find(L";") != std::string::npos)
                                        {
                                            keyboardManagerState.AddAppSpecificShortcut(targetApp.c_str(), Shortcut(originalKeys.c_str()), Shortcut(newRemapKeys.c_str()));
                                        }

                                        // If remapped to a key
                                        else
                                        {
                                            keyboardManagerState.AddAppSpecificShortcut(targetApp.c_str(), Shortcut(originalKeys.c_str()), std::stoul(newRemapKeys.c_str()));
                                        }
                                    }
                                    catch (...)
                                    {
                                        // Improper Key Data JSON. Try the next shortcut.
                                    }
                                }
                            }
                            catch (...)
                            {
                                // Improper JSON format for os level shortcut remaps. Skip to next remap type
                            }
                        }
                    }
                    catch (...)
                    {
                        // Improper JSON format for shortcut remaps. Skip to next remap type
                    }
                }
            }
        }
        catch (...)
        {
            // Unable to load inital config.
        }
    }

    // Hook procedure definition
    static LRESULT CALLBACK hook_proc(int nCode, WPARAM wParam, LPARAM lParam)
    {
        LowlevelKeyboardEvent event;
        if (nCode == HC_ACTION)
        {
            event.lParam = reinterpret_cast<KBDLLHOOKSTRUCT*>(lParam);
            event.wParam = wParam;
            std::lock_guard configLock{ keyboardmanager_object_ptr->configAccessMutex };
            if (keyboardmanager_object_ptr->HandleKeyboardHookEvent(&event) == 1)
            {
                // Reset Num Lock whenever a NumLock key down event is suppressed since Num Lock key state change occurs before it is intercepted by low level hooks
                if (event.lParam->vkCode == VK_NUMLOCK && (event.wParam == WM_KEYDOWN || event.wParam == WM_SYSKEYDOWN) && event.lParam->dwExtraInfo != KeyboardManagerConstants::KEYBOARDMANAGER_SUPPRESS_FLAG)
                {
                    KeyboardEventHandlers::SetNumLockToPreviousState(keyboardmanager_object_ptr->inputHandler);
                }
                return 1;
            }
        }
        return CallNextHookEx(hook_handle_copy, nCode, wParam, lParam);
    }

    void start_lowlevel_keyboard_hook()
    {
#if defined(DISABLE_LOWLEVEL_HOOKS_WHEN_DEBUGGED)
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
                DWORD errorCode = GetLastError();
                show_last_error_message(L"SetWindowsHookEx", errorCode, L"PowerToys - Keyboard Manager");
                auto errorMessage = get_last_error_message(errorCode);
                Trace::Error(errorCode, errorMessage.has_value() ? errorMessage.value() : L"", L"start_lowlevel_keyboard_hook.SetWindowsHookEx");
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
        // If remappings are disabled (due to the remap tables getting updated) skip the rest of the hook
        if (!keyboardManagerState.AreRemappingsEnabled())
        {
            return 0;
        }

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

        // If the Detect Shortcut Window from Remap Keys is currently activated, then suppress the keyboard event
        KeyboardManagerHelper::KeyboardHookDecision remapKeyShortcutUIDetected = keyboardManagerState.DetectShortcutUIBackend(data, true);
        if (remapKeyShortcutUIDetected == KeyboardManagerHelper::KeyboardHookDecision::Suppress)
        {
            return 1;
        }
        else if (remapKeyShortcutUIDetected == KeyboardManagerHelper::KeyboardHookDecision::SkipHook)
        {
            return 0;
        }

        // Remap a key
        intptr_t SingleKeyRemapResult = KeyboardEventHandlers::HandleSingleKeyRemapEvent(inputHandler, data, keyboardManagerState);

        // Single key remaps have priority. If a key is remapped, only the remapped version should be visible to the shortcuts and hence the event should be suppressed here.
        if (SingleKeyRemapResult == 1)
        {
            return 1;
        }

        // If the Detect Shortcut Window is currently activated, then suppress the keyboard event
        KeyboardManagerHelper::KeyboardHookDecision shortcutUIDetected = keyboardManagerState.DetectShortcutUIBackend(data, false);
        if (shortcutUIDetected == KeyboardManagerHelper::KeyboardHookDecision::Suppress)
        {
            return 1;
        }
        else if (shortcutUIDetected == KeyboardManagerHelper::KeyboardHookDecision::SkipHook)
        {
            return 0;
        }

        /* This feature has not been enabled (code from proof of concept stage)
        * 
        //// Remap a key to behave like a modifier instead of a toggle
        //intptr_t SingleKeyToggleToModResult = KeyboardEventHandlers::HandleSingleKeyToggleToModEvent(inputHandler, data, keyboardManagerState);
        */

        // Handle an app-specific shortcut remapping
        intptr_t AppSpecificShortcutRemapResult = KeyboardEventHandlers::HandleAppSpecificShortcutRemapEvent(inputHandler, data, keyboardManagerState);

        // If an app-specific shortcut is remapped then the os-level shortcut remapping should be suppressed.
        if (AppSpecificShortcutRemapResult == 1)
        {
            return 1;
        }

        // Handle an os-level shortcut remapping
        return KeyboardEventHandlers::HandleOSLevelShortcutRemapEvent(inputHandler, data, keyboardManagerState);
    }
};

HHOOK KeyboardManager::hook_handle_copy;
HHOOK KeyboardManager::hook_handle;
KeyboardManager* KeyboardManager::keyboardmanager_object_ptr;