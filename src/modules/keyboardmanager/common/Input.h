#pragma once

#include <common/utils/winapi_error.h>
#include <keyboardmanager/common/InputInterface.h>
#include <keyboardmanager/common/Helpers.h>

namespace KeyboardManagerInput
{
    // Class used to wrap keyboard input library methods
    class Input : public InputInterface
    {
    public:
        // Function to simulate input
        void SendVirtualInput(std::vector<INPUT>& inputs)
        {
            const auto size = static_cast<UINT>(inputs.size());
            if (SendInput(size, inputs.data(), sizeof(INPUT)) != size)
            {
                Logger::error(L"Failed to send input. {0}", get_last_error_or_default(GetLastError()));
            }
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
