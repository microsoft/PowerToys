#include "pch.h"
#include "centralized_kb_hook.h"

namespace CentralizedKeyboardHook
{
    struct HotkeyDescriptor
    {
        Hotkey hotkey;
        std::wstring moduleName;
        std::function<bool()> action;

        bool operator<(const HotkeyDescriptor& other) const 
        {
            return hotkey < other.hotkey;
        };

    };

    std::multiset<HotkeyDescriptor> hotkeyDescriptors;
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

        const std::function<bool()>* action = nullptr;
        {
            // Hold the lock for the shortest possible duration
            std::unique_lock lock{ mutex };
            HotkeyDescriptor dummy{ .hotkey = hotkey };
            auto it = hotkeyDescriptors.find(dummy);
            if (it != hotkeyDescriptors.end())
            {
                action = &it->action;
            }
        }

        if (action)
        {
            if ((*action)())
            {
                return 1;
            }
        }

        return CallNextHookEx(hHook, nCode, wParam, lParam);
    }

    void SetHotkeyAction(const std::wstring& moduleName, const Hotkey& hotkey, std::function<bool()>&& action) noexcept
    {
        std::unique_lock lock{ mutex };
        hotkeyDescriptors.insert({ .hotkey = hotkey, .moduleName = moduleName, .action = action });
    }

    void ClearModuleHotkeys(const std::wstring& moduleName) noexcept
    {
        auto it = hotkeyDescriptors.begin();
        while (it != hotkeyDescriptors.end())
        {
            if (it->moduleName == moduleName)
            {
                it = hotkeyDescriptors.erase(it);
            }
            else
            {
                ++it;
            }
        }
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
