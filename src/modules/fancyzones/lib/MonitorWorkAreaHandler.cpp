#include "pch.h"
#include "MonitorWorkAreaHandler.h"
#include "VirtualDesktopUtils.h"

winrt::com_ptr<IZoneWindow> MonitorWorkAreaHandler::GetWorkArea(const GUID& desktopId, HMONITOR monitor)
{
    auto desktopIt = workAreaMap.find(desktopId);
    if (desktopIt != std::end(workAreaMap))
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

winrt::com_ptr<IZoneWindow> MonitorWorkAreaHandler::GetWorkArea(HWND window)
{
    HMONITOR monitor = MonitorFromWindow(window, MONITOR_DEFAULTTONULL);
    GUID desktopId{};
    if (monitor && VirtualDesktopUtils::GetWindowDesktopId(window, &desktopId))
    {
        return GetWorkArea(desktopId, monitor);
    }
    return nullptr;
}

const std::unordered_map<HMONITOR, winrt::com_ptr<IZoneWindow>>& MonitorWorkAreaHandler::GetWorkAreasByDesktopId(const GUID& desktopId)
{
    if (workAreaMap.contains(desktopId))
    {
        return workAreaMap[desktopId];
    }
    return {};
}

std::vector<winrt::com_ptr<IZoneWindow>> MonitorWorkAreaHandler::GetAllWorkAreas()
{
    std::vector<winrt::com_ptr<IZoneWindow>> workAreas{};
    for (const auto& [desktopId, perDesktopData] : workAreaMap)
    {
        std::transform(std::begin(perDesktopData),
                       std::end(perDesktopData),
                       std::back_inserter(workAreas),
                       [](const auto& item) { return item.second; });
    }
    return workAreas;
}

void MonitorWorkAreaHandler::AddWorkArea(const GUID& desktopId, HMONITOR monitor, winrt::com_ptr<IZoneWindow>& workArea)
{
    if (!workAreaMap.contains(desktopId))
    {
        workAreaMap[desktopId] = {};
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
