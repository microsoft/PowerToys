#pragma once

#include <common/LowlevelKeyboardEvent.h>
#include <common/LowlevelMouseEvent.h>
#include "pch.h"
#include <functional>

interface IAltDragSettings;

struct WinHookEvent;

interface IAltDrag : public IUnknown
{
    /**
     * Start and initialize AltDrag.
     */
    IFACEMETHOD_(void, Run)
    () = 0;
    /**
     * Stop AltDrag and do the clean up.
     */
    IFACEMETHOD_(void, Destroy)
    () = 0;
};

/**
 * Core AltDrag functionality.
 */
interface IAltDragCallback : public IUnknown
{
    /**
     * Process keyboard event.
     *
     * @param   info Information about low level keyboard event.
     * @returns Boolean indicating if this event should be passed on further to other applications
     *          in event chain, or should it be suppressed.
     */
    IFACEMETHOD_(bool, OnKeyEvent)
    (LowlevelKeyboardEvent* info) = 0;


    /**
     * Process mouse event.
     *
     * @param   info Information about low level mouse event.
     * @returns Boolean indicating if this event should be passed on further to other applications
     *          in event chain, or should it be suppressed.
     */
    IFACEMETHOD_(bool, OnMouseEvent)
    (LowlevelMouseEvent* info) = 0;

    /**
     * Callback triggered when user changes AltDrag settings.
     */
    IFACEMETHOD_(void, SettingsChanged)
    () = 0;
};


winrt::com_ptr<IAltDrag> MakeAltDrag(HINSTANCE hinstance, const winrt::com_ptr<IAltDragSettings>& settings, std::function<void()> disableCallback) noexcept;
