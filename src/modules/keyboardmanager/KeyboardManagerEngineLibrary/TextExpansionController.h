#pragma once

#include <atomic>
#include <functional>
#include <memory>
#include <mutex>
#include <optional>
#include <unordered_map>
#include <unordered_set>

#include <common/hooks/LowlevelKeyboardEvent.h>
#include <keyboardmanager/common/InputInterface.h>
#include <keyboardmanager/common/MappingConfiguration.h>

#include "ITextExpansionBackend.h"

class TextExpansionController
{
public:
    enum class EventDisposition : uint8_t
    {
        Ignore,
        Continue,
        FreshActionKeyDown,
        Suppress,
        ForcePassThrough,
    };

    explicit TextExpansionController(
        std::unique_ptr<ITextExpansionBackend> backend,
        std::function<bool(uint64_t)> queuePendingActivation = {});
    ~TextExpansionController();

    bool Start(KeyboardManagerInput::InputInterface& input);
    void Stop() noexcept;
    bool SetTextExpansions(const TextExpansionTable& rules) noexcept;
    bool HasConfiguredTextExpansions() const noexcept;

    // Called before editor/reload gates and before existing remaps. It fixes the
    // handling for this entire physical key press on its first down transition.
    EventDisposition BeginKeyboardEvent(LowlevelKeyboardEvent* data) noexcept;
    void NotifyHigherPriorityEventHandled(LowlevelKeyboardEvent* data) noexcept;
    void NotifyAloneRemapEventHandled(LowlevelKeyboardEvent* data, bool wasPending) noexcept;
    void TrackKeyboardEvent(LowlevelKeyboardEvent* data) noexcept;
    void ResetBuffer() noexcept;

    // Called only for a fresh action-key down that existing remaps did not consume.
    intptr_t TryActivate(
        KeyboardManagerInput::InputInterface& input,
        LowlevelKeyboardEvent* data) noexcept;
    TextExpansionResult CompletePendingActivation(uint64_t generation) noexcept;

    bool HasPendingWork() const noexcept;
    bool HasPendingBackendWork() const noexcept;
    void RetryPendingBackendWork() noexcept;

private:
    enum class ActionKeyPressDisposition : uint8_t
    {
        Passthrough,
        Suppressed,
    };

    enum class PendingActivationInterruption : uint8_t
    {
        None,
        Replayed,
        Suppress,
    };

    struct PendingActivationRelease
    {
        uint64_t generation = 0;
        DWORD physicalActionKey = 0;
        DWORD physicalActionScanCode = 0;
        bool physicalActionExtended = false;
        size_t physicalActionKeyIdentity = 0;
        bool actionReleased = false;
        bool commitQueued = false;
        std::unordered_set<DWORD> activationModifierKeys;
        std::unordered_set<DWORD> pressedActivationModifierKeys;
        std::unordered_set<DWORD> suppressedNewModifierKeys;
    };

    bool IsBackendReady() noexcept;
    bool HasPressedActionKey() const noexcept;
    bool ShouldForceArmingEvent(DWORD physicalKey, bool keyDown) noexcept;
    void UpdateTrackedPressStateLocked() noexcept;
    bool QueueBackendWork(uint64_t generation) noexcept;
    PendingActivationInterruption InterruptPendingActivationForNewInput(
        DWORD physicalKey,
        size_t physicalKeyIdentity,
        DWORD scanCode,
        bool extended) noexcept;
    bool HandlePendingActivationReleaseEvent(DWORD physicalKey, size_t physicalKeyIdentity, bool keyDown, bool keyUp) noexcept;

    std::unique_ptr<ITextExpansionBackend> backend;
    std::function<bool(uint64_t)> queuePendingActivation;
    std::atomic_bool backendReady = false;
    std::atomic_uint64_t nextActivationGeneration = 0;
    std::atomic_uint64_t pendingActivationGeneration = 0;
    std::atomic_bool cleanupMessageQueued = false;
    std::atomic_bool backendRecoveryPending = false;
    std::atomic<std::shared_ptr<const TextExpansionIndex>> textExpansionIndex;
    mutable std::atomic_bool hasTrackedPressState = false;
    mutable std::atomic_bool arming = false;
    mutable std::atomic_bool armingReleaseObserved = false;
    KeyboardManagerInput::InputInterface* inputState = nullptr;

    mutable std::mutex pressStateMutex;
    std::unordered_map<size_t, ActionKeyPressDisposition> actionKeyPresses;
    std::unordered_set<size_t> recoverySuppressedKeys;
    std::unordered_set<size_t> pendingReplayKeys;
    std::unordered_set<DWORD> higherPriorityModifierKeys;
    std::optional<PendingActivationRelease> pendingActivationRelease;
};
