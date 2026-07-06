// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

// Installs a low-level keyboard hook on a dedicated message-loop thread and
// translates raw input into KeystrokeEvent values on an in-process lock-free
// queue. This replaces team4's separate keyboard service + named-pipe IPC with
// direct in-process capture, mirroring how Mouse Highlighter hooks the mouse.
#pragma once

#include <functional>
#include <thread>

#include <Windows.h>

#include "KeystrokeTypes.h"

namespace InputHighlighter
{
    class KeyboardCapture
    {
    public:
        KeyboardCapture() = default;
        ~KeyboardCapture();

        KeyboardCapture(const KeyboardCapture&) = delete;
        KeyboardCapture& operator=(const KeyboardCapture&) = delete;

        // Starts capturing. 'notify' is invoked (on the capture thread) after events
        // are enqueued so the consumer can drain via TryPop; keep it lean (e.g. post
        // a message or set an event). Returns false if the hook could not be set.
        bool Start(std::function<void()> notify);

        // Registers a callback (invoked on the capture thread) fired when one of the
        // configured overlay control shortcuts is pressed. The int argument is the
        // hotkey id: 0 = switch monitor, 1 = cycle display mode. Set before Start.
        void SetHotkeyHandler(std::function<void(int)> onHotkey) { m_onHotkey = std::move(onHotkey); }

        // Updates the overlay control shortcuts. Safe to call at any time; a matched
        // chord is swallowed (not shown as a keystroke).
        void SetHotkeys(const HotkeyChord& switchMonitor, const HotkeyChord& cycleDisplayMode)
        {
            m_switchMonitorHotkey = switchMonitor;
            m_cycleDisplayModeHotkey = cycleDisplayMode;
        }

        // Stops capturing and joins the capture thread.
        void Stop();

        // Consumer-side drain. Safe to call from a single consumer thread.
        bool TryPop(KeystrokeEvent& out) { return m_queue.try_pop(out); }

        bool IsRunning() const { return m_running.load(std::memory_order_acquire); }

    private:
        static LRESULT CALLBACK HookProc(int nCode, WPARAM wParam, LPARAM lParam);
        void ThreadMain();
        void EmitDown(UINT vk, UINT scanCode);
        // Returns true if the key-down matched a control shortcut and should be
        // swallowed (not forwarded, not shown). Runs on the capture thread.
        bool HandleKeyDown(UINT vk, UINT scanCode);
        void HandleKeyUp(UINT vk);

        static KeyboardCapture* s_instance;

        SpscRing<KeystrokeEvent, 1024> m_queue;
        std::function<void()> m_notify;
        std::function<void(int)> m_onHotkey;
        HotkeyChord m_switchMonitorHotkey;
        HotkeyChord m_cycleDisplayModeHotkey;
        // vk of the control shortcut currently held down, so auto-repeat fires the
        // action only once. Read/written only on the capture thread.
        UINT m_activeHotkeyVk = 0;
        std::thread m_thread;
        HHOOK m_hook = nullptr;
        DWORD m_threadId = 0;
        std::atomic<bool> m_running{ false };
    };
}
