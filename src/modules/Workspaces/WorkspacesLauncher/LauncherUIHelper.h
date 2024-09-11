#pragma once

#include <LaunchingApp.h>
#include <WorkspacesLib/IPCHelper.h>

class LauncherUIHelper
{
public:
    LauncherUIHelper();
    ~LauncherUIHelper();

    void LaunchUI();
    void UpdateLaunchStatus(LaunchingApps launchedApps) const;

private:
    DWORD uiProcessId;
    IPCHelper ipcHelper;
};
