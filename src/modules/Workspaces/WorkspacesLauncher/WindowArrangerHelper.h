#pragma once

#include <WorkspacesLib/IPCHelper.h>
#include <WorkspacesLib/WorkspacesData.h>

#include <common/utils/OnThreadExecutor.h>
#include <wil/resource.h>

class WindowArrangerHelper
{
public:
    WindowArrangerHelper(std::function<void(const std::wstring&)> ipcCallback, std::wstring operationId = L"");
    ~WindowArrangerHelper();

    DWORD Launch(const std::wstring& projectId, bool elevated, std::function<bool()> keepWaitingCallback, DWORD timeoutSeconds = 0);
    void UpdateLaunchStatus(const WorkspacesData::LaunchingAppState& appState) const;
    void KeepLaunchingAlive() const;
    void SendSnapshot(const WorkspacesData::WorkspacesProject& project) const;
    void AcknowledgeResult() const;

private:
    DWORD m_processId;
    wil::unique_handle m_process;
    std::wstring m_operationId;
    IPCHelper m_ipcHelper;
    OnThreadExecutor m_threadExecutor;
};
