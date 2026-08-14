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
        SendVirtualInputResult SendVirtualInput(const std::vector<INPUT>& inputs) override
        {
            if (inputs.empty())
            {
                return { SendVirtualInputStatus::Complete, 0 };
            }

            std::vector<INPUT> copy = inputs;
            UINT eventCount = SendInput(static_cast<UINT>(copy.size()), copy.data(), sizeof(INPUT));
            if (eventCount == 0)
            {
                // Nothing was injected (e.g. blocked by UIPI). The caller passes the
                // original key through so the user is never left with a dead key.
                Logger::error(
                    L"Failed to send input events. {}",
                    get_last_error_or_default(GetLastError()));
                return { SendVirtualInputStatus::None, 0 };
            }
            if (eventCount != copy.size())
            {
                Logger::warn(
                    L"Partially sent input events ({} of {}). {}",
                    eventCount,
                    static_cast<UINT>(copy.size()),
                    get_last_error_or_default(GetLastError()));
                return { SendVirtualInputStatus::Partial, eventCount };
            }
            return { SendVirtualInputStatus::Complete, eventCount };
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
