#include "pch.h"
#include "centralized_kb_hook.h"
#include <set>
#include <common/debug_control.h>
#include <common/utils/winapi_error.h>
#include <common/logger/logger.h>
#include <common/interop/shared_constants.h>

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

    // To store information about handling pressed keys.
    struct PressedKeyDescriptor
    {
        DWORD virtualKey; // Virtual Key code of the key we're keeping track of.
        std::wstring moduleName;
        std::function<bool()> action;
        UINT_PTR idTimer; // Timer ID for calling SET_TIMER with.
        UINT millisecondsToPress; // How much time the key must be pressed.
        bool operator<(const PressedKeyDescriptor& other) const
        {
            // We'll use the virtual key as the real key, since looking for a hit with the key is done in the more time sensitive path (low level keyboard hook).
            return virtualKey < other.virtualKey;
        };
    };
    std::multiset<PressedKeyDescriptor> pressedKeyDescriptors;
    std::mutex pressedKeyMutex;

    // Virtual key codes whose long-press timer fired and that requested
    // Start Menu suppression. The dummy keystroke is injected when the
    // matching key is finally released so the chord break is fresh when
    // Windows processes the Win-up; injecting it at timer-fire time is
    // ~press_time ms early and is sometimes "forgotten" by the shell.
    std::set<DWORD> pendingChordBreaks;
    std::mutex pendingChordBreakMutex;

    // keep track of last pressed key, to detect repeated keys and if there are more keys pressed.
    const DWORD VK_DISABLED = CommonSharedConstants::VK_DISABLED;
    DWORD vkCodePressed = VK_DISABLED;

    // Save the runner window handle for registering timers.
    HWND runnerWindow;

    struct DestroyOnExit
    {
        ~DestroyOnExit()
        {
            Stop();
        }
    } destroyOnExitObj;

    // Handle the pressed key proc
    void PressedKeyTimerProc(
        HWND hwnd,
        UINT /*message*/,
        UINT_PTR idTimer,
        DWORD /*dwTime*/)
    {
        std::multiset<PressedKeyDescriptor> copy;
        {
            // Make a copy, to look for the action to call.
            std::unique_lock lock{ pressedKeyMutex };
            copy = pressedKeyDescriptors;
        }
        for (const auto& it : copy)
        {
            if (it.idTimer == idTimer)
            {
                if (it.action())
                {
                    // Defer the actual SendInput until the matching key is
                    // released. See pendingChordBreaks comment above.
                    std::unique_lock lock{ pendingChordBreakMutex };
                    pendingChordBreaks.insert(it.virtualKey);
                }
                // The descriptor is one-shot per hold: clearing it avoids
                // double-firing when the same module re-registers the action
                // before the next press.
                break;
            }
        }

        KillTimer(hwnd, idTimer);
    }

    LRESULT CALLBACK KeyboardHookProc(_In_ int nCode, _In_ WPARAM wParam, _In_ LPARAM lParam)
    {
        if (nCode < 0)
        {
            return CallNextHookEx(hHook, nCode, wParam, lParam);
        }

        const auto& keyPressInfo = *reinterpret_cast<KBDLLHOOKSTRUCT*>(lParam);

        if (keyPressInfo.dwExtraInfo == PowertoyModuleIface::CENTRALIZED_KEYBOARD_HOOK_DONT_TRIGGER_FLAG)
        {
            // The new keystroke was generated from one of our actions. We should pass it along.
            return CallNextHookEx(hHook, nCode, wParam, lParam);
        }

        // Check if the keys are pressed.
        if (!pressedKeyDescriptors.empty())
        {
            bool wasKeyPressed = vkCodePressed != VK_DISABLED;
            // Hold the lock for the shortest possible duration
            if ((wParam == WM_KEYDOWN || wParam == WM_SYSKEYDOWN))
            {
                if (!wasKeyPressed)
                {
                    // If no key was pressed before, let's start a timer to take into account this new key.
                    std::unique_lock lock{ pressedKeyMutex };
                    PressedKeyDescriptor dummy{ .virtualKey = keyPressInfo.vkCode };
                    auto [it, last] = pressedKeyDescriptors.equal_range(dummy);
                    for (; it != last; ++it)
                    {
                        SetTimer(runnerWindow, it->idTimer, it->millisecondsToPress, PressedKeyTimerProc);
                    }
                }
                else if (vkCodePressed != keyPressInfo.vkCode)
                {
                    // If a different key was pressed, let's clear the timers we have started for the previous key.
                    std::unique_lock lock{ pressedKeyMutex };
                    PressedKeyDescriptor dummy{ .virtualKey = vkCodePressed };
                    auto [it, last] = pressedKeyDescriptors.equal_range(dummy);
                    for (; it != last; ++it)
                    {
                        KillTimer(runnerWindow, it->idTimer);
                    }
                }
                vkCodePressed = keyPressInfo.vkCode;
            }
            if (wParam == WM_KEYUP || wParam == WM_SYSKEYUP)
            {
                {
                    std::unique_lock lock{ pressedKeyMutex };
                    PressedKeyDescriptor dummy{ .virtualKey = keyPressInfo.vkCode };
                    auto [it, last] = pressedKeyDescriptors.equal_range(dummy);
                    for (; it != last; ++it)
                    {
                        KillTimer(runnerWindow, it->idTimer);
                    }
                }

                // If a long-press action fired earlier for this key and the
                // module asked for Start Menu suppression, inject the dummy
                // event NOW -- while this Win-up is still in-flight in the
                // hook and before the shell sees it. This matches the v0.99
                // ShortcutGuide trick of breaking the Win-only chord at the
                // moment of release.
                bool sendChordBreak = false;
                {
                    std::unique_lock lock{ pendingChordBreakMutex };
                    auto it = pendingChordBreaks.find(keyPressInfo.vkCode);
                    if (it != pendingChordBreaks.end())
                    {
                        pendingChordBreaks.erase(it);
                        sendChordBreak = true;
                    }
                }
                if (sendChordBreak)
                {
                    INPUT dummyEvent[1] = {};
                    dummyEvent[0].type = INPUT_KEYBOARD;
                    dummyEvent[0].ki.wVk = 0xFF;
                    dummyEvent[0].ki.dwFlags = KEYEVENTF_KEYUP;
                    SendInput(1, dummyEvent, sizeof(INPUT));
                }

                vkCodePressed = 0x100;
            }
        }

        if ((wParam != WM_KEYDOWN) && (wParam != WM_SYSKEYDOWN))
        {
            return CallNextHookEx(hHook, nCode, wParam, lParam);
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
            return CallNextHookEx(hHook, nCode, wParam, lParam);
        }

        std::function<bool()> action;
        {
            // Hold the lock for the shortest possible duration
            std::unique_lock lock{ mutex };
            HotkeyDescriptor dummy{ .hotkey = hotkey };
            auto it = hotkeyDescriptors.find(dummy);
            if (it != hotkeyDescriptors.end())
            {
                action = it->action;
            }
        }

        if (action)
        {
            if (action())
            {
                // After invoking the hotkey send a dummy key to prevent Start Menu from activating
                INPUT dummyEvent[1] = {};
                dummyEvent[0].type = INPUT_KEYBOARD;
                dummyEvent[0].ki.wVk = 0xFF;
                dummyEvent[0].ki.dwFlags = KEYEVENTF_KEYUP;
                SendInput(1, dummyEvent, sizeof(INPUT));

                // Swallow the key press
                return 1;
            }
        }

        return CallNextHookEx(hHook, nCode, wParam, lParam);
    }

    void SetHotkeyAction(const std::wstring& moduleName, const Hotkey& hotkey, std::function<bool()>&& action) noexcept
    {
        Logger::trace(L"Register hotkey action for {}", moduleName);
        std::unique_lock lock{ mutex };
        hotkeyDescriptors.insert({ .hotkey = hotkey, .moduleName = moduleName, .action = std::move(action) });
    }

    void AddPressedKeyAction(const std::wstring& moduleName, const DWORD vk, const UINT milliseconds, std::function<bool()>&& action) noexcept
    {
        // Calculate a unique TimerID.
        auto hash = std::hash<std::wstring>{}(moduleName); // Hash the module as the upper part of the timer ID.
        const UINT upperId = hash & 0xFFFF;
        const UINT lowerId = vk & 0xFFFF; // The key to press can be the lower ID.
        const UINT timerId = upperId << 16 | lowerId;
        std::unique_lock lock{ pressedKeyMutex };
        pressedKeyDescriptors.insert({ .virtualKey = vk, .moduleName = moduleName, .action = std::move(action), .idTimer = timerId, .millisecondsToPress = milliseconds });
    }

    void ClearModuleHotkeys(const std::wstring& moduleName) noexcept
    {
        Logger::trace(L"UnRegister hotkey action for {}", moduleName);
        {
            std::unique_lock lock{ mutex };
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
        ClearModulePressedKeyActions(moduleName);
    }

    void ClearModulePressedKeyActions(const std::wstring& moduleName) noexcept
    {
        std::unique_lock lock{ pressedKeyMutex };
        auto it = pressedKeyDescriptors.begin();
        while (it != pressedKeyDescriptors.end())
        {
            if (it->moduleName == moduleName)
            {
                if (runnerWindow)
                {
                    KillTimer(runnerWindow, it->idTimer);
                }
                it = pressedKeyDescriptors.erase(it);
            }
            else
            {
                ++it;
            }
        }
    }

    void Start() noexcept
    {
#if defined(DISABLE_LOWLEVEL_HOOKS_WHEN_DEBUGGED)
        const bool hook_disabled = IsDebuggerPresent();
#else
        const bool hook_disabled = false;
#endif
        if (!hook_disabled)
        {
            if (!hHook)
            {
                hHook = SetWindowsHookExW(WH_KEYBOARD_LL, KeyboardHookProc, NULL, NULL);
                if (!hHook)
                {
                    DWORD errorCode = GetLastError();
                    show_last_error_message(L"SetWindowsHookEx", errorCode, L"centralized_kb_hook");
                }
            }
        }
    }

    void Stop() noexcept
    {
        if (hHook && UnhookWindowsHookEx(hHook))
        {
            hHook = NULL;
        }
    }

    void RegisterWindow(HWND hwnd) noexcept
    {
        runnerWindow = hwnd;
    }
}
