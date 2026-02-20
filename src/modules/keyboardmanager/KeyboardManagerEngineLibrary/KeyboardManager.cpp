#include "pch.h"
#include "KeyboardManager.h"
#include <interface/powertoy_module_interface.h>
#include <common/SettingsAPI/settings_objects.h>
#include <common/interop/shared_constants.h>
#include <common/debug_control.h>
#include <common/utils/winapi_error.h>
#include <common/logger/logger_settings.h>

#include <keyboardmanager/common/KeyboardManagerConstants.h>
#include <keyboardmanager/common/Helpers.h>
#include <keyboardmanager/common/KeyboardEventHandlers.h>
#include <ctime>

#include "KeyboardEventHandlers.h"
#include "trace.h"

HHOOK KeyboardManager::hookHandleCopy;
HHOOK KeyboardManager::hookHandle;
HHOOK KeyboardManager::mouseHookHandleCopy;
HHOOK KeyboardManager::mouseHookHandle;
KeyboardManager* KeyboardManager::keyboardManagerObjectPtr;

namespace
{
    DWORD mainThreadId = {};
}

KeyboardManager::KeyboardManager()
{
    mainThreadId = GetCurrentThreadId();

    // Load the initial settings.
    LoadSettings();

    // Set the static pointer to the newest object of the class
    keyboardManagerObjectPtr = this;

    std::filesystem::path modulePath(PTSettingsHelper::get_module_save_folder_location(moduleName));
    auto changeSettingsCallback = [this](DWORD err) {
        Logger::trace(L"{} event was signaled", KeyboardManagerConstants::SettingsEventName);
        if (err != ERROR_SUCCESS)
        {
            Logger::error(L"Failed to watch settings changes. {}", get_last_error_or_default(err));
        }

        loadingSettings = true;
        bool loadedSuccessfully = false;
        try
        {
            LoadSettings();
            loadedSuccessfully = true;
        }
        catch (...)
        {
            Logger::error("Failed to load settings");
        }

        loadingSettings = false;

        if (!loadedSuccessfully)
            return;

        const bool newHasRemappings = HasRegisteredRemappingsUnchecked();
        // We didn't have any bindings before and we have now
        if (newHasRemappings && !hookHandle)
            PostThreadMessageW(mainThreadId, StartHookMessageID, 0, 0);

        // All bindings were removed
        if (!newHasRemappings && hookHandle)
            StopLowlevelKeyboardHook();

        // Handle mouse hook based on mouse remappings
        // Use PostThreadMessage to ensure hooks are managed from the main message loop thread
        const bool newHasMouseRemappings = HasMouseRemappings();
        if (newHasMouseRemappings && !mouseHookHandle)
            PostThreadMessageW(mainThreadId, StartMouseHookMessageID, 0, 0);

        if (!newHasMouseRemappings && mouseHookHandle)
            PostThreadMessageW(mainThreadId, StopMouseHookMessageID, 0, 0);
    };

    editorIsRunningEvent = CreateEvent(nullptr, true, false, KeyboardManagerConstants::EditorWindowEventName.c_str());
    settingsEventWaiter.start(KeyboardManagerConstants::SettingsEventName, changeSettingsCallback);
}

void KeyboardManager::LoadSettings()
{
    bool loadedSuccessful = state.LoadSettings();
    if (!loadedSuccessful)
    {
        std::this_thread::sleep_for(std::chrono::milliseconds(500));

        // retry once
        state.LoadSettings();
    }
    try
    {
        // Send telemetry about configured key/shortcut to key/shortcut mappings, OS an app specific level.
        Trace::SendKeyAndShortcutRemapLoadedConfiguration(state);
    }
    catch (...)
    {
        try
        {
            Logger::error("Failed to send telemetry for the configured remappings.");
            // Try not to crash the app sending telemetry. Everything inside a try.
            Trace::ErrorSendingKeyAndShortcutRemapLoadedConfiguration();
        }
        catch (...)
        {

        }
    }
}

LRESULT CALLBACK KeyboardManager::HookProc(int nCode, const WPARAM wParam, const LPARAM lParam)
{
    LowlevelKeyboardEvent event{};
    if (nCode == HC_ACTION)
    {
        event.lParam = reinterpret_cast<KBDLLHOOKSTRUCT*>(lParam);
        event.wParam = wParam;
        event.lParam->vkCode = Helpers::EncodeKeyNumpadOrigin(event.lParam->vkCode, event.lParam->flags & LLKHF_EXTENDED);

        if (keyboardManagerObjectPtr->HandleKeyboardHookEvent(&event) == 1)
        {
            // Reset Num Lock whenever a NumLock key down event is suppressed since Num Lock key state change occurs before it is intercepted by low level hooks
            if (event.lParam->vkCode == VK_NUMLOCK && (event.wParam == WM_KEYDOWN || event.wParam == WM_SYSKEYDOWN) && event.lParam->dwExtraInfo != KeyboardManagerConstants::KEYBOARDMANAGER_SUPPRESS_FLAG)
            {
                KeyboardEventHandlers::SetNumLockToPreviousState(keyboardManagerObjectPtr->inputHandler);
            }
            return 1;
        }
    }

    return CallNextHookEx(hookHandleCopy, nCode, wParam, lParam);
}

void KeyboardManager::StartLowlevelKeyboardHook()
{
#if defined(DISABLE_LOWLEVEL_HOOKS_WHEN_DEBUGGED)
    if (IsDebuggerPresent())
    {
        return;
    }
#endif

    if (!hookHandle)
    {
        hookHandle = SetWindowsHookEx(WH_KEYBOARD_LL, HookProc, GetModuleHandle(NULL), NULL);
        hookHandleCopy = hookHandle;
        if (!hookHandle)
        {
            DWORD errorCode = GetLastError();
            show_last_error_message(L"SetWindowsHookEx", errorCode, L"PowerToys - Keyboard Manager");
            auto errorMessage = get_last_error_message(errorCode);
            Trace::Error(errorCode, errorMessage.has_value() ? errorMessage.value() : L"", L"StartLowlevelKeyboardHook::SetWindowsHookEx");
        }
    }
}

void KeyboardManager::StopLowlevelKeyboardHook()
{
    if (hookHandle)
    {
        UnhookWindowsHookEx(hookHandle);
        hookHandle = nullptr;
    }
}

void KeyboardManager::StartLowlevelMouseHook()
{
#if defined(DISABLE_LOWLEVEL_HOOKS_WHEN_DEBUGGED)
    if (IsDebuggerPresent())
    {
        return;
    }
#endif

    if (!mouseHookHandle)
    {
        Logger::info(L"Starting low-level mouse hook");
        mouseHookHandle = SetWindowsHookEx(WH_MOUSE_LL, MouseHookProc, GetModuleHandle(NULL), NULL);
        mouseHookHandleCopy = mouseHookHandle;
        if (!mouseHookHandle)
        {
            DWORD errorCode = GetLastError();
            show_last_error_message(L"SetWindowsHookEx", errorCode, L"PowerToys - Keyboard Manager (Mouse)");
            auto errorMessage = get_last_error_message(errorCode);
            Trace::Error(errorCode, errorMessage.has_value() ? errorMessage.value() : L"", L"StartLowlevelMouseHook::SetWindowsHookEx");
        }
        else
        {
            Logger::info(L"Low-level mouse hook started successfully");
        }
    }
}

void KeyboardManager::StopLowlevelMouseHook()
{
    if (mouseHookHandle)
    {
        Logger::info(L"Stopping low-level mouse hook");
        if (UnhookWindowsHookEx(mouseHookHandle))
        {
            Logger::info(L"Low-level mouse hook stopped successfully");
        }
        else
        {
            DWORD errorCode = GetLastError();
            Logger::error(L"Failed to unhook mouse hook. Error: {}", errorCode);
        }
        mouseHookHandle = nullptr;
        mouseHookHandleCopy = nullptr;
    }
}

LRESULT CALLBACK KeyboardManager::MouseHookProc(int nCode, const WPARAM wParam, const LPARAM lParam)
{
    if (nCode == HC_ACTION)
    {
        MSLLHOOKSTRUCT* mouseData = reinterpret_cast<MSLLHOOKSTRUCT*>(lParam);

        if (keyboardManagerObjectPtr->HandleMouseHookEvent(wParam, mouseData) == 1)
        {
            return 1;
        }
    }

    return CallNextHookEx(mouseHookHandleCopy, nCode, wParam, lParam);
}

intptr_t KeyboardManager::HandleMouseHookEvent(WPARAM wParam, MSLLHOOKSTRUCT* data) noexcept
{
    if (loadingSettings)
    {
        return 0;
    }

    // Suspend remapping if remap key/shortcut window is opened
    if (editorIsRunningEvent != nullptr && WaitForSingleObject(editorIsRunningEvent, 0) == WAIT_OBJECT_0)
    {
        return 0;
    }

    // If the event was injected by us, don't process it
    if (data->dwExtraInfo == KeyboardManagerConstants::KEYBOARDMANAGER_MOUSE_FLAG)
    {
        return 0;
    }

    // Get the mouse button from the message
    auto mouseButton = MouseButtonHelpers::MouseButtonFromMessage(wParam, data->mouseData);
    if (!mouseButton.has_value())
    {
        return 0; // Not a button we care about (e.g., mouse move)
    }

    bool isButtonDown = MouseButtonHelpers::IsMouseButtonDown(wParam);

    // Try app-specific mouse button remap first
    intptr_t appSpecificResult = KeyboardEventHandlers::HandleAppSpecificMouseButtonRemapEvent(inputHandler, *mouseButton, isButtonDown, state);
    if (appSpecificResult == 1)
    {
        return 1;
    }

    // Handle global mouse button remap
    return KeyboardEventHandlers::HandleMouseButtonRemapEvent(inputHandler, *mouseButton, isButtonDown, state);
}

bool KeyboardManager::HasRegisteredRemappings() const
{
    constexpr int MaxAttempts = 5;

    if (loadingSettings)
    {
        for (int currentAttempt = 0; currentAttempt < MaxAttempts; ++currentAttempt)
        {
            std::this_thread::sleep_for(std::chrono::milliseconds(500));
            if (!loadingSettings)
                break;
        }
    }

    // Assume that we have registered remappings to be on the safe side if we couldn't check
    if (loadingSettings)
        return true;

    return HasRegisteredRemappingsUnchecked();
}

// Checks whether any keyboard-hook remappings are registered.
// Note: mouseButtonReMap and appSpecificMouseButtonReMap are intentionally excluded
// because they are handled by a separate mouse hook (see HasMouseRemappings).
bool KeyboardManager::HasRegisteredRemappingsUnchecked() const
{
    return !(state.appSpecificShortcutReMap.empty() && state.appSpecificShortcutReMapSortedKeys.empty() && state.osLevelShortcutReMap.empty() && state.osLevelShortcutReMapSortedKeys.empty() && state.singleKeyReMap.empty() && state.singleKeyToTextReMap.empty() && state.keyToMouseReMap.empty() && state.appSpecificKeyToMouseReMap.empty());
}

bool KeyboardManager::HasMouseRemappings() const
{
    return !state.mouseButtonReMap.empty() || !state.appSpecificMouseButtonReMap.empty();
}

intptr_t KeyboardManager::HandleKeyboardHookEvent(LowlevelKeyboardEvent* data) noexcept
{
    if (loadingSettings)
    {
        return 0;
    }

    // Suspend remapping if remap key/shortcut window is opened
    if (editorIsRunningEvent != nullptr && WaitForSingleObject(editorIsRunningEvent, 0) == WAIT_OBJECT_0)
    {
        return 0;
    }

    // If key has suppress flag, then suppress it
    if (data->lParam->dwExtraInfo == KeyboardManagerConstants::KEYBOARDMANAGER_SUPPRESS_FLAG)
    {
        return 1;
    }

    // Remap a key
    intptr_t SingleKeyRemapResult = KeyboardEventHandlers::HandleSingleKeyRemapEvent(inputHandler, data, state);

    // Single key remaps have priority. If a key is remapped, only the remapped version should be visible to the shortcuts and hence the event should be suppressed here.
    if (SingleKeyRemapResult == 1)
    {
        return 1;
    }

    // Handle app-specific key-to-mouse remap (pressing a key triggers a mouse click in specific app)
    intptr_t AppSpecificKeyToMouseRemapResult = KeyboardEventHandlers::HandleAppSpecificKeyToMouseRemapEvent(inputHandler, data, state);

    if (AppSpecificKeyToMouseRemapResult == 1)
    {
        return 1;
    }

    // Handle global key-to-mouse remap (pressing a key triggers a mouse click)
    intptr_t KeyToMouseRemapResult = KeyboardEventHandlers::HandleKeyToMouseRemapEvent(inputHandler, data, state);

    if (KeyToMouseRemapResult == 1)
    {
        return 1;
    }

    /* This feature has not been enabled (code from proof of concept stage)
        // Remap a key to behave like a modifier instead of a toggle
        intptr_t SingleKeyToggleToModResult = KeyboardEventHandlers::HandleSingleKeyToggleToModEvent(inputHandler, data, keyboardManagerState);
    */

    // Handle an app-specific shortcut remapping
    intptr_t AppSpecificShortcutRemapResult = KeyboardEventHandlers::HandleAppSpecificShortcutRemapEvent(inputHandler, data, state);

    // If an app-specific shortcut is remapped then the os-level shortcut remapping should be suppressed.
    if (AppSpecificShortcutRemapResult == 1)
    {
        return 1;
    }

    intptr_t SingleKeyToTextRemapResult = KeyboardEventHandlers::HandleSingleKeyToTextRemapEvent(inputHandler, data, state);

    if (SingleKeyToTextRemapResult == 1)
    {
        return 1;
    }

    // Handle an os-level shortcut remapping
    return KeyboardEventHandlers::HandleOSLevelShortcutRemapEvent(inputHandler, data, state);
}
