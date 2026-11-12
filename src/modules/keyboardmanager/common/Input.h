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
        // Function to simulate input. Returns false only when nothing could be injected
        // (the call was fully blocked); returns true on full or partial success. A partial
        // injection means some remap events already reached the system, so passing the
        // original key through on top of them would corrupt the input stream (e.g. leave a
        // modifier stuck). In that rare case we suppress the original and log a warning.
        bool SendVirtualInput(const std::vector<INPUT>& inputs)
        {
            if (inputs.empty())
            {
                return true;
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
                return false;
            }
            if (eventCount != copy.size())
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
            return true;
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
