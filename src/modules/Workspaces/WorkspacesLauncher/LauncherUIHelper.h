#pragma once

#include <WorkspacesLib/WorkspacesData.h>
#include <WorkspacesLib/IPCHelper.h>

class LauncherUIHelper
{
public:
    LauncherUIHelper();
    ~LauncherUIHelper();

    void LaunchUI();
    void UpdateLaunchStatus(WorkspacesData::LaunchingAppStateMap launchedApps) const;

private:
    DWORD uiProcessId;
    IPCHelper ipcHelper;
};
