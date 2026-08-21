#include "pch.h"
#include "CompatibilityTextExpansionBackend.h"

#include <algorithm>
#include <iterator>
#include <utility>

#include <keyboardmanager/common/Helpers.h>
#include <keyboardmanager/common/KeyboardManagerConstants.h>

#include "UIAutomationTextExpansionContext.h"

namespace
{
    constexpr auto textContextOperationTimeout = std::chrono::milliseconds(125);

    std::chrono::steady_clock::time_point FreshTextContextDeadline() noexcept
    {
        return std::chrono::steady_clock::now() + textContextOperationTimeout;
    }

    struct ActivationCompletionGuard
    {
        std::atomic_bool& active;
        ~ActivationCompletionGuard()
        {
            active.store(false, std::memory_order_release);
        }
    };

    std::vector<INPUT> CreateCleanupForInjectedPrefix(
        const std::vector<INPUT>& inputs,
        const size_t injectedCount)
    {
        std::vector<INPUT> outstandingDowns;
        const size_t prefixLength = (std::min)(inputs.size(), injectedCount);
        for (size_t index = 0; index < prefixLength; ++index)
        {
            const INPUT& event = inputs[index];
            if (event.type != INPUT_KEYBOARD)
            {
                continue;
            }

            const bool keyUp = (event.ki.dwFlags & KEYEVENTF_KEYUP) != 0;
            if (!keyUp)
            {
                outstandingDowns.push_back(event);
                continue;
            }

            const auto matchingDown = std::find_if(
                outstandingDowns.rbegin(),
                outstandingDowns.rend(),
                [&](const INPUT& down) {
                    const bool unicode = (event.ki.dwFlags & KEYEVENTF_UNICODE) != 0;
                    return unicode ? down.ki.wScan == event.ki.wScan : down.ki.wVk == event.ki.wVk;
                });
            if (matchingDown != outstandingDowns.rend())
            {
                outstandingDowns.erase(std::next(matchingDown).base());
            }
        }

        std::vector<INPUT> cleanup;
        cleanup.reserve(outstandingDowns.size());
        for (auto iterator = outstandingDowns.rbegin(); iterator != outstandingDowns.rend(); ++iterator)
        {
            INPUT release = *iterator;
            release.ki.dwFlags |= KEYEVENTF_KEYUP;
            cleanup.push_back(release);
        }
        return cleanup;
    }

    std::vector<INPUT> CreateUninjectedSuffix(
        const std::vector<INPUT>& inputs,
        const size_t injectedCount)
    {
        const size_t prefixLength = (std::min)(inputs.size(), injectedCount);
        return { inputs.begin() + prefixLength, inputs.end() };
    }

    KeyboardManagerInput::SendVirtualInputResult SendModifierReleases(
        KeyboardManagerInput::InputInterface& input,
        const std::vector<DWORD>& modifierKeys,
        std::vector<INPUT>& sentEvents)
    {
        if (modifierKeys.empty())
        {
            return { KeyboardManagerInput::SendVirtualInputStatus::Complete, 0 };
        }

        Helpers::SetDummyKeyEvent(sentEvents, KeyboardManagerConstants::KEYBOARDMANAGER_SHORTCUT_FLAG);
        for (const DWORD key : modifierKeys)
        {
            Helpers::SetKeyEvent(
                sentEvents,
                INPUT_KEYBOARD,
                static_cast<WORD>(key),
                KEYEVENTF_KEYUP,
                KeyboardManagerConstants::KEYBOARDMANAGER_SHORTCUT_FLAG);
        }

        return input.SendVirtualInput(sentEvents);
    }

    void AppendTextUnit(std::vector<INPUT>& events, const wchar_t value)
    {
        if (value == L'\r' || value == L'\n')
        {
            Helpers::SetKeyEvent(
                events,
                INPUT_KEYBOARD,
                VK_RETURN,
                0,
                KeyboardManagerConstants::KEYBOARDMANAGER_SHORTCUT_FLAG);
            Helpers::SetKeyEvent(
                events,
                INPUT_KEYBOARD,
                VK_RETURN,
                KEYEVENTF_KEYUP,
                KeyboardManagerConstants::KEYBOARDMANAGER_SHORTCUT_FLAG);
            return;
        }

        INPUT down{};
        down.type = INPUT_KEYBOARD;
        down.ki.dwFlags = KEYEVENTF_UNICODE;
        down.ki.dwExtraInfo = KeyboardManagerConstants::KEYBOARDMANAGER_SHORTCUT_FLAG;
        down.ki.wScan = value;
        events.push_back(down);

        INPUT up = down;
        up.ki.dwFlags |= KEYEVENTF_KEYUP;
        events.push_back(up);
    }

    TextExpansionResult SendReplacementText(
        KeyboardManagerInput::InputInterface& input,
        const std::wstring& text,
        bool& inputStreamMutated,
        bool& replacementTextCommitted,
        const std::function<void()>& markCommitted,
        const std::function<bool()>& isTargetWindowCurrent,
        const std::function<void(std::vector<INPUT>)>& queueCleanup)
    {
        // Submit each UTF-16 unit separately after the complete activation press has been
        // released. Large KEYEVENTF_UNICODE batches are known to repeat or drop characters
        // in real text controls.
        std::vector<INPUT> unit;
        unit.reserve(2);

        for (size_t index = 0; index < text.size(); ++index)
        {
            if (!isTargetWindowCurrent())
            {
                return inputStreamMutated ? TextExpansionResult::FailedChangedOrUnknown :
                                            TextExpansionResult::FailedUnchanged;
            }

            wchar_t value = text[index];
            if (value == L'\r' && index + 1 < text.size() && text[index + 1] == L'\n')
            {
                ++index;
            }

            unit.clear();
            AppendTextUnit(unit, value);
            const auto result = input.SendVirtualInput(unit);
            if (result.status == KeyboardManagerInput::SendVirtualInputStatus::None)
            {
                return inputStreamMutated ? TextExpansionResult::FailedChangedOrUnknown :
                                            TextExpansionResult::FailedUnchanged;
            }

            if (result.injectedEventCount != 0)
            {
                inputStreamMutated = true;
                if (!replacementTextCommitted)
                {
                    markCommitted();
                    replacementTextCommitted = true;
                }
            }
            if (result.status == KeyboardManagerInput::SendVirtualInputStatus::Partial)
            {
                queueCleanup(CreateCleanupForInjectedPrefix(unit, result.injectedEventCount));
                return TextExpansionResult::FailedChangedOrUnknown;
            }
        }

        return TextExpansionResult::Replaced;
    }

    TextExpansionResult MapPreparationStatus(const TextExpansionPreparationStatus status)
    {
        switch (status)
        {
        case TextExpansionPreparationStatus::NoMatch:
            return TextExpansionResult::NoMatch;
        case TextExpansionPreparationStatus::UnsupportedContext:
            return TextExpansionResult::UnsupportedContext;
        case TextExpansionPreparationStatus::FailedUnchanged:
            return TextExpansionResult::FailedUnchanged;
        case TextExpansionPreparationStatus::FailedChangedOrUnknown:
            return TextExpansionResult::FailedChangedOrUnknown;
        case TextExpansionPreparationStatus::Prepared:
        default:
            return TextExpansionResult::FailedChangedOrUnknown;
        }
    }
}

CompatibilityTextExpansionBackend::CompatibilityTextExpansionBackend(
    KeyboardManagerInput::InputInterface& input,
    std::unique_ptr<ITextExpansionTextContext> textContext) :
    input(input),
    textContext(textContext ? std::move(textContext) : CreateUIAutomationTextExpansionContext())
{
}

bool CompatibilityTextExpansionBackend::Start()
{
    if (started.load(std::memory_order_acquire))
    {
        return true;
    }

    const bool ready = textContext && textContext->Start();
    started.store(ready, std::memory_order_release);
    return ready;
}

void CompatibilityTextExpansionBackend::Stop() noexcept
{
    started.store(false, std::memory_order_release);

    std::scoped_lock activationLock(activationMutex);
    std::vector<DWORD> modifierKeys;
    if (pendingActivation)
    {
        modifierKeys = pendingActivation->activationModifierKeys;
        RollbackPreparedSelection();
        pendingActivation.reset();
    }
    activationInProgress.store(false, std::memory_order_release);

    ReleaseCapturedModifiers(modifierKeys);
    RetryPendingCleanup();
    if (textContext)
    {
        textContext->Stop();
    }
}

TextExpansionResult CompatibilityTextExpansionBackend::PrepareActivation(const TextExpansionRequest& request)
{
    if (!started.load(std::memory_order_acquire) || !textContext || request.candidates.empty())
    {
        return TextExpansionResult::UnsupportedContext;
    }

    {
        std::scoped_lock lock(pendingCleanupMutex);
        if (!pendingCleanup.empty())
        {
            return TextExpansionResult::FailedChangedOrUnknown;
        }
    }

    std::scoped_lock activationLock(activationMutex);
    if (activationInProgress.exchange(true, std::memory_order_acq_rel))
    {
        return TextExpansionResult::FailedUnchanged;
    }

    try
    {
        const auto preparation = textContext->Prepare(request.candidates, request.deadline);
        if (preparation.status != TextExpansionPreparationStatus::Prepared)
        {
            activationInProgress.store(false, std::memory_order_release);
            return MapPreparationStatus(preparation.status);
        }

        if (!textContext->VerifyPreparedSelection(request.deadline))
        {
            activationInProgress.store(false, std::memory_order_release);
            return RollbackPreparedSelection();
        }
        if (std::chrono::steady_clock::now() >= request.deadline)
        {
            activationInProgress.store(false, std::memory_order_release);
            return RollbackPreparedSelection();
        }

        pendingActivation = PendingActivation{
            .activationModifierKeys = request.activationModifierKeys,
            .replacementText = preparation.replacementText,
        };
        return TextExpansionResult::Prepared;
    }
    catch (...)
    {
        activationInProgress.store(false, std::memory_order_release);
        return RollbackPreparedSelection();
    }
}

TextExpansionResult CompatibilityTextExpansionBackend::CompletePendingActivation() noexcept
{
    std::scoped_lock activationLock(activationMutex);
    if (!pendingActivation || !activationInProgress.load(std::memory_order_acquire) ||
        !started.load(std::memory_order_acquire) || !textContext)
    {
        return TextExpansionResult::FailedUnchanged;
    }

    PendingActivation activation = std::move(*pendingActivation);
    pendingActivation.reset();
    ActivationCompletionGuard completionGuard{ activationInProgress };
    const auto& modifierKeys = activation.activationModifierKeys;
    bool inputStreamMutated = !modifierKeys.empty();
    bool modifierReleaseAttempted = false;
    bool replacementTextCommitted = false;
    bool committedSelectionFinished = false;

    try
    {
        // This deadline intentionally starts in the message-pump callback. The hook-side
        // preparation deadline may already have expired while this message was queued.
        if (!textContext->VerifyPreparedSelection(FreshTextContextDeadline()))
        {
            const auto rollbackResult = RollbackPreparedSelection();
            modifierReleaseAttempted = true;
            ReleaseCapturedModifiers(modifierKeys);
            return modifierKeys.empty() ? rollbackResult : TextExpansionResult::FailedChangedOrUnknown;
        }

        modifierReleaseAttempted = true;
        if (!ReleaseCapturedModifiers(modifierKeys))
        {
            RollbackPreparedSelection();
            return TextExpansionResult::FailedChangedOrUnknown;
        }

        // Modifier neutralization itself can change focus or selection. Revalidate once
        // more immediately before the first replacement unit, then perform no UIA work
        // until the complete input stream has been submitted.
        if (!modifierKeys.empty() && !textContext->VerifyPreparedSelection(FreshTextContextDeadline()))
        {
            if (inputStreamMutated)
            {
                RollbackPreparedSelection();
                return TextExpansionResult::FailedChangedOrUnknown;
            }
            return RollbackPreparedSelection();
        }

        const TextExpansionResult inputResult = SendReplacementText(
            input,
            activation.replacementText,
            inputStreamMutated,
            replacementTextCommitted,
            [this] { textContext->MarkCommitted(); },
            [this] { return textContext->IsTargetWindowCurrent(); },
            [this](std::vector<INPUT> cleanup) { QueuePendingCleanup(std::move(cleanup)); });
        if (inputResult == TextExpansionResult::Replaced)
        {
            FinishCommittedSelection();
            committedSelectionFinished = true;
            return textContext->ConfirmReplacement(activation.replacementText, FreshTextContextDeadline()) ?
                       TextExpansionResult::Replaced :
                       TextExpansionResult::FailedChangedOrUnknown;
        }

        if (!replacementTextCommitted)
        {
            const auto rollbackResult = RollbackPreparedSelection();
            return inputStreamMutated ? TextExpansionResult::FailedChangedOrUnknown : rollbackResult;
        }

        // Once any text or modifier input reached the target, restoring only the UIA
        // selection cannot prove the complete input stream unchanged.
        FinishCommittedSelection();
        committedSelectionFinished = true;
        return TextExpansionResult::FailedChangedOrUnknown;
    }
    catch (...)
    {
        if (!modifierReleaseAttempted)
        {
            ReleaseCapturedModifiers(modifierKeys);
        }
        if (replacementTextCommitted && !committedSelectionFinished)
        {
            FinishCommittedSelection();
        }
        if (replacementTextCommitted)
        {
            return TextExpansionResult::FailedChangedOrUnknown;
        }
        const auto rollbackResult = RollbackPreparedSelection();
        return inputStreamMutated ? TextExpansionResult::FailedChangedOrUnknown : rollbackResult;
    }
}

TextExpansionResult CompatibilityTextExpansionBackend::CancelPendingActivation() noexcept
{
    std::scoped_lock activationLock(activationMutex);
    if (!pendingActivation)
    {
        return TextExpansionResult::FailedUnchanged;
    }

    const auto modifierKeys = pendingActivation->activationModifierKeys;
    pendingActivation.reset();
    activationInProgress.store(false, std::memory_order_release);
    const auto rollbackResult = RollbackPreparedSelection();
    ReleaseCapturedModifiers(modifierKeys);
    return modifierKeys.empty() ? rollbackResult : TextExpansionResult::FailedChangedOrUnknown;
}

bool CompatibilityTextExpansionBackend::ReleaseCapturedModifiers(
    const std::vector<DWORD>& modifierKeys) noexcept
{
    if (modifierKeys.empty())
    {
        return true;
    }

    std::vector<INPUT> modifierEvents;
    try
    {
        const auto result = SendModifierReleases(input, modifierKeys, modifierEvents);
        if (!result.IsComplete())
        {
            QueuePendingCleanup(CreateUninjectedSuffix(modifierEvents, result.injectedEventCount));
        }
        return result.IsComplete();
    }
    catch (...)
    {
        try
        {
            QueuePendingCleanup(std::move(modifierEvents));
        }
        catch (...)
        {
        }
        return false;
    }
}

TextExpansionResult CompatibilityTextExpansionBackend::RollbackPreparedSelection() noexcept
{
    try
    {
        return textContext && textContext->Rollback(FreshTextContextDeadline()) ?
                   TextExpansionResult::FailedUnchanged :
                   TextExpansionResult::FailedChangedOrUnknown;
    }
    catch (...)
    {
        return TextExpansionResult::FailedChangedOrUnknown;
    }
}

void CompatibilityTextExpansionBackend::FinishCommittedSelection() noexcept
{
    if (textContext)
    {
        textContext->Finish(FreshTextContextDeadline());
    }
}

void CompatibilityTextExpansionBackend::QueuePendingCleanup(std::vector<INPUT> cleanup)
{
    if (cleanup.empty())
    {
        return;
    }

    {
        std::scoped_lock lock(pendingCleanupMutex);
        pendingCleanup.insert(pendingCleanup.end(), cleanup.begin(), cleanup.end());
    }
    RetryPendingCleanup();
}

void CompatibilityTextExpansionBackend::RetryPendingCleanup() noexcept
{
    if (textContext && !activationInProgress.load(std::memory_order_acquire) &&
        textContext->HasPendingWork())
    {
        try
        {
            textContext->Rollback(FreshTextContextDeadline());
        }
        catch (...)
        {
        }
    }

    std::vector<INPUT> cleanup;
    {
        std::scoped_lock lock(pendingCleanupMutex);
        cleanup.swap(pendingCleanup);
    }
    if (cleanup.empty())
    {
        return;
    }

    KeyboardManagerInput::SendVirtualInputResult result;
    try
    {
        result = input.SendVirtualInput(cleanup);
    }
    catch (...)
    {
        result = { KeyboardManagerInput::SendVirtualInputStatus::None, 0 };
    }

    const size_t injectedCount = (std::min)(cleanup.size(), static_cast<size_t>(result.injectedEventCount));
    if (injectedCount == cleanup.size())
    {
        return;
    }

    std::vector<INPUT> remaining(cleanup.begin() + injectedCount, cleanup.end());
    std::scoped_lock lock(pendingCleanupMutex);
    remaining.insert(remaining.end(), pendingCleanup.begin(), pendingCleanup.end());
    pendingCleanup = std::move(remaining);
}

bool CompatibilityTextExpansionBackend::ShouldBlockNewInput() const noexcept
{
    {
        std::scoped_lock lock(pendingCleanupMutex);
        if (!pendingCleanup.empty())
        {
            return true;
        }
    }
    return textContext && textContext->ShouldBlockNewInput();
}

bool CompatibilityTextExpansionBackend::HasPendingWork() const noexcept
{
    return activationInProgress.load(std::memory_order_acquire) || ShouldBlockNewInput() ||
           (textContext && textContext->HasPendingWork());
}
