#include <functional>
#include <memory>
#include <string>
#include <type_traits>
#include <Windows.h>
#include <thread>

namespace ProcessWaiter
{
    void OnProcessTerminate(std::wstring parent_pid, std::function<void(DWORD)> callback)
    {
        DWORD pid = 0;
        try
        {
            pid = std::stol(parent_pid);
        }
        catch (...)
        {
            if (callback)
            {
                callback(ERROR_INVALID_PARAMETER);
            }
            return;
        }
        // Detached on purpose: the INFINITE wait must never block the caller or process shutdown.
        std::thread([=]() {
            std::unique_ptr<std::remove_pointer_t<HANDLE>, decltype(&CloseHandle)> process{ OpenProcess(SYNCHRONIZE, FALSE, pid), &CloseHandle };
            if (!process)
            {
                if (callback)
                {
                    callback(GetLastError());
                }
                return;
            }

            const DWORD result = WaitForSingleObject(process.get(), INFINITE) == WAIT_OBJECT_0 ? ERROR_SUCCESS : GetLastError();
            process.reset();
            if (callback)
            {
                callback(result);
            }
        }).detach();
    }
}
