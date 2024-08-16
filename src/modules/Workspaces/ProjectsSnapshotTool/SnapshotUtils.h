#pragma once

#include <ProjectsLib/ProjectsData.h>

namespace SnapshotUtils
{
    std::vector<WorkspacesData::WorkspacesProject::Application> GetApps(const std::function<unsigned int(HWND)> getMonitorNumberFromWindowHandle);
};
