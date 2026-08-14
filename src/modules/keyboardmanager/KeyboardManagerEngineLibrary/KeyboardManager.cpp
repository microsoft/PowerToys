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
#include <UIAutomation.h>
#include <wrl/implements.h>

#include "KeyboardEventHandlers.h"
#include "trace.h"

HHOOK KeyboardManager::hookHandleCopy;
HHOOK KeyboardManager::hookHandle;
HHOOK KeyboardManager::mouseHookHandle;
KeyboardManager* KeyboardManager::keyboardManagerObjectPtr;

namespace
{
    DWORD mainThreadId = {};
    constexpr wchar_t editorInstanceMutexName[] = L"Local\\PowerToys_KBMEditor_InstanceMutex";

    class TextReplacementFocusChangedEventHandler final : public Microsoft::WRL::RuntimeClass<Microsoft::WRL::RuntimeClassFlags<Microsoft::WRL::ClassicCom>, IUIAutomationFocusChangedEventHandler>
    {
    public:
        explicit TextReplacementFocusChangedEventHandler(State& state) : state(state)
        {
        }

        HRESULT STDMETHODCALLTYPE HandleFocusChangedEvent(IUIAutomationElement*) override
        {
            state.InvalidateTextReplacementContext();
            return S_OK;
        }

    private:
        State& state;
    };

    HWND GetFocusedTextReplacementWindow()
    {
        GUITHREADINFO guiThreadInfo{};
        guiThreadInfo.cbSize = sizeof(guiThreadInfo);
        if (GetGUIThreadInfo(0, &guiThreadInfo))
        {
            if (guiThreadInfo.hwndFocus)
            {
                return guiThreadInfo.hwndFocus;
            }
            if (guiThreadInfo.hwndActive)
            {
                return guiThreadInfo.hwndActive;
            }
        }

        return GetForegroundWindow();
    }

    DWORD GetWindowProcessId(const HWND window)
    {
        DWORD processId = 0;
        if (window)
        {
            GetWindowThreadProcessId(window, &processId);
        }
        return processId;
    }

    bool IsKnownNativeEditClass(const HWND window)
    {
        wchar_t className[128]{};
        if (!window || GetClassNameW(window, className, static_cast<int>(std::size(className))) == 0)
        {
            return false;
        }

        const std::wstring_view name{ className };
        return _wcsicmp(className, L"Edit") == 0 ||
               _wcsnicmp(className, L"RichEdit", 8) == 0 ||
               name.find(L"WindowsForms10.EDIT") != std::wstring_view::npos;
    }

    bool IsWritableNativeEdit(const HWND window)
    {
        if (!IsKnownNativeEditClass(window) || !IsWindowEnabled(window) || !IsWindowVisible(window))
        {
            return false;
        }

        const LONG_PTR style = GetWindowLongPtrW(window, GWL_STYLE);
        return (style & ES_READONLY) == 0;
    }

    bool TryGetBoolProperty(IUIAutomationElement* element, const PROPERTYID propertyId, bool& value)
    {
        VARIANT property{};
        VariantInit(&property);
        const HRESULT result = element->GetCurrentPropertyValueEx(propertyId, TRUE, &property);
        const bool available = SUCCEEDED(result) && property.vt == VT_BOOL;
        if (available)
        {
            value = property.boolVal == VARIANT_TRUE;
        }
        VariantClear(&property);
        return available;
    }

    bool TryGetIntProperty(IUIAutomationElement* element, const PROPERTYID propertyId, int& value)
    {
        VARIANT property{};
        VariantInit(&property);
        const HRESULT result = element->GetCurrentPropertyValueEx(propertyId, TRUE, &property);
        const bool available = SUCCEEDED(result) && property.vt == VT_I4;
        if (available)
        {
            value = property.lVal;
        }
        VariantClear(&property);
        return available;
    }

    bool IsWritableTextRange(IUIAutomationTextRange* range)
    {
        if (!range)
        {
            return false;
        }

        VARIANT readOnly{};
        VariantInit(&readOnly);
        const HRESULT result = range->GetAttributeValue(UIA_IsReadOnlyAttributeId, &readOnly);
        const bool writable = SUCCEEDED(result) && readOnly.vt == VT_BOOL && readOnly.boolVal == VARIANT_FALSE;
        VariantClear(&readOnly);
        return writable;
    }

    bool IsWritableTextControl(IUIAutomationElement* element)
    {
        winrt::com_ptr<IUIAutomationTextPattern2> textPattern2;
        if (SUCCEEDED(element->GetCurrentPatternAs(UIA_TextPattern2Id, __uuidof(IUIAutomationTextPattern2), textPattern2.put_void())) && textPattern2)
        {
            BOOL caretActive = FALSE;
            winrt::com_ptr<IUIAutomationTextRange> caretRange;
            if (SUCCEEDED(textPattern2->GetCaretRange(&caretActive, caretRange.put())) && caretActive && IsWritableTextRange(caretRange.get()))
            {
                return true;
            }
        }

        winrt::com_ptr<IUIAutomationTextPattern> textPattern;
        if (FAILED(element->GetCurrentPatternAs(UIA_TextPatternId, __uuidof(IUIAutomationTextPattern), textPattern.put_void())) || !textPattern)
        {
            return false;
        }

        winrt::com_ptr<IUIAutomationTextRangeArray> selections;
        if (FAILED(textPattern->GetSelection(selections.put())) || !selections)
        {
            return false;
        }

        int selectionCount = 0;
        if (FAILED(selections->get_Length(&selectionCount)) || selectionCount < 1)
        {
            return false;
        }

        winrt::com_ptr<IUIAutomationTextRange> selection;
        return SUCCEEDED(selections->GetElement(0, selection.put())) && IsWritableTextRange(selection.get());
    }

    bool IsWritableAutomationTextControl(IUIAutomation* automation, const HWND expectedWindow, const DWORD expectedProcessId)
    {
        if (!automation || !expectedWindow || !expectedProcessId)
        {
            return false;
        }

        winrt::com_ptr<IUIAutomationElement> element;
        if (FAILED(automation->GetFocusedElement(element.put())) || !element)
        {
            return false;
        }

        bool hasKeyboardFocus = false;
        bool keyboardFocusable = false;
        bool enabled = false;
        int processId = 0;
        int controlType = 0;
        if (!TryGetBoolProperty(element.get(), UIA_HasKeyboardFocusPropertyId, hasKeyboardFocus) || !hasKeyboardFocus ||
            !TryGetBoolProperty(element.get(), UIA_IsKeyboardFocusablePropertyId, keyboardFocusable) || !keyboardFocusable ||
            !TryGetBoolProperty(element.get(), UIA_IsEnabledPropertyId, enabled) || !enabled ||
            !TryGetIntProperty(element.get(), UIA_ProcessIdPropertyId, processId) || static_cast<DWORD>(processId) != expectedProcessId ||
            !TryGetIntProperty(element.get(), UIA_ControlTypePropertyId, controlType))
        {
            return false;
        }

        if (controlType == UIA_EditControlTypeId)
        {
            bool valuePatternAvailable = false;
            if (TryGetBoolProperty(element.get(), UIA_IsValuePatternAvailablePropertyId, valuePatternAvailable) && valuePatternAvailable)
            {
                bool readOnly = true;
                return TryGetBoolProperty(element.get(), UIA_ValueIsReadOnlyPropertyId, readOnly) && !readOnly;
            }

            bool textEditPatternAvailable = false;
            return TryGetBoolProperty(element.get(), UIA_IsTextEditPatternAvailablePropertyId, textEditPatternAvailable) && textEditPatternAvailable;
        }

        return (controlType == UIA_DocumentControlTypeId || controlType == UIA_TextControlTypeId) &&
               IsWritableTextControl(element.get());
    }
}

KeyboardManager::KeyboardManager()
{
    mainThreadId = GetCurrentThreadId();

    // Load the initial settings.
    LoadSettings();

    // Set the static pointer to the newest object of the class
    keyboardManagerObjectPtr = this;

    // These handles remain valid for the full KeyboardManager lifetime so input and
    // accessibility callbacks can signal refresh without racing a CloseHandle call.
    textReplacementContextStopEvent = CreateEventW(nullptr, TRUE, FALSE, nullptr);
    textReplacementContextRefreshEvent = CreateEventW(nullptr, FALSE, FALSE, nullptr);
    if (!textReplacementContextStopEvent || !textReplacementContextRefreshEvent)
    {
        Logger::error(L"Failed to create text replacement context tracker events. {}", get_last_error_or_default(GetLastError()));
    }
    state.textReplacementContextRefreshEvent.store(textReplacementContextRefreshEvent, std::memory_order_release);

    std::filesystem::path modulePath(PTSettingsHelper::get_module_save_folder_location(moduleName));
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
    // PostThreadMessage requires the destination thread to have created its message queue.
    MSG message{};
    PeekMessageW(&message, nullptr, WM_USER, WM_USER, PM_NOREMOVE);
    settingsEventWaiter.start(KeyboardManagerConstants::SettingsEventName, changeSettingsCallback);
}

KeyboardManager::~KeyboardManager()
{
    settingsEventWaiter.stop();
    StopLowlevelKeyboardHook();
    state.textReplacementContextRefreshEvent.store(nullptr, std::memory_order_release);
    if (textReplacementContextRefreshEvent)
    {
        CloseHandle(textReplacementContextRefreshEvent);
        textReplacementContextRefreshEvent = nullptr;
    }
    if (textReplacementContextStopEvent)
    {
        CloseHandle(textReplacementContextStopEvent);
        textReplacementContextStopEvent = nullptr;
    }
    if (editorIsRunningEvent)
    {
        CloseHandle(editorIsRunningEvent);
    }
    keyboardManagerObjectPtr = nullptr;
}

bool KeyboardManager::LoadSettings()
{
    KeyboardEventHandlers::ResetTextReplacementRuntimeState(state);
    bool loadedSuccessful = state.LoadSettings();
    if (!loadedSuccessful)
    {
        std::this_thread::sleep_for(std::chrono::milliseconds(500));

        // retry once
        loadedSuccessful = state.LoadSettings();
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

    return loadedSuccessful;
}

void KeyboardManager::ReloadSettings()
{
    if (HasActiveRemap() || !state.textReplacementSuppressedTriggerKeys.empty())
    {
        settingsReloadDeferred = true;
        return;
    }

    settingsReloadDeferred = false;
    StopLowlevelKeyboardHook();
    try
    {
        LoadSettings();
    }
    catch (...)
    {
        Logger::error("Failed to load settings");
    }
    state.SetActivatedApp(KeyboardManagerConstants::NoActivatedApp);
    if (HasRegisteredRemappings())
    {
        StartLowlevelKeyboardHook();
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
        KeyboardEventHandlers::UpdateTextReplacementToggleKeyState(&event, hookResult == 1, keyboardManagerObjectPtr->state);
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

bool KeyboardManager::IsEditorRunning()
{
    if (!editorIsRunningEvent || WaitForSingleObject(editorIsRunningEvent, 0) != WAIT_OBJECT_0)
    {
        return false;
    }

    // Participate in the owning-mutex protocol used by both editors. If the event
    // remains signaled after its owner exits, the mutex is available or no longer exists.
    const HANDLE instanceMutex = CreateMutexW(nullptr, TRUE, editorInstanceMutexName);
    if (!instanceMutex)
    {
        return true;
    }

    const bool markerAlreadyExisted = GetLastError() == ERROR_ALREADY_EXISTS;
    if (!markerAlreadyExisted)
    {
        ResetEvent(editorIsRunningEvent);
        ReleaseMutex(instanceMutex);
        CloseHandle(instanceMutex);
        Logger::warn(L"Cleared a stale Keyboard Manager editor event after its owner exited.");
        return false;
    }

    const DWORD waitResult = WaitForSingleObject(instanceMutex, 0);
    if (waitResult == WAIT_TIMEOUT)
    {
        CloseHandle(instanceMutex);
        return true;
    }

    if (waitResult == WAIT_OBJECT_0 || waitResult == WAIT_ABANDONED)
    {
        ResetEvent(editorIsRunningEvent);
        ReleaseMutex(instanceMutex);
        CloseHandle(instanceMutex);
        Logger::warn(L"Cleared a stale Keyboard Manager editor event after its mutex became available.");
        return false;
    }

    CloseHandle(instanceMutex);
    return true;
}

LRESULT CALLBACK KeyboardManager::MouseHookProc(const int nCode, const WPARAM wParam, const LPARAM lParam)
{
    if (nCode == HC_ACTION && keyboardManagerObjectPtr != nullptr)
    {
        switch (wParam)
        {
        case WM_LBUTTONDOWN:
        case WM_RBUTTONDOWN:
        case WM_MBUTTONDOWN:
        case WM_XBUTTONDOWN:
            keyboardManagerObjectPtr->state.InvalidateTextReplacementContext();
            break;
        }
    }

    return CallNextHookEx(nullptr, nCode, wParam, lParam);
}

void CALLBACK KeyboardManager::TextReplacementWinEventProc(HWINEVENTHOOK, DWORD, HWND, LONG, LONG, DWORD, DWORD)
{
    if (keyboardManagerObjectPtr)
    {
        keyboardManagerObjectPtr->state.InvalidateTextReplacementContext();
    }
}

void KeyboardManager::StartTextReplacementContextTracking()
{
    if (state.textReplacementContextTrackingEnabled.exchange(true, std::memory_order_acq_rel))
    {
        return;
    }

    if (!textReplacementContextStopEvent || !textReplacementContextRefreshEvent)
    {
        state.textReplacementContextStatus.store(TextReplacementContextStatus::Blocked, std::memory_order_release);
        state.textReplacementClassifiedContextEpoch.store(state.textReplacementContextEpoch.load(std::memory_order_acquire), std::memory_order_release);
        return;
    }

    ResetEvent(textReplacementContextStopEvent);
    state.textReplacementContextStatus.store(TextReplacementContextStatus::Pending, std::memory_order_release);
    state.textReplacementClassifiedContextEpoch.store(0, std::memory_order_release);

    constexpr DWORD winEventFlags = WINEVENT_OUTOFCONTEXT | WINEVENT_SKIPOWNPROCESS;
    textReplacementForegroundHook = SetWinEventHook(EVENT_SYSTEM_FOREGROUND, EVENT_SYSTEM_FOREGROUND, nullptr, TextReplacementWinEventProc, 0, 0, winEventFlags);
    textReplacementFocusHook = SetWinEventHook(EVENT_OBJECT_FOCUS, EVENT_OBJECT_FOCUS, nullptr, TextReplacementWinEventProc, 0, 0, winEventFlags);
    textReplacementDesktopHook = SetWinEventHook(EVENT_SYSTEM_DESKTOPSWITCH, EVENT_SYSTEM_DESKTOPSWITCH, nullptr, TextReplacementWinEventProc, 0, 0, winEventFlags);

    if (!textReplacementForegroundHook || !textReplacementFocusHook || !textReplacementDesktopHook)
    {
        Logger::warn(L"Failed to install one or more text replacement context WinEvent hooks. Continuing with the available context tracking sources.");
    }

    textReplacementContextThread = std::thread([this] { TextReplacementContextThreadProc(); });
}

void KeyboardManager::StopTextReplacementContextTracking() noexcept
{
    state.textReplacementContextTrackingEnabled.store(false, std::memory_order_release);
    state.textReplacementContextStatus.store(TextReplacementContextStatus::Blocked, std::memory_order_release);
    state.textReplacementClassifiedContextEpoch.store(0, std::memory_order_release);

    if (textReplacementForegroundHook)
    {
        UnhookWinEvent(textReplacementForegroundHook);
        textReplacementForegroundHook = nullptr;
    }
    if (textReplacementFocusHook)
    {
        UnhookWinEvent(textReplacementFocusHook);
        textReplacementFocusHook = nullptr;
    }
    if (textReplacementDesktopHook)
    {
        UnhookWinEvent(textReplacementDesktopHook);
        textReplacementDesktopHook = nullptr;
    }

    if (textReplacementContextStopEvent)
    {
        SetEvent(textReplacementContextStopEvent);
    }

    const DWORD workerThreadId = textReplacementContextThreadId.load(std::memory_order_acquire);
    if (workerThreadId)
    {
        CoCancelCall(workerThreadId, 0);
    }

    if (textReplacementContextThread.joinable())
    {
        textReplacementContextThread.join();
    }

    if (textReplacementContextStopEvent)
    {
        ResetEvent(textReplacementContextStopEvent);
    }
    if (textReplacementContextRefreshEvent)
    {
        ResetEvent(textReplacementContextRefreshEvent);
    }
}

void KeyboardManager::TextReplacementContextThreadProc()
{
    textReplacementContextThreadId.store(GetCurrentThreadId(), std::memory_order_release);
    struct ThreadIdResetGuard
    {
        std::atomic<DWORD>& threadId;

        ~ThreadIdResetGuard()
        {
            threadId.store(0, std::memory_order_release);
        }
    } threadIdResetGuard{ textReplacementContextThreadId };

    const HRESULT apartmentResult = CoInitializeEx(nullptr, COINIT_MULTITHREADED);
    if (FAILED(apartmentResult))
    {
        state.textReplacementContextStatus.store(TextReplacementContextStatus::Blocked, std::memory_order_release);
        state.textReplacementClassifiedContextEpoch.store(state.textReplacementContextEpoch.load(std::memory_order_acquire), std::memory_order_release);
        return;
    }

    CoEnableCallCancellation(nullptr);
    winrt::com_ptr<IUIAutomation> automation;
    HRESULT automationResult = CoCreateInstance(CLSID_CUIAutomation8, nullptr, CLSCTX_INPROC_SERVER, IID_IUIAutomation, automation.put_void());
    if (FAILED(automationResult))
    {
        automationResult = CoCreateInstance(CLSID_CUIAutomation, nullptr, CLSCTX_INPROC_SERVER, IID_IUIAutomation, automation.put_void());
    }

    if (automation)
    {
        if (auto automation6 = automation.try_as<IUIAutomation6>())
        {
            constexpr DWORD providerTimeoutMilliseconds = 250;
            automation6->put_ConnectionTimeout(providerTimeoutMilliseconds);
            automation6->put_TransactionTimeout(providerTimeoutMilliseconds);
        }
    }

    auto focusChangedHandler = Microsoft::WRL::Make<TextReplacementFocusChangedEventHandler>(state);
    const bool focusHandlerRegistered = focusChangedHandler && SUCCEEDED(automationResult) &&
                                        SUCCEEDED(automation->AddFocusChangedEventHandler(nullptr, focusChangedHandler.Get()));
    if (!focusHandlerRegistered)
    {
        Logger::error(L"Failed to register the UI Automation focus handler. Text replacement is blocked for safety.");
        state.textReplacementContextStatus.store(TextReplacementContextStatus::Blocked, std::memory_order_release);
        state.textReplacementClassifiedContextEpoch.store(state.textReplacementContextEpoch.load(std::memory_order_acquire), std::memory_order_release);
    }
    else
    {
        state.InvalidateTextReplacementContext();
    }

    const HANDLE events[] = { textReplacementContextStopEvent, textReplacementContextRefreshEvent };
    while (focusHandlerRegistered && WaitForMultipleObjects(static_cast<DWORD>(std::size(events)), events, FALSE, INFINITE) == WAIT_OBJECT_0 + 1)
    {
        // Focus providers often update after the mouse-down WinEvent. Debounce outside
        // the input hook so a stale editable snapshot can never authorize the next key.
        if (WaitForSingleObject(textReplacementContextStopEvent, 20) == WAIT_OBJECT_0)
        {
            break;
        }

        const uint64_t requestedEpoch = state.textReplacementContextEpoch.load(std::memory_order_acquire);
        const HWND focusedWindow = GetFocusedTextReplacementWindow();
        const DWORD processId = GetWindowProcessId(focusedWindow);
        bool editable = IsWritableNativeEdit(focusedWindow);
        if (!editable && SUCCEEDED(automationResult))
        {
            editable = IsWritableAutomationTextControl(automation.get(), focusedWindow, processId);
        }

        if (requestedEpoch == state.textReplacementContextEpoch.load(std::memory_order_acquire))
        {
            state.textReplacementContextWindow.store(focusedWindow, std::memory_order_release);
            state.textReplacementContextProcessId.store(processId, std::memory_order_release);
            state.textReplacementContextStatus.store(editable ? TextReplacementContextStatus::Editable : TextReplacementContextStatus::Blocked, std::memory_order_release);
            // Publish this last. The hook authorizes the snapshot only when it matches
            // the latest invalidation epoch, preventing a stale query from reviving it.
            state.textReplacementClassifiedContextEpoch.store(requestedEpoch, std::memory_order_release);
        }
    }

    if (focusHandlerRegistered)
    {
        automation->RemoveFocusChangedEventHandler(focusChangedHandler.Get());
    }
    focusChangedHandler.Reset();
    automation = nullptr;
    CoDisableCallCancellation(nullptr);
    CoUninitialize();
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
        else
        {
            KeyboardEventHandlers::InitializeTextReplacementToggleKeyState(state);
        }
    }

    const bool hasTextReplacements = !state.textReplacements.empty();
    if (hookHandle && hasTextReplacements && !mouseHookHandle)
    {
        mouseHookHandle = SetWindowsHookEx(WH_MOUSE_LL, MouseHookProc, GetModuleHandle(nullptr), 0);
        if (!mouseHookHandle)
        {
            Logger::error(L"Failed to install the Keyboard Manager text replacement mouse hook. {}", get_last_error_or_default(GetLastError()));
        }
    }
    else if (!hasTextReplacements && mouseHookHandle)
    {
        UnhookWindowsHookEx(mouseHookHandle);
        mouseHookHandle = nullptr;
    }

    if (hookHandle && hasTextReplacements && mouseHookHandle)
    {
        StartTextReplacementContextTracking();
    }
    else
    {
        StopTextReplacementContextTracking();
    }
}

void KeyboardManager::StopLowlevelKeyboardHook()
{
    if (hookHandle)
    {
        UnhookWindowsHookEx(hookHandle);
        hookHandle = nullptr;
        hookHandleCopy = nullptr;
    }

    if (mouseHookHandle)
    {
        UnhookWindowsHookEx(mouseHookHandle);
        mouseHookHandle = nullptr;
    }

    StopTextReplacementContextTracking();
}

bool KeyboardManager::HasActiveRemap() const
{
    if (!state.singleKeyRemapActiveKeys.empty())
    {
        return true;
    }

    if (std::any_of(state.osLevelShortcutReMap.begin(), state.osLevelShortcutReMap.end(), [](const auto& mapping) { return mapping.second.isShortcutInvoked; }))
    {
        return true;
    }

    return std::any_of(state.appSpecificShortcutReMap.begin(), state.appSpecificShortcutReMap.end(), [](const auto& appMappings) {
        return std::any_of(appMappings.second.begin(), appMappings.second.end(), [](const auto& mapping) { return mapping.second.isShortcutInvoked; });
    });
}

void KeyboardManager::QueueDeferredSettingsReloadIfReady()
{
    if (!settingsReloadDeferred || HasActiveRemap() || !state.textReplacementSuppressedTriggerKeys.empty())
    {
        return;
    }

    if (PostThreadMessageW(mainThreadId, ReloadSettingsMessageID, 0, 0))
    {
        settingsReloadDeferred = false;
    }
    else
    {
        Logger::error(L"Failed to post the deferred Keyboard Manager settings reload message. {}", get_last_error_or_default(GetLastError()));
    }
}

bool KeyboardManager::HasRegisteredRemappings() const
{
    return !(state.appSpecificShortcutReMap.empty() && state.appSpecificShortcutReMapSortedKeys.empty() && state.osLevelShortcutReMap.empty() && state.osLevelShortcutReMapSortedKeys.empty() && state.singleKeyReMap.empty() && state.singleKeyToTextReMap.empty() && state.textReplacements.empty());
}

intptr_t KeyboardManager::HandleKeyboardHookEvent(LowlevelKeyboardEvent* data) noexcept
{
    // Once a trigger-key key-down has been swallowed, its repeats and matching
    // key-up must be swallowed before editor suspension or any fresh remap can
    // reinterpret the physical key.
    if (KeyboardEventHandlers::HandleTextReplacementSuppressedKeyEvent(data, state) == 1)
    {
        return 1;
    }

    // Suspend remapping if remap key/shortcut window is opened
    if (IsEditorRunning())
    {
        // Do not start fresh remaps while the editor is open, but continue every
        // remap that already owns output state until its input sequence is complete.
        const intptr_t activeRemapResult = KeyboardEventHandlers::HandleActiveRemapEvent(inputHandler, data, state);
        if (activeRemapResult == 1)
        {
            KeyboardEventHandlers::ResetTextReplacementRuntimeState(state);
        }
        return activeRemapResult;
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
        KeyboardEventHandlers::ResetTextReplacementRuntimeState(state);
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
        KeyboardEventHandlers::ResetTextReplacementRuntimeState(state);
        return 1;
    }

    intptr_t SingleKeyToTextRemapResult = KeyboardEventHandlers::HandleSingleKeyToTextRemapEvent(inputHandler, data, state);

    if (SingleKeyToTextRemapResult == 1)
    {
        KeyboardEventHandlers::ResetTextReplacementRuntimeState(state);
        return 1;
    }

    // Handle an OS-level shortcut before typed text replacement so a configured
    // shortcut always wins when both mappings match the same physical input.
    const intptr_t osLevelShortcutRemapResult = KeyboardEventHandlers::HandleOSLevelShortcutRemapEvent(inputHandler, data, state);
    if (osLevelShortcutRemapResult == 1)
    {
        KeyboardEventHandlers::ResetTextReplacementRuntimeState(state);
        return 1;
    }

    intptr_t TextReplacementResult = KeyboardEventHandlers::HandleTextReplacementEvent(inputHandler, data, state);

    if (TextReplacementResult == 1)
    {
        return 1;
    }

    return 0;
}
