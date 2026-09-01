#include "pch.h"
#include "BufferTextExpansionBackend.h"

#include <algorithm>
#include <array>
#include <iterator>
#include <string_view>
#include <utility>

#include <common/interop/shared_constants.h>
#include <keyboardmanager/common/Helpers.h>
#include <keyboardmanager/common/KeyboardManagerConstants.h>

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

    std::optional<size_t> GetInjectedInputIdentity(const INPUT& event) noexcept
    {
        if (event.type != INPUT_KEYBOARD ||
            (event.ki.dwFlags & KEYEVENTF_UNICODE) != 0 ||
            event.ki.wVk == 0 || event.ki.wVk == KeyboardManagerConstants::DUMMY_KEY)
        {
            return std::nullopt;
        }

        const UINT mappedScanCode = MapVirtualKeyW(event.ki.wVk, MAPVK_VK_TO_VSC_EX);
        DWORD scanCode = event.ki.wScan & 0xFF;
        if (scanCode == 0)
        {
            scanCode = mappedScanCode & 0xFF;
        }
        if (scanCode == 0)
        {
            return std::nullopt;
        }

        constexpr size_t keyCount = 256;
        const bool extended = (event.ki.dwFlags & KEYEVENTF_EXTENDEDKEY) != 0 ||
                              (mappedScanCode & 0xFF00) != 0;
        return static_cast<size_t>(scanCode) +
               (extended ? keyCount : 0);
    }

    void RecordAbandonedKeyUps(const std::vector<INPUT>& inputs, std::bitset<512>& keys) noexcept
    {
        for (const auto& event : inputs)
        {
            if (event.type == INPUT_KEYBOARD && (event.ki.dwFlags & KEYEVENTF_KEYUP) != 0)
            {
                if (const auto identity = GetInjectedInputIdentity(event))
                {
                    keys.set(*identity);
                }
            }
        }
    }

    constexpr bool IsHighSurrogate(const wchar_t value) noexcept
    {
        const auto codeUnit = static_cast<uint16_t>(value);
        return codeUnit >= 0xD800 && codeUnit <= 0xDBFF;
    }

    constexpr bool IsLowSurrogate(const wchar_t value) noexcept
    {
        const auto codeUnit = static_cast<uint16_t>(value);
        return codeUnit >= 0xDC00 && codeUnit <= 0xDFFF;
    }

    bool IsValidPrintableUtf16(const std::wstring_view text) noexcept
    {
        if (text.empty())
        {
            return false;
        }

        for (size_t index = 0; index < text.size(); ++index)
        {
            const auto codeUnit = static_cast<uint16_t>(text[index]);
            if (IsHighSurrogate(text[index]))
            {
                if (++index >= text.size() || !IsLowSurrogate(text[index]))
                {
                    return false;
                }
            }
            else if (IsLowSurrogate(text[index]) || codeUnit < 0x20 ||
                     (codeUnit >= 0x7F && codeUnit <= 0x9F))
            {
                return false;
            }
        }

        return true;
    }

    void TrimBuffer(std::wstring& text)
    {
        constexpr size_t maximumLength = KeyboardManagerConstants::MaxTextExpansionSourceLength;
        if (text.size() <= maximumLength)
        {
            return;
        }

        size_t eraseCount = text.size() - maximumLength;
        if (eraseCount < text.size() && IsLowSurrogate(text[eraseCount]))
        {
            ++eraseCount;
        }
        text.erase(0, eraseCount);
    }

    void PopLastScalar(std::wstring& text) noexcept
    {
        if (text.empty())
        {
            return;
        }

        size_t eraseFrom = text.size() - 1;
        if (IsLowSurrogate(text[eraseFrom]) && eraseFrom != 0 && IsHighSurrogate(text[eraseFrom - 1]))
        {
            --eraseFrom;
        }
        text.erase(eraseFrom);
    }

    void SetKeyboardStateKey(BYTE keyState[256], const int key, const bool pressed) noexcept
    {
        if (pressed)
        {
            keyState[key] |= 0x80;
        }
        else
        {
            keyState[key] &= ~0x80;
        }
    }

    void SetKeyboardStateModifier(
        KeyboardManagerInput::InputInterface& input,
        BYTE keyState[256],
        const int genericKey,
        const int leftKey,
        const int rightKey)
    {
        const bool leftPressed = input.GetVirtualKeyState(leftKey);
        const bool rightPressed = input.GetVirtualKeyState(rightKey);
        SetKeyboardStateKey(
            keyState,
            genericKey,
            input.GetVirtualKeyState(genericKey) || leftPressed || rightPressed);
        SetKeyboardStateKey(keyState, leftKey, leftPressed);
        SetKeyboardStateKey(keyState, rightKey, rightPressed);
    }

    void SetKeyboardStateToggle(BYTE keyState[256], const int key, const bool enabled) noexcept
    {
        if (enabled)
        {
            keyState[key] |= 0x01;
        }
        else
        {
            keyState[key] &= ~0x01;
        }
    }

    bool IsModifierPressed(
        KeyboardManagerInput::InputInterface& input,
        const int genericKey,
        const int leftKey,
        const int rightKey)
    {
        return input.GetVirtualKeyState(genericKey) ||
               input.GetVirtualKeyState(leftKey) ||
               input.GetVirtualKeyState(rightKey);
    }

    bool IsAltGrPressed(KeyboardManagerInput::InputInterface& input)
    {
        return input.GetVirtualKeyState(VK_RMENU) &&
               IsModifierPressed(input, VK_CONTROL, VK_LCONTROL, VK_RCONTROL) &&
               !input.GetVirtualKeyState(VK_LMENU);
    }

    bool IsShortcutModifierPressed(KeyboardManagerInput::InputInterface& input)
    {
        const bool winPressed = input.GetVirtualKeyState(VK_LWIN) || input.GetVirtualKeyState(VK_RWIN);
        const bool ctrlOrAltPressed = IsModifierPressed(input, VK_CONTROL, VK_LCONTROL, VK_RCONTROL) ||
                                      IsModifierPressed(input, VK_MENU, VK_LMENU, VK_RMENU);
        return winPressed || (ctrlOrAltPressed && !IsAltGrPressed(input));
    }

    BufferTextExpansionBackend::InputContext GetDefaultInputContext()
    {
        BufferTextExpansionBackend::InputContext context;
        context.foregroundWindow = GetForegroundWindow();
        if (!context.foregroundWindow)
        {
            return context;
        }

        context.focusedWindow = context.foregroundWindow;
        GUITHREADINFO guiThreadInfo{};
        guiThreadInfo.cbSize = sizeof(guiThreadInfo);
        if (GetGUIThreadInfo(0, &guiThreadInfo))
        {
            if (guiThreadInfo.hwndFocus)
            {
                context.focusedWindow = guiThreadInfo.hwndFocus;
            }
            else if (guiThreadInfo.hwndActive)
            {
                context.focusedWindow = guiThreadInfo.hwndActive;
            }
        }

        GetWindowThreadProcessId(context.focusedWindow, &context.processId);
        return context;
    }

    BufferTextExpansionBackend::TextEvent GetDefaultTextEvent(
        KeyboardManagerInput::InputInterface& input,
        const LowlevelKeyboardEvent* data,
        const bool capsLockOn)
    {
        BufferTextExpansionBackend::TextEvent event;
        if (!data || !data->lParam || !IsKeyDown(data->wParam))
        {
            return event;
        }

        const DWORD vkCode = Helpers::ClearKeyNumpadOrigin(data->lParam->vkCode);
        if (vkCode > 0xFF)
        {
            return event;
        }

        std::array<BYTE, 256> keyState{};
        if (!GetKeyboardState(keyState.data()))
        {
            return event;
        }

        SetKeyboardStateModifier(input, keyState.data(), VK_SHIFT, VK_LSHIFT, VK_RSHIFT);
        SetKeyboardStateModifier(input, keyState.data(), VK_CONTROL, VK_LCONTROL, VK_RCONTROL);
        SetKeyboardStateModifier(input, keyState.data(), VK_MENU, VK_LMENU, VK_RMENU);
        // The low-level hook runs before the asynchronous toggle state is updated.
        // Keep an explicit physical-press model instead of trusting the hook thread's
        // potentially stale GetKeyboardState toggle bit.
        SetKeyboardStateToggle(keyState.data(), VK_CAPITAL, capsLockOn);
        keyState[vkCode] |= 0x80;

        const HWND foregroundWindow = GetForegroundWindow();
        const DWORD foregroundThread = foregroundWindow ? GetWindowThreadProcessId(foregroundWindow, nullptr) : 0;
        const HKL layout = GetKeyboardLayout(foregroundThread);
        const UINT scanCode = data->lParam->scanCode ?
                                  data->lParam->scanCode :
                                  MapVirtualKeyExW(vkCode, MAPVK_VK_TO_VSC, layout);
        wchar_t output[8]{};
        constexpr UINT doNotChangeKeyboardState = 1u << 2;
        const int result = ToUnicodeEx(
            vkCode,
            scanCode,
            keyState.data(),
            output,
            static_cast<int>(std::size(output)),
            doNotChangeKeyboardState,
            layout);
        if (result < 0)
        {
            event.kind = BufferTextExpansionBackend::TextEventKind::DeadKey;
            return event;
        }
        if (result == 0)
        {
            return event;
        }

        event.text.assign(output, output + (std::min)(result, static_cast<int>(std::size(output))));
        if (IsValidPrintableUtf16(event.text))
        {
            event.kind = BufferTextExpansionBackend::TextEventKind::Text;
        }
        else
        {
            event.text.clear();
        }
        return event;
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

            if ((event.ki.dwFlags & KEYEVENTF_KEYUP) == 0)
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

    TextExpansionResult SendBackspaces(
        KeyboardManagerInput::InputInterface& input,
        const size_t count,
        bool& inputStreamMutated,
        const std::function<bool()>& isTargetCurrent,
        const std::function<void(std::vector<INPUT>)>& queueCleanup)
    {
        std::vector<INPUT> pair;
        pair.reserve(2);
        for (size_t index = 0; index < count; ++index)
        {
            if (!isTargetCurrent())
            {
                return inputStreamMutated ? TextExpansionResult::FailedChangedOrUnknown :
                                            TextExpansionResult::FailedUnchanged;
            }

            pair.clear();
            Helpers::SetKeyEvent(
                pair,
                INPUT_KEYBOARD,
                VK_BACK,
                0,
                KeyboardManagerConstants::KEYBOARDMANAGER_SHORTCUT_FLAG);
            Helpers::SetKeyEvent(
                pair,
                INPUT_KEYBOARD,
                VK_BACK,
                KEYEVENTF_KEYUP,
                KeyboardManagerConstants::KEYBOARDMANAGER_SHORTCUT_FLAG);

            const auto result = input.SendVirtualInput(pair);
            if (result.status == KeyboardManagerInput::SendVirtualInputStatus::None)
            {
                return inputStreamMutated ? TextExpansionResult::FailedChangedOrUnknown :
                                            TextExpansionResult::FailedUnchanged;
            }
            if (result.injectedEventCount != 0)
            {
                inputStreamMutated = true;
            }
            if (result.status == KeyboardManagerInput::SendVirtualInputStatus::Partial)
            {
                queueCleanup(CreateCleanupForInjectedPrefix(pair, result.injectedEventCount));
                return TextExpansionResult::FailedChangedOrUnknown;
            }
        }
        return TextExpansionResult::Replaced;
    }

    TextExpansionResult SendReplacementText(
        KeyboardManagerInput::InputInterface& input,
        const std::wstring& text,
        bool& inputStreamMutated,
        const std::function<bool()>& isTargetCurrent,
        const std::function<void(std::vector<INPUT>)>& queueCleanup)
    {
        std::vector<INPUT> unit;
        unit.reserve(4);
        for (size_t index = 0; index < text.size(); ++index)
        {
            if (!isTargetCurrent())
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
            Helpers::SetTextInputUnit(
                unit,
                value,
                KeyboardManagerConstants::KEYBOARDMANAGER_SHORTCUT_FLAG);
            if (IsHighSurrogate(value) && index + 1 < text.size() && IsLowSurrogate(text[index + 1]))
            {
                Helpers::SetTextInputUnit(
                    unit,
                    text[++index],
                    KeyboardManagerConstants::KEYBOARDMANAGER_SHORTCUT_FLAG);
            }
            const auto result = input.SendVirtualInput(unit);
            if (result.status == KeyboardManagerInput::SendVirtualInputStatus::None)
            {
                return inputStreamMutated ? TextExpansionResult::FailedChangedOrUnknown :
                                            TextExpansionResult::FailedUnchanged;
            }
            if (result.injectedEventCount != 0)
            {
                inputStreamMutated = true;
            }
            if (result.status == KeyboardManagerInput::SendVirtualInputStatus::Partial)
            {
                queueCleanup(CreateCleanupForInjectedPrefix(unit, result.injectedEventCount));
                return TextExpansionResult::FailedChangedOrUnknown;
            }
        }
        return TextExpansionResult::Replaced;
    }
}

BufferTextExpansionBackend::BufferTextExpansionBackend(
    KeyboardManagerInput::InputInterface& input,
    TextProvider textProvider,
    ContextProvider contextProvider) :
    input(input),
    textProvider(textProvider ? std::move(textProvider) : TextProvider{ GetDefaultTextEvent }),
    contextProvider(contextProvider ? std::move(contextProvider) : ContextProvider{ GetDefaultInputContext })
{
}

bool BufferTextExpansionBackend::Start()
{
    {
        std::scoped_lock lock(pendingCleanupMutex);
        if (!pendingCleanup.empty() || abandonedKeyUps.any())
        {
            return false;
        }
        cleanupAttemptsWithoutProgress = 0;
    }
    {
        std::scoped_lock lock(bufferMutex);
        ResetBufferLocked();
        capsLockOn = (GetKeyState(VK_CAPITAL) & 0x0001) != 0;
        capsLockPressed = false;
    }
    started.store(true, std::memory_order_release);
    return true;
}

void BufferTextExpansionBackend::Stop() noexcept
{
    started.store(false, std::memory_order_release);

    std::vector<DWORD> modifierKeys;
    {
        std::scoped_lock lock(activationMutex);
        if (pendingActivation)
        {
            modifierKeys = pendingActivation->activationModifierKeys;
            pendingActivation.reset();
        }
        activationInProgress.store(false, std::memory_order_release);
    }

    ReleaseCapturedModifiers(modifierKeys);
    // The queue is closed once started is false. Every retry either consumes at
    // least one event or reaches the bounded no-progress fault threshold.
    while (HasPendingWork())
    {
        RetryPendingCleanup();
    }
    {
        std::scoped_lock lock(bufferMutex);
        ResetBufferLocked();
        capsLockPressed = false;
    }
}

bool BufferTextExpansionBackend::IsReady() const noexcept
{
    return started.load(std::memory_order_acquire);
}

bool BufferTextExpansionBackend::HasRecoveryKeyState() const noexcept
{
    std::scoped_lock lock(pendingCleanupMutex);
    return abandonedKeyUps.any();
}

bool BufferTextExpansionBackend::HandleRecoveryKeyEvent(const LowlevelKeyboardEvent* data) noexcept
{
    const auto identity = Helpers::GetPhysicalKeyEventIndex(data);
    if (!identity)
    {
        return false;
    }

    std::scoped_lock lock(pendingCleanupMutex);
    const bool matches = abandonedKeyUps.test(*identity);
    if (matches && IsKeyUp(data->wParam))
    {
        abandonedKeyUps.reset(*identity);
    }
    return matches;
}

void BufferTextExpansionBackend::TrackKeyboardEvent(const LowlevelKeyboardEvent* data) noexcept
{
    if (!started.load(std::memory_order_acquire) || !data || !data->lParam)
    {
        return;
    }

    const bool keyDown = IsKeyDown(data->wParam);
    const bool keyUp = IsKeyUp(data->wParam);
    if (!keyDown && !keyUp)
    {
        return;
    }

    try
    {
        const DWORD vkCode = Helpers::ClearKeyNumpadOrigin(data->lParam->vkCode);
        if (keyUp)
        {
            if (vkCode == VK_CAPITAL)
            {
                std::scoped_lock lock(bufferMutex);
                capsLockPressed = false;
            }
            return;
        }

        const bool injectedByKeyboardManager =
            (data->lParam->dwExtraInfo & CommonSharedConstants::KEYBOARDMANAGER_INJECTED_FLAG) != 0;

        const InputContext currentContext = GetCurrentContext();
        std::scoped_lock lock(bufferMutex);
        if (!currentContext.IsValid())
        {
            ResetBufferLocked();
            return;
        }
        if (currentContext != bufferContext)
        {
            ResetBufferLocked();
            bufferContext = currentContext;
        }

        if (Helpers::IsModifierKey(vkCode))
        {
            return;
        }

        if (vkCode == VK_CAPITAL)
        {
            if (!capsLockPressed)
            {
                capsLockOn = !capsLockOn;
                capsLockPressed = true;
            }
            return;
        }

        if (injectedByKeyboardManager)
        {
            return;
        }

        if (vkCode == VK_NUMLOCK || vkCode == VK_SCROLL)
        {
            return;
        }

        if (vkCode == VK_BACK && (IsShortcutModifierPressed(input) || IsAltGrPressed(input)))
        {
            buffer.clear();
            pendingPacketHighSurrogate = L'\0';
            return;
        }

        if (vkCode == VK_BACK)
        {
            pendingPacketHighSurrogate = L'\0';
            PopLastScalar(buffer);
            return;
        }

        if (IsShortcutModifierPressed(input))
        {
            buffer.clear();
            pendingPacketHighSurrogate = L'\0';
            return;
        }

        if (vkCode == VK_PACKET)
        {
            const wchar_t packetUnit = static_cast<wchar_t>(data->lParam->scanCode & 0xFFFF);
            if (IsHighSurrogate(packetUnit))
            {
                if (pendingPacketHighSurrogate != L'\0')
                {
                    buffer.clear();
                }
                pendingPacketHighSurrogate = packetUnit;
                return;
            }
            if (IsLowSurrogate(packetUnit) && IsHighSurrogate(pendingPacketHighSurrogate))
            {
                buffer.push_back(pendingPacketHighSurrogate);
                buffer.push_back(packetUnit);
                pendingPacketHighSurrogate = L'\0';
                TrimBuffer(buffer);
                return;
            }
            if (pendingPacketHighSurrogate != L'\0' || IsLowSurrogate(packetUnit))
            {
                buffer.clear();
                pendingPacketHighSurrogate = L'\0';
                return;
            }

            const std::wstring packetText(1, packetUnit);
            if (!IsValidPrintableUtf16(packetText))
            {
                buffer.clear();
                return;
            }
            buffer.append(packetText);
            TrimBuffer(buffer);
            return;
        }

        pendingPacketHighSurrogate = L'\0';
        const TextEvent event = textProvider(input, data, capsLockOn);
        if (event.kind == TextEventKind::DeadKey)
        {
            // A dead key changes the keyboard composition state without committing
            // text. Do not allow a suffix collected before it to bridge the composition.
            buffer.clear();
            return;
        }
        if (event.kind != TextEventKind::Text || !IsValidPrintableUtf16(event.text))
        {
            buffer.clear();
            return;
        }

        buffer.append(event.text);
        TrimBuffer(buffer);
    }
    catch (...)
    {
        ResetBuffer();
    }
}

void BufferTextExpansionBackend::ResetBuffer() noexcept
{
    std::scoped_lock lock(bufferMutex);
    ResetBufferLocked();
}

TextExpansionResult BufferTextExpansionBackend::PrepareActivation(const TextExpansionRequest& request)
{
    if (!started.load(std::memory_order_acquire) || !request.index ||
        !request.index->HasActivation(request.actionKey, request.modifierMask))
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
        const InputContext currentContext = GetCurrentContext();
        std::scoped_lock bufferLock(bufferMutex);
        if (!currentContext.IsValid() || currentContext != bufferContext)
        {
            ResetBufferLocked();
            bufferContext = currentContext;
            activationInProgress.store(false, std::memory_order_release);
            return TextExpansionResult::NoMatch;
        }

        const auto selected = request.index->FindLongestMatch(
            request.actionKey,
            request.modifierMask,
            buffer);
        if (!selected)
        {
            activationInProgress.store(false, std::memory_order_release);
            return TextExpansionResult::NoMatch;
        }

        const auto* rule = request.index->GetRule(*selected);
        if (!rule)
        {
            activationInProgress.store(false, std::memory_order_release);
            return TextExpansionResult::FailedUnchanged;
        }
        pendingActivation = PendingActivation{
            .index = request.index,
            .ruleIndex = *selected,
            .activationModifierKeys = request.activationModifierKeys,
            .backspaceCount = rule->backspaceCount,
            .targetContext = currentContext,
            .contextEpoch = contextEpoch.load(std::memory_order_acquire),
        };
        buffer.clear();
        pendingPacketHighSurrogate = L'\0';
        return TextExpansionResult::Prepared;
    }
    catch (...)
    {
        activationInProgress.store(false, std::memory_order_release);
        return TextExpansionResult::FailedUnchanged;
    }
}

TextExpansionResult BufferTextExpansionBackend::CompletePendingActivation() noexcept
{
    std::scoped_lock activationLock(activationMutex);
    if (!pendingActivation || !activationInProgress.load(std::memory_order_acquire) ||
        !started.load(std::memory_order_acquire))
    {
        return TextExpansionResult::FailedUnchanged;
    }

    PendingActivation activation = std::move(*pendingActivation);
    pendingActivation.reset();
    ActivationCompletionGuard completionGuard{ activationInProgress };
    const auto* rule = activation.index ? activation.index->GetRule(activation.ruleIndex) : nullptr;
    if (!rule)
    {
        return TextExpansionResult::FailedUnchanged;
    }
    const auto& modifierKeys = activation.activationModifierKeys;
    bool inputStreamMutated = !modifierKeys.empty();
    bool modifierReleaseAttempted = false;

    try
    {
        if (!IsTargetContextCurrent(activation.targetContext, activation.contextEpoch))
        {
            modifierReleaseAttempted = true;
            ReleaseCapturedModifiers(modifierKeys);
            return modifierKeys.empty() ? TextExpansionResult::FailedUnchanged :
                                          TextExpansionResult::FailedChangedOrUnknown;
        }

        modifierReleaseAttempted = true;
        if (!ReleaseCapturedModifiers(modifierKeys))
        {
            return TextExpansionResult::FailedChangedOrUnknown;
        }
        if (!IsTargetContextCurrent(activation.targetContext, activation.contextEpoch))
        {
            return inputStreamMutated ? TextExpansionResult::FailedChangedOrUnknown :
                                        TextExpansionResult::FailedUnchanged;
        }

        const auto isTargetCurrent = [this, &activation] {
            return IsTargetContextCurrent(activation.targetContext, activation.contextEpoch);
        };
        const auto queueCleanup = [this](std::vector<INPUT> cleanup) {
            QueuePendingCleanup(std::move(cleanup));
        };

        const TextExpansionResult backspaceResult = SendBackspaces(
            input,
            activation.backspaceCount,
            inputStreamMutated,
            isTargetCurrent,
            queueCleanup);
        if (backspaceResult != TextExpansionResult::Replaced)
        {
            return backspaceResult;
        }

        return SendReplacementText(
            input,
            rule->replacementText,
            inputStreamMutated,
            isTargetCurrent,
            queueCleanup);
    }
    catch (...)
    {
        if (!modifierReleaseAttempted)
        {
            ReleaseCapturedModifiers(modifierKeys);
        }
        return inputStreamMutated ? TextExpansionResult::FailedChangedOrUnknown :
                                    TextExpansionResult::FailedUnchanged;
    }
}

bool BufferTextExpansionBackend::RecoverPendingActivation(
    const TextExpansionRecoveryRequest& request) noexcept
{
    std::scoped_lock activationLock(activationMutex);
    if (!pendingActivation || request.actionKey == 0 || request.replayKey == 0)
    {
        return false;
    }

    const bool targetStillCurrent = IsTargetContextCurrent(
        pendingActivation->targetContext,
        pendingActivation->contextEpoch);
    pendingActivation.reset();
    activationInProgress.store(false, std::memory_order_release);

    {
        std::scoped_lock bufferLock(bufferMutex);
        ResetBufferLocked();
    }
    try
    {
        static_assert(
            (KeyboardManagerConstants::KEYBOARDMANAGER_TEXT_EXPANSION_REPLAY_FLAG &
             CommonSharedConstants::KEYBOARDMANAGER_INJECTED_FLAG) == 0);

        // SendInput guarantees that one INPUT array is inserted serially without
        // interleaving. Suppress the current physical down and submit one ordered
        // batch: old trigger tap, swallowed modifier releases, replayed current down.
        std::vector<INPUT> recoveryEvents;
        recoveryEvents.reserve(5 + request.releasedActivationModifierKeys.size());
        if (targetStillCurrent)
        {
            INPUT actionDown{};
            actionDown.type = INPUT_KEYBOARD;
            actionDown.ki.wVk = static_cast<WORD>(request.actionKey);
            actionDown.ki.wScan = request.actionScanCode != 0 ?
                                      static_cast<WORD>(request.actionScanCode) :
                                      static_cast<WORD>(MapVirtualKeyW(request.actionKey, MAPVK_VK_TO_VSC));
            actionDown.ki.dwFlags = request.actionExtended ? KEYEVENTF_EXTENDEDKEY : 0;
            actionDown.ki.dwExtraInfo = KeyboardManagerConstants::KEYBOARDMANAGER_SHORTCUT_FLAG;
            recoveryEvents.push_back(actionDown);
            INPUT actionUp = actionDown;
            actionUp.ki.dwFlags |= KEYEVENTF_KEYUP;
            recoveryEvents.push_back(actionUp);
        }

        const size_t dummyStart = recoveryEvents.size();
        size_t firstModifierRelease = dummyStart;
        if (!request.releasedActivationModifierKeys.empty())
        {
            Helpers::SetDummyKeyEvent(
                recoveryEvents,
                KeyboardManagerConstants::KEYBOARDMANAGER_SHORTCUT_FLAG);
            firstModifierRelease = recoveryEvents.size();
            for (const DWORD key : request.releasedActivationModifierKeys)
            {
                Helpers::SetKeyEvent(
                    recoveryEvents,
                    INPUT_KEYBOARD,
                    static_cast<WORD>(key),
                    KEYEVENTF_KEYUP,
                    KeyboardManagerConstants::KEYBOARDMANAGER_SHORTCUT_FLAG);
            }
        }
        const size_t modifierReleaseEnd = recoveryEvents.size();

        INPUT replay{};
        replay.type = INPUT_KEYBOARD;
        replay.ki.wVk = static_cast<WORD>(request.replayKey);
        replay.ki.wScan = request.replayScanCode != 0 ?
                              static_cast<WORD>(request.replayScanCode) :
                              static_cast<WORD>(MapVirtualKeyW(request.replayKey, MAPVK_VK_TO_VSC));
        replay.ki.dwFlags = request.replayExtended ? KEYEVENTF_EXTENDEDKEY : 0;
        replay.ki.dwExtraInfo = KeyboardManagerConstants::KEYBOARDMANAGER_TEXT_EXPANSION_REPLAY_FLAG;
        recoveryEvents.push_back(replay);

        const auto result = input.SendVirtualInput(recoveryEvents);
        if (result.IsComplete())
        {
            return true;
        }

        auto cleanup = CreateCleanupForInjectedPrefix(recoveryEvents, result.injectedEventCount);
        if (!request.releasedActivationModifierKeys.empty() &&
            static_cast<size_t>(result.injectedEventCount) <= dummyStart)
        {
            // No dummy event reached the input stream. Preserve the existing
            // modifier-release guard before retrying uninjected Win/Alt/Ctrl/Shift ups.
            Helpers::SetDummyKeyEvent(
                cleanup,
                KeyboardManagerConstants::KEYBOARDMANAGER_SHORTCUT_FLAG);
        }
        const size_t firstUninjectedModifier =
            (std::max)(firstModifierRelease, static_cast<size_t>(result.injectedEventCount));
        for (size_t index = firstUninjectedModifier; index < modifierReleaseEnd; ++index)
        {
            cleanup.push_back(recoveryEvents[index]);
        }
        QueuePendingCleanup(std::move(cleanup));
        return false;
    }
    catch (...)
    {
        // The physical modifier-up events were already swallowed. Best-effort release
        // them even if recovery batch construction or submission faults.
        ReleaseCapturedModifiers(request.releasedActivationModifierKeys);
        return false;
    }
}

TextExpansionResult BufferTextExpansionBackend::CancelPendingActivation() noexcept
{
    std::scoped_lock activationLock(activationMutex);
    if (!pendingActivation)
    {
        return TextExpansionResult::FailedUnchanged;
    }

    const auto modifierKeys = pendingActivation->activationModifierKeys;
    pendingActivation.reset();
    activationInProgress.store(false, std::memory_order_release);
    ReleaseCapturedModifiers(modifierKeys);
    return modifierKeys.empty() ? TextExpansionResult::FailedUnchanged :
                                  TextExpansionResult::FailedChangedOrUnknown;
}

BufferTextExpansionBackend::InputContext BufferTextExpansionBackend::GetCurrentContext() const noexcept
{
    try
    {
        return contextProvider ? contextProvider() : InputContext{};
    }
    catch (...)
    {
        return {};
    }
}

bool BufferTextExpansionBackend::IsTargetContextCurrent(
    const InputContext& expected,
    const uint64_t expectedEpoch) const noexcept
{
    return expected.IsValid() &&
           contextEpoch.load(std::memory_order_acquire) == expectedEpoch &&
           GetCurrentContext() == expected;
}

void BufferTextExpansionBackend::ResetBufferLocked() noexcept
{
    contextEpoch.fetch_add(1, std::memory_order_acq_rel);
    buffer.clear();
    bufferContext = {};
    pendingPacketHighSurrogate = L'\0';
}

bool BufferTextExpansionBackend::ReleaseCapturedModifiers(
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

void BufferTextExpansionBackend::QueuePendingCleanup(std::vector<INPUT> cleanup)
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

void BufferTextExpansionBackend::RetryPendingCleanup() noexcept
{
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
        std::scoped_lock lock(pendingCleanupMutex);
        if (pendingCleanup.empty())
        {
            cleanupAttemptsWithoutProgress = 0;
        }
        return;
    }

    cleanup.erase(cleanup.begin(), cleanup.begin() + injectedCount);
    bool recoveryExhausted = false;
    {
        std::scoped_lock lock(pendingCleanupMutex);
        if (injectedCount != 0)
        {
            cleanupAttemptsWithoutProgress = 0;
        }
        else
        {
            ++cleanupAttemptsWithoutProgress;
        }

        if (cleanupAttemptsWithoutProgress >= MaximumCleanupAttemptsWithoutProgress)
        {
            started.store(false, std::memory_order_release);
            RecordAbandonedKeyUps(cleanup, abandonedKeyUps);
            RecordAbandonedKeyUps(pendingCleanup, abandonedKeyUps);
            pendingCleanup.clear();
            cleanupAttemptsWithoutProgress = 0;
            recoveryExhausted = true;
        }
        else
        {
            try
            {
                cleanup.insert(cleanup.end(), pendingCleanup.begin(), pendingCleanup.end());
                pendingCleanup = std::move(cleanup);
            }
            catch (...)
            {
                started.store(false, std::memory_order_release);
                RecordAbandonedKeyUps(cleanup, abandonedKeyUps);
                RecordAbandonedKeyUps(pendingCleanup, abandonedKeyUps);
                pendingCleanup.clear();
                cleanupAttemptsWithoutProgress = 0;
                recoveryExhausted = true;
            }
        }
    }

    if (recoveryExhausted)
    {
        ResetBuffer();
        Logger::error(L"Keyboard Manager Text Expansion cleanup could not recover; disabling Text Expansion until affected keys are released and settings are reloaded.");
    }
}

bool BufferTextExpansionBackend::ShouldBlockNewInput() const noexcept
{
    if (activationInProgress.load(std::memory_order_acquire))
    {
        return true;
    }

    std::scoped_lock lock(pendingCleanupMutex);
    return !pendingCleanup.empty();
}

bool BufferTextExpansionBackend::HasPendingWork() const noexcept
{
    return ShouldBlockNewInput();
}
