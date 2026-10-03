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
HHOOK KeyboardManager::mouseHookHandle;
HHOOK KeyboardManager::mouseHookHandleCopy;
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
    hasRegisteredRemappings = HasRegisteredRemappingsUnchecked();
    hasAloneRemappings = !state.aloneSingleKeyReMap.empty();

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
            hasRegisteredRemappings = HasRegisteredRemappingsUnchecked();
            hasAloneRemappings = !state.aloneSingleKeyReMap.empty();
            loadedSuccessfully = true;
        }
        catch (...)
        {
            Logger::error("Failed to load settings");
        }

        loadingSettings = false;

        if (!loadedSuccessfully)
            return;

        // Serialize both start and stop with hook callbacks. Unhooking from this
        // worker could let an outstanding callback overwrite capture readiness.
        PostThreadMessageW(mainThreadId, UpdateHookMessageID, 0, 0);
    };

    editorIsRunningEvent = CreateEvent(nullptr, true, false, KeyboardManagerConstants::EditorWindowEventName.c_str());
    editorLifetimeMutex = CreateMutex(nullptr, false, KeyboardManagerConstants::EditorWindowMutexName.c_str());
    editorCaptureReadyEvent = CreateEvent(nullptr, true, true, KeyboardManagerConstants::EditorCaptureReadyEventName.c_str());
    if (editorCaptureReadyEvent)
    {
        SetEvent(editorCaptureReadyEvent);
    }

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

    // The reload above rebuilt the alone remap table; discard any leftover alone runtime state so a key
    // that was physically held across the reload can't leave a stale pending/combination entry (which a
    // later event would promote, injecting an unmatched original key-down). No-op on the initial load.
    state.ClearAllAloneKeyState();
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
    if (nCode != HC_ACTION)
    {
        return CallNextHookEx(hookHandleCopy, nCode, wParam, lParam);
    }

    LowlevelKeyboardEvent event{};
    event.lParam = reinterpret_cast<KBDLLHOOKSTRUCT*>(lParam);
    event.wParam = wParam;
    event.lParam->vkCode = Helpers::EncodeKeyNumpadOrigin(event.lParam->vkCode, event.lParam->flags & LLKHF_EXTENDED);

    EditorCaptureReadyScope publishCaptureReady(
        keyboardManagerObjectPtr->editorSuspensionState,
        keyboardManagerObjectPtr->editorCaptureReadyEvent,
        EditorSuspensionState::IsSourceEvent(event));

    if (keyboardManagerObjectPtr->HandleKeyboardHookEvent(&event) == 1)
    {
        // Reset Num Lock whenever a NumLock key down event is suppressed since Num Lock key state change occurs before it is intercepted by low level hooks
        if (event.lParam->vkCode == VK_NUMLOCK && (event.wParam == WM_KEYDOWN || event.wParam == WM_SYSKEYDOWN) && event.lParam->dwExtraInfo != KeyboardManagerConstants::KEYBOARDMANAGER_SUPPRESS_FLAG)
        {
            KeyboardEventHandlers::SetNumLockToPreviousState(keyboardManagerObjectPtr->inputHandler);
        }
        return 1;
    }

    return CallNextHookEx(hookHandleCopy, nCode, wParam, lParam);
}

LRESULT CALLBACK KeyboardManager::MouseHookProc(int nCode, const WPARAM wParam, const LPARAM lParam)
{
    if (nCode == HC_ACTION)
    {
        // Only a deliberate button-press or wheel means a held alone key is being used in
        // combination. Mouse MOVE must NOT count, or an incidental move would swallow the tap.
        // Mouse events are never suppressed here — we only promote the held key, then pass through.
        switch (wParam)
        {
        case WM_LBUTTONDOWN:
        case WM_RBUTTONDOWN:
        case WM_MBUTTONDOWN:
        case WM_XBUTTONDOWN:
        case WM_MOUSEWHEEL:
        case WM_MOUSEHWHEEL:
            keyboardManagerObjectPtr->HandleMouseHookEvent();
            break;
        default:
            break;
        }
    }

    return CallNextHookEx(mouseHookHandleCopy, nCode, wParam, lParam);
}

void KeyboardManager::HandleMouseHookEvent() noexcept
{
    if (loadingSettings)
    {
        return;
    }

    // Finish any gesture that began before the editor opened, just like the
    // keyboard hook. Keys pressed during suspension never become tap candidates.
    if (editorSuspensionState.IsSuspended())
    {
        return;
    }

    // Common path: no alone key is held, so a click/scroll is none of our business.
    if (!state.HasPendingAloneKeys())
    {
        return;
    }

    // An alone-mapped key is held and the user clicked/scrolled: promote it to a real modifier so
    // the mouse action is seen in combination (e.g. Ctrl+Click, Ctrl+Wheel). The matching real
    // key-up is injected by the keyboard handler when the alone key is released.
    KeyboardEventHandlers::PromotePendingAloneKeysToCombination(inputHandler, state);
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
        // Releases while the hook was stopped could not be observed.
        const bool editorOpen =
            (editorIsRunningEvent != nullptr && WaitForSingleObject(editorIsRunningEvent, 0) == WAIT_OBJECT_0) ||
            IsEditorMutexOwned(editorLifetimeMutex);
        editorSuspensionState.Reset(editorOpen);
        hookHandle = SetWindowsHookEx(WH_KEYBOARD_LL, HookProc, GetModuleHandle(NULL), NULL);
        hookHandleCopy = hookHandle;
        if (!hookHandle)
        {
            DWORD errorCode = GetLastError();
            show_last_error_message(L"SetWindowsHookEx", errorCode, L"PowerToys - Keyboard Manager");
            auto errorMessage = get_last_error_message(errorCode);
            Trace::Error(errorCode, errorMessage.has_value() ? errorMessage.value() : L"", L"StartLowlevelKeyboardHook::SetWindowsHookEx");
        }
        else
        {
            // Sample after installation so releases cannot fall into a gap before
            // the hook exists. These modifiers can already participate in shortcuts.
            for (const DWORD key : { VK_LCONTROL, VK_RCONTROL, VK_LMENU, VK_RMENU, VK_LSHIFT, VK_RSHIFT, VK_LWIN, VK_RWIN })
            {
                if (inputHandler.GetVirtualKeyState(key))
                {
                    editorSuspensionState.AddInitiallyPressedModifier(key);
                }
            }
        }

        if (editorCaptureReadyEvent)
        {
            if (editorSuspensionState.IsIdle())
            {
                SetEvent(editorCaptureReadyEvent);
            }
            else
            {
                ResetEvent(editorCaptureReadyEvent);
            }
        }
    }

    // The "Alone" (dual-key) feature also needs a mouse hook so a click/scroll while an alone key is
    // held counts as a combination. Only install it when alone remaps exist; keep it in sync with the
    // keyboard hook (this runs on the hook message-loop thread, where SetWindowsHookEx must be called).
    if (hookHandle && hasAloneRemappings)
    {
        StartLowlevelMouseHook();
    }
    else
    {
        StopLowlevelMouseHook();
    }
}

void KeyboardManager::StopLowlevelKeyboardHook()
{
    if (hookHandle)
    {
        UnhookWindowsHookEx(hookHandle);
        hookHandle = nullptr;
        if (editorCaptureReadyEvent)
        {
            SetEvent(editorCaptureReadyEvent);
        }
    }

    StopLowlevelMouseHook();
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
        mouseHookHandle = SetWindowsHookEx(WH_MOUSE_LL, MouseHookProc, GetModuleHandle(NULL), NULL);
        mouseHookHandleCopy = mouseHookHandle;
        if (!mouseHookHandle)
        {
            DWORD errorCode = GetLastError();
            show_last_error_message(L"SetWindowsHookEx", errorCode, L"PowerToys - Keyboard Manager");
            auto errorMessage = get_last_error_message(errorCode);
            Trace::Error(errorCode, errorMessage.has_value() ? errorMessage.value() : L"", L"StartLowlevelMouseHook::SetWindowsHookEx");
        }
    }
}

void KeyboardManager::StopLowlevelMouseHook()
{
    if (mouseHookHandle)
    {
        UnhookWindowsHookEx(mouseHookHandle);
        mouseHookHandle = nullptr;
    }
}

void KeyboardManager::UpdateLowlevelKeyboardHook()
{
    // Queued updates use the latest completed configuration. Reading this published
    // value never blocks the hook thread or races a reload of the mapping tables.
    if (hasRegisteredRemappings)
    {
        StartLowlevelKeyboardHook();
    }
    else
    {
        StopLowlevelKeyboardHook();
    }
}

bool KeyboardManager::HasRegisteredRemappingsUnchecked() const
{
    return !(state.appSpecificShortcutReMap.empty() && state.appSpecificShortcutReMapSortedKeys.empty() && state.osLevelShortcutReMap.empty() && state.osLevelShortcutReMapSortedKeys.empty() && state.singleKeyReMap.empty() && state.aloneSingleKeyReMap.empty() && state.singleKeyToTextReMap.empty());
}

intptr_t KeyboardManager::HandleKeyboardHookEvent(LowlevelKeyboardEvent* data) noexcept
{
    const bool suspensionRequested =
        (editorIsRunningEvent != nullptr && WaitForSingleObject(editorIsRunningEvent, 0) == WAIT_OBJECT_0) ||
        IsEditorMutexOwned(editorLifetimeMutex);
    const bool wasSuspended = editorSuspensionState.IsSuspended();
    bool skipSingleKeyRemapping = false;
    const bool skipRemapping = editorSuspensionState.ShouldSkipRemapping(*data, suspensionRequested, &skipSingleKeyRemapping);

    // Keep tracking source releases even while settings are loading.
    if (loadingSettings)
    {
        return 0;
    }

    if (!wasSuspended && editorSuspensionState.IsSuspended())
    {
        // A chord prefix must not survive a trip through the editor.
        KeyboardEventHandlers::ResetAllStartedChords(state, std::nullopt);
        for (auto& app : state.appSpecificShortcutReMapSortedKeys)
        {
            KeyboardEventHandlers::ResetAllStartedChords(state, app.first);
        }
    }

    // If key has suppress flag, then suppress it
    if (data->lParam->dwExtraInfo == KeyboardManagerConstants::KEYBOARDMANAGER_SUPPRESS_FLAG)
    {
        return 1;
    }

    if (skipRemapping)
    {
        return 0;
    }
    // Remap a key tapped alone (dual-key). Runs before the regular single-key remap so it can hold
    // the key-down (lazy) and decide tap-vs-combination; if it handles the event, suppress the original.
    if (!skipSingleKeyRemapping && KeyboardEventHandlers::HandleSingleKeyAloneRemapEvent(inputHandler, data, state) == 1)
    {
        return 1;
    }

    // A modifier held before hook startup has no single-key remap to release,
    // but its up must still finish any shortcut invoked since startup.
    if (!skipSingleKeyRemapping && KeyboardEventHandlers::HandleSingleKeyRemapEvent(inputHandler, data, state) == 1)
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
