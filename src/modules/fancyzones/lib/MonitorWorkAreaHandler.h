#pragma once

interface IZoneWindow;

namespace std
{
    template<>
    struct hash<GUID>
    {
        size_t operator()(const GUID& Value) const
        {
            RPC_STATUS status = RPC_S_OK;
            return ::UuidHash(&const_cast<GUID&>(Value), &status);
        }
    };
}

class MonitorWorkAreaHandler
{
public:
    /**
     * Get work area based on virtual desktop id and monitor handle.
     *
     * @param[in]  desktopId Virtual desktop identifier.
     * @param[in]  monitor   Monitor handle.
     *
     * @returns    Object representing single work area, interface to all actions available on work area
     *             (e.g. moving windows through zone layout specified for that work area).
     */
    winrt::com_ptr<IZoneWindow> GetWorkArea(const GUID& desktopId, HMONITOR monitor);

    /**
     * Get work area on which specified window is located.
     *
     * @param[in]  window Window handle.
     *
     * @returns    Object representing single work area, interface to all actions available on work area
     *             (e.g. moving windows through zone layout specified for that work area).
     */
    winrt::com_ptr<IZoneWindow> GetWorkArea(HWND window);

    /**
     * Get map of all work areas on single virtual desktop. Key in the map is monitor handle, while value
     * represents single work area.
     *
     * @param[in]  desktopId Virtual desktop identifier.
     *
     * @returns    Map containing pairs of monitor and work area for that monitor (within same virtual desktop).
     */
    const std::unordered_map<HMONITOR, winrt::com_ptr<IZoneWindow>>& GetWorkAreasByDesktopId(const GUID& desktopId);

    /**
     * @returns    All registered work areas.
     */
    std::vector<winrt::com_ptr<IZoneWindow>> GetAllWorkAreas();

    /**
     * Register new work area.
     *
     * @param[in]  desktopId Virtual desktop identifier.
     * @param[in]  monitor   Monitor handle.
     * @param[in]  workAra   Object representing single work area.
     */
    void AddWorkArea(const GUID& desktopId, HMONITOR monitor, winrt::com_ptr<IZoneWindow>& workArea);

    /**
     * Check if work area is already registered.
     *
     * @param[in]  desktopId Virtual desktop identifier.
     * @param[in]  monitor   Monitor handle.
     *
     * @returns    Boolean indicating whether work area defined by virtual desktop id and monitor is already registered.
     */
    bool IsNewWorkArea(const GUID& desktopId, HMONITOR monitor);

    /**
     * Register changes in current virtual desktop layout.
     *
     * @param[in]  active  Array of currently active virtual desktop identifiers.
     */
    void RegisterUpdates(const std::vector<GUID>& active);

    /**
     * Clear all persisted work area related data.
     */
    void Clear();

private:
    // Work area is uniquely defined by monitor and virtual desktop id.
    std::unordered_map<GUID, std::unordered_map<HMONITOR, winrt::com_ptr<IZoneWindow>>> workAreaMap;
};
