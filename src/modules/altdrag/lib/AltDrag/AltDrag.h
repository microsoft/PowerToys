#pragma once

#include <common/LowlevelKeyboardEvent.h>
#include <common/LowlevelMouseEvent.h>
#include "pch.h"
#include <functional>

interface __declspec(uuid("{50D3F0F5-736E-4186-BDF4-3D6B13150C3A}"))  IAltDragSettings;

struct WinHookEvent;

interface __declspec(uuid("{50D3F0F5-736E-4186-BDF4-3D6B23150C3A}")) IAltDrag : public IUnknown
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
interface __declspec(uuid("{50D3F0F5-736E-4186-BDF4-3D6B32150C3A}"))  IAltDragCallback : public IUnknown
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
