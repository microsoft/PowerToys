#pragma once

#include "TextExpansionTypes.h"

class ITextExpansionTextContext
{
public:
    virtual ~ITextExpansionTextContext() = default;

    virtual bool Start() = 0;
    virtual void Stop() noexcept = 0;

    virtual TextExpansionPreparationResult Prepare(
        const std::vector<TextExpansionCandidate>& candidates,
        std::chrono::steady_clock::time_point deadline) = 0;
    virtual bool VerifyPreparedSelection(std::chrono::steady_clock::time_point deadline) = 0;
    virtual bool IsTargetContextCurrent(std::chrono::steady_clock::time_point deadline) = 0;
    virtual bool IsTargetWindowCurrent() const noexcept = 0;
    virtual bool ConfirmReplacement(
        std::wstring_view replacementText,
        std::chrono::steady_clock::time_point deadline) = 0;
    virtual bool Rollback(std::chrono::steady_clock::time_point deadline) = 0;
    // Called synchronously as soon as the first replacement-text event is accepted
    // by SendInput. From this point onward no cancellation or shutdown path may
    // restore the original caret/selection.
    virtual void MarkCommitted() noexcept = 0;
    virtual void Finish(std::chrono::steady_clock::time_point deadline) noexcept = 0;
    virtual bool ShouldBlockNewInput() const noexcept = 0;
    virtual bool HasPendingWork() const noexcept = 0;
};
