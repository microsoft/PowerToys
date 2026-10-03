// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#pragma once

#include <bitset>
#include <common/hooks/LowlevelKeyboardEvent.h>
#include <common/interop/shared_constants.h>
#include <keyboardmanager/common/KeyboardManagerConstants.h>

// A mutex is owned only while the editor's UI thread is alive. Probing an unowned or
// abandoned mutex acquires it, so always release that temporary ownership immediately.
inline bool IsEditorMutexOwned(HANDLE mutex) noexcept
{
    if (mutex == nullptr)
    {
        return false;
    }

    const DWORD result = WaitForSingleObject(mutex, 0);
    if (result == WAIT_OBJECT_0 || result == WAIT_ABANDONED)
    {
        ReleaseMutex(mutex);
    }

    return result == WAIT_TIMEOUT;
}

// Change modes only between complete key gestures. In particular, an editor opening
// after a remapped key-down must not prevent the corresponding remapped key-up.
class EditorSuspensionState
{
public:
    static bool IsSourceEvent(const LowlevelKeyboardEvent& event) noexcept
    {
        const auto extraInfo = event.lParam->dwExtraInfo;
        return !(extraInfo & CommonSharedConstants::KEYBOARDMANAGER_INJECTED_FLAG) &&
               extraInfo != KeyboardManagerConstants::KEYBOARDMANAGER_REPLAY_FLAG;
    }

    bool ShouldSkipRemapping(const LowlevelKeyboardEvent& event, bool suspensionRequested, bool* skipSingleKeyRemapping = nullptr) noexcept
    {
        if (skipSingleKeyRemapping)
        {
            *skipSingleKeyRemapping = false;
        }

        const auto& key = *event.lParam;
        if (!IsSourceEvent(event))
        {
            // Injected releases can reenter the hook after the final source key-up.
            // They must finish in the same mode and must not create held source keys.
            return suspended;
        }

        if (IsIdle())
        {
            suspended = suspensionRequested;
        }

        // Scan codes remain stable if Num Lock or a remapped Shift changes the VK
        // between down and up. Synthetic input without a scan code uses a separate
        // range so it cannot collide with a physical key's scan code.
        const size_t keyId = key.scanCode != 0
                                 ? (key.scanCode & 0xff) + ((key.flags & LLKHF_EXTENDED) ? 256 : 0)
                                 : 512 + (key.vkCode & 0xff);

        if (event.wParam == WM_KEYDOWN || event.wParam == WM_SYSKEYDOWN)
        {
            initiallyPressedModifiers.reset(key.vkCode & 0xff);
            pressedKeys.set(keyId);
        }
        else if (event.wParam == WM_KEYUP || event.wParam == WM_SYSKEYUP)
        {
            const bool hadKeyDown = pressedKeys.test(keyId);
            pressedKeys.reset(keyId);
            const bool initiallyPressed = initiallyPressedModifiers.test(key.vkCode & 0xff);
            initiallyPressedModifiers.reset(key.vkCode & 0xff);
            if (initiallyPressed && !hadKeyDown)
            {
                // This modifier could participate in a shortcut after the hook started,
                // but its own single-key remap never received a down to pair with this up.
                if (skipSingleKeyRemapping)
                {
                    *skipSingleKeyRemapping = true;
                }

                return suspended;
            }

            if (!hadKeyDown)
            {
                // The hook may have started while this key was already held.
                return true;
            }
        }

        return suspended;
    }

    bool IsSuspended() const noexcept
    {
        return suspended;
    }

    bool IsIdle() const noexcept
    {
        return pressedKeys.none() && initiallyPressedModifiers.none();
    }

    void AddInitiallyPressedModifier(DWORD keyCode) noexcept
    {
        initiallyPressedModifiers.set(keyCode & 0xff);
    }

    void Reset(bool suspensionRequested = false) noexcept
    {
        pressedKeys.reset();
        initiallyPressedModifiers.reset();
        suspended = suspensionRequested;
    }

private:
    std::bitset<768> pressedKeys;
    std::bitset<256> initiallyPressedModifiers;
    bool suspended = false;
};

// Publish readiness only after the entire hook chain has processed a source event.
// A downstream recorder handling the first down must still see the prior idle state;
// a recorder handling the last up must let that old gesture finish before capturing.
class EditorCaptureReadyScope
{
public:
    EditorCaptureReadyScope(const EditorSuspensionState& state, HANDLE readyEvent, bool sourceEvent) noexcept :
        state(state), readyEvent(readyEvent), sourceEvent(sourceEvent)
    {
    }

    EditorCaptureReadyScope(const EditorCaptureReadyScope&) = delete;
    EditorCaptureReadyScope& operator=(const EditorCaptureReadyScope&) = delete;

    ~EditorCaptureReadyScope()
    {
        if (sourceEvent && readyEvent)
        {
            if (state.IsIdle())
            {
                SetEvent(readyEvent);
            }
            else
            {
                ResetEvent(readyEvent);
            }
        }
    }

private:
    const EditorSuspensionState& state;
    HANDLE readyEvent;
    bool sourceEvent;
};
