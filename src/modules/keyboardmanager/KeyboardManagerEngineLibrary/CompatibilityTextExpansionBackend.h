#pragma once

#include <atomic>
#include <memory>
#include <mutex>
#include <optional>
#include <vector>

#include <keyboardmanager/common/InputInterface.h>

#include "ITextExpansionBackend.h"
#include "ITextExpansionTextContext.h"

class CompatibilityTextExpansionBackend final : public ITextExpansionBackend
{
public:
    explicit CompatibilityTextExpansionBackend(
        KeyboardManagerInput::InputInterface& input,
        std::unique_ptr<ITextExpansionTextContext> textContext = {});

    bool Start() override;
    void Stop() noexcept override;
    TextExpansionResult PrepareActivation(const TextExpansionRequest& request) override;
    TextExpansionResult CompletePendingActivation() noexcept override;
    TextExpansionResult CancelPendingActivation() noexcept override;
    void RetryPendingCleanup() noexcept override;
    bool ShouldBlockNewInput() const noexcept override;
    bool HasPendingWork() const noexcept override;

private:
    struct PendingActivation
    {
        std::vector<DWORD> activationModifierKeys;
        std::wstring replacementText;
    };

    void QueuePendingCleanup(std::vector<INPUT> cleanup);
    bool ReleaseCapturedModifiers(const std::vector<DWORD>& modifierKeys) noexcept;
    TextExpansionResult RollbackPreparedSelection() noexcept;
    void FinishCommittedSelection() noexcept;

    KeyboardManagerInput::InputInterface& input;
    std::unique_ptr<ITextExpansionTextContext> textContext;
    std::atomic_bool started = false;
    std::atomic_bool activationInProgress = false;
    mutable std::mutex activationMutex;
    std::optional<PendingActivation> pendingActivation;
    mutable std::mutex pendingCleanupMutex;
    std::vector<INPUT> pendingCleanup;
};
