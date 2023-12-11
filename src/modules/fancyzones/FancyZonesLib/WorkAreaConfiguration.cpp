#include "pch.h"
#include "WorkAreaConfiguration.h"

#include <FancyZonesLib/WorkArea.h>

WorkArea* const WorkAreaConfiguration::GetWorkArea(HMONITOR monitor) const
{
    auto iter = m_workAreaMap.find(monitor);
    if (iter != m_workAreaMap.end())
    {
        return iter->second.get();
    }

    return nullptr;
}

WorkArea* const WorkAreaConfiguration::GetWorkAreaFromCursor() const
{
    const auto allMonitorsWorkArea = GetWorkArea(nullptr);
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

        return GetWorkArea(MonitorFromPoint(cursorPoint, MONITOR_DEFAULTTONULL));
    }
}

WorkArea* const WorkAreaConfiguration::GetWorkAreaFromWindow(HWND window) const
{
    const auto allMonitorsWorkArea = GetWorkArea(nullptr);
    if (allMonitorsWorkArea)
    {
        // First, check if there's a work area spanning all monitors (signalled by the NULL monitor handle)
        return allMonitorsWorkArea;
    }
    else
    {
        // Otherwise, look for the work area based on the window's position
        HMONITOR monitor = MonitorFromWindow(window, MONITOR_DEFAULTTONULL);
        return GetWorkArea(monitor);
    }
}

const std::unordered_map<HMONITOR, std::unique_ptr<WorkArea>>& WorkAreaConfiguration::GetAllWorkAreas() const noexcept
{
    return m_workAreaMap;
}

void WorkAreaConfiguration::AddWorkArea(HMONITOR monitor, std::unique_ptr<WorkArea> workArea)
{
    m_workAreaMap.insert({ monitor, std::move(workArea) });
}

void WorkAreaConfiguration::Clear() noexcept
{
    m_workAreaMap.clear();
}
