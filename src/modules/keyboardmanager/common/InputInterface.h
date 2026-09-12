#pragma once

#include <cstdint>
#include <string>
#include <vector>
#include <Windows.h>

namespace KeyboardManagerInput
{
    enum class SendVirtualInputStatus : uint8_t
    {
        None,
        Partial,
        Complete,
    };

    struct SendVirtualInputResult
    {
        SendVirtualInputStatus status = SendVirtualInputStatus::None;
        UINT injectedEventCount = 0;

        explicit operator bool() const noexcept
        {
            return status != SendVirtualInputStatus::None;
        }

        bool IsComplete() const noexcept
        {
            return status == SendVirtualInputStatus::Complete;
        }
    };

    // Interface used to wrap keyboard input library methods
    class InputInterface
    {
    public:
        // Function to simulate input. The precise prefix count lets callers repair a
        // partially injected key sequence instead of guessing what reached the system.
        virtual SendVirtualInputResult SendVirtualInput(const std::vector<INPUT>& inputs) = 0;

        // Function to get the state of a particular key
        virtual bool GetVirtualKeyState(int key) = 0;

        // Function to get the foreground process name
        virtual void GetForegroundProcess(_Out_ std::wstring& foregroundProcess) = 0;
    };
}
