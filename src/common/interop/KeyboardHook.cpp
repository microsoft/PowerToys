#include "pch.h"
#include "KeyboardHook.h"
#include "KeyboardHook.g.cpp"
#include <common/debug_control.h>
#include <common/utils/winapi_error.h>

namespace winrt::PowerToys::Interop::implementation
{
    std::mutex KeyboardHook::instancesMutex;
    std::unordered_set<KeyboardHook*> KeyboardHook::instances;

    KeyboardHook::KeyboardHook(winrt::PowerToys::Interop::KeyboardEventCallback const& keyboardEventCallback, winrt::PowerToys::Interop::IsActiveCallback const& isActiveCallback, winrt::PowerToys::Interop::FilterKeyboardEvent const& filterKeyboardEvent)
    {
        this->keyboardEventCallback = keyboardEventCallback;
        this->isActiveCallback = isActiveCallback;
        this->filterKeyboardEvent = filterKeyboardEvent;
    }

    void KeyboardHook::Close()
    {
        std::unique_lock lock { instancesMutex };
        auto iter = instances.find(this);
        if (iter != instances.end())
        {
            instances.erase(iter);
        }
        if (instances.size() < 1 && hookHandle != nullptr)
        {
            if (UnhookWindowsHookEx(hookHandle))
            {
                hookHandle = nullptr;
            }
        }
    }


    void KeyboardHook::Start()
    {
#if defined(DISABLE_LOWLEVEL_HOOKS_WHEN_DEBUGGED)
        const bool hookDisabled = IsDebuggerPresent();
#else
        const bool hookDisabled = false;
#endif
        if (!hookDisabled)
        {
            std::unique_lock lock { instancesMutex };
            assert(instances.find(this) == instances.end());
            // register low level hook procedure
            instances.insert(this);
            if (hookHandle == nullptr)
            {
                hookHandle = SetWindowsHookEx(
                    WH_KEYBOARD_LL,
                    HookProc,
                    0,
                    0);
                if (hookHandle == nullptr)
                {
                    DWORD errorCode = GetLastError();
                    show_last_error_message(L"SetWindowsHookEx", errorCode, L"PowerToys - Interop");
                }
            }
        }
    }
    LRESULT KeyboardHook::HookProc(int nCode, WPARAM wParam, LPARAM lParam)
    {
        if (nCode == HC_ACTION)
        {
            std::vector<KeyboardHook*> instances_copy;
            {
                /* Use a copy of instances, to iterate through the copy without needing to maintain the lock */
                std::unique_lock lock{ instancesMutex };
                instances_copy.reserve(instances.size());
                std::copy(instances.begin(), instances.end(), std::back_inserter(instances_copy));
            }

            for (auto const& s_instance : instances_copy)
            {
                if (s_instance->isActiveCallback())
                {
                    KeyboardEvent ev;
                    ev.message = wParam;
                    ev.key = reinterpret_cast<KBDLLHOOKSTRUCT*>(lParam)->vkCode;
                    ev.dwExtraInfo = reinterpret_cast<KBDLLHOOKSTRUCT*>(lParam)->dwExtraInfo;

                    // Ignore the keyboard hook if the FilterkeyboardEvent returns false.
                    if ((s_instance->filterKeyboardEvent != nullptr && !s_instance->filterKeyboardEvent(ev)))
                    {
                        continue;
                    }

                    s_instance->keyboardEventCallback(ev);
                    return 1;
                }
            }
        }
        return CallNextHookEx(NULL, nCode, wParam, lParam);
    }
}
