#pragma once

#include <WorkspacesLib/WorkspacesData.h>
#include <WorkspacesLib/LauncherIpcServer.h>
#include <WorkspacesLib/SignatureVerification.h>

class LauncherUIHelper
{
public:
    LauncherUIHelper(std::function<void(const std::wstring&)> ipcCallback, std::function<void(LauncherIpcFailure)> failureCallback);
    ~LauncherUIHelper();

    bool LaunchUI();
    void Shutdown();
    void UpdateLaunchStatus(WorkspacesData::LaunchingAppStateMap launchedApps) const;
    bool IsRunning() const;
    bool IsReady() const;
    bool MarkReady();
    bool WaitForReady() const;
    void Disconnect();
    bool RequestApproval(const std::wstring& requestId, const std::wstring& name, const std::wstring& path, const std::wstring& arguments, const SignatureVerification::Result& result) const;
    void DismissApproval(const std::wstring& requestId) const;

private:
    DWORD m_processId;
    wil::unique_handle m_process;
    wil::unique_event m_readyEvent;
    std::atomic<bool> m_ready{};
    mutable LauncherIpcServer m_channel;
};
