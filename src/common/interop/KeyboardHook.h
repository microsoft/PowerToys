#pragma once
#include "KeyboardHook.g.h"

namespace winrt::interop::implementation
{
    struct KeyboardHook : KeyboardHookT<KeyboardHook>
    {
        // KeyboardHook() = default;

        KeyboardHook(winrt::interop::KeyboardEventCallback const& keyboardEventCallback, winrt::interop::IsActiveCallback const& isActiveCallback, winrt::interop::FilterKeyboardEvent const& filterKeyboardEvent);
        void Start();
        void Close();

    private:
        winrt::interop::KeyboardEventCallback keyboardEventCallback;
        winrt::interop::IsActiveCallback isActiveCallback;
        winrt::interop::FilterKeyboardEvent filterKeyboardEvent;
        HHOOK hookHandle = nullptr;
        static LRESULT CALLBACK HookProc(int nCode, WPARAM wParam, LPARAM lParam);
        static inline KeyboardHook* s_instance = nullptr; // Only support one instance of KeyboardHook
    };
}
namespace winrt::interop::factory_implementation
{
    struct KeyboardHook : KeyboardHookT<KeyboardHook, implementation::KeyboardHook>
    {
    };
}
