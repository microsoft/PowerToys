#pragma once

#include "DxgiAPI.h"
#include "ToolState.h"

#include <common/utils/serialized.h>

#include <cstdint>
#include <functional>
#include <thread>

std::thread StartCapturingThread(DxgiAPI* dxgiAPI,
                                 const CommonState& commonState,
                                 Serialized<MeasureToolState>& state,
                                 HWND targetWindow,
                                 MonitorInfo targetMonitor);

struct BoundsCaptureThread
{
    std::thread thread;
    std::function<void(uint64_t)> requestFrame;
};

BoundsCaptureThread StartBoundsCapturingThread(DxgiAPI* dxgiAPI,
                                               const CommonState& commonState,
                                               HWND targetWindow,
                                               MonitorInfo targetMonitor);