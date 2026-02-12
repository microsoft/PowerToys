// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#pragma once
#include <windows.h>
#include <vector>
#include <string>
#include "MonitorTopology.h"

// Core cursor wrapping engine
class CursorWrapCore
{
public:
    CursorWrapCore();

    void UpdateMonitorInfo();
    
    // Handle mouse move with wrap mode filtering
    // wrapMode: 0=Both, 1=VerticalOnly, 2=HorizontalOnly
    // disableOnSingleMonitor: if true, cursor wrapping is disabled when only one monitor is connected
    POINT HandleMouseMove(const POINT& currentPos, bool disableWrapDuringDrag, int wrapMode, bool disableOnSingleMonitor);

    const std::vector<MonitorInfo>& GetMonitors() const { return m_monitors; }
    size_t GetMonitorCount() const { return m_monitors.size(); }
    const MonitorTopology& GetTopology() const { return m_topology; }

private:
#ifdef _DEBUG
    std::wstring GenerateTopologyJSON() const;
#endif

    std::vector<MonitorInfo> m_monitors;
    MonitorTopology m_topology;
};
