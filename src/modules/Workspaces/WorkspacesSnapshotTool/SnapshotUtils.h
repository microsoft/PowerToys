#pragma once

#include <WorkspacesLib/WorkspacesData.h>

namespace SnapshotUtils
{
    std::vector<WorkspacesData::WorkspacesProject::Application> GetApps(bool isGuidNeeded, bool skipMinimized, const std::function<unsigned int(HWND)> getMonitorNumberFromWindowHandle, const std::function<WorkspacesData::WorkspacesProject::Monitor::MonitorRect(unsigned int)> getMonitorRect);
};
