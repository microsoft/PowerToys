#include "pch.h"
#include "root_kbhook.h"

namespace RootKeyboardHook
{
    std::map<Hotkey, std::function<void()>> hotkeyToActionMap;
    std::mutex mutex;
    HHOOK hHook{};

    struct DestroyOnExit
    {
        ~DestroyOnExit()
        {
            Stop();
        }
    } destroyOnExitObj;

    LRESULT CALLBACK KeyboardHookProc(_In_ int nCode, _In_ WPARAM wParam, _In_ LPARAM lParam)
    {
        if (nCode < 0 || wParam != WM_KEYDOWN)
        {
            return CallNextHookEx(hHook, nCode, wParam, lParam);
        }

        const auto& keyPressInfo = *reinterpret_cast<KBDLLHOOKSTRUCT*>(lParam);

        Hotkey hotkey{
            .win = (GetAsyncKeyState(VK_LWIN) & 0x8000) || (GetAsyncKeyState(VK_RWIN) & 0x8000),
            .ctrl = static_cast<bool>(GetAsyncKeyState(VK_CONTROL) & 0x8000),
            .shift = static_cast<bool>(GetAsyncKeyState(VK_SHIFT) & 0x8000),
            .alt = static_cast<bool>(GetAsyncKeyState(VK_MENU) & 0x8000),
            .key = static_cast<unsigned char>(keyPressInfo.vkCode)
        };

        std::function<void()>* action = nullptr;
        {
            // Hold the lock for the shortest possible duration
            std::unique_lock lock{ mutex };
            auto it = hotkeyToActionMap.find(hotkey);
            if (it != hotkeyToActionMap.end())
            {
                action = &it->second;
            }
        }

        if (action)
        {
            (*action)();
            return 1;
        }

        return CallNextHookEx(hHook, nCode, wParam, lParam);
    }

    void SetHotkeyAction(const Hotkey& hotkey, std::function<void()>&& action) noexcept
    {
        std::unique_lock lock{ mutex };
        hotkeyToActionMap[hotkey] = action;
    }

    void ClearHotkeyAction(const Hotkey& hotkey) noexcept
    {
        std::unique_lock lock{ mutex };
        hotkeyToActionMap.erase(hotkey);
    }

    void Start() noexcept
    {
        if (!hHook)
        {
            hHook = SetWindowsHookExW(WH_KEYBOARD_LL, KeyboardHookProc, NULL, NULL);
        }
    }

    void Stop() noexcept
    {
        if (hHook && UnhookWindowsHookEx(hHook))
        {
            hHook = NULL;
        }
    }
}
