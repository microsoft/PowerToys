#pragma once
#include "KeyboardHook.g.h"

namespace winrt::PowerToys::Interop::implementation
{
    struct KeyboardHook : KeyboardHookT<KeyboardHook>
    {
        // KeyboardHook() = default;

        KeyboardHook(winrt::PowerToys::Interop::KeyboardEventCallback const& keyboardEventCallback, winrt::PowerToys::Interop::IsActiveCallback const& isActiveCallback, winrt::PowerToys::Interop::FilterKeyboardEvent const& filterKeyboardEvent);
        void Start();
        void Close();

    private:
        winrt::PowerToys::Interop::KeyboardEventCallback keyboardEventCallback;
        winrt::PowerToys::Interop::IsActiveCallback isActiveCallback;
        winrt::PowerToys::Interop::FilterKeyboardEvent filterKeyboardEvent;
        HHOOK hookHandle = nullptr;
        static LRESULT CALLBACK HookProc(int nCode, WPARAM wParam, LPARAM lParam);
        static inline KeyboardHook* s_instance = nullptr; // Only support one instance of KeyboardHook
    };
}
namespace winrt::PowerToys::Interop::factory_implementation
{
    struct KeyboardHook : KeyboardHookT<KeyboardHook, implementation::KeyboardHook>
    {
    };
}
