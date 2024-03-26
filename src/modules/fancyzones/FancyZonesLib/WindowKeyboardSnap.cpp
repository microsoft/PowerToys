#include "pch.h"
#include "WindowKeyboardSnap.h"

#include <FancyZonesLib/Settings.h>
#include <FancyZonesLib/trace.h>
#include <FancyZonesLib/WindowUtils.h>
#include <FancyZonesLib/WorkArea.h>
#include <FancyZonesLib/util.h>

#include <common/logger/logger.h>
#include <common/utils/winapi_error.h>

bool WindowKeyboardSnap::Snap(HWND window, HMONITOR monitor, DWORD vkCode, const std::unordered_map<HMONITOR, std::unique_ptr<WorkArea>>& activeWorkAreas, const std::vector<HMONITOR>& monitors)
{
    return (vkCode == VK_LEFT || vkCode == VK_RIGHT) && SnapHotkeyBasedOnZoneNumber(window, vkCode, monitor, activeWorkAreas, monitors);
}

bool WindowKeyboardSnap::Snap(HWND window, RECT windowRect, HMONITOR monitor, DWORD vkCode, const std::unordered_map<HMONITOR, std::unique_ptr<WorkArea>>& activeWorkAreas, const std::vector<std::pair<HMONITOR, RECT>>& monitors)
{
    if (!activeWorkAreas.contains(monitor))
    {
        return false;
    }

    // clean previous extension data
    m_extendData.Reset();

    const auto& currentWorkArea = activeWorkAreas.at(monitor);
    if (monitors.size() > 1 && FancyZonesSettings::settings().moveWindowAcrossMonitors)
    {
        // Multi monitor environment.
        // First, try to stay on the same monitor
        bool success = MoveByDirectionAndPosition(window, windowRect, vkCode, false, currentWorkArea.get());
        if (success)
        {
            return true;
        }

        // Try to snap on another monitor
        success = SnapBasedOnPositionOnAnotherMonitor(window, windowRect, vkCode, monitor, activeWorkAreas, monitors);
        if (success)
        {
            // Unsnap from previous work area
            currentWorkArea->Unsnap(window);
        }

        return success;
    }
    else
    {
        // Single monitor environment, or combined multi-monitor environment.
        return MoveByDirectionAndPosition(window, windowRect, vkCode, true, currentWorkArea.get());
    }
}

bool WindowKeyboardSnap::Extend(HWND window, RECT windowRect, HMONITOR monitor, DWORD vkCode, const std::unordered_map<HMONITOR, std::unique_ptr<WorkArea>>& activeWorkAreas)
{
    if (!activeWorkAreas.contains(monitor))
    {
        return false;
    }

    // continue extension process
    const auto& workArea = activeWorkAreas.at(monitor);
    return Extend(window, windowRect, vkCode, workArea.get());
}

bool WindowKeyboardSnap::SnapHotkeyBasedOnZoneNumber(HWND window, DWORD vkCode, HMONITOR current, const std::unordered_map<HMONITOR, std::unique_ptr<WorkArea>>& activeWorkAreas, const std::vector<HMONITOR>& monitors)
{
    // clean previous extension data
    m_extendData.Reset();

    if (current && monitors.size() > 1 && FancyZonesSettings::settings().moveWindowAcrossMonitors)
    {
        // Multi monitor environment.
        auto currMonitor = std::find(std::begin(monitors), std::end(monitors), current);
        do
        {
            if (activeWorkAreas.contains(*currMonitor))
            {
                const auto& workArea = activeWorkAreas.at(*currMonitor);
            
                if (MoveByDirectionAndIndex(window, vkCode, false /* cycle through zones */, workArea.get()))
                {
                    // unassign from previous work area
                    for (auto& [_, prevWorkArea] : activeWorkAreas)
                    {
                        if (prevWorkArea && workArea != prevWorkArea)
                        {
                            prevWorkArea->Unsnap(window);
                        }
                    }

                    return true;
                }
                // We iterated through all zones in current monitor zone layout, move on to next one (or previous depending on direction).
                if (vkCode == VK_RIGHT)
                {
                    currMonitor = std::next(currMonitor);
                    if (currMonitor == std::end(monitors))
                    {
                        currMonitor = std::begin(monitors);
                    }
                }
                else if (vkCode == VK_LEFT)
                {
                    if (currMonitor == std::begin(monitors))
                    {
                        currMonitor = std::end(monitors);
                    }
                    currMonitor = std::prev(currMonitor);
                }
            }
        } while (*currMonitor != current);
    }
    else
    {
        if (activeWorkAreas.contains(current))
        {
            const auto& workArea = activeWorkAreas.at(current);
            bool moved = MoveByDirectionAndIndex(window, vkCode, true /* cycle through zones */, workArea.get());

            if (FancyZonesSettings::settings().restoreSize && !moved)
            {
                FancyZonesWindowUtils::RestoreWindowOrigin(window);
                FancyZonesWindowUtils::RestoreWindowSize(window);
            }

            return moved;
        }
    }

    return false;
}

bool WindowKeyboardSnap::SnapBasedOnPositionOnAnotherMonitor(HWND window, RECT windowRect, DWORD vkCode, HMONITOR current, const std::unordered_map<HMONITOR, std::unique_ptr<WorkArea>>& activeWorkAreas, const std::vector<std::pair<HMONITOR, RECT>>& monitors)
{
    // Extract zones from all other monitors and target one of them
    std::vector<RECT> zoneRects;
    std::vector<std::pair<ZoneIndex, WorkArea*>> zoneRectsInfo;
    RECT currentMonitorRect{ .top = 0, .bottom = -1 };

    for (const auto& [monitor, monitorRect] : monitors)
    {
        if (monitor == current)
        {
            currentMonitorRect = monitorRect;
        }
        else
        {
            if (activeWorkAreas.contains(monitor))
            {
                const auto& workArea = activeWorkAreas.at(monitor);
                const auto& layout = workArea->GetLayout();
                if (layout)
                {
                    const auto& zones = layout->Zones();
                    for (const auto& [zoneId, zone] : zones)
                    {
                        RECT zoneRect = zone.GetZoneRect();

                        zoneRect.left += monitorRect.left;
                        zoneRect.right += monitorRect.left;
                        zoneRect.top += monitorRect.top;
                        zoneRect.bottom += monitorRect.top;

                        zoneRects.emplace_back(zoneRect);
                        zoneRectsInfo.emplace_back(zoneId, workArea.get());
                    }
                }
            }
        }
    }

    auto chosenIdx = FancyZonesUtils::ChooseNextZoneByPosition(vkCode, windowRect, zoneRects);

    if (chosenIdx < zoneRects.size())
    {
        // Moving to another monitor succeeded
        const auto& [trueZoneIdx, workArea] = zoneRectsInfo[chosenIdx];
        bool snapped = false;
        if (workArea)
        {
            snapped = workArea->Snap(window, { trueZoneIdx });
        }

        if (snapped)
        {
            Trace::FancyZones::KeyboardSnapWindowToZone(workArea->GetLayout().get(), workArea->GetLayoutWindows());
        }

        return snapped;
    }

    // We reached the end of all monitors.
    // Try again, cycling on all monitors.
    // First, add zones from the origin monitor to zoneRects
    // Sanity check: the current monitor is valid
    if (currentMonitorRect.top <= currentMonitorRect.bottom)
    {
        const auto& currentWorkArea = activeWorkAreas.at(current);
        if (currentWorkArea)
        {
            const auto& layout = currentWorkArea->GetLayout();
            if (layout)
            {
                const auto& zones = layout->Zones();
                for (const auto& [zoneId, zone] : zones)
                {
                    RECT zoneRect = zone.GetZoneRect();

                    zoneRect.left += currentMonitorRect.left;
                    zoneRect.right += currentMonitorRect.left;
                    zoneRect.top += currentMonitorRect.top;
                    zoneRect.bottom += currentMonitorRect.top;

                    zoneRects.emplace_back(zoneRect);
                    zoneRectsInfo.emplace_back(zoneId, currentWorkArea.get());
                }
            }
        }
    }
    else
    {
        return false;
    }

    RECT combinedRect = FancyZonesUtils::GetMonitorsCombinedRect<&MONITORINFOEX::rcWork>(monitors);
    windowRect = FancyZonesUtils::PrepareRectForCycling(windowRect, combinedRect, vkCode);
    chosenIdx = FancyZonesUtils::ChooseNextZoneByPosition(vkCode, windowRect, zoneRects);
    if (chosenIdx < zoneRects.size())
    {
        // Moving to another monitor succeeded
        const auto& [trueZoneIdx, workArea] = zoneRectsInfo[chosenIdx];

        bool snapped = false;
        if (workArea)
        {
            snapped = workArea->Snap(window, { trueZoneIdx });
        }

        if (snapped)
        {
            Trace::FancyZones::KeyboardSnapWindowToZone(workArea->GetLayout().get(), workArea->GetLayoutWindows());
        }

        return snapped;
    }
    else
    {
        // Giving up
        return false;
    }
}

bool WindowKeyboardSnap::MoveByDirectionAndIndex(HWND window, DWORD vkCode, bool cycle, WorkArea* const workArea)
{
    if (!workArea)
    {
        return false;
    }

    const auto& layout = workArea->GetLayout();
    const auto& zones = layout->Zones();
    const auto& layoutWindows = workArea->GetLayoutWindows();
    if (!layout || zones.empty())
    {
        return false;
    }

    auto zoneIndexes = layoutWindows.GetZoneIndexSetFromWindow(window);
    const auto numZones = zones.size();
    bool snapped = false;

    // The window was not assigned to any zone here
    if (zoneIndexes.size() == 0)
    {
        const ZoneIndex zone = vkCode == VK_LEFT ? numZones - 1 : 0;
        snapped = workArea->Snap(window, { zone });
    }
    else
    {
        const ZoneIndex oldId = zoneIndexes[0];

        // We reached the edge
        if ((vkCode == VK_LEFT && oldId == 0) || (vkCode == VK_RIGHT && oldId == static_cast<int64_t>(numZones) - 1))
        {
            if (!cycle)
            {
                return false;
            }

            const ZoneIndex zone = vkCode == VK_LEFT ? numZones - 1 : 0;
            snapped = workArea->Snap(window, { zone });
        }
        else
        {
            // We didn't reach the edge
            if (vkCode == VK_LEFT)
            {
                snapped = workArea->Snap(window, { oldId - 1 });
            }
            else
            {
                snapped = workArea->Snap(window, { oldId + 1 });
            }
        }
    }

    if (snapped)
    {
        Trace::FancyZones::KeyboardSnapWindowToZone(workArea->GetLayout().get(), workArea->GetLayoutWindows());
    }

    return snapped;
}

bool WindowKeyboardSnap::MoveByDirectionAndPosition(HWND window, RECT windowRect, DWORD vkCode, bool cycle, WorkArea* const workArea)
{
    if (!workArea)
    {
        return false;
    }

    const auto& layout = workArea->GetLayout();
    const auto& zones = layout->Zones();
    const auto& layoutWindows = workArea->GetLayoutWindows();
    if (!layout || zones.empty())
    {
        return false;
    }

    std::vector<bool> usedZoneIndices(zones.size(), false);
    auto windowZones = layoutWindows.GetZoneIndexSetFromWindow(window);

    for (const ZoneIndex id : windowZones)
    {
        usedZoneIndices[id] = true;
    }

    std::vector<RECT> zoneRects;
    ZoneIndexSet freeZoneIndices;

    for (const auto& [zoneId, zone] : zones)
    {
        if (!usedZoneIndices[zoneId])
        {
            zoneRects.emplace_back(zones.at(zoneId).GetZoneRect());
            freeZoneIndices.emplace_back(zoneId);
        }
    }

    // Move to coordinates relative to windowZone
    const auto& workAreaRect = workArea->GetWorkAreaRect();
    windowRect.top -= workAreaRect.top();
    windowRect.bottom -= workAreaRect.top();
    windowRect.left -= workAreaRect.left();
    windowRect.right -= workAreaRect.left();

    ZoneIndex result = FancyZonesUtils::ChooseNextZoneByPosition(vkCode, windowRect, zoneRects);
    if (static_cast<size_t>(result) < zoneRects.size())
    {
        bool success = workArea->Snap(window, { freeZoneIndices[result] });
        if (success)
        {
            Trace::FancyZones::KeyboardSnapWindowToZone(layout.get(), layoutWindows); 
        }
        return success;
    }
    else if (cycle)
    {
        // Try again from the position off the screen in the opposite direction to vkCode
        // Consider all zones as available
        zoneRects.resize(zones.size());
        std::transform(zones.begin(), zones.end(), zoneRects.begin(), [](auto zone) { return zone.second.GetZoneRect(); });
        windowRect = FancyZonesUtils::PrepareRectForCycling(windowRect, RECT(workAreaRect.left(), workAreaRect.top(), workAreaRect.right(), workAreaRect.bottom()), vkCode);
        result = FancyZonesUtils::ChooseNextZoneByPosition(vkCode, windowRect, zoneRects);

        if (static_cast<size_t>(result) < zoneRects.size())
        {
            bool success = workArea->Snap(window, { result });

            if (success)
            {
                Trace::FancyZones::KeyboardSnapWindowToZone(layout.get(), layoutWindows);
            }
            
            return success;
        }
    }

    return false;
}

bool WindowKeyboardSnap::Extend(HWND window, RECT windowRect, DWORD vkCode, WorkArea* const workArea)
{
    if (!workArea)
    {
        return false;
    }

    const auto& layout = workArea->GetLayout();
    const auto& layoutWindows = workArea->GetLayoutWindows();
    if (!layout || layout->Zones().empty())
    {
        return false;
    }

    const auto& zones = layout->Zones();
    auto appliedZones = layoutWindows.GetZoneIndexSetFromWindow(window);
    
    std::vector<bool> usedZoneIndices(zones.size(), false);
    std::vector<RECT> zoneRects;
    ZoneIndexSet freeZoneIndices;

    // If selectManyZones = true for the second time, use the last zone into which we moved
    // instead of the window rect and enable moving to all zones except the old one
    if (m_extendData.IsExtended(window))
    {
        usedZoneIndices[m_extendData.windowFinalIndex] = true;
        windowRect = zones.at(m_extendData.windowFinalIndex).GetZoneRect();
    }
    else
    {
        for (const ZoneIndex idx : appliedZones)
        {
            usedZoneIndices[idx] = true;
        }

        // Move to coordinates relative to windowZone
        const auto& workAreaRect = workArea->GetWorkAreaRect();
        windowRect.top -= workAreaRect.top();
        windowRect.bottom -= workAreaRect.top();
        windowRect.left -= workAreaRect.left();
        windowRect.right -= workAreaRect.left();

        m_extendData.Set(window);
    }

    for (size_t i = 0; i < zones.size(); i++)
    {
        if (!usedZoneIndices[i])
        {
            zoneRects.emplace_back(zones.at(i).GetZoneRect());
            freeZoneIndices.emplace_back(i);
        }
    }

    const auto result = FancyZonesUtils::ChooseNextZoneByPosition(vkCode, windowRect, zoneRects);
    if (result >= zoneRects.size())
    {
        return false;
    }

    ZoneIndex targetZone = freeZoneIndices[result];
    ZoneIndexSet resultIndexSet;

    // First time with selectManyZones = true for this window?
    if (m_extendData.windowFinalIndex == -1)
    {
        // Already zoned?
        if (appliedZones.size())
        {
            m_extendData.windowInitialIndexSet = appliedZones;
            m_extendData.windowFinalIndex = targetZone;
            resultIndexSet = layout->GetCombinedZoneRange(appliedZones, { targetZone });
        }
        else
        {
            m_extendData.windowInitialIndexSet = { targetZone };
            m_extendData.windowFinalIndex = targetZone;
            resultIndexSet = { targetZone };
        }
    }
    else
    {
        auto deletethis = m_extendData.windowInitialIndexSet;
        m_extendData.windowFinalIndex = targetZone;
        resultIndexSet = layout->GetCombinedZoneRange(m_extendData.windowInitialIndexSet, { targetZone });
    }

    bool success = workArea->Snap(window, resultIndexSet);
    if (success)
    {
        Trace::FancyZones::KeyboardSnapWindowToZone(workArea->GetLayout().get(), workArea->GetLayoutWindows());
    }

    return success;
}
