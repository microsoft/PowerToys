#include "pch.h"
#include "TextExpansionController.h"

#include <utility>

#include <common/interop/shared_constants.h>
#include <keyboardmanager/common/Helpers.h>

namespace
{
    constexpr bool IsKeyDown(const WPARAM message) noexcept
    {
        return message == WM_KEYDOWN || message == WM_SYSKEYDOWN;
    }

    constexpr bool IsKeyUp(const WPARAM message) noexcept
    {
        return message == WM_KEYUP || message == WM_SYSKEYUP;
    }

    bool ModifierStateMatches(
        KeyboardManagerInput::InputInterface& input,
        const ModifierKey expected,
        const int leftKey,
        const int rightKey) noexcept
    {
        const bool leftDown = input.GetVirtualKeyState(leftKey);
        const bool rightDown = input.GetVirtualKeyState(rightKey);

        switch (expected)
        {
        case ModifierKey::Disabled:
            return !leftDown && !rightDown;
        case ModifierKey::Left:
            return leftDown && !rightDown;
        case ModifierKey::Right:
            return !leftDown && rightDown;
        case ModifierKey::Both:
            // "Both" is the side-agnostic representation used by shortcut capture.
            return leftDown || rightDown;
        default:
            return false;
        }
    }

    std::vector<DWORD> GetPressedActivationModifiers(
        KeyboardManagerInput::InputInterface& input,
        const Shortcut& activation)
    {
        std::vector<DWORD> keys;
        const auto append = [&](const ModifierKey expected, const DWORD left, const DWORD right) {
            if ((expected == ModifierKey::Left || expected == ModifierKey::Both) && input.GetVirtualKeyState(left))
            {
                keys.push_back(left);
            }
            if ((expected == ModifierKey::Right || expected == ModifierKey::Both) && input.GetVirtualKeyState(right))
            {
                keys.push_back(right);
            }
        };

        append(activation.winKey, VK_LWIN, VK_RWIN);
        append(activation.ctrlKey, VK_LCONTROL, VK_RCONTROL);
        append(activation.altKey, VK_LMENU, VK_RMENU);
        append(activation.shiftKey, VK_LSHIFT, VK_RSHIFT);
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

bool TextExpansionController::Start()
{
    if (backendReady.load(std::memory_order_acquire))
    {
        return true;
    }

    const bool started = backend && backend->Start();
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
    }

    std::scoped_lock lock(pressStateMutex);
    actionKeyPresses.clear();
    recoverySuppressedKeys.clear();
    higherPriorityModifierKeys.clear();
    pendingActivationRelease.reset();
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

    const DWORD physicalKey = data->lParam->vkCode;
    if (HandlePendingActivationReleaseEvent(physicalKey, keyDown, keyUp))
    {
        return EventDisposition::Suppress;
    }

    {
        std::scoped_lock lock(pressStateMutex);
        if (const auto suppressed = recoverySuppressedKeys.find(physicalKey);
            suppressed != recoverySuppressedKeys.end())
        {
            if (keyUp)
            {
                recoverySuppressedKeys.erase(suppressed);
            }
            return EventDisposition::Suppress;
        }
    }

    if (Helpers::IsModifierKey(Helpers::ClearKeyNumpadOrigin(physicalKey)))
    {
        std::scoped_lock lock(pressStateMutex);
        if (keyUp)
        {
            higherPriorityModifierKeys.erase(physicalKey);
        }
        // Modifier events not owned by a pending activation/recovery press continue
        // through the normal remap pipeline.
        return EventDisposition::Continue;
    }

    {
        std::scoped_lock lock(pressStateMutex);
        const auto activePress = actionKeyPresses.find(physicalKey);
        if (activePress != actionKeyPresses.end())
        {
            const bool suppress = activePress->second == ActionKeyPressDisposition::Suppressed;
            if (keyUp)
            {
                actionKeyPresses.erase(activePress);
            }
            return suppress ? EventDisposition::Suppress : EventDisposition::Continue;
        }
    }

    if (keyUp)
    {
        return EventDisposition::Continue;
    }

    if (backend && backend->ShouldBlockNewInput())
    {
        std::scoped_lock lock(pressStateMutex);
        recoverySuppressedKeys.insert(physicalKey);
        QueueBackendWork(0);
        return EventDisposition::Suppress;
    }

    // Record every physical non-modifier press, even if an existing remap later
    // consumes its first down. A repeat can therefore never become a fresh Text
    // Expansion activation after a settings or modifier-state change.
    {
        std::scoped_lock lock(pressStateMutex);
        actionKeyPresses.emplace(physicalKey, ActionKeyPressDisposition::Passthrough);
    }
    return EventDisposition::FreshActionKeyDown;
}

void TextExpansionController::NotifyHigherPriorityEventHandled(LowlevelKeyboardEvent* data) noexcept
{
    if (!data || !data->lParam || !IsKeyDown(data->wParam) ||
        (data->lParam->dwExtraInfo & CommonSharedConstants::KEYBOARDMANAGER_INJECTED_FLAG) != 0)
    {
        return;
    }

    const DWORD physicalKey = data->lParam->vkCode;
    if (Helpers::IsModifierKey(Helpers::ClearKeyNumpadOrigin(physicalKey)))
    {
        std::scoped_lock lock(pressStateMutex);
        higherPriorityModifierKeys.insert(physicalKey);
    }
}

intptr_t TextExpansionController::TryActivate(
    KeyboardManagerInput::InputInterface& input,
    const DWORD physicalKey,
    const TextExpansionTable& rules) noexcept
{
    const DWORD actionKey = Helpers::ClearKeyNumpadOrigin(physicalKey);

    {
        std::scoped_lock lock(pressStateMutex);
        if (!higherPriorityModifierKeys.empty())
        {
            return 0;
        }
    }

    bool configuredActionKey = false;
    std::optional<Shortcut> matchedActivation;
    std::vector<TextExpansionCandidate> candidates;
    candidates.reserve(rules.size());
    for (size_t index = 0; index < rules.size(); ++index)
    {
        const auto& rule = rules[index];
        if (!rule.enabled || Helpers::ClearKeyNumpadOrigin(rule.activation.GetActionKey()) != actionKey)
        {
            continue;
        }

        configuredActionKey = true;
        if (ActivationMatches(input, rule.activation, actionKey))
        {
            if (!matchedActivation)
            {
                matchedActivation = rule.activation;
            }
            candidates.push_back({ rule.sourceText, rule.replacementText, index });
        }
    }

    if (!configuredActionKey)
    {
        return 0;
    }

    if (candidates.empty() || !matchedActivation || !backendReady.load(std::memory_order_acquire) || !backend)
    {
        return 0;
    }

    TextExpansionResult result = TextExpansionResult::FailedUnchanged;
    const auto pressedActivationModifiers = GetPressedActivationModifiers(input, *matchedActivation);
    try
    {
        TextExpansionRequest request{
            .activationShortcut = *matchedActivation,
            .activationModifierKeys = pressedActivationModifiers,
            .candidates = std::move(candidates),
            .deadline = std::chrono::steady_clock::now() + std::chrono::milliseconds(125),
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
                .actionReleased = false,
                .activationModifierKeys = modifierKeys,
                .pressedActivationModifierKeys = modifierKeys,
            };
        }
        suppress = true;
    }

    if (suppress)
    {
        std::scoped_lock lock(pressStateMutex);
        if (const auto activePress = actionKeyPresses.find(physicalKey); activePress != actionKeyPresses.end())
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
    if (!backendReady.load(std::memory_order_acquire) || !backend)
    {
        return TextExpansionResult::FailedUnchanged;
    }

    try
    {
        if (generation == 0)
        {
            cleanupMessageQueued.store(false, std::memory_order_release);
            backend->RetryPendingCleanup();
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
        backend->RetryPendingCleanup();
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

bool TextExpansionController::HandlePendingActivationReleaseEvent(
    const DWORD physicalKey,
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
        if (physicalKey == pending.physicalActionKey)
        {
            if (keyDown)
            {
                pending.actionReleased = false;
            }
            else if (keyUp)
            {
                pending.actionReleased = true;
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
            recoverySuppressedKeys.insert(physicalKey);
            suppress = true;
        }
        else if (isModifier && keyUp && pending.suppressedNewModifierKeys.contains(physicalKey))
        {
            pending.suppressedNewModifierKeys.erase(physicalKey);
            recoverySuppressedKeys.erase(physicalKey);
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
    {
        std::scoped_lock lock(pressStateMutex);
        if (!actionKeyPresses.empty() || !recoverySuppressedKeys.empty() ||
            !higherPriorityModifierKeys.empty())
        {
            return true;
        }
    }

    return backend && backend->HasPendingWork();
}

bool TextExpansionController::HasPendingBackendWork() const noexcept
{
    return backend && backend->HasPendingWork();
}

void TextExpansionController::RetryPendingBackendWork() noexcept
{
    if (backend)
    {
        backend->RetryPendingCleanup();
    }
}

bool TextExpansionController::ActivationMatches(
    KeyboardManagerInput::InputInterface& input,
    const Shortcut& activation,
    const DWORD physicalActionKey) const noexcept
{
    return !activation.HasChord() &&
           Helpers::ClearKeyNumpadOrigin(activation.GetActionKey()) == physicalActionKey &&
           ModifierStateMatches(input, activation.winKey, VK_LWIN, VK_RWIN) &&
           ModifierStateMatches(input, activation.ctrlKey, VK_LCONTROL, VK_RCONTROL) &&
           ModifierStateMatches(input, activation.altKey, VK_LMENU, VK_RMENU) &&
           ModifierStateMatches(input, activation.shiftKey, VK_LSHIFT, VK_RSHIFT);
}
