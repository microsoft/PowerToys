#pragma once

#include <atomic>
#include <bitset>
#include <cstdint>
#include <functional>
#include <mutex>
#include <optional>
#include <string>
#include <vector>

#include <common/hooks/LowlevelKeyboardEvent.h>
#include <keyboardmanager/common/InputInterface.h>

#include "ITextExpansionBackend.h"

class BufferTextExpansionBackend final : public ITextExpansionBackend
{
public:
    struct InputContext
    {
        HWND foregroundWindow = nullptr;
        HWND focusedWindow = nullptr;
        DWORD processId = 0;

        bool IsValid() const noexcept
        {
            return foregroundWindow != nullptr && focusedWindow != nullptr && processId != 0;
        }

        bool operator==(const InputContext&) const = default;
    };

    enum class TextEventKind : uint8_t
    {
        None,
        DeadKey,
        Text,
    };

    struct TextEvent
    {
        TextEventKind kind = TextEventKind::None;
        std::wstring text;
    };

    using TextProvider = std::function<TextEvent(
        KeyboardManagerInput::InputInterface& input,
        const LowlevelKeyboardEvent* event,
        bool capsLockOn)>;
    using ContextProvider = std::function<InputContext()>;

    explicit BufferTextExpansionBackend(
        KeyboardManagerInput::InputInterface& input,
        TextProvider textProvider = {},
        ContextProvider contextProvider = {});

    bool Start() override;
    void Stop() noexcept override;
    bool IsReady() const noexcept override;
    bool HasRecoveryKeyState() const noexcept override;
    bool HandleRecoveryKeyEvent(const LowlevelKeyboardEvent* data) noexcept override;
    void TrackKeyboardEvent(const LowlevelKeyboardEvent* data) noexcept override;
    void ResetBuffer() noexcept override;
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
        size_t backspaceCount = 0;
        std::wstring replacementText;
        InputContext targetContext;
        uint64_t contextEpoch = 0;
    };

    InputContext GetCurrentContext() const noexcept;
    bool IsTargetContextCurrent(const InputContext& expected, uint64_t expectedEpoch) const noexcept;
    void ResetBufferLocked() noexcept;
    void QueuePendingCleanup(std::vector<INPUT> cleanup);
    bool ReleaseCapturedModifiers(const std::vector<DWORD>& modifierKeys) noexcept;

    KeyboardManagerInput::InputInterface& input;
    TextProvider textProvider;
    ContextProvider contextProvider;
    std::atomic_bool started = false;
    std::atomic_bool activationInProgress = false;

    mutable std::mutex bufferMutex;
    std::wstring buffer;
    InputContext bufferContext;
    std::atomic_uint64_t contextEpoch = 0;
    wchar_t pendingPacketHighSurrogate = L'\0';
    bool capsLockOn = false;
    bool capsLockPressed = false;

    mutable std::mutex activationMutex;
    std::optional<PendingActivation> pendingActivation;

    mutable std::mutex pendingCleanupMutex;
    std::vector<INPUT> pendingCleanup;
    size_t cleanupAttemptsWithoutProgress = 0;
    std::bitset<512> abandonedKeyUps;

    static constexpr size_t MaximumCleanupAttemptsWithoutProgress = 8;
};
