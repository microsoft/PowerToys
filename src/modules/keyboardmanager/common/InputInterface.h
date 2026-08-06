#pragma once

#include <string>
#include <vector>
#include <Windows.h>

namespace KeyboardManagerInput
{
    enum class VirtualInputResult
    {
        None,
        Partial,
        Complete,
    };

    // Interface used to wrap keyboard input library methods
    class InputInterface
    {
    public:
        // Function to simulate input. Returns false only when nothing could be injected
        // (the call was fully blocked); returns true on full or partial success.
        virtual bool SendVirtualInput(const std::vector<INPUT>& inputs) = 0;

        // Detailed injection result for callers that must distinguish partial mutation.
        // Existing implementations remain source compatible through this default.
        virtual VirtualInputResult SendVirtualInputWithResult(const std::vector<INPUT>& inputs)
        {
            return SendVirtualInput(inputs) ? VirtualInputResult::Complete : VirtualInputResult::None;
        }

        // Function to get the state of a particular key
        virtual bool GetVirtualKeyState(int key) = 0;

        // Function to get the foreground process name
        virtual void GetForegroundProcess(_Out_ std::wstring& foregroundProcess) = 0;
    };
}
