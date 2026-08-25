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
    if (HasEnabledTextExpansion(state.textExpansions) && !textExpansionController->Start())
    {
        Logger::error(L"Failed to start the Keyboard Manager Buffer Text Expansion backend.");
    }

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
        // invalid data and has already retained the last-known-good snapshot.
        loadResult = state.LoadSettingsWithResult();
    }
    if (loadResult == MappingConfigurationLoadResult::Partial)
    {
        Logger::error(L"Keyboard Manager settings contained invalid entries; retained the last-known-good runtime snapshot.");
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

void KeyboardManager::ReloadSettings()
{
    deferredReloadPosted.store(false, std::memory_order_release);
    if (textExpansionController && textExpansionController->HasPendingWork())
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

    if (textExpansionController)
    {
        if (HasEnabledTextExpansion(state.textExpansions))
        {
            if (!textExpansionController->Start())
            {
                Logger::error(L"Failed to start the Keyboard Manager Buffer Text Expansion backend after settings reload.");
            }
        }
        else
        {
            textExpansionController->Stop();
        }
    }

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
        (textExpansionController && textExpansionController->HasPendingWork()) ||
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
    if (nCode == HC_ACTION && keyboardManagerObjectPtr &&
        (wParam == WM_LBUTTONDOWN || wParam == WM_RBUTTONDOWN ||
         wParam == WM_MBUTTONDOWN || wParam == WM_XBUTTONDOWN))
    {
        if (keyboardManagerObjectPtr->textExpansionController)
        {
            keyboardManagerObjectPtr->textExpansionController->ResetBuffer();
        }
    }

    return CallNextHookEx(mouseHookHandle, nCode, wParam, lParam);
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

    if (hookHandle && !mouseHookHandle && HasEnabledTextExpansion(state.textExpansions))
    {
        mouseHookHandle = SetWindowsHookEx(WH_MOUSE_LL, MouseHookProc, GetModuleHandle(NULL), NULL);
        if (!mouseHookHandle)
        {
            const DWORD errorCode = GetLastError();
            const auto errorMessage = get_last_error_message(errorCode);
            Trace::Error(
                errorCode,
                errorMessage.has_value() ? errorMessage.value() : L"",
                L"StartLowlevelKeyboardHook::SetWindowsHookEx(WH_MOUSE_LL)");
            if (textExpansionController)
            {
                // Mouse clicks can move the caret without changing HWND/PID. Without
                // this hook the buffered suffix cannot be used safely.
                textExpansionController->Stop();
            }
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
           !(state.appSpecificShortcutReMap.empty() && state.appSpecificShortcutReMapSortedKeys.empty() && state.osLevelShortcutReMap.empty() && state.osLevelShortcutReMapSortedKeys.empty() && state.singleKeyReMap.empty() && state.singleKeyToTextReMap.empty());
}

intptr_t KeyboardManager::HandleKeyboardHookEvent(LowlevelKeyboardEvent* data) noexcept
{
    // If key has suppress flag, then suppress it
    if (data->lParam->dwExtraInfo == KeyboardManagerConstants::KEYBOARDMANAGER_SUPPRESS_FLAG)
    {
        return 1;
    }

    const auto textExpansionDisposition = textExpansionController ?
                                              textExpansionController->BeginKeyboardEvent(data) :
                                              TextExpansionController::EventDisposition::Ignore;
    const DWORD textExpansionPhysicalKey = data->lParam->vkCode;
    if (textExpansionDisposition == TextExpansionController::EventDisposition::Suppress)
    {
        return 1;
    }

    if (settingsReloadDeferred.load(std::memory_order_acquire))
    {
        if (textExpansionController)
        {
            textExpansionController->ResetBuffer();
        }
        QueueDeferredSettingsReloadIfReady();
        return 0;
    }

    if (loadingSettings)
    {
        if (textExpansionController)
        {
            textExpansionController->ResetBuffer();
        }
        return 0;
    }

    // Suspend remapping if remap key/shortcut window is opened
    if (editorIsRunningEvent != nullptr && WaitForSingleObject(editorIsRunningEvent, 0) == WAIT_OBJECT_0)
    {
        if (textExpansionController)
        {
            // Remapping is suspended while the editor is open, but the buffer backend
            // still needs physical toggle-key transitions such as Caps Lock.
            textExpansionController->TrackKeyboardEvent(data);
            textExpansionController->ResetBuffer();
        }
        return 0;
    }

    // Remap a key
    intptr_t SingleKeyRemapResult = KeyboardEventHandlers::HandleSingleKeyRemapEvent(inputHandler, data, state);

    // Single key remaps have priority. If a key is remapped, only the remapped version should be visible to the shortcuts and hence the event should be suppressed here.
    if (SingleKeyRemapResult == 1)
    {
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
    intptr_t AppSpecificShortcutRemapResult = KeyboardEventHandlers::HandleAppSpecificShortcutRemapEvent(inputHandler, data, state);

    // If an app-specific shortcut is remapped then the os-level shortcut remapping should be suppressed.
    if (AppSpecificShortcutRemapResult == 1)
    {
        if (textExpansionController)
        {
            textExpansionController->NotifyHigherPriorityEventHandled(data);
        }
        return 1;
    }

    intptr_t SingleKeyToTextRemapResult = KeyboardEventHandlers::HandleSingleKeyToTextRemapEvent(inputHandler, data, state);

    if (SingleKeyToTextRemapResult == 1)
    {
        if (textExpansionController)
        {
            textExpansionController->NotifyHigherPriorityEventHandled(data);
        }
        return 1;
    }

    // Handle an os-level shortcut remapping. Existing remaps always take precedence
    // over a new Text Expansion activation using the same key or shortcut.
    const intptr_t OSLevelShortcutRemapResult = KeyboardEventHandlers::HandleOSLevelShortcutRemapEvent(inputHandler, data, state);
    if (OSLevelShortcutRemapResult == 1)
    {
        if (textExpansionController)
        {
            textExpansionController->NotifyHigherPriorityEventHandled(data);
        }
        return 1;
    }

    if (textExpansionDisposition == TextExpansionController::EventDisposition::FreshActionKeyDown &&
        textExpansionController)
    {
        const intptr_t activationResult = textExpansionController->TryActivate(
            inputHandler,
            textExpansionPhysicalKey,
            state.textExpansions);
        if (activationResult == 1)
        {
            return 1;
        }
    }

    if (textExpansionController)
    {
        textExpansionController->TrackKeyboardEvent(data);
    }

    return 0;
}
