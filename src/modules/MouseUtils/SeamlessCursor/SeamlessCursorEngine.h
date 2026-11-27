// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#pragma once

#include <windows.h>

#include <array>
#include <cstddef>
#include <optional>
#include <string>
#include <vector>

// Layout and mapping logic only. It makes no Win32 calls, so it is unit tested directly.
// All coordinates are physical pixels in virtual-desktop space.
namespace seamless_cursor
{
    enum class Edge
    {
        Left = 0,
        Right = 1,
        Top = 2,
        Bottom = 3,
    };

    constexpr size_t edgeCount = 4;

    Edge Opposite(Edge edge) noexcept;

    // True for Top and Bottom, where the coordinate along the edge is x.
    constexpr bool IsHorizontal(Edge edge) noexcept
    {
        return edge == Edge::Top || edge == Edge::Bottom;
    }

    constexpr bool IsLandscape(const RECT& rect) noexcept
    {
        return rect.right - rect.left >= rect.bottom - rect.top;
    }

    // Pixel range of a monitor along the given edge: [EdgeStart, EdgeStart + EdgeLength).
    int EdgeStart(const RECT& rect, Edge edge) noexcept;
    int EdgeLength(const RECT& rect, Edge edge) noexcept;

    struct Monitor
    {
        std::wstring id;
        RECT rect{};
    };

    // One pair of markers the user aligned at the same physical spot, stored as fractions of the
    // edge length of monitor A and monitor B, so they survive re-arranging and resolution changes.
    struct MarkerPair
    {
        double a = 0.0;
        double b = 0.0;
    };

    struct BorderCalibration
    {
        std::wstring monitorA;
        std::wstring monitorB;
        Edge edgeOfA = Edge::Top; // Monitor B lies across this edge of monitor A.
        bool enabled = true;
        std::array<MarkerPair, 2> pairs{};

        // Orientation at calibration time; a rotated monitor invalidates the fractions.
        bool landscapeA = true;
        bool landscapeB = true;

        // A record saved for a pair that faces across a gap: there, "not enabled" means unglued. The engine does not
        // need it.
        bool glue = false;
    };

    // Part of a border that the Windows layout shares between two monitors.
    struct SharedBorder
    {
        size_t monitorA = 0;
        size_t monitorB = 0;
        Edge edgeOfA = Edge::Top;
        int start = 0; // Shared range along the edge: [start, end).
        int end = 0;
    };

    // Returns every touching pair once, with monitorA < monitorB.
    std::vector<SharedBorder> FindSharedBorders(const std::vector<RECT>& monitors);

    // Pairs that face each other across a gap the Windows layout cannot close: B lies entirely beyond one edge of A,
    // their ranges along that edge overlap (start/end), and no other monitor lies in between. Such a pair can be
    // glued: calibrated like a shared border, so the cursor crosses the gap. Same order as FindSharedBorders.
    std::vector<SharedBorder> FindGlueCandidates(const std::vector<RECT>& monitors);

    enum class CalibrationIssue
    {
        None,
        MarkersTooClose, // Still usable, only less precise.
        InvalidMarkers, // At the same spot, or outside the edge.
        OrderReversed,
        ScaleOutOfRange,
    };

    constexpr bool IsUsable(CalibrationIssue issue) noexcept
    {
        return issue == CalibrationIssue::None || issue == CalibrationIssue::MarkersTooClose;
    }

    // Linear map of the coordinate along a border, from monitor A pixels to monitor B pixels.
    // Coordinates are continuous: pixel u covers [u, u + 1).
    struct BorderMap
    {
        double originA = 0.0;
        double originB = 0.0;
        double scale = 1.0; // Monitor B pixels per monitor A pixel.

        double ToB(double uA) const noexcept { return originB + scale * (uA - originA); }
        double ToA(double uB) const noexcept { return originA + (uB - originB) / scale; }
        BorderMap Inverse() const noexcept { return { originB, originA, 1.0 / scale }; }
    };

    // Continuous coordinate of a marker along the edge.
    double MarkerPosition(const RECT& rect, Edge edge, double fraction) noexcept;
    double MarkerFraction(const RECT& rect, Edge edge, double position) noexcept;

    CalibrationIssue Validate(const BorderCalibration& calibration, const RECT& a, const RECT& b) noexcept;

    // Only meaningful when Validate returns a usable result.
    BorderMap MakeBorderMap(const BorderCalibration& calibration, const RECT& a, const RECT& b) noexcept;

    struct EngineOptions
    {
        // Where nothing is physically across, stop the cursor. Otherwise it goes to the nearest corner of the monitor
        // Windows connects there, and at outer edges Windows decides (including its "Ease cursor movement between
        // displays" setting).
        bool stopWhereNothingIsAcross = true;
    };

    class Engine
    {
    public:
        void Build(const std::vector<Monitor>& monitors, const std::vector<BorderCalibration>& calibrations, EngineOptions options = {});

        struct Remap
        {
            POINT point{};

            // The cursor stops at an outer desktop edge. Other cursor tools (CursorWrap, Mouse Without Borders) act
            // there, so they get the move first.
            bool outerWall = false;
        };

        // cursor: position before the move. requested: position the move asks for.
        // Returns the remapped position, or nothing when Windows should handle the move.
        std::optional<Remap> Resolve(POINT cursor, POINT requested);

        bool IsEmpty() const noexcept;

        struct EdgePart
        {
            int start = 0; // Range along the edge: [start, end).
            int end = 0;
            size_t target = 0; // Index into the monitors passed to Build.
            bool wall = false; // The cursor stops here instead of crossing.
        };

        // How moves across this edge of the monitor with this rectangle are handled; other parts are left to Windows.
        std::vector<EdgePart> EdgeParts(const RECT& monitor, Edge edge) const;

        // True when a move across `edge` of the monitor with this rectangle, at `along` (the coordinate along
        // the edge), is remapped. Other cursor tools use it to leave those parts of the edge alone.
        bool IsEdgeClaimed(const RECT& monitor, Edge edge, int along) const noexcept;

        // For a pointer that jumps between monitors without crossing a calibrated border, as when CursorWrap wraps it
        // around the desktop: the coordinate on monitor `to` at the same physical position as `along` on monitor
        // `from`. `edge` is the edge of `from` the pointer leaves through; `along` and the result are coordinates along
        // it (y for left and right, x for top and bottom). Only borders on edges of the same orientation tell where that
        // position is, so the result follows a chain of those, the one with the fewest borders; without one, nothing.
        std::optional<int> MapJump(const RECT& from, Edge edge, int along, const RECT& to) const noexcept;

    private:
        struct Segment
        {
            int start = 0; // Range along the source edge: [start, end).
            int end = 0;
            size_t target = 0;
            BorderMap map; // From source pixels to target pixels.
            bool wall = false; // Nothing is physically across, so the cursor stops.
            bool outer = false; // A wall where Windows connects no monitor either.
        };

        struct MonitorEdges
        {
            RECT rect{};
            std::array<std::vector<Segment>, edgeCount> edges;

            // Ranges where Windows connects a monitor across each edge, calibrated or not.
            std::array<std::vector<std::pair<int, int>>, edgeCount> windowsRanges;
        };

        struct Jump
        {
            POINT point{};
            size_t target = 0;
            bool outerWall = false;
        };

        // crossing: where along the edge the move crosses it, clamped to the edge.
        std::optional<Jump> Cross(size_t source, Edge edge, int crossing, POINT requested) const;
        bool IsWindowsConnected(size_t source, Edge edge, int crossing) const noexcept;

        // The monitor that Windows places across `edge` of `source` at `crossing`.
        std::optional<size_t> WindowsNeighbor(size_t source, Edge edge, int crossing) const noexcept;

        // Index into m_jumpMaps for borders of the given orientation (horizontal: top and bottom edges).
        size_t JumpIndex(bool horizontal, size_t from, size_t to) const noexcept;

        // True when `point` lies in another monitor that the calibration does not link to `source`.
        bool IsInUnlinkedMonitor(size_t source, POINT point) const noexcept;

        std::vector<MonitorEdges> m_monitors;

        // For MapJump, per orientation of edges: the composed map from every monitor to every other along the chain of
        // calibrated borders with the fewest borders, or nothing without one. Built here, so MapJump neither searches
        // nor allocates in CursorWrap's mouse hook.
        std::vector<std::optional<BorderMap>> m_jumpMaps;
        size_t m_lastSource = 0;
    };
}
