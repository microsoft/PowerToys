#pragma once

#include "DxgiAPI.h"
#include "ToolState.h"

#include <common/utils/serialized.h>

std::thread StartCapturingThread(DxgiAPI* dxgiAPI,
                                 const CommonState& commonState,
                                 Serialized<MeasureToolState>& state,
                                 HWND targetWindow,
                                 MonitorInfo targetMonitor);