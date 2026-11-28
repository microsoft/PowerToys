#include "pch.h"
#include "MouseButtonsHook.h"
#include <common/debug_control.h>

#include <cstdlib>

#pragma region public

HHOOK MouseButtonsHook::hHook = {};
std::function<void()> MouseButtonsHook::secondaryClickCallback = {};
std::function<void()> MouseButtonsHook::middleClickCallback = {};
std::function<bool(bool)> MouseButtonsHook::wheelCallback = {};

namespace
{
    // High-resolution wheels and touchpads report a notch (WHEEL_DELTA) as several smaller
    // packets, so partial deltas are accumulated until a whole notch has been rolled.
    int verticalWheelDeltaAccumulator = 0;
    int horizontalWheelDeltaAccumulator = 0;
}

MouseButtonsHook::MouseButtonsHook(std::function<void()> extRightClickCallback, std::function<void()> extMiddleClickCallback, std::function<bool(bool)> extWheelCallback)
{
    secondaryClickCallback = std::move(extRightClickCallback);
    middleClickCallback = std::move(extMiddleClickCallback);
    wheelCallback = std::move(extWheelCallback);
}

void MouseButtonsHook::enable()
{
#if defined(DISABLE_LOWLEVEL_HOOKS_WHEN_DEBUGGED)
    if (IsDebuggerPresent())
    {
        return;
    }
#endif
    if (!hHook)
    {
        hHook = SetWindowsHookEx(WH_MOUSE_LL, MouseButtonsProc, GetModuleHandle(NULL), 0);
    }
}

void MouseButtonsHook::disable()
{
    // Don't let a partial notch from this drag leak into the next one
    verticalWheelDeltaAccumulator = 0;
    horizontalWheelDeltaAccumulator = 0;

    if (hHook)
    {
        UnhookWindowsHookEx(hHook);
        hHook = NULL;
    }
}

#pragma endregion

#pragma region private

LRESULT CALLBACK MouseButtonsHook::MouseButtonsProc(int nCode, WPARAM wParam, LPARAM lParam)
{
    if (nCode == HC_ACTION)
    {
        if (wParam == WM_RBUTTONDOWN || wParam == WM_XBUTTONDOWN)
        {
            secondaryClickCallback();
        }
        else if (wParam == WM_MBUTTONDOWN)
        {
            middleClickCallback();
        }
        else if (wParam == WM_MOUSEWHEEL || wParam == WM_MOUSEHWHEEL)
        {
            const bool horizontal = wParam == WM_MOUSEHWHEEL;
            int& wheelDeltaAccumulator = horizontal ? horizontalWheelDeltaAccumulator : verticalWheelDeltaAccumulator;
            const int delta = GET_WHEEL_DELTA_WPARAM(reinterpret_cast<MSLLHOOKSTRUCT*>(lParam)->mouseData);
            if (delta != 0 && wheelDeltaAccumulator != 0 && (delta < 0) != (wheelDeltaAccumulator < 0))
            {
                // Direction reversed: the partial notch rolled the other way is not going to complete
                wheelDeltaAccumulator = 0;
            }
            wheelDeltaAccumulator += delta;

            bool handled = false;
            while (std::abs(wheelDeltaAccumulator) >= WHEEL_DELTA)
            {
                const bool positive = wheelDeltaAccumulator > 0;
                // Up/left selects the previous layout, down/right selects the next layout.
                const bool previous = horizontal ? !positive : positive;
                if (!wheelCallback(previous))
                {
                    // Nothing was switched, drop the remainder so it can't trigger a stray switch later
                    wheelDeltaAccumulator = 0;
                    break;
                }

                handled = true;
                wheelDeltaAccumulator -= positive ? WHEEL_DELTA : -WHEEL_DELTA;
            }

            if (handled)
            {
                // The wheel event was used to switch layouts, don't let it scroll the underlying window
                return 1;
            }
        }
    }
    return CallNextHookEx(hHook, nCode, wParam, lParam);
}

#pragma endregion
