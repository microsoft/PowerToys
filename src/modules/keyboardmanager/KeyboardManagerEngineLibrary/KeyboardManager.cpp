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

    // Load the initial settings. This is the hook thread and no hook is installed yet, so the
    // key-sequence table can be published right here.
    LoadSettings();
    state.PublishStagedSequenceTable();

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

        // This is a worker thread. The key-sequence table was parsed into the loader's own table and
        // staged by LoadSettings, and this thread does nothing more about it. The hook thread makes
        // the staged table live itself: at the next key event if the hook is installed (see
        // KeyboardEventHandlers::DispatchKeyboardEvent), or else when it installs the hook (see
        // StartLowlevelKeyboardHook).

        const bool newHasRemappings = HasRegisteredRemappingsUnchecked();
        noRemappingsLoaded = !newHasRemappings;

        // We didn't have any bindings before and we have now
        if (newHasRemappings && !hookHandle)
            PostThreadMessageW(mainThreadId, StartHookMessageID, 0, 0);

        // All bindings were removed. The hook is removed by the hook thread, which knows whether a
        // press that a key sequence replaced is still held (see StopLowlevelKeyboardHookWhenIdle).
        if (!newHasRemappings && hookHandle)
            PostThreadMessageW(mainThreadId, StopHookMessageID, 0, 0);

        // The set of "Alone" (dual-key) remaps may have changed without changing whether ANY binding
        // exists, so reconcile the companion mouse hook. Route an install through the same StartHook
        // message (SetWindowsHookEx must run on the hook thread); a removal is done inline (the mouse
        // hook suppresses nothing, so no press depends on it).
        const bool needMouseHook = !state.aloneSingleKeyReMap.empty();
        if (needMouseHook && hookHandle && !mouseHookHandle)
            PostThreadMessageW(mainThreadId, StartHookMessageID, 0, 0);
        else if (!needMouseHook && mouseHookHandle)
            StopLowlevelMouseHook();
    };

    editorIsRunningEvent = CreateEvent(nullptr, true, false, KeyboardManagerConstants::EditorWindowEventName.c_str());
    settingsEventWaiter.start(KeyboardManagerConstants::SettingsEventName, changeSettingsCallback);
}

void KeyboardManager::LoadSettings()
{
    // The settings are read and parsed into a configuration of their own, without touching the tables
    // in use, and swapped in afterwards (State::ReplaceLoadedMappings), so the hook never sees tables
    // that are half rebuilt.
    MappingConfiguration loaded;
    if (!loaded.LoadSettings())
    {
        std::this_thread::sleep_for(std::chrono::milliseconds(500));

        // retry once
        MappingConfiguration retried;
        retried.LoadSettings();
        if (retried.IsConfigurationFileRead() || !loaded.IsConfigurationFileRead())
        {
            loaded = std::move(retried);
        }
    }

    // A load that could not read the configuration file left the tables as they were before (it
    // returned before changing them), and so does this one. The swap also forgets the "Alone"
    // runtime state, so a key held across the reload can't leave a stale pending/combination entry.
    if (loaded.IsConfigurationFileRead())
    {
        state.ReplaceLoadedMappings(std::move(loaded));
    }

    // The key-sequence remaps are not applied by the loading thread. Hand a finished copy over to
    // the hook thread instead (see State::PublishStagedSequenceTable). This covers the key-sequence
    // table and its press records only; the other tables are swapped in above, under loadingSettings
    // as before.
    state.StageSequenceTable();
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

    // Suspend while the remap key/shortcut editor window is capturing input, mirroring
    // HandleKeyboardHookEvent.
    if (editorIsRunningEvent != nullptr && WaitForSingleObject(editorIsRunningEvent, 0) == WAIT_OBJECT_0)
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
        // The new hook starts with the key-sequence table of the settings loaded last. No event was
        // seen while there was no hook, so the press records start afresh; if there are key-sequence
        // remaps, the keys that are down right now are recorded as presses that already reached the
        // system. This runs on the hook thread, before the hook can deliver anything. (A hook that
        // is installed already publishes a staged table itself, at its next key event.)
        state.StartSequencePressTracking(inputHandler);

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

    // The "Alone" (dual-key) feature also needs a mouse hook so a click/scroll while an alone key is
    // held counts as a combination. Only install it when alone remaps exist; keep it in sync with the
    // keyboard hook (this runs on the hook message-loop thread, where SetWindowsHookEx must be called).
    if (!state.aloneSingleKeyReMap.empty())
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
    }

    StopLowlevelMouseHook();
}

void KeyboardManager::StopLowlevelKeyboardHookWhenIdle()
{
    // Remappings were loaded again since the message was posted, or there is no hook.
    if (!noRemappingsLoaded || !hookHandle)
    {
        hookStopPending = false;
        return;
    }

    // A press that a key sequence replaced is still held. Without the hook its auto-repeats and its
    // key-up would reach the app, for a key-down the app never got. HandleKeyboardHookEvent posts
    // this message again once no such press is left.
    if (state.SequencePresses().HasSuppressedPress(GetTickCount(), inputHandler))
    {
        hookStopPending = true;
        return;
    }

    hookStopPending = false;
    StopLowlevelKeyboardHook();
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

bool KeyboardManager::HasRegisteredRemappingsUnchecked() const
{
    return !(state.appSpecificShortcutReMap.empty() && state.appSpecificShortcutReMapSortedKeys.empty() && state.osLevelShortcutReMap.empty() && state.osLevelShortcutReMapSortedKeys.empty() && state.singleKeyReMap.empty() && state.aloneSingleKeyReMap.empty() && state.singleKeyToSequenceReMap.empty() && state.singleKeyToTextReMap.empty());
}

intptr_t KeyboardManager::HandleKeyboardHookEvent(LowlevelKeyboardEvent* data) noexcept
{
    // Remapping is suspended while the settings are being loaded, and while the remap key/shortcut
    // window is opened.
    const bool remappingSuspended = loadingSettings || (editorIsRunningEvent != nullptr && WaitForSingleObject(editorIsRunningEvent, 0) == WAIT_OBJECT_0);

    // The handler chain lives in KeyboardEventHandlers so the unit tests can run it as a whole.
    const intptr_t result = KeyboardEventHandlers::DispatchKeyboardEvent(inputHandler, data, state, remappingSuspended);

    // The hook is to be removed once the presses that key sequences replaced have ended. This event
    // may have ended the last one (its key-up was just suppressed): ask the hook thread to try
    // again, from its message loop rather than from inside the hook.
    if (hookStopPending && !state.SequencePresses().HasSuppressedPress(data->lParam->time, inputHandler))
    {
        hookStopPending = false;
        PostThreadMessageW(mainThreadId, StopHookMessageID, 0, 0);
    }

    return result;
}
