#pragma once

#include <keyboardmanager/common/InputInterface.h>
#include <keyboardmanager/common/Helpers.h>

namespace KeyboardManagerInput
{
    // Class used to wrap keyboard input library methods
    class Input : public InputInterface
    {
    public:
        // Function to simulate input
        UINT SendVirtualInput(UINT cInputs, LPINPUT pInputs, int cbSize)
        {
            return SendInput(cInputs, pInputs, cbSize);
        }

        // Function to get the state of a particular key
        bool GetVirtualKeyState(int key)
        {
            return (GetAsyncKeyState(key) & 0x8000);
        }

        // Function to get the foreground process name
        void GetForegroundProcess(_Out_ std::wstring& foregroundProcess)
        {
            foregroundProcess = Helpers::GetCurrentApplication(false);
        }
    };
}
