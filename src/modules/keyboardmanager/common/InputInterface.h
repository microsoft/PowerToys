#pragma once

#include <string>
#include <vector>
#include <Windows.h>

namespace KeyboardManagerInput
{
    // Interface used to wrap keyboard input library methods
    class InputInterface
    {
    public:
        // Function to simulate input. Returns false only when nothing could be injected
        // (the call was fully blocked); returns true on full or partial success.
        virtual bool SendVirtualInput(const std::vector<INPUT>& inputs) = 0;

        // Function to get the state of a particular key
        virtual bool GetVirtualKeyState(int key) = 0;

        // Function to get the foreground process name
        virtual void GetForegroundProcess(_Out_ std::wstring& foregroundProcess) = 0;
    };
}