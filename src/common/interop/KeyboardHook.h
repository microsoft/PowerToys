#pragma once
#include "KeyboardHook.g.h"
#include <mutex>
#include <unordered_set>

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

        // This class used to be C++/CX, which meant it ran on .NET runtime and was able to send function pointer for delegates as hook procedures for SetWindowsHookEx which kept an object reference.
        // There doesn't seem to be a way to do this outside of the .NET runtime that allows us to get a proper C-style function.
        // The alternative when porting to C++/winrt is to keep track of every instance and use a single proc instead of one per object. This should also make it lighter.
        static std::mutex instancesMutex;
        static std::unordered_set<KeyboardHook*> instances;
        static inline HHOOK hookHandle = nullptr;
        static LRESULT CALLBACK HookProc(int nCode, WPARAM wParam, LPARAM lParam);
    };
}
namespace winrt::PowerToys::Interop::factory_implementation
{
    struct KeyboardHook : KeyboardHookT<KeyboardHook, implementation::KeyboardHook>
    {
    };
}
