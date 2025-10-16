#include "pch.h"
#include "ProcessWaiter.h"

#include <utility>

namespace ProcessWaiter
{
    void OnProcessTerminate(std::wstring parent_pid, std::function<void(DWORD)> callback)
    {
        DWORD pid = 0;
        try
        {
            pid = std::stoul(parent_pid);
        }
        catch (...)
        {
            callback(ERROR_INVALID_PARAMETER);
            return;
        }

        std::thread([pid, callback = std::move(callback)]() mutable {
            wil::unique_handle process{ OpenProcess(SYNCHRONIZE, FALSE, pid) };
            if (process)
            {
                if (WaitForSingleObject(process.get(), INFINITE) == WAIT_OBJECT_0)
                {
                    callback(ERROR_SUCCESS);
                }
                else
                {
                    callback(GetLastError());
                }
            }
            else
            {
                callback(GetLastError());
            }
        }).detach();
    }
}
