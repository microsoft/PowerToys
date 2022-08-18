#pragma once
#include "D2DState.h"
#include "ToolState.h"

#include <common/utils/serialized.h>

void DrawMeasureToolTick(const CommonState& commonState,
                         Serialized<MeasureToolState>& toolState,
                         HWND overlayWindow,
                         D2DState& d2dState);