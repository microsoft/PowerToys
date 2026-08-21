#include "pch.h"
#include "UIAutomationTextExpansionContext.h"

#include <UIAutomation.h>

#include <algorithm>
#include <atomic>
#include <condition_variable>
#include <limits>
#include <mutex>
#include <thread>
#include <utility>

namespace
{
    HWND GetFocusedWindow() noexcept
    {
        GUITHREADINFO info{};
        info.cbSize = sizeof(info);
        if (GetGUIThreadInfo(0, &info))
        {
            if (info.hwndFocus)
            {
                return info.hwndFocus;
            }
            if (info.hwndActive)
            {
                return info.hwndActive;
            }
        }
        return GetForegroundWindow();
    }

    DWORD GetWindowProcessId(const HWND window) noexcept
    {
        DWORD processId = 0;
        if (window)
        {
            GetWindowThreadProcessId(window, &processId);
        }
        return processId;
    }

    bool TryGetElementProcessId(IUIAutomationElement* element, DWORD& processId)
    {
        if (!element)
        {
            return false;
        }

        VARIANT property{};
        VariantInit(&property);
        const HRESULT result = element->GetCurrentPropertyValueEx(UIA_ProcessIdPropertyId, TRUE, &property);
        const bool valid = SUCCEEDED(result) && property.vt == VT_I4 && property.lVal > 0;
        if (valid)
        {
            processId = static_cast<DWORD>(property.lVal);
        }
        VariantClear(&property);
        return valid;
    }

    bool IsCollapsed(IUIAutomationTextRange* range)
    {
        int comparison = 0;
        return range &&
               SUCCEEDED(range->CompareEndpoints(
                   TextPatternRangeEndpoint_Start,
                   range,
                   TextPatternRangeEndpoint_End,
                   &comparison)) &&
               comparison == 0;
    }

    bool AreEqual(IUIAutomationTextRange* left, IUIAutomationTextRange* right)
    {
        if (!left || !right)
        {
            return false;
        }

        int start = 0;
        int end = 0;
        return SUCCEEDED(left->CompareEndpoints(
                   TextPatternRangeEndpoint_Start,
                   right,
                   TextPatternRangeEndpoint_Start,
                   &start)) &&
               start == 0 &&
               SUCCEEDED(left->CompareEndpoints(
                   TextPatternRangeEndpoint_End,
                   right,
                   TextPatternRangeEndpoint_End,
                   &end)) &&
               end == 0;
    }

    bool AreSameElement(IUIAutomation* automation, IUIAutomationElement* left, IUIAutomationElement* right)
    {
        BOOL same = FALSE;
        return automation && left && right &&
               SUCCEEDED(automation->CompareElements(left, right, &same)) && same;
    }

    bool TryRead(IUIAutomationTextRange* range, std::wstring& text)
    {
        BSTR value = nullptr;
        const HRESULT result = range ? range->GetText(-1, &value) : E_POINTER;
        if (FAILED(result) || !value)
        {
            SysFreeString(value);
            return false;
        }

        text.assign(value, SysStringLen(value));
        SysFreeString(value);
        return true;
    }

    bool TryGetSingleSelection(
        IUIAutomationElement* element,
        winrt::com_ptr<IUIAutomationTextRange>& selection)
    {
        winrt::com_ptr<IUIAutomationTextPattern> textPattern;
        winrt::com_ptr<IUIAutomationTextRangeArray> selections;
        if (!element ||
            FAILED(element->GetCurrentPatternAs(UIA_TextPatternId, __uuidof(IUIAutomationTextPattern), textPattern.put_void())) ||
            !textPattern || FAILED(textPattern->GetSelection(selections.put())) || !selections)
        {
            return false;
        }

        int count = 0;
        return SUCCEEDED(selections->get_Length(&count)) && count == 1 &&
               SUCCEEDED(selections->GetElement(0, selection.put())) && selection;
    }

    class UIAutomationTextExpansionContext final : public ITextExpansionTextContext
    {
    public:
        ~UIAutomationTextExpansionContext() override
        {
            Stop();
        }

        bool Start() override
        {
            std::unique_lock lock(mutex);
            if (worker.joinable())
            {
                return ready;
            }

            stopping.store(false, std::memory_order_release);
            initialized = false;
            ready = false;
            worker = std::thread([this] { WorkerProc(); });
            initializationChanged.wait_for(lock, std::chrono::milliseconds(500), [this] { return initialized; });
            return initialized && ready;
        }

        void Stop() noexcept override
        {
            {
                std::scoped_lock lock(mutex);
                if (!worker.joinable())
                {
                    return;
                }
                stopping.store(true, std::memory_order_release);
                canceledThrough.store(nextRequestId, std::memory_order_release);
            }
            requestChanged.notify_all();
            responseChanged.notify_all();

            if (const DWORD id = workerThreadId.load(std::memory_order_acquire))
            {
                CoCancelCall(id, 0);
            }

            if (worker.joinable())
            {
                worker.join();
            }

            std::scoped_lock lock(mutex);
            initialized = false;
            ready = false;
            requestPending = false;
            responseReady = false;
            activeRequest.store(false, std::memory_order_release);
            preparedStateActive.store(false, std::memory_order_release);
            selectionMayHaveChanged.store(false, std::memory_order_release);
            rollbackAllowed.store(false, std::memory_order_release);
            targetWindow.store(nullptr, std::memory_order_release);
            targetProcessId.store(0, std::memory_order_release);
        }

        TextExpansionPreparationResult Prepare(
            const std::vector<TextExpansionCandidate>& candidates,
            const std::chrono::steady_clock::time_point deadline) override
        {
            TextExpansionPreparationResult result;
            if (!Invoke(Command::Prepare, candidates, {}, deadline, result, nullptr))
            {
                result.status = selectionMayHaveChanged.load(std::memory_order_acquire) ?
                                    TextExpansionPreparationStatus::FailedChangedOrUnknown :
                                    TextExpansionPreparationStatus::FailedUnchanged;
            }
            return result;
        }

        bool VerifyPreparedSelection(const std::chrono::steady_clock::time_point deadline) override
        {
            bool result = false;
            TextExpansionPreparationResult unused;
            return Invoke(Command::Verify, {}, {}, deadline, unused, &result) && result;
        }

        bool IsTargetContextCurrent(const std::chrono::steady_clock::time_point deadline) override
        {
            bool result = false;
            TextExpansionPreparationResult unused;
            return Invoke(Command::VerifyTarget, {}, {}, deadline, unused, &result) && result;
        }

        bool ConfirmReplacement(
            const std::wstring_view replacementText,
            const std::chrono::steady_clock::time_point deadline) override
        {
            bool result = false;
            TextExpansionPreparationResult unused;
            return Invoke(Command::Confirm, {}, std::wstring{ replacementText }, deadline, unused, &result) && result;
        }

        bool Rollback(const std::chrono::steady_clock::time_point deadline) override
        {
            bool result = false;
            TextExpansionPreparationResult unused;
            return Invoke(Command::Rollback, {}, {}, deadline, unused, &result) && result;
        }

        void MarkCommitted() noexcept override
        {
            rollbackAllowed.store(false, std::memory_order_release);
        }

        void Finish(const std::chrono::steady_clock::time_point deadline) noexcept override
        {
            TextExpansionPreparationResult unused;
            bool result = false;
            Invoke(Command::Finish, {}, {}, deadline, unused, &result);
        }

        bool HasPendingWork() const noexcept override
        {
            return activeRequest.load(std::memory_order_acquire) || ShouldBlockNewInput();
        }

        bool ShouldBlockNewInput() const noexcept override
        {
            return (preparedStateActive.load(std::memory_order_acquire) ||
                    selectionMayHaveChanged.load(std::memory_order_acquire)) &&
                   IsTargetWindowCurrent();
        }

    private:
        bool IsTargetWindowCurrent() const noexcept override
        {
            const HWND expectedWindow = targetWindow.load(std::memory_order_acquire);
            const DWORD expectedProcessId = targetProcessId.load(std::memory_order_acquire);
            return expectedWindow && expectedProcessId && GetFocusedWindow() == expectedWindow &&
                   GetWindowProcessId(expectedWindow) == expectedProcessId;
        }

        enum class Command : uint8_t
        {
            Prepare,
            Verify,
            VerifyTarget,
            Confirm,
            Rollback,
            Finish,
        };

        struct Request
        {
            uint64_t id = 0;
            Command command = Command::Prepare;
            std::vector<TextExpansionCandidate> candidates;
            std::wstring text;
            std::chrono::steady_clock::time_point deadline;
        };

        bool Invoke(
            const Command command,
            const std::vector<TextExpansionCandidate>& candidates,
            const std::wstring& text,
            const std::chrono::steady_clock::time_point deadline,
            TextExpansionPreparationResult& preparationResult,
            bool* booleanResult) noexcept
        {
            std::unique_lock lock(mutex);
            if (!ready || stopping.load(std::memory_order_acquire) || requestPending)
            {
                return false;
            }

            const uint64_t id = ++nextRequestId;
            currentRequest = { id, command, candidates, text, deadline };
            requestPending = true;
            responseReady = false;
            activeRequest.store(true, std::memory_order_release);
            requestChanged.notify_one();

            if (!responseChanged.wait_until(lock, deadline, [this, id] {
                    return stopping.load(std::memory_order_acquire) || (responseReady && responseRequestId == id);
                }))
            {
                canceledThrough.store(id, std::memory_order_release);
                lock.unlock();
                if (const DWORD threadId = workerThreadId.load(std::memory_order_acquire))
                {
                    CoCancelCall(threadId, 0);
                }
                return false;
            }

            if (stopping.load(std::memory_order_acquire) || responseRequestId != id)
            {
                return false;
            }

            preparationResult = responsePreparation;
            if (booleanResult)
            {
                *booleanResult = responseBoolean;
            }
            responseReady = false;
            return true;
        }

        bool IsCanceled(const uint64_t id) const noexcept
        {
            return stopping.load(std::memory_order_acquire) || canceledThrough.load(std::memory_order_acquire) >= id;
        }

        void WorkerProc()
        {
            workerThreadId.store(GetCurrentThreadId(), std::memory_order_release);
            const HRESULT apartmentResult = CoInitializeEx(nullptr, COINIT_MULTITHREADED);
            if (SUCCEEDED(apartmentResult))
            {
                CoEnableCallCancellation(nullptr);
            }

            winrt::com_ptr<IUIAutomation> automation;
            HRESULT automationResult = E_FAIL;
            bool timeoutsConfigured = false;
            if (SUCCEEDED(apartmentResult))
            {
                automationResult = CoCreateInstance(
                    CLSID_CUIAutomation8,
                    nullptr,
                    CLSCTX_INPROC_SERVER,
                    IID_IUIAutomation,
                    automation.put_void());
                if (FAILED(automationResult))
                {
                    automationResult = CoCreateInstance(
                        CLSID_CUIAutomation,
                        nullptr,
                        CLSCTX_INPROC_SERVER,
                        IID_IUIAutomation,
                        automation.put_void());
                }

                if (automation)
                {
                    // The timeout setters are available from IUIAutomation2 onward. Using a
                    // newer interface would unnecessarily exclude supported Windows builds.
                    if (auto automation2 = automation.try_as<IUIAutomation2>())
                    {
                        // UIAutomationCore rejects values below 50 milliseconds.
                        constexpr DWORD providerTimeoutMilliseconds = 50;
                        timeoutsConfigured = SUCCEEDED(automation2->put_ConnectionTimeout(providerTimeoutMilliseconds)) &&
                                             SUCCEEDED(automation2->put_TransactionTimeout(providerTimeoutMilliseconds));
                    }
                }
            }

            {
                std::scoped_lock lock(mutex);
                ready = SUCCEEDED(automationResult) && timeoutsConfigured;
                initialized = true;
            }
            initializationChanged.notify_all();

            winrt::com_ptr<IUIAutomationTextRange> preparedRange;
            winrt::com_ptr<IUIAutomationTextRange> rollbackCaret;
            winrt::com_ptr<IUIAutomationTextRange> preparedReplacementStart;
            winrt::com_ptr<IUIAutomationElement> preparedElement;
            std::wstring preparedSourceText;
            HWND preparedWindow = nullptr;
            DWORD preparedProcessId = 0;
            HWND committedWindow = nullptr;
            DWORD committedProcessId = 0;
            winrt::com_ptr<IUIAutomationElement> committedElement;
            winrt::com_ptr<IUIAutomationTextRange> committedReplacementStart;

            while (true)
            {
                Request request;
                {
                    std::unique_lock lock(mutex);
                    requestChanged.wait(lock, [this] { return stopping.load(std::memory_order_acquire) || requestPending; });
                    if (stopping.load(std::memory_order_acquire))
                    {
                        break;
                    }
                    request = std::move(currentRequest);
                }

                TextExpansionPreparationResult preparation;
                bool booleanResult = false;
                switch (request.command)
                {
                case Command::Prepare:
                    preparation = PrepareOnWorker(
                        automation.get(),
                        request,
                        preparedRange,
                        rollbackCaret,
                        preparedReplacementStart,
                        preparedElement,
                        preparedSourceText,
                        preparedWindow,
                        preparedProcessId);
                    break;
                case Command::Verify:
                    booleanResult = VerifyOnWorker(
                        automation.get(), preparedElement.get(), preparedRange.get(), preparedSourceText, preparedWindow, preparedProcessId);
                    break;
                case Command::VerifyTarget:
                    booleanResult = VerifyTargetOnWorker(
                        automation.get(), preparedElement.get(), preparedWindow, preparedProcessId);
                    break;
                case Command::Confirm:
                    booleanResult = ConfirmReplacementOnWorker(
                        automation.get(), request.id, request.text, committedElement.get(), committedReplacementStart.get(), committedWindow, committedProcessId, request.deadline);
                    targetWindow.store(nullptr, std::memory_order_release);
                    targetProcessId.store(0, std::memory_order_release);
                    break;
                case Command::Rollback:
                    booleanResult = RollbackOnWorker(
                        automation.get(), preparedElement, preparedRange, rollbackCaret, preparedReplacementStart, preparedWindow, preparedProcessId);
                    break;
                case Command::Finish:
                    committedElement = preparedElement;
                    committedReplacementStart = preparedReplacementStart;
                    committedWindow = preparedWindow;
                    committedProcessId = preparedProcessId;
                    preparedRange = nullptr;
                    rollbackCaret = nullptr;
                    preparedReplacementStart = nullptr;
                    preparedElement = nullptr;
                    preparedSourceText.clear();
                    preparedWindow = nullptr;
                    preparedProcessId = 0;
                    preparedStateActive.store(false, std::memory_order_release);
                    selectionMayHaveChanged.store(false, std::memory_order_release);
                    rollbackAllowed.store(false, std::memory_order_release);
                    booleanResult = true;
                    break;
                }

                if (IsCanceled(request.id) && preparedStateActive.load(std::memory_order_acquire) &&
                    rollbackAllowed.load(std::memory_order_acquire))
                {
                    RollbackOnWorker(
                        automation.get(), preparedElement, preparedRange, rollbackCaret, preparedReplacementStart, preparedWindow, preparedProcessId);
                }

                {
                    std::scoped_lock lock(mutex);
                    requestPending = false;
                    activeRequest.store(false, std::memory_order_release);
                    responseRequestId = request.id;
                    responsePreparation = std::move(preparation);
                    responseBoolean = booleanResult;
                    responseReady = true;
                }
                responseChanged.notify_all();
            }

            if (preparedStateActive.load(std::memory_order_acquire))
            {
                if (rollbackAllowed.load(std::memory_order_acquire))
                {
                    RollbackOnWorker(automation.get(), preparedElement, preparedRange, rollbackCaret, preparedReplacementStart, preparedWindow, preparedProcessId);
                }
                else
                {
                    preparedRange = nullptr;
                    rollbackCaret = nullptr;
                    preparedReplacementStart = nullptr;
                    preparedElement = nullptr;
                    preparedSourceText.clear();
                    preparedWindow = nullptr;
                    preparedProcessId = 0;
                    preparedStateActive.store(false, std::memory_order_release);
                    selectionMayHaveChanged.store(false, std::memory_order_release);
                    targetWindow.store(nullptr, std::memory_order_release);
                    targetProcessId.store(0, std::memory_order_release);
                }
            }

            // Release every UI Automation proxy while this thread's COM apartment is
            // still initialized. Letting these com_ptr instances unwind after
            // CoUninitialize can invoke provider Release calls outside a valid apartment.
            preparedRange = nullptr;
            rollbackCaret = nullptr;
            preparedReplacementStart = nullptr;
            preparedElement = nullptr;
            committedElement = nullptr;
            committedReplacementStart = nullptr;
            automation = nullptr;
            if (SUCCEEDED(apartmentResult))
            {
                CoDisableCallCancellation(nullptr);
                CoUninitialize();
            }
            workerThreadId.store(0, std::memory_order_release);
        }

        TextExpansionPreparationResult PrepareOnWorker(
            IUIAutomation* automation,
            const Request& request,
            winrt::com_ptr<IUIAutomationTextRange>& preparedRange,
            winrt::com_ptr<IUIAutomationTextRange>& rollbackCaret,
            winrt::com_ptr<IUIAutomationTextRange>& preparedReplacementStart,
            winrt::com_ptr<IUIAutomationElement>& preparedElement,
            std::wstring& preparedSourceText,
            HWND& preparedWindow,
            DWORD& preparedProcessId)
        {
            TextExpansionPreparationResult result;
            result.status = TextExpansionPreparationStatus::UnsupportedContext;
            if (!automation || request.candidates.empty() || IsCanceled(request.id))
            {
                return result;
            }

            if (preparedStateActive.load(std::memory_order_acquire))
            {
                if (!RollbackOnWorker(automation, preparedElement, preparedRange, rollbackCaret, preparedReplacementStart, preparedWindow, preparedProcessId))
                {
                    result.status = TextExpansionPreparationStatus::FailedChangedOrUnknown;
                    return result;
                }
            }
            targetWindow.store(nullptr, std::memory_order_release);
            targetProcessId.store(0, std::memory_order_release);

            const HWND focusedWindow = GetFocusedWindow();
            const DWORD windowProcessId = GetWindowProcessId(focusedWindow);
            winrt::com_ptr<IUIAutomationElement> element;
            DWORD elementProcessId = 0;
            winrt::com_ptr<IUIAutomationTextRange> caret;
            if (!focusedWindow || !windowProcessId ||
                FAILED(automation->GetFocusedElement(element.put())) || !element ||
                !TryGetElementProcessId(element.get(), elementProcessId) || elementProcessId != windowProcessId ||
                !TryGetSingleSelection(element.get(), caret))
            {
                return result;
            }

            if (!IsCollapsed(caret.get()))
            {
                result.status = TextExpansionPreparationStatus::NoMatch;
                return result;
            }

            const auto longest = std::max_element(
                request.candidates.begin(),
                request.candidates.end(),
                [](const auto& left, const auto& right) {
                    return left.sourceText.size() < right.sourceText.size();
                });
            const size_t maximumLength = longest->sourceText.size();
            winrt::com_ptr<IUIAutomationTextRange> searchRange;
            if (FAILED(caret->Clone(searchRange.put())) || !searchRange)
            {
                return result;
            }

            int moved = 0;
            const int units = -static_cast<int>((std::min)(maximumLength, static_cast<size_t>((std::numeric_limits<int>::max)())));
            if (FAILED(searchRange->MoveEndpointByUnit(TextPatternRangeEndpoint_Start, TextUnit_Character, units, &moved)))
            {
                return result;
            }

            std::wstring caretPrefix;
            if (!TryRead(searchRange.get(), caretPrefix))
            {
                return result;
            }

            const auto selectedCandidate = SelectTextExpansionCandidate(request.candidates, caretPrefix);
            if (!selectedCandidate)
            {
                result.status = TextExpansionPreparationStatus::NoMatch;
                return result;
            }
            const auto& matched = request.candidates[*selectedCandidate];

            BSTR source = SysAllocStringLen(matched.sourceText.data(), static_cast<UINT>(matched.sourceText.size()));
            if (!source)
            {
                result.status = TextExpansionPreparationStatus::FailedUnchanged;
                return result;
            }
            winrt::com_ptr<IUIAutomationTextRange> matchedRange;
            const HRESULT findResult = searchRange->FindText(source, TRUE, FALSE, matchedRange.put());
            SysFreeString(source);
            int endComparison = 0;
            std::wstring actualText;
            if (FAILED(findResult) || !matchedRange ||
                FAILED(matchedRange->CompareEndpoints(
                    TextPatternRangeEndpoint_End,
                    caret.get(),
                    TextPatternRangeEndpoint_End,
                    &endComparison)) ||
                endComparison != 0 || !TryRead(matchedRange.get(), actualText) || actualText != matched.sourceText)
            {
                result.status = TextExpansionPreparationStatus::FailedUnchanged;
                return result;
            }

            winrt::com_ptr<IUIAutomationTextRange> originalCaret;
            winrt::com_ptr<IUIAutomationTextRange> replacementStart;
            if (FAILED(caret->Clone(originalCaret.put())) || !originalCaret ||
                FAILED(matchedRange->Clone(replacementStart.put())) || !replacementStart ||
                FAILED(replacementStart->MoveEndpointByRange(
                    TextPatternRangeEndpoint_End,
                    matchedRange.get(),
                    TextPatternRangeEndpoint_Start)))
            {
                result.status = TextExpansionPreparationStatus::FailedUnchanged;
                return result;
            }

            targetWindow.store(focusedWindow, std::memory_order_release);
            targetProcessId.store(windowProcessId, std::memory_order_release);
            selectionMayHaveChanged.store(true, std::memory_order_release);
            if (IsCanceled(request.id))
            {
                selectionMayHaveChanged.store(false, std::memory_order_release);
                targetWindow.store(nullptr, std::memory_order_release);
                targetProcessId.store(0, std::memory_order_release);
                result.status = TextExpansionPreparationStatus::FailedUnchanged;
                return result;
            }

            preparedRange = std::move(matchedRange);
            rollbackCaret = std::move(originalCaret);
            preparedReplacementStart = std::move(replacementStart);
            preparedElement = element;
            preparedSourceText = matched.sourceText;
            preparedWindow = focusedWindow;
            preparedProcessId = windowProcessId;
            preparedStateActive.store(true, std::memory_order_release);
            rollbackAllowed.store(true, std::memory_order_release);

            const HRESULT selectResult = preparedRange->Select();
            if (FAILED(selectResult))
            {
                const bool restored = RollbackOnWorker(
                    automation, preparedElement, preparedRange, rollbackCaret, preparedReplacementStart, preparedWindow, preparedProcessId);
                result.status = restored ? TextExpansionPreparationStatus::FailedUnchanged :
                                           TextExpansionPreparationStatus::FailedChangedOrUnknown;
                return result;
            }

            if (IsCanceled(request.id) ||
                !VerifyOnWorker(automation, preparedElement.get(), preparedRange.get(), preparedSourceText, preparedWindow, preparedProcessId))
            {
                const bool restored = RollbackOnWorker(
                    automation, preparedElement, preparedRange, rollbackCaret, preparedReplacementStart, preparedWindow, preparedProcessId);
                result.status = restored ? TextExpansionPreparationStatus::FailedUnchanged :
                                           TextExpansionPreparationStatus::FailedChangedOrUnknown;
                return result;
            }

            result.status = TextExpansionPreparationStatus::Prepared;
            result.replacementText = matched.replacementText;
            result.profileIndex = matched.profileIndex;
            targetWindow.store(preparedWindow, std::memory_order_release);
            targetProcessId.store(preparedProcessId, std::memory_order_release);
            return result;
        }

        bool VerifyOnWorker(
            IUIAutomation* automation,
            IUIAutomationElement* expectedElement,
            IUIAutomationTextRange* expectedRange,
            const std::wstring_view expectedText,
            const HWND expectedWindow,
            const DWORD expectedProcessId)
        {
            if (!automation || !expectedRange || GetFocusedWindow() != expectedWindow ||
                GetWindowProcessId(expectedWindow) != expectedProcessId)
            {
                return false;
            }

            winrt::com_ptr<IUIAutomationElement> element;
            winrt::com_ptr<IUIAutomationTextRange> selection;
            DWORD elementProcessId = 0;
            std::wstring actualText;
            return SUCCEEDED(automation->GetFocusedElement(element.put())) && element &&
                   AreSameElement(automation, element.get(), expectedElement) &&
                   TryGetElementProcessId(element.get(), elementProcessId) && elementProcessId == expectedProcessId &&
                   TryGetSingleSelection(element.get(), selection) && !IsCollapsed(selection.get()) &&
                   AreEqual(selection.get(), expectedRange) && TryRead(selection.get(), actualText) && actualText == expectedText;
        }

        bool VerifyTargetOnWorker(
            IUIAutomation* automation,
            IUIAutomationElement* expectedElement,
            const HWND expectedWindow,
            const DWORD expectedProcessId)
        {
            if (!automation || !expectedElement || GetFocusedWindow() != expectedWindow ||
                GetWindowProcessId(expectedWindow) != expectedProcessId)
            {
                return false;
            }

            winrt::com_ptr<IUIAutomationElement> currentElement;
            return SUCCEEDED(automation->GetFocusedElement(currentElement.put())) && currentElement &&
                   AreSameElement(automation, currentElement.get(), expectedElement);
        }

        bool ConfirmReplacementOnWorker(
            IUIAutomation* automation,
            const uint64_t requestId,
            const std::wstring_view expectedText,
            IUIAutomationElement* expectedElement,
            IUIAutomationTextRange* replacementStart,
            const HWND expectedWindow,
            const DWORD expectedProcessId,
            const std::chrono::steady_clock::time_point deadline)
        {
            while (!IsCanceled(requestId) && std::chrono::steady_clock::now() < deadline)
            {
                if (TryConfirmReplacementOnce(
                        automation, expectedText, expectedElement, replacementStart, expectedWindow, expectedProcessId))
                {
                    return true;
                }
                Sleep(5);
            }

            return false;
        }

        bool TryConfirmReplacementOnce(
            IUIAutomation* automation,
            const std::wstring_view expectedText,
            IUIAutomationElement* expectedElement,
            IUIAutomationTextRange* replacementStart,
            const HWND expectedWindow,
            const DWORD expectedProcessId)
        {
            if (!automation || expectedText.empty() || !expectedElement || !replacementStart || !expectedWindow || !expectedProcessId ||
                GetFocusedWindow() != expectedWindow || GetWindowProcessId(expectedWindow) != expectedProcessId)
            {
                return false;
            }

            winrt::com_ptr<IUIAutomationElement> element;
            winrt::com_ptr<IUIAutomationTextRange> caret;
            DWORD elementProcessId = 0;
            if (FAILED(automation->GetFocusedElement(element.put())) || !element ||
                !AreSameElement(automation, element.get(), expectedElement) ||
                !TryGetElementProcessId(element.get(), elementProcessId) || elementProcessId != expectedProcessId ||
                !TryGetSingleSelection(element.get(), caret) || !IsCollapsed(caret.get()))
            {
                return false;
            }

            winrt::com_ptr<IUIAutomationTextRange> replacementRange;
            if (FAILED(replacementStart->Clone(replacementRange.put())) || !replacementRange ||
                FAILED(replacementRange->MoveEndpointByRange(
                    TextPatternRangeEndpoint_End,
                    caret.get(),
                    TextPatternRangeEndpoint_End)))
            {
                return false;
            }

            int ordering = 0;
            if (FAILED(replacementRange->CompareEndpoints(
                    TextPatternRangeEndpoint_Start,
                    replacementRange.get(),
                    TextPatternRangeEndpoint_End,
                    &ordering)) || ordering > 0)
            {
                return false;
            }

            std::wstring actualReplacement;
            if (!TryRead(replacementRange.get(), actualReplacement))
            {
                return false;
            }
            return IsTextExpansionReplacementExact(actualReplacement, expectedText);
        }

        bool RollbackOnWorker(
            IUIAutomation* automation,
            winrt::com_ptr<IUIAutomationElement>& preparedElement,
            winrt::com_ptr<IUIAutomationTextRange>& preparedRange,
            winrt::com_ptr<IUIAutomationTextRange>& rollbackCaret,
            winrt::com_ptr<IUIAutomationTextRange>& preparedReplacementStart,
            HWND& preparedWindow,
            DWORD& preparedProcessId)
        {
            if (preparedStateActive.load(std::memory_order_acquire) &&
                !rollbackAllowed.load(std::memory_order_acquire))
            {
                // Replacement input has already reached the target. Never move the
                // caret back to its pre-expansion position, including from explicit
                // recovery, cancellation, Stop, or worker-exit paths.
                preparedRange = nullptr;
                rollbackCaret = nullptr;
                preparedReplacementStart = nullptr;
                preparedElement = nullptr;
                preparedWindow = nullptr;
                preparedProcessId = 0;
                preparedStateActive.store(false, std::memory_order_release);
                selectionMayHaveChanged.store(false, std::memory_order_release);
                targetWindow.store(nullptr, std::memory_order_release);
                targetProcessId.store(0, std::memory_order_release);
                return false;
            }

            bool restored = !preparedStateActive.load(std::memory_order_acquire);
            if (!restored && automation && preparedElement && rollbackCaret)
            {
                restored = SUCCEEDED(rollbackCaret->Select());
                if (restored)
                {
                    winrt::com_ptr<IUIAutomationTextRange> current;
                    restored = TryGetSingleSelection(preparedElement.get(), current) && IsCollapsed(current.get()) &&
                               AreEqual(current.get(), rollbackCaret.get());
                }
            }

            if (restored)
            {
                preparedRange = nullptr;
                rollbackCaret = nullptr;
                preparedReplacementStart = nullptr;
                preparedElement = nullptr;
                preparedWindow = nullptr;
                preparedProcessId = 0;
                preparedStateActive.store(false, std::memory_order_release);
                selectionMayHaveChanged.store(false, std::memory_order_release);
                rollbackAllowed.store(false, std::memory_order_release);
                targetWindow.store(nullptr, std::memory_order_release);
                targetProcessId.store(0, std::memory_order_release);
            }
            return restored;
        }

        mutable std::mutex mutex;
        std::condition_variable initializationChanged;
        std::condition_variable requestChanged;
        std::condition_variable responseChanged;
        std::thread worker;
        std::atomic_bool stopping = false;
        bool initialized = false;
        bool ready = false;
        bool requestPending = false;
        bool responseReady = false;
        uint64_t nextRequestId = 0;
        uint64_t responseRequestId = 0;
        Request currentRequest;
        TextExpansionPreparationResult responsePreparation;
        bool responseBoolean = false;
        std::atomic_uint64_t canceledThrough = 0;
        std::atomic<DWORD> workerThreadId = 0;
        std::atomic_bool activeRequest = false;
        std::atomic_bool preparedStateActive = false;
        std::atomic_bool selectionMayHaveChanged = false;
        std::atomic_bool rollbackAllowed = false;
        std::atomic<HWND> targetWindow = nullptr;
        std::atomic<DWORD> targetProcessId = 0;
    };
}

std::unique_ptr<ITextExpansionTextContext> CreateUIAutomationTextExpansionContext()
{
    return std::make_unique<UIAutomationTextExpansionContext>();
}
