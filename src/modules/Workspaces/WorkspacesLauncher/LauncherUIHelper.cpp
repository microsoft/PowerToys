#include "pch.h"
#include "LauncherUIHelper.h"

#include <filesystem>
#include <shellapi.h>

#include <common/utils/OnThreadExecutor.h>
#include <common/utils/winapi_error.h>

LauncherUIHelper::~LauncherUIHelper()
{
    OnThreadExecutor().submit(OnThreadExecutor::task_t{ [&] {
        std::this_thread::sleep_for(std::chrono::milliseconds(1000));

        HANDLE uiProcess = OpenProcess(PROCESS_ALL_ACCESS, false, uiProcessId);
        if (uiProcess)
        {
            bool res = TerminateProcess(uiProcess, 0);
            if (!res)
            {
                Logger::error(L"Unable to terminate UI process: {}", get_last_error_or_default(GetLastError()));
            }
        }
        else
        {
            Logger::error(L"Unable to find UI process: {}", get_last_error_or_default(GetLastError()));
        }
        
        std::filesystem::remove(WorkspacesData::LaunchWorkspacesFile());
    } }).wait();
}

void LauncherUIHelper::LaunchUI()
{
    Logger::trace(L"Starting WorkspacesLauncherUI");

    STARTUPINFO info = { sizeof(info) };
    PROCESS_INFORMATION pi = { 0 };
    TCHAR buffer[MAX_PATH] = { 0 };
    GetModuleFileName(NULL, buffer, MAX_PATH);
    std::wstring path = std::filesystem::path(buffer).parent_path();
    path.append(L"\\PowerToys.WorkspacesLauncherUI.exe");
    auto succeeded = CreateProcessW(path.c_str(), nullptr, nullptr, nullptr, FALSE, 0, nullptr, nullptr, &info, &pi);
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

void LauncherUIHelper::UpdateLaunchStatus(LaunchingApps launchedApps)
{
    WorkspacesData::AppLaunchData appData = WorkspacesData::AppLaunchData();
    appData.appLaunchInfoList.reserve(launchedApps.size());
    appData.launcherProcessID = GetCurrentProcessId();
    for (auto& app : launchedApps)
    {
        WorkspacesData::AppLaunchInfo appLaunchInfo = WorkspacesData::AppLaunchInfo();
        appLaunchInfo.name = app.application.name;
        appLaunchInfo.path = app.application.path;
        appLaunchInfo.state = app.state;

        appData.appLaunchInfoList.push_back(appLaunchInfo);
    }

    json::to_file(WorkspacesData::LaunchWorkspacesFile(), WorkspacesData::AppLaunchDataJSON::ToJson(appData));
}
