#include "pch.h"
#include "MonitorWorkAreaHandler.h"
#include "VirtualDesktop.h"
#include "WorkArea.h"
#include "util.h"

#include <common/logger/logger.h>

std::shared_ptr<WorkArea> MonitorWorkAreaHandler::GetWorkArea(const GUID& desktopId, HMONITOR monitor)
{
    auto desktopIt = workAreaMap.find(desktopId);
    if (desktopIt != workAreaMap.end())
    {
        auto& perDesktopData = desktopIt->second;
        auto monitorIt = perDesktopData.find(monitor);
        if (monitorIt != std::end(perDesktopData))
        {
            return monitorIt->second;
        }
    }
    return nullptr;
}

std::shared_ptr<WorkArea> MonitorWorkAreaHandler::GetWorkAreaFromCursor(const GUID& desktopId)
{
    auto allMonitorsWorkArea = GetWorkArea(desktopId, NULL);
    if (allMonitorsWorkArea)
    {
        // First, check if there's a work area spanning all monitors (signalled by the NULL monitor handle)
        return allMonitorsWorkArea;
    }
    else
    {
        // Otherwise, look for the work area based on cursor position
        POINT cursorPoint;
        if (!GetCursorPos(&cursorPoint))
        {
            return nullptr;
        }

        return GetWorkArea(desktopId, MonitorFromPoint(cursorPoint, MONITOR_DEFAULTTONULL));
    }
}

std::shared_ptr<WorkArea> MonitorWorkAreaHandler::GetWorkArea(HWND window, const GUID& desktopId)
{
    auto allMonitorsWorkArea = GetWorkArea(desktopId, NULL);
    if (allMonitorsWorkArea)
    {
        // First, check if there's a work area spanning all monitors (signalled by the NULL monitor handle)
        return allMonitorsWorkArea;
    }
    else
    {
        // Otherwise, look for the work area based on the window's position
        HMONITOR monitor = MonitorFromWindow(window, MONITOR_DEFAULTTONULL);
        return GetWorkArea(desktopId, monitor);
    }
}

const std::unordered_map<HMONITOR, std::shared_ptr<WorkArea>>& MonitorWorkAreaHandler::GetWorkAreasByDesktopId(const GUID& desktopId)
{
    if (workAreaMap.contains(desktopId))
    {
        return workAreaMap[desktopId];
    }

    static const std::unordered_map<HMONITOR, std::shared_ptr<WorkArea>> empty{};
    return empty;
}

std::vector<std::shared_ptr<WorkArea>> MonitorWorkAreaHandler::GetAllWorkAreas()
{
    std::vector<std::shared_ptr<WorkArea>> workAreas{};
    for (const auto& [desktopId, perDesktopData] : workAreaMap)
    {
        std::transform(std::begin(perDesktopData),
                       std::end(perDesktopData),
                       std::back_inserter(workAreas),
                       [](const auto& item) { return item.second; });
    }
    return workAreas;
}

void MonitorWorkAreaHandler::AddWorkArea(const GUID& desktopId, HMONITOR monitor, std::shared_ptr<WorkArea>& workArea)
{
    if (!workAreaMap.contains(desktopId))
    {
        workAreaMap[desktopId] = {};

        auto desktopIdStr = FancyZonesUtils::GuidToString(desktopId);
        if (desktopIdStr)
        {
            Logger::info(L"Add work area on the desktop {}", desktopIdStr.value()); 
        }
    }
    auto& perDesktopData = workAreaMap[desktopId];
    perDesktopData[monitor] = std::move(workArea);
}

bool MonitorWorkAreaHandler::IsNewWorkArea(const GUID& desktopId, HMONITOR monitor)
{
    if (workAreaMap.contains(desktopId))
    {
        const auto& perDesktopData = workAreaMap[desktopId];
        if (perDesktopData.contains(monitor))
        {
            return false;
        }
    }
    return true;
}

void MonitorWorkAreaHandler::RegisterUpdates(const std::vector<GUID>& active)
{
    std::unordered_set<GUID> activeVirtualDesktops(std::begin(active), std::end(active));
    for (auto desktopIt = std::begin(workAreaMap); desktopIt != std::end(workAreaMap);)
    {
        auto activeIt = activeVirtualDesktops.find(desktopIt->first);
        if (activeIt == std::end(activeVirtualDesktops))
        {
            // virtual desktop deleted, remove entry from the map
            desktopIt = workAreaMap.erase(desktopIt);
        }
        else
        {
            activeVirtualDesktops.erase(desktopIt->first); // virtual desktop already in map, skip it
            ++desktopIt;
        }
    }
    // register new virtual desktops, if any
    for (const auto& id : activeVirtualDesktops)
    {
        workAreaMap[id] = {};
    }
}

void MonitorWorkAreaHandler::Clear()
{
    workAreaMap.clear();
}
