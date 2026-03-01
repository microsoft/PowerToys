// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#pragma once
#include <windows.h>
#include <vector>
#include <string>
#include "MonitorTopology.h"

// Distance threshold to prevent rapid back-and-forth wrapping (in pixels)
constexpr int WRAP_DISTANCE_THRESHOLD = 50;

// Cursor movement direction
struct CursorDirection
{
    int dx; // Horizontal movement (positive = right, negative = left)
    int dy; // Vertical movement (positive = down, negative = up)
    
    bool IsMovingLeft() const { return dx < 0; }
    bool IsMovingRight() const { return dx > 0; }
    bool IsMovingUp() const { return dy < 0; }
    bool IsMovingDown() const { return dy > 0; }
    
    // Returns true if horizontal movement is dominant
    bool IsPrimarilyHorizontal() const { return abs(dx) >= abs(dy); }
};

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

    // Reset wrap state (call when disabling/re-enabling)
    void ResetWrapState();

private:
#ifdef _DEBUG
    std::wstring GenerateTopologyJSON() const;
#endif

    // Calculate movement direction from previous position
    CursorDirection CalculateDirection(const POINT& currentPos) const;
    
    // Check if cursor is within threshold distance of last wrap position
    bool IsWithinWrapThreshold(const POINT& currentPos) const;

    std::vector<MonitorInfo> m_monitors;
    MonitorTopology m_topology;
    
    // Movement tracking for direction-based edge priority
    POINT m_previousPosition = { LONG_MIN, LONG_MIN };
    bool m_hasPreviousPosition = false;
    
    // Wrap stability: prevent rapid oscillation
    POINT m_lastWrapDestination = { LONG_MIN, LONG_MIN };
    bool m_hasLastWrapDestination = false;
};
