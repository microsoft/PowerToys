#pragma once

#include "GuidUtils.h"
#include <FancyZonesLib/FancyZonesDataTypes.h>

class WorkArea;

class MonitorWorkAreaMap
{
public:
    /**
     * Get work area based on virtual desktop id and monitor handle.
     *
     * @param[in]  monitor   Monitor handle.
     *
     * @returns    Object representing single work area, interface to all actions available on work area
     *             (e.g. moving windows through zone layout specified for that work area).
     */
    WorkArea* const GetWorkArea(HMONITOR monitor) const;

    /**
     * Get work area based on virtual desktop id and the current cursor position.
     *
     * @returns    Object representing single work area, interface to all actions available on work area
     *             (e.g. moving windows through zone layout specified for that work area).
     */
    WorkArea* const GetWorkAreaFromCursor() const;

    /**
     * Get work area on which specified window is located.
     *
     * @param[in]  window Window handle.
     * 
     * @returns    Object representing single work area, interface to all actions available on work area
     *             (e.g. moving windows through zone layout specified for that work area).
     */
    WorkArea* const GetWorkAreaFromWindow(HWND window) const;

    /**
     * @returns    All registered work areas.
     */
    const std::unordered_map<HMONITOR, std::unique_ptr<WorkArea>>& GetAllWorkAreas() const noexcept;

    /**
     * Register new work area.
     *
     * @param[in]  monitor   Monitor handle.
     * @param[in]  workAra   Object representing single work area.
     */
    void AddWorkArea(HMONITOR monitor, std::unique_ptr<WorkArea> workArea);

    FancyZonesDataTypes::WorkAreaId GetParent(HMONITOR monitor) const;

    /**
    * Saving current work area IDs as parents for later use.
    */
    void SaveParentIds();

    /**
     * Clear all persisted work area related data.
     */
    void Clear() noexcept;
    
private:
    std::unordered_map<HMONITOR, std::unique_ptr<WorkArea>> m_workAreaMap;
    std::unordered_map<HMONITOR, FancyZonesDataTypes::WorkAreaId> m_workAreaParents{};
};
