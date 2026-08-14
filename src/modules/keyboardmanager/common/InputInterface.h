#pragma once

#include <cstddef>
#include <string>
#include <vector>
#include <Windows.h>

namespace KeyboardManagerInput
{
    enum class SendVirtualInputStatus
    {
        None,
        Partial,
        Complete,
    };

    struct SendVirtualInputResult
    {
        SendVirtualInputStatus status = SendVirtualInputStatus::None;
        size_t injectedEventCount = 0;

        constexpr bool IsComplete() const noexcept
        {
            return status == SendVirtualInputStatus::Complete;
        }

        constexpr bool HasInjectedEvents() const noexcept
        {
            return injectedEventCount != 0;
        }
    };

    // Interface used to wrap keyboard input library methods
    class InputInterface
    {
    public:
        // Function to simulate input. The exact prefix count matters because SendInput can
        // succeed partially; callers must not mistake a truncated sequence for completion.
        virtual SendVirtualInputResult SendVirtualInput(const std::vector<INPUT>& inputs) = 0;

        // Function to get the state of a particular key
        virtual bool GetVirtualKeyState(int key) = 0;

        // Function to get the foreground process name
        virtual void GetForegroundProcess(_Out_ std::wstring& foregroundProcess) = 0;
    };
}
