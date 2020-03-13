#pragma once
#include "FancyZones.h"
#include "lib/ZoneSet.h"

namespace ZoneWindowUtils
{
    const std::wstring& GetActiveZoneSetTmpPath();
    const std::wstring& GetAppliedZoneSetTmpPath();
    const std::wstring& GetCustomZoneSetsTmpPath();
    std::wstring GenerateUniqueId(HMONITOR monitor, PCWSTR deviceId, PCWSTR virtualDesktopId);
}

/**
 * Class representing single work area, which is defined by monitor and virtual desktop.
 */
interface __declspec(uuid("{7F017528-8110-4FB3-BE41-F472969C2560}")) IZoneWindow : public IUnknown
{
    /**
     * A window is being moved or resized. Track down window position and give zone layout
     * hints if dragging functionality is enabled.
     *
     * @param   window      Handle of window being moved or resized.
     * @param   dragEnabled Boolean indicating is giving hints about active zone layout enabled.
     *                      Hints are given while dragging window while holding SHIFT key.
     */
    IFACEMETHOD(MoveSizeEnter)(HWND window, bool dragEnabled) = 0;
    /**
     * A window has changed location, shape, or size. Track down window position and give zone layout
     * hints if dragging functionality is enabled.
     *
     * @param   ptScreen    Cursor coordinates.
     * @param   dragEnabled Boolean indicating is giving hints about active zone layout enabled.
     *                      Hints are given while dragging window while holding SHIFT key.
     */
    IFACEMETHOD(MoveSizeUpdate)(POINT const& ptScreen, bool dragEnabled) = 0;
    /**
     * The movement or resizing of a window has finished. Assign window to the zone of it
     * is dropped within zone borders.
     *
     * @param window   Handle of window being moved or resized.
     * @param ptScreen Cursor coordinates where window is droped.
     */
    IFACEMETHOD(MoveSizeEnd)(HWND window, POINT const& ptScreen) = 0;
    /**
     * @returns Boolean indicating is giving hints about active zone layout enabled. Hints are
     *          given while dragging window while holding SHIFT key.
     */
    IFACEMETHOD_(bool, IsDragEnabled)() = 0;
    /**
     * Assign window to the zone based on zone index inside zone layout.
     *
     * @param   window Handle of window which should be assigned to zone.
     * @param   index  Zone index within zone layout.
     */
    IFACEMETHOD_(void, MoveWindowIntoZoneByIndex)(HWND window, int index) = 0;
    /**
     * Assign window to the zone based on direction (using WIN + LEFT/RIGHT arrow).
     *
     * @param   window Handle of window which should be assigned to zone.
     * @param   vkCode Pressed arrow key.
     */
    IFACEMETHOD_(void, MoveWindowIntoZoneByDirection)(HWND window, DWORD vkCode) = 0;
    /**
     * Cycle through active zone layouts (giving hints about each layout).
     *
     * @param   vkCode Pressed key representing layout index.
     */
    IFACEMETHOD_(void, CycleActiveZoneSet)(DWORD vkCode) = 0;
    /**
     * Save information about zone in which window was assigned, when closing the window.
     * Used once we open same window again to assign it to its previous zone.
     *
     * @param   window Window handle.
     */
    IFACEMETHOD_(void, SaveWindowProcessToZoneIndex)(HWND window) = 0;
    /**
     * @returns Unique work area identifier. Format: <device-id>_<resolution>_<virtual-desktop-id>
     */
    IFACEMETHOD_(std::wstring, UniqueId)() = 0;
    /**
     * @returns Work area resolution (not same as monitor resolution).
     */
    IFACEMETHOD_(std::wstring, WorkAreaKey)() = 0;
    /**
     * @returns Active zone layout for this work area.
     */
    IFACEMETHOD_(IZoneSet*, ActiveZoneSet)() = 0;
    IFACEMETHOD_(void, ShowZoneWindow)() = 0;
    IFACEMETHOD_(void, HideZoneWindow)() = 0;
};

winrt::com_ptr<IZoneWindow> MakeZoneWindow(IZoneWindowHost* host, HINSTANCE hinstance, HMONITOR monitor,
    const std::wstring& uniqueId, bool flashZones) noexcept;
