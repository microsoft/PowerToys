#include "pch.h"
#include "TextExpansionController.h"

#include <utility>

#include <common/interop/shared_constants.h>
#include <keyboardmanager/common/Helpers.h>
#include <keyboardmanager/common/KeyboardManagerConstants.h>

namespace
{
    constexpr uint8_t LeftWinMask = 1u << 0;
    constexpr uint8_t RightWinMask = 1u << 1;
    constexpr uint8_t LeftCtrlMask = 1u << 2;
    constexpr uint8_t RightCtrlMask = 1u << 3;
    constexpr uint8_t LeftAltMask = 1u << 4;
    constexpr uint8_t RightAltMask = 1u << 5;
    constexpr uint8_t LeftShiftMask = 1u << 6;
    constexpr uint8_t RightShiftMask = 1u << 7;

    constexpr bool IsKeyDown(const WPARAM message) noexcept
    {
        return message == WM_KEYDOWN || message == WM_SYSKEYDOWN;
    }

    constexpr bool IsKeyUp(const WPARAM message) noexcept
    {
        return message == WM_KEYUP || message == WM_SYSKEYUP;
    }

    uint8_t GetPressedModifierMask(KeyboardManagerInput::InputInterface& input) noexcept
    {
        uint8_t mask = 0;
        const auto append = [&](const int key, const uint8_t bit) {
            if (input.GetVirtualKeyState(key))
            {
                mask |= bit;
            }
        };
        append(VK_LWIN, LeftWinMask);
        append(VK_RWIN, RightWinMask);
        append(VK_LCONTROL, LeftCtrlMask);
        append(VK_RCONTROL, RightCtrlMask);
        append(VK_LMENU, LeftAltMask);
        append(VK_RMENU, RightAltMask);
        append(VK_LSHIFT, LeftShiftMask);
        append(VK_RSHIFT, RightShiftMask);
        return mask;
    }

    std::vector<DWORD> GetPressedActivationModifiers(const uint8_t modifierMask)
    {
        std::vector<DWORD> keys;
        keys.reserve(8);
        const auto append = [&](const uint8_t bit, const DWORD key) {
            if ((modifierMask & bit) != 0)
            {
                keys.push_back(key);
            }
        };
        append(LeftWinMask, VK_LWIN);
        append(RightWinMask, VK_RWIN);
        append(LeftCtrlMask, VK_LCONTROL);
        append(RightCtrlMask, VK_RCONTROL);
        append(LeftAltMask, VK_LMENU);
        append(RightAltMask, VK_RMENU);
        append(LeftShiftMask, VK_LSHIFT);
        append(RightShiftMask, VK_RSHIFT);
        return keys;
    }
}

TextExpansionController::TextExpansionController(
    std::unique_ptr<ITextExpansionBackend> backend,
    std::function<bool(uint64_t)> queuePendingActivation) :
    backend(std::move(backend)),
    queuePendingActivation(std::move(queuePendingActivation))
{
}

TextExpansionController::~TextExpansionController()
{
    Stop();
}

bool TextExpansionController::Start(KeyboardManagerInput::InputInterface& input)
{
    if (IsBackendReady())
    {
        return true;
    }

    const bool started = backend && backend->Start();
    backendRecoveryPending.store(
        backend && backend->HasRecoveryKeyState(),
        std::memory_order_release);
    if (started)
    {
        try
        {
            std::scoped_lock lock(pressStateMutex);
            inputState = &input;
            arming.store(HasPressedActionKey(), std::memory_order_release);
            armingReleaseObserved.store(false, std::memory_order_release);
            UpdateTrackedPressStateLocked();
        }
        catch (...)
        {
            backend->Stop();
            backendRecoveryPending.store(backend->HasRecoveryKeyState(), std::memory_order_release);
            backendReady.store(false, std::memory_order_release);
            return false;
        }
    }
    backendReady.store(started, std::memory_order_release);
    return started;
}

void TextExpansionController::Stop() noexcept
{
    backendReady.store(false, std::memory_order_release);
    pendingActivationGeneration.store(0, std::memory_order_release);
    cleanupMessageQueued.store(false, std::memory_order_release);
    if (backend)
    {
        backend->Stop();
        backendRecoveryPending.store(backend->HasRecoveryKeyState(), std::memory_order_release);
    }

    std::scoped_lock lock(pressStateMutex);
    actionKeyPresses.clear();
    recoverySuppressedKeys.clear();
    pendingReplayKeys.clear();
    higherPriorityModifierKeys.clear();
    pendingActivationRelease.reset();
    inputState = nullptr;
    arming.store(false, std::memory_order_release);
    armingReleaseObserved.store(false, std::memory_order_release);
    hasTrackedPressState.store(false, std::memory_order_release);
}

TextExpansionController::EventDisposition TextExpansionController::BeginKeyboardEvent(
    LowlevelKeyboardEvent* data) noexcept
{
    if (!data || !data->lParam ||
        (data->lParam->dwExtraInfo & CommonSharedConstants::KEYBOARDMANAGER_INJECTED_FLAG) != 0)
    {
        return EventDisposition::Ignore;
    }

    const bool keyDown = IsKeyDown(data->wParam);
    const bool keyUp = IsKeyUp(data->wParam);
    if (!keyDown && !keyUp)
    {
        return EventDisposition::Ignore;
    }

    const bool backendReadySnapshot = backendReady.load(std::memory_order_acquire);
    const bool trackedPressState = hasTrackedPressState.load(std::memory_order_acquire);
    const bool recoveryPending = backendRecoveryPending.load(std::memory_order_acquire);
    if (!backendReadySnapshot && !trackedPressState && !recoveryPending)
    {
        return EventDisposition::Ignore;
    }

    const DWORD physicalKey = data->lParam->vkCode;
    const size_t physicalKeyIdentity =
        Helpers::GetPhysicalKeyEventIndex(data).value_or(static_cast<size_t>(physicalKey));
    if (data->lParam->dwExtraInfo == KeyboardManagerConstants::KEYBOARDMANAGER_TEXT_EXPANSION_REPLAY_FLAG)
    {
        std::scoped_lock lock(pressStateMutex);
        pendingReplayKeys.erase(physicalKeyIdentity);
        UpdateTrackedPressStateLocked();
    }
    if (recoveryPending && backend && backend->HandleRecoveryKeyEvent(data))
    {
        backendRecoveryPending.store(backend->HasRecoveryKeyState(), std::memory_order_release);
        if (keyUp)
        {
            std::scoped_lock lock(pressStateMutex);
            actionKeyPresses.erase(physicalKeyIdentity);
            recoverySuppressedKeys.erase(physicalKeyIdentity);
            higherPriorityModifierKeys.erase(physicalKey);
            UpdateTrackedPressStateLocked();
        }
        return EventDisposition::ForcePassThrough;
    }

    bool backendIsReady = IsBackendReady();
    if (!backendIsReady && !hasTrackedPressState.load(std::memory_order_acquire))
    {
        return EventDisposition::Ignore;
    }

    const auto interruption = keyDown ?
                                  InterruptPendingActivationForNewInput(
                                      physicalKey,
                                      physicalKeyIdentity,
                                      data->lParam->scanCode,
                                      (data->lParam->flags & LLKHF_EXTENDED) != 0) :
                                  PendingActivationInterruption::None;
    if (interruption == PendingActivationInterruption::Suppress ||
        interruption == PendingActivationInterruption::Replayed)
    {
        return EventDisposition::Suppress;
    }

    if (HandlePendingActivationReleaseEvent(physicalKey, physicalKeyIdentity, keyDown, keyUp))
    {
        return EventDisposition::Suppress;
    }

    {
        std::scoped_lock lock(pressStateMutex);
        if (const auto suppressed = recoverySuppressedKeys.find(physicalKeyIdentity);
            suppressed != recoverySuppressedKeys.end())
        {
            if (keyUp)
            {
                recoverySuppressedKeys.erase(suppressed);
                UpdateTrackedPressStateLocked();
            }
            // Once recovery has faulted, no delayed synthetic key-up remains. Let the
            // current physical press finish so its real key-up can restore key state.
            return backendIsReady ? EventDisposition::Suppress : EventDisposition::Continue;
        }
    }

    const bool blockNewInput = backendIsReady && backend->ShouldBlockNewInput();

    if (Helpers::IsModifierKey(Helpers::ClearKeyNumpadOrigin(physicalKey)))
    {
        if (blockNewInput && keyDown)
        {
            {
                std::scoped_lock lock(pressStateMutex);
                recoverySuppressedKeys.insert(physicalKeyIdentity);
                UpdateTrackedPressStateLocked();
            }
            QueueBackendWork(0);
            return EventDisposition::Suppress;
        }

        std::scoped_lock lock(pressStateMutex);
        if (keyUp)
        {
            higherPriorityModifierKeys.erase(physicalKey);
            UpdateTrackedPressStateLocked();
        }
        // Modifier events not owned by a pending activation/recovery press continue
        // through the normal remap pipeline.
        return EventDisposition::Continue;
    }

    bool suppressPassthroughRepeat = false;
    {
        std::scoped_lock lock(pressStateMutex);
        const auto activePress = actionKeyPresses.find(physicalKeyIdentity);
        if (activePress != actionKeyPresses.end())
        {
            const bool suppress = activePress->second == ActionKeyPressDisposition::Suppressed;
            if (keyUp)
            {
                actionKeyPresses.erase(activePress);
                UpdateTrackedPressStateLocked();
            }
            else if (!suppress && blockNewInput)
            {
                suppressPassthroughRepeat = true;
            }
            if (!suppressPassthroughRepeat)
            {
                return suppress ? EventDisposition::Suppress : EventDisposition::Continue;
            }
        }
    }

    if (ShouldForceArmingEvent(physicalKey, keyDown))
    {
        return EventDisposition::ForcePassThrough;
    }
    if (suppressPassthroughRepeat)
    {
        QueueBackendWork(0);
        return EventDisposition::Suppress;
    }

    if (keyUp)
    {
        return EventDisposition::Continue;
    }

    if (!backendIsReady)
    {
        return EventDisposition::Ignore;
    }

    if (blockNewInput)
    {
        {
            std::scoped_lock lock(pressStateMutex);
            recoverySuppressedKeys.insert(physicalKeyIdentity);
            UpdateTrackedPressStateLocked();
        }
        QueueBackendWork(0);
        return EventDisposition::Suppress;
    }

    // Record every physical non-modifier press, even if an existing remap later
    // consumes its first down. A repeat can therefore never become a fresh Text
    // Expansion activation after a settings or modifier-state change.
    {
        std::scoped_lock lock(pressStateMutex);
        actionKeyPresses.emplace(physicalKeyIdentity, ActionKeyPressDisposition::Passthrough);
        UpdateTrackedPressStateLocked();
    }
    return EventDisposition::FreshActionKeyDown;
}

void TextExpansionController::NotifyHigherPriorityEventHandled(LowlevelKeyboardEvent* data) noexcept
{
    if (!IsBackendReady() || !data || !data->lParam || !IsKeyDown(data->wParam) ||
        (data->lParam->dwExtraInfo & CommonSharedConstants::KEYBOARDMANAGER_INJECTED_FLAG) != 0)
    {
        return;
    }

    ResetBuffer();

    const DWORD physicalKey = data->lParam->vkCode;
    if (Helpers::IsModifierKey(Helpers::ClearKeyNumpadOrigin(physicalKey)))
    {
        std::scoped_lock lock(pressStateMutex);
        higherPriorityModifierKeys.insert(physicalKey);
        UpdateTrackedPressStateLocked();
    }
}

bool TextExpansionController::SetTextExpansions(const TextExpansionTable& rules) noexcept
{
    try
    {
        textExpansionIndex.store(
            std::make_shared<const TextExpansionIndex>(rules),
            std::memory_order_release);
        return true;
    }
    catch (...)
    {
        // Never keep using an index for a configuration that failed to rebuild.
        textExpansionIndex.store(nullptr, std::memory_order_release);
        return false;
    }
}

bool TextExpansionController::HasConfiguredTextExpansions() const noexcept
{
    const auto index = textExpansionIndex.load(std::memory_order_acquire);
    return index && !index->Empty();
}

void TextExpansionController::NotifyAloneRemapEventHandled(
    LowlevelKeyboardEvent* data,
    const bool wasPending) noexcept
{
    if (!data || !data->lParam ||
        (data->lParam->dwExtraInfo & CommonSharedConstants::KEYBOARDMANAGER_INJECTED_FLAG) != 0)
    {
        return;
    }

    const DWORD physicalKey = data->lParam->vkCode;
    const bool sourceIsModifier =
        Helpers::IsModifierKey(Helpers::ClearKeyNumpadOrigin(physicalKey));
    if (IsKeyDown(data->wParam))
    {
        if (!sourceIsModifier)
        {
            NotifyHigherPriorityEventHandled(data);
        }
        return;
    }

    if (wasPending && IsKeyUp(data->wParam))
    {
        // A completed Alone tap injected a different action. A modifier source is
        // intentionally ignored on key-down because it can still become the original
        // modifier in a Text Expansion activation chord.
        ResetBuffer();
    }
}

void TextExpansionController::TrackKeyboardEvent(LowlevelKeyboardEvent* data) noexcept
{
    if (!IsBackendReady())
    {
        return;
    }

    try
    {
        backend->TrackKeyboardEvent(data);
    }
    catch (...)
    {
        backend->ResetBuffer();
    }
}

void TextExpansionController::ResetBuffer() noexcept
{
    if (IsBackendReady())
    {
        backend->ResetBuffer();
    }
}

intptr_t TextExpansionController::TryActivate(
    KeyboardManagerInput::InputInterface& input,
    LowlevelKeyboardEvent* data) noexcept
{
    if (!IsBackendReady() || !data || !data->lParam)
    {
        return 0;
    }

    const DWORD physicalKey = data->lParam->vkCode;
    const size_t physicalKeyIdentity =
        Helpers::GetPhysicalKeyEventIndex(data).value_or(static_cast<size_t>(physicalKey));
    const DWORD actionKey = Helpers::ClearKeyNumpadOrigin(physicalKey);
    const auto index = textExpansionIndex.load(std::memory_order_acquire);
    const uint8_t modifierMask = GetPressedModifierMask(input);
    if (!index || !index->HasActivation(actionKey, modifierMask))
    {
        return 0;
    }

    {
        std::scoped_lock lock(pressStateMutex);
        if (!higherPriorityModifierKeys.empty() || arming.load(std::memory_order_acquire))
        {
            return 0;
        }
    }
    // A source key may still be physically held after its character was delivered.
    // Its release is not new text input; a later distinct key-down interrupts this
    // prepared activation before that newer input is dispatched.

    if (!IsBackendReady())
    {
        return 0;
    }

    TextExpansionResult result = TextExpansionResult::FailedUnchanged;
    const auto pressedActivationModifiers = GetPressedActivationModifiers(modifierMask);
    try
    {
        TextExpansionRequest request{
            .index = index,
            .actionKey = actionKey,
            .modifierMask = modifierMask,
            .activationModifierKeys = pressedActivationModifiers,
        };
        result = backend->PrepareActivation(request);
    }
    catch (...)
    {
        result = TextExpansionResult::FailedChangedOrUnknown;
    }

    const bool prepared = result == TextExpansionResult::Prepared;
    bool suppress = result == TextExpansionResult::FailedChangedOrUnknown;
    if (prepared)
    {
        uint64_t generation = nextActivationGeneration.fetch_add(1, std::memory_order_acq_rel) + 1;
        if (generation == 0)
        {
            generation = nextActivationGeneration.fetch_add(1, std::memory_order_acq_rel) + 1;
        }
        pendingActivationGeneration.store(generation, std::memory_order_release);
        {
            const std::unordered_set<DWORD> modifierKeys(
                pressedActivationModifiers.begin(),
                pressedActivationModifiers.end());
            std::scoped_lock lock(pressStateMutex);
            pendingActivationRelease = PendingActivationRelease{
                .generation = generation,
                .physicalActionKey = physicalKey,
                .physicalActionScanCode = data->lParam->scanCode,
                .physicalActionExtended = (data->lParam->flags & LLKHF_EXTENDED) != 0,
                .physicalActionKeyIdentity = physicalKeyIdentity,
                .actionReleased = false,
                .activationModifierKeys = modifierKeys,
                .pressedActivationModifierKeys = modifierKeys,
            };
            UpdateTrackedPressStateLocked();
        }
        suppress = true;
    }

    if (suppress)
    {
        std::scoped_lock lock(pressStateMutex);
        if (const auto activePress = actionKeyPresses.find(physicalKeyIdentity); activePress != actionKeyPresses.end())
        {
            activePress->second = ActionKeyPressDisposition::Suppressed;
        }
    }

    if (pendingActivationGeneration.load(std::memory_order_acquire) == 0 &&
        backend->HasPendingWork())
    {
        QueueBackendWork(0);
    }

    return suppress ? 1 : 0;
}

TextExpansionResult TextExpansionController::CompletePendingActivation(const uint64_t generation) noexcept
{
    if (!IsBackendReady())
    {
        return TextExpansionResult::FailedUnchanged;
    }

    try
    {
        if (generation == 0)
        {
            cleanupMessageQueued.store(false, std::memory_order_release);
            RetryPendingBackendWork();
            return TextExpansionResult::FailedUnchanged;
        }

        {
            std::scoped_lock lock(pressStateMutex);
            if (pendingActivationRelease && pendingActivationRelease->generation == generation)
            {
                const bool callbackOwnsReadyCommit = pendingActivationRelease->commitQueued &&
                                                     pendingActivationRelease->actionReleased &&
                                                     pendingActivationRelease->pressedActivationModifierKeys.empty() &&
                                                     pendingActivationRelease->suppressedNewModifierKeys.empty();
                if (!callbackOwnsReadyCommit)
                {
                    // This queued callback raced with a new physical press. Let that
                    // press finish and post the same generation again.
                    pendingActivationRelease->commitQueued = false;
                    return TextExpansionResult::FailedUnchanged;
                }
                pendingActivationRelease.reset();
                UpdateTrackedPressStateLocked();
            }
        }

        uint64_t expectedGeneration = generation;
        if (!pendingActivationGeneration.compare_exchange_strong(
                expectedGeneration,
                0,
                std::memory_order_acq_rel))
        {
            return TextExpansionResult::FailedUnchanged;
        }

        const auto result = backend->CompletePendingActivation();
        RetryPendingBackendWork();
        return result;
    }
    catch (...)
    {
        return TextExpansionResult::FailedChangedOrUnknown;
    }
}

bool TextExpansionController::QueueBackendWork(const uint64_t generation) noexcept
{
    if (generation == 0 && pendingActivationGeneration.load(std::memory_order_acquire) != 0)
    {
        // The activation is either waiting for full key release or already queued. Do
        // not use a recovery message to bypass that release gate.
        return true;
    }

    if (generation == 0 && cleanupMessageQueued.exchange(true, std::memory_order_acq_rel))
    {
        return true;
    }

    try
    {
        if (queuePendingActivation && queuePendingActivation(generation))
        {
            return true;
        }
    }
    catch (...)
    {
    }

    if (generation == 0)
    {
        cleanupMessageQueued.store(false, std::memory_order_release);
    }
    return false;
}

TextExpansionController::PendingActivationInterruption TextExpansionController::InterruptPendingActivationForNewInput(
    const DWORD physicalKey,
    const size_t physicalKeyIdentity,
    const DWORD scanCode,
    const bool extended) noexcept
{
    TextExpansionRecoveryRequest recovery;
    bool currentPressWasAlreadyTracked = false;
    try
    {
        std::scoped_lock lock(pressStateMutex);
        if (!pendingActivationRelease)
        {
            return PendingActivationInterruption::None;
        }

        const auto& pending = *pendingActivationRelease;
        if (physicalKeyIdentity == pending.physicalActionKeyIdentity && !pending.actionReleased)
        {
            // While the original action is still down this is only auto-repeat. Once
            // its up was observed, the same identity denotes a new physical press and
            // must interrupt/replay like any other newer input.
            return PendingActivationInterruption::None;
        }
        if (Helpers::IsModifierKey(Helpers::ClearKeyNumpadOrigin(physicalKey)) &&
            pending.pressedActivationModifierKeys.contains(physicalKey))
        {
            // Repeats of an original activation modifier remain owned by the pending
            // press. A genuinely new or re-pressed modifier cancels first, then its
            // physical down continues through the normal remap pipeline.
            return PendingActivationInterruption::None;
        }

        recovery.actionKey = Helpers::ClearKeyNumpadOrigin(pending.physicalActionKey);
        recovery.actionScanCode = pending.physicalActionScanCode;
        recovery.actionExtended = pending.physicalActionExtended;
        recovery.replayKey = Helpers::ClearKeyNumpadOrigin(physicalKey);
        recovery.replayScanCode = scanCode;
        recovery.replayExtended = extended;
        recovery.releasedActivationModifierKeys.reserve(pending.activationModifierKeys.size());
        for (const DWORD key : pending.activationModifierKeys)
        {
            if (!pending.pressedActivationModifierKeys.contains(key))
            {
                recovery.releasedActivationModifierKeys.push_back(key);
            }
        }

        if (!pending.suppressedNewModifierKeys.empty())
        {
            // This state can remain only after an earlier failed interruption. Raw
            // replay would bypass the modifier's normal remap and can leave it stuck.
            return PendingActivationInterruption::None;
        }
        currentPressWasAlreadyTracked = actionKeyPresses.contains(physicalKeyIdentity);

        uint64_t expectedGeneration = pendingActivationRelease->generation;
        if (!pendingActivationGeneration.compare_exchange_strong(
                expectedGeneration,
                0,
                std::memory_order_acq_rel))
        {
            // Completion already owns the prepared transaction. Keep blocking new
            // input until it finishes rather than interleaving a physical key with
            // replacement injection.
            return PendingActivationInterruption::None;
        }

        pendingActivationRelease.reset();
        pendingReplayKeys.insert(physicalKeyIdentity);
        UpdateTrackedPressStateLocked();
    }
    catch (...)
    {
        return PendingActivationInterruption::None;
    }

    const bool recovered = backend && backend->RecoverPendingActivation(recovery);
    if (recovered)
    {
        return PendingActivationInterruption::Replayed;
    }

    {
        std::scoped_lock lock(pressStateMutex);
        pendingReplayKeys.erase(physicalKeyIdentity);
        if (!currentPressWasAlreadyTracked)
        {
            recoverySuppressedKeys.insert(physicalKeyIdentity);
        }
        UpdateTrackedPressStateLocked();
    }
    if (backend && backend->HasPendingWork())
    {
        QueueBackendWork(0);
    }
    return PendingActivationInterruption::Suppress;
}

bool TextExpansionController::HandlePendingActivationReleaseEvent(
    const DWORD physicalKey,
    const size_t physicalKeyIdentity,
    const bool keyDown,
    const bool keyUp) noexcept
{
    uint64_t generationToQueue = 0;
    bool suppress = false;
    {
        std::scoped_lock lock(pressStateMutex);
        if (!pendingActivationRelease)
        {
            return false;
        }

        auto& pending = *pendingActivationRelease;
        if (physicalKeyIdentity == pending.physicalActionKeyIdentity)
        {
            suppress = true;
            if (keyDown)
            {
                pending.actionReleased = false;
                actionKeyPresses[physicalKeyIdentity] = ActionKeyPressDisposition::Suppressed;
            }
            else if (keyUp)
            {
                pending.actionReleased = true;
                actionKeyPresses.erase(physicalKeyIdentity);
            }
        }

        const bool isModifier = Helpers::IsModifierKey(Helpers::ClearKeyNumpadOrigin(physicalKey));
        if (pending.pressedActivationModifierKeys.contains(physicalKey))
        {
            // The original modifier down was already delivered before Prepare. Swallow
            // its repeats and physical up; Complete will inject a dummy plus its key-up.
            suppress = true;
            if (keyUp)
            {
                pending.pressedActivationModifierKeys.erase(physicalKey);
            }
        }
        else if (isModifier && keyDown)
        {
            // Any modifier pressed after Prepare (including the opposite side or a
            // repress of an original side) is recovery input, not part of activation.
            pending.suppressedNewModifierKeys.insert(physicalKey);
            recoverySuppressedKeys.insert(physicalKeyIdentity);
            suppress = true;
        }
        else if (isModifier && keyUp && pending.suppressedNewModifierKeys.contains(physicalKey))
        {
            pending.suppressedNewModifierKeys.erase(physicalKey);
            recoverySuppressedKeys.erase(physicalKeyIdentity);
            suppress = true;
        }
        else if (keyUp && pending.activationModifierKeys.contains(physicalKey))
        {
            // Do not leak duplicate original-side key-up events before the committed
            // synthetic release.
            suppress = true;
        }

        if (pending.actionReleased && pending.pressedActivationModifierKeys.empty() &&
            pending.suppressedNewModifierKeys.empty() && !pending.commitQueued)
        {
            generationToQueue = pending.generation;
            pending.commitQueued = true;
        }
    }

    if (generationToQueue == 0 || QueueBackendWork(generationToQueue))
    {
        return suppress;
    }

    uint64_t expectedGeneration = generationToQueue;
    if (!pendingActivationGeneration.compare_exchange_strong(
            expectedGeneration,
            0,
            std::memory_order_acq_rel))
    {
        return suppress;
    }

    {
        std::scoped_lock lock(pressStateMutex);
        if (pendingActivationRelease && pendingActivationRelease->generation == generationToQueue)
        {
            pendingActivationRelease.reset();
            UpdateTrackedPressStateLocked();
        }
    }

    backend->CancelPendingActivation();
    if (backend->HasPendingWork())
    {
        QueueBackendWork(0);
    }
    return suppress;
}

bool TextExpansionController::HasPendingWork() const noexcept
{
    if (arming.load(std::memory_order_acquire) &&
        armingReleaseObserved.load(std::memory_order_acquire) &&
        !HasPressedActionKey())
    {
        arming.store(false, std::memory_order_release);
        armingReleaseObserved.store(false, std::memory_order_release);
    }

    if (!hasTrackedPressState.load(std::memory_order_acquire) &&
        !backendReady.load(std::memory_order_acquire))
    {
        return false;
    }

    {
        std::scoped_lock lock(pressStateMutex);
        if (!actionKeyPresses.empty() || !recoverySuppressedKeys.empty() || !pendingReplayKeys.empty() ||
            !higherPriorityModifierKeys.empty() || arming.load(std::memory_order_acquire) ||
            pendingActivationRelease.has_value())
        {
            return true;
        }
    }

    const bool backendPending = backendReady.load(std::memory_order_acquire) &&
                                backend && backend->HasPendingWork();
    if (!backendPending)
    {
        hasTrackedPressState.store(false, std::memory_order_release);
    }
    return backendPending;
}

bool TextExpansionController::HasPendingBackendWork() const noexcept
{
    return backendReady.load(std::memory_order_acquire) && backend && backend->HasPendingWork();
}

void TextExpansionController::RetryPendingBackendWork() noexcept
{
    if (backendReady.load(std::memory_order_acquire) && backend)
    {
        backend->RetryPendingCleanup();
        backendRecoveryPending.store(backend->HasRecoveryKeyState(), std::memory_order_release);
        if (!backend->IsReady())
        {
            backendReady.store(false, std::memory_order_release);
        }
    }
}

bool TextExpansionController::IsBackendReady() noexcept
{
    if (!backendReady.load(std::memory_order_acquire) || !backend)
    {
        return false;
    }

    if (!backend->IsReady())
    {
        backendRecoveryPending.store(backend->HasRecoveryKeyState(), std::memory_order_release);
        backendReady.store(false, std::memory_order_release);
        return false;
    }
    return true;
}

bool TextExpansionController::HasPressedActionKey() const noexcept
{
    if (!inputState)
    {
        return false;
    }

    try
    {
        for (DWORD key = 1; key <= 0xFF; ++key)
        {
            if (key == VK_LBUTTON || key == VK_RBUTTON || key == VK_MBUTTON ||
                key == VK_XBUTTON1 || key == VK_XBUTTON2)
            {
                continue;
            }
            if (!Helpers::IsModifierKey(key) && inputState->GetVirtualKeyState(static_cast<int>(key)))
            {
                return true;
            }
        }
    }
    catch (...)
    {
        return true;
    }
    return false;
}

bool TextExpansionController::ShouldForceArmingEvent(
    const DWORD physicalKey,
    const bool keyDown) noexcept
{
    if (!arming.load(std::memory_order_acquire) ||
        Helpers::IsModifierKey(Helpers::ClearKeyNumpadOrigin(physicalKey)))
    {
        return false;
    }

    if (!keyDown || HasPressedActionKey())
    {
        if (!keyDown)
        {
            armingReleaseObserved.store(true, std::memory_order_release);
            if (!HasPressedActionKey())
            {
                arming.store(false, std::memory_order_release);
                armingReleaseObserved.store(false, std::memory_order_release);
                std::scoped_lock lock(pressStateMutex);
                UpdateTrackedPressStateLocked();
            }
        }
        return true;
    }

    arming.store(false, std::memory_order_release);
    armingReleaseObserved.store(false, std::memory_order_release);
    std::scoped_lock lock(pressStateMutex);
    UpdateTrackedPressStateLocked();
    return false;
}

void TextExpansionController::UpdateTrackedPressStateLocked() noexcept
{
    hasTrackedPressState.store(
        !actionKeyPresses.empty() || !recoverySuppressedKeys.empty() || !pendingReplayKeys.empty() ||
            !higherPriorityModifierKeys.empty() || arming.load(std::memory_order_acquire) ||
            pendingActivationRelease.has_value(),
        std::memory_order_release);
}
