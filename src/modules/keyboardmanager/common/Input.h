#pragma once

#include <common/logger/logger.h>
#include <common/utils/winapi_error.h>
#include <keyboardmanager/common/Helpers.h>
#include <keyboardmanager/common/InputInterface.h>

namespace KeyboardManagerInput
{
    // Class used to wrap keyboard input library methods
    class Input : public InputInterface
    {
    public:
        // Function to simulate input
        void SendVirtualInput(const std::vector<INPUT>& inputs)
        {
            std::vector<INPUT> copy = inputs;
            UINT eventCount = SendInput(static_cast<UINT>(copy.size()), copy.data(), sizeof(INPUT));
            if (eventCount != copy.size())
            {
                Logger::error(
                    L"Failed to send input events. {}",
                    get_last_error_or_default(GetLastError()));
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
