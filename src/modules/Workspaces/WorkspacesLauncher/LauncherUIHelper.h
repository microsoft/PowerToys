#pragma once

#include <WorkspacesLib/WorkspacesData.h>
#include <WorkspacesLib/IPCHelper.h>

class LauncherUIHelper
{
public:
    LauncherUIHelper(std::function<void(const std::wstring&)> ipcCallback);
    ~LauncherUIHelper();

    void LaunchUI();
    void UpdateLaunchStatus(WorkspacesData::LaunchingAppStateMap launchedApps) const;

private:
    DWORD m_processId;
    IPCHelper m_ipcHelper;
};
