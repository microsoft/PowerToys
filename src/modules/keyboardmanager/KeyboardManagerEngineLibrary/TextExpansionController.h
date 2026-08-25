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
    };

    explicit TextExpansionController(
        std::unique_ptr<ITextExpansionBackend> backend,
        std::function<bool(uint64_t)> queuePendingActivation = {});
    ~TextExpansionController();

    bool Start();
    void Stop() noexcept;

    // Called before editor/reload gates and before existing remaps. It fixes the
    // handling for this entire physical key press on its first down transition.
    EventDisposition BeginKeyboardEvent(LowlevelKeyboardEvent* data) noexcept;
    void NotifyHigherPriorityEventHandled(LowlevelKeyboardEvent* data) noexcept;
    void TrackKeyboardEvent(LowlevelKeyboardEvent* data) noexcept;
    void ResetBuffer() noexcept;

    // Called only for a fresh action-key down that existing remaps did not consume.
    intptr_t TryActivate(
        KeyboardManagerInput::InputInterface& input,
        DWORD physicalActionKey,
        const TextExpansionTable& rules) noexcept;
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

    struct PendingActivationRelease
    {
        uint64_t generation = 0;
        DWORD physicalActionKey = 0;
        bool actionReleased = false;
        bool commitQueued = false;
        std::unordered_set<DWORD> activationModifierKeys;
        std::unordered_set<DWORD> pressedActivationModifierKeys;
        std::unordered_set<DWORD> suppressedNewModifierKeys;
    };

    bool ActivationMatches(
        KeyboardManagerInput::InputInterface& input,
        const Shortcut& activation,
        DWORD physicalActionKey) const noexcept;
    bool QueueBackendWork(uint64_t generation) noexcept;
    bool HandlePendingActivationReleaseEvent(DWORD physicalKey, bool keyDown, bool keyUp) noexcept;

    std::unique_ptr<ITextExpansionBackend> backend;
    std::function<bool(uint64_t)> queuePendingActivation;
    std::atomic_bool backendReady = false;
    std::atomic_uint64_t nextActivationGeneration = 0;
    std::atomic_uint64_t pendingActivationGeneration = 0;
    std::atomic_bool cleanupMessageQueued = false;

    mutable std::mutex pressStateMutex;
    std::unordered_map<DWORD, ActionKeyPressDisposition> actionKeyPresses;
    std::unordered_set<DWORD> recoverySuppressedKeys;
    std::unordered_set<DWORD> higherPriorityModifierKeys;
    std::optional<PendingActivationRelease> pendingActivationRelease;
};
