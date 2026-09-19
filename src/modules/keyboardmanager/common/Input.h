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
        // Function to simulate input. The result reports the exact injected prefix so
        // transaction-oriented callers can distinguish no mutation from partial mutation.
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
            if (eventCount != static_cast<UINT>(copy.size()))
            {
                // Partial injection: SendInput stopped after some events. Report success so
                // the caller suppresses the original event rather than layering it on top of
                // a half-applied remap, which could strand a key or modifier down.
                Logger::warn(
                    L"Partially sent input events ({} of {}). {}",
                    eventCount,
                    static_cast<UINT>(copy.size()),
                    get_last_error_or_default(GetLastError()));
            }
            return {
                eventCount == static_cast<UINT>(copy.size()) ? SendVirtualInputStatus::Complete : SendVirtualInputStatus::Partial,
                eventCount,
            };
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
