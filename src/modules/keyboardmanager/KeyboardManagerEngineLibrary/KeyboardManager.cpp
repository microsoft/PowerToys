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

        keyboardManagerObjectPtr->lastHookEventTick = GetTickCount();

        // Answer the watchdog probe and swallow it so it never reaches anything else.
        if (event.lParam->dwExtraInfo == KeyboardManagerConstants::KEYBOARDMANAGER_HOOK_PROBE_FLAG)
        {
            keyboardManagerObjectPtr->hookProbePending = false;
            keyboardManagerObjectPtr->missedHookProbes = 0;
            return 1;
        }

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

// Windows silently unregisters a low level hook whose callback exceeds
// LowLevelHooksTimeout (300 ms by default), and there is no notification and no API to ask
// whether a HHOOK is still installed. Until now nothing reinstalled it, so a single slow
// callback could leave every remap dead until PowerToys was restarted.
//
// Inject a tagged key event and check on the next tick whether the hook saw it. The probe
// uses an unassigned virtual key and the hook swallows it, so applications never see it.
//
// The probe is only sent when the system reports user input that the hook did not observe.
// Injecting on an idle machine would refresh the system idle timer and keep the display
// awake, and a hook dropped while nobody is typing does no harm until typing resumes, at
// which point the comparison below notices it.
void KeyboardManager::VerifyHookIsStillInstalled()
{
    if (!hookHandle)
    {
        hookProbePending = false;
        missedHookProbes = 0;
        return;
    }

    if (hookProbePending)
    {
        ++missedHookProbes;
        if (missedHookProbes >= KeyboardManagerConstants::HookWatchdogMissesBeforeReinstall)
        {
            Logger::warn(L"Low level keyboard hook stopped receiving events. Reinstalling it.");
            Trace::Error(0, L"Low level keyboard hook was dropped", L"KeyboardManager::VerifyHookIsStillInstalled");

            // Both calls reset the watchdog state through StopHookWatchdog.
            StopLowlevelKeyboardHook();
            StartLowlevelKeyboardHook();
            return;
        }
    }

    LASTINPUTINFO lastInput{};
    lastInput.cbSize = sizeof(lastInput);
    if (!GetLastInputInfo(&lastInput))
    {
        return;
    }

    // Wrap safe comparison. A positive result means the system registered input after the
    // last event the hook saw, which is the only situation worth probing.
    if (static_cast<int>(lastInput.dwTime - lastHookEventTick) <= 0)
    {
        hookProbePending = false;
        missedHookProbes = 0;
        return;
    }

    INPUT probe{};
    probe.type = INPUT_KEYBOARD;
    probe.ki.wVk = static_cast<WORD>(KeyboardManagerConstants::DUMMY_KEY);
    probe.ki.dwFlags = KEYEVENTF_KEYUP;
    probe.ki.dwExtraInfo = KeyboardManagerConstants::KEYBOARDMANAGER_HOOK_PROBE_FLAG;

    hookProbePending = true;
    if (!inputHandler.SendVirtualInput({ probe }))
    {
        // The probe never entered the input stream, so its absence says nothing about the
        // hook. Do not count it as a miss.
        hookProbePending = false;
    }
}

void CALLBACK KeyboardManager::HookWatchdogTimerProc(HWND, UINT, UINT_PTR, DWORD)
{
    if (keyboardManagerObjectPtr != nullptr)
    {
        keyboardManagerObjectPtr->VerifyHookIsStillInstalled();
    }
}

void KeyboardManager::StartHookWatchdog()
{
    if (!hookWatchdogTimerId)
    {
        hookWatchdogTimerId = SetTimer(nullptr, 0, KeyboardManagerConstants::HookWatchdogIntervalMs, HookWatchdogTimerProc);
        if (!hookWatchdogTimerId)
        {
            Logger::error(L"Failed to start the keyboard hook watchdog. {}", get_last_error_or_default(GetLastError()));
        }
    }
}

void KeyboardManager::StopHookWatchdog()
{
    if (hookWatchdogTimerId)
    {
        KillTimer(nullptr, hookWatchdogTimerId);
        hookWatchdogTimerId = 0;
    }

    hookProbePending = false;
    missedHookProbes = 0;
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
            return;
        }
    }

    StartHookWatchdog();
}

void KeyboardManager::StopLowlevelKeyboardHook()
{
    StopHookWatchdog();

    if (hookHandle)
    {
        UnhookWindowsHookEx(hookHandle);
        hookHandle = nullptr;
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
    return !(state.appSpecificShortcutReMap.empty() && state.appSpecificShortcutReMapSortedKeys.empty() && state.osLevelShortcutReMap.empty() && state.osLevelShortcutReMapSortedKeys.empty() && state.singleKeyReMap.empty() && state.singleKeyToTextReMap.empty());
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
