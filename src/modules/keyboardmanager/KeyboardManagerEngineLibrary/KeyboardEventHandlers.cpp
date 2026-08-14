#include "pch.h"
#include <shellapi.h>
#include "KeyboardEventHandlers.h"

#include <common/interop/shared_constants.h>
#include <common/utils/elevation.h>

#include <keyboardmanager/common/InputInterface.h>
#include <keyboardmanager/common/Helpers.h>
#include <keyboardmanager/KeyboardManagerEngineLibrary/trace.h>

#include <TlHelp32.h>
#include <thread>
#include <future>
#include <chrono>
#include <array>
#include <cwctype>
#include <iterator>

#include <winrt/Windows.UI.Notifications.h>
#include <winrt/Windows.Data.Xml.Dom.h>

#include <windows.h>
#include <string>
#include <urlmon.h>
#include <mmsystem.h>

using namespace winrt;
using namespace Windows::UI::Notifications;
using namespace Windows::Data::Xml::Dom;

namespace
{
    bool GeneratedByKBM(const LowlevelKeyboardEvent* data)
    {
        return data->lParam->dwExtraInfo & CommonSharedConstants::KEYBOARDMANAGER_INJECTED_FLAG;
    }

    void UpdateNumpadWithShift(LowlevelKeyboardEvent* data, State& state)
    {
        //Function for fixing numpad when used as shift https://github.com/microsoft/PowerToys/issues/22346
        //VK_CLEAR is not encoded in IsNumpadOriginated
        if (Helpers::IsNumpadOriginated(data->lParam->vkCode) || data->lParam->vkCode == VK_CLEAR)
        {
            // Decode it. If it is VK_CLEAR it will do nothing
            DWORD decodedKey = Helpers::ClearKeyNumpadOrigin(data->lParam->vkCode);
            //check if we already have a stored scanID
            auto scanKey = MapVirtualKey(decodedKey, MAPVK_VK_TO_VSC);
            auto it = state.scanMap.find(scanKey);
            if (it != state.scanMap.end())
            {
                auto keyIt = state.GetSingleKeyRemap(it->second);
                if (keyIt)
                {
                    //if key is stored as shift replace it with the numpad key
                    auto keyValue = keyIt.value();
                    if (keyValue->second.index() == 0)
                    {
                        auto key = std::get<DWORD>(keyValue->second);
                        if (key == VK_LSHIFT || key == VK_RSHIFT || key == VK_SHIFT)
                        {
                            if (state.numpadKeyPressed[it->second])
                            {
                                //replace it with original numpad
                                data->lParam->vkCode = it->second;
                            }
                        }
                    }
                    if (keyValue->second.index() == 1)
                    {
                        auto key = std::get<Shortcut>(keyValue->second);
                        if (key.shiftKey != ModifierKey::Disabled)
                        {
                            if (state.numpadKeyPressed[it->second])
                            { 
                                //replace it with original numpad
                                data->lParam->vkCode = it->second;
                            }
                        }
                    }
                }
            }
        }
        if (Helpers::IsNumpadKeyThatIsAffectedByShift(data->lParam->vkCode))
        {
            // store if the Numpad key was pressed or not. If numpad numbers were pressed but then we get the same key KEY UP but with Numpad unlocked we will replace it.
            state.numpadKeyPressed[data->lParam->vkCode] = (data->wParam == WM_KEYDOWN || data->wParam == WM_SYSKEYDOWN);
        }
    }

    void SetKeyboardStateKey(BYTE keyState[256], const int key, const bool pressed)
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

    void SetKeyboardStateModifier(KeyboardManagerInput::InputInterface& ii, BYTE keyState[256], const int genericKey, const int leftKey, const int rightKey)
    {
        const bool leftPressed = ii.GetVirtualKeyState(leftKey);
        const bool rightPressed = ii.GetVirtualKeyState(rightKey);
        const bool pressed = ii.GetVirtualKeyState(genericKey) || leftPressed || rightPressed;

        SetKeyboardStateKey(keyState, genericKey, pressed);
        SetKeyboardStateKey(keyState, leftKey, leftPressed);
        SetKeyboardStateKey(keyState, rightKey, rightPressed);
    }

    void SetKeyboardStateToggle(BYTE keyState[256], const int key, const bool toggled)
    {
        if (toggled)
        {
            keyState[key] |= 0x01;
        }
        else
        {
            keyState[key] &= ~0x01;
        }
    }

    bool IsModifierPressed(KeyboardManagerInput::InputInterface& ii, const int genericKey, const int leftKey, const int rightKey)
    {
        return ii.GetVirtualKeyState(genericKey) || ii.GetVirtualKeyState(leftKey) || ii.GetVirtualKeyState(rightKey);
    }

    bool IsAltGrPressed(KeyboardManagerInput::InputInterface& ii)
    {
        return ii.GetVirtualKeyState(VK_RMENU) &&
               IsModifierPressed(ii, VK_CONTROL, VK_LCONTROL, VK_RCONTROL) &&
               !ii.GetVirtualKeyState(VK_LMENU);
    }

    bool IsTextReplacementShortcutModifierPressed(KeyboardManagerInput::InputInterface& ii)
    {
        const bool winPressed = ii.GetVirtualKeyState(VK_LWIN) ||
                                ii.GetVirtualKeyState(VK_RWIN) ||
                                ii.GetVirtualKeyState(CommonSharedConstants::VK_WIN_BOTH);
        const bool ctrlOrAltPressed = IsModifierPressed(ii, VK_CONTROL, VK_LCONTROL, VK_RCONTROL) ||
                                      IsModifierPressed(ii, VK_MENU, VK_LMENU, VK_RMENU);
        return winPressed || (ctrlOrAltPressed && !IsAltGrPressed(ii));
    }

    bool IsTextReplacementActivationModifierPressed(KeyboardManagerInput::InputInterface& ii)
    {
        return IsModifierPressed(ii, VK_SHIFT, VK_LSHIFT, VK_RSHIFT) ||
               IsModifierPressed(ii, VK_CONTROL, VK_LCONTROL, VK_RCONTROL) ||
               IsModifierPressed(ii, VK_MENU, VK_LMENU, VK_RMENU) ||
               ii.GetVirtualKeyState(VK_LWIN) ||
               ii.GetVirtualKeyState(VK_RWIN) ||
               ii.GetVirtualKeyState(CommonSharedConstants::VK_WIN_BOTH);
    }

    constexpr bool IsTextReplacementTriggerKey(const DWORD key)
    {
        return key == VK_SPACE || key == VK_RETURN || key == VK_TAB;
    }

    HWND GetTextReplacementWindow()
    {
        GUITHREADINFO guiThreadInfo{};
        guiThreadInfo.cbSize = sizeof(GUITHREADINFO);
        if (GetGUIThreadInfo(0, &guiThreadInfo))
        {
            if (guiThreadInfo.hwndFocus != nullptr)
            {
                return guiThreadInfo.hwndFocus;
            }

            if (guiThreadInfo.hwndActive != nullptr)
            {
                return guiThreadInfo.hwndActive;
            }
        }

        return GetForegroundWindow();
    }

    DWORD GetTextReplacementWindowProcessId(HWND window)
    {
        DWORD processId = 0;
        if (window != nullptr)
        {
            GetWindowThreadProcessId(window, &processId);
        }

        return processId;
    }

    constexpr bool IsHighSurrogate(const wchar_t value)
    {
        return value >= 0xD800 && value <= 0xDBFF;
    }

    constexpr bool IsLowSurrogate(const wchar_t value)
    {
        return value >= 0xDC00 && value <= 0xDFFF;
    }

    constexpr bool IsValidPrintableUtf16(std::wstring_view text)
    {
        for (size_t index = 0; index < text.size(); ++index)
        {
            const wchar_t value = text[index];
            if (IsHighSurrogate(value))
            {
                if (index + 1 >= text.size() || !IsLowSurrogate(text[index + 1]))
                {
                    return false;
                }

                ++index;
                continue;
            }

            if (IsLowSurrogate(value) || value < 0x20 || (value >= 0x7F && value <= 0x9F))
            {
                return false;
            }
        }

        return !text.empty();
    }

    void PopLastUtf16Scalar(std::wstring& text)
    {
        if (text.empty())
        {
            return;
        }

        text.pop_back();
        if (!text.empty() && IsHighSurrogate(text.back()))
        {
            text.pop_back();
        }
    }

    void TrimUtf16Buffer(std::wstring& text, const size_t maximumLength)
    {
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

    enum class KeyboardTextEventKind
    {
        None,
        DeadKey,
        PacketHighSurrogate,
        Text,
    };

    struct KeyboardTextEvent
    {
        KeyboardTextEventKind kind = KeyboardTextEventKind::None;
        std::wstring text;
        bool resetBufferBeforeText = false;
    };

    KeyboardTextEvent GetTextFromKeyboardEvent(KeyboardManagerInput::InputInterface& ii, const LowlevelKeyboardEvent* data, State& state)
    {
        KeyboardTextEvent event;
        const DWORD vkCode = Helpers::ClearKeyNumpadOrigin(data->lParam->vkCode);
        if (vkCode == VK_PACKET)
        {
            const wchar_t packetUnit = static_cast<wchar_t>(data->lParam->scanCode & 0xFFFF);
            if (IsHighSurrogate(packetUnit))
            {
                if (state.textReplacementPendingPacketHighSurrogate != L'\0')
                {
                    // Two leading surrogates cannot form one scalar. Stop tracking the
                    // malformed packet sequence so it cannot bridge an existing suffix.
                    state.textReplacementPendingPacketHighSurrogate = L'\0';
                    state.textReplacementBuffer.clear();
                }
                else
                {
                    state.textReplacementPendingPacketHighSurrogate = packetUnit;
                }
                event.kind = KeyboardTextEventKind::PacketHighSurrogate;
                return event;
            }

            if (IsLowSurrogate(packetUnit) && IsHighSurrogate(state.textReplacementPendingPacketHighSurrogate))
            {
                event.text.push_back(state.textReplacementPendingPacketHighSurrogate);
                event.text.push_back(packetUnit);
                state.textReplacementPendingPacketHighSurrogate = L'\0';
                event.kind = KeyboardTextEventKind::Text;
                return event;
            }

            if (state.textReplacementPendingPacketHighSurrogate != L'\0' || IsLowSurrogate(packetUnit))
            {
                state.textReplacementPendingPacketHighSurrogate = L'\0';
                event.resetBufferBeforeText = true;
                return event;
            }

            event.text.push_back(packetUnit);
            event.kind = IsValidPrintableUtf16(event.text) ? KeyboardTextEventKind::Text : KeyboardTextEventKind::None;
            return event;
        }

        if (state.textReplacementPendingPacketHighSurrogate != L'\0')
        {
            state.textReplacementPendingPacketHighSurrogate = L'\0';
            event.resetBufferBeforeText = true;
        }

        const HWND foregroundWindow = GetForegroundWindow();
        const DWORD foregroundThreadId = foregroundWindow ? GetWindowThreadProcessId(foregroundWindow, nullptr) : 0;
        const HKL layout = GetKeyboardLayout(foregroundThreadId);
        const UINT scanCode = data->lParam->scanCode ? data->lParam->scanCode : MapVirtualKeyExW(vkCode, MAPVK_VK_TO_VSC, layout);
        std::array<BYTE, 256> keyState{};

        if (!GetKeyboardState(keyState.data()))
        {
            return event;
        }

        SetKeyboardStateModifier(ii, keyState.data(), VK_SHIFT, VK_LSHIFT, VK_RSHIFT);
        SetKeyboardStateModifier(ii, keyState.data(), VK_CONTROL, VK_LCONTROL, VK_RCONTROL);
        SetKeyboardStateModifier(ii, keyState.data(), VK_MENU, VK_LMENU, VK_RMENU);
        SetKeyboardStateToggle(keyState.data(), VK_CAPITAL, state.textReplacementCapsLockOn);
        keyState[vkCode] |= 0x80;

        wchar_t output[8]{};
        constexpr UINT toUnicodeFlags = 1u << 2; // Do not change keyboard state.
        const int result = ToUnicodeEx(vkCode, scanCode, keyState.data(), output, static_cast<int>(std::size(output)), toUnicodeFlags, layout);
        if (result < 0)
        {
            state.textReplacementDeadKeyPending = true;
            event.kind = KeyboardTextEventKind::DeadKey;
            return event;
        }

        if (result == 0)
        {
            return event;
        }

        event.text.assign(output, output + (std::min)(result, static_cast<int>(std::size(output))));
        if (!IsValidPrintableUtf16(event.text))
        {
            event.text.clear();
            return event;
        }

        event.kind = KeyboardTextEventKind::Text;
        return event;
    }

    void AppendTextInputEvents(std::vector<INPUT>& inputs, std::wstring_view text)
    {
        for (size_t index = 0; index < text.size(); ++index)
        {
            wchar_t value = text[index];
            if (value == L'\r' && index + 1 < text.size() && text[index + 1] == L'\n')
            {
                ++index;
            }

            if (value == L'\r' || value == L'\n')
            {
                Helpers::SetKeyEvent(inputs, INPUT_KEYBOARD, VK_RETURN, 0, KeyboardManagerConstants::KEYBOARDMANAGER_SHORTCUT_FLAG);
                Helpers::SetKeyEvent(inputs, INPUT_KEYBOARD, VK_RETURN, KEYEVENTF_KEYUP, KeyboardManagerConstants::KEYBOARDMANAGER_SHORTCUT_FLAG);
                continue;
            }

            INPUT keyDown{};
            keyDown.type = INPUT_KEYBOARD;
            keyDown.ki.dwFlags = KEYEVENTF_UNICODE;
            keyDown.ki.dwExtraInfo = KeyboardManagerConstants::KEYBOARDMANAGER_SHORTCUT_FLAG;
            keyDown.ki.wScan = value;
            inputs.push_back(keyDown);

            INPUT keyUp = keyDown;
            keyUp.ki.dwFlags |= KEYEVENTF_KEYUP;
            inputs.push_back(keyUp);
        }
    }

    enum class TextReplacementInputResult
    {
        Completed,
        FailedBeforeMutation,
        FailedAfterMutation,
    };

    constexpr size_t maximumTextReplacementInputsPerBatch = 32;

    void BestEffortReleaseInjectedPrefix(
        KeyboardManagerInput::InputInterface& ii,
        State& state,
        const std::vector<INPUT>& inputs,
        const size_t injectedEventCount)
    {
        std::vector<INPUT> pendingKeyUps;
        for (size_t index = 0; index < (std::min)(injectedEventCount, inputs.size()); ++index)
        {
            const INPUT& input = inputs[index];
            if (input.type != INPUT_KEYBOARD)
            {
                continue;
            }

            if ((input.ki.dwFlags & KEYEVENTF_KEYUP) == 0)
            {
                INPUT keyUp = input;
                keyUp.ki.dwFlags |= KEYEVENTF_KEYUP;
                pendingKeyUps.push_back(keyUp);
            }
            else
            {
                const auto matchingDown = std::find_if(pendingKeyUps.rbegin(), pendingKeyUps.rend(), [&input](const INPUT& candidate) {
                    return candidate.ki.wVk == input.ki.wVk && candidate.ki.wScan == input.ki.wScan;
                });
                if (matchingDown != pendingKeyUps.rend())
                {
                    pendingKeyUps.erase(std::next(matchingDown).base());
                }
            }
        }

        std::reverse(pendingKeyUps.begin(), pendingKeyUps.end());
        if (!pendingKeyUps.empty())
        {
            const auto cleanupResult = ii.SendVirtualInput(pendingKeyUps);
            const size_t completedCount = (std::min)(cleanupResult.injectedEventCount, pendingKeyUps.size());
            if (completedCount < pendingKeyUps.size())
            {
                state.QueuePendingInputCleanup(std::vector<INPUT>(
                    std::make_move_iterator(pendingKeyUps.begin() + completedCount),
                    std::make_move_iterator(pendingKeyUps.end())));
            }
        }
    }

    TextReplacementInputResult SendInputBatch(
        KeyboardManagerInput::InputInterface& ii,
        State& state,
        const std::vector<INPUT>& inputs,
        bool& inputStreamMutated,
        const std::function<bool()>& isCurrent)
    {
        if (inputs.empty())
        {
            return TextReplacementInputResult::Completed;
        }

        bool transactionIsCurrent = false;
        if (isCurrent)
        {
            try
            {
                transactionIsCurrent = isCurrent();
            }
            catch (...)
            {
                transactionIsCurrent = false;
            }
        }
        if (!transactionIsCurrent)
        {
            return inputStreamMutated ? TextReplacementInputResult::FailedAfterMutation : TextReplacementInputResult::FailedBeforeMutation;
        }

        const auto injectionResult = ii.SendVirtualInput(inputs);
        if (!injectionResult.IsComplete())
        {
            if (injectionResult.status == KeyboardManagerInput::SendVirtualInputStatus::Partial)
            {
                BestEffortReleaseInjectedPrefix(ii, state, inputs, injectionResult.injectedEventCount);
            }
            inputStreamMutated = inputStreamMutated || injectionResult.HasInjectedEvents();
            return inputStreamMutated ? TextReplacementInputResult::FailedAfterMutation : TextReplacementInputResult::FailedBeforeMutation;
        }

        inputStreamMutated = true;
        return TextReplacementInputResult::Completed;
    }

    TextReplacementInputResult SendTextInputInSmallBatches(
        KeyboardManagerInput::InputInterface& ii,
        State& state,
        const std::wstring_view text,
        bool& inputStreamMutated,
        const std::function<bool()>& isCurrent)
    {
        std::vector<INPUT> inputs;
        inputs.reserve(maximumTextReplacementInputsPerBatch);

        for (size_t index = 0; index < text.size();)
        {
            size_t unitCount = 1;
            bool isSurrogatePair = false;
            if (text[index] == L'\r' && index + 1 < text.size() && text[index + 1] == L'\n')
            {
                unitCount = 2;
            }
            else if (IsHighSurrogate(text[index]) && index + 1 < text.size() && IsLowSurrogate(text[index + 1]))
            {
                unitCount = 2;
                isSurrogatePair = true;
            }

            const size_t inputCount = isSurrogatePair ? 4 : 2;
            if (!inputs.empty() && inputs.size() + inputCount > maximumTextReplacementInputsPerBatch)
            {
                const TextReplacementInputResult batchResult = SendInputBatch(ii, state, inputs, inputStreamMutated, isCurrent);
                if (batchResult != TextReplacementInputResult::Completed)
                {
                    return batchResult;
                }
                inputs.clear();
            }

            AppendTextInputEvents(inputs, text.substr(index, unitCount));
            index += unitCount;
        }

        return SendInputBatch(ii, state, inputs, inputStreamMutated, isCurrent);
    }

    void ClearDeadKeyTracking(State& state)
    {
        state.textReplacementDeadKeyPending = false;
    }

    void ClearTextReplacementBuffer(State& state)
    {
        state.textReplacementBuffer.clear();
        state.textReplacementPendingPacketHighSurrogate = L'\0';
    }

    void RetryPendingSingleKeyRemapReleases(KeyboardManagerInput::InputInterface& ii, State& state)
    {
        for (const DWORD pendingSourceKey : state.GetSingleKeyRemapReleasePendingKeys())
        {
            const auto* pendingPress = state.GetSingleKeyRemapPressState(pendingSourceKey);
            if (pendingPress == nullptr)
            {
                continue;
            }

            const bool suppressedPhysicalPressHeld = pendingPress->suppressedPhysicalPressHeld;
            const auto retryResult = ii.SendVirtualInput(pendingPress->releaseEvents);
            if (retryResult.IsComplete())
            {
                if (suppressedPhysicalPressHeld)
                {
                    state.SetSingleKeyRemapSuppressed(pendingSourceKey);
                }
                else
                {
                    state.ClearSingleKeyRemapPressState(pendingSourceKey);
                }
            }
        }
    }

}

namespace KeyboardEventHandlers
{
    KeyboardManagerInput::SendVirtualInputResult RetryPendingInputCleanup(KeyboardManagerInput::InputInterface& ii, State& state) noexcept
    {
        std::vector<INPUT> cleanupEvents = state.TakePendingInputCleanup();
        if (cleanupEvents.empty())
        {
            return { KeyboardManagerInput::SendVirtualInputStatus::Complete, 0 };
        }

        KeyboardManagerInput::SendVirtualInputResult result;
        try
        {
            result = ii.SendVirtualInput(cleanupEvents);
        }
        catch (...)
        {
            state.PrependPendingInputCleanup(std::move(cleanupEvents));
            return { KeyboardManagerInput::SendVirtualInputStatus::None, 0 };
        }

        const size_t completedCount = (std::min)(result.injectedEventCount, cleanupEvents.size());
        if (completedCount < cleanupEvents.size())
        {
            std::vector<INPUT> remainingEvents(
                std::make_move_iterator(cleanupEvents.begin() + completedCount),
                std::make_move_iterator(cleanupEvents.end()));
            state.PrependPendingInputCleanup(std::move(remainingEvents));
        }

        return {
            completedCount == 0 ? KeyboardManagerInput::SendVirtualInputStatus::None :
            completedCount == cleanupEvents.size() ? KeyboardManagerInput::SendVirtualInputStatus::Complete :
                                                     KeyboardManagerInput::SendVirtualInputStatus::Partial,
            completedCount,
        };
    }

    static intptr_t HandleSingleKeyRemapEventCore(KeyboardManagerInput::InputInterface& ii, LowlevelKeyboardEvent* data, State& state, const bool updateNumpadState) noexcept
    {
        // Injected events are deliberately allowed to continue to the shortcut layer, but
        // must never re-enter single-key ownership or release retry handling.
        if (GeneratedByKBM(data))
        {
            return 0;
        }

        if (updateNumpadState)
        {
            UpdateNumpadWithShift(data, state);
        }
        const DWORD sourceKey = data->lParam->vkCode;
        const bool isKeyUp = data->wParam == WM_KEYUP || data->wParam == WM_SYSKEYUP;

        // A target release that was blocked no longer has a physical key-up to drive it.
        // Retry it on subsequent physical keyboard activity while retaining ownership and
        // suppressing reload. Re-sending already delivered key-ups is harmless and is much
        // safer than guessing which prefix of a partial SendInput remained held.
        RetryPendingSingleKeyRemapReleases(ii, state);

        if (const auto* existingPress = state.GetSingleKeyRemapPressState(sourceKey))
        {
            if (existingPress->owner == SingleKeyRemapPressOwner::OriginalPassthrough)
            {
                // The initial remapped down was not injected, so every repeat and the
                // matching up belong to the original key for this entire physical press.
                if (isKeyUp)
                {
                    state.ClearSingleKeyRemapPressState(sourceKey);
                }
                return 0;
            }

            if (existingPress->owner == SingleKeyRemapPressOwner::Suppressed)
            {
                if (isKeyUp)
                {
                    state.ClearSingleKeyRemapPressState(sourceKey);
                }
                return 1;
            }

            if (existingPress->releasePending)
            {
                // The retry above was blocked again. Do not allow a new original event to
                // leak while the old remapped target is still owned by this source key.
                state.SetSingleKeyRemapSuppressedPhysicalPressHeld(sourceKey, !isKeyUp);
                return 1;
            }

            if (isKeyUp)
            {
                const auto releaseResult = ii.SendVirtualInput(existingPress->releaseEvents);
                if (releaseResult.IsComplete())
                {
                    state.ClearSingleKeyRemapPressState(sourceKey);
                }
                else
                {
                    state.SetSingleKeyRemapReleasePending(sourceKey);
                }
                return 1;
            }

            // Auto-repeat cannot change ownership. Even a fully blocked repeat remains
            // suppressed because the original down was never passed to the application.
            ii.SendVirtualInput(existingPress->repeatEvents);
            return 1;
        }

        // A key-up without an owned physical press must not synthesize a target key-up.
        if (isKeyUp)
        {
            return 0;
        }

        const auto remapping = state.GetSingleKeyRemap(sourceKey);
        if (!remapping)
        {
            return 0;
        }

        auto it = remapping.value();

        // Check if the remap is to a key or a shortcut.
        const bool remapToKey = it->second.index() == 0;
        if (remapToKey && std::get<DWORD>(it->second) == CommonSharedConstants::VK_DISABLED)
        {
            state.SetSingleKeyRemapSuppressed(sourceKey);
            return 1;
        }

        DWORD target;
        if (remapToKey)
        {
            target = Helpers::FilterArtificialKeys(std::get<DWORD>(it->second));
        }
        else
        {
            target = Helpers::FilterArtificialKeys(std::get<Shortcut>(it->second).GetActionKey());
        }

        const auto lowerLevelResetResult = ResetIfModifierKeyForLowerLevelKeyHandlers(ii, it->first, target);
        bool inputStreamMutatedBeforeTarget = lowerLevelResetResult.HasInjectedEvents();

        // If a Ctrl/Alt/Shift key is remapped to a non-modifier key, reset the modifier
        // state before injecting the target so it is not delivered as WM_SYSKEYDOWN.
        if (Helpers::IsModifierKey(it->first) && !Helpers::IsModifierKey(target) && target != VK_CAPITAL &&
            !(it->first == VK_LWIN || it->first == VK_RWIN || it->first == CommonSharedConstants::VK_WIN_BOTH))
        {
            std::vector<INPUT> suppressList;
            Helpers::SetKeyEvent(suppressList, INPUT_KEYBOARD, static_cast<WORD>(it->first), KEYEVENTF_KEYUP, KeyboardManagerConstants::KEYBOARDMANAGER_SUPPRESS_FLAG);
            inputStreamMutatedBeforeTarget = ii.SendVirtualInput(suppressList).HasInjectedEvents() || inputStreamMutatedBeforeTarget;
        }

        std::vector<INPUT> keyDownEvents;
        std::vector<INPUT> keyUpEvents;
        if (remapToKey)
        {
            Helpers::SetKeyEvent(keyDownEvents, INPUT_KEYBOARD, static_cast<WORD>(target), 0, KeyboardManagerConstants::KEYBOARDMANAGER_SINGLEKEY_FLAG);
            Helpers::SetKeyEvent(keyUpEvents, INPUT_KEYBOARD, static_cast<WORD>(target), KEYEVENTF_KEYUP, KeyboardManagerConstants::KEYBOARDMANAGER_SINGLEKEY_FLAG);
        }
        else
        {
            const Shortcut targetShortcut = std::get<Shortcut>(it->second);
            Helpers::SetModifierKeyEvents(targetShortcut, Modifiers(), keyDownEvents, true, KeyboardManagerConstants::KEYBOARDMANAGER_SINGLEKEY_FLAG);
            Helpers::SetKeyEvent(keyDownEvents, INPUT_KEYBOARD, static_cast<WORD>(targetShortcut.GetActionKey()), 0, KeyboardManagerConstants::KEYBOARDMANAGER_SINGLEKEY_FLAG);

            Helpers::SetKeyEvent(keyUpEvents, INPUT_KEYBOARD, static_cast<WORD>(targetShortcut.GetActionKey()), KEYEVENTF_KEYUP, KeyboardManagerConstants::KEYBOARDMANAGER_SINGLEKEY_FLAG);
            Helpers::SetModifierKeyEvents(targetShortcut, Modifiers(), keyUpEvents, false, KeyboardManagerConstants::KEYBOARDMANAGER_SINGLEKEY_FLAG);
        }

        const auto injectionResult = ii.SendVirtualInput(keyDownEvents);
        if (injectionResult.status == KeyboardManagerInput::SendVirtualInputStatus::None)
        {
            // Nothing from the remap reached the system, so the original key owns this
            // complete physical press and must receive all repeats plus its matching up.
            if (inputStreamMutatedBeforeTarget)
            {
                state.SetSingleKeyRemapSuppressed(sourceKey);
                return 1;
            }

            state.SetSingleKeyRemapPassthrough(sourceKey);
            return 0;
        }

        if (injectionResult.status == KeyboardManagerInput::SendVirtualInputStatus::Partial)
        {
            // Only the reported prefix reached the system. Clean up precisely that prefix
            // and suppress the rest of this physical press; a full target release would
            // emit key-ups for suffix keys that were never injected (and could release a
            // key the user is holding independently).
            BestEffortReleaseInjectedPrefix(ii, state, keyDownEvents, injectionResult.injectedEventCount);
            state.SetSingleKeyRemapSuppressed(sourceKey);
            return 1;
        }

        // Only a complete target down sequence owns the matching full release.
        state.SetSingleKeyRemapTarget(sourceKey, std::move(keyDownEvents), std::move(keyUpEvents));

        // If Caps Lock is being remapped to Ctrl/Alt/Shift, reset the modifier state to
        // lower-level handlers (and likewise for each modifier in a shortcut target).
        if (remapToKey)
        {
            ResetIfModifierKeyForLowerLevelKeyHandlers(ii, target, it->first);
        }
        else
        {
            for (const DWORD shortcutKey : std::get<Shortcut>(it->second).GetKeyCodes())
            {
                ResetIfModifierKeyForLowerLevelKeyHandlers(ii, shortcutKey, it->first);
            }
        }

        if (remapToKey)
        {
            static int dayWeLastSentKeyToKeyTelemetryOn = -1;
            const auto currentDay = std::chrono::duration_cast<std::chrono::days>(std::chrono::system_clock::now().time_since_epoch()).count();
            if (dayWeLastSentKeyToKeyTelemetryOn != currentDay)
            {
                Trace::DailyKeyToKeyRemapInvoked();
                dayWeLastSentKeyToKeyTelemetryOn = currentDay;
            }
        }
        else
        {
            static int dayWeLastSentKeyToShortcutTelemetryOn = -1;
            const auto currentDay = std::chrono::duration_cast<std::chrono::days>(std::chrono::system_clock::now().time_since_epoch()).count();
            if (dayWeLastSentKeyToShortcutTelemetryOn != currentDay)
            {
                Trace::DailyKeyToShortcutRemapInvoked();
                dayWeLastSentKeyToShortcutTelemetryOn = currentDay;
            }
        }

        return 1;
    }

    // Function to handle a single key remap
    intptr_t HandleSingleKeyRemapEvent(KeyboardManagerInput::InputInterface& ii, LowlevelKeyboardEvent* data, State& state) noexcept
    {
        return HandleSingleKeyRemapEventCore(ii, data, state, true);
    }

    /* This feature has not been enabled (code from proof of concept stage)
    * 
    // Function to change a key's behavior from toggle to modifier
    __declspec(dllexport) intptr_t HandleSingleKeyToggleToModEvent(InputInterface& ii, LowlevelKeyboardEvent* data, State& State) noexcept
    {
        // Check if the key event was generated by KeyboardManager to avoid remapping events generated by us.
        if (!(data->lParam->dwExtraInfo & CommonSharedConstants::KEYBOARDMANAGER_INJECTED_FLAG))
        {
            // The mutex should be unlocked before SendInput is called to avoid re-entry into the same mutex. More details can be found at https://github.com/microsoft/PowerToys/pull/1789#issuecomment-607555837
            std::unique_lock<std::mutex> lock(State.singleKeyToggleToMod_mutex);
            auto it = State.singleKeyToggleToMod.find(data->lParam->vkCode);
            if (it != State.singleKeyToggleToMod.end())
            {
                // To avoid long presses (which leads to continuous keydown messages) from toggling the key on and off
                if (data->wParam == WM_KEYDOWN || data->wParam == WM_SYSKEYDOWN)
                {
                    if (it->second == false)
                    {
                        State.singleKeyToggleToMod[data->lParam->vkCode] = true;
                    }
                    else
                    {
                        lock.unlock();
                        return 1;
                    }
                }
                std::vector<INPUT> keyEventList;
                Helpers::SetKeyEvent(keyEventList, INPUT_KEYBOARD, (WORD)data->lParam->vkCode, 0, KeyboardManagerConstants::KEYBOARDMANAGER_SINGLEKEY_FLAG);
                Helpers::SetKeyEvent(keyEventList, INPUT_KEYBOARD, (WORD)data->lParam->vkCode, KEYEVENTF_KEYUP, KeyboardManagerConstants::KEYBOARDMANAGER_SINGLEKEY_FLAG);

                lock.unlock();
                ii.SendVirtualInput(keyEventList);

                // Reset the long press flag when the key has been lifted.
                if (data->wParam == WM_KEYUP || data->wParam == WM_SYSKEYUP)
                {
                    lock.lock();
                    State.singleKeyToggleToMod[data->lParam->vkCode] = false;
                    lock.unlock();
                }

                return 1;
            }
        }

        return 0;
    }
    */

    // Function to handle a shortcut remap
    intptr_t HandleShortcutRemapEvent(KeyboardManagerInput::InputInterface& ii, LowlevelKeyboardEvent* data, State& state, const std::optional<std::wstring>& activatedApp, const bool allowNewRemappings) noexcept
    {
        auto resetChordsResults = ResetChordsIfNeeded(data, state, activatedApp);

        // Check if any shortcut is currently in the invoked state
        bool isShortcutInvoked = state.CheckShortcutRemapInvoked(activatedApp);

        // Get shortcut table for given activatedApp
        ShortcutRemapTable& reMap = state.GetShortcutRemapTable(activatedApp);

        // Iterate through the shortcut remaps and apply whichever has been pressed
        for (auto& itShortcut : state.GetSortedShortcutRemapVector(activatedApp))
        {
            const auto it = reMap.find(itShortcut);

            // If a shortcut is currently in the invoked state then skip till the shortcut that is currently invoked
            if (isShortcutInvoked && !it->second.isShortcutInvoked)
            {
                continue;
            }

            // Check if the remap is to a key or a shortcut
            const bool remapToKey = it->second.targetShortcut.index() == 0;
            const bool remapToShortcut = it->second.targetShortcut.index() == 1;
            const bool remapToText = it->second.targetShortcut.index() == 2;
            const bool isRunProgram = (remapToShortcut && std::get<Shortcut>(it->second.targetShortcut).IsRunProgram());
            const bool isOpenUri = (remapToShortcut && std::get<Shortcut>(it->second.targetShortcut).IsOpenURI());
            const size_t src_size = it->first.Size();
            const size_t dest_size = remapToShortcut ? std::get<Shortcut>(it->second.targetShortcut).Size() : 1;

            bool isMatchOnChordEnd = false;
            bool isMatchOnChordStart = false;

            static bool isAltRightKeyInvoked = false;

            // Check if the right Alt key (AltGr) is pressed.
            if (data->lParam->vkCode == VK_RMENU && ii.GetVirtualKeyState(VK_LCONTROL) && (data->wParam == WM_KEYDOWN || data->wParam == WM_SYSKEYDOWN))
            {
                isAltRightKeyInvoked = true;
            }
            else if (data->lParam->vkCode == VK_RMENU && (data->wParam == WM_KEYUP || data->wParam == WM_SYSKEYUP))
            {
                isAltRightKeyInvoked = false;
            }

            // If the shortcut has been pressed down
            if (!it->second.isShortcutInvoked && it->first.CheckModifiersKeyboardState(ii))
            {
                // if not a mod key, check for chord stuff
                if (!resetChordsResults.CurrentKeyIsModifierKey && (data->wParam == WM_KEYDOWN || data->wParam == WM_SYSKEYDOWN))
                {
                    if (it->first.exactMatch == true && !it->first.IsKeyboardStateClearExceptShortcut(ii))
                    {
                        continue;
                    }

                    if (itShortcut.HasChord())
                    {
                        if (!resetChordsResults.AnyChordStarted && data->lParam->vkCode == itShortcut.GetActionKey() && !itShortcut.IsChordStarted() && itShortcut.HasChord())
                        {
                            // start new chord
                            // Logger::trace(L"ChordKeyboardHandler:new chord started for {}", data->lParam->vkCode);
                            isMatchOnChordStart = true;
                            ResetAllOtherStartedChords(state, activatedApp, data->lParam->vkCode);
                            itShortcut.SetChordStarted(true);
                            continue;
                        }

                        if (itShortcut.IsChordStarted() && itShortcut.HasChord())
                        {
                            if (data->lParam->vkCode == itShortcut.GetSecondKey())
                            {
                                Logger::trace(L"ChordKeyboardHandler:found chord match {}, {}", itShortcut.GetActionKey(), itShortcut.GetSecondKey());
                                isMatchOnChordEnd = true;
                            }
                            // Resets chord status for the shortcut. A key was pressed and we registered if it was the end of the chord. We can reset it.
                            if (data->lParam->vkCode != itShortcut.GetActionKey())
                            {
                                itShortcut.SetChordStarted(false);
                            }
                        }

                        if (resetChordsResults.AnyChordStarted && !isMatchOnChordEnd)
                        {
                            // Logger::trace(L"ChordKeyboardHandler:waiting on second key of chord, checked {} for {}", itShortcut.GetSecondKey(), data->lParam->vkCode);
                            // this is a key and there is a mod, but it's not the second key of a chord.
                            // we can't do anything with this key, we're waiting.
                            continue;
                        }
                    }
                }

                if (isMatchOnChordEnd || (!resetChordsResults.AnyChordStarted && !itShortcut.HasChord() && (data->lParam->vkCode == it->first.GetActionKey() && (data->wParam == WM_KEYDOWN || data->wParam == WM_SYSKEYDOWN))))
                {
                    ResetAllStartedChords(state, activatedApp);
                    resetChordsResults.AnyChordStarted = false;

                    // Check if any other keys have been pressed apart from the shortcut. If true, then check for the next shortcut. This is to be done only for shortcut to shortcut remaps
                    if (!it->first.IsKeyboardStateClearExceptShortcut(ii) && (remapToShortcut || (remapToKey && std::get<DWORD>(it->second.targetShortcut) == CommonSharedConstants::VK_DISABLED)))
                    {
                        continue;
                    }

                    std::vector<INPUT> keyEventList;

                    // Remember which win key was pressed initially
                    if (ii.GetVirtualKeyState(VK_RWIN))
                    {
                        it->second.modifierKeysInvoked.winKey = ModifierKey::Right;
                    }
                    else if (ii.GetVirtualKeyState(VK_LWIN))
                    {
                        it->second.modifierKeysInvoked.winKey = ModifierKey::Left;
                    }
                    if (ii.GetVirtualKeyState(VK_RCONTROL))
                    {
                        it->second.modifierKeysInvoked.ctrlKey = ModifierKey::Right;
                    }
                    else if (ii.GetVirtualKeyState(VK_LCONTROL))
                    {
                        it->second.modifierKeysInvoked.ctrlKey = ModifierKey::Left;
                    }
                    if (ii.GetVirtualKeyState(VK_RSHIFT))
                    {
                        it->second.modifierKeysInvoked.shiftKey = ModifierKey::Right;
                    }
                    else if (ii.GetVirtualKeyState(VK_LSHIFT))
                    {
                        it->second.modifierKeysInvoked.shiftKey = ModifierKey::Left;
                    }
                    if (ii.GetVirtualKeyState(VK_RMENU))
                    {
                        it->second.modifierKeysInvoked.altKey = ModifierKey::Right;
                    }
                    else if (ii.GetVirtualKeyState(VK_LMENU))
                    {
                        it->second.modifierKeysInvoked.altKey = ModifierKey::Left;
                    }

                    if (isRunProgram)
                    {
                        auto threadFunction = [it]() {
                            CreateOrShowProcessForShortcut(std::get<Shortcut>(it->second.targetShortcut));
                        };

                        std::thread myThread(threadFunction);
                        if (myThread.joinable())
                        {
                            myThread.detach();
                        }

                        Logger::trace(L"ChordKeyboardHandler:returning..");
                        return 1;
                    }
                    else if (isOpenUri)
                    {
                        auto shortcut = std::get<Shortcut>(it->second.targetShortcut);

                        auto uri = shortcut.uriToOpen;
                        auto newUri = uri;

                        if (!PathIsURL(uri.c_str()))
                        {
                            WCHAR url[1024];
                            DWORD bufferSize = 1024;

                            if (UrlCreateFromPathW(uri.c_str(), url, &bufferSize, 0) == S_OK)
                            {
                                newUri = url;
                                Logger::trace(L"ChordKeyboardHandler:ConvertPathToURI from {} to {}", uri, url);
                            }
                            else
                            {
                                // need access to text resources, maybe "convert-resx-to-rc.ps1" is not working to get
                                // text from KeyboardManagerEditor to here in KeyboardManagerEngineLibrary land?
                                toast(L"Error", L"Could not understand the Path or URI");
                                return 1;
                            }
                        }

                        auto threadFunction = [newUri]() {
                            HINSTANCE result = ShellExecute(NULL, L"open", newUri.c_str(), NULL, NULL, SW_SHOWNORMAL);

                            if (result == reinterpret_cast<HINSTANCE>(HINSTANCE_ERROR))
                            {
                                // need access to text resources, maybe "convert-resx-to-rc.ps1" is not working to get
                                // text from KeyboardManagerEditor to here in KeyboardManagerEngineLibrary land?
                                toast(L"Error", L"Could not understand the Path or URI");
                            }
                        };

                        std::thread myThread(threadFunction);
                        if (myThread.joinable())
                        {
                            myThread.detach();
                        }

                        Logger::trace(L"ChordKeyboardHandler:returning..");
                        return 1;
                    }
                    else if (remapToShortcut)
                    {
                        // Get the common keys between the two shortcuts if this is not a runProgram shortcut

                        int commonKeys = it->first.GetCommonModifiersCount(std::get<Shortcut>(it->second.targetShortcut));

                        // If the original shortcut modifiers are a subset of the new shortcut
                        if (commonKeys == src_size - 1)
                        {
                            // key down for all new shortcut keys except the common modifiers
                            keyEventList = std::vector<INPUT>{};
                            Helpers::SetModifierKeyEvents(std::get<Shortcut>(it->second.targetShortcut), it->second.modifierKeysInvoked, keyEventList, true, KeyboardManagerConstants::KEYBOARDMANAGER_SHORTCUT_FLAG, it->first);
                            Helpers::SetKeyEvent(keyEventList, INPUT_KEYBOARD, static_cast<WORD>(std::get<Shortcut>(it->second.targetShortcut).GetActionKey()), 0, KeyboardManagerConstants::KEYBOARDMANAGER_SHORTCUT_FLAG);
                        }
                        else
                        {
                            // Dummy key, key up for all the original shortcut modifier keys and key down for all the new shortcut keys but common keys in each are not repeated
                            // Send a dummy key event to prevent modifier press+release from being triggered. Example: Win+A->Ctrl+V, press Win+A, since Win will be released here we need to send a dummy event before it
                            Helpers::SetDummyKeyEvent(keyEventList, KeyboardManagerConstants::KEYBOARDMANAGER_SHORTCUT_FLAG);

                            // Release original shortcut state (release in reverse order of shortcut to be accurate)
                            Helpers::SetModifierKeyEvents(it->first, it->second.modifierKeysInvoked, keyEventList, false, KeyboardManagerConstants::KEYBOARDMANAGER_SHORTCUT_FLAG, std::get<Shortcut>(it->second.targetShortcut));

                            // Set new shortcut key down state
                            Helpers::SetModifierKeyEvents(std::get<Shortcut>(it->second.targetShortcut), it->second.modifierKeysInvoked, keyEventList, true, KeyboardManagerConstants::KEYBOARDMANAGER_SHORTCUT_FLAG, it->first);
                            Helpers::SetKeyEvent(keyEventList, INPUT_KEYBOARD, static_cast<WORD>(std::get<Shortcut>(it->second.targetShortcut).GetActionKey()), 0, KeyboardManagerConstants::KEYBOARDMANAGER_SHORTCUT_FLAG);
                        }

                        // Modifier state reset might be required for this key depending on the shortcut's action and target modifiers - ex: Win+Caps -> Ctrl+A
                        if (it->first.GetCtrlKey(it->second.modifierKeysInvoked.ctrlKey) == NULL && it->first.GetAltKey(it->second.modifierKeysInvoked.altKey) == NULL && it->first.GetShiftKey(it->second.modifierKeysInvoked.shiftKey) == NULL)
                        {
                            Shortcut temp = std::get<Shortcut>(it->second.targetShortcut);
                            for (auto keys : temp.GetKeyCodes())
                            {
                                ResetIfModifierKeyForLowerLevelKeyHandlers(ii, keys, data->lParam->vkCode);
                            }
                        }
                    }
                    else if (remapToKey)
                    {
                        // Do not send Disable key
                        if (std::get<DWORD>(it->second.targetShortcut) == CommonSharedConstants::VK_DISABLED)
                        {
                            // Since the original shortcut's action key is pressed, set it to true
                            it->second.isOriginalActionKeyPressed = true;
                        }

                        // Send a dummy key event to prevent modifier press+release from being triggered. Example: Win+A->V, press Win+A, since Win will be released here we need to send a dummy event before it
                        Helpers::SetDummyKeyEvent(keyEventList, KeyboardManagerConstants::KEYBOARDMANAGER_SHORTCUT_FLAG);

                        // Release original shortcut state (release in reverse order of shortcut to be accurate)
                        Helpers::SetModifierKeyEvents(it->first, it->second.modifierKeysInvoked, keyEventList, false, KeyboardManagerConstants::KEYBOARDMANAGER_SHORTCUT_FLAG);

                        // Set target key down state
                        if (std::get<DWORD>(it->second.targetShortcut) != CommonSharedConstants::VK_DISABLED)
                        {
                            Helpers::SetKeyEvent(keyEventList, INPUT_KEYBOARD, static_cast<WORD>(Helpers::FilterArtificialKeys(std::get<DWORD>(it->second.targetShortcut))), 0, KeyboardManagerConstants::KEYBOARDMANAGER_SHORTCUT_FLAG);
                        }

                        // Modifier state reset might be required for this key depending on the shortcut's action and target modifier - ex: Win+Caps -> Ctrl
                        if (it->first.GetCtrlKey(it->second.modifierKeysInvoked.ctrlKey) == NULL && it->first.GetAltKey(it->second.modifierKeysInvoked.altKey) == NULL && it->first.GetShiftKey(it->second.modifierKeysInvoked.shiftKey) == NULL)
                        {
                            ResetIfModifierKeyForLowerLevelKeyHandlers(ii, static_cast<WORD>(Helpers::FilterArtificialKeys(std::get<DWORD>(it->second.targetShortcut))), data->lParam->vkCode);
                        }
                    }
                    // Remapped to text
                    else
                    {
                        auto& remapping = std::get<std::wstring>(it->second.targetShortcut);

                        Helpers::SetDummyKeyEvent(keyEventList, KeyboardManagerConstants::KEYBOARDMANAGER_SHORTCUT_FLAG);

                        // Release original shortcut state (release in reverse order of shortcut to be accurate)
                        Helpers::SetModifierKeyEvents(it->first, it->second.modifierKeysInvoked, keyEventList, false, KeyboardManagerConstants::KEYBOARDMANAGER_SHORTCUT_FLAG);

                        // Send modifier release events first, then send text directly
                        // (SendTextInput handles multiline by flushing between chunks)
                        const auto modifierReleaseResult = ii.SendVirtualInput(keyEventList);
                        if (modifierReleaseResult.status == KeyboardManagerInput::SendVirtualInputStatus::Partial)
                        {
                            BestEffortReleaseInjectedPrefix(ii, state, keyEventList, modifierReleaseResult.injectedEventCount);
                        }
                        if (modifierReleaseResult.status == KeyboardManagerInput::SendVirtualInputStatus::None)
                        {
                            return 0;
                        }
                        if (!modifierReleaseResult.IsComplete())
                        {
                            // Some original modifiers were already released. Suppress the
                            // source action and retain shortcut ownership for later cleanup,
                            // but do not type into this half-transitioned keyboard state.
                            it->second.isShortcutInvoked = true;
                            if (activatedApp)
                            {
                                state.SetActivatedApp(*activatedApp);
                            }
                            return 1;
                        }
                        keyEventList.clear();
                        std::vector<INPUT> pendingInputCleanup;
                        Helpers::SendTextInput(remapping, ii, pendingInputCleanup);
                        state.QueuePendingInputCleanup(std::move(pendingInputCleanup));
                    }

                    Logger::trace(L"ChordKeyboardHandler:keyEventList.size:{}", keyEventList.size());

                    const auto activationResult = ii.SendVirtualInput(keyEventList);
                    if (activationResult.status == KeyboardManagerInput::SendVirtualInputStatus::Partial)
                    {
                        BestEffortReleaseInjectedPrefix(ii, state, keyEventList, activationResult.injectedEventCount);
                    }
                    if (activationResult.status == KeyboardManagerInput::SendVirtualInputStatus::None)
                    {
                        return 0;
                    }

                    it->second.isShortcutInvoked = true;
                    // If app specific shortcut is invoked, store the target application
                    // only after the target input was accepted.
                    if (activatedApp)
                    {
                        state.SetActivatedApp(*activatedApp);
                    }
                    if (activatedApp.has_value())
                    {
                        if (remapToKey)
                        {
                            static int dayWeLastSentAppSpecificShortcutToKeyTelemetryOn = -1;
                            auto currentDay = std::chrono::duration_cast<std::chrono::days>(std::chrono::system_clock::now().time_since_epoch()).count();
                            if (dayWeLastSentAppSpecificShortcutToKeyTelemetryOn != currentDay)
                            {
                                Trace::DailyAppSpecificShortcutToKeyRemapInvoked();
                                dayWeLastSentAppSpecificShortcutToKeyTelemetryOn = currentDay;
                            }
                        }
                        else if (remapToShortcut && (!isRunProgram) && (!isOpenUri))
                        {
                            static int dayWeLastSentAppSpecificShortcutToShortcutTelemetryOn = -1;
                            auto currentDay = std::chrono::duration_cast<std::chrono::days>(std::chrono::system_clock::now().time_since_epoch()).count();
                            if (dayWeLastSentAppSpecificShortcutToShortcutTelemetryOn != currentDay)
                            {
                                Trace::DailyAppSpecificShortcutToShortcutRemapInvoked();
                                dayWeLastSentAppSpecificShortcutToShortcutTelemetryOn = currentDay;
                            }
                        }
                    }
                    else
                    {
                        if (remapToKey)
                        {
                            static int dayWeLastSentShortcutToKeyTelemetryOn = -1;
                            auto currentDay = std::chrono::duration_cast<std::chrono::days>(std::chrono::system_clock::now().time_since_epoch()).count();
                            if (dayWeLastSentShortcutToKeyTelemetryOn != currentDay)
                            {
                                Trace::DailyShortcutToKeyRemapInvoked();
                                dayWeLastSentShortcutToKeyTelemetryOn = currentDay;
                            }
                        }
                        else if (remapToShortcut && (!isRunProgram) && (!isOpenUri))
                        {
                            static int dayWeLastSentShortcutToShortcutTelemetryOn = -1;
                            auto currentDay = std::chrono::duration_cast<std::chrono::days>(std::chrono::system_clock::now().time_since_epoch()).count();
                            if (dayWeLastSentShortcutToShortcutTelemetryOn != currentDay)
                            {
                                Trace::DailyShortcutToShortcutRemapInvoked();
                                dayWeLastSentShortcutToShortcutTelemetryOn = currentDay;
                            }
                        }
                    }

                    return 1;
                }
            }
            else if (it->second.isShortcutInvoked)
            {
                // The shortcut has already been pressed down at least once, i.e. the shortcut has been invoked
                // There are 6 cases to be handled if the shortcut has been pressed down
                // 1. The user lets go of one of the modifier keys - reset the keyboard back to the state of the keys actually being pressed down
                // 2. The user keeps the shortcut pressed - the shortcut is repeated (for example you could hold down Ctrl+V and it will keep pasting)
                // 3. The user lets go of the action key - keep modifiers of the new shortcut until some other key event which doesn't apply to the original shortcut
                // 4. The user presses a modifier key in the original shortcut - suppress that key event since the original shortcut is already held down physically (This case can occur only if a user has a duplicated modifier key (possibly by remapping) or if user presses both L/R versions of a modifier remapped with "Both")
                // 5. The user presses any key apart from the action key or a modifier key in the original shortcut - revert the keyboard state to just the original modifiers being held down along with the current key press
                // 6. The user releases any key apart from original modifier or original action key - This can't happen since the key down would have to happen first, which is handled above

                // Prevents the unintended release of the Ctrl part when AltGr is pressed. AltGr acts as both Ctrl and Alt being pressed.
                // After triggering a shortcut involving AltGr, the system might attempt to release the Ctrl part. This code ensures Ctrl remains pressed, maintaining the AltGr state correctly.
                if (isAltRightKeyInvoked && data->lParam->vkCode == VK_LCONTROL && (data->wParam == WM_KEYUP || data->wParam == WM_SYSKEYUP))
                {
                    break;
                }

                // Get the common keys between the two shortcuts
                int commonKeys = (remapToShortcut && !isRunProgram) ? it->first.GetCommonModifiersCount(std::get<Shortcut>(it->second.targetShortcut)) : 0;

                // Case 1: If any of the modifier keys of the original shortcut are released before the action key
                if ((it->first.CheckWinKey(data->lParam->vkCode) || it->first.CheckCtrlKey(data->lParam->vkCode) || it->first.CheckAltKey(data->lParam->vkCode) || it->first.CheckShiftKey(data->lParam->vkCode)) && (data->wParam == WM_KEYUP || data->wParam == WM_SYSKEYUP))
                {
                    // Release new shortcut, and set original shortcut keys except the one released
                    std::vector<INPUT> keyEventList;
                    if (remapToShortcut && !isRunProgram)
                    {
                        // If the target shortcut's action key is pressed, then it should be released
                        bool isActionKeyPressed = ii.GetVirtualKeyState(std::get<Shortcut>(it->second.targetShortcut).GetActionKey());

                        // Release new shortcut state (release in reverse order of shortcut to be accurate)
                        if (isActionKeyPressed)
                        {
                            Helpers::SetKeyEvent(keyEventList, INPUT_KEYBOARD, static_cast<WORD>(std::get<Shortcut>(it->second.targetShortcut).GetActionKey()), KEYEVENTF_KEYUP, KeyboardManagerConstants::KEYBOARDMANAGER_SHORTCUT_FLAG);
                        }

                        Helpers::SetModifierKeyEvents(std::get<Shortcut>(it->second.targetShortcut), it->second.modifierKeysInvoked, keyEventList, false, KeyboardManagerConstants::KEYBOARDMANAGER_SHORTCUT_FLAG, it->first, data->lParam->vkCode);

                        if (!isAltRightKeyInvoked)
                        {
                            // Set original shortcut key down state except the action key and the released modifier since the original action key may or may not be held down. If it is held down it will generate its own key message
                            Helpers::SetModifierKeyEvents(it->first, it->second.modifierKeysInvoked, keyEventList, true, KeyboardManagerConstants::KEYBOARDMANAGER_SHORTCUT_FLAG, std::get<Shortcut>(it->second.targetShortcut), data->lParam->vkCode);
                        }
                        else
                        {
                            isAltRightKeyInvoked = false;
                        }

                        // Send a dummy key event to prevent modifier press+release from being triggered. Example: Win+Ctrl+A->Ctrl+V, press Win+Ctrl+A and release A then Ctrl, since Win will be pressed here we need to send a dummy event after it
                        Helpers::SetDummyKeyEvent(keyEventList, KeyboardManagerConstants::KEYBOARDMANAGER_SHORTCUT_FLAG);
                    }
                    else if (remapToKey)
                    {
                        bool isTargetKeyPressed = (std::get<DWORD>(it->second.targetShortcut) != CommonSharedConstants::VK_DISABLED) && ii.GetVirtualKeyState(Helpers::FilterArtificialKeys(std::get<DWORD>(it->second.targetShortcut)));

                        // Release new key state
                        if (std::get<DWORD>(it->second.targetShortcut) != CommonSharedConstants::VK_DISABLED && isTargetKeyPressed)
                        {
                            Helpers::SetKeyEvent(keyEventList, INPUT_KEYBOARD, static_cast<WORD>(Helpers::FilterArtificialKeys(std::get<DWORD>(it->second.targetShortcut))), KEYEVENTF_KEYUP, KeyboardManagerConstants::KEYBOARDMANAGER_SHORTCUT_FLAG);
                        }

                        // Ensures that after releasing both the action key and AltGr, Ctrl does not remain falsely pressed.
                        if (!isAltRightKeyInvoked)
                        {
                            // Set original shortcut key down state except the action key and the released modifier since the original action key may or may not be held down. If it is held down it will generate its own key message
                            Helpers::SetModifierKeyEvents(it->first, it->second.modifierKeysInvoked, keyEventList, true, KeyboardManagerConstants::KEYBOARDMANAGER_SHORTCUT_FLAG, Shortcut(), data->lParam->vkCode);
                        }
                        else
                        {
                            isAltRightKeyInvoked = false;
                        }

                        // Send a dummy key event to prevent modifier press+release from being triggered. Example: Win+Ctrl+A->V, press Win+Ctrl+A and release A then Ctrl, since Win will be pressed here we need to send a dummy event after it
                        Helpers::SetDummyKeyEvent(keyEventList, KeyboardManagerConstants::KEYBOARDMANAGER_SHORTCUT_FLAG);
                    }

                    const auto releaseResult = ii.SendVirtualInput(keyEventList);
                    if (releaseResult.status == KeyboardManagerInput::SendVirtualInputStatus::Partial)
                    {
                        BestEffortReleaseInjectedPrefix(ii, state, keyEventList, releaseResult.injectedEventCount);
                    }
                    if (!releaseResult.IsComplete())
                    {
                        // This shortcut already owns injected output. Never leak the
                        // physical key-up; retain ownership so a later event retries the
                        // complete release sequence.
                        return 1;
                    }

                    // Commit the ownership transition only after the target release was accepted.
                    it->second.isShortcutInvoked = false;
                    it->second.modifierKeysInvoked.Reset();
                    it->second.isOriginalActionKeyPressed = false;

                    // If app specific shortcut has finished invoking, reset the target application.
                    if (activatedApp)
                    {
                        state.SetActivatedApp(KeyboardManagerConstants::NoActivatedApp);
                    }
                    return 1;
                }

                // The system will see the modifiers of the new shortcut as being held down because of the shortcut remap
                if (!remapToShortcut || (remapToShortcut && std::get<Shortcut>(it->second.targetShortcut).CheckModifiersKeyboardState(ii)))
                {
                    // Case 2: If the original shortcut is still held down the keyboard will get a key down message of the action key in the original shortcut and the new shortcut's modifiers will be held down (keys held down send repeated keydown messages)
                    if (((data->lParam->vkCode == it->first.GetActionKey() && !it->first.HasChord()) || (data->lParam->vkCode == it->first.GetSecondKey() && it->first.HasChord())) && (data->wParam == WM_KEYDOWN || data->wParam == WM_SYSKEYDOWN))
                    {
                        // In case of mapping to disable do not send anything
                        if (remapToKey && std::get<DWORD>(it->second.targetShortcut) == CommonSharedConstants::VK_DISABLED)
                        {
                            // Since the original shortcut's action key is pressed, set it to true
                            it->second.isOriginalActionKeyPressed = true;
                            return 1;
                        }

                        std::vector<INPUT> keyEventList;
                        if (remapToShortcut)
                        {
                            Helpers::SetKeyEvent(keyEventList, INPUT_KEYBOARD, static_cast<WORD>(std::get<Shortcut>(it->second.targetShortcut).GetActionKey()), 0, KeyboardManagerConstants::KEYBOARDMANAGER_SHORTCUT_FLAG);
                        }
                        else if (remapToKey)
                        {
                            Helpers::SetKeyEvent(keyEventList, INPUT_KEYBOARD, static_cast<WORD>(Helpers::FilterArtificialKeys(std::get<DWORD>(it->second.targetShortcut))), 0, KeyboardManagerConstants::KEYBOARDMANAGER_SHORTCUT_FLAG);
                        }
                        else if (remapToText)
                        {
                            auto& remapping = std::get<std::wstring>(it->second.targetShortcut);
                            std::vector<INPUT> pendingInputCleanup;
                            Helpers::SendTextInput(remapping, ii, pendingInputCleanup);
                            state.QueuePendingInputCleanup(std::move(pendingInputCleanup));
                            return 1;
                        }

                        // A repeat can fail without changing which side owns the physical
                        // press. Suppress it even when none of the repeated target events
                        // could be injected.
                        ii.SendVirtualInput(keyEventList);
                        return 1;
                    }

                    // Case 3: If the action key is released from the original shortcut, keep modifiers of the new shortcut until some other key event which doesn't apply to the original shortcut
                    if (!remapToText && ((!it->first.HasChord() && data->lParam->vkCode == it->first.GetActionKey()) || (it->first.HasChord() && data->lParam->vkCode == it->first.GetSecondKey())) && (data->wParam == WM_KEYUP || data->wParam == WM_SYSKEYUP))
                    {
                        std::vector<INPUT> keyEventList;
                        bool resetRemapStateAfterInput = false;
                        bool resetActivatedAppAfterInput = false;
                        if (remapToShortcut && !it->first.HasChord())
                        {
                            // Just lift the action key for no chords.
                            Helpers::SetKeyEvent(keyEventList, INPUT_KEYBOARD, static_cast<WORD>(std::get<Shortcut>(it->second.targetShortcut).GetActionKey()), KEYEVENTF_KEYUP, KeyboardManagerConstants::KEYBOARDMANAGER_SHORTCUT_FLAG);
                        }
                        else if (remapToShortcut && it->first.HasChord())
                        {
                            // If it has a chord, we'll want a full clean contemplated in the else, since you can't really repeat chords by pressing the end key again.

                            // Key up for all new shortcut keys, key down for original shortcut modifiers and current key press but common keys aren't repeated
                            Helpers::SetKeyEvent(keyEventList, INPUT_KEYBOARD, static_cast<WORD>(std::get<Shortcut>(it->second.targetShortcut).GetActionKey()), KEYEVENTF_KEYUP, KeyboardManagerConstants::KEYBOARDMANAGER_SHORTCUT_FLAG);

                            // Release new shortcut state (release in reverse order of shortcut to be accurate)
                            Helpers::SetModifierKeyEvents(std::get<Shortcut>(it->second.targetShortcut), it->second.modifierKeysInvoked, keyEventList, false, KeyboardManagerConstants::KEYBOARDMANAGER_SHORTCUT_FLAG, it->first);

                            // Set old shortcut key down state
                            Helpers::SetModifierKeyEvents(it->first, it->second.modifierKeysInvoked, keyEventList, true, KeyboardManagerConstants::KEYBOARDMANAGER_SHORTCUT_FLAG, std::get<Shortcut>(it->second.targetShortcut));

                            resetRemapStateAfterInput = true;
                            resetActivatedAppAfterInput = activatedApp.has_value();
                        }
                        else if (std::get<DWORD>(it->second.targetShortcut) == CommonSharedConstants::VK_DISABLED)
                        {
                            // If remapped to disable, do nothing and suppress the key event
                            // Since the original shortcut's action key is released, set it to false
                            it->second.isOriginalActionKeyPressed = false;
                            return 1;
                        }
                        else
                        {
                            // Check if the keyboard state is clear apart from the target remap key (by creating a temp Shortcut object with the target key)
                            bool isKeyboardStateClear = Shortcut(std::vector<int32_t>({ Helpers::FilterArtificialKeys(std::get<DWORD>(it->second.targetShortcut)) })).IsKeyboardStateClearExceptShortcut(ii);

                            // If the keyboard state is clear, we release the target key but do not reset the remap state
                            if (isKeyboardStateClear)
                            {
                                Helpers::SetKeyEvent(keyEventList, INPUT_KEYBOARD, static_cast<WORD>(Helpers::FilterArtificialKeys(std::get<DWORD>(it->second.targetShortcut))), KEYEVENTF_KEYUP, KeyboardManagerConstants::KEYBOARDMANAGER_SHORTCUT_FLAG);
                            }
                            else
                            {
                                // If any other key is pressed, then the keyboard state must be reverted back to the physical keys.
                                // This is to take cases like Ctrl+A->D remap and user presses B+Ctrl+A and releases A, or Ctrl+A+B and releases A

                                // Release new key state
                                Helpers::SetKeyEvent(keyEventList, INPUT_KEYBOARD, static_cast<WORD>(Helpers::FilterArtificialKeys(std::get<DWORD>(it->second.targetShortcut))), KEYEVENTF_KEYUP, KeyboardManagerConstants::KEYBOARDMANAGER_SHORTCUT_FLAG);

                                if (!isAltRightKeyInvoked)
                                {
                                    // Set original shortcut key down state except the action key
                                    Helpers::SetModifierKeyEvents(it->first, it->second.modifierKeysInvoked, keyEventList, true, KeyboardManagerConstants::KEYBOARDMANAGER_SHORTCUT_FLAG);
                                }

                                // Send a dummy key event to prevent modifier press+release from being triggered. Example: Win+A->V, press Shift+Win+A and release A, since Win will be pressed here we need to send a dummy event after it
                                Helpers::SetDummyKeyEvent(keyEventList, KeyboardManagerConstants::KEYBOARDMANAGER_SHORTCUT_FLAG);

                                if (!isAltRightKeyInvoked)
                                {
                                    resetRemapStateAfterInput = true;
                                }

                                resetActivatedAppAfterInput = activatedApp.has_value();
                            }
                        }

                        const auto releaseResult = ii.SendVirtualInput(keyEventList);
                        if (releaseResult.status == KeyboardManagerInput::SendVirtualInputStatus::Partial)
                        {
                            BestEffortReleaseInjectedPrefix(ii, state, keyEventList, releaseResult.injectedEventCount);
                        }
                        if (!releaseResult.IsComplete())
                        {
                            return 1;
                        }

                        if (resetRemapStateAfterInput)
                        {
                            it->second.isShortcutInvoked = false;
                            it->second.modifierKeysInvoked.Reset();
                            it->second.isOriginalActionKeyPressed = false;
                        }
                        if (resetActivatedAppAfterInput)
                        {
                            state.SetActivatedApp(KeyboardManagerConstants::NoActivatedApp);
                        }
                        return 1;
                    }

                    // Case 4: If a modifier key in the original shortcut is pressed then suppress that key event since the original shortcut is already held down physically - This case can occur only if a user has a duplicated modifier key (possibly by remapping) or if user presses both L/R versions of a modifier remapped with "Both"
                    if ((it->first.CheckWinKey(data->lParam->vkCode) || it->first.CheckCtrlKey(data->lParam->vkCode) || it->first.CheckAltKey(data->lParam->vkCode) || it->first.CheckShiftKey(data->lParam->vkCode)) && (data->wParam == WM_KEYDOWN || data->wParam == WM_SYSKEYDOWN))
                    {
                        if (remapToShortcut)
                        {
                            // Modifier state reset might be required for this key depending on the target shortcut action key - ex: Ctrl+A -> Win+Caps
                            if (std::get<Shortcut>(it->second.targetShortcut).GetCtrlKey(it->second.modifierKeysInvoked.ctrlKey) == NULL && std::get<Shortcut>(it->second.targetShortcut).GetAltKey(it->second.modifierKeysInvoked.altKey) == NULL && std::get<Shortcut>(it->second.targetShortcut).GetShiftKey(it->second.modifierKeysInvoked.shiftKey) == NULL)
                            {
                                ResetIfModifierKeyForLowerLevelKeyHandlers(ii, data->lParam->vkCode, std::get<Shortcut>(it->second.targetShortcut).GetActionKey());
                            }
                        }
                        else if (std::get<DWORD>(it->second.targetShortcut) != CommonSharedConstants::VK_DISABLED)
                        {
                            // If it is not remapped to Disable
                            // Modifier state reset might be required for this key depending on the target key - ex: Ctrl+A -> Caps
                            ResetIfModifierKeyForLowerLevelKeyHandlers(ii, data->lParam->vkCode, Helpers::FilterArtificialKeys(std::get<DWORD>(it->second.targetShortcut)));
                        }

                        // Suppress the modifier as it is already physically pressed
                        return 1;
                    }

                    // Case 5: If any key apart from the action key or a modifier key in the original shortcut is pressed then revert the keyboard state to just the original modifiers being held down along with the current key press
                    if (data->wParam == WM_KEYDOWN || data->wParam == WM_SYSKEYDOWN)
                    {
                        if (remapToShortcut)
                        {
                            const RemapShortcut previousRemapState = it->second;
                            const std::wstring previousActivatedApp = state.GetActivatedApp();
                            RemapShortcut* newlyInvokedRemapping = nullptr;
                            std::optional<RemapShortcut> previousNewRemapState;

                            // Modifier state reset might be required for this key depending on the target shortcut action key - ex: Ctrl+A -> Win+Caps, Shift is pressed. System should not see Shift and Caps pressed together
                            if (std::get<Shortcut>(it->second.targetShortcut).GetCtrlKey(it->second.modifierKeysInvoked.ctrlKey) == NULL && std::get<Shortcut>(it->second.targetShortcut).GetAltKey(it->second.modifierKeysInvoked.altKey) == NULL && std::get<Shortcut>(it->second.targetShortcut).GetShiftKey(it->second.modifierKeysInvoked.shiftKey) == NULL)
                            {
                                ResetIfModifierKeyForLowerLevelKeyHandlers(ii, data->lParam->vkCode, std::get<Shortcut>(it->second.targetShortcut).GetActionKey());
                            }

                            std::vector<INPUT> keyEventList;

                            // Check if a new remapping should be applied
                            Shortcut currentlyPressed = it->first;
                            currentlyPressed.actionKey = data->lParam->vkCode;
                            auto newRemappingIter = allowNewRemappings ? reMap.find(currentlyPressed) : reMap.end();
                            if (newRemappingIter != reMap.end() && !newRemappingIter->first.HasChord())
                            {
                                auto& newRemapping = newRemappingIter->second;
                                Shortcut from = std::get<Shortcut>(it->second.targetShortcut);
                                if (newRemapping.RemapToKey())
                                {
                                    DWORD to = std::get<0>(newRemapping.targetShortcut);
                                    if (!isAltRightKeyInvoked)
                                    {
                                        Helpers::SetModifierKeyEvents(from, it->second.modifierKeysInvoked, keyEventList, false, KeyboardManagerConstants::KEYBOARDMANAGER_SHORTCUT_FLAG);
                                    }
                                    if (ii.GetVirtualKeyState(static_cast<WORD>(from.actionKey)))
                                    {
                                        // If the action key from the last shortcut is still being pressed, release it.
                                        Helpers::SetKeyEvent(keyEventList, INPUT_KEYBOARD, static_cast<WORD>(from.actionKey), KEYEVENTF_KEYUP, KeyboardManagerConstants::KEYBOARDMANAGER_SHORTCUT_FLAG);
                                    }
                                    Helpers::SetKeyEvent(keyEventList, INPUT_KEYBOARD, static_cast<WORD>(to), 0, KeyboardManagerConstants::KEYBOARDMANAGER_SHORTCUT_FLAG);
                                }
                                else
                                {
                                    Shortcut to = std::get<Shortcut>(newRemapping.targetShortcut);
                                    if (!isAltRightKeyInvoked)
                                    {
                                        Helpers::SetModifierKeyEvents(from, it->second.modifierKeysInvoked, keyEventList, false, KeyboardManagerConstants::KEYBOARDMANAGER_SHORTCUT_FLAG, to);
                                    }
                                    if (ii.GetVirtualKeyState(static_cast<WORD>(from.actionKey)))
                                    {
                                        // If the action key from the last shortcut is still being pressed, release it.
                                        Helpers::SetKeyEvent(keyEventList, INPUT_KEYBOARD, static_cast<WORD>(from.actionKey), KEYEVENTF_KEYUP, KeyboardManagerConstants::KEYBOARDMANAGER_SHORTCUT_FLAG);
                                    }
                                    if (!isAltRightKeyInvoked)
                                    {
                                        Helpers::SetModifierKeyEvents(to, it->second.modifierKeysInvoked, keyEventList, true, KeyboardManagerConstants::KEYBOARDMANAGER_SHORTCUT_FLAG, from);
                                    }
                                    Helpers::SetKeyEvent(keyEventList, INPUT_KEYBOARD, static_cast<WORD>(to.actionKey), 0, KeyboardManagerConstants::KEYBOARDMANAGER_SHORTCUT_FLAG);
                                    newlyInvokedRemapping = &newRemapping;
                                    previousNewRemapState = newRemapping;
                                    newRemapping.isShortcutInvoked = true;
                                }
                            }
                            else
                            {
                                // If the target shortcut's action key is pressed, then it should be released and original shortcut's action key should be set
                                bool isActionKeyPressed = ii.GetVirtualKeyState(std::get<Shortcut>(it->second.targetShortcut).GetActionKey());

                                // Release new shortcut state (release in reverse order of shortcut to be accurate)
                                if (isActionKeyPressed)
                                {
                                    Helpers::SetKeyEvent(keyEventList, INPUT_KEYBOARD, static_cast<WORD>(std::get<Shortcut>(it->second.targetShortcut).GetActionKey()), KEYEVENTF_KEYUP, KeyboardManagerConstants::KEYBOARDMANAGER_SHORTCUT_FLAG);
                                }
                                if (!isAltRightKeyInvoked)
                                {
                                    Helpers::SetModifierKeyEvents(std::get<Shortcut>(it->second.targetShortcut), it->second.modifierKeysInvoked, keyEventList, false, KeyboardManagerConstants::KEYBOARDMANAGER_SHORTCUT_FLAG, it->first);

                                    // Set old shortcut key down state
                                    Helpers::SetModifierKeyEvents(it->first, it->second.modifierKeysInvoked, keyEventList, true, KeyboardManagerConstants::KEYBOARDMANAGER_SHORTCUT_FLAG, std::get<Shortcut>(it->second.targetShortcut));
                                }

                                // key down for original shortcut action key with shortcut flag so that we don't invoke the same shortcut remap again
                                if (isActionKeyPressed)
                                {
                                    Helpers::SetKeyEvent(keyEventList, INPUT_KEYBOARD, static_cast<WORD>(it->first.GetActionKey()), 0, KeyboardManagerConstants::KEYBOARDMANAGER_SHORTCUT_FLAG);
                                }

                                // Send current key pressed without shortcut flag so that it can be reprocessed in case the physical keys pressed are a different remapped shortcut
                                Helpers::SetKeyEvent(keyEventList, INPUT_KEYBOARD, static_cast<WORD>(data->lParam->vkCode), 0, 0);

                                // Do not send a dummy key as we want the current key press to behave as normal i.e. it can do press+release functionality if required. Required to allow a shortcut to Win key remap invoked directly after shortcut to shortcut is released to open start menu
                            }

                            if (!isAltRightKeyInvoked)
                            {
                                // Reset the remap state
                                it->second.isShortcutInvoked = false;
                                it->second.modifierKeysInvoked.Reset();
                                it->second.isOriginalActionKeyPressed = false;
                            }

                            // If app specific shortcut has finished invoking, reset the target application
                            if (activatedApp)
                            {
                                state.SetActivatedApp(KeyboardManagerConstants::NoActivatedApp);
                            }

                            const auto transitionResult = ii.SendVirtualInput(keyEventList);
                            if (transitionResult.status == KeyboardManagerInput::SendVirtualInputStatus::Partial)
                            {
                                BestEffortReleaseInjectedPrefix(ii, state, keyEventList, transitionResult.injectedEventCount);
                            }
                            if (!transitionResult.IsComplete())
                            {
                                it->second = previousRemapState;
                                if (newlyInvokedRemapping && previousNewRemapState)
                                {
                                    *newlyInvokedRemapping = *previousNewRemapState;
                                }
                                state.SetActivatedApp(previousActivatedApp);
                                return transitionResult.status == KeyboardManagerInput::SendVirtualInputStatus::None ? 0 : 1;
                            }

                            if (newlyInvokedRemapping && activatedApp)
                            {
                                state.SetActivatedApp(*activatedApp);
                            }
                            return 1;
                        }
                        else
                        {
                            // For remap to key, if the original action key is not currently pressed, we should revert the keyboard state to the physical keys. If it is pressed we should not suppress the event so that shortcut to key remaps can be pressed with other keys. Example use-case: Alt+D->Win, allows Alt+D+A to perform Win+A

                            // Modifier state reset might be required for this key depending on the target key - ex: Ctrl+A -> Caps, Shift is pressed. System should not see Shift and Caps pressed together

                            auto maybeTargetKey = std::get_if<DWORD>(&it->second.targetShortcut);

                            if (maybeTargetKey)
                            {
                                ResetIfModifierKeyForLowerLevelKeyHandlers(ii, data->lParam->vkCode, Helpers::FilterArtificialKeys(*maybeTargetKey));
                            }

                            // If the shortcut is remapped to Disable then we have to revert the keyboard state to the physical keys
                            bool isRemapToDisable = maybeTargetKey && (*maybeTargetKey == CommonSharedConstants::VK_DISABLED);
                            bool isOriginalActionKeyPressed = false;

                            if (maybeTargetKey && !isRemapToDisable)
                            {
                                // If the remap target key is currently pressed, then we do not have to revert the keyboard state to the physical keys
                                if (ii.GetVirtualKeyState((Helpers::FilterArtificialKeys(*maybeTargetKey))))
                                {
                                    isOriginalActionKeyPressed = true;
                                }
                            }
                            else
                            {
                                isOriginalActionKeyPressed = it->second.isOriginalActionKeyPressed;
                            }

                            if (isRemapToDisable || !isOriginalActionKeyPressed)
                            {
                                const RemapShortcut previousRemapState = it->second;
                                const std::wstring previousActivatedApp = state.GetActivatedApp();
                                std::vector<INPUT> keyEventList;

                                if (!isAltRightKeyInvoked)
                                {
                                    // Set original shortcut key down state
                                    Helpers::SetModifierKeyEvents(it->first, it->second.modifierKeysInvoked, keyEventList, true, KeyboardManagerConstants::KEYBOARDMANAGER_SHORTCUT_FLAG);
                                }

                                // Send the original action key only if it is physically pressed. For remappings to keys other than disabled we already check earlier that it is not pressed in this scenario. For remap to disable
                                if (isRemapToDisable && isOriginalActionKeyPressed)
                                {
                                    // Set original action key
                                    Helpers::SetKeyEvent(keyEventList, INPUT_KEYBOARD, static_cast<WORD>(it->first.GetActionKey()), 0, KeyboardManagerConstants::KEYBOARDMANAGER_SHORTCUT_FLAG);
                                }

                                // Send current key pressed without shortcut flag so that it can be reprocessed in case the physical keys pressed are a different remapped shortcut
                                Helpers::SetKeyEvent(keyEventList, INPUT_KEYBOARD, static_cast<WORD>(data->lParam->vkCode), 0, 0);

                                // Do not send a dummy key as we want the current key press to behave as normal i.e. it can do press+release functionality if required. Required to allow a shortcut to Win key remap invoked directly after another shortcut to key remap is released to open start menu

                                if (!isAltRightKeyInvoked)
                                {
                                    // Reset the remap state
                                    it->second.isShortcutInvoked = false;
                                    it->second.modifierKeysInvoked.Reset();
                                    it->second.isOriginalActionKeyPressed = false;
                                }

                                // If app specific shortcut has finished invoking, reset the target application
                                if (activatedApp != KeyboardManagerConstants::NoActivatedApp)
                                {
                                    state.SetActivatedApp(KeyboardManagerConstants::NoActivatedApp);
                                }

                                const auto transitionResult = ii.SendVirtualInput(keyEventList);
                                if (transitionResult.status == KeyboardManagerInput::SendVirtualInputStatus::Partial)
                                {
                                    BestEffortReleaseInjectedPrefix(ii, state, keyEventList, transitionResult.injectedEventCount);
                                }
                                if (!transitionResult.IsComplete())
                                {
                                    it->second = previousRemapState;
                                    state.SetActivatedApp(previousActivatedApp);
                                    return transitionResult.status == KeyboardManagerInput::SendVirtualInputStatus::None ? 0 : 1;
                                }
                                return 1;
                            }
                            else
                            {
                                return 0;
                            }
                        }
                    }
                    // Case 6: If any key apart from original modifier or original action key is released - This can't happen since the key down would have to happen first, which is handled above. If a key up message is generated for some other key (maybe by code) do not suppress it
                }
            }
        }

        return 0;
    }

    std::wstring URL_encode(const std::wstring& filepath)
    {
        std::wostringstream escaped;
        escaped.fill('0');
        escaped << std::hex;

        for (wchar_t ch : filepath)
        {
            // Encode special characters except for colon after drive letter
            if (!iswalnum(ch) && ch != L'-' && ch != L'_' && ch != L'.' && ch != L'~' && !(ch == L':' && std::isalpha(filepath[0])))
            {
                escaped << std::uppercase;
                //escaped << '%' << std::setw(2) << int((unsigned char)ch);
                escaped << '%' << std::setw(2) << static_cast<int>((static_cast<unsigned char>(ch)));
                escaped << std::nouppercase;
            }
            else
            {
                escaped << ch;
            }
        }

        return escaped.str();
    }

    std::wstring ConvertPathToURI(const std::wstring& filePath)
    {
        std::wstring fileUri = std::filesystem::absolute(filePath).wstring();
        std::replace(fileUri.begin(), fileUri.end(), L'\\', L'/');
        fileUri = L"file:///" + URL_encode(fileUri);

        return fileUri;
    }

    void ResetAllOtherStartedChords(State& state, const std::optional<std::wstring>& activatedApp, DWORD keyToKeep)
    {
        for (auto& itShortcut_2 : state.GetSortedShortcutRemapVector(activatedApp))
        {
            if (keyToKeep == NULL || itShortcut_2.actionKey != keyToKeep)
            {
                itShortcut_2.SetChordStarted(false);
            }
        }
    }

    void ResetAllStartedChords(State& state, const std::optional<std::wstring>& activatedApp)
    {
        ResetAllOtherStartedChords(state, activatedApp, NULL);
    }

    ResetChordsResults ResetChordsIfNeeded(LowlevelKeyboardEvent* data, State& state, const std::optional<std::wstring>& activatedApp)
    {
        ResetChordsResults result;
        result.AnyChordStarted = false;
        result.CurrentKeyIsModifierKey = false;

        bool isNewControlKey = false;
        bool anyChordStarted = false;
        if (VK_LWIN == data->lParam->vkCode || VK_RWIN == data->lParam->vkCode)
        {
            isNewControlKey = true;
        }
        if (VK_LSHIFT == data->lParam->vkCode || VK_RSHIFT == data->lParam->vkCode)
        {
            isNewControlKey = true;
        }
        if (VK_LMENU == data->lParam->vkCode || VK_RMENU == data->lParam->vkCode)
        {
            isNewControlKey = true;
        }
        if (VK_LCONTROL == data->lParam->vkCode || VK_RCONTROL == data->lParam->vkCode)
        {
            isNewControlKey = true;
        }

        if (isNewControlKey)
        {
            //Logger::trace(L"ChordKeyboardHandler:reset");

            for (auto& itShortcut : state.GetSortedShortcutRemapVector(activatedApp))
            {
                itShortcut.SetChordStarted(false);
            }
            result.CurrentKeyIsModifierKey = true;
        }
        else
        {
            for (auto& itShortcut : state.GetSortedShortcutRemapVector(activatedApp))
            {
                if (itShortcut.IsChordStarted())
                {
                    result.AnyChordStarted = true;
                    break;
                }
            }
        }

        return result;
    }

    struct handle_data
    {
        unsigned long process_id;
        HWND window_handle;
    };

    // used for reactivating a window for a program we already started.
    HWND FindMainWindow(unsigned long process_id, const bool allowNonVisible)
    {
        handle_data data;
        data.process_id = process_id;
        data.window_handle = 0;

        if (allowNonVisible)
        {
            EnumWindows(EnumWindowsCallbackAllowNonVisible, reinterpret_cast<LPARAM>(&data));
        }
        else
        {
            EnumWindows(EnumWindowsCallback, reinterpret_cast<LPARAM>(&data));
        }

        return data.window_handle;
    }

    // used by FindMainWindow
    BOOL CALLBACK EnumWindowsCallbackAllowNonVisible(HWND handle, LPARAM lParam)
    {
        handle_data& data = *reinterpret_cast<handle_data*>(lParam);
        unsigned long process_id = 0;
        GetWindowThreadProcessId(handle, &process_id);

        if (data.process_id == process_id)
        {
            data.window_handle = handle;
            return FALSE;
        }
        return TRUE;
    }

    // used by FindMainWindow
    BOOL CALLBACK EnumWindowsCallback(HWND handle, LPARAM lParam)
    {
        handle_data& data = *reinterpret_cast<handle_data*>(lParam);
        unsigned long process_id = 0;
        GetWindowThreadProcessId(handle, &process_id);

        if (data.process_id != process_id || !(GetWindow(handle, GW_OWNER) == static_cast<HWND>(0) && IsWindowVisible(handle)))
        {
            return TRUE;
        }

        data.window_handle = handle;
        return FALSE;
    }

    // GetProcessIdByName also used by HandleCreateProcessHotKeysAndChords

    std::vector<DWORD> GetProcessesIdByName(const std::wstring& processName)
    {
        std::vector<DWORD> processIds;
        HANDLE snapshot = CreateToolhelp32Snapshot(TH32CS_SNAPPROCESS, 0);

        if (snapshot != INVALID_HANDLE_VALUE)
        {
            PROCESSENTRY32 processEntry;
            processEntry.dwSize = sizeof(PROCESSENTRY32);

            if (Process32First(snapshot, &processEntry))
            {
                do
                {
                    if (_wcsicmp(processEntry.szExeFile, processName.c_str()) == 0)
                    {
                        processIds.push_back(processEntry.th32ProcessID);
                    }
                } while (Process32Next(snapshot, &processEntry));
            }

            CloseHandle(snapshot);
        }

        return processIds;
    }

    DWORD GetProcessIdByName(const std::wstring& processName)
    {
        DWORD pid = 0;
        HANDLE snapshot = CreateToolhelp32Snapshot(TH32CS_SNAPPROCESS, 0);

        if (snapshot != INVALID_HANDLE_VALUE)
        {
            PROCESSENTRY32 processEntry;
            processEntry.dwSize = sizeof(PROCESSENTRY32);

            if (Process32First(snapshot, &processEntry))
            {
                do
                {
                    if (_wcsicmp(processEntry.szExeFile, processName.c_str()) == 0)
                    {
                        pid = processEntry.th32ProcessID;
                        break;
                    }
                } while (Process32Next(snapshot, &processEntry));
            }

            CloseHandle(snapshot);
        }

        return pid;
    }

    // Use to find a process by its name
    std::wstring GetFileNameFromPath(const std::wstring& fullPath)
    {
        size_t found = fullPath.find_last_of(L"\\");
        if (found != std::wstring::npos)
        {
            return fullPath.substr(found + 1);
        }
        return fullPath;
    }

    void toast(param::hstring const& message1, param::hstring const& message2) noexcept
    {
        try
        {
            // Alternatively can build DOM from code:
            XmlDocument toastXml;
            XmlElement toastElement = toastXml.CreateElement(L"toast");
            XmlElement visualElement = toastXml.CreateElement(L"visual");
            XmlElement bindingElement = toastXml.CreateElement(L"binding");
            XmlElement textElement1 = toastXml.CreateElement(L"text");
            XmlElement textElement2 = toastXml.CreateElement(L"text");

            toastXml.AppendChild(toastElement);
            toastElement.AppendChild(visualElement);
            visualElement.AppendChild(bindingElement);

            bindingElement.AppendChild(textElement1);
            bindingElement.AppendChild(textElement2);

            bindingElement.SetAttribute(L"template", L"ToastGeneric");

            textElement1.InnerText(message1);
            textElement2.InnerText(message2);

            Logger::trace(L"ChordKeyboardHandler:toastXml {}", toastXml.GetXml());
            std::wstring APPLICATION_ID = L"Microsoft.PowerToysWin32";
            const auto notifier = ToastNotificationManager::ToastNotificationManager::CreateToastNotifier(APPLICATION_ID);

            ToastNotification notification{ toastXml };
            notifier.Show(notification);
        }
        catch (...)
        {
        }

        /*std::thread{ [message] {
    
        } }.detach();*/
    }

    void CreateOrShowProcessForShortcut(Shortcut shortcut) noexcept
    {
        WCHAR fullExpandedFilePath[MAX_PATH];
        DWORD result = ExpandEnvironmentStrings(shortcut.runProgramFilePath.c_str(), fullExpandedFilePath, MAX_PATH);

        auto fileNamePart = GetFileNameFromPath(fullExpandedFilePath);

        Logger::trace(L"ChordKeyboardHandler:{}, trying to run {}", fileNamePart, fullExpandedFilePath);
        //lastKeyInChord = 0;

        DWORD targetPid = GetProcessIdByName(fileNamePart);

        /*if (fileNamePart != L"explorer.exe" && fileNamePart != L"powershell.exe" && fileNamePart != L"cmd.exe" && fileNamePart != L"msedge.exe")
        {
            targetPid = GetProcessIdByName(fileNamePart);
        }*/

        Logger::trace(L"ChordKeyboardHandler:{}, already running, pid:{}, alreadyRunningAction:{}", fileNamePart, targetPid, shortcut.alreadyRunningAction);

        if (targetPid != 0 && shortcut.alreadyRunningAction != Shortcut::ProgramAlreadyRunningAction::StartAnother)
        {
            if (shortcut.alreadyRunningAction == Shortcut::ProgramAlreadyRunningAction::EndTask)
            {
                TerminateProcessesByName(fileNamePart);
                return;
            }
            else if (shortcut.alreadyRunningAction == Shortcut::ProgramAlreadyRunningAction::Close)
            {
                CloseProcessByName(fileNamePart);
                Logger::trace(L"ChordKeyboardHandler:{}, CloseProcessByName returning 3", fileNamePart);
                return;
            }
            else if (shortcut.alreadyRunningAction == Shortcut::ProgramAlreadyRunningAction::ShowWindow)
            {
                auto processIds = GetProcessesIdByName(fileNamePart);

                for (DWORD pid : processIds)
                {
                    ShowProgram(targetPid, fileNamePart, false, false, 0);
                }

                //if (!ShowProgram(targetPid, fileNamePart, false, false, 0))
                //{
                //    /*auto future = std::async(std::launch::async, [=] {
                //    std::this_thread::sleep_for(std::chrono::milliseconds(30));
                //    Logger::trace(L"ChordKeyboardHandler:{}, second try, pid:{}", fileNamePart, targetPid);
                //    ShowProgram(targetPid, fileNamePart, false, false);
                //});*/
                //}
                return;
            }
        }
        else
        {
            DWORD dwAttrib = GetFileAttributesW(fullExpandedFilePath);

            if (dwAttrib == INVALID_FILE_ATTRIBUTES)
            {
                std::wstring title = fmt::format(L"Error starting {}", fileNamePart);
                std::wstring message = fmt::format(L"The program was not found.");
                toast(title, message);
                return;
            }

            std::wstring expandedArgs;
            DWORD dwSize = ExpandEnvironmentStrings(shortcut.runProgramArgs.c_str(), nullptr, 0);
            expandedArgs.resize(dwSize);
            DWORD result = ExpandEnvironmentStrings(shortcut.runProgramArgs.c_str(), expandedArgs.data(), dwSize);

            WCHAR currentDir[MAX_PATH];
            WCHAR* currentDirPtr = currentDir;
            result = ExpandEnvironmentStrings(shortcut.runProgramStartInDir.c_str(), currentDir, MAX_PATH);

            if (shortcut.runProgramStartInDir == L"")
            {
                currentDirPtr = nullptr;
            }
            else
            {
                DWORD dwAttrib = GetFileAttributesW(currentDir);

                if (dwAttrib == INVALID_FILE_ATTRIBUTES)
                {
                    std::wstring title = fmt::format(L"Error starting {}", fileNamePart);
                    std::wstring message = fmt::format(L"The start in path was not valid. It could not be used.", currentDir);
                    currentDirPtr = nullptr;
                    toast(title, message);
                    return;
                }
            }

            DWORD processId = 0;
            HANDLE newProcessHandle;

            if (shortcut.elevationLevel == Shortcut::ElevationLevel::Elevated)
            {
                newProcessHandle = run_elevated(fullExpandedFilePath, expandedArgs, currentDirPtr, (shortcut.startWindowType == Shortcut::StartWindowType::Normal));
                processId = GetProcessId(newProcessHandle);
            }
            else if (shortcut.elevationLevel == Shortcut::ElevationLevel::NonElevated)
            {
                run_non_elevated(fullExpandedFilePath, expandedArgs, &processId, currentDirPtr, (shortcut.startWindowType == Shortcut::StartWindowType::Normal));
            }
            else if (shortcut.elevationLevel == Shortcut::ElevationLevel::DifferentUser)
            {
                newProcessHandle = run_as_different_user(fullExpandedFilePath, expandedArgs, currentDirPtr, (shortcut.startWindowType == Shortcut::StartWindowType::Normal));
                processId = GetProcessId(newProcessHandle);
            }

            if (processId == 0)
            {
                std::wstring title = fmt::format(L"Error starting {}", fileNamePart);
                std::wstring message = fmt::format(L"The application might not have started.");
                toast(title, message);
                return;
            }

            if (shortcut.startWindowType == Shortcut::StartWindowType::Hidden)
            {
                HideProgram(processId, fileNamePart, 0);
            }
            //ShowProgram(processId, fileNamePart, true, false, (shortcut.startWindowType == Shortcut::StartWindowType::Hidden), 0);
        }
        return;
    }

    void CloseProcessByName(const std::wstring& fileNamePart)
    {
        auto processIds = GetProcessesIdByName(fileNamePart);

        if (processIds.size() == 0)
        {
            Logger::trace(L"ChordKeyboardHandler:{}, Nothing To WM_CLOSE", fileNamePart);
            return;
        }

        auto threadFunction = [fileNamePart]() {
            auto processIds = GetProcessesIdByName(fileNamePart);
            auto retryCount = 10;
            while (processIds.size() > 0 && retryCount-- > 0)
            {
                //Logger::trace(L"ChordKeyboardHandler:{}, WM_CLOSE 'ing {}processIds ", fileNamePart, processIds.size());
                for (DWORD pid : processIds)
                {
                    //Logger::trace(L"ChordKeyboardHandler:{}, WM_CLOSE ({}) -> pid:{}", fileNamePart, retryCount, pid);
                    HWND hwnd = FindMainWindow(pid, false);
                    SendMessage(hwnd, WM_CLOSE, 0, 0);

                    // small sleep between when there are a lot might help
                    Sleep(10);
                }

                processIds = GetProcessesIdByName(fileNamePart);
                if (processIds.size() <= 0)
                {
                    Logger::trace(L"ChordKeyboardHandler:{}, WM_CLOSE done", fileNamePart);
                    break;
                }
                else
                {
                    Sleep(100);
                }
            }
        };

        processIds = GetProcessesIdByName(fileNamePart);

        if (processIds.size() > 0)
        {
            std::thread myThread(threadFunction);
            if (myThread.joinable())
            {
                myThread.detach();
            }
        }

        Logger::trace(L"ChordKeyboardHandler:{}, CloseProcessByName returning", fileNamePart);
    }

    void TerminateProcessesByName(const std::wstring& fileNamePart)
    {
        auto processIds = GetProcessesIdByName(fileNamePart);

        if (processIds.size() == 0)
        {
            Logger::trace(L"ChordKeyboardHandler:{}, Nothing To PROCESS_TERMINATE", fileNamePart);
            return;
        }

        for (DWORD pid : processIds)
        {
            HANDLE hProcess = OpenProcess(PROCESS_TERMINATE, FALSE, pid);
            Logger::trace(L"ChordKeyboardHandler:{}, PROCESS_TERMINATE (1) -> pid:{}", fileNamePart, pid);
            if (hProcess != NULL)
            {
                if (!TerminateProcess(hProcess, 0))
                {
                    CloseHandle(hProcess);
                }
                else
                {
                    CloseHandle(hProcess);
                }
            }
        }
    }

    bool HideProgram(DWORD pid, std::wstring programName, int retryCount)
    {
        Logger::trace(L"ChordKeyboardHandler:HideProgram starting with {},{}, retryCount:{}", pid, programName, retryCount);

        HWND hwnd = FindMainWindow(pid, false);
        if (hwnd == NULL)
        {
            if (retryCount < 20)
            {
                Logger::trace(L"ChordKeyboardHandler:hwnd not found will retry for pid:{}", pid);
                auto future = std::async(std::launch::async, [=] {
                    std::this_thread::sleep_for(std::chrono::milliseconds(50));
                    auto result = HideProgram(pid, programName, retryCount + 1);
                    return false;
                });
            }
        }

        hwnd = FindWindow(nullptr, nullptr);

        auto anyHideResultFailed = false;

        Logger::trace(L"ChordKeyboardHandler:{}:{},{}, FindWindow, HideProgram (all)", programName, pid, retryCount);
        while (hwnd)
        {
            DWORD pidForHwnd;
            GetWindowThreadProcessId(hwnd, &pidForHwnd);
            if (pid == pidForHwnd)
            {
                if (IsWindowVisible(hwnd))
                {
                    ShowWindow(hwnd, SW_HIDE);
                    Logger::trace(L"ChordKeyboardHandler:{}, tryToHide {}, {}", programName, reinterpret_cast<uintptr_t>(hwnd), anyHideResultFailed);
                }
            }
            hwnd = FindWindowEx(NULL, hwnd, NULL, NULL);
        }

        return true;
    }

    bool ShowProgram(DWORD pid, std::wstring programName, bool isNewProcess, bool minimizeIfVisible, int retryCount)
    {
        Logger::trace(L"ChordKeyboardHandler:ShowProgram starting with {},{},isNewProcess:{}, tryToHide:{} retryCount:{}", pid, programName, isNewProcess, retryCount);

        // a good place to look for this...
        // https://github.com/ritchielawrence/cmdow

        // try by main window.
        auto allowNonVisible = false;

        HWND hwnd = FindMainWindow(pid, allowNonVisible);

        if (hwnd == NULL)
        {
            if (retryCount < 20)
            {
                Logger::trace(L"ChordKeyboardHandler:hwnd not found will retry for pid:{}, allowNonVisible:{}", pid, allowNonVisible);

                auto future = std::async(std::launch::async, [=] {
                    std::this_thread::sleep_for(std::chrono::milliseconds(50));
                    auto result = ShowProgram(pid, programName, isNewProcess, minimizeIfVisible, retryCount + 1);
                    return false;
                });
            }
        }
        else
        {
            Logger::trace(L"ChordKeyboardHandler:{}, got hwnd from FindMainWindow", programName);

            if (hwnd == GetForegroundWindow())
            {
                // only hide if this was a call from an already open program, don't make small if we just opened it.
                if (!isNewProcess && minimizeIfVisible)
                {
                    Logger::trace(L"ChordKeyboardHandler:{}, got GetForegroundWindow, doing SW_MINIMIZE", programName);
                    return ShowWindow(hwnd, SW_MINIMIZE);
                }
                return false;
            }
            else
            {
                Logger::trace(L"ChordKeyboardHandler:{}, not ForegroundWindow, doing SW_RESTORE", programName);

                // Check if the window is minimized
                if (IsIconic(hwnd))
                {
                    // Show the window since SetForegroundWindow fails on minimized windows
                    if (!ShowWindow(hwnd, SW_RESTORE))
                    {
                        Logger::error(L"ShowWindow failed");
                    }
                }

                INPUT inputs[1] = { { .type = INPUT_MOUSE } };
                SendInput(ARRAYSIZE(inputs), inputs, sizeof(INPUT));

                if (!SetForegroundWindow(hwnd))
                {
                    auto errorCode = GetLastError();
                    Logger::warn(L"ChordKeyboardHandler:{}, failed to SetForegroundWindow, {}", programName, errorCode);
                    return false;
                }
                else
                {
                    Logger::trace(L"ChordKeyboardHandler:{}, success on SetForegroundWindow", programName);
                    return true;
                }
            }
        }

        if (isNewProcess)
        {
            return true;
        }

        if (false)
        {
            // try by console.
            hwnd = FindWindow(nullptr, nullptr);
            if (AttachConsole(pid))
            {
                Logger::trace(L"ChordKeyboardHandler:{}, success on AttachConsole", programName);

                // Get the console window handle
                hwnd = GetConsoleWindow();
                auto showByConsoleSuccess = false;
                if (hwnd != NULL)
                {
                    Logger::trace(L"ChordKeyboardHandler:{}, success on GetConsoleWindow, doing SW_RESTORE", programName);

                    ShowWindow(hwnd, SW_RESTORE);

                    if (!SetForegroundWindow(hwnd))
                    {
                        auto errorCode = GetLastError();
                        Logger::warn(L"ChordKeyboardHandler:{}, failed to SetForegroundWindow, {}", programName, errorCode);
                    }
                    else
                    {
                        Logger::trace(L"ChordKeyboardHandler:{}, success on SetForegroundWindow", programName);
                        showByConsoleSuccess = true;
                    }
                }

                // Detach from the console
                FreeConsole();
                if (showByConsoleSuccess)
                {
                    return true;
                }
            }
        }

        // try to just show them all (if they have a title)!.
        hwnd = FindWindow(nullptr, nullptr);

        auto anyHideResultFailed = false;
        if (hwnd)
        {
            Logger::trace(L"ChordKeyboardHandler:{}:{},{}, FindWindow (show all mode)", programName, pid, retryCount);
            while (hwnd)
            {
                DWORD pidForHwnd;
                GetWindowThreadProcessId(hwnd, &pidForHwnd);
                if (pid == pidForHwnd)
                {
                    int length = GetWindowTextLength(hwnd);

                    if (length > 0)
                    {
                        ShowWindow(hwnd, SW_RESTORE);

                        // hwnd is the window handle with targetPid
                        if (SetForegroundWindow(hwnd))
                        {
                            Logger::trace(L"ChordKeyboardHandler:{}, success on SetForegroundWindow", programName);
                            return true;
                        }
                        else
                        {
                            auto errorCode = GetLastError();
                            Logger::warn(L"ChordKeyboardHandler:{}, failed to SetForegroundWindow, {}", programName, errorCode);
                        }
                    }
                }
                hwnd = FindWindowEx(NULL, hwnd, NULL, NULL);
            }
        }

        return false;
    }

    // Function to handle an os-level shortcut remap
    intptr_t HandleOSLevelShortcutRemapEvent(KeyboardManagerInput::InputInterface& ii, LowlevelKeyboardEvent* data, State& state) noexcept
    {
        // Check if the key event was generated by KeyboardManager to avoid remapping events generated by us.
        if (data->lParam->dwExtraInfo != KeyboardManagerConstants::KEYBOARDMANAGER_SHORTCUT_FLAG)
        {
            bool result = HandleShortcutRemapEvent(ii, data, state);
            return result;
        }

        return 0;
    }

    // Function to handle an app-specific shortcut remap
    intptr_t HandleAppSpecificShortcutRemapEvent(KeyboardManagerInput::InputInterface& ii, LowlevelKeyboardEvent* data, State& state) noexcept
    {
        // Check if the key event was generated by KeyboardManager to avoid remapping events generated by us.
        if (data->lParam->dwExtraInfo != KeyboardManagerConstants::KEYBOARDMANAGER_SHORTCUT_FLAG)
        {
            std::wstring process_name;

            // Allocate MAX_PATH amount of memory
            process_name.resize(MAX_PATH);
            ii.GetForegroundProcess(process_name);

            // Remove elements after null character
            process_name.erase(std::find(process_name.begin(), process_name.end(), L'\0'), process_name.end());

            if (process_name.empty())
            {
                return 0;
            }

            // Convert process name to lowercase
            std::transform(process_name.begin(), process_name.end(), process_name.begin(), towlower);

            std::wstring query_string;

            AppSpecificShortcutRemapTable::iterator it;

            // Check if an app-specific shortcut is already activated
            if (state.GetActivatedApp() == KeyboardManagerConstants::NoActivatedApp)
            {
                query_string = process_name;
                it = state.appSpecificShortcutReMap.find(query_string);

                // If no entry is found, search for the process name without its file extension
                if (it == state.appSpecificShortcutReMap.end())
                {
                    // Find index of the file extension
                    size_t extensionIndex = process_name.find_last_of(L".");
                    query_string = process_name.substr(0, extensionIndex);
                    it = state.appSpecificShortcutReMap.find(query_string);
                }
            }
            else
            {
                query_string = state.GetActivatedApp();
                it = state.appSpecificShortcutReMap.find(query_string);
            }

            if (it != state.appSpecificShortcutReMap.end())
            {
                bool result = HandleShortcutRemapEvent(ii, data, state, query_string);
                return result;
            }
        }

        return 0;
    }

    intptr_t HandleActiveSingleKeyRemapEvent(KeyboardManagerInput::InputInterface& ii, LowlevelKeyboardEvent* data, State& state) noexcept
    {
        if (GeneratedByKBM(data))
        {
            return 0;
        }

        UpdateNumpadWithShift(data, state);
        if (state.HasSingleKeyRemapPressState(data->lParam->vkCode))
        {
            return HandleSingleKeyRemapEventCore(ii, data, state, false);
        }

        RetryPendingSingleKeyRemapReleases(ii, state);
        return 0;
    }

    intptr_t HandleActiveShortcutRemapEvent(KeyboardManagerInput::InputInterface& ii, LowlevelKeyboardEvent* data, State& state, const std::optional<std::wstring>& activatedApp) noexcept
    {
        // Shortcut-generated events must not feed the shortcut layer again. Events from a
        // single-key remap are intentionally allowed so an active chained shortcut sees
        // the generated action-key up and can release its target.
        if (data->lParam->dwExtraInfo == KeyboardManagerConstants::KEYBOARDMANAGER_SHORTCUT_FLAG)
        {
            return 0;
        }

        if (activatedApp)
        {
            const auto appTable = state.appSpecificShortcutReMap.find(*activatedApp);
            if (appTable == state.appSpecificShortcutReMap.end() ||
                std::none_of(appTable->second.begin(), appTable->second.end(), [](const auto& mapping) { return mapping.second.isShortcutInvoked; }))
            {
                return 0;
            }
            return HandleShortcutRemapEvent(ii, data, state, activatedApp, false);
        }

        if (std::none_of(state.osLevelShortcutReMap.begin(), state.osLevelShortcutReMap.end(), [](const auto& mapping) { return mapping.second.isShortcutInvoked; }))
        {
            return 0;
        }
        return HandleShortcutRemapEvent(ii, data, state, std::nullopt, false);
    }

    intptr_t HandleActiveRemapEvent(KeyboardManagerInput::InputInterface& ii, LowlevelKeyboardEvent* data, State& state) noexcept
    {
        if (HandleActiveSingleKeyRemapEvent(ii, data, state) == 1)
        {
            return 1;
        }

        const bool isKeyUp = data->wParam == WM_KEYUP || data->wParam == WM_SYSKEYUP;
        bool eventSuppressed = false;
        const std::wstring activatedApp = state.GetActivatedApp();
        if (activatedApp != KeyboardManagerConstants::NoActivatedApp)
        {
            eventSuppressed = HandleActiveShortcutRemapEvent(ii, data, state, activatedApp) == 1;
            if (eventSuppressed && !isKeyUp)
            {
                return 1;
            }
        }

        const bool osEventSuppressed = HandleActiveShortcutRemapEvent(ii, data, state) == 1;
        return eventSuppressed || osEventSuppressed ? 1 : 0;
    }

    // Function to ensure Ctrl/Shift/Alt modifier key state is not detected as pressed down by applications which detect keys at a lower level than hooks when it is remapped for scenarios where its required
    KeyboardManagerInput::SendVirtualInputResult ResetIfModifierKeyForLowerLevelKeyHandlers(KeyboardManagerInput::InputInterface& ii, DWORD key, DWORD target)
    {
        // If the target is Caps Lock and the other key is either Ctrl/Alt/Shift then reset the modifier state to lower level handlers
        if (target == VK_CAPITAL)
        {
            // If the argument is either of the Ctrl/Shift/Alt modifier key codes
            if (Helpers::IsModifierKey(key) && !(key == VK_LWIN || key == VK_RWIN || key == CommonSharedConstants::VK_WIN_BOTH))
            {
                std::vector<INPUT> keyEventList;

                // Use the suppress flag to ensure these are not intercepted by any remapped keys or shortcuts
                Helpers::SetKeyEvent(keyEventList, INPUT_KEYBOARD, static_cast<WORD>(key), KEYEVENTF_KEYUP, KeyboardManagerConstants::KEYBOARDMANAGER_SUPPRESS_FLAG);
                return ii.SendVirtualInput(keyEventList);
            }
        }
        return { KeyboardManagerInput::SendVirtualInputStatus::Complete, 0 };
    }

    // Function to generate a unicode string in response to a single keypress
    intptr_t HandleSingleKeyToTextRemapEvent(KeyboardManagerInput::InputInterface& ii, LowlevelKeyboardEvent* data, State& state)
    {
        if (GeneratedByKBM(data))
        {
            return 0;
        }

        // Only send the text on key-down events. WM_SYSKEYDOWN is sent instead of
        // WM_KEYDOWN while Alt is held, so accept it too or the remap silently drops.
        if (data->wParam != WM_KEYDOWN && data->wParam != WM_SYSKEYDOWN)
        {
            return 0;
        }

        const auto remapping = state.GetSingleKeyToTextRemapEvent(data->lParam->vkCode);
        if (!remapping)
        {
            return 0;
        }

        // Release held modifiers before text injection to prevent Ctrl+text corruption
        constexpr int modifierKeys[] = { VK_LCONTROL, VK_RCONTROL, VK_LSHIFT, VK_RSHIFT, VK_LMENU, VK_RMENU, VK_LWIN, VK_RWIN };
        std::vector<INPUT> releaseEvents;

        // A dummy key event must precede the modifier releases so that releasing a
        // held Win (Start Menu) or Alt (menu bar) does not trigger its lone-press
        // action when we inject the modifier key-up.
        Helpers::SetDummyKeyEvent(releaseEvents, KeyboardManagerConstants::KEYBOARDMANAGER_SHORTCUT_FLAG);

        bool anyModifierHeld = false;
        for (int vk : modifierKeys)
        {
            if (ii.GetVirtualKeyState(vk))
            {
                Helpers::SetKeyEvent(releaseEvents, INPUT_KEYBOARD, static_cast<WORD>(vk), KEYEVENTF_KEYUP, KeyboardManagerConstants::KEYBOARDMANAGER_SHORTCUT_FLAG);
                anyModifierHeld = true;
            }
        }

        // Only inject the dummy + modifier releases when a modifier was actually held.
        if (anyModifierHeld)
        {
            const auto releaseResult = ii.SendVirtualInput(releaseEvents);
            if (releaseResult.status == KeyboardManagerInput::SendVirtualInputStatus::Partial)
            {
                BestEffortReleaseInjectedPrefix(ii, state, releaseEvents, releaseResult.injectedEventCount);
            }
            if (releaseResult.status == KeyboardManagerInput::SendVirtualInputStatus::None)
            {
                return 0;
            }
            if (!releaseResult.IsComplete())
            {
                return 1;
            }
        }

        std::vector<INPUT> pendingInputCleanup;
        const auto textResult = Helpers::SendTextInput(*remapping, ii, pendingInputCleanup);
        state.QueuePendingInputCleanup(std::move(pendingInputCleanup));
        if (!anyModifierHeld && textResult.status == KeyboardManagerInput::SendVirtualInputStatus::None)
        {
            // No modifier release and no target text reached the system, so the original
            // single key can safely pass through.
            return 0;
        }

        // Intentionally do NOT re-press the released modifiers. Once we inject a
        // KEYUP for a modifier, GetAsyncKeyState (and therefore GetVirtualKeyState)
        // reports it as up, so there is no reliable way to tell whether the user is
        // still physically holding the key or has released it. Re-pressing
        // unconditionally would risk leaving a modifier stuck down if the user let
        // go during injection — the exact failure this change set prevents. Leaving
        // the modifier released is always safe: the user taps it again to re-engage.

        return 1;
    }

    intptr_t HandleTextReplacementSuppressedKeyEvent(LowlevelKeyboardEvent* data, State& state) noexcept
    {
        if (GeneratedByKBM(data))
        {
            return 0;
        }

        // Pair the exact physical key identity so main Enter and numpad Enter do
        // not consume each other's repeat or key-up.
        const DWORD vkCode = data->lParam->vkCode;
        const bool isKeyUp = data->wParam == WM_KEYUP || data->wParam == WM_SYSKEYUP;
        if (isKeyUp && IsTextReplacementTriggerKey(Helpers::ClearKeyNumpadOrigin(vkCode)))
        {
            state.textReplacementTriggerKeysDown.erase(vkCode);
        }

        if (const auto suppressedKey = state.textReplacementSuppressedTriggerKeys.find(vkCode);
            suppressedKey != state.textReplacementSuppressedTriggerKeys.end())
        {
            if (isKeyUp)
            {
                state.textReplacementSuppressedTriggerKeys.erase(suppressedKey);
                return 1;
            }

            if (data->wParam == WM_KEYDOWN || data->wParam == WM_SYSKEYDOWN)
            {
                return 1;
            }
        }

        return 0;
    }

    intptr_t HandleTextReplacementEvent(KeyboardManagerInput::InputInterface& ii, LowlevelKeyboardEvent* data, State& state, const TextReplacementTransactionCallbacks& transactionCallbacks)
    {
        if (HandleTextReplacementSuppressedKeyEvent(data, state) == 1)
        {
            return 1;
        }

        if (GeneratedByKBM(data))
        {
            return 0;
        }

        const DWORD vkCode = Helpers::ClearKeyNumpadOrigin(data->lParam->vkCode);
        const bool isTriggerKey = IsTextReplacementTriggerKey(vkCode);
        const bool freshTriggerKeyDown = isTriggerKey &&
                                         (data->wParam == WM_KEYDOWN || data->wParam == WM_SYSKEYDOWN) &&
                                         state.textReplacementTriggerKeysDown.insert(data->lParam->vkCode).second;

        if (state.textReplacements.empty())
        {
            return 0;
        }

        if (data->wParam != WM_KEYDOWN && data->wParam != WM_SYSKEYDOWN)
        {
            return 0;
        }

        const uint64_t contextEpoch = state.textReplacementContextEpoch.load(std::memory_order_acquire);
        if (contextEpoch != state.textReplacementObservedContextEpoch)
        {
            ResetTextReplacementRuntimeState(state);
        }

        const HWND foregroundWindow = GetTextReplacementWindow();
        const DWORD foregroundProcessId = GetTextReplacementWindowProcessId(foregroundWindow);
        if (foregroundWindow != state.textReplacementWindow || foregroundProcessId != state.textReplacementProcessId)
        {
            ClearTextReplacementBuffer(state);
            state.textReplacementProcessId = foregroundProcessId;
            state.textReplacementWindow = foregroundWindow;
        }

        if (!state.textReplacementContextTrackingEnabled.load(std::memory_order_acquire))
        {
            ClearTextReplacementBuffer(state);
            return 0;
        }

        {
            const uint64_t authorizationEpoch = state.textReplacementContextEpoch.load(std::memory_order_acquire);
            const uint64_t classifiedEpoch = state.textReplacementClassifiedContextEpoch.load(std::memory_order_acquire);
            const TextReplacementContextStatus contextStatus = state.textReplacementContextStatus.load(std::memory_order_acquire);
            const bool contextMatches = foregroundWindow == state.textReplacementContextWindow.load(std::memory_order_acquire) &&
                                        foregroundProcessId == state.textReplacementContextProcessId.load(std::memory_order_acquire);
            if (classifiedEpoch != authorizationEpoch || contextStatus == TextReplacementContextStatus::Pending)
            {
                ClearTextReplacementBuffer(state);
                return 0;
            }

            if (contextStatus == TextReplacementContextStatus::Blocked)
            {
                ClearTextReplacementBuffer(state);
                return 0;
            }

            if (!contextMatches)
            {
                ClearTextReplacementBuffer(state);
                state.InvalidateTextReplacementContext();
                return 0;
            }

            if (state.textReplacementContextEpoch.load(std::memory_order_acquire) != authorizationEpoch)
            {
                ClearTextReplacementBuffer(state);
                return 0;
            }
        }

        if (Helpers::IsModifierKey(vkCode))
        {
            return 0;
        }

        if (vkCode == VK_CAPITAL || vkCode == VK_NUMLOCK || vkCode == VK_SCROLL)
        {
            return 0;
        }

        if (vkCode == VK_BACK && (IsTextReplacementShortcutModifierPressed(ii) || IsAltGrPressed(ii)))
        {
            ClearTextReplacementBuffer(state);
            return 0;
        }

        if (vkCode == VK_BACK)
        {
            if (state.textReplacementPendingPacketHighSurrogate != L'\0')
            {
                state.textReplacementPendingPacketHighSurrogate = L'\0';
            }
            else if (state.textReplacementDeadKeyPending)
            {
                ClearDeadKeyTracking(state);
            }
            else
            {
                PopLastUtf16Scalar(state.textReplacementBuffer);
            }

            return 0;
        }

        const bool canActivate = freshTriggerKeyDown &&
                                 !IsTextReplacementActivationModifierPressed(ii) &&
                                 !state.textReplacementDeadKeyPending &&
                                 state.textReplacementPendingPacketHighSurrogate == L'\0';
        if (canActivate)
        {
            const std::wstring_view textReplacementBufferView{ state.textReplacementBuffer };
            for (size_t length = textReplacementBufferView.length(); length != 0; --length)
            {
                const std::wstring_view trigger = textReplacementBufferView.substr(textReplacementBufferView.length() - length);
                if (IsLowSurrogate(trigger.front()))
                {
                    continue;
                }

                const auto replacement = state.textReplacements.find(trigger);
                if (replacement == state.textReplacements.end() || replacement->second.triggerKey != vkCode)
                {
                    continue;
                }

                TextReplacementPreparationResult preparationResult = TextReplacementPreparationResult::NotPrepared;
                if (transactionCallbacks.prepare)
                {
                    try
                    {
                        preparationResult = transactionCallbacks.prepare(
                            trigger,
                            replacement->second.text.find_first_of(L"\r\n") != std::wstring::npos);
                    }
                    catch (...)
                    {
                        // Preparation may have selected text before it failed. Treat an
                        // exception as a committed failure, never as safe pass-through.
                        preparationResult = TextReplacementPreparationResult::CommittedFailure;
                    }
                }

                ClearTextReplacementBuffer(state);
                ClearDeadKeyTracking(state);

                if (preparationResult == TextReplacementPreparationResult::NotPrepared)
                {
                    return 0;
                }

                const auto rollbackPreparedSelection = [&transactionCallbacks]() {
                    if (!transactionCallbacks.rollback)
                    {
                        return false;
                    }
                    try
                    {
                        return transactionCallbacks.rollback();
                    }
                    catch (...)
                    {
                        return false;
                    }
                };
                const auto finishPreparedSelection = [&transactionCallbacks]() {
                    if (transactionCallbacks.finish)
                    {
                        try
                        {
                            transactionCallbacks.finish();
                        }
                        catch (...)
                        {
                            Logger::error(L"Failed to finish the text replacement selection transaction.");
                        }
                    }
                };

                if (preparationResult == TextReplacementPreparationResult::CommittedFailure)
                {
                    rollbackPreparedSelection();
                    state.textReplacementSuppressedTriggerKeys.insert(data->lParam->vkCode);
                    Logger::error(L"Text replacement preparation failed after committing target selection; the trigger key was suppressed.");
                    return 1;
                }

                bool inputStreamMutated = false;
                const TextReplacementInputResult inputResult = SendTextInputInSmallBatches(ii, state, replacement->second.text, inputStreamMutated, transactionCallbacks.isCurrent);
                if (inputResult == TextReplacementInputResult::FailedBeforeMutation)
                {
                    // Only a synchronously restored and re-verified collapsed caret makes
                    // it safe to deliver the user's physical Space/Enter/Tab.
                    if (rollbackPreparedSelection())
                    {
                        return 0;
                    }

                    state.textReplacementSuppressedTriggerKeys.insert(data->lParam->vkCode);
                    Logger::error(L"Text replacement input was blocked and its prepared selection could not be safely rolled back; the trigger key was suppressed.");
                    return 1;
                }

                finishPreparedSelection();
                state.textReplacementSuppressedTriggerKeys.insert(data->lParam->vkCode);
                if (inputResult == TextReplacementInputResult::FailedAfterMutation)
                {
                    Logger::error(L"Text replacement input failed after modifying the input stream; the trigger key was suppressed to avoid further corruption.");
                }
                return 1;
            }
        }

        if (IsTextReplacementShortcutModifierPressed(ii))
        {
            ClearTextReplacementBuffer(state);
            return 0;
        }

        if (state.textReplacementDeadKeyPending && vkCode == VK_PACKET)
        {
            ClearTextReplacementBuffer(state);
            return 0;
        }

        const bool deadKeyWasPending = state.textReplacementDeadKeyPending;
        const auto textEvent = GetTextFromKeyboardEvent(ii, data, state);
        if (textEvent.kind == KeyboardTextEventKind::DeadKey || textEvent.kind == KeyboardTextEventKind::PacketHighSurrogate)
        {
            if (textEvent.kind == KeyboardTextEventKind::DeadKey)
            {
                ClearTextReplacementBuffer(state);
            }
            return 0;
        }

        if (textEvent.kind != KeyboardTextEventKind::Text)
        {
            ClearTextReplacementBuffer(state);
            return 0;
        }

        if (deadKeyWasPending)
        {
            ClearTextReplacementBuffer(state);
            ClearDeadKeyTracking(state);
            // ToUnicodeEx returned the composed character(s). They are independent of the
            // pre-dead-key suffix, but are safe to use as the beginning of a new suffix.
            state.textReplacementBuffer.append(textEvent.text);
            TrimUtf16Buffer(state.textReplacementBuffer, state.maxTextReplacementTriggerLength);
            return 0;
        }

        if (textEvent.resetBufferBeforeText)
        {
            ClearTextReplacementBuffer(state);
        }

        state.textReplacementBuffer.append(textEvent.text);
        TrimUtf16Buffer(state.textReplacementBuffer, state.maxTextReplacementTriggerLength);

        return 0;
    }

    void ResetTextReplacementRuntimeState(State& state) noexcept
    {
        ClearTextReplacementBuffer(state);
        state.textReplacementProcessId = 0;
        state.textReplacementWindow = nullptr;
        state.textReplacementObservedContextEpoch = state.textReplacementContextEpoch.load(std::memory_order_acquire);
    }

    void InitializeTextReplacementToggleKeyState(State& state) noexcept
    {
        state.textReplacementCapsLockOn = (GetKeyState(VK_CAPITAL) & 0x1) != 0;
    }

    void UpdateTextReplacementToggleKeyState(const LowlevelKeyboardEvent* data, const bool eventSuppressed, State& state) noexcept
    {
        if (eventSuppressed || (data->wParam != WM_KEYDOWN && data->wParam != WM_SYSKEYDOWN))
        {
            return;
        }

        if (Helpers::ClearKeyNumpadOrigin(data->lParam->vkCode) == VK_CAPITAL)
        {
            state.textReplacementCapsLockOn = !state.textReplacementCapsLockOn;
        }
    }
}
