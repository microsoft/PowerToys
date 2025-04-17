#include "pch.h"
#include "KeyboardListener.h"
#include "KeyboardListener.g.cpp"

// #include <common/logger/logger.h>
// #include <common/utils/logger_helper.h>
#include <common/utils/winapi_error.h>

namespace
{
}

namespace winrt::CmdPalKeyboardService::implementation
{
    KeyboardListener::KeyboardListener()
    {
        s_instance = this;
    }

    void KeyboardListener::Start()
    {
#if defined(DISABLE_LOWLEVEL_HOOKS_WHEN_DEBUGGED)
        const bool hook_disabled = IsDebuggerPresent();
#else
        const bool hook_disabled = false;
#endif
        if (!hook_disabled)
        {
            if (!s_llKeyboardHook)
            {
                s_llKeyboardHook = SetWindowsHookExW(WH_KEYBOARD_LL, LowLevelKeyboardProc, NULL, NULL);
                if (!s_llKeyboardHook)
                {
                    DWORD errorCode = GetLastError();
                    show_last_error_message(L"SetWindowsHookEx", errorCode, L"CmdPalKeyboardService");
                }
            }
        }
    }

    void KeyboardListener::Stop()
    {
        if (s_llKeyboardHook && UnhookWindowsHookEx(s_llKeyboardHook))
        {
            s_llKeyboardHook = NULL;
        }
    }

    void KeyboardListener::SetHotkeyAction(bool win, bool ctrl, bool shift, bool alt, uint8_t key, hstring const& id)
    {
        Hotkey hotkey = { .win = win, .ctrl = ctrl, .shift = shift, .alt = alt, .key = key };
        std::unique_lock lock{ mutex };

        HotkeyDescriptor desc = { .hotkey = hotkey, .id = std::wstring(id) };
        hotkeyDescriptors.insert(desc);
    }

    void KeyboardListener::ClearHotkey(hstring const& id)
    {
        {
            std::unique_lock lock{ mutex };
            auto it = hotkeyDescriptors.begin();
            while (it != hotkeyDescriptors.end())
            {
                if (it->id == id)
                {
                    it = hotkeyDescriptors.erase(it);
                }
                else
                {
                    ++it;
                }
            }
        }
    }

    void KeyboardListener::ClearHotkeys()
    {
        {
            std::unique_lock lock{ mutex };
            auto it = hotkeyDescriptors.begin();
            while (it != hotkeyDescriptors.end())
            {
                it = hotkeyDescriptors.erase(it);
            }
        }
    }

    void KeyboardListener::SetProcessCommand(ProcessCommand processCommand)
    {
        m_processCommandCb = [trigger = std::move(processCommand)](hstring const& id) {
            trigger(id);
        };
    }

    LRESULT KeyboardListener::DoLowLevelKeyboardProc(int nCode, WPARAM wParam, LPARAM lParam)
    {
        const auto& keyPressInfo = *reinterpret_cast<KBDLLHOOKSTRUCT*>(lParam);

        if ((wParam != WM_KEYDOWN) && (wParam != WM_SYSKEYDOWN))
        {
            return CallNextHookEx(NULL, nCode, wParam, lParam);
        }

        Hotkey hotkey{
            .win = (GetAsyncKeyState(VK_LWIN) & 0x8000) || (GetAsyncKeyState(VK_RWIN) & 0x8000),
            .ctrl = static_cast<bool>(GetAsyncKeyState(VK_CONTROL) & 0x8000),
            .shift = static_cast<bool>(GetAsyncKeyState(VK_SHIFT) & 0x8000),
            .alt = static_cast<bool>(GetAsyncKeyState(VK_MENU) & 0x8000),
            .key = static_cast<unsigned char>(keyPressInfo.vkCode)
        };

        if (hotkey == Hotkey{})
        {
            return CallNextHookEx(NULL, nCode, wParam, lParam);
        }

        bool do_action = false;
        std::wstring actionId{};

        {
            // Hold the lock for the shortest possible duration
            std::unique_lock lock{ mutex };
            HotkeyDescriptor dummy{ .hotkey = hotkey };
            auto it = hotkeyDescriptors.find(dummy);
            if (it != hotkeyDescriptors.end())
            {
                do_action = true;
                actionId = it->id;
            }
        }

        if (do_action)
        {
            m_processCommandCb(hstring{ actionId });

            // After invoking the hotkey send a dummy key to prevent Start Menu from activating
            INPUT dummyEvent[1] = {};
            dummyEvent[0].type = INPUT_KEYBOARD;
            dummyEvent[0].ki.wVk = 0xFF;
            dummyEvent[0].ki.dwFlags = KEYEVENTF_KEYUP;
            SendInput(1, dummyEvent, sizeof(INPUT));

            // Swallow the key press
            return 1;
        }

        return CallNextHookEx(NULL, nCode, wParam, lParam);
    }

    LRESULT KeyboardListener::LowLevelKeyboardProc(int nCode, WPARAM wParam, LPARAM lParam)
    {
        if (s_instance == nullptr)
        {
            return CallNextHookEx(NULL, nCode, wParam, lParam);
        }

        if (nCode < 0)
        {
            return CallNextHookEx(NULL, nCode, wParam, lParam);
        }

        return s_instance->DoLowLevelKeyboardProc(nCode, wParam, lParam);
    }
}
