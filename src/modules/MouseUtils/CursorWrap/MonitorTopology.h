// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#pragma once
#include <windows.h>
#include <vector>
#include <map>

// Monitor information structure
struct MonitorInfo
{
    RECT rect;
    bool isPrimary;
    int monitorId;
};

// Edge type enumeration
enum class EdgeType
{
    Left = 0,
    Right = 1,
    Top = 2,
    Bottom = 3
};

// Wrap mode enumeration (matches Settings UI dropdown)
enum class WrapMode
{
    Both = 0,           // Wrap in both directions
    VerticalOnly = 1,   // Only wrap top/bottom
    HorizontalOnly = 2  // Only wrap left/right
};

// Represents a single edge of a monitor
struct MonitorEdge
{
    HMONITOR monitor;
    EdgeType type;
    int start;      // For vertical edges: Y start; horizontal: X start
    int end;        // For vertical edges: Y end; horizontal: X end
    int position;   // For vertical edges: X coord; horizontal: Y coord
    bool isOuter;   // True if no adjacent monitor touches this edge
};

// Monitor topology helper - manages edge-based monitor layout
struct MonitorTopology
{
    void Initialize(const std::vector<MonitorInfo>& monitors);

    // Check if cursor is on an outer edge of the given monitor
    // wrapMode filters which edges are considered (Both, VerticalOnly, HorizontalOnly)
    bool IsOnOuterEdge(HMONITOR monitor, const POINT& cursorPos, EdgeType& outEdgeType, WrapMode wrapMode) const;

    // Get the wrap destination point for a cursor on an outer edge
    POINT GetWrapDestination(HMONITOR fromMonitor, const POINT& cursorPos, EdgeType edgeType) const;

    // Get monitor at point (helper)
    HMONITOR GetMonitorFromPoint(const POINT& pt) const;

    // Get monitor rectangle (helper)
    bool GetMonitorRect(HMONITOR monitor, RECT& rect) const;

    // Get outer edges collection (for debugging)
    const std::vector<MonitorEdge>& GetOuterEdges() const { return m_outerEdges; }

    // Detect gaps between monitors that should be snapped together
    struct GapInfo {
        int monitor1Index;
        int monitor2Index;
        int horizontalGap;
        int verticalOverlap;
    };
    std::vector<GapInfo> DetectMonitorGaps() const;

private:
    std::vector<MonitorInfo> m_monitors;
    std::vector<MonitorEdge> m_outerEdges;

    // Map from (monitor, edge type) to edge info
    std::map<std::pair<HMONITOR, EdgeType>, MonitorEdge> m_edgeMap;

    // Helper to get consistent HMONITOR from RECT
    HMONITOR GetMonitorFromRect(const RECT& rect) const;
    
    void BuildEdgeMap();
    void IdentifyOuterEdges();

    // Check if two edges are adjacent (within tolerance)
    bool EdgesAreAdjacent(const MonitorEdge& edge1, const MonitorEdge& edge2, int tolerance = 50) const;

    // Find the opposite outer edge for wrapping
    MonitorEdge FindOppositeOuterEdge(EdgeType fromEdge, int relativePosition) const;

    // Calculate relative position along an edge (0.0 to 1.0)
    double GetRelativePosition(const MonitorEdge& edge, int coordinate) const;

    // Convert relative position to absolute coordinate on target edge
    int GetAbsolutePosition(const MonitorEdge& edge, double relativePosition) const;
};
