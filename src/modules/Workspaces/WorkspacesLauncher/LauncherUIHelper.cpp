#include "pch.h"
#include "LauncherUIHelper.h"

#include <filesystem>
#include <shellapi.h>

#include <common/utils/OnThreadExecutor.h>
#include <common/utils/winapi_error.h>

#include <AppLauncher.h>

LauncherUIHelper::LauncherUIHelper(std::function<void(const std::wstring&)> ipcCallback) :
    m_processId{},
    m_ipcHelper(IPCHelperStrings::LauncherUIPipeName, IPCHelperStrings::UIPipeName, ipcCallback)
{
}

LauncherUIHelper::~LauncherUIHelper()
{
    OnThreadExecutor().submit(OnThreadExecutor::task_t{ [&] {
        std::this_thread::sleep_for(std::chrono::milliseconds(1000));

        Logger::info(L"Stopping WorkspacesLauncherUI with pid {}", m_processId);
    
        HANDLE uiProcess = OpenProcess(PROCESS_ALL_ACCESS, false, m_processId);
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
    } }).wait();
}

void LauncherUIHelper::LaunchUI()
{
    Logger::trace(L"Starting WorkspacesLauncherUI");
    
    TCHAR buffer[MAX_PATH] = { 0 };
    GetModuleFileName(NULL, buffer, MAX_PATH);
    std::wstring path = std::filesystem::path(buffer).parent_path();

    auto res = AppLauncher::LaunchApp(path + L"\\PowerToys.WorkspacesLauncherUI.exe", L"", false);
    if (res.isOk())
    {
        auto value = res.value();
        m_processId = GetProcessId(value.hProcess);
        CloseHandle(value.hProcess);
        Logger::info(L"WorkspacesLauncherUI started with pid {}", m_processId);
    }
    else
    {
        Logger::error(L"Failed to launch PowerToys.WorkspacesLauncherUI: {}", res.error());
    }
}

void LauncherUIHelper::UpdateLaunchStatus(WorkspacesData::LaunchingAppStateMap launchedApps) const
{
    WorkspacesData::AppLaunchData appData;
    appData.launcherProcessID = GetCurrentProcessId();
    for (auto& [app, data] : launchedApps)
    {
        appData.appsStateList.insert({ app, { app, nullptr, data.state } });
    }

    m_ipcHelper.send(WorkspacesData::AppLaunchDataJSON::ToJson(appData).ToString().c_str());
}
