#pragma once

#include <LaunchingApp.h>

class LauncherUIHelper
{
public:
    LauncherUIHelper() = default;
    ~LauncherUIHelper();

    void LaunchUI();
    void UpdateLaunchStatus(LaunchingApps launchedApps);

private:
    DWORD uiProcessId;
};
