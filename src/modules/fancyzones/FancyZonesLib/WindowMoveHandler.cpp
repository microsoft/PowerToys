#include "pch.h"
#include "WindowMoveHandler.h"

#include <common/display/dpi_aware.h>
#include <common/logger/logger.h>
#include <common/utils/elevation.h>
#include <common/utils/winapi_error.h>

#include "FancyZonesData/AppZoneHistory.h"
#include "Settings.h"
#include "WorkArea.h"
#include <FancyZonesLib/FancyZonesWindowProcessing.h>
#include <FancyZonesLib/NotificationUtil.h>
#include <FancyZonesLib/WindowUtils.h>


WindowMoveHandler::WindowMoveHandler()
{
}

void WindowMoveHandler::MoveWindowIntoZoneByIndexSet(HWND window, const ZoneIndexSet& indexSet, std::shared_ptr<WorkArea> workArea) noexcept
{
    if (workArea)
    {
        workArea->MoveWindowIntoZoneByIndexSet(window, indexSet);
    }
}

bool WindowMoveHandler::MoveWindowIntoZoneByDirectionAndIndex(HWND window, DWORD vkCode, bool cycle, std::shared_ptr<WorkArea> workArea) noexcept
{
    return workArea && workArea->MoveWindowIntoZoneByDirectionAndIndex(window, vkCode, cycle);
}

bool WindowMoveHandler::MoveWindowIntoZoneByDirectionAndPosition(HWND window, DWORD vkCode, bool cycle, std::shared_ptr<WorkArea> workArea) noexcept
{
    return workArea && workArea->MoveWindowIntoZoneByDirectionAndPosition(window, vkCode, cycle);
}

bool WindowMoveHandler::ExtendWindowByDirectionAndPosition(HWND window, DWORD vkCode, std::shared_ptr<WorkArea> workArea) noexcept
{
    return workArea && workArea->ExtendWindowByDirectionAndPosition(window, vkCode);
}

void WindowMoveHandler::AssignWindowsToZones(const std::unordered_map<HMONITOR, std::shared_ptr<WorkArea>>& activeWorkAreas, bool updatePositions) noexcept
{
    for (const auto& window : VirtualDesktop::instance().GetWindowsFromCurrentDesktop())
    {
        auto zoneIndexSet = FancyZonesWindowProperties::RetrieveZoneIndexProperty(window);
        if (zoneIndexSet.size() == 0)
        {
            continue;
        }

        auto monitor = MonitorFromWindow(window, MONITOR_DEFAULTTONULL);
        if (monitor && activeWorkAreas.contains(monitor))
        {
            activeWorkAreas.at(monitor)->MoveWindowIntoZoneByIndexSet(window, zoneIndexSet, updatePositions);
        }
    }
}
