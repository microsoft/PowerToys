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
#include <limits>
#include <UIAutomation.h>

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

        SetLastError(ERROR_SUCCESS);
        const LONG_PTR style = GetWindowLongPtrW(window, GWL_STYLE);
        if (style == 0 && GetLastError() != ERROR_SUCCESS)
        {
            return false;
        }
        return (style & (ES_READONLY | ES_PASSWORD)) == 0;
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

    bool IsKnownTerminalWindow(HWND window);

    bool IsWritableAutomationTextControl(IUIAutomation* automation, const HWND expectedWindow, const DWORD expectedProcessId)
    {
        if (!automation || !expectedWindow || !expectedProcessId || IsKnownTerminalWindow(expectedWindow))
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
        bool isPassword = true;
        bool textEditPatternAvailable = false;
        int processId = 0;
        int controlType = 0;
        if (!TryGetBoolProperty(element.get(), UIA_HasKeyboardFocusPropertyId, hasKeyboardFocus) || !hasKeyboardFocus ||
            !TryGetBoolProperty(element.get(), UIA_IsKeyboardFocusablePropertyId, keyboardFocusable) || !keyboardFocusable ||
            !TryGetBoolProperty(element.get(), UIA_IsEnabledPropertyId, enabled) || !enabled ||
            !TryGetBoolProperty(element.get(), UIA_IsPasswordPropertyId, isPassword) || isPassword ||
            !TryGetBoolProperty(element.get(), UIA_IsTextEditPatternAvailablePropertyId, textEditPatternAvailable) || !textEditPatternAvailable ||
            !TryGetIntProperty(element.get(), UIA_ProcessIdPropertyId, processId) || static_cast<DWORD>(processId) != expectedProcessId ||
            !TryGetIntProperty(element.get(), UIA_ControlTypePropertyId, controlType))
        {
            return false;
        }

        return (controlType == UIA_EditControlTypeId || controlType == UIA_DocumentControlTypeId || controlType == UIA_TextControlTypeId) &&
               IsWritableTextControl(element.get());
    }

    bool IsTextReplacementMultilineTarget(const HWND window)
    {
        if (!IsKnownNativeEditClass(window))
        {
            return false;
        }

        SetLastError(ERROR_SUCCESS);
        const LONG_PTR style = GetWindowLongPtrW(window, GWL_STYLE);
        if (style == 0 && GetLastError() != ERROR_SUCCESS)
        {
            return false;
        }
        return (style & ES_MULTILINE) != 0 && (style & ES_WANTRETURN) != 0;
    }

    bool HasTerminalWindowClass(const HWND window)
    {
        wchar_t className[128]{};
        if (!window || GetClassNameW(window, className, static_cast<int>(std::size(className))) == 0)
        {
            return false;
        }

        return _wcsicmp(className, L"ConsoleWindowClass") == 0 ||
               _wcsicmp(className, L"CASCADIA_HOSTING_WINDOW_CLASS") == 0;
    }

    bool IsKnownTerminalWindow(const HWND window)
    {
        return HasTerminalWindowClass(window) || HasTerminalWindowClass(GetAncestor(window, GA_ROOT));
    }

    bool IsCollapsedTextRange(IUIAutomationTextRange* range)
    {
        if (!range)
        {
            return false;
        }

        int comparison = 0;
        return SUCCEEDED(range->CompareEndpoints(
                   TextPatternRangeEndpoint_Start,
                   range,
                   TextPatternRangeEndpoint_End,
                   &comparison)) &&
               comparison == 0;
    }

    bool AreTextRangesEqual(IUIAutomationTextRange* left, IUIAutomationTextRange* right);

    bool TryGetCollapsedCaretRange(IUIAutomationElement* element, winrt::com_ptr<IUIAutomationTextRange>& caretRange)
    {
        winrt::com_ptr<IUIAutomationTextPattern> textPattern;
        if (FAILED(element->GetCurrentPatternAs(UIA_TextPatternId, __uuidof(IUIAutomationTextPattern), textPattern.put_void())) || !textPattern)
        {
            return false;
        }

        SupportedTextSelection supportedSelection = SupportedTextSelection_None;
        if (FAILED(textPattern->get_SupportedTextSelection(&supportedSelection)) || supportedSelection == SupportedTextSelection_None)
        {
            return false;
        }

        winrt::com_ptr<IUIAutomationTextRangeArray> selections;
        if (FAILED(textPattern->GetSelection(selections.put())) || !selections)
        {
            return false;
        }

        int selectionCount = 0;
        if (FAILED(selections->get_Length(&selectionCount)) || selectionCount != 1 ||
            FAILED(selections->GetElement(0, caretRange.put())) || !IsCollapsedTextRange(caretRange.get()))
        {
            return false;
        }

        winrt::com_ptr<IUIAutomationTextPattern2> textPattern2;
        if (SUCCEEDED(element->GetCurrentPatternAs(UIA_TextPattern2Id, __uuidof(IUIAutomationTextPattern2), textPattern2.put_void())) && textPattern2)
        {
            BOOL caretActive = FALSE;
            winrt::com_ptr<IUIAutomationTextRange> providerCaretRange;
            if (FAILED(textPattern2->GetCaretRange(&caretActive, providerCaretRange.put())) || !caretActive ||
                !IsCollapsedTextRange(providerCaretRange.get()) || !AreTextRangesEqual(caretRange.get(), providerCaretRange.get()))
            {
                caretRange = nullptr;
                return false;
            }
        }

        return true;
    }

    bool HasNoActiveComposition(IUIAutomationElement* element)
    {
        bool textEditPatternAvailable = false;
        if (!TryGetBoolProperty(element, UIA_IsTextEditPatternAvailablePropertyId, textEditPatternAvailable) || !textEditPatternAvailable)
        {
            // Without TextEditPattern there is no provider-independent way to prove
            // that the caret is not inside an IME composition. Stay fail-closed.
            return false;
        }

        winrt::com_ptr<IUIAutomationTextEditPattern> textEditPattern;
        if (FAILED(element->GetCurrentPatternAs(UIA_TextEditPatternId, __uuidof(IUIAutomationTextEditPattern), textEditPattern.put_void())) || !textEditPattern)
        {
            return false;
        }

        winrt::com_ptr<IUIAutomationTextRange> activeComposition;
        return SUCCEEDED(textEditPattern->GetActiveComposition(activeComposition.put())) && !activeComposition;
    }

    bool TryReadTextRange(IUIAutomationTextRange* range, std::wstring& text)
    {
        BSTR value = nullptr;
        const HRESULT result = range->GetText(-1, &value);
        if (FAILED(result) || !value)
        {
            SysFreeString(value);
            return false;
        }

        text.assign(value, SysStringLen(value));
        SysFreeString(value);
        return true;
    }

    bool TryCreateTriggerSelection(
        IUIAutomation* automation,
        const HWND expectedWindow,
        const DWORD expectedProcessId,
        const std::wstring_view trigger,
        const bool targetHasNewline,
        winrt::com_ptr<IUIAutomationTextRange>& triggerRange,
        winrt::com_ptr<IUIAutomationTextRange>& rollbackCaretRange)
    {
        if (!automation || !expectedWindow || !expectedProcessId || trigger.empty() || IsKnownTerminalWindow(expectedWindow) ||
            GetFocusedTextReplacementWindow() != expectedWindow || GetWindowProcessId(expectedWindow) != expectedProcessId ||
            (targetHasNewline && !IsTextReplacementMultilineTarget(expectedWindow)))
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
        bool isPassword = true;
        int processId = 0;
        int controlType = 0;
        if (!TryGetBoolProperty(element.get(), UIA_HasKeyboardFocusPropertyId, hasKeyboardFocus) || !hasKeyboardFocus ||
            !TryGetBoolProperty(element.get(), UIA_IsKeyboardFocusablePropertyId, keyboardFocusable) || !keyboardFocusable ||
            !TryGetBoolProperty(element.get(), UIA_IsEnabledPropertyId, enabled) || !enabled ||
            !TryGetBoolProperty(element.get(), UIA_IsPasswordPropertyId, isPassword) || isPassword ||
            !TryGetIntProperty(element.get(), UIA_ProcessIdPropertyId, processId) || static_cast<DWORD>(processId) != expectedProcessId ||
            !TryGetIntProperty(element.get(), UIA_ControlTypePropertyId, controlType) ||
            (controlType != UIA_EditControlTypeId && controlType != UIA_DocumentControlTypeId && controlType != UIA_TextControlTypeId) ||
            !HasNoActiveComposition(element.get()))
        {
            return false;
        }

        winrt::com_ptr<IUIAutomationTextRange> caretRange;
        if (!TryGetCollapsedCaretRange(element.get(), caretRange) || !IsWritableTextRange(caretRange.get()) ||
            FAILED(caretRange->Clone(triggerRange.put())) || !triggerRange)
        {
            return false;
        }

        int moved = 0;
        const int requestedUnits = -static_cast<int>((std::min)(trigger.size(), static_cast<size_t>((std::numeric_limits<int>::max)())));
        if (FAILED(triggerRange->MoveEndpointByUnit(TextPatternRangeEndpoint_Start, TextUnit_Character, requestedUnits, &moved)) || moved == 0)
        {
            return false;
        }

        BSTR triggerText = SysAllocStringLen(trigger.data(), static_cast<UINT>(trigger.size()));
        if (!triggerText)
        {
            return false;
        }

        winrt::com_ptr<IUIAutomationTextRange> foundRange;
        const HRESULT findResult = triggerRange->FindText(triggerText, TRUE, FALSE, foundRange.put());
        SysFreeString(triggerText);
        if (FAILED(findResult) || !foundRange)
        {
            return false;
        }

        int endComparison = 0;
        std::wstring actualText;
        if (FAILED(foundRange->CompareEndpoints(
                TextPatternRangeEndpoint_End,
                caretRange.get(),
                TextPatternRangeEndpoint_End,
                &endComparison)) ||
            endComparison != 0 || !TryReadTextRange(foundRange.get(), actualText) || actualText != trigger)
        {
            return false;
        }

        triggerRange = std::move(foundRange);

        if (FAILED(triggerRange->Clone(rollbackCaretRange.put())) || !rollbackCaretRange ||
            FAILED(rollbackCaretRange->MoveEndpointByRange(
                TextPatternRangeEndpoint_Start,
                triggerRange.get(),
                TextPatternRangeEndpoint_End)))
        {
            return false;
        }

        return IsCollapsedTextRange(rollbackCaretRange.get());
    }

    bool AreTextRangesEqual(IUIAutomationTextRange* left, IUIAutomationTextRange* right)
    {
        if (!left || !right)
        {
            return false;
        }

        int startComparison = 0;
        int endComparison = 0;
        return SUCCEEDED(left->CompareEndpoints(
                   TextPatternRangeEndpoint_Start,
                   right,
                   TextPatternRangeEndpoint_Start,
                   &startComparison)) &&
               startComparison == 0 &&
               SUCCEEDED(left->CompareEndpoints(
                   TextPatternRangeEndpoint_End,
                   right,
                   TextPatternRangeEndpoint_End,
                   &endComparison)) &&
               endComparison == 0;
    }

    bool IsCurrentAutomationSelection(
        IUIAutomation* automation,
        const HWND expectedWindow,
        const DWORD expectedProcessId,
        IUIAutomationTextRange* expectedRange,
        const bool expectedCollapsed)
    {
        if (!automation || !expectedRange || GetFocusedTextReplacementWindow() != expectedWindow ||
            GetWindowProcessId(expectedWindow) != expectedProcessId)
        {
            return false;
        }

        winrt::com_ptr<IUIAutomationElement> element;
        winrt::com_ptr<IUIAutomationTextPattern> textPattern;
        winrt::com_ptr<IUIAutomationTextRangeArray> selections;
        if (FAILED(automation->GetFocusedElement(element.put())) || !element ||
            FAILED(element->GetCurrentPatternAs(UIA_TextPatternId, __uuidof(IUIAutomationTextPattern), textPattern.put_void())) || !textPattern ||
            FAILED(textPattern->GetSelection(selections.put())) || !selections)
        {
            return false;
        }

        int processId = 0;
        int selectionCount = 0;
        winrt::com_ptr<IUIAutomationTextRange> currentSelection;
        return TryGetIntProperty(element.get(), UIA_ProcessIdPropertyId, processId) &&
               static_cast<DWORD>(processId) == expectedProcessId &&
               SUCCEEDED(selections->get_Length(&selectionCount)) && selectionCount == 1 &&
               SUCCEEDED(selections->GetElement(0, currentSelection.put())) && currentSelection &&
               IsCollapsedTextRange(currentSelection.get()) == expectedCollapsed &&
               AreTextRangesEqual(currentSelection.get(), expectedRange);
    }

    bool HasCollapsedCurrentAutomationSelection(
        IUIAutomation* automation,
        const HWND expectedWindow,
        const DWORD expectedProcessId)
    {
        if (!automation || GetFocusedTextReplacementWindow() != expectedWindow ||
            GetWindowProcessId(expectedWindow) != expectedProcessId)
        {
            return false;
        }

        winrt::com_ptr<IUIAutomationElement> element;
        int processId = 0;
        winrt::com_ptr<IUIAutomationTextRange> caretRange;
        return SUCCEEDED(automation->GetFocusedElement(element.put())) && element &&
               TryGetIntProperty(element.get(), UIA_ProcessIdPropertyId, processId) &&
               static_cast<DWORD>(processId) == expectedProcessId &&
               TryGetCollapsedCaretRange(element.get(), caretRange);
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
    textReplacementContextRequestEvent = CreateEventW(nullptr, FALSE, FALSE, nullptr);
    textReplacementContextReadyEvent = CreateEventW(nullptr, TRUE, FALSE, nullptr);
    textReplacementContextCommitEvent = CreateEventW(nullptr, FALSE, FALSE, nullptr);
    textReplacementContextCancelEvent = CreateEventW(nullptr, TRUE, FALSE, nullptr);
    textReplacementContextFinishedEvent = CreateEventW(nullptr, TRUE, FALSE, nullptr);
    if (!textReplacementContextStopEvent || !textReplacementContextRefreshEvent ||
        !textReplacementContextRequestEvent || !textReplacementContextReadyEvent ||
        !textReplacementContextCommitEvent || !textReplacementContextCancelEvent ||
        !textReplacementContextFinishedEvent)
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
    if (textReplacementContextRequestEvent)
    {
        CloseHandle(textReplacementContextRequestEvent);
        textReplacementContextRequestEvent = nullptr;
    }
    if (textReplacementContextReadyEvent)
    {
        CloseHandle(textReplacementContextReadyEvent);
        textReplacementContextReadyEvent = nullptr;
    }
    if (textReplacementContextCommitEvent)
    {
        CloseHandle(textReplacementContextCommitEvent);
        textReplacementContextCommitEvent = nullptr;
    }
    if (textReplacementContextCancelEvent)
    {
        CloseHandle(textReplacementContextCancelEvent);
        textReplacementContextCancelEvent = nullptr;
    }
    if (textReplacementContextFinishedEvent)
    {
        CloseHandle(textReplacementContextFinishedEvent);
        textReplacementContextFinishedEvent = nullptr;
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

        const std::wstring textReplacementBufferBeforeEvent = keyboardManagerObjectPtr->state.textReplacementBuffer;
        const intptr_t hookResult = keyboardManagerObjectPtr->HandleKeyboardHookEvent(&event);
        const bool physicalKeyDown = (wParam == WM_KEYDOWN || wParam == WM_SYSKEYDOWN) &&
                                     event.lParam->dwExtraInfo != KeyboardManagerConstants::KEYBOARDMANAGER_SUPPRESS_FLAG &&
                                     event.lParam->dwExtraInfo != KeyboardManagerConstants::KEYBOARDMANAGER_SHORTCUT_FLAG &&
                                     event.lParam->dwExtraInfo != KeyboardManagerConstants::KEYBOARDMANAGER_SINGLEKEY_FLAG;
        if (hookResult == 0 && physicalKeyDown && textReplacementBufferBeforeEvent != keyboardManagerObjectPtr->state.textReplacementBuffer)
        {
            keyboardManagerObjectPtr->textReplacementIgnoredSelectionEventWindow.store(GetFocusedTextReplacementWindow(), std::memory_order_release);
            keyboardManagerObjectPtr->textReplacementIgnoredSelectionEventExpires.store(GetTickCount() + 100, std::memory_order_release);
            keyboardManagerObjectPtr->textReplacementIgnoreNextSelectionEvent.store(true, std::memory_order_release);
        }
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

void CALLBACK KeyboardManager::TextReplacementWinEventProc(HWINEVENTHOOK, const DWORD event, const HWND window, LONG, LONG, DWORD, const DWORD eventTime)
{
    if (keyboardManagerObjectPtr)
    {
        if (event == EVENT_OBJECT_TEXTSELECTIONCHANGED &&
            window == keyboardManagerObjectPtr->textReplacementIgnoredSelectionEventWindow.load(std::memory_order_acquire) &&
            static_cast<LONG>(keyboardManagerObjectPtr->textReplacementIgnoredSelectionEventExpires.load(std::memory_order_acquire) - eventTime) >= 0 &&
            keyboardManagerObjectPtr->textReplacementIgnoreNextSelectionEvent.exchange(false, std::memory_order_acq_rel))
        {
            keyboardManagerObjectPtr->textReplacementIgnoredSelectionEventWindow.store(nullptr, std::memory_order_release);
            keyboardManagerObjectPtr->textReplacementIgnoredSelectionEventExpires.store(0, std::memory_order_release);
            return;
        }
        keyboardManagerObjectPtr->state.InvalidateTextReplacementContext();
    }
}

void KeyboardManager::StartTextReplacementContextTracking()
{
    if (state.textReplacementContextTrackingEnabled.exchange(true, std::memory_order_acq_rel))
    {
        return;
    }

    if (!textReplacementContextStopEvent || !textReplacementContextRefreshEvent ||
        !textReplacementContextRequestEvent || !textReplacementContextReadyEvent ||
        !textReplacementContextCommitEvent || !textReplacementContextCancelEvent ||
        !textReplacementContextFinishedEvent)
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
    textReplacementSelectionHook = SetWinEventHook(EVENT_OBJECT_TEXTSELECTIONCHANGED, EVENT_OBJECT_TEXTSELECTIONCHANGED, nullptr, TextReplacementWinEventProc, 0, 0, winEventFlags);
    textReplacementSelectionTrackingAvailable.store(textReplacementSelectionHook != nullptr, std::memory_order_release);

    if (!textReplacementSelectionHook)
    {
        Logger::error(L"Failed to install the text selection WinEvent hook. Text replacement is blocked for safety.");
        state.textReplacementContextStatus.store(TextReplacementContextStatus::Blocked, std::memory_order_release);
        state.textReplacementClassifiedContextEpoch.store(state.textReplacementContextEpoch.load(std::memory_order_acquire), std::memory_order_release);
        return;
    }

    if (!textReplacementForegroundHook || !textReplacementFocusHook || !textReplacementDesktopHook)
    {
        Logger::warn(L"Failed to install one or more text replacement context WinEvent hooks. Continuing with the available context tracking sources.");
    }

    textReplacementContextThread = std::thread([this] { TextReplacementContextThreadProc(); });
}

void KeyboardManager::StopTextReplacementContextTracking() noexcept
{
    textReplacementSelectionTrackingAvailable.store(false, std::memory_order_release);
    state.textReplacementContextTrackingEnabled.store(false, std::memory_order_release);
    state.textReplacementContextStatus.store(TextReplacementContextStatus::Blocked, std::memory_order_release);
    state.textReplacementClassifiedContextEpoch.store(0, std::memory_order_release);

    {
        std::scoped_lock lock(textReplacementContextRequestMutex);
        if (textReplacementContextRequestInFlight)
        {
            textReplacementContextRequest.canceled = true;
        }
    }
    if (textReplacementContextStopEvent)
    {
        SetEvent(textReplacementContextStopEvent);
    }
    if (textReplacementContextCancelEvent)
    {
        SetEvent(textReplacementContextCancelEvent);
    }

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
    if (textReplacementSelectionHook)
    {
        UnhookWinEvent(textReplacementSelectionHook);
        textReplacementSelectionHook = nullptr;
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
    {
        std::scoped_lock lock(textReplacementContextRequestMutex);
        textReplacementContextRequestInFlight = false;
        textReplacementContextRequestPhase = TextReplacementContextRequestPhase::Idle;
    }

    if (textReplacementContextStopEvent)
    {
        ResetEvent(textReplacementContextStopEvent);
    }
    if (textReplacementContextRefreshEvent)
    {
        ResetEvent(textReplacementContextRefreshEvent);
    }
    if (textReplacementContextRequestEvent)
    {
        ResetEvent(textReplacementContextRequestEvent);
    }
    if (textReplacementContextReadyEvent)
    {
        ResetEvent(textReplacementContextReadyEvent);
    }
    if (textReplacementContextCommitEvent)
    {
        ResetEvent(textReplacementContextCommitEvent);
    }
    if (textReplacementContextCancelEvent)
    {
        ResetEvent(textReplacementContextCancelEvent);
    }
    if (textReplacementContextFinishedEvent)
    {
        ResetEvent(textReplacementContextFinishedEvent);
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

    bool providerTimeoutsConfigured = false;
    if (automation)
    {
        if (auto automation6 = automation.try_as<IUIAutomation6>())
        {
            // Keep every provider RPC well below the low-level hook timeout. A slow
            // provider causes a fail-closed missed replacement, never hook removal.
            constexpr DWORD providerTimeoutMilliseconds = 40;
            providerTimeoutsConfigured = SUCCEEDED(automation6->put_ConnectionTimeout(providerTimeoutMilliseconds)) &&
                                         SUCCEEDED(automation6->put_TransactionTimeout(providerTimeoutMilliseconds));
        }
    }

    const bool automationReady = SUCCEEDED(automationResult) && providerTimeoutsConfigured;
    if (!automationReady)
    {
        Logger::error(L"Failed to configure bounded UI Automation access. Text replacement is blocked for safety.");
        state.textReplacementContextStatus.store(TextReplacementContextStatus::Blocked, std::memory_order_release);
        state.textReplacementClassifiedContextEpoch.store(state.textReplacementContextEpoch.load(std::memory_order_acquire), std::memory_order_release);
    }
    else
    {
        state.InvalidateTextReplacementContext();
    }

    winrt::com_ptr<IUIAutomationTextRange> preparedRollbackCaretRange;
    HWND preparedWindow = nullptr;
    DWORD preparedProcessId = 0;
    uint64_t preparedContextEpoch = 0;
    uint64_t preparedRequestId = 0;
    const auto publishRecoveryGuardLocked = [this](const uint64_t requestId, const bool blockInput, const HWND window, const DWORD processId) {
        if (blockInput)
        {
            textReplacementRecoveryGuardRequestId = requestId;
            textReplacementRecoveryWindow.store(window, std::memory_order_release);
            textReplacementRecoveryProcessId.store(processId, std::memory_order_release);
            textReplacementRecoveryBlocksInput.store(true, std::memory_order_release);
        }
        else if (textReplacementRecoveryGuardRequestId == requestId)
        {
            textReplacementRecoveryGuardRequestId = 0;
            textReplacementRecoveryWindow.store(nullptr, std::memory_order_release);
            textReplacementRecoveryProcessId.store(0, std::memory_order_release);
            textReplacementRecoveryBlocksInput.store(false, std::memory_order_release);
        }
    };

    const HANDLE events[] = { textReplacementContextStopEvent, textReplacementContextRequestEvent, textReplacementContextRefreshEvent };
    while (automationReady)
    {
        const DWORD waitResult = WaitForMultipleObjects(static_cast<DWORD>(std::size(events)), events, FALSE, INFINITE);
        if (waitResult == WAIT_OBJECT_0)
        {
            break;
        }

        if (waitResult == WAIT_OBJECT_0 + 1)
        {
            TextReplacementContextRequest request;
            {
                std::scoped_lock lock(textReplacementContextRequestMutex);
                request = textReplacementContextRequest;
            }

            if (request.kind == TextReplacementContextRequestKind::Rollback)
            {
                const HWND recoveryWindow = preparedWindow;
                const DWORD recoveryProcessId = preparedProcessId;
                const uint64_t restoredSelectionRequestId = preparedRequestId;
                const uint64_t lastPreparedSelectionRequestId = textReplacementLastPreparedSelectionRequestId.load(std::memory_order_acquire);
                bool selectionAlreadyFinished = false;
                {
                    std::scoped_lock lock(textReplacementContextRequestMutex);
                    selectionAlreadyFinished = (preparedRequestId != 0 && textReplacementContextFinishedSelectionId >= preparedRequestId) ||
                                               (lastPreparedSelectionRequestId != 0 && textReplacementLastSuccessfullyRestoredRequestId >= lastPreparedSelectionRequestId);
                }
                const bool restored = selectionAlreadyFinished ||
                                      (preparedRollbackCaretRange && preparedWindow && preparedProcessId &&
                                       GetFocusedTextReplacementWindow() == preparedWindow &&
                                       SUCCEEDED(preparedRollbackCaretRange->Select()) &&
                                       IsCurrentAutomationSelection(
                                           automation.get(),
                                           preparedWindow,
                                           preparedProcessId,
                                           preparedRollbackCaretRange.get(),
                                           true));
                const bool recoveryMustBlock = !restored;
                if (restored)
                {
                    preparedRollbackCaretRange = nullptr;
                    preparedWindow = nullptr;
                    preparedProcessId = 0;
                    preparedContextEpoch = 0;
                    preparedRequestId = 0;
                    textReplacementPreparedSelectionRequestId.store(0, std::memory_order_release);
                }
                {
                    std::scoped_lock lock(textReplacementContextRequestMutex);
                    if (textReplacementContextRequest.id == request.id)
                    {
                        textReplacementContextCompletedRequestId = request.id;
                        textReplacementContextPreparationOutcome = restored ? TextReplacementPreparationOutcome::Prepared : TextReplacementPreparationOutcome::CommittedFailure;
                        if (restored && restoredSelectionRequestId != 0)
                        {
                            textReplacementLastSuccessfullyRestoredRequestId = (std::max)(
                                textReplacementLastSuccessfullyRestoredRequestId,
                                restoredSelectionRequestId);
                        }
                        textReplacementContextRequestInFlight = false;
                        textReplacementContextRequestPhase = TextReplacementContextRequestPhase::Idle;
                        publishRecoveryGuardLocked(request.id, recoveryMustBlock, recoveryWindow, recoveryProcessId);
                    }
                }
                SetEvent(textReplacementContextFinishedEvent);
                continue;
            }

            preparedRollbackCaretRange = nullptr;
            preparedWindow = nullptr;
            preparedProcessId = 0;
            preparedContextEpoch = 0;
            preparedRequestId = 0;
            winrt::com_ptr<IUIAutomationTextRange> triggerRange;
            winrt::com_ptr<IUIAutomationTextRange> rollbackCaretRange;
            const bool candidateReady = request.expectedContextEpoch == state.textReplacementContextEpoch.load(std::memory_order_acquire) &&
                                        TryCreateTriggerSelection(
                                            automation.get(),
                                            request.expectedWindow,
                                            request.expectedProcessId,
                                            request.trigger,
                                            request.targetHasNewline,
                                            triggerRange,
                                            rollbackCaretRange);
            {
                std::scoped_lock lock(textReplacementContextRequestMutex);
                if (textReplacementContextRequest.id == request.id)
                {
                    textReplacementContextCompletedRequestId = request.id;
                    textReplacementContextCandidateReady = candidateReady;
                    textReplacementContextPreparationOutcome = TextReplacementPreparationOutcome::NotPrepared;
                    if (!candidateReady)
                    {
                        textReplacementContextRequestInFlight = false;
                        textReplacementContextRequestPhase = TextReplacementContextRequestPhase::Idle;
                    }
                }
            }
            SetEvent(textReplacementContextReadyEvent);
            if (!candidateReady)
            {
                continue;
            }

            const HANDLE decisionEvents[] = { textReplacementContextStopEvent, textReplacementContextCancelEvent, textReplacementContextCommitEvent };
            const DWORD decision = WaitForMultipleObjects(static_cast<DWORD>(std::size(decisionEvents)), decisionEvents, FALSE, INFINITE);
            if (decision == WAIT_OBJECT_0)
            {
                break;
            }
            if (decision != WAIT_OBJECT_0 + 2)
            {
                std::scoped_lock lock(textReplacementContextRequestMutex);
                if (textReplacementContextRequest.id == request.id)
                {
                    textReplacementContextRequestInFlight = false;
                    textReplacementContextRequestPhase = TextReplacementContextRequestPhase::Idle;
                }
                continue;
            }

            // Re-read the caret immediately before the only mutating selection call. The
            // first pass bounded the hook wait; this pass closes focus/caret races.
            triggerRange = nullptr;
            rollbackCaretRange = nullptr;
            const auto requestWasCanceled = [this, requestId = request.id] {
                std::scoped_lock lock(textReplacementContextRequestMutex);
                return textReplacementContextRequest.id != requestId || textReplacementContextRequest.canceled;
            };
            const bool stillValid = !requestWasCanceled() &&
                                    request.expectedContextEpoch == state.textReplacementContextEpoch.load(std::memory_order_acquire) &&
                                    TryCreateTriggerSelection(
                                        automation.get(),
                                        request.expectedWindow,
                                        request.expectedProcessId,
                                        request.trigger,
                                        request.targetHasNewline,
                                        triggerRange,
                                        rollbackCaretRange);

            TextReplacementPreparationOutcome outcome = TextReplacementPreparationOutcome::NotPrepared;
            bool recoveryMustBlock = false;
            bool selectionAuthorized = false;
            if (stillValid)
            {
                std::scoped_lock lock(textReplacementContextRequestMutex);
                if (textReplacementContextRequest.id == request.id && !textReplacementContextRequest.canceled)
                {
                    textReplacementContextRequestPhase = TextReplacementContextRequestPhase::SelectingOrSelected;
                    selectionAuthorized = true;
                }
            }
            if (selectionAuthorized)
            {
                textReplacementIgnoredSelectionEventWindow.store(request.expectedWindow, std::memory_order_release);
                textReplacementIgnoredSelectionEventExpires.store(GetTickCount() + 100, std::memory_order_release);
                textReplacementIgnoreNextSelectionEvent.store(true, std::memory_order_release);
                std::wstring selectedText;
                const bool selected = SUCCEEDED(triggerRange->Select()) &&
                                      request.expectedContextEpoch == state.textReplacementContextEpoch.load(std::memory_order_acquire) &&
                                      GetFocusedTextReplacementWindow() == request.expectedWindow &&
                                      GetWindowProcessId(request.expectedWindow) == request.expectedProcessId &&
                                      IsCurrentAutomationSelection(
                                          automation.get(),
                                          request.expectedWindow,
                                          request.expectedProcessId,
                                          triggerRange.get(),
                                          false) &&
                                      TryReadTextRange(triggerRange.get(), selectedText) && selectedText == request.trigger &&
                                      request.expectedContextEpoch == state.textReplacementContextEpoch.load(std::memory_order_acquire);
                const bool abandoned = requestWasCanceled();
                if (selected && !abandoned)
                {
                    preparedRollbackCaretRange = std::move(rollbackCaretRange);
                    preparedWindow = request.expectedWindow;
                    preparedProcessId = request.expectedProcessId;
                    preparedContextEpoch = request.expectedContextEpoch;
                    preparedRequestId = request.id;
                    textReplacementPreparedSelectionRequestId.store(request.id, std::memory_order_release);
                    textReplacementLastPreparedSelectionRequestId.store(request.id, std::memory_order_release);
                    outcome = TextReplacementPreparationOutcome::Prepared;
                }
                else
                {
                    // Selection may mutate provider state even when it reports failure.
                    // Restore the original collapsed caret before allowing passthrough.
                    const bool restored = rollbackCaretRange &&
                                          SUCCEEDED(rollbackCaretRange->Select()) &&
                                          IsCurrentAutomationSelection(
                                              automation.get(),
                                              request.expectedWindow,
                                              request.expectedProcessId,
                                              rollbackCaretRange.get(),
                                              true);
                    outcome = restored ? TextReplacementPreparationOutcome::NotPrepared : TextReplacementPreparationOutcome::CommittedFailure;
                    recoveryMustBlock = !restored;
                    if (!restored)
                    {
                        preparedRollbackCaretRange = std::move(rollbackCaretRange);
                        preparedWindow = request.expectedWindow;
                        preparedProcessId = request.expectedProcessId;
                        preparedContextEpoch = request.expectedContextEpoch;
                        preparedRequestId = request.id;
                        textReplacementPreparedSelectionRequestId.store(request.id, std::memory_order_release);
                        textReplacementLastPreparedSelectionRequestId.store(request.id, std::memory_order_release);
                    }
                    textReplacementIgnoreNextSelectionEvent.store(false, std::memory_order_release);
                    textReplacementIgnoredSelectionEventWindow.store(nullptr, std::memory_order_release);
                    textReplacementIgnoredSelectionEventExpires.store(0, std::memory_order_release);
                }
            }
            else if (requestWasCanceled())
            {
                // Cancellation before Select guarantees that this request made no UI
                // mutation, so no recovery guard is required.
                recoveryMustBlock = false;
            }

            bool lateCancellationRequiresRollback = false;
            {
                std::scoped_lock lock(textReplacementContextRequestMutex);
                if (textReplacementContextRequest.id == request.id)
                {
                    lateCancellationRequiresRollback = outcome == TextReplacementPreparationOutcome::Prepared && textReplacementContextRequest.canceled;
                    if (!lateCancellationRequiresRollback)
                    {
                        textReplacementContextCompletedRequestId = request.id;
                        textReplacementContextPreparationOutcome = outcome;
                        textReplacementContextRequestInFlight = false;
                        textReplacementContextRequestPhase = TextReplacementContextRequestPhase::Idle;
                        publishRecoveryGuardLocked(request.id, recoveryMustBlock, request.expectedWindow, request.expectedProcessId);
                    }
                }
            }
            if (lateCancellationRequiresRollback)
            {
                const uint64_t restoredSelectionRequestId = preparedRequestId;
                const bool restored = preparedRollbackCaretRange &&
                                      SUCCEEDED(preparedRollbackCaretRange->Select()) &&
                                      IsCurrentAutomationSelection(
                                          automation.get(),
                                          preparedWindow,
                                          preparedProcessId,
                                          preparedRollbackCaretRange.get(),
                                          true);
                recoveryMustBlock = !restored;
                if (restored)
                {
                    preparedRollbackCaretRange = nullptr;
                    preparedWindow = nullptr;
                    preparedProcessId = 0;
                    preparedContextEpoch = 0;
                    preparedRequestId = 0;
                    textReplacementPreparedSelectionRequestId.store(0, std::memory_order_release);
                }
                outcome = restored ? TextReplacementPreparationOutcome::NotPrepared : TextReplacementPreparationOutcome::CommittedFailure;
                textReplacementIgnoreNextSelectionEvent.store(false, std::memory_order_release);
                textReplacementIgnoredSelectionEventWindow.store(nullptr, std::memory_order_release);
                textReplacementIgnoredSelectionEventExpires.store(0, std::memory_order_release);
                std::scoped_lock lock(textReplacementContextRequestMutex);
                if (textReplacementContextRequest.id == request.id)
                {
                    textReplacementContextCompletedRequestId = request.id;
                    textReplacementContextPreparationOutcome = outcome;
                    if (restored && restoredSelectionRequestId != 0)
                    {
                        textReplacementLastSuccessfullyRestoredRequestId = (std::max)(
                            textReplacementLastSuccessfullyRestoredRequestId,
                            restoredSelectionRequestId);
                    }
                    textReplacementContextRequestInFlight = false;
                    textReplacementContextRequestPhase = TextReplacementContextRequestPhase::Idle;
                    publishRecoveryGuardLocked(request.id, recoveryMustBlock, request.expectedWindow, request.expectedProcessId);
                }
            }
            SetEvent(textReplacementContextFinishedEvent);
            continue;
        }

        if (waitResult == WAIT_OBJECT_0 + 2)
        {
            // Focus providers often update after the mouse-down WinEvent. Debounce outside
            // the input hook so a stale editable snapshot can never authorize the next key.
            if (WaitForSingleObject(textReplacementContextStopEvent, 20) == WAIT_OBJECT_0)
            {
                break;
            }

            bool preparedSelectionAbandoned = false;
            {
                std::scoped_lock lock(textReplacementContextRequestMutex);
                preparedSelectionAbandoned = preparedRequestId != 0 && textReplacementContextFinishedSelectionId >= preparedRequestId;
            }
            if (preparedSelectionAbandoned)
            {
                preparedRollbackCaretRange = nullptr;
                preparedWindow = nullptr;
                preparedProcessId = 0;
                preparedContextEpoch = 0;
                preparedRequestId = 0;
            }
            else if (preparedRollbackCaretRange && GetFocusedTextReplacementWindow() == preparedWindow)
            {
                const bool caretAlreadyReset = HasCollapsedCurrentAutomationSelection(automation.get(), preparedWindow, preparedProcessId);
                const bool restored = caretAlreadyReset ||
                                      (SUCCEEDED(preparedRollbackCaretRange->Select()) &&
                                       IsCurrentAutomationSelection(
                                           automation.get(),
                                           preparedWindow,
                                           preparedProcessId,
                                           preparedRollbackCaretRange.get(),
                                           true));
                if (restored)
                {
                    const uint64_t restoredSelectionRequestId = preparedRequestId;
                    preparedRollbackCaretRange = nullptr;
                    preparedWindow = nullptr;
                    preparedProcessId = 0;
                    preparedContextEpoch = 0;
                    preparedRequestId = 0;
                    textReplacementPreparedSelectionRequestId.store(0, std::memory_order_release);
                    std::scoped_lock lock(textReplacementContextRequestMutex);
                    if (restoredSelectionRequestId != 0)
                    {
                        textReplacementLastSuccessfullyRestoredRequestId = (std::max)(
                            textReplacementLastSuccessfullyRestoredRequestId,
                            restoredSelectionRequestId);
                    }
                    textReplacementRecoveryGuardRequestId = 0;
                    textReplacementRecoveryWindow.store(nullptr, std::memory_order_release);
                    textReplacementRecoveryProcessId.store(0, std::memory_order_release);
                    textReplacementRecoveryBlocksInput.store(false, std::memory_order_release);
                }
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
    }

    bool selectionAlreadyFinished = false;
    {
        std::scoped_lock lock(textReplacementContextRequestMutex);
        selectionAlreadyFinished = preparedRequestId != 0 && textReplacementContextFinishedSelectionId >= preparedRequestId;
    }
    bool shutdownRecoveryComplete = selectionAlreadyFinished || !preparedRollbackCaretRange;
    if (!shutdownRecoveryComplete)
    {
        // This is the only provider call allowed after stop is requested. The mandatory
        // IUIAutomation6 transaction timeout bounds it before the keyboard hook is removed.
        shutdownRecoveryComplete = SUCCEEDED(preparedRollbackCaretRange->Select()) &&
                                   IsCurrentAutomationSelection(
                                       automation.get(),
                                       preparedWindow,
                                       preparedProcessId,
                                       preparedRollbackCaretRange.get(),
                                       true);
    }
    if (!shutdownRecoveryComplete)
    {
        Logger::error(L"Unable to restore a prepared text replacement selection before stopping context tracking.");
    }
    else
    {
        std::scoped_lock lock(textReplacementContextRequestMutex);
        textReplacementRecoveryGuardRequestId = 0;
        textReplacementRecoveryWindow.store(nullptr, std::memory_order_release);
        textReplacementRecoveryProcessId.store(0, std::memory_order_release);
        textReplacementRecoveryBlocksInput.store(false, std::memory_order_release);
    }
    textReplacementPreparedSelectionRequestId.store(0, std::memory_order_release);
    textReplacementIgnoreNextSelectionEvent.store(false, std::memory_order_release);
    textReplacementIgnoredSelectionEventWindow.store(nullptr, std::memory_order_release);
    textReplacementIgnoredSelectionEventExpires.store(0, std::memory_order_release);
    preparedRollbackCaretRange = nullptr;

    automation = nullptr;
    CoDisableCallCancellation(nullptr);
    CoUninitialize();
}

KeyboardManager::TextReplacementPreparationOutcome KeyboardManager::PrepareTextReplacement(
    const std::wstring_view trigger,
    const bool targetHasNewline) noexcept
{
    constexpr ULONGLONG maximumHookTransactionMilliseconds = 125;
    textReplacementTransactionDeadline = GetTickCount64() + maximumHookTransactionMilliseconds;
    const auto remainingTransactionTime = [this]() -> DWORD {
        const ULONGLONG now = GetTickCount64();
        if (now >= textReplacementTransactionDeadline)
        {
            return 0;
        }
        return static_cast<DWORD>(textReplacementTransactionDeadline - now);
    };

    if (trigger.empty() || HasActiveRemap() || !state.textReplacementSuppressedTriggerKeys.empty() ||
        !textReplacementSelectionTrackingAvailable.load(std::memory_order_acquire) ||
        !state.textReplacementContextTrackingEnabled.load(std::memory_order_acquire) ||
        !textReplacementContextThread.joinable())
    {
        return TextReplacementPreparationOutcome::NotPrepared;
    }

    const uint64_t contextEpoch = state.textReplacementContextEpoch.load(std::memory_order_acquire);
    const HWND focusedWindow = GetFocusedTextReplacementWindow();
    const DWORD processId = GetWindowProcessId(focusedWindow);
    if (!focusedWindow || !processId ||
        state.textReplacementClassifiedContextEpoch.load(std::memory_order_acquire) != contextEpoch ||
        state.textReplacementContextStatus.load(std::memory_order_acquire) != TextReplacementContextStatus::Editable ||
        state.textReplacementContextWindow.load(std::memory_order_acquire) != focusedWindow ||
        state.textReplacementContextProcessId.load(std::memory_order_acquire) != processId)
    {
        return TextReplacementPreparationOutcome::NotPrepared;
    }

    uint64_t requestId = 0;
    {
        std::scoped_lock lock(textReplacementContextRequestMutex);
        if (textReplacementContextRequestInFlight)
        {
            return TextReplacementPreparationOutcome::NotPrepared;
        }

        requestId = ++textReplacementContextNextRequestId;
        textReplacementContextRequest = {
            .id = requestId,
            .kind = TextReplacementContextRequestKind::Prepare,
            .trigger = std::wstring{ trigger },
            .expectedWindow = focusedWindow,
            .expectedProcessId = processId,
            .expectedContextEpoch = contextEpoch,
            .targetHasNewline = targetHasNewline,
            .canceled = false,
        };
        textReplacementContextCompletedRequestId = 0;
        textReplacementContextCandidateReady = false;
        textReplacementContextPreparationOutcome = TextReplacementPreparationOutcome::NotPrepared;
        textReplacementContextRequestInFlight = true;
        textReplacementContextRequestPhase = TextReplacementContextRequestPhase::Querying;
    }

    ResetEvent(textReplacementContextReadyEvent);
    ResetEvent(textReplacementContextCommitEvent);
    ResetEvent(textReplacementContextCancelEvent);
    ResetEvent(textReplacementContextFinishedEvent);
    if (!SetEvent(textReplacementContextRequestEvent))
    {
        std::scoped_lock lock(textReplacementContextRequestMutex);
        textReplacementContextRequestInFlight = false;
        textReplacementContextRequestPhase = TextReplacementContextRequestPhase::Idle;
        return TextReplacementPreparationOutcome::NotPrepared;
    }

    const HANDLE readyEvents[] = { textReplacementContextReadyEvent, textReplacementContextStopEvent };
    const DWORD readyResult = WaitForMultipleObjects(static_cast<DWORD>(std::size(readyEvents)), readyEvents, FALSE, remainingTransactionTime());
    if (readyResult != WAIT_OBJECT_0)
    {
        {
            std::scoped_lock lock(textReplacementContextRequestMutex);
            if (textReplacementContextRequest.id == requestId && textReplacementContextRequestInFlight)
            {
                textReplacementContextRequest.canceled = true;
            }
        }
        SetEvent(textReplacementContextCancelEvent);
        return TextReplacementPreparationOutcome::NotPrepared;
    }

    {
        std::scoped_lock lock(textReplacementContextRequestMutex);
        if (textReplacementContextCompletedRequestId != requestId || !textReplacementContextCandidateReady)
        {
            return TextReplacementPreparationOutcome::NotPrepared;
        }
    }

    // The query phase is cancellable and has not changed the target. Once committed,
    // wait for an explicit Select result so a late provider call cannot mutate UI after
    // the physical activation key has been passed through.
    if (!SetEvent(textReplacementContextCommitEvent))
    {
        SetEvent(textReplacementContextCancelEvent);
        return TextReplacementPreparationOutcome::NotPrepared;
    }

    const HANDLE finishedEvents[] = { textReplacementContextFinishedEvent, textReplacementContextStopEvent };
    if (WaitForMultipleObjects(static_cast<DWORD>(std::size(finishedEvents)), finishedEvents, FALSE, remainingTransactionTime()) != WAIT_OBJECT_0)
    {
        {
            std::scoped_lock lock(textReplacementContextRequestMutex);
            if (textReplacementContextRequest.id == requestId && textReplacementContextRequestInFlight)
            {
                // The worker checks this immediately before and after Select. If the
                // provider call finishes late, it restores the old collapsed caret.
                textReplacementContextRequest.canceled = true;
                if (textReplacementContextRequestPhase == TextReplacementContextRequestPhase::SelectingOrSelected)
                {
                    textReplacementRecoveryGuardRequestId = requestId;
                    textReplacementRecoveryWindow.store(focusedWindow, std::memory_order_release);
                    textReplacementRecoveryProcessId.store(processId, std::memory_order_release);
                    textReplacementRecoveryBlocksInput.store(true, std::memory_order_release);
                }
            }
        }
        SetEvent(textReplacementContextCancelEvent);
        return TextReplacementPreparationOutcome::CommittedFailure;
    }

    std::scoped_lock lock(textReplacementContextRequestMutex);
    return textReplacementContextCompletedRequestId == requestId ? textReplacementContextPreparationOutcome : TextReplacementPreparationOutcome::CommittedFailure;
}

bool KeyboardManager::RollbackPreparedTextReplacement() noexcept
{
    const auto remainingTransactionTime = [this]() -> DWORD {
        const ULONGLONG now = GetTickCount64();
        if (now >= textReplacementTransactionDeadline)
        {
            return 0;
        }
        return static_cast<DWORD>(textReplacementTransactionDeadline - now);
    };

    const uint64_t selectionRequestId = textReplacementLastPreparedSelectionRequestId.load(std::memory_order_acquire);
    uint64_t requestId = 0;
    {
        std::scoped_lock lock(textReplacementContextRequestMutex);
        if (selectionRequestId != 0 && textReplacementLastSuccessfullyRestoredRequestId >= selectionRequestId)
        {
            return true;
        }
        if (textReplacementContextRequestInFlight || !textReplacementContextThread.joinable())
        {
            return false;
        }

        HWND recoveryWindow = textReplacementRecoveryWindow.load(std::memory_order_acquire);
        DWORD recoveryProcessId = textReplacementRecoveryProcessId.load(std::memory_order_acquire);
        if (!recoveryWindow && textReplacementContextRequest.kind == TextReplacementContextRequestKind::Prepare)
        {
            recoveryWindow = textReplacementContextRequest.expectedWindow;
            recoveryProcessId = textReplacementContextRequest.expectedProcessId;
        }

        requestId = ++textReplacementContextNextRequestId;
        textReplacementContextRequest = {
            .id = requestId,
            .kind = TextReplacementContextRequestKind::Rollback,
        };
        textReplacementContextCompletedRequestId = 0;
        textReplacementContextPreparationOutcome = TextReplacementPreparationOutcome::CommittedFailure;
        textReplacementContextRequestInFlight = true;
        textReplacementContextRequestPhase = TextReplacementContextRequestPhase::SelectingOrSelected;
        textReplacementRecoveryGuardRequestId = requestId;
        textReplacementRecoveryWindow.store(recoveryWindow, std::memory_order_release);
        textReplacementRecoveryProcessId.store(recoveryProcessId, std::memory_order_release);
        textReplacementRecoveryBlocksInput.store(true, std::memory_order_release);
    }

    ResetEvent(textReplacementContextFinishedEvent);
    if (!SetEvent(textReplacementContextRequestEvent))
    {
        std::scoped_lock lock(textReplacementContextRequestMutex);
        textReplacementContextRequestInFlight = false;
        textReplacementContextRequestPhase = TextReplacementContextRequestPhase::Idle;
        return false;
    }

    const HANDLE finishedEvents[] = { textReplacementContextFinishedEvent, textReplacementContextStopEvent };
    if (WaitForMultipleObjects(static_cast<DWORD>(std::size(finishedEvents)), finishedEvents, FALSE, remainingTransactionTime()) != WAIT_OBJECT_0)
    {
        return false;
    }

    std::scoped_lock lock(textReplacementContextRequestMutex);
    return textReplacementContextCompletedRequestId == requestId &&
           textReplacementContextPreparationOutcome == TextReplacementPreparationOutcome::Prepared;
}

void KeyboardManager::FinishPreparedTextReplacement() noexcept
{
    {
        std::scoped_lock lock(textReplacementContextRequestMutex);
        textReplacementContextFinishedSelectionId = (std::max)(
            textReplacementContextFinishedSelectionId,
            textReplacementContextCompletedRequestId);
        textReplacementRecoveryGuardRequestId = 0;
        textReplacementRecoveryWindow.store(nullptr, std::memory_order_release);
        textReplacementRecoveryProcessId.store(0, std::memory_order_release);
        textReplacementRecoveryBlocksInput.store(false, std::memory_order_release);
    }
    textReplacementPreparedSelectionRequestId.store(0, std::memory_order_release);
    textReplacementIgnoreNextSelectionEvent.store(false, std::memory_order_release);
    textReplacementIgnoredSelectionEventWindow.store(nullptr, std::memory_order_release);
    textReplacementIgnoredSelectionEventExpires.store(0, std::memory_order_release);
    // Releasing the worker-owned COM range is not part of the input transaction and
    // must never extend the low-level hook callback. The worker replaces or clears it
    // on the next context request and on shutdown.
}

bool KeyboardManager::IsPreparedTextReplacementCurrent() const noexcept
{
    HWND expectedWindow = nullptr;
    DWORD expectedProcessId = 0;
    uint64_t expectedContextEpoch = 0;
    {
        std::scoped_lock lock(textReplacementContextRequestMutex);
        if (textReplacementContextRequestInFlight ||
            textReplacementContextPreparationOutcome != TextReplacementPreparationOutcome::Prepared ||
            textReplacementContextCompletedRequestId == 0 ||
            textReplacementContextCompletedRequestId != textReplacementContextRequest.id ||
            textReplacementPreparedSelectionRequestId.load(std::memory_order_acquire) != textReplacementContextCompletedRequestId ||
            textReplacementContextRequest.kind != TextReplacementContextRequestKind::Prepare)
        {
            return false;
        }

        expectedWindow = textReplacementContextRequest.expectedWindow;
        expectedProcessId = textReplacementContextRequest.expectedProcessId;
        expectedContextEpoch = textReplacementContextRequest.expectedContextEpoch;
    }

    return textReplacementSelectionTrackingAvailable.load(std::memory_order_acquire) &&
           !textReplacementRecoveryBlocksInput.load(std::memory_order_acquire) &&
           state.textReplacementContextEpoch.load(std::memory_order_acquire) == expectedContextEpoch &&
           GetFocusedTextReplacementWindow() == expectedWindow &&
           GetWindowProcessId(expectedWindow) == expectedProcessId;
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
    // Give any exact cleanup suffix left by a partial SendInput one final chance while
    // the hook is still installed and can suppress the generated events.
    if (state.HasPendingInputCleanup())
    {
        KeyboardEventHandlers::RetryPendingInputCleanup(inputHandler, state);
    }

    // Cancel, restore any prepared selection, and join the bounded provider worker
    // while the keyboard hook is still present to protect generated cleanup input.
    StopTextReplacementContextTracking();

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

}

bool KeyboardManager::HasActiveRemap() const
{
    if (!state.singleKeyRemapActiveKeys.empty() || state.HasPendingInputCleanup())
    {
        return true;
    }

    const HWND focusedWindow = GetFocusedTextReplacementWindow();
    const DWORD focusedProcessId = GetWindowProcessId(focusedWindow);
    if (textReplacementRecoveryBlocksInput.load(std::memory_order_acquire))
    {
        const HWND recoveryWindow = textReplacementRecoveryWindow.load(std::memory_order_acquire);
        if (recoveryWindow && IsWindow(recoveryWindow) && focusedWindow == recoveryWindow &&
            focusedProcessId == textReplacementRecoveryProcessId.load(std::memory_order_acquire))
        {
            return true;
        }
    }
    {
        std::scoped_lock lock(textReplacementContextRequestMutex);
        const bool selectionTransactionActive = textReplacementContextRequestInFlight ||
                                                textReplacementPreparedSelectionRequestId.load(std::memory_order_acquire) != 0;
        if (selectionTransactionActive)
        {
            return true;
        }
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
    // Internal cleanup events must never reach the editor gate or the foreground app.
    if (data->lParam->dwExtraInfo == KeyboardManagerConstants::KEYBOARDMANAGER_SUPPRESS_FLAG)
    {
        return 1;
    }

    const bool generatedByKeyboardManager =
        data->lParam->dwExtraInfo == KeyboardManagerConstants::KEYBOARDMANAGER_SHORTCUT_FLAG ||
        data->lParam->dwExtraInfo == KeyboardManagerConstants::KEYBOARDMANAGER_SINGLEKEY_FLAG;
    if (!generatedByKeyboardManager && state.HasPendingInputCleanup())
    {
        // Cleanup owns only its generated suffix. The physical event still proceeds
        // through the normal pipeline so its down/up pairing is never disrupted.
        KeyboardEventHandlers::RetryPendingInputCleanup(inputHandler, state);
    }

    // Once a trigger-key key-down has been swallowed, its repeats and matching
    // key-up must be swallowed before editor suspension or any fresh remap can
    // reinterpret the physical key.
    if (KeyboardEventHandlers::HandleTextReplacementSuppressedKeyEvent(data, state) == 1)
    {
        return 1;
    }

    const bool isKeyDown = data->wParam == WM_KEYDOWN || data->wParam == WM_SYSKEYDOWN;
    // Preparation is allowed only with zero KBM output owners. During recovery every
    // key-down is blocked so it cannot overwrite the protected selection; key-ups pass
    // through so an existing physical press outside KBM is never stranded.
    if (textReplacementRecoveryBlocksInput.load(std::memory_order_acquire) && isKeyDown)
    {
        bool cancelInFlightSelection = false;
        const HWND recoveryWindow = textReplacementRecoveryWindow.load(std::memory_order_acquire);
        const DWORD recoveryProcessId = textReplacementRecoveryProcessId.load(std::memory_order_acquire);
        const HWND focusedWindow = GetFocusedTextReplacementWindow();
        if (recoveryWindow && IsWindow(recoveryWindow) && focusedWindow == recoveryWindow &&
            GetWindowProcessId(focusedWindow) == recoveryProcessId)
        {
            return 1;
        }

        // The protected selection is no longer the active input context. Do not turn
        // recovery into a global keyboard lock; abandon the old token and let the worker
        // release its COM range without changing the newly focused control.
        {
            std::scoped_lock lock(textReplacementContextRequestMutex);
            const uint64_t selectionRequestId = textReplacementLastPreparedSelectionRequestId.load(std::memory_order_acquire);
            textReplacementContextFinishedSelectionId = (std::max)(textReplacementContextFinishedSelectionId, selectionRequestId);
            textReplacementRecoveryGuardRequestId = 0;
            textReplacementRecoveryWindow.store(nullptr, std::memory_order_release);
            textReplacementRecoveryProcessId.store(0, std::memory_order_release);
            textReplacementRecoveryBlocksInput.store(false, std::memory_order_release);
            textReplacementPreparedSelectionRequestId.store(0, std::memory_order_release);
            if (textReplacementContextRequestInFlight)
            {
                textReplacementContextRequest.canceled = true;
                cancelInFlightSelection = true;
            }
        }
        textReplacementIgnoreNextSelectionEvent.store(false, std::memory_order_release);
        textReplacementIgnoredSelectionEventWindow.store(nullptr, std::memory_order_release);
        textReplacementIgnoredSelectionEventExpires.store(0, std::memory_order_release);
        if (textReplacementContextRefreshEvent)
        {
            SetEvent(textReplacementContextRefreshEvent);
        }
        if (cancelInFlightSelection && textReplacementContextCancelEvent)
        {
            SetEvent(textReplacementContextCancelEvent);
        }
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

    const KeyboardEventHandlers::TextReplacementTransactionCallbacks textReplacementTransaction{
        .prepare = [this](const std::wstring_view trigger, const bool targetContainsNewline) {
            switch (PrepareTextReplacement(trigger, targetContainsNewline))
            {
            case TextReplacementPreparationOutcome::Prepared:
                return KeyboardEventHandlers::TextReplacementPreparationResult::Prepared;
            case TextReplacementPreparationOutcome::CommittedFailure:
                return KeyboardEventHandlers::TextReplacementPreparationResult::CommittedFailure;
            default:
                return KeyboardEventHandlers::TextReplacementPreparationResult::NotPrepared;
            }
        },
        .rollback = [this] { return RollbackPreparedTextReplacement(); },
        .isCurrent = [this] { return IsPreparedTextReplacementCurrent(); },
        .finish = [this] { FinishPreparedTextReplacement(); },
    };
    intptr_t TextReplacementResult = KeyboardEventHandlers::HandleTextReplacementEvent(inputHandler, data, state, textReplacementTransaction);

    if (TextReplacementResult == 1)
    {
        return 1;
    }

    return 0;
}
