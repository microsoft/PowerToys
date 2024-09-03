#include "pch.h"
#include "WindowArrangerHelper.h"

#include <filesystem>

#include <common/utils/OnThreadExecutor.h>
#include <common/utils/winapi_error.h>

#include <WorkspacesLib/WorkspacesData.h>

WindowArrangerHelper::~WindowArrangerHelper()
{
    OnThreadExecutor().submit(OnThreadExecutor::task_t{
        [&] {
            std::this_thread::sleep_for(std::chrono::milliseconds(6000));

            HANDLE uiProcess = OpenProcess(PROCESS_ALL_ACCESS, false, uiProcessId);
            if (uiProcess)
            {
                bool res = TerminateProcess(uiProcess, 0);
                if (!res)
                {
                    Logger::error(L"Unable to terminate PowerToys.WorkspacesWindowArranger process: {}", get_last_error_or_default(GetLastError()));
                }
            }
            else
            {
                Logger::error(L"Unable to find PowerToys.WorkspacesWindowArranger process: {}", get_last_error_or_default(GetLastError()));
            }
        } }).wait();
}

void WindowArrangerHelper::Launch(const std::wstring& projectId)
{
    Logger::trace(L"Starting WorkspacesWindowArranger");

    STARTUPINFO info = { sizeof(info) };
    PROCESS_INFORMATION pi = { 0 };
    TCHAR buffer[MAX_PATH] = { 0 };
    GetModuleFileName(NULL, buffer, MAX_PATH);
    std::wstring path = std::filesystem::path(buffer).parent_path();
    path.append(L"\\PowerToys.WorkspacesWindowArranger.exe");
    std::wstring commandLine = projectId;
    auto succeeded = CreateProcessW(path.c_str(), commandLine.data(), nullptr, nullptr, FALSE, 0, nullptr, nullptr, &info, &pi);
    if (succeeded)
    {
        if (pi.hProcess)
        {
            uiProcessId = pi.dwProcessId;
            CloseHandle(pi.hProcess);
        }
        if (pi.hThread)
        {
            CloseHandle(pi.hThread);
        }
    }
    else
    {
        Logger::error(L"CreateProcessW() failed. {}", get_last_error_or_default(GetLastError()));
    }
}
