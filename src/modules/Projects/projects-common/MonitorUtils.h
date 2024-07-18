#pragma once

#include <ProjectsLib/ProjectsData.h>
#include <common/Display/DisplayUtils.h>

namespace MonitorUtils
{
    inline std::vector<ProjectsData::Project::Monitor> IdentifyMonitors() noexcept
    {
        auto displaysResult = DisplayUtils::GetDisplays();

        int retryCounter = 0;
        while (!displaysResult.first && retryCounter < 100)
        {
            std::this_thread::sleep_for(std::chrono::milliseconds(30));
            displaysResult = DisplayUtils::GetDisplays();
            retryCounter++;
        }

        std::vector<ProjectsData::Project::Monitor> result{};
        for (const auto& data : displaysResult.second)
        {
            result.emplace_back(ProjectsData::Project::Monitor{
                .monitor = data.monitor,
                .id = data.id,
                .instanceId = data.instanceId,
                .number = data.number,
                .dpi = data.dpi,
                .monitorRectDpiAware = ProjectsData::Project::Monitor::MonitorRect{
                    .top = data.monitorRectDpiAware.top,
                    .left = data.monitorRectDpiAware.left,
                    .width = data.monitorRectDpiAware.right - data.monitorRectDpiAware.left,
                    .height = data.monitorRectDpiAware.bottom - data.monitorRectDpiAware.top,
                },
                .monitorRectDpiUnaware = ProjectsData::Project::Monitor::MonitorRect{
                    .top = data.monitorRectDpiUnaware.top,
                    .left = data.monitorRectDpiUnaware.left,
                    .width = data.monitorRectDpiUnaware.right - data.monitorRectDpiUnaware.left,
                    .height = data.monitorRectDpiUnaware.bottom - data.monitorRectDpiUnaware.top,
                },
            });
        }

        return result;
    }
}