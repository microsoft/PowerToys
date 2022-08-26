#pragma once
#include "D2DState.h"
#include "ToolState.h"

void DrawBoundsToolTick(const CommonState& commonState,
                        const BoundsToolState& toolState,
                        const HWND overlayWindow,
                        const D2DState& d2dState);

LRESULT CALLBACK BoundsToolWndProc(HWND window, UINT message, WPARAM wparam, LPARAM lparam) noexcept;