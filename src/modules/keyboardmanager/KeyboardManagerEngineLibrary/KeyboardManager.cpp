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
#include <algorithm>
#include <ctime>
#include <deque>
#include <mutex>
#include <optional>

#include "KeyboardEventHandlers.h"
#include "BufferTextExpansionBackend.h"
#include "trace.h"

HHOOK KeyboardManager::hookHandleCopy;
HHOOK KeyboardManager::hookHandle;
HHOOK KeyboardManager::mouseHookHandle;
HHOOK KeyboardManager::mouseHookHandleCopy;
KeyboardManager* KeyboardManager::keyboardManagerObjectPtr;

namespace
{
    DWORD mainThreadId = {};
    std::atomic_uint64_t nextTextExpansionInstanceId = 0;
    std::mutex textExpansionMessageMutex;
    struct TextExpansionMessageToken
    {
        uint64_t instanceId = 0;
        uint64_t generation = 0;
    };
    std::deque<TextExpansionMessageToken> textExpansionMessageTokens;

    bool QueueTextExpansionMessage(
        const uint64_t instanceId,
        const uint64_t generation,
        DWORD& errorCode)
    {
        std::scoped_lock lock(textExpansionMessageMutex);
        textExpansionMessageTokens.push_back({ instanceId, generation });
        if (PostThreadMessageW(mainThreadId, KeyboardManager::TextExpansionCommitMessageID, generation, 0))
        {
            return true;
        }

        errorCode = GetLastError();
        textExpansionMessageTokens.pop_back();
        return false;
    }

    std::optional<TextExpansionMessageToken> PopTextExpansionMessageToken()
    {
        std::scoped_lock lock(textExpansionMessageMutex);
        if (textExpansionMessageTokens.empty())
        {
            return std::nullopt;
        }

        const auto token = textExpansionMessageTokens.front();
        textExpansionMessageTokens.pop_front();
        return token;
    }

    void AbandonTextExpansionMessages(const uint64_t instanceId)
    {
        std::scoped_lock lock(textExpansionMessageMutex);
        for (auto& token : textExpansionMessageTokens)
        {
            if (token.instanceId == instanceId)
            {
                // Keep a tombstone for each already-posted message. Its callback must
                // consume exactly one queue entry rather than accidentally taking a
                // transaction belonging to a later KeyboardManager instance.
                token = {};
            }
        }
    }

    bool HasEnabledTextExpansion(const TextExpansionTable& rules)
    {
        return std::any_of(rules.begin(), rules.end(), [](const TextExpansionRule& rule) {
            return rule.enabled;
        });
    }

}

KeyboardManager::KeyboardManager()
{
    mainThreadId = GetCurrentThreadId();
    textExpansionInstanceId = nextTextExpansionInstanceId.fetch_add(1, std::memory_order_acq_rel) + 1;

    // Load the initial settings.
    LoadSettings();

    // Set the static pointer to the newest object of the class
    keyboardManagerObjectPtr = this;

    textExpansionController = std::make_unique<TextExpansionController>(
        std::make_unique<BufferTextExpansionBackend>(inputHandler),
        [this](const uint64_t generation) {
            DWORD errorCode = ERROR_SUCCESS;
            if (QueueTextExpansionMessage(textExpansionInstanceId, generation, errorCode))
            {
                return true;
            }

            Logger::error(
                L"Failed to post the Keyboard Manager Text Expansion commit message. {}",
                get_last_error_or_default(errorCode));
            return false;
        });
    auto changeSettingsCallback = [](DWORD err) {
        Logger::trace(L"{} event was signaled", KeyboardManagerConstants::SettingsEventName);
        if (err != ERROR_SUCCESS)
        {
            Logger::error(L"Failed to watch settings changes. {}", get_last_error_or_default(err));
        }

        if (!PostThreadMessageW(mainThreadId, ReloadSettingsMessageID, 0, 0))
        {
            Logger::error(L"Failed to post the Keyboard Manager settings reload message. {}", get_last_error_or_default(GetLastError()));
        }
    };

    editorIsRunningEvent = CreateEvent(nullptr, true, false, KeyboardManagerConstants::EditorWindowEventName.c_str());
    // PostThreadMessage requires the destination thread to have a message queue.
    MSG message{};
    PeekMessageW(&message, nullptr, WM_USER, WM_USER, PM_NOREMOVE);
    settingsEventWaiter.start(KeyboardManagerConstants::SettingsEventName, changeSettingsCallback);
}

KeyboardManager::~KeyboardManager()
{
    Shutdown();
    if (editorIsRunningEvent)
    {
        CloseHandle(editorIsRunningEvent);
        editorIsRunningEvent = nullptr;
    }
    keyboardManagerObjectPtr = nullptr;
}

void KeyboardManager::Shutdown() noexcept
{
    if (shutdownStarted.exchange(true, std::memory_order_acq_rel))
    {
        return;
    }

    settingsEventWaiter.stop();
    if (deferredReloadTimer)
    {
        KillTimer(nullptr, deferredReloadTimer);
        deferredReloadTimer = 0;
    }
    // Stop the Buffer Text Expansion backend before detaching the keyboard hook.
    if (textExpansionController)
    {
        textExpansionController->Stop();
    }
    AbandonTextExpansionMessages(textExpansionInstanceId);
    StopLowlevelKeyboardHook();
}

void KeyboardManager::LoadSettings()
{
    auto loadResult = state.LoadSettingsWithResult();
    if (loadResult == MappingConfigurationLoadResult::Failure)
    {
        std::this_thread::sleep_for(std::chrono::milliseconds(500));

        // Retry only file-level/transient failures. A Partial result is deterministic
        // invalid data whose valid entries have already been applied.
        loadResult = state.LoadSettingsWithResult();
    }
    if (loadResult == MappingConfigurationLoadResult::Partial)
    {
        Logger::error(L"Keyboard Manager settings contained invalid entries; skipped them and loaded the valid entries.");
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

void KeyboardManager::ReloadSettings()
{
    deferredReloadPosted.store(false, std::memory_order_release);
    if (HasPendingInputWork())
    {
        settingsReloadDeferred.store(true, std::memory_order_release);
        ArmDeferredReloadTimer();
        return;
    }

    if (deferredReloadTimer)
    {
        KillTimer(nullptr, deferredReloadTimer);
        deferredReloadTimer = 0;
    }

    settingsReloadDeferred.store(false, std::memory_order_release);
    loadingSettings.store(true, std::memory_order_release);
    StopLowlevelKeyboardHook();
    if (textExpansionController)
    {
        textExpansionController->Stop();
    }
    try
    {
        LoadSettings();
    }
    catch (...)
    {
        Logger::error("Failed to load settings");
    }
    loadingSettings.store(false, std::memory_order_release);

    if (HasRegisteredRemappingsUnchecked())
    {
        StartLowlevelKeyboardHook();
    }
}

void KeyboardManager::CompletePendingTextExpansion() noexcept
{
    const auto token = PopTextExpansionMessageToken();
    if (token && token->instanceId == textExpansionInstanceId && textExpansionController)
    {
        textExpansionController->CompletePendingActivation(token->generation);
    }
    if (textExpansionController && textExpansionController->HasPendingBackendWork())
    {
        ArmDeferredReloadTimer();
    }
    QueueDeferredSettingsReloadIfReady();
}

void KeyboardManager::ArmDeferredReloadTimer() noexcept
{
    if (!deferredReloadTimer)
    {
        deferredReloadTimer = SetTimer(nullptr, 0, 50, DeferredReloadTimerProc);
    }
}

void CALLBACK KeyboardManager::DeferredReloadTimerProc(HWND, UINT, const UINT_PTR timerId, DWORD)
{
    if (!keyboardManagerObjectPtr || timerId != keyboardManagerObjectPtr->deferredReloadTimer)
    {
        return;
    }
    if (keyboardManagerObjectPtr->textExpansionController)
    {
        keyboardManagerObjectPtr->textExpansionController->RetryPendingBackendWork();
    }
    keyboardManagerObjectPtr->QueueDeferredSettingsReloadIfReady();

    const bool backendStillPending = keyboardManagerObjectPtr->textExpansionController &&
                                     keyboardManagerObjectPtr->textExpansionController->HasPendingBackendWork();
    if (!backendStillPending &&
        !keyboardManagerObjectPtr->settingsReloadDeferred.load(std::memory_order_acquire) &&
        keyboardManagerObjectPtr->deferredReloadTimer)
    {
        KillTimer(nullptr, keyboardManagerObjectPtr->deferredReloadTimer);
        keyboardManagerObjectPtr->deferredReloadTimer = 0;
    }
}

void KeyboardManager::QueueDeferredSettingsReloadIfReady() noexcept
{
    if (!settingsReloadDeferred.load(std::memory_order_acquire) ||
        HasPendingInputWork() ||
        deferredReloadPosted.exchange(true, std::memory_order_acq_rel))
    {
        return;
    }

    if (!PostThreadMessageW(mainThreadId, ReloadSettingsMessageID, 0, 0))
    {
        deferredReloadPosted.store(false, std::memory_order_release);
    }
    else if (deferredReloadTimer)
    {
        KillTimer(nullptr, deferredReloadTimer);
        deferredReloadTimer = 0;
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

        const intptr_t hookResult = keyboardManagerObjectPtr->HandleKeyboardHookEvent(&event);
        keyboardManagerObjectPtr->QueueDeferredSettingsReloadIfReady();
        if (hookResult == 1)
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
    if (nCode == HC_ACTION && keyboardManagerObjectPtr)
    {
        // A button press can move the caret without changing HWND/PID, so invalidate
        // the buffered suffix before handling any pending "Alone" key. Wheel events
        // do not move the caret, but still turn a pending Alone key into a combination.
        switch (wParam)
        {
        case WM_LBUTTONDOWN:
        case WM_RBUTTONDOWN:
        case WM_MBUTTONDOWN:
        case WM_XBUTTONDOWN:
            if (keyboardManagerObjectPtr->textExpansionController)
            {
                keyboardManagerObjectPtr->textExpansionController->ResetBuffer();
            }
            keyboardManagerObjectPtr->HandleMouseHookEvent();
            break;
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

    bool textExpansionReady = false;
    if (hookHandle && HasEnabledTextExpansion(state.textExpansions) && textExpansionController)
    {
        textExpansionReady = textExpansionController->Start(inputHandler);
        if (!textExpansionReady)
        {
            Logger::error(L"Failed to start the Keyboard Manager Buffer Text Expansion backend.");
        }
    }
    else if (textExpansionController)
    {
        textExpansionController->Stop();
    }

    // Text Expansion needs mouse button presses to invalidate its buffered suffix,
    // while "Alone" remaps need button/wheel events to promote a pending key into a
    // combination. Share one companion mouse hook for both features.
    const bool mouseHookRequired = textExpansionReady || !state.aloneSingleKeyReMap.empty();
    if (hookHandle && mouseHookRequired)
    {
        StartLowlevelMouseHook();
    }
    else
    {
        StopLowlevelMouseHook();
    }

    if (textExpansionReady && !mouseHookHandle && textExpansionController)
    {
        // Mouse clicks can move the caret without changing HWND/PID. Without this
        // hook the buffered suffix cannot be used safely, so fail closed.
        textExpansionController->Stop();
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
    const bool hasEnabledTextExpansion = HasEnabledTextExpansion(state.textExpansions);
    return hasEnabledTextExpansion ||
           !(state.appSpecificShortcutReMap.empty() && state.appSpecificShortcutReMapSortedKeys.empty() && state.osLevelShortcutReMap.empty() && state.osLevelShortcutReMapSortedKeys.empty() && state.singleKeyReMap.empty() && state.aloneSingleKeyReMap.empty() && state.singleKeyToTextReMap.empty());
}

bool KeyboardManager::HasPendingInputWork() const noexcept
{
    return activeRemapPresses.any() || state.HasPendingAloneKeys() || state.HasInvokedShortcutRemap() ||
           (textExpansionController && textExpansionController->HasPendingWork());
}

intptr_t KeyboardManager::HandleKeyboardHookEvent(LowlevelKeyboardEvent* data) noexcept
{
    // If key has suppress flag, then suppress it
    if (data->lParam->dwExtraInfo == KeyboardManagerConstants::KEYBOARDMANAGER_SUPPRESS_FLAG)
    {
        return 1;
    }

    const bool keyDown = data->wParam == WM_KEYDOWN || data->wParam == WM_SYSKEYDOWN;
    const bool keyUp = data->wParam == WM_KEYUP || data->wParam == WM_SYSKEYUP;
    const bool injectedByKeyboardManager =
        (data->lParam->dwExtraInfo & CommonSharedConstants::KEYBOARDMANAGER_INJECTED_FLAG) != 0;
    const DWORD physicalKey = data->lParam->vkCode;
    const auto physicalPressIndex = Helpers::GetPhysicalKeyEventIndex(data);
    const bool remapPressWasActive = !injectedByKeyboardManager && physicalPressIndex &&
                                     activeRemapPresses.test(*physicalPressIndex);
    if (remapPressWasActive && keyUp)
    {
        // The matching key-up must still traverse the old remap snapshot. The bit can
        // be cleared now because deferred reload is queued only after this hook returns.
        activeRemapPresses.reset(*physicalPressIndex);
    }

    const auto rememberHandledRemapPress = [&] {
        if (!injectedByKeyboardManager && keyDown && physicalPressIndex)
        {
            activeRemapPresses.set(*physicalPressIndex);
        }
    };

    const auto textExpansionDisposition = textExpansionController ?
                                              textExpansionController->BeginKeyboardEvent(data) :
                                              TextExpansionController::EventDisposition::Ignore;
    if (textExpansionDisposition == TextExpansionController::EventDisposition::ForcePassThrough)
    {
        // Text Expansion recovery owns this release. Let the physical key-up through,
        // but retire any lazy Alone combination state without injecting a second key-up.
        if (keyUp && state.IsAloneCombination(physicalKey))
        {
            state.ClearAloneKeyState(physicalKey);
        }
        else if (keyUp && state.IsAlonePending(physicalKey))
        {
            state.ClearAloneKeyState(physicalKey);
            textExpansionController->ResetBuffer();
        }

        // Arming events still reach the foreground application, so track them unless
        // the buffer is already suspended. A faulted recovery backend makes this a no-op.
        const bool bufferSuspended = settingsReloadDeferred.load(std::memory_order_acquire) ||
                                     loadingSettings.load(std::memory_order_acquire) ||
                                     (editorIsRunningEvent != nullptr &&
                                      WaitForSingleObject(editorIsRunningEvent, 0) == WAIT_OBJECT_0);
        if (bufferSuspended)
        {
            textExpansionController->ResetBuffer();
        }
        else
        {
            textExpansionController->TrackKeyboardEvent(data);
        }
        return 0;
    }
    if (textExpansionDisposition == TextExpansionController::EventDisposition::Suppress)
    {
        // A pending Text Expansion transaction owns captured modifier releases. If an
        // Alone key was promoted to its original modifier, only clear the lazy-remap
        // state here; the backend will emit the one matching synthetic key-up.
        if (keyUp && state.IsAloneCombination(physicalKey))
        {
            state.ClearAloneKeyState(physicalKey);
        }
        else if (keyUp && state.IsAlonePending(physicalKey))
        {
            state.ClearAloneKeyState(physicalKey);
            textExpansionController->ResetBuffer();
        }
        return 1;
    }

    const bool reloadDeferred = settingsReloadDeferred.load(std::memory_order_acquire);
    if (reloadDeferred && textExpansionController && !injectedByKeyboardManager)
    {
        // Keep the old remap snapshot active until every intercepted press gets its
        // matching release, but do not collect text for a configuration being replaced.
        textExpansionController->ResetBuffer();
    }

    if (loadingSettings)
    {
        if (textExpansionController && !injectedByKeyboardManager)
        {
            textExpansionController->ResetBuffer();
        }
        return 0;
    }

    // Suspend remapping if remap key/shortcut window is opened
    const bool editorIsOpen = editorIsRunningEvent != nullptr &&
                              WaitForSingleObject(editorIsRunningEvent, 0) == WAIT_OBJECT_0;
    const bool shortcutRemapWasInvoked = editorIsOpen && state.HasInvokedShortcutRemap();
    const bool drainShortcutOnly = editorIsOpen && shortcutRemapWasInvoked && !remapPressWasActive;
    if (editorIsOpen)
    {
        if (textExpansionController && !injectedByKeyboardManager)
        {
            // Remapping is suspended while the editor is open, but the buffer backend
            // still needs physical toggle-key transitions such as Caps Lock.
            textExpansionController->TrackKeyboardEvent(data);
            textExpansionController->ResetBuffer();
        }
        if (!remapPressWasActive && !shortcutRemapWasInvoked)
        {
            return 0;
        }
    }

    // Remap a key tapped alone (dual-key). This has priority over the regular
    // single-key remap. While the editor is open, only finish a physical remap
    // press that started before the editor gate; unrelated editor input must pass.
    const bool aloneWasPending = keyUp && state.IsAlonePending(physicalKey);
    const intptr_t SingleKeyAloneRemapResult = drainShortcutOnly ?
                                                       0 :
                                                       KeyboardEventHandlers::HandleSingleKeyAloneRemapEvent(inputHandler, data, state);
    if (SingleKeyAloneRemapResult == 1)
    {
        rememberHandledRemapPress();
        if (textExpansionController)
        {
            textExpansionController->NotifyAloneRemapEventHandled(data, aloneWasPending);
        }
        return 1;
    }

    // Remap a key
    intptr_t SingleKeyRemapResult = drainShortcutOnly ?
                                          0 :
                                          KeyboardEventHandlers::HandleSingleKeyRemapEvent(inputHandler, data, state);

    // Single key remaps have priority. If a key is remapped, only the remapped version should be visible to the shortcuts and hence the event should be suppressed here.
    if (SingleKeyRemapResult == 1)
    {
        rememberHandledRemapPress();
        if (textExpansionController)
        {
            textExpansionController->NotifyHigherPriorityEventHandled(data);
        }
        return 1;
    }

    /* This feature has not been enabled (code from proof of concept stage)
        // Remap a key to behave like a modifier instead of a toggle
        intptr_t SingleKeyToggleToModResult = KeyboardEventHandlers::HandleSingleKeyToggleToModEvent(inputHandler, data, keyboardManagerState);
    */

    // Handle an app-specific shortcut remapping
    intptr_t AppSpecificShortcutRemapResult = KeyboardEventHandlers::HandleAppSpecificShortcutRemapEventWithOptions(
        inputHandler,
        data,
        state,
        !editorIsOpen);

    // If an app-specific shortcut is remapped then the os-level shortcut remapping should be suppressed.
    if (AppSpecificShortcutRemapResult == 1)
    {
        rememberHandledRemapPress();
        if (textExpansionController)
        {
            textExpansionController->NotifyHigherPriorityEventHandled(data);
        }
        return 1;
    }

    intptr_t SingleKeyToTextRemapResult = drainShortcutOnly ?
                                                0 :
                                                KeyboardEventHandlers::HandleSingleKeyToTextRemapEvent(inputHandler, data, state);

    if (SingleKeyToTextRemapResult == 1)
    {
        rememberHandledRemapPress();
        if (textExpansionController)
        {
            textExpansionController->NotifyHigherPriorityEventHandled(data);
        }
        return 1;
    }

    // Handle an os-level shortcut remapping. Existing remaps always take precedence
    // over a new Text Expansion activation using the same key or shortcut.
    const intptr_t OSLevelShortcutRemapResult = KeyboardEventHandlers::HandleOSLevelShortcutRemapEventWithOptions(
        inputHandler,
        data,
        state,
        !editorIsOpen);
    if (OSLevelShortcutRemapResult == 1)
    {
        rememberHandledRemapPress();
        if (textExpansionController)
        {
            textExpansionController->NotifyHigherPriorityEventHandled(data);
        }
        return 1;
    }

    if (!reloadDeferred && !editorIsOpen &&
        textExpansionDisposition == TextExpansionController::EventDisposition::FreshActionKeyDown &&
        textExpansionController)
    {
        const intptr_t activationResult = textExpansionController->TryActivate(
            inputHandler,
            data,
            state.textExpansions);
        if (activationResult == 1)
        {
            return 1;
        }
    }

    if (textExpansionController && !reloadDeferred && !editorIsOpen)
    {
        textExpansionController->TrackKeyboardEvent(data);
    }

    return 0;
}
