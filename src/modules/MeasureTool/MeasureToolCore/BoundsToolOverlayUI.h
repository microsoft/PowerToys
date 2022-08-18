#pragma once
#include "D2DState.h"
#include "ToolState.h"

void DrawBoundsToolTick(const CommonState& commonState,
                        const BoundsToolState& toolState,
                        HWND overlayWindow,
                        const D2DState& d2dState);
