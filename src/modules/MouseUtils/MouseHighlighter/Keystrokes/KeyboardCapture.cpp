// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#include "KeyboardCapture.h"

#include <array>

namespace InputHighlighter
{
    KeyboardCapture* KeyboardCapture::s_instance = nullptr;

    namespace
    {
        uint64_t NowMicros()
        {
            static LARGE_INTEGER freq = [] {
                LARGE_INTEGER f;
                QueryPerformanceFrequency(&f);
                return f;
            }();

            LARGE_INTEGER c;
            QueryPerformanceCounter(&c);
            return static_cast<uint64_t>((c.QuadPart * 1'000'000) / freq.QuadPart);
        }

#pragma warning(push)
#pragma warning(disable : 26497) // runtime key-state calls; cannot be constexpr
        // Read the *physical* key state. A low-level keyboard hook runs on a
        // dedicated background thread that never owns keyboard focus, so the
        // per-thread state (GetKeyState/GetKeyboardState) is not synchronized
        // with the real keys. That makes held modifiers read as up whenever focus
        // is on another process (e.g. the desktop), which breaks shortcut
        // detection. GetAsyncKeyState reports the true hardware state regardless
        // of focus.
        std::array<bool, 4> SnapshotMods()
        {
            auto down = [](int vk) { return (GetAsyncKeyState(vk) & 0x8000) != 0; };
            return std::array<bool, 4>{
                down(VK_CONTROL),
                down(VK_MENU),
                down(VK_SHIFT),
                (down(VK_LWIN) || down(VK_RWIN)),
            };
        }

        // The keyboard layout of whatever window currently has focus, falling
        // back to the hook thread's own layout. Ensures characters translate
        // using the layout the user is actually typing against.
        HKL ForegroundLayout()
        {
            const HWND fg = GetForegroundWindow();
            const DWORD tid = fg ? GetWindowThreadProcessId(fg, nullptr) : 0;
            return GetKeyboardLayout(tid);
        }

        // Translate a virtual key + scan code into a printable character, if any.
        // Non-printable keys (Enter, arrows, control combos, ...) yield 0.
        char32_t VkToChar(UINT vk, UINT scanCode)
        {
            // Seed toggle keys (Caps/Num/Scroll Lock) from the cached state, then
            // overlay the live physical modifier state so Shift/AltGr characters
            // are correct even when another process (e.g. the desktop) has focus.
            BYTE keyState[256] = {};
            GetKeyboardState(keyState);

            auto syncAsync = [&keyState](int key) {
                keyState[key] = static_cast<BYTE>((GetAsyncKeyState(key) & 0x8000) ? 0x80 : 0x00);
            };
            for (const int key : { VK_SHIFT, VK_LSHIFT, VK_RSHIFT,
                                   VK_CONTROL, VK_LCONTROL, VK_RCONTROL,
                                   VK_MENU, VK_LMENU, VK_RMENU })
            {
                syncAsync(key);
            }

            WCHAR buf[4] = { 0 };
            HKL layout = ForegroundLayout();

            // ToUnicodeEx can mutate dead-key state; acceptable for an overlay.
            const int rc = ToUnicodeEx(vk, scanCode, keyState, buf, ARRAYSIZE(buf), 0, layout);
            if (rc <= 0)
            {
                return 0;
            }

            if (!iswprint(buf[0]))
            {
                return 0;
            }

            return static_cast<char32_t>(buf[0]);
        }
#pragma warning(pop)
    }

    KeyboardCapture::~KeyboardCapture()
    {
        Stop();
    }

    bool KeyboardCapture::Start(std::function<void()> notify)
    {
        if (m_running.load(std::memory_order_acquire))
        {
            return true;
        }

        m_notify = std::move(notify);
        s_instance = this;
        m_running.store(true, std::memory_order_release);
        m_thread = std::thread(&KeyboardCapture::ThreadMain, this);
        return true;
    }

    void KeyboardCapture::Stop()
    {
        if (!m_running.exchange(false))
        {
            if (m_thread.joinable())
            {
                m_thread.join();
            }

            return;
        }

        if (m_threadId != 0)
        {
            PostThreadMessageW(m_threadId, WM_QUIT, 0, 0);
        }

        if (m_thread.joinable())
        {
            m_thread.join();
        }

        if (s_instance == this)
        {
            s_instance = nullptr;
        }
    }

    void KeyboardCapture::ThreadMain()
    {
        m_threadId = GetCurrentThreadId();

        m_hook = SetWindowsHookExW(WH_KEYBOARD_LL, HookProc, GetModuleHandleW(nullptr), 0);
        if (m_hook == nullptr)
        {
            m_running.store(false, std::memory_order_release);
            return;
        }

        MSG msg;
        while (GetMessageW(&msg, nullptr, 0, 0) > 0)
        {
            TranslateMessage(&msg);
            DispatchMessageW(&msg);
        }

        if (m_hook != nullptr)
        {
            UnhookWindowsHookEx(m_hook);
            m_hook = nullptr;
        }

        m_threadId = 0;
    }

    void KeyboardCapture::EmitDown(UINT vk, UINT scanCode)
    {
        KeystrokeEvent e{};
        e.type = KeystrokeEventType::Down;
        e.vk = vk;
        e.ch = VkToChar(vk, scanCode);
        e.mods = SnapshotMods();
        e.tsMicros = NowMicros();
        m_queue.try_push(e);

        if (m_notify)
        {
            m_notify();
        }
    }

    bool KeyboardCapture::HandleKeyDown(UINT vk, UINT scanCode)
    {
        const auto mods = SnapshotMods();

        int hotkeyId = -1;
        if (m_switchMonitorHotkey.Matches(vk, mods))
        {
            hotkeyId = 0;
        }
        else if (m_cycleDisplayModeHotkey.Matches(vk, mods))
        {
            hotkeyId = 1;
        }

        if (hotkeyId >= 0)
        {
            // Fire once per physical press; swallow auto-repeat while held.
            if (m_activeHotkeyVk != vk)
            {
                m_activeHotkeyVk = vk;
                if (m_onHotkey)
                {
                    m_onHotkey(hotkeyId);
                }
            }

            return true; // swallow: don't type or display the shortcut key
        }

        EmitDown(vk, scanCode);
        return false;
    }

    void KeyboardCapture::HandleKeyUp(UINT vk)
    {
        if (m_activeHotkeyVk == vk)
        {
            m_activeHotkeyVk = 0;
        }
    }

    LRESULT CALLBACK KeyboardCapture::HookProc(int nCode, WPARAM wParam, LPARAM lParam)
    {
        if (nCode == HC_ACTION && s_instance != nullptr)
        {
            const KBDLLHOOKSTRUCT* p = reinterpret_cast<KBDLLHOOKSTRUCT*>(lParam);
            switch (wParam)
            {
            case WM_KEYDOWN:
            case WM_SYSKEYDOWN:
                if (s_instance->HandleKeyDown(p->vkCode, p->scanCode))
                {
                    return 1; // swallow matched control shortcut
                }
                break;
            case WM_KEYUP:
            case WM_SYSKEYUP:
                s_instance->HandleKeyUp(p->vkCode);
                break;
            default:
                break;
            }
        }

        return CallNextHookEx(nullptr, nCode, wParam, lParam);
    }
}
