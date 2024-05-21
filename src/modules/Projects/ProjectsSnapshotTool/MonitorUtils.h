#pragma once

#include <vector>

#include "../projects-common/Data.h"

// FancyZones: MonitorUtils.h
namespace MonitorUtils
{
    namespace Display
    {
        std::pair<bool, std::vector<Project::Monitor>> GetDisplays();
        std::pair<std::wstring, std::wstring> SplitDisplayDeviceId(const std::wstring& str) noexcept;
    }

    std::vector<Project::Monitor> IdentifyMonitors() noexcept;
}