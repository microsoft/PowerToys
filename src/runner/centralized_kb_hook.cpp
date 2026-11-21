#include "pch.h"
#include "centralized_kb_hook.h"
#include "hotkey_dispatcher.h"
#include "slow_hotkey_watchdog.h"
#include <atomic>
#include <future>
#include <common/debug_control.h>
#include <common/utils/winapi_error.h>
#include <common/logger/logger.h>
#include <common/interop/shared_constants.h>

// Threading model:
// - The WH_KEYBOARD_LL hook runs on a dedicated hook thread that does nothing else. For each
//   registered hotkey it only calls the fast should_handle callback to decide whether the
//   key press is swallowed.
// - Module actions are queued to HotkeyDispatch::Dispatcher and run in FIFO order on the
//   runner's main thread, which is the thread that calls the other module methods
//   (enable, disable, set_config, ...). Modules rely on that serialization.
// - The hook waits up to HandoffBudget for the queued action before it releases the key
//   press, so fast actions finish while the runner still has the foreground activation
//   rights of the key press, as they did when they ran inside the hook. Slow actions keep
//   running after the hook returns, so they can no longer stall input.
namespace CentralizedKeyboardHook
{
    namespace
    {
        constexpr auto HandoffBudget = SlowHotkeyWatchdog::SlowThreshold;
        constexpr std::chrono::milliseconds HookThreadExitTimeout{ 2000 };
        constexpr UINT WM_DRAIN_HOTKEY_ACTIONS = WM_APP + 1;
        constexpr wchar_t DispatchWindowClassName[] = L"PToyCentralizedKbHookDispatch";
    }

    struct HotkeyDescriptor
    {
        Hotkey hotkey;
        std::wstring moduleName;
        std::function<HotkeyDisposition()> shouldHandle;
        std::function<void()> invoke;
        std::shared_ptr<const HotkeyDispatch::HotkeyLabel> label;

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
        std::function<void()> action;
        std::shared_ptr<const HotkeyDispatch::HotkeyLabel> label;
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
    std::atomic_bool hasPressedKeyDescriptors{ false };

    // keep track of last pressed key, to detect repeated keys and if there are more keys pressed.
    const DWORD VK_DISABLED = CommonSharedConstants::VK_DISABLED;
    std::atomic<DWORD> vkCodePressed{ VK_DISABLED };

    HotkeyDispatch::Dispatcher dispatcher;

    // Message-only window on the main thread. Posting to it wakes the hotkey worker. Window
    // messages, unlike thread messages, are also dispatched by modal loops.
    HWND dispatchWindow{};

    // Hook thread state. timerWindow is owned by the hook thread and hosts the key hold timers.
    std::thread hookThread;
    DWORD hookThreadId{};
    HWND timerWindow{};

    struct DestroyOnExit
    {
        ~DestroyOnExit()
        {
            Stop();
        }
    } destroyOnExitObj;

    // Runs on the hook thread when a key was held long enough.
    void PressedKeyTimerProc(
        HWND hwnd,
        UINT /*message*/,
        UINT_PTR idTimer,
        DWORD /*dwTime*/)
    {
        KillTimer(hwnd, idTimer);

        std::vector<HotkeyDispatch::WorkItem> actions;
        {
            std::unique_lock lock{ pressedKeyMutex };
            for (const auto& it : pressedKeyDescriptors)
            {
                // Revalidate that the key is still physically held before firing.
                // This prevents ghost activations after the key was already released.
                if (it.idTimer == idTimer && (GetAsyncKeyState(static_cast<int>(it.virtualKey)) & 0x8000))
                {
                    actions.push_back({ it.action, it.label });
                }
            }
        }

        for (auto& action : actions)
        {
            dispatcher.Enqueue(std::move(action));
        }
    }

    // Slow path only: the report is queued so the hook thread doesn't log.
    void QueueSlowCheckReport(const std::shared_ptr<const HotkeyDispatch::HotkeyLabel>& label, SlowHotkeyWatchdog::Clock::duration elapsed) noexcept
    {
        if (!label)
        {
            return;
        }

        dispatcher.Enqueue({ [label, elapsed] {
                                SlowHotkeyWatchdog::ReportSlowAction(L"keyboard hook should_handle_hotkey", label->moduleName, label->hotkeyName + L" (should_handle_hotkey)", elapsed);
                            },
                             nullptr });
    }

    // Runs on the worker after each queued action.
    void OnActionCompleted(const HotkeyDispatch::WorkItem& item, HotkeyDispatch::Clock::duration elapsed, std::exception_ptr error) noexcept
    {
        try
        {
            if (error)
            {
                const auto moduleName = item.label ? item.label->moduleName : std::wstring{ L"<unknown>" };
                try
                {
                    std::rethrow_exception(error);
                }
                catch (const std::exception& e)
                {
                    Logger::error(L"Hotkey action for {} threw an exception", moduleName);
                    Logger::error("Hotkey action exception: {}", e.what());
                }
                catch (...)
                {
                    Logger::error(L"Hotkey action for {} threw an unknown exception", moduleName);
                }
            }

            if (item.label && SlowHotkeyWatchdog::IsSlow(elapsed)) [[unlikely]]
            {
                SlowHotkeyWatchdog::ReportSlowAction(item.label->dispatchPath, item.label->moduleName, item.label->hotkeyName, elapsed);
            }
        }
        catch (...)
        {
        }
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
        if (hasPressedKeyDescriptors.load(std::memory_order_relaxed))
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
                        SetTimer(timerWindow, it->idTimer, it->millisecondsToPress, PressedKeyTimerProc);
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
                        KillTimer(timerWindow, it->idTimer);
                    }
                }
                vkCodePressed = keyPressInfo.vkCode;
            }
            if (wParam == WM_KEYUP || wParam == WM_SYSKEYUP)
            {
                std::unique_lock lock{ pressedKeyMutex };
                PressedKeyDescriptor dummy{ .virtualKey = keyPressInfo.vkCode };
                auto [it, last] = pressedKeyDescriptors.equal_range(dummy);
                for (; it != last; ++it)
                {
                    KillTimer(timerWindow, it->idTimer);
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

        std::function<HotkeyDisposition()> shouldHandle;
        std::function<void()> invoke;
        std::shared_ptr<const HotkeyDispatch::HotkeyLabel> label;
        {
            // Hold the lock for the shortest possible duration
            std::unique_lock lock{ mutex };
            HotkeyDescriptor dummy{ .hotkey = hotkey };
            auto it = hotkeyDescriptors.find(dummy);
            if (it != hotkeyDescriptors.end())
            {
                shouldHandle = it->shouldHandle;
                invoke = it->invoke;
                label = it->label;
            }
        }

        if (shouldHandle)
        {
            auto disposition = HotkeyDisposition::PassThrough;
            const auto checkStart = SlowHotkeyWatchdog::Clock::now();
            try
            {
                disposition = shouldHandle();
            }
            catch (...)
            {
                disposition = HotkeyDisposition::PassThrough;
            }

            const auto checkElapsed = SlowHotkeyWatchdog::Clock::now() - checkStart;
            if (SlowHotkeyWatchdog::IsSlow(checkElapsed)) [[unlikely]]
            {
                QueueSlowCheckReport(label, checkElapsed);
            }

            if (disposition != HotkeyDisposition::PassThrough)
            {
                const auto ticket = dispatcher.Enqueue({ std::move(invoke), std::move(label) });

                // Let a fast action finish while this key press is still being processed. A
                // slow one keeps running on the worker after the budget, without stalling input.
                dispatcher.WaitForCompletion(ticket, HandoffBudget);

                if (disposition == HotkeyDisposition::InvokeAndSwallow)
                {
                    // After invoking the hotkey send a dummy key to prevent Start Menu from activating
                    INPUT dummyEvent[1] = {};
                    dummyEvent[0].type = INPUT_KEYBOARD;
                    dummyEvent[0].ki.wVk = 0xFF;
                    dummyEvent[0].ki.dwFlags = KEYEVENTF_KEYUP;
                    dummyEvent[0].ki.dwExtraInfo = PowertoyModuleIface::CENTRALIZED_KEYBOARD_HOOK_DONT_TRIGGER_FLAG;
                    SendInput(1, dummyEvent, sizeof(INPUT));

                    // Swallow the key press
                    return 1;
                }
            }
        }

        return CallNextHookEx(hHook, nCode, wParam, lParam);
    }

    void HookThreadProc(std::promise<DWORD> ready) noexcept
    {
        // Create the message queue before reporting readiness, so that Stop() can post WM_QUIT.
        MSG msg{};
        PeekMessageW(&msg, nullptr, WM_USER, WM_USER, PM_NOREMOVE);

        timerWindow = CreateWindowExW(0, L"STATIC", L"", 0, 0, 0, 0, 0, HWND_MESSAGE, nullptr, nullptr, nullptr);
        hHook = SetWindowsHookExW(WH_KEYBOARD_LL, KeyboardHookProc, NULL, NULL);
        const DWORD error = hHook ? ERROR_SUCCESS : GetLastError();
        ready.set_value(error);

        if (hHook)
        {
            while (GetMessageW(&msg, nullptr, 0, 0) > 0)
            {
                DispatchMessageW(&msg);
            }

            UnhookWindowsHookEx(hHook);
            hHook = NULL;
        }

        if (timerWindow)
        {
            // Also kills the key hold timers.
            DestroyWindow(timerWindow);
            timerWindow = nullptr;
        }
    }

    LRESULT CALLBACK DispatchWindowProc(HWND hwnd, UINT message, WPARAM wParam, LPARAM lParam)
    {
        if (message == WM_DRAIN_HOTKEY_ACTIONS)
        {
            dispatcher.DrainPending();
            return 0;
        }

        return DefWindowProcW(hwnd, message, wParam, lParam);
    }

    bool CreateDispatchWindow() noexcept
    {
        const auto instance = GetModuleHandleW(nullptr);
        WNDCLASSEXW windowClass{ .cbSize = sizeof(WNDCLASSEXW), .lpfnWndProc = DispatchWindowProc, .hInstance = instance, .lpszClassName = DispatchWindowClassName };
        if (!RegisterClassExW(&windowClass) && GetLastError() != ERROR_CLASS_ALREADY_EXISTS)
        {
            return false;
        }

        dispatchWindow = CreateWindowExW(0, DispatchWindowClassName, L"", 0, 0, 0, 0, 0, HWND_MESSAGE, nullptr, instance, nullptr);
        return dispatchWindow != nullptr;
    }

    void SetHotkeyAction(const std::wstring& moduleName, const Hotkey& hotkey, std::function<HotkeyDisposition()>&& shouldHandle, std::function<void()>&& invoke) noexcept
    {
        Logger::trace(L"Register hotkey action for {}", moduleName);
        try
        {
            auto label = std::make_shared<const HotkeyDispatch::HotkeyLabel>(HotkeyDispatch::HotkeyLabel{
                .dispatchPath = L"keyboard hook",
                .moduleName = moduleName,
                .hotkeyName = SlowHotkeyWatchdog::DescribeHotkey(hotkey.win, hotkey.ctrl, hotkey.shift, hotkey.alt, hotkey.key) });

            std::unique_lock lock{ mutex };
            hotkeyDescriptors.insert({ .hotkey = hotkey, .moduleName = moduleName, .shouldHandle = std::move(shouldHandle), .invoke = std::move(invoke), .label = std::move(label) });
        }
        catch (...)
        {
            Logger::error(L"Failed to register hotkey action for {}", moduleName);
        }
    }

    void AddPressedKeyAction(const std::wstring& moduleName, const DWORD vk, const UINT milliseconds, std::function<void()>&& action) noexcept
    {
        // Calculate a unique TimerID.
        auto hash = std::hash<std::wstring>{}(moduleName); // Hash the module as the upper part of the timer ID.
        const UINT upperId = hash & 0xFFFF;
        const UINT lowerId = vk & 0xFFFF; // The key to press can be the lower ID.
        const UINT timerId = upperId << 16 | lowerId;
        try
        {
            auto label = std::make_shared<const HotkeyDispatch::HotkeyLabel>(HotkeyDispatch::HotkeyLabel{
                .dispatchPath = L"key hold timer",
                .moduleName = moduleName,
                .hotkeyName = L"hold " + SlowHotkeyWatchdog::DescribeHotkey(false, false, false, false, vk) });

            std::unique_lock lock{ pressedKeyMutex };
            pressedKeyDescriptors.insert({ .virtualKey = vk, .moduleName = moduleName, .action = std::move(action), .label = std::move(label), .idTimer = timerId, .millisecondsToPress = milliseconds });
            hasPressedKeyDescriptors = true;
        }
        catch (...)
        {
            Logger::error(L"Failed to register pressed key action for {}", moduleName);
        }
    }

    void ClearPressedKeyActions(const std::wstring& moduleName) noexcept
    {
        Logger::trace(L"UnRegister pressed key action for {}", moduleName);
        std::unique_lock lock{ pressedKeyMutex };
        const DWORD trackedKey = vkCodePressed.load();
        bool removedTrackedKey = false;
        auto it = pressedKeyDescriptors.begin();
        while (it != pressedKeyDescriptors.end())
        {
            if (it->moduleName == moduleName)
            {
                // A timer that is still pending for this descriptor finds no match when it
                // fires on the hook thread and stops itself.
                removedTrackedKey |= it->virtualKey == trackedKey;
                it = pressedKeyDescriptors.erase(it);
            }
            else
            {
                ++it;
            }
        }

        hasPressedKeyDescriptors = !pressedKeyDescriptors.empty();
        if (pressedKeyDescriptors.empty())
        {
            vkCodePressed = VK_DISABLED;
        }
        else if (removedTrackedKey)
        {
            PressedKeyDescriptor trackedKeyDescriptor{ .virtualKey = trackedKey };
            const auto [first, last] = pressedKeyDescriptors.equal_range(trackedKeyDescriptor);
            if (first == last)
            {
                vkCodePressed = VK_DISABLED;
            }
        }
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
        ClearPressedKeyActions(moduleName);
    }

    void Start() noexcept
    {
#if defined(DISABLE_LOWLEVEL_HOOKS_WHEN_DEBUGGED)
        const bool hook_disabled = IsDebuggerPresent();
#else
        const bool hook_disabled = false;
#endif
        if (hook_disabled || hookThread.joinable())
        {
            return;
        }

        if (!dispatchWindow && !CreateDispatchWindow())
        {
            DWORD errorCode = GetLastError();
            show_last_error_message(L"CreateWindowEx", errorCode, L"centralized_kb_hook");
            return;
        }

        try
        {
            dispatcher.Start(
                [] { PostMessageW(dispatchWindow, WM_DRAIN_HOTKEY_ACTIONS, 0, 0); },
                OnActionCompleted);

            std::promise<DWORD> ready;
            auto hookResult = ready.get_future();
            hookThread = std::thread(HookThreadProc, std::move(ready));
            hookThreadId = GetThreadId(hookThread.native_handle());

            const DWORD errorCode = hookResult.get();
            if (errorCode != ERROR_SUCCESS)
            {
                hookThread.join();
                hookThreadId = 0;
                dispatcher.Shutdown();
                show_last_error_message(L"SetWindowsHookEx", errorCode, L"centralized_kb_hook");
            }
        }
        catch (...)
        {
            dispatcher.Shutdown();
            Logger::error("Failed to start the centralized keyboard hook thread");
        }
    }

    void Stop() noexcept
    {
        // Drop the queued actions before anything else, so that no module action runs after
        // this point even if the caller pumps messages while unloading modules.
        dispatcher.Shutdown();

        if (hookThread.joinable())
        {
            PostThreadMessageW(hookThreadId, WM_QUIT, 0, 0);
            if (WaitForSingleObject(hookThread.native_handle(), static_cast<DWORD>(HookThreadExitTimeout.count())) == WAIT_OBJECT_0)
            {
                hookThread.join();
            }
            else
            {
                Logger::warn("The centralized keyboard hook thread didn't exit in time");
                hookThread.detach();
            }

            hookThreadId = 0;
        }

        vkCodePressed = VK_DISABLED;

        if (dispatchWindow && DestroyWindow(dispatchWindow))
        {
            dispatchWindow = nullptr;
        }
    }
}
