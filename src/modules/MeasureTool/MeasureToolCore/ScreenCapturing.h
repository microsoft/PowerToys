#pragma once

#include "ToolState.h"

#include <common/utils/serialized.h>

void StartCapturingThread(const CommonState& commonState,
                          Serialized<MeasureToolState>& state,
                          HWND targetWindow,
                          HMONITOR targetMonitor);