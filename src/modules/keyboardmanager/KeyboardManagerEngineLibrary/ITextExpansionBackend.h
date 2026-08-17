#pragma once

#include "TextExpansionTypes.h"

class ITextExpansionBackend
{
public:
    virtual ~ITextExpansionBackend() = default;

    virtual bool Start() = 0;
    virtual void Stop() noexcept = 0;
    // Prepare is called from the low-level hook. It may use the text context, but
    // must never inject input. A Prepared result owns the selected source range
    // until CompletePendingActivation or CancelPendingActivation is called.
    virtual TextExpansionResult PrepareActivation(const TextExpansionRequest& request) = 0;

    // Completion is dispatched through the engine thread's message queue so the
    // low-level hook has returned before SendInput starts delivering replacement
    // text to the target application.
    virtual TextExpansionResult CompletePendingActivation() noexcept = 0;
    virtual TextExpansionResult CancelPendingActivation() noexcept = 0;
    virtual void RetryPendingCleanup() noexcept = 0;
    virtual bool ShouldBlockNewInput() const noexcept = 0;
    virtual bool HasPendingWork() const noexcept = 0;
};
