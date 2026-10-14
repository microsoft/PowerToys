#pragma once

#include <WorkspacesLib/WorkspacesData.h>
#include <WorkspacesLib/IPCHelper.h>
#include <WorkspacesLib/SignatureVerification.h>

class LauncherUIHelper
{
public:
    LauncherUIHelper(std::function<void(const std::wstring&)> ipcCallback);
    ~LauncherUIHelper();

    bool LaunchUI();
    void UpdateLaunchStatus(WorkspacesData::LaunchingAppStateMap launchedApps) const;
    bool IsRunning() const;
    bool RequestApproval(const std::wstring& requestId, const std::wstring& name, const std::wstring& path, const std::wstring& arguments, const SignatureVerification::Result& result) const;
    void DismissApproval(const std::wstring& requestId) const;

private:
    DWORD m_processId;
    wil::unique_handle m_process;
    IPCHelper m_ipcHelper;
};
