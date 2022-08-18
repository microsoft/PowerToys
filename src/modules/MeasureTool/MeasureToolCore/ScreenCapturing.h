#pragma once

#include "ToolState.h"

#include <common/utils/serialized.h>

void StartCapturingThread(Serialized<MeasureToolState>& state, HWND targetWindow, HMONITOR targetMonitor);