#pragma once

interface IZoneWindow;
interface IFancyZonesSettings;
interface IZoneSet;

struct WinHookEvent;

interface __declspec(uuid("{50D3F0F5-736E-4186-BDF4-3D6BEE150C3A}")) IFancyZones : public IUnknown
{
    /**
     * Start and initialize FancyZones.
     */
    IFACEMETHOD_(void, Run)() = 0;
    /**
     * Stop FancyZones and do the clean up.
     */
    IFACEMETHOD_(void, Destroy)() = 0;
};

/**
 * Core FancyZones functionality.
 */
interface __declspec(uuid("{2CB37E8F-87E6-4AEC-B4B2-E0FDC873343F}")) IFancyZonesCallback : public IUnknown
{
    /**
     * Inform FancyZones that user has switched between virtual desktops.
     */
    IFACEMETHOD_(void, VirtualDesktopChanged)() = 0;
    /**
     * Callback from WinEventHook to FancyZones
     *
     * @param   data  Handle of window being moved or resized.
     */
    IFACEMETHOD_(void, HandleWinHookEvent)(const WinHookEvent* data) = 0;
    /**
     * Process keyboard event.
     *
     * @param   info Information about low level keyboard event.
     * @returns Boolean indicating if this event should be passed on further to other applications
     *          in event chain, or should it be suppressed.
     */
    IFACEMETHOD_(bool, OnKeyDown)(PKBDLLHOOKSTRUCT info) = 0;
    /**
     * Toggle FancyZones editor application.
     */
    IFACEMETHOD_(void, ToggleEditor)() = 0;
    /**
     * Callback triggered when user changes FancyZones settings.
     */
    IFACEMETHOD_(void, SettingsChanged)() = 0;
};

/**
 * Helper functions used by each ZoneWindow (representing work area).
 */
interface __declspec(uuid("{5C8D99D6-34B2-4F4A-A8E5-7483F6869775}")) IZoneWindowHost : public IUnknown
{
    /**
     * Assign window to appropriate zone inside new zone layout.
     */
    IFACEMETHOD_(void, MoveWindowsOnActiveZoneSetChange)() = 0;
        /**
     * @returns Basic zone color.
     */
    IFACEMETHOD_(COLORREF, GetZoneColor)() = 0;
        /**
     * @returns Zone border color.
     */
    IFACEMETHOD_(COLORREF, GetZoneBorderColor)() = 0;
    /**
     * @returns Color used to highlight zone while giving zone layout hints.
     */
    IFACEMETHOD_(COLORREF, GetZoneHighlightColor)() = 0;
    /**
     * @returns Integer in range [0, 100] indicating opacity of highlighted zone (while giving zone layout hints).
     */
    IFACEMETHOD_(int, GetZoneHighlightOpacity)() = 0;
    /**
     * @returns Boolean indicating if dragged window should be transparent.
     */
    IFACEMETHOD_(bool, isMakeDraggedWindowTransparentActive) () = 0;
    /**
     * @returns Boolean indicating if move/size operation is currently active.
     */
    IFACEMETHOD_(bool, InMoveSize) () = 0;
};

winrt::com_ptr<IFancyZones> MakeFancyZones(HINSTANCE hinstance, const winrt::com_ptr<IFancyZonesSettings>& settings) noexcept;
