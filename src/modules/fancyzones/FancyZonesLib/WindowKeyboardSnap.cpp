#include "pch.h"
#include "WindowKeyboardSnap.h"

#include <FancyZonesLib/Settings.h>
#include <FancyZonesLib/trace.h>
#include <FancyZonesLib/WindowUtils.h>
#include <FancyZonesLib/WorkArea.h>
#include <FancyZonesLib/util.h>

bool WindowKeyboardSnap::SnapForegroundWindow(DWORD vkCode, const std::unordered_map<HMONITOR, std::unique_ptr<WorkArea>>& activeWorkAreas)
{
    // We already checked in ShouldProcessSnapHotkey whether the foreground window is a candidate for zoning
    auto window = GetForegroundWindow();

    HMONITOR monitor{nullptr};
    if (!FancyZonesSettings::settings().spanZonesAcrossMonitors)
    {
        monitor = MonitorFromWindow(window, MONITOR_DEFAULTTONULL);
    }

    if (FancyZonesSettings::settings().moveWindowsBasedOnPosition)
    {
        return SnapHotkeyBasedOnPosition(window, vkCode, monitor, activeWorkAreas);
    }

    return (vkCode == VK_LEFT || vkCode == VK_RIGHT) && SnapHotkeyBasedOnZoneNumber(window, vkCode, monitor, activeWorkAreas);
}

bool WindowKeyboardSnap::SnapHotkeyBasedOnZoneNumber(HWND window, DWORD vkCode, HMONITOR current, const std::unordered_map<HMONITOR, std::unique_ptr<WorkArea>>& activeWorkAreas)
{
    std::vector<HMONITOR> monitors = FancyZonesUtils::GetMonitorsOrdered();
    if (current && monitors.size() > 1 && FancyZonesSettings::settings().moveWindowAcrossMonitors)
    {
        // Multi monitor environment.
        auto currMonitor = std::find(std::begin(monitors), std::end(monitors), current);
        do
        {
            if (activeWorkAreas.contains(*currMonitor))
            {
                const auto& workArea = activeWorkAreas.at(*currMonitor);
            
                if (workArea && workArea->MoveWindowIntoZoneByDirectionAndIndex(window, vkCode, false /* cycle through zones */))
                {
                    // unassign from previous work area
                    for (auto& [_, prevWorkArea] : activeWorkAreas)
                    {
                        if (prevWorkArea && workArea != prevWorkArea)
                        {
                            prevWorkArea->Unsnap(window);
                        }
                    }

                    Trace::FancyZones::KeyboardSnapWindowToZone(workArea->GetLayout().get(), workArea->GetLayoutWindows().get());
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
            // Single monitor environment, or combined multi-monitor environment.
            if (FancyZonesSettings::settings().restoreSize)
            {
                bool moved = workArea && workArea->MoveWindowIntoZoneByDirectionAndIndex(window, vkCode, false /* cycle through zones */);
                if (!moved)
                {
                    FancyZonesWindowUtils::RestoreWindowOrigin(window);
                    FancyZonesWindowUtils::RestoreWindowSize(window);
                }
                else if (workArea)
                {
                    Trace::FancyZones::KeyboardSnapWindowToZone(workArea->GetLayout().get(), workArea->GetLayoutWindows().get());
                }
                return moved;
            }
            else
            {
                bool moved = workArea && workArea->MoveWindowIntoZoneByDirectionAndIndex(window, vkCode, true /* cycle through zones */);

                if (moved)
                {
                    Trace::FancyZones::KeyboardSnapWindowToZone(workArea->GetLayout().get(), workArea->GetLayoutWindows().get());
                }

                return moved;
            }
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
                workArea->MoveWindowIntoZoneByIndexSet(window, { trueZoneIdx });
                Trace::FancyZones::KeyboardSnapWindowToZone(workArea->GetLayout().get(), workArea->GetLayoutWindows().get());
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
                workArea->MoveWindowIntoZoneByIndexSet(window, { trueZoneIdx });
                Trace::FancyZones::KeyboardSnapWindowToZone(workArea->GetLayout().get(), workArea->GetLayoutWindows().get());
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
    // Check whether Alt is used in the shortcut key combination
    if (GetAsyncKeyState(VK_MENU) & 0x8000)
    {
        bool result = workArea && workArea->ExtendWindowByDirectionAndPosition(window, vkCode);
        if (result)
        {
            Trace::FancyZones::KeyboardSnapWindowToZone(workArea->GetLayout().get(), workArea->GetLayoutWindows().get());
        }
        return result;
    }
    else
    {
        bool result = workArea && workArea->MoveWindowIntoZoneByDirectionAndPosition(window, vkCode, cycle);
        if (result)
        {
            Trace::FancyZones::KeyboardSnapWindowToZone(workArea->GetLayout().get(), workArea->GetLayoutWindows().get());
        }
        return result;
    }
}
