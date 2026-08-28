#pragma once

#include <common/hooks/LowlevelKeyboardEvent.h>

#include "TextExpansionTypes.h"

class ITextExpansionBackend
{
public:
    virtual ~ITextExpansionBackend() = default;

    virtual bool Start() = 0;
    virtual void Stop() noexcept = 0;
    // A backend can fault itself after a bounded input-recovery failure.
    virtual bool IsReady() const noexcept = 0;
    // Returns true when a raw physical cycle must bypass remaps to release key state
    // left behind by an abandoned partial SendInput sequence.
    virtual bool HasRecoveryKeyState() const noexcept = 0;
    virtual bool HandleRecoveryKeyEvent(const LowlevelKeyboardEvent* data) noexcept = 0;

    // Called only after the event has passed all higher-priority Keyboard Manager
    // handlers and will be delivered to the foreground application.
    virtual void TrackKeyboardEvent(const LowlevelKeyboardEvent* data) noexcept = 0;
    virtual void ResetBuffer() noexcept = 0;

    // Prepare is called from the low-level hook and must never inject input. A
    // Prepared result reserves the matched suffix until completion or cancellation.
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
