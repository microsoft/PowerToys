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
            // This is called for every key event, down and up, from inside the low-level
            // keyboard hook. Resolving the name costs an OpenProcess plus a
            // QueryFullProcessImageName, and Windows silently drops hooks whose callbacks
            // exceed LowLevelHooksTimeout (300 ms by default). Cache the result and only pay
            // that cost when the foreground window actually changes. The key includes the
            // owning process id so a recycled window handle cannot serve a stale name.
            // Only accessed from the (serialized) low-level keyboard hook thread.
            const HWND foregroundWindow = GetForegroundWindow();
            DWORD foregroundProcessId = 0;
            if (foregroundWindow != nullptr)
            {
                GetWindowThreadProcessId(foregroundWindow, &foregroundProcessId);
            }

            if (!m_foregroundCacheValid ||
                foregroundWindow != m_cachedForegroundWindow ||
                foregroundProcessId != m_cachedForegroundProcessId)
            {
                bool resolvedFromUWPFrame = false;
                m_cachedForegroundProcessName = Helpers::GetApplicationForWindow(foregroundWindow, false, &resolvedFromUWPFrame);
                m_cachedForegroundWindow = foregroundWindow;
                m_cachedForegroundProcessId = foregroundProcessId;

                // A full-screen UWP app is reached through an ApplicationFrameHost frame
                // window, which can outlive the hosted app it currently shows. That makes the
                // frame window unusable as a cache key, so keep resolving it on every event.
                m_foregroundCacheValid = !resolvedFromUWPFrame;
            }

            foregroundProcess = m_cachedForegroundProcessName;
        }

    private:
        // Cached result of the last foreground process lookup, see GetForegroundProcess.
        HWND m_cachedForegroundWindow = nullptr;
        DWORD m_cachedForegroundProcessId = 0;
        std::wstring m_cachedForegroundProcessName;
        bool m_foregroundCacheValid = false;
    };
}
