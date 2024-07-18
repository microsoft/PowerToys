#pragma once

#include <ProjectsLib/ProjectsData.h>

namespace SnapshotUtils
{
    std::vector<ProjectsData::Project::Application> GetApps(const std::function<unsigned int(HWND)> getMonitorNumberFromWindowHandle);
};
