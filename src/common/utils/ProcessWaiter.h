#include <functional>
#include <string>
#include <Windows.h>
#include <thread>

namespace ProcessWaiter
{
    void OnProcessTerminate(std::wstring parent_pid, std::function<void(DWORD)> callback)
    {
        DWORD pid = std::stol(parent_pid);
        std::thread([=]() {
            HANDLE process = OpenProcess(SYNCHRONIZE, FALSE, pid);
            if (process != nullptr)
            {
                if (WaitForSingleObject(process, INFINITE) == WAIT_OBJECT_0)
                {
                    CloseHandle(process);
                    callback(ERROR_SUCCESS);
                }
                else
                {
                    CloseHandle(process);
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
