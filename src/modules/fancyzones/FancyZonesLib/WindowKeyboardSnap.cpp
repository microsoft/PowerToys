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
    if (FancyZonesSettings::settings().moveWindowsBasedOnPosition)
    {
        return SnapHotkeyBasedOnPosition(window, vkCode, monitor, activeWorkAreas);
    }

    return (vkCode == VK_LEFT || vkCode == VK_RIGHT) && SnapHotkeyBasedOnZoneNumber(window, vkCode, monitor, activeWorkAreas, monitors);
}

bool WindowKeyboardSnap::SnapHotkeyBasedOnZoneNumber(HWND window, DWORD vkCode, HMONITOR current, const std::unordered_map<HMONITOR, std::unique_ptr<WorkArea>>& activeWorkAreas, const std::vector<HMONITOR>& monitors)
{
    // clean previous extention data
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

                    Trace::FancyZones::KeyboardSnapWindowToZone(workArea->GetLayout().get(), workArea->GetLayoutWindows());
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
            bool moved = MoveByDirectionAndIndex(window, vkCode, FancyZonesSettings::settings().moveWindowAcrossMonitors /* cycle through zones */, workArea.get());

            if (FancyZonesSettings::settings().restoreSize && !moved)
            {
                FancyZonesWindowUtils::RestoreWindowOrigin(window);
                FancyZonesWindowUtils::RestoreWindowSize(window);
            }

            if (moved && workArea)
            {
                Trace::FancyZones::KeyboardSnapWindowToZone(workArea->GetLayout().get(), workArea->GetLayoutWindows());
            }

            return moved;
        }
    }

    return false;
}

bool WindowKeyboardSnap::SnapHotkeyBasedOnPosition(HWND window, DWORD vkCode, HMONITOR current, const std::unordered_map<HMONITOR, std::unique_ptr<WorkArea>>& activeWorkAreas)
{
    if (!activeWorkAreas.contains(current))
    {
        return false;
    }

    const auto& currentWorkArea = activeWorkAreas.at(current);
    auto allMonitors = FancyZonesUtils::GetAllMonitorRects<&MONITORINFOEX::rcWork>();

    if (current && allMonitors.size() > 1 && FancyZonesSettings::settings().moveWindowAcrossMonitors)
    {
        // Multi monitor environment.
        // First, try to stay on the same monitor
        bool success = ProcessDirectedSnapHotkey(window, vkCode, false, currentWorkArea.get());
        if (success)
        {
            return true;
        }

        // If that didn't work, extract zones from all other monitors and target one of them
        std::vector<RECT> zoneRects;
        std::vector<std::pair<ZoneIndex, WorkArea*>> zoneRectsInfo;
        RECT currentMonitorRect{ .top = 0, .bottom = -1 };

        for (const auto& [monitor, monitorRect] : allMonitors)
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

        // Ensure we can get the windowRect, if not, just quit
        RECT windowRect;
        if (!GetWindowRect(window, &windowRect))
        {
            return false;
        }

        auto chosenIdx = FancyZonesUtils::ChooseNextZoneByPosition(vkCode, windowRect, zoneRects);

        if (chosenIdx < zoneRects.size())
        {
            // Moving to another monitor succeeded
            const auto& [trueZoneIdx, workArea] = zoneRectsInfo[chosenIdx];
            if (workArea)
            {
                workArea->Snap(window, { trueZoneIdx });
                Trace::FancyZones::KeyboardSnapWindowToZone(workArea->GetLayout().get(), workArea->GetLayoutWindows());
            }

            return true;
        }

        // We reached the end of all monitors.
        // Try again, cycling on all monitors.
        // First, add zones from the origin monitor to zoneRects
        // Sanity check: the current monitor is valid
        if (currentMonitorRect.top <= currentMonitorRect.bottom)
        {
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

        RECT combinedRect = FancyZonesUtils::GetAllMonitorsCombinedRect<&MONITORINFOEX::rcWork>();
        windowRect = FancyZonesUtils::PrepareRectForCycling(windowRect, combinedRect, vkCode);
        chosenIdx = FancyZonesUtils::ChooseNextZoneByPosition(vkCode, windowRect, zoneRects);
        if (chosenIdx < zoneRects.size())
        {
            // Moving to another monitor succeeded
            const auto& [trueZoneIdx, workArea] = zoneRectsInfo[chosenIdx];

            if (workArea)
            {
                workArea->Snap(window, { trueZoneIdx });
                Trace::FancyZones::KeyboardSnapWindowToZone(workArea->GetLayout().get(), workArea->GetLayoutWindows());
            }

            return true;
        }
        else
        {
            // Giving up
            return false;
        }
    }
    else
    {
        // Single monitor environment, or combined multi-monitor environment.
        return ProcessDirectedSnapHotkey(window, vkCode, true, currentWorkArea.get());
    }

    return false;
}

bool WindowKeyboardSnap::ProcessDirectedSnapHotkey(HWND window, DWORD vkCode, bool cycle, WorkArea* const workArea)
{
    if (!workArea)
    {
        return false;
    }

    bool result = false;

    // Check whether Alt is used in the shortcut key combination
    if (GetAsyncKeyState(VK_MENU) & 0x8000)
    {
        // continue extention process
        result = Extend(window, vkCode, workArea);
    }
    else
    {
        // clean previous extention data
        m_extendData.Reset();

        result = MoveByDirectionAndPosition(window, vkCode, cycle, workArea);    
    }

    if (result)
    {
        Trace::FancyZones::KeyboardSnapWindowToZone(workArea->GetLayout().get(), workArea->GetLayoutWindows());
    }

    return result;
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

    // The window was not assigned to any zone here
    if (zoneIndexes.size() == 0)
    {
        const ZoneIndex zone = vkCode == VK_LEFT ? numZones - 1 : 0;
        workArea->Snap(window, { zone });
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
            workArea->Snap(window, { zone });
        }
        else
        {
            // We didn't reach the edge
            if (vkCode == VK_LEFT)
            {
                workArea->Snap(window, { oldId - 1 });
            }
            else
            {
                workArea->Snap(window, { oldId + 1 });
            }
        }
    }

    return true;
}

bool WindowKeyboardSnap::MoveByDirectionAndPosition(HWND window, DWORD vkCode, bool cycle, WorkArea* const workArea)
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

    RECT windowRect;
    if (!GetWindowRect(window, &windowRect))
    {
        Logger::error(L"GetWindowRect failed, {}", get_last_error_or_default(GetLastError()));
        return false;
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
        workArea->Snap(window, { freeZoneIndices[result] });
        Trace::FancyZones::KeyboardSnapWindowToZone(layout.get(), layoutWindows);
        return true;
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
            workArea->Snap(window, { result });
            Trace::FancyZones::KeyboardSnapWindowToZone(layout.get(), layoutWindows);
            return true;
        }
    }

    return false;
}

bool WindowKeyboardSnap::Extend(HWND window, DWORD vkCode, WorkArea* const workArea)
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

    RECT windowRect;
    if (!GetWindowRect(window, &windowRect))
    {
        Logger::error(L"GetWindowRect failed, {}", get_last_error_or_default(GetLastError()));
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

    workArea->Snap(window, resultIndexSet);

    return true;
}
