// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#include "pch.h"
#include "MonitorTopology.h"
#include "../../../common/logger/logger.h"
#include <algorithm>
#include <cmath>

void MonitorTopology::Initialize(const std::vector<MonitorInfo>& monitors)
{
#ifdef _DEBUG
    Logger::info(L"======= TOPOLOGY INITIALIZATION START =======");
    Logger::info(L"Initializing edge-based topology for {} monitors", monitors.size());
#endif

    m_monitors = monitors;
    m_outerEdges.clear();
    m_edgeMap.clear();

    if (monitors.empty())
    {
        return;
    }

    BuildEdgeMap();
    IdentifyOuterEdges();

#ifdef _DEBUG
    Logger::info(L"Found {} outer edges", m_outerEdges.size());
    for (const auto& edge : m_outerEdges)
    {
        const wchar_t* typeStr = L"Unknown";
        switch (edge.type)
        {
        case EdgeType::Left: typeStr = L"Left"; break;
        case EdgeType::Right: typeStr = L"Right"; break;
        case EdgeType::Top: typeStr = L"Top"; break;
        case EdgeType::Bottom: typeStr = L"Bottom"; break;
        }
        Logger::info(L"Outer edge: {} at position {}, range [{}, {}]",
            typeStr, edge.position, edge.start, edge.end);
    }
    Logger::info(L"======= TOPOLOGY INITIALIZATION COMPLETE =======");
#endif
}

void MonitorTopology::BuildEdgeMap()
{
    // Create edges for each monitor
    for (const auto& monitor : m_monitors)
    {
        HMONITOR hMonitor = MonitorFromRect(&monitor.rect, MONITOR_DEFAULTTONEAREST);

        // Left edge
        MonitorEdge leftEdge;
        leftEdge.monitor = hMonitor;
        leftEdge.type = EdgeType::Left;
        leftEdge.position = monitor.rect.left;
        leftEdge.start = monitor.rect.top;
        leftEdge.end = monitor.rect.bottom;
        leftEdge.isOuter = true; // Will be updated in IdentifyOuterEdges
        m_edgeMap[{hMonitor, EdgeType::Left}] = leftEdge;
      
        // Right edge
        MonitorEdge rightEdge;
        rightEdge.monitor = hMonitor;
        rightEdge.type = EdgeType::Right;
        rightEdge.position = monitor.rect.right - 1;
        rightEdge.start = monitor.rect.top;
        rightEdge.end = monitor.rect.bottom;
        rightEdge.isOuter = true;
        m_edgeMap[{hMonitor, EdgeType::Right}] = rightEdge;

        // Top edge
        MonitorEdge topEdge;
        topEdge.monitor = hMonitor;
        topEdge.type = EdgeType::Top;
        topEdge.position = monitor.rect.top;
        topEdge.start = monitor.rect.left;
        topEdge.end = monitor.rect.right;
        topEdge.isOuter = true;
        m_edgeMap[{hMonitor, EdgeType::Top}] = topEdge;

        // Bottom edge
        MonitorEdge bottomEdge;
        bottomEdge.monitor = hMonitor;
        bottomEdge.type = EdgeType::Bottom;
        bottomEdge.position = monitor.rect.bottom - 1;
        bottomEdge.start = monitor.rect.left;
        bottomEdge.end = monitor.rect.right;
        bottomEdge.isOuter = true;
        m_edgeMap[{hMonitor, EdgeType::Bottom}] = bottomEdge;
    }
}

void MonitorTopology::IdentifyOuterEdges()
{
    const int tolerance = 50;
    
    // Check each edge against all other edges to find adjacent ones
    for (auto& [key1, edge1] : m_edgeMap)
    {
        for (const auto& [key2, edge2] : m_edgeMap)
        {
            if (edge1.monitor == edge2.monitor)
            {
                continue; // Same monitor
            }

            // Check if edges are adjacent
            if (EdgesAreAdjacent(edge1, edge2, tolerance))
            {
                edge1.isOuter = false;
                break; // This edge has an adjacent monitor
            }
        }

        if (edge1.isOuter)
        {
            m_outerEdges.push_back(edge1);
        }
    }
}

bool MonitorTopology::EdgesAreAdjacent(const MonitorEdge& edge1, const MonitorEdge& edge2, int tolerance) const
{
    // Edges must be opposite types to be adjacent
    bool oppositeTypes = false;

    if ((edge1.type == EdgeType::Left && edge2.type == EdgeType::Right) ||
        (edge1.type == EdgeType::Right && edge2.type == EdgeType::Left) ||
        (edge1.type == EdgeType::Top && edge2.type == EdgeType::Bottom) ||
        (edge1.type == EdgeType::Bottom && edge2.type == EdgeType::Top))
    {
        oppositeTypes = true;
    }

    if (!oppositeTypes)
    {
        return false;
    }

    // Check if positions are within tolerance
    if (abs(edge1.position - edge2.position) > tolerance)
    {
        return false;
    }

    // Check if perpendicular ranges overlap
    int overlapStart = max(edge1.start, edge2.start);
    int overlapEnd = min(edge1.end, edge2.end);

    return overlapEnd > overlapStart + tolerance;
}

bool MonitorTopology::IsOnOuterEdge(HMONITOR monitor, const POINT& cursorPos, EdgeType& outEdgeType, WrapMode wrapMode) const
{
    RECT monitorRect;
    if (!GetMonitorRect(monitor, monitorRect))
    {
        return false;
    }

    // Check each edge type
    const int edgeThreshold = 1;

    // At corners, multiple edges may match - collect all candidates and try each
    // to find one with a valid wrap destination
    std::vector<EdgeType> candidateEdges;

    // Left edge - only if mode allows horizontal wrapping
    if ((wrapMode == WrapMode::Both || wrapMode == WrapMode::HorizontalOnly) &&
        cursorPos.x <= monitorRect.left + edgeThreshold)
    {
        auto it = m_edgeMap.find({monitor, EdgeType::Left});
        if (it != m_edgeMap.end() && it->second.isOuter)
        {
            candidateEdges.push_back(EdgeType::Left);
        }
    }

    // Right edge - only if mode allows horizontal wrapping
    if ((wrapMode == WrapMode::Both || wrapMode == WrapMode::HorizontalOnly) &&
        cursorPos.x >= monitorRect.right - 1 - edgeThreshold)
    {
        auto it = m_edgeMap.find({monitor, EdgeType::Right});
        if (it != m_edgeMap.end() && it->second.isOuter)
        {
            candidateEdges.push_back(EdgeType::Right);
        }
    }

    // Top edge - only if mode allows vertical wrapping
    if ((wrapMode == WrapMode::Both || wrapMode == WrapMode::VerticalOnly) &&
        cursorPos.y <= monitorRect.top + edgeThreshold)
    {
        auto it = m_edgeMap.find({monitor, EdgeType::Top});
        if (it != m_edgeMap.end() && it->second.isOuter)
        {
            candidateEdges.push_back(EdgeType::Top);
        }
    }

    // Bottom edge - only if mode allows vertical wrapping
    if ((wrapMode == WrapMode::Both || wrapMode == WrapMode::VerticalOnly) &&
        cursorPos.y >= monitorRect.bottom - 1 - edgeThreshold)
    {
        auto it = m_edgeMap.find({monitor, EdgeType::Bottom});
        if (it != m_edgeMap.end() && it->second.isOuter)
        {
            candidateEdges.push_back(EdgeType::Bottom);
        }
    }

    if (candidateEdges.empty())
    {
        return false;
    }

    // Try each candidate edge and return first with valid wrap destination
    for (EdgeType candidate : candidateEdges)
    {
        MonitorEdge oppositeEdge = FindOppositeOuterEdge(candidate,
            (candidate == EdgeType::Left || candidate == EdgeType::Right) ? cursorPos.y : cursorPos.x);

        if (oppositeEdge.monitor != nullptr)
        {
            outEdgeType = candidate;
            return true;
        }
    }

    return false;
}

POINT MonitorTopology::GetWrapDestination(HMONITOR fromMonitor, const POINT& cursorPos, EdgeType edgeType) const
{
    auto it = m_edgeMap.find({fromMonitor, edgeType});
    if (it == m_edgeMap.end())
    {
        return cursorPos; // Edge not found
    }

    const MonitorEdge& fromEdge = it->second;

    // Calculate relative position on current edge (0.0 to 1.0)
    double relativePos = GetRelativePosition(fromEdge,
        (edgeType == EdgeType::Left || edgeType == EdgeType::Right) ? cursorPos.y : cursorPos.x);

    // Find opposite outer edge
    MonitorEdge oppositeEdge = FindOppositeOuterEdge(edgeType,
        (edgeType == EdgeType::Left || edgeType == EdgeType::Right) ? cursorPos.y : cursorPos.x);

    if (oppositeEdge.monitor == nullptr)
    {
        // No opposite edge found, wrap within same monitor
        RECT monitorRect;
        if (GetMonitorRect(fromMonitor, monitorRect))
        {
            POINT result = cursorPos;
            switch (edgeType)
            {
            case EdgeType::Left:
                result.x = monitorRect.right - 2;
                break;
            case EdgeType::Right:
                result.x = monitorRect.left + 1;
                break;
            case EdgeType::Top:
                result.y = monitorRect.bottom - 2;
                break;
            case EdgeType::Bottom:
                result.y = monitorRect.top + 1;
                break;
            }
            return result;
        }
        return cursorPos;
    }

    // Calculate target position on opposite edge
    POINT result;

    if (edgeType == EdgeType::Left || edgeType == EdgeType::Right)
    {
        // Horizontal edge -> vertical movement
        result.x = oppositeEdge.position;
        result.y = GetAbsolutePosition(oppositeEdge, relativePos);
    }
    else
    {
        // Vertical edge -> horizontal movement
        result.y = oppositeEdge.position;
        result.x = GetAbsolutePosition(oppositeEdge, relativePos);
    }

    return result;
}

MonitorEdge MonitorTopology::FindOppositeOuterEdge(EdgeType fromEdge, int relativePosition) const
{
    EdgeType targetType;
    bool findMax; // true = find max position, false = find min position

    switch (fromEdge)
    {
    case EdgeType::Left:
        targetType = EdgeType::Right;
        findMax = true;
        break;
    case EdgeType::Right:
        targetType = EdgeType::Left;
        findMax = false;
        break;
    case EdgeType::Top:
        targetType = EdgeType::Bottom;
        findMax = true;
        break;
    case EdgeType::Bottom:
        targetType = EdgeType::Top;
        findMax = false;
        break;
    default:
        return {}; // Invalid edge type
    }

    MonitorEdge result = {};
    int extremePosition = findMax ? INT_MIN : INT_MAX;

    for (const auto& edge : m_outerEdges)
    {
        if (edge.type != targetType)
        {
            continue;
        }

        // Check if this edge overlaps with the relative position
        if (relativePosition >= edge.start && relativePosition <= edge.end)
        {
            if ((findMax && edge.position > extremePosition) ||
                (!findMax && edge.position < extremePosition))
            {
                extremePosition = edge.position;
                result = edge;
            }
        }
    }

    return result;
}

double MonitorTopology::GetRelativePosition(const MonitorEdge& edge, int coordinate) const
{
    if (edge.end == edge.start)
    {
        return 0.5; // Avoid division by zero
    }

    int clamped = max(edge.start, min(coordinate, edge.end));
    // Use int64_t to avoid overflow warning C26451
    int64_t numerator = static_cast<int64_t>(clamped) - static_cast<int64_t>(edge.start);
    int64_t denominator = static_cast<int64_t>(edge.end) - static_cast<int64_t>(edge.start);
    return static_cast<double>(numerator) / static_cast<double>(denominator);
}

int MonitorTopology::GetAbsolutePosition(const MonitorEdge& edge, double relativePosition) const
{
    // Use int64_t to prevent arithmetic overflow during subtraction and multiplication
    int64_t range = static_cast<int64_t>(edge.end) - static_cast<int64_t>(edge.start);
    int64_t offset = static_cast<int64_t>(relativePosition * static_cast<double>(range));
    // Clamp result to int range before returning
    int64_t result = static_cast<int64_t>(edge.start) + offset;
    return static_cast<int>(result);
}

std::vector<MonitorTopology::GapInfo> MonitorTopology::DetectMonitorGaps() const
{
    std::vector<GapInfo> gaps;
    const int gapThreshold = 50; // Same as ADJACENCY_TOLERANCE

    // Check each pair of monitors
    for (size_t i = 0; i < m_monitors.size(); ++i)
    {
        for (size_t j = i + 1; j < m_monitors.size(); ++j)
        {
            const auto& m1 = m_monitors[i];
            const auto& m2 = m_monitors[j];

            // Check vertical overlap
            int vOverlapStart = max(m1.rect.top, m2.rect.top);
            int vOverlapEnd = min(m1.rect.bottom, m2.rect.bottom);
            int vOverlap = vOverlapEnd - vOverlapStart;

            if (vOverlap <= 0)
            {
                continue; // No vertical overlap, skip
            }

            // Check horizontal gap
            int hGap = min(abs(m1.rect.right - m2.rect.left), abs(m2.rect.right - m1.rect.left));

            if (hGap > gapThreshold)
            {
                GapInfo gap;
                gap.monitor1Index = static_cast<int>(i);
                gap.monitor2Index = static_cast<int>(j);
                gap.horizontalGap = hGap;
                gap.verticalOverlap = vOverlap;
                gaps.push_back(gap);
            }
        }
    }

    return gaps;
}

HMONITOR MonitorTopology::GetMonitorFromPoint(const POINT& pt) const
{
    return MonitorFromPoint(pt, MONITOR_DEFAULTTONEAREST);
}

bool MonitorTopology::GetMonitorRect(HMONITOR monitor, RECT& rect) const
{
    for (const auto& monitorInfo : m_monitors)
    {
        HMONITOR hMon = MonitorFromRect(&monitorInfo.rect, MONITOR_DEFAULTTONEAREST);
        if (hMon == monitor)
        {
            rect = monitorInfo.rect;
            return true;
        }
    }
    return false;
}

HMONITOR MonitorTopology::GetMonitorFromRect(const RECT& rect) const
{
    return MonitorFromRect(&rect, MONITOR_DEFAULTTONEAREST);
}
