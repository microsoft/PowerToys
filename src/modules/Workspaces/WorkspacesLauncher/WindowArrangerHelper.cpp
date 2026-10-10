#include "pch.h"
#include "WindowArrangerHelper.h"

#include <filesystem>

#include <common/utils/OnThreadExecutor.h>
#include <common/utils/winapi_error.h>

#include <WorkspacesLib/WorkspacesData.h>

#include <AppLauncher.h>

WindowArrangerHelper::WindowArrangerHelper(std::function<void(const std::wstring&)> ipcCallback, std::wstring operationId) :
    m_processId{},
    m_operationId(operationId),
    m_ipcHelper(IPCHelperStrings::LauncherArrangerPipeName + operationId, IPCHelperStrings::WindowArrangerPipeName + operationId, ipcCallback, operationId.empty() ? interop_auth::CallerPolicy{} : IPCHelper::ModulePeer(L"PowerToys.WorkspacesWindowArranger.exe"))
{
}

WindowArrangerHelper::~WindowArrangerHelper()
{
    if (!m_process)
        return;
    Logger::info(L"Stopping WorkspacesWindowArranger with pid {}", m_processId);
    if (WaitForSingleObject(m_process.get(), 0) == WAIT_TIMEOUT)
    {
        bool res = TerminateProcess(m_process.get(), 0);
        if (!res)
        {
            Logger::error(L"Unable to terminate PowerToys.WorkspacesWindowArranger process: {}", get_last_error_or_default(GetLastError()));
        }
    }
}

DWORD WindowArrangerHelper::Launch(const std::wstring& projectId, bool elevated, std::function<bool()> keepWaitingCallback, DWORD timeoutSeconds)
{
    Logger::trace(L"Starting WorkspacesWindowArranger");

    TCHAR buffer[MAX_PATH] = { 0 };
    GetModuleFileName(NULL, buffer, MAX_PATH);
    std::wstring path = std::filesystem::path(buffer).parent_path();

    std::wstring arguments = projectId;
    if (!m_operationId.empty())
        arguments += L" --cli-worker " + m_operationId + L" " + std::to_wstring(GetCurrentProcessId()) + L" " + std::to_wstring(timeoutSeconds);
    auto res = AppLauncher::LaunchApp(path + L"\\PowerToys.WorkspacesWindowArranger.exe", arguments, elevated);
    if (res.isOk())
    {
        auto value = res.value();
        m_processId = GetProcessId(value.hProcess);
        m_process.reset(value.hProcess);
        Logger::info(L"WorkspacesWindowArranger started with pid {}", m_processId);
        std::atomic_bool timeoutExpired = false;
        m_threadExecutor.submit(OnThreadExecutor::task_t{
            [&] {
                while (keepWaitingCallback())
                {
                    if (WaitForSingleObject(m_process.get(), 100) != WAIT_TIMEOUT)
                    {
                        break;
                    }
                }
                
                Logger::trace(L"Finished waiting WorkspacesWindowArranger");
            }}).wait();

        timeoutExpired = true;
        return ERROR_SUCCESS;
    }
    else
    {
        Logger::error(L"Failed to launch PowerToys.WorkspacesWindowArranger: {}", res.error().message);
        return res.error().code;
    }
}

void WindowArrangerHelper::SendSnapshot(const WorkspacesData::WorkspacesProject& project) const
{
    json::JsonObject snapshot;
    snapshot.SetNamedValue(L"protocolVersion", json::value(1));
    snapshot.SetNamedValue(L"operationId", json::value(m_operationId));
    snapshot.SetNamedValue(L"workspace", WorkspacesData::WorkspacesProjectJSON::ToJson(project));
    m_ipcHelper.send(snapshot.Stringify().c_str());
}

void WindowArrangerHelper::AcknowledgeResult() const
{
    m_ipcHelper.send(L"result-ack");
}

void WindowArrangerHelper::UpdateLaunchStatus(const WorkspacesData::LaunchingAppState& appState) const
{
    m_ipcHelper.send(WorkspacesData::AppLaunchInfoJSON::ToJson({ appState.application, nullptr, appState.state }).ToString().c_str());
}

void WindowArrangerHelper::KeepLaunchingAlive() const
{
    m_ipcHelper.send(L"launching-heartbeat");
}
