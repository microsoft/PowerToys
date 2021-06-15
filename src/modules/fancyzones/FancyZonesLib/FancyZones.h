#pragma once

#include <common/hooks/WinHookEvent.h>
#include "Settings.h"

#include <functional>

interface IZoneWindow;
interface IFancyZonesSettings;
interface IZoneSet;

struct WinHookEvent;

interface __declspec(uuid("{50D3F0F5-736E-4186-BDF4-3D6BEE150C3A}")) IFancyZones : public IUnknown
{
    /**
     * Start and initialize FancyZones.
     */
    IFACEMETHOD_(void, Run)
    () = 0;
    /**
     * Stop FancyZones and do the clean up.
     */
    IFACEMETHOD_(void, Destroy)
    () = 0;
};

/**
 * Core FancyZones functionality.
 */
interface __declspec(uuid("{2CB37E8F-87E6-4AEC-B4B2-E0FDC873343F}")) IFancyZonesCallback : public IUnknown
{
    /**
     * Inform FancyZones that user has switched between virtual desktops.
     */
    IFACEMETHOD_(void, VirtualDesktopChanged)
    () = 0;
    /**
     * Callback from WinEventHook to FancyZones
     *
     * @param   data  Handle of window being moved or resized.
     */
    IFACEMETHOD_(void, HandleWinHookEvent)
    (const WinHookEvent* data) = 0;
    /**
     * Process keyboard event.
     *
     * @param   info Information about low level keyboard event.
     * @returns Boolean indicating if this event should be passed on further to other applications
     *          in event chain, or should it be suppressed.
     */
    IFACEMETHOD_(bool, OnKeyDown)
    (PKBDLLHOOKSTRUCT info) = 0;
};

/**
 * Helper functions used by each ZoneWindow (representing work area).
 */
interface __declspec(uuid("{5C8D99D6-34B2-4F4A-A8E5-7483F6869775}")) IZoneWindowHost : public IUnknown
{
    /**
     * @returns Boolean indicating if dragged window should be transparent.
     */
    IFACEMETHOD_(bool, isMakeDraggedWindowTransparentActive)
    () = 0;
    /**
     * @returns Boolean indicating if move/size operation is currently active.
     */
    IFACEMETHOD_(bool, InMoveSize)
    () = 0;
    /**
     * @returns Enumeration value indicating the algorithm used to choose one of multiple overlapped zones to highlight.
     */
    IFACEMETHOD_(Settings::OverlappingZonesAlgorithm, GetOverlappingZonesAlgorithm)
    () = 0;
};

winrt::com_ptr<IFancyZones> MakeFancyZones(HINSTANCE hinstance, const winrt::com_ptr<IFancyZonesSettings>& settings, std::function<void()> disableCallback) noexcept;
