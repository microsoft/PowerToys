#include "pch.h"
#include "WindowArrangerHelper.h"

#include <filesystem>

#include <common/utils/OnThreadExecutor.h>
#include <common/utils/winapi_error.h>

#include <WorkspacesLib/WorkspacesData.h>

#include <AppLauncher.h>

WindowArrangerHelper::WindowArrangerHelper(std::function<void(const std::wstring&)> ipcCallback) :
    m_processId{},
    m_ipcHelper(IPCHelperStrings::LauncherArrangerPipeName, IPCHelperStrings::WindowArrangerPipeName, ipcCallback)
{
}

WindowArrangerHelper::~WindowArrangerHelper()
{
    Logger::info(L"Stopping WorkspacesWindowArranger with pid {}", m_processId);
    
    HANDLE process = m_process.get();
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
}

void WindowArrangerHelper::Launch(const WorkspacesData::WorkspacesProject& project, bool elevated, std::function<bool()> keepWaitingCallback)
{
    Logger::trace(L"Starting WorkspacesWindowArranger");

    TCHAR buffer[MAX_PATH] = { 0 };
    GetModuleFileName(NULL, buffer, MAX_PATH);
    std::wstring path = std::filesystem::path(buffer).parent_path();

    Workspaces::LaunchSession session;
    auto res = AppLauncher::LaunchApp(path + L"\\PowerToys.WorkspacesWindowArranger.exe", session.Arguments(), elevated);
    if (res.isOk())
    {
        auto value = res.value();
        m_processId = GetProcessId(value.hProcess);
        m_process.reset(value.hProcess);
        session.Send(m_process.get(), project);
        Logger::info(L"WorkspacesWindowArranger started with pid {}", m_processId);
        std::atomic_bool timeoutExpired = false;
        m_threadExecutor.submit(OnThreadExecutor::task_t{
            [&] {
                HANDLE process = value.hProcess;
                while (keepWaitingCallback() && WaitForSingleObject(process, 0) == WAIT_TIMEOUT)
                {
                    WaitForSingleObject(process, 100);
                }
                
                Logger::trace(L"Finished waiting WorkspacesWindowArranger");
            }}).wait();

        timeoutExpired = true;
    }
    else
    {
        Logger::error(L"Failed to launch PowerToys.WorkspacesWindowArranger: {}", res.error());
        throw PowerToys::ProtectedStorage::StorageError(PowerToys::ProtectedStorage::ErrorCode::AuthorizationRequired);
    }
}

void WindowArrangerHelper::UpdateLaunchStatus(const WorkspacesData::LaunchingAppState& appState) const
{
    m_ipcHelper.send(WorkspacesData::AppLaunchInfoJSON::ToJson({ appState.application, nullptr, appState.state }).ToString().c_str());
}
