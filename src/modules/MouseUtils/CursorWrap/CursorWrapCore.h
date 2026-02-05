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
    
    // Reset sticky edge state (call when settings change or wrap is disabled)
    void ResetStickyEdgeState();
    
    // Handle mouse move with wrap mode filtering and sticky edge support
    // wrapMode: 0=Both, 1=VerticalOnly, 2=HorizontalOnly
    // stickyEdgeEnabled: if true, cursor must stay at edge for stickyDelayMs before wrapping
    // stickyEdgeDelayMs: delay in milliseconds before wrap occurs when sticky edge is enabled
    POINT HandleMouseMove(const POINT& currentPos, bool disableWrapDuringDrag, int wrapMode, 
                          bool stickyEdgeEnabled, int stickyEdgeDelayMs);

    const std::vector<MonitorInfo>& GetMonitors() const { return m_monitors; }
    const MonitorTopology& GetTopology() const { return m_topology; }

private:
#ifdef _DEBUG
    std::wstring GenerateTopologyJSON() const;
#endif

    std::vector<MonitorInfo> m_monitors;
    MonitorTopology m_topology;
    
    // Sticky edge state
    bool m_stickyEdgeActive = false;         // True when cursor is at a wrappable edge waiting to wrap
    ULONGLONG m_stickyEdgeStartTime = 0;     // Tick count when cursor first hit the edge
    POINT m_stickyEdgePosition = {0, 0};     // Position when sticky edge was triggered
    EdgeType m_stickyEdgeType = EdgeType::Left; // Which edge type triggered sticky state
    HMONITOR m_stickyEdgeMonitor = nullptr;  // Monitor where sticky edge was triggered
};
