#pragma once

#include "ToolState.h"

#include <common/utils/serialized.h>

struct D3DState
{
    winrt::com_ptr<ID3D11Device> d3dDevice;
    winrt::com_ptr<IDXGIDevice> dxgiDevice;
    winrt::com_ptr<IInspectable> d3dDeviceInspectable;

    D3DState();
};

std::thread StartCapturingThread(D3DState* d3dState,
                                 const CommonState& commonState,
                                 Serialized<MeasureToolState>& state,
                                 HWND targetWindow,
                                 MonitorInfo targetMonitor);