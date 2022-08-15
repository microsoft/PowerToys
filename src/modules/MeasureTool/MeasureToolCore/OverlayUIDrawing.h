#pragma once

#include "MeasureToolState.h"

HWND LaunchOverlayUI(MeasureToolState& measureToolState,
                     HMONITOR monitor,
                     std::function<void()> onCompleted);
HWND LaunchOverlayUI(BoundsToolState& boundsToolState,
                     HMONITOR monitor,
                     std::function<void()> onCompleted);