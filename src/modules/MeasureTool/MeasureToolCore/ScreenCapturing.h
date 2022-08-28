#pragma once

#include "ToolState.h"

#include <common/utils/serialized.h>

std::thread StartCapturingThread(const CommonState& commonState,
                                 Serialized<MeasureToolState>& state,
                                 HWND targetWindow,
                                 MonitorInfo targetMonitor);