#pragma once

#include <projects-common/Data.h>

namespace SnapshotUtils
{
    std::vector<Project::Application> GetApps(const std::function<unsigned int(HWND)> getMonitorNumberFromWindowHandle);
};
