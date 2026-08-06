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
    constexpr wchar_t editorInstanceMutexName[] = L"Local\\PowerToys_KBMEditor_InstanceMutex";

    class TextReplacementFocusChangedEventHandler final : public IUIAutomationFocusChangedEventHandler
    {
    public:
        explicit TextReplacementFocusChangedEventHandler(State& state) : state(state)
        {
        }

        HRESULT STDMETHODCALLTYPE QueryInterface(REFIID interfaceId, void** object) override
        {
            if (!object)
            {
                return E_POINTER;
            }

            if (interfaceId == __uuidof(IUnknown) || interfaceId == __uuidof(IUIAutomationFocusChangedEventHandler))
            {
                *object = static_cast<IUIAutomationFocusChangedEventHandler*>(this);
                AddRef();
                return S_OK;
            }

            *object = nullptr;
            return E_NOINTERFACE;
        }

        ULONG STDMETHODCALLTYPE AddRef() override
        {
            return ++referenceCount;
        }

        ULONG STDMETHODCALLTYPE Release() override
        {
            const ULONG remainingReferences = --referenceCount;
            if (remainingReferences == 0)
            {
                delete this;
            }
            return remainingReferences;
        }

        HRESULT STDMETHODCALLTYPE HandleFocusChangedEvent(IUIAutomationElement*) override
        {
            state.InvalidateTextReplacementContext();
            return S_OK;
        }

    private:
        std::atomic_ulong referenceCount = 1;
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

    bool IsSafeNativeEdit(const HWND window)
    {
        if (!IsKnownNativeEditClass(window) || !IsWindowEnabled(window) || !IsWindowVisible(window))
        {
            return false;
        }

        const LONG_PTR style = GetWindowLongPtrW(window, GWL_STYLE);
        if ((style & ES_READONLY) != 0 || (style & ES_PASSWORD) != 0)
        {
            return false;
        }

        DWORD_PTR passwordCharacter = 0;
        if (!SendMessageTimeoutW(window, EM_GETPASSWORDCHAR, 0, 0, SMTO_ABORTIFHUNG | SMTO_BLOCK, 50, &passwordCharacter))
        {
            return false;
        }

        return passwordCharacter == 0;
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

    bool IsWritableDocument(IUIAutomationElement* element)
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

    bool IsSafeAutomationEdit(IUIAutomation* automation, const HWND expectedWindow, const DWORD expectedProcessId)
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
        bool password = true;
        int processId = 0;
        int controlType = 0;
        if (!TryGetBoolProperty(element.get(), UIA_HasKeyboardFocusPropertyId, hasKeyboardFocus) || !hasKeyboardFocus ||
            !TryGetBoolProperty(element.get(), UIA_IsKeyboardFocusablePropertyId, keyboardFocusable) || !keyboardFocusable ||
            !TryGetBoolProperty(element.get(), UIA_IsEnabledPropertyId, enabled) || !enabled ||
            !TryGetBoolProperty(element.get(), UIA_IsPasswordPropertyId, password) || password ||
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
               IsWritableDocument(element.get());
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

        // Hook and context-tracker handles are owned by the main message thread.
        if (!PostThreadMessageW(mainThreadId, RefreshHooksMessageID, 0, 0))
        {
            Logger::error(L"Failed to post the Keyboard Manager hook refresh message. {}", get_last_error_or_default(GetLastError()));
        }
    };

    editorIsRunningEvent = CreateEvent(nullptr, true, false, KeyboardManagerConstants::EditorWindowEventName.c_str());
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

void KeyboardManager::LoadSettings()
{
    state.RequestTextReplacementRuntimeReset();
    bool loadedSuccessful = state.LoadSettings();
    if (!loadedSuccessful)
    {
        std::this_thread::sleep_for(std::chrono::milliseconds(500));

        // retry once
        state.LoadSettings();
    }
    if (!state.PublishTextReplacementRuntimeConfiguration())
    {
        Logger::error(L"Failed to publish the Keyboard Manager text replacement runtime configuration. The previous configuration will remain active.");
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

        const intptr_t hookResult = keyboardManagerObjectPtr->HandleKeyboardHookEvent(&event);
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

    // Atomically participate in the same owning-mutex protocol as both editors.
    // Creating a new marker proves that no editor can own it at this instant and
    // avoids clearing an event that a concurrently starting editor just signaled.
    const HANDLE instanceMutex = CreateMutexW(nullptr, TRUE, editorInstanceMutexName);
    if (!instanceMutex)
    {
        // Access-denied and other indeterminate cases fail closed: another integrity
        // level may own the editor marker and must not receive remapped input.
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
        ReleaseMutex(instanceMutex);
        CloseHandle(instanceMutex);
        ResetEvent(editorIsRunningEvent);
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

    return CallNextHookEx(mouseHookHandleCopy, nCode, wParam, lParam);
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
    state.textReplacementContextEditable.store(false, std::memory_order_release);
    state.textReplacementContextStatus.store(TextReplacementContextStatus::Pending, std::memory_order_release);
    state.textReplacementContextInfrastructureReady.store(false, std::memory_order_release);
    state.textReplacementClassifiedContextEpoch.store(0, std::memory_order_release);

    constexpr DWORD winEventFlags = WINEVENT_OUTOFCONTEXT | WINEVENT_SKIPOWNPROCESS;
    textReplacementForegroundHook = SetWinEventHook(EVENT_SYSTEM_FOREGROUND, EVENT_SYSTEM_FOREGROUND, nullptr, TextReplacementWinEventProc, 0, 0, winEventFlags);
    textReplacementFocusHook = SetWinEventHook(EVENT_OBJECT_FOCUS, EVENT_OBJECT_FOCUS, nullptr, TextReplacementWinEventProc, 0, 0, winEventFlags);
    textReplacementDesktopHook = SetWinEventHook(EVENT_SYSTEM_DESKTOPSWITCH, EVENT_SYSTEM_DESKTOPSWITCH, nullptr, TextReplacementWinEventProc, 0, 0, winEventFlags);

    if (!textReplacementForegroundHook || !textReplacementFocusHook || !textReplacementDesktopHook)
    {
        Logger::error(L"Failed to install all text replacement context WinEvent hooks. Text replacement is blocked for safety.");
        state.textReplacementContextStatus.store(TextReplacementContextStatus::Blocked, std::memory_order_release);
        state.textReplacementClassifiedContextEpoch.store(state.textReplacementContextEpoch.load(std::memory_order_acquire), std::memory_order_release);
        return;
    }

    textReplacementContextThread = std::thread([this] { TextReplacementContextThreadProc(); });
    state.InvalidateTextReplacementContext();
}

void KeyboardManager::StopTextReplacementContextTracking() noexcept
{
    state.textReplacementContextTrackingEnabled.store(false, std::memory_order_release);
    state.textReplacementContextEditable.store(false, std::memory_order_release);
    state.textReplacementContextStatus.store(TextReplacementContextStatus::Blocked, std::memory_order_release);
    state.textReplacementContextInfrastructureReady.store(false, std::memory_order_release);
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

    textReplacementContextThreadId.store(0, std::memory_order_release);
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

    winrt::com_ptr<TextReplacementFocusChangedEventHandler> focusChangedHandler;
    focusChangedHandler.attach(new (std::nothrow) TextReplacementFocusChangedEventHandler(state));
    const bool focusHandlerRegistered = focusChangedHandler && SUCCEEDED(automationResult) &&
                                        SUCCEEDED(automation->AddFocusChangedEventHandler(nullptr, focusChangedHandler.get()));
    if (!focusHandlerRegistered)
    {
        Logger::error(L"Failed to register the UI Automation focus handler. Text replacement is blocked for safety.");
        state.textReplacementContextStatus.store(TextReplacementContextStatus::Blocked, std::memory_order_release);
        state.textReplacementClassifiedContextEpoch.store(state.textReplacementContextEpoch.load(std::memory_order_acquire), std::memory_order_release);
    }
    else
    {
        state.textReplacementContextInfrastructureReady.store(true, std::memory_order_release);
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
        bool editable = IsSafeNativeEdit(focusedWindow);
        if (!editable && SUCCEEDED(automationResult))
        {
            editable = IsSafeAutomationEdit(automation.get(), focusedWindow, processId);
        }

        if (requestedEpoch == state.textReplacementContextEpoch.load(std::memory_order_acquire))
        {
            state.textReplacementContextWindow.store(focusedWindow, std::memory_order_release);
            state.textReplacementContextProcessId.store(processId, std::memory_order_release);
            state.textReplacementContextEditable.store(editable, std::memory_order_release);
            state.textReplacementContextStatus.store(editable ? TextReplacementContextStatus::Editable : TextReplacementContextStatus::Blocked, std::memory_order_release);
            // Publish this last. The hook authorizes the snapshot only when it matches
            // the latest invalidation epoch, preventing a stale query from reviving it.
            state.textReplacementClassifiedContextEpoch.store(requestedEpoch, std::memory_order_release);
        }
    }

    state.textReplacementContextInfrastructureReady.store(false, std::memory_order_release);
    if (focusHandlerRegistered)
    {
        automation->RemoveFocusChangedEventHandler(focusChangedHandler.get());
    }
    focusChangedHandler = nullptr;
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
    }

    const bool hasTextReplacements = state.HasTextReplacements();
    if (hookHandle && hasTextReplacements && !mouseHookHandle)
    {
        mouseHookHandle = SetWindowsHookEx(WH_MOUSE_LL, MouseHookProc, GetModuleHandle(nullptr), 0);
        mouseHookHandleCopy = mouseHookHandle;
        if (!mouseHookHandle)
        {
            Logger::error(L"Failed to install the Keyboard Manager text replacement mouse hook. {}", get_last_error_or_default(GetLastError()));
        }
    }
    else if (!hasTextReplacements && mouseHookHandle)
    {
        UnhookWindowsHookEx(mouseHookHandle);
        mouseHookHandle = nullptr;
        mouseHookHandleCopy = nullptr;
    }

    if (hookHandle && hasTextReplacements)
    {
        StartTextReplacementContextTracking();
    }
    else
    {
        StopTextReplacementContextTracking();
    }

    KeyboardEventHandlers::InitializeTextReplacementToggleKeyState(state);
}

void KeyboardManager::StopLowlevelKeyboardHook()
{
    StopTextReplacementContextTracking();
    state.textReplacementToggleStateInitialized = false;

    if (mouseHookHandle)
    {
        UnhookWindowsHookEx(mouseHookHandle);
        mouseHookHandle = nullptr;
        mouseHookHandleCopy = nullptr;
    }

    if (hookHandle)
    {
        UnhookWindowsHookEx(hookHandle);
        hookHandle = nullptr;
        hookHandleCopy = nullptr;
    }
}

void KeyboardManager::RefreshLowlevelHooks()
{
    if (HasRegisteredRemappingsUnchecked())
    {
        StartLowlevelKeyboardHook();
    }
    else
    {
        StopLowlevelKeyboardHook();
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
    return !(state.appSpecificShortcutReMap.empty() && state.appSpecificShortcutReMapSortedKeys.empty() && state.osLevelShortcutReMap.empty() && state.osLevelShortcutReMapSortedKeys.empty() && state.singleKeyReMap.empty() && state.singleKeyToTextReMap.empty() && !state.HasTextReplacements());
}

intptr_t KeyboardManager::HandleKeyboardHookEvent(LowlevelKeyboardEvent* data) noexcept
{
    if (loadingSettings)
    {
        return 0;
    }

    // Suspend remapping if remap key/shortcut window is opened
    if (IsEditorRunning())
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

    intptr_t TextReplacementResult = KeyboardEventHandlers::HandleTextReplacementEvent(inputHandler, data, state);

    if (TextReplacementResult == 1)
    {
        return 1;
    }

    // Handle an os-level shortcut remapping
    const intptr_t osLevelShortcutRemapResult = KeyboardEventHandlers::HandleOSLevelShortcutRemapEvent(inputHandler, data, state);
    if (osLevelShortcutRemapResult == 1)
    {
        KeyboardEventHandlers::ResetTextReplacementRuntimeState(state);
    }
    return osLevelShortcutRemapResult;
}
