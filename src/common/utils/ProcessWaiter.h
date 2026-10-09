#include <functional>
#include <string>
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
        std::thread([=]() {
            HANDLE process = OpenProcess(SYNCHRONIZE, FALSE, pid);
            if (process != nullptr)
            {
                if (WaitForSingleObject(process, INFINITE) == WAIT_OBJECT_0)
                {
                    CloseHandle(process);
                    if (callback)
                    {
                        callback(ERROR_SUCCESS);
                    }
                }
                else
                {
                    CloseHandle(process);
                    if (callback)
                    {
                        callback(GetLastError());
                    }
                }
            }
            else
            {
                if (callback)
                {
                    callback(GetLastError());
                }
            }
        }).detach();
    }
}
