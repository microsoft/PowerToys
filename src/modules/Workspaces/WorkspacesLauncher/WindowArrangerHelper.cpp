#include "pch.h"
#include "WindowArrangerHelper.h"

#include <filesystem>

#include <common/utils/OnThreadExecutor.h>
#include <common/utils/winapi_error.h>

#include <WorkspacesLib/WorkspacesData.h>

#include <AppLauncher.h>

WindowArrangerHelper::~WindowArrangerHelper()
{
    OnThreadExecutor().submit(OnThreadExecutor::task_t{
        [&] {
            std::this_thread::sleep_for(std::chrono::milliseconds(6000));

            HANDLE process = OpenProcess(PROCESS_ALL_ACCESS, false, processId);
            if (process)
            {
                bool res = TerminateProcess(process, 0);
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

void WindowArrangerHelper::Launch(const std::wstring& projectId, bool elevated)
{
    Logger::trace(L"Starting WorkspacesWindowArranger");

    TCHAR buffer[MAX_PATH] = { 0 };
    GetModuleFileName(NULL, buffer, MAX_PATH);
    std::wstring path = std::filesystem::path(buffer).parent_path();

    auto res = LaunchApp(path + L"\\PowerToys.WorkspacesWindowArranger.exe", projectId, elevated);
    if (res.isOk())
    {
        auto value = res.value();
        processId = GetProcessId(value.hProcess);
        CloseHandle(value.hProcess);
    }
    else
    {
        Logger::error(L"Failed to launch PowerToys.WorkspacesWindowArranger: {}", res.error());
    }
}
