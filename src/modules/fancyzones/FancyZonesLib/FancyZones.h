#pragma once

#include <common/hooks/WinHookEvent.h>

#include <functional>

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

winrt::com_ptr<IFancyZones> MakeFancyZones(HINSTANCE hinstance, std::function<void()> disableCallback) noexcept;
