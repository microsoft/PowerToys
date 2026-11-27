// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#include "pch.h"
#include "SeamlessCursorEngine.h"

#include <algorithm>
#include <cmath>
#include <limits>
#include <utility>

namespace seamless_cursor
{
    namespace
    {
        constexpr double minScale = 0.2;
        constexpr double maxScale = 5.0;
        constexpr double minMarkerSeparation = 0.3; // Fraction of the physically shared span.

        bool Contains(const RECT& rect, POINT pt) noexcept
        {
            return pt.x >= rect.left && pt.x < rect.right && pt.y >= rect.top && pt.y < rect.bottom;
        }

        constexpr bool IsFraction(double value) noexcept
        {
            return value >= 0.0 && value <= 1.0; // Also rejects NaN.
        }

        // Directed link: leaving `source` through `edge` enters `target`.
        struct Link
        {
            size_t source = 0;
            Edge edge = Edge::Top;
            size_t target = 0;
            BorderMap map;
            int sharedStart = 0;
            int sharedEnd = 0;
        };

        // Source pixels whose mapped position lands on the target's facing edge: [first, second).
        std::pair<int, int> PhysicalRange(const Link& link, const RECT& target) noexcept
        {
            const Edge facing = Opposite(link.edge);
            const double targetStart = EdgeStart(target, facing);
            const double targetEnd = targetStart + EdgeLength(target, facing);

            // A pixel maps through its center.
            return { static_cast<int>(std::ceil(link.map.ToA(targetStart) - 0.5)),
                     static_cast<int>(std::ceil(link.map.ToA(targetEnd) - 0.5)) };
        }

        // How far past the edge the requested point is, in pixels (at least 1).
        int Overshoot(const RECT& rect, Edge edge, POINT requested) noexcept
        {
            switch (edge)
            {
            case Edge::Left:
                return rect.left - requested.x;
            case Edge::Right:
                return requested.x - (rect.right - 1);
            case Edge::Top:
                return rect.top - requested.y;
            default:
                return requested.y - (rect.bottom - 1);
            }
        }

        // Where the line between pixels that an edge lies on is.
        int EdgeLine(const RECT& rect, Edge edge) noexcept
        {
            switch (edge)
            {
            case Edge::Left:
                return rect.left;
            case Edge::Right:
                return rect.right;
            case Edge::Top:
                return rect.top;
            default:
                return rect.bottom;
            }
        }

        // Fraction of cursor -> requested where it crosses the boundary between two pixel rows or columns.
        constexpr double CrossingParameter(int cursor, int requested, double boundary) noexcept
        {
            return (boundary - cursor) / (static_cast<double>(requested) - cursor);
        }
    }

    Edge Opposite(Edge edge) noexcept
    {
        switch (edge)
        {
        case Edge::Left:
            return Edge::Right;
        case Edge::Right:
            return Edge::Left;
        case Edge::Top:
            return Edge::Bottom;
        default:
            return Edge::Top;
        }
    }

    int EdgeStart(const RECT& rect, Edge edge) noexcept
    {
        return IsHorizontal(edge) ? rect.left : rect.top;
    }

    int EdgeLength(const RECT& rect, Edge edge) noexcept
    {
        return IsHorizontal(edge) ? rect.right - rect.left : rect.bottom - rect.top;
    }

    std::vector<SharedBorder> FindSharedBorders(const std::vector<RECT>& monitors)
    {
        std::vector<SharedBorder> borders;
        for (size_t i = 0; i < monitors.size(); ++i)
        {
            for (size_t j = i + 1; j < monitors.size(); ++j)
            {
                const RECT& a = monitors[i];
                const RECT& b = monitors[j];
                const auto add = [&](Edge edgeOfA, int start, int end) {
                    if (start < end)
                    {
                        borders.push_back({ i, j, edgeOfA, start, end });
                    }
                };

                const int left = std::max(a.left, b.left);
                const int right = std::min(a.right, b.right);
                const int top = std::max(a.top, b.top);
                const int bottom = std::min(a.bottom, b.bottom);

                if (b.bottom == a.top)
                {
                    add(Edge::Top, left, right);
                }
                if (b.top == a.bottom)
                {
                    add(Edge::Bottom, left, right);
                }
                if (b.right == a.left)
                {
                    add(Edge::Left, top, bottom);
                }
                if (b.left == a.right)
                {
                    add(Edge::Right, top, bottom);
                }
            }
        }

        return borders;
    }

    std::vector<SharedBorder> FindGlueCandidates(const std::vector<RECT>& monitors)
    {
        const auto intersects = [](const RECT& r, const RECT& gap) {
            return r.left < gap.right && gap.left < r.right && r.top < gap.bottom && gap.top < r.bottom;
        };

        std::vector<SharedBorder> candidates;
        for (size_t i = 0; i < monitors.size(); ++i)
        {
            for (size_t j = i + 1; j < monitors.size(); ++j)
            {
                const RECT& a = monitors[i];
                const RECT& b = monitors[j];
                const int left = std::max(a.left, b.left);
                const int right = std::min(a.right, b.right);
                const int top = std::max(a.top, b.top);
                const int bottom = std::min(a.bottom, b.bottom);

                std::optional<SharedBorder> candidate;
                RECT gap{};
                if (top < bottom && b.left > a.right)
                {
                    candidate = SharedBorder{ i, j, Edge::Right, top, bottom };
                    gap = { a.right, top, b.left, bottom };
                }
                else if (top < bottom && b.right < a.left)
                {
                    candidate = SharedBorder{ i, j, Edge::Left, top, bottom };
                    gap = { b.right, top, a.left, bottom };
                }
                else if (left < right && b.top > a.bottom)
                {
                    candidate = SharedBorder{ i, j, Edge::Bottom, left, right };
                    gap = { left, a.bottom, right, b.top };
                }
                else if (left < right && b.bottom < a.top)
                {
                    candidate = SharedBorder{ i, j, Edge::Top, left, right };
                    gap = { left, b.bottom, right, a.top };
                }

                // A monitor in between gets the cursor first, so gluing across it would skip that monitor.
                if (candidate && std::none_of(monitors.begin(), monitors.end(), [&](const RECT& m) { return intersects(m, gap); }))
                {
                    candidates.push_back(*candidate);
                }
            }
        }

        return candidates;
    }

    double MarkerPosition(const RECT& rect, Edge edge, double fraction) noexcept
    {
        return EdgeStart(rect, edge) + fraction * EdgeLength(rect, edge);
    }

    double MarkerFraction(const RECT& rect, Edge edge, double position) noexcept
    {
        return (position - EdgeStart(rect, edge)) / EdgeLength(rect, edge);
    }

    CalibrationIssue Validate(const BorderCalibration& calibration, const RECT& a, const RECT& b) noexcept
    {
        const auto& pairs = calibration.pairs;
        for (const auto& pair : pairs)
        {
            if (!IsFraction(pair.a) || !IsFraction(pair.b))
            {
                return CalibrationIssue::InvalidMarkers;
            }
        }

        const Edge edgeOfB = Opposite(calibration.edgeOfA);
        const double deltaA = MarkerPosition(a, calibration.edgeOfA, pairs[1].a) - MarkerPosition(a, calibration.edgeOfA, pairs[0].a);
        const double deltaB = MarkerPosition(b, edgeOfB, pairs[1].b) - MarkerPosition(b, edgeOfB, pairs[0].b);
        if (std::abs(deltaA) < 1.0 || std::abs(deltaB) < 1.0)
        {
            return CalibrationIssue::InvalidMarkers;
        }

        if ((deltaA > 0) != (deltaB > 0))
        {
            return CalibrationIssue::OrderReversed;
        }

        const double scale = deltaB / deltaA;
        if (scale < minScale || scale > maxScale)
        {
            return CalibrationIssue::ScaleOutOfRange;
        }

        // Precision drops with distance from the markers, so they should cover a good part of the span that
        // both monitors physically share. The markers lie inside that span, so it is never shorter than deltaA.
        const BorderMap map{ MarkerPosition(a, calibration.edgeOfA, pairs[0].a), MarkerPosition(b, edgeOfB, pairs[0].b), scale };
        const double startB = EdgeStart(b, edgeOfB);
        const double sharedStart = std::max(static_cast<double>(EdgeStart(a, calibration.edgeOfA)), map.ToA(startB));
        const double sharedEnd = std::min(static_cast<double>(EdgeStart(a, calibration.edgeOfA)) + EdgeLength(a, calibration.edgeOfA), map.ToA(startB + EdgeLength(b, edgeOfB)));
        if (std::abs(deltaA) < minMarkerSeparation * (sharedEnd - sharedStart))
        {
            return CalibrationIssue::MarkersTooClose;
        }

        return CalibrationIssue::None;
    }

    BorderMap MakeBorderMap(const BorderCalibration& calibration, const RECT& a, const RECT& b) noexcept
    {
        const Edge edgeOfA = calibration.edgeOfA;
        const Edge edgeOfB = Opposite(edgeOfA);
        const auto& pairs = calibration.pairs;
        const double a0 = MarkerPosition(a, edgeOfA, pairs[0].a);
        const double a1 = MarkerPosition(a, edgeOfA, pairs[1].a);
        const double b0 = MarkerPosition(b, edgeOfB, pairs[0].b);
        const double b1 = MarkerPosition(b, edgeOfB, pairs[1].b);
        return { a0, b0, (b1 - b0) / (a1 - a0) };
    }

    void Engine::Build(const std::vector<Monitor>& monitors, const std::vector<BorderCalibration>& calibrations, EngineOptions options)
    {
        m_monitors.clear();
        m_jumpMaps.clear();
        m_lastSource = 0;

        std::vector<RECT> rects;
        rects.reserve(monitors.size());
        for (const auto& monitor : monitors)
        {
            rects.push_back(monitor.rect);
            m_monitors.push_back({ monitor.rect, {} });
        }

        const auto findMonitor = [&monitors](const std::wstring& id) -> std::optional<size_t> {
            const auto it = std::find_if(monitors.begin(), monitors.end(), [&id](const Monitor& m) { return m.id == id; });
            return it == monitors.end() ? std::nullopt : std::optional<size_t>{ static_cast<size_t>(it - monitors.begin()) };
        };

        const auto shared = FindSharedBorders(rects);
        const auto glued = FindGlueCandidates(rects);
        std::vector<Link> links;
        for (const auto& calibration : calibrations)
        {
            const auto a = findMonitor(calibration.monitorA);
            const auto b = findMonitor(calibration.monitorB);
            if (!calibration.enabled || !a || !b || *a == *b ||
                IsLandscape(rects[*a]) != calibration.landscapeA || IsLandscape(rects[*b]) != calibration.landscapeB)
            {
                continue;
            }

            // The Windows layout must still place B across the calibrated edge of A: touching, or glued across a gap.
            const auto matches = [&](const SharedBorder& s) {
                return (s.monitorA == *a && s.monitorB == *b && s.edgeOfA == calibration.edgeOfA) ||
                       (s.monitorA == *b && s.monitorB == *a && s.edgeOfA == Opposite(calibration.edgeOfA));
            };
            const auto border = std::find_if(shared.begin(), shared.end(), matches);
            const bool touching = border != shared.end();
            const bool alreadyLinked = std::any_of(links.begin(), links.end(), [&](const Link& l) {
                return l.source == *a && l.target == *b;
            });
            if ((!touching && std::none_of(glued.begin(), glued.end(), matches)) ||
                alreadyLinked || !IsUsable(Validate(calibration, rects[*a], rects[*b])))
            {
                continue;
            }

            // Windows does not connect glued monitors at all, so only their physical overlap is remapped.
            const int sharedStart = touching ? border->start : 0;
            const int sharedEnd = touching ? border->end : 0;
            const auto map = MakeBorderMap(calibration, rects[*a], rects[*b]);
            links.push_back({ *a, calibration.edgeOfA, *b, map, sharedStart, sharedEnd });
            links.push_back({ *b, Opposite(calibration.edgeOfA), *a, map.Inverse(), sharedStart, sharedEnd });
        }

        const size_t count = m_monitors.size();
        m_jumpMaps.assign(2 * count * count, std::nullopt);
        for (const bool horizontal : { false, true })
        {
            for (size_t from = 0; from < count; ++from)
            {
                // Breadth first, so the chain with the fewest borders wins.
                m_jumpMaps[JumpIndex(horizontal, from, from)] = BorderMap{};
                std::vector<size_t> queue{ from };
                for (size_t next = 0; next < queue.size(); ++next)
                {
                    const size_t monitor = queue[next];
                    const BorderMap toMonitor = *m_jumpMaps[JumpIndex(horizontal, from, monitor)];
                    for (const auto& link : links)
                    {
                        auto& toTarget = m_jumpMaps[JumpIndex(horizontal, from, link.target)];
                        if (link.source == monitor && IsHorizontal(link.edge) == horizontal && !toTarget)
                        {
                            // The link's map applied after the map to its source monitor.
                            toTarget = BorderMap{ toMonitor.originA, link.map.ToB(toMonitor.originB), toMonitor.scale * link.map.scale };
                            queue.push_back(link.target);
                        }
                    }
                }
            }
        }

        for (size_t source = 0; source < m_monitors.size(); ++source)
        {
            for (size_t edgeIndex = 0; edgeIndex < edgeCount; ++edgeIndex)
            {
                const Edge edge = static_cast<Edge>(edgeIndex);
                std::vector<const Link*> edgeLinks;
                std::vector<std::pair<int, int>> physicalRanges;
                for (const auto& link : links)
                {
                    if (link.source == source && link.edge == edge)
                    {
                        edgeLinks.push_back(&link);
                        physicalRanges.push_back(PhysicalRange(link, rects[link.target]));
                    }
                }

                // Every range Windows connects on this edge, to calibrated monitors or not.
                auto& windowsRanges = m_monitors[source].windowsRanges[edgeIndex];
                for (const auto& border : shared)
                {
                    if ((border.monitorA == source && border.edgeOfA == edge) || (border.monitorB == source && Opposite(border.edgeOfA) == edge))
                    {
                        windowsRanges.push_back({ border.start, border.end });
                    }
                }

                if (edgeLinks.empty())
                {
                    continue;
                }

                const int edgeStart = EdgeStart(rects[source], edge);
                const int edgeEnd = edgeStart + EdgeLength(rects[source], edge);
                std::vector<int> cuts{ edgeStart, edgeEnd };
                for (size_t i = 0; i < edgeLinks.size(); ++i)
                {
                    cuts.insert(cuts.end(), { physicalRanges[i].first, physicalRanges[i].second, edgeLinks[i]->sharedStart, edgeLinks[i]->sharedEnd });
                }

                for (const auto& [start, end] : windowsRanges)
                {
                    cuts.insert(cuts.end(), { start, end });
                }

                for (auto& cut : cuts)
                {
                    cut = std::clamp(cut, edgeStart, edgeEnd);
                }

                std::sort(cuts.begin(), cuts.end());
                cuts.erase(std::unique(cuts.begin(), cuts.end()), cuts.end());

                auto& segments = m_monitors[source].edges[edgeIndex];
                for (size_t c = 0; c + 1 < cuts.size(); ++c)
                {
                    const int start = cuts[c];
                    const int end = cuts[c + 1];

                    // A monitor that is physically across the edge wins over plain Windows adjacency.
                    // If calibrations claim two physical neighbors here, prefer the one hit nearer its center.
                    const Link* winner = nullptr;
                    double bestDistance = std::numeric_limits<double>::max();
                    for (size_t i = 0; i < edgeLinks.size(); ++i)
                    {
                        if (start < physicalRanges[i].first || start >= physicalRanges[i].second)
                        {
                            continue;
                        }

                        const RECT& target = rects[edgeLinks[i]->target];
                        const Edge facing = Opposite(edge);
                        const double center = EdgeStart(target, facing) + EdgeLength(target, facing) / 2.0;
                        const double distance = std::abs(edgeLinks[i]->map.ToB((static_cast<double>(start) + end) / 2.0) - center) / EdgeLength(target, facing);
                        if (distance < bestDistance)
                        {
                            bestDistance = distance;
                            winner = edgeLinks[i];
                        }
                    }

                    // Where Windows connects the monitors but nothing is physically across, the cursor stops at
                    // the edge, as it would at the edge of a physical screen, or goes to the target's nearest corner.
                    const bool wall = winner == nullptr && options.stopWhereNothingIsAcross;
                    for (size_t i = 0; !winner && i < edgeLinks.size(); ++i)
                    {
                        if (start >= edgeLinks[i]->sharedStart && start < edgeLinks[i]->sharedEnd)
                        {
                            winner = edgeLinks[i];
                        }
                    }

                    Segment segment;
                    if (winner)
                    {
                        segment = { start, end, winner->target, winner->map, wall, false };
                    }
                    else if (options.stopWhereNothingIsAcross &&
                             std::none_of(windowsRanges.begin(), windowsRanges.end(), [start](const auto& r) { return start >= r.first && start < r.second; }))
                    {
                        // Nothing is across in the calibration or in the Windows layout. Windows would stop the cursor
                        // too, unless "Ease cursor movement between displays" moves it to a nearby monitor.
                        segment = { start, end, source, {}, true, true };
                    }
                    else
                    {
                        // An uncalibrated monitor that Windows connects: its own layout applies.
                        continue;
                    }

                    if (!segments.empty() && segments.back().end == start && segments.back().target == segment.target &&
                        segments.back().wall == segment.wall && segments.back().outer == segment.outer)
                    {
                        segments.back().end = end;
                    }
                    else
                    {
                        segments.push_back(segment);
                    }
                }
            }
        }
    }

    std::optional<Engine::Remap> Engine::Resolve(POINT cursor, POINT requested)
    {
        if (m_monitors.empty())
        {
            return std::nullopt;
        }

        if (m_lastSource >= m_monitors.size() || !Contains(m_monitors[m_lastSource].rect, cursor))
        {
            const auto it = std::find_if(m_monitors.begin(), m_monitors.end(), [cursor](const MonitorEdges& m) { return Contains(m.rect, cursor); });
            if (it == m_monitors.end())
            {
                return std::nullopt;
            }

            m_lastSource = static_cast<size_t>(it - m_monitors.begin());
        }

        const RECT& rect = m_monitors[m_lastSource].rect;
        if (Contains(rect, requested))
        {
            return std::nullopt;
        }

        // The boundary sits between the outermost pixel row/column and the next one.
        struct Exit
        {
            Edge edge = Edge::Top;
            double t = 0.0;
        };
        std::array<Exit, 2> exits{};
        size_t exitCount = 0;
        if (requested.x < rect.left)
        {
            exits[exitCount++] = { Edge::Left, CrossingParameter(cursor.x, requested.x, rect.left - 0.5) };
        }
        else if (requested.x >= rect.right)
        {
            exits[exitCount++] = { Edge::Right, CrossingParameter(cursor.x, requested.x, rect.right - 0.5) };
        }

        if (requested.y < rect.top)
        {
            exits[exitCount++] = { Edge::Top, CrossingParameter(cursor.y, requested.y, rect.top - 0.5) };
        }
        else if (requested.y >= rect.bottom)
        {
            exits[exitCount++] = { Edge::Bottom, CrossingParameter(cursor.y, requested.y, rect.bottom - 0.5) };
        }

        // Through a corner, try the edge the move crosses first.
        if (exitCount == 2 && exits[1].t < exits[0].t)
        {
            std::swap(exits[0], exits[1]);
        }

        // Through a corner, a wall on the first edge must not hide a real crossing of the second one, and a monitor
        // that Windows connects without calibration takes the cursor whichever edge is crossed first.
        std::optional<Jump> wall;
        bool leavesDesktop = false; // Across an edge where Windows connects no monitor and the engine has no part.
        for (size_t i = 0; i < exitCount; ++i)
        {
            const Edge edge = exits[i].edge;
            const int cursorAlong = IsHorizontal(edge) ? cursor.x : cursor.y;
            const int requestedAlong = IsHorizontal(edge) ? requested.x : requested.y;
            const int edgeStart = EdgeStart(rect, edge);

            // Pixels map through their centers, so the move runs from cursorAlong + 0.5 to requestedAlong + 0.5.
            const int crossing = std::clamp(
                static_cast<int>(std::floor(cursorAlong + 0.5 + exits[i].t * (static_cast<double>(requestedAlong) - cursorAlong))),
                edgeStart,
                edgeStart + EdgeLength(rect, edge) - 1);

            const auto jump = Cross(m_lastSource, edge, crossing, requested);
            if (!jump)
            {
                if (!IsWindowsConnected(m_lastSource, edge, crossing))
                {
                    // An outer desktop edge left to Windows, where other cursor tools act.
                    leavesDesktop = true;
                    continue;
                }

                // Windows takes the cursor to the requested point. After a wall on the edge crossed first, that point can
                // lie beyond the corner in a monitor the calibration links to this one, or in no monitor, from where
                // Windows moves it to the nearest one. Then take the cursor straight across this edge instead, as a move
                // across this edge alone would go. Otherwise the move is left to Windows, as without calibration.
                if (!wall || IsInUnlinkedMonitor(m_lastSource, requested))
                {
                    return std::nullopt;
                }

                // After an outer wall, other cursor tools act first, as at outer desktop edges. However far the move
                // overshoots, the point stays in the monitor Windows connects there.
                POINT point = requested;
                (IsHorizontal(edge) ? point.x : point.y) = crossing;
                if (const auto neighbor = WindowsNeighbor(m_lastSource, edge, crossing))
                {
                    const RECT& target = m_monitors[*neighbor].rect;
                    point.x = std::clamp(point.x, target.left, target.right - 1);
                    point.y = std::clamp(point.y, target.top, target.bottom - 1);
                }

                return Remap{ point, wall->outerWall };
            }

            if (jump->target == m_lastSource)
            {
                if (wall)
                {
                    // An outer wall on either edge means other cursor tools act first.
                    wall->outerWall = wall->outerWall || jump->outerWall;
                }
                else
                {
                    wall = jump;
                }

                continue;
            }

            m_lastSource = jump->target;
            return Remap{ jump->point, false };
        }

        // A wall next to an outer desktop edge stops the cursor where other cursor tools act too, so they go first.
        return wall ? std::optional<Remap>{ Remap{ wall->point, wall->outerWall || leavesDesktop } } : std::nullopt;
    }

    std::optional<int> Engine::MapJump(const RECT& from, Edge edge, int along, const RECT& to) const noexcept
    {
        const auto indexOf = [this](const RECT& rect) -> std::optional<size_t> {
            const auto it = std::find_if(m_monitors.begin(), m_monitors.end(), [&rect](const MonitorEdges& m) {
                return m.rect.left == rect.left && m.rect.top == rect.top && m.rect.right == rect.right && m.rect.bottom == rect.bottom;
            });
            return it == m_monitors.end() ? std::nullopt : std::optional<size_t>{ static_cast<size_t>(it - m_monitors.begin()) };
        };

        const auto source = indexOf(from);
        const auto target = indexOf(to);
        if (!source || !target)
        {
            return std::nullopt;
        }

        // Pixels map through their centers.
        const auto& map = m_jumpMaps[JumpIndex(IsHorizontal(edge), *source, *target)];
        if (!map)
        {
            return std::nullopt;
        }

        return static_cast<int>(std::floor(map->ToB(along + 0.5)));
    }

    size_t Engine::JumpIndex(bool horizontal, size_t from, size_t to) const noexcept
    {
        return ((horizontal ? m_monitors.size() : 0) + from) * m_monitors.size() + to;
    }

    std::optional<size_t> Engine::WindowsNeighbor(size_t source, Edge edge, int crossing) const noexcept
    {
        const RECT& rect = m_monitors[source].rect;
        const Edge facing = Opposite(edge);
        for (size_t i = 0; i < m_monitors.size(); ++i)
        {
            const RECT& other = m_monitors[i].rect;
            const bool touches = EdgeLine(other, facing) == EdgeLine(rect, edge);
            const int start = EdgeStart(other, facing);
            if (i != source && touches && crossing >= start && crossing < start + EdgeLength(other, facing))
            {
                return i;
            }
        }

        return std::nullopt;
    }

    bool Engine::IsInUnlinkedMonitor(size_t source, POINT point) const noexcept
    {
        for (size_t target = 0; target < m_monitors.size(); ++target)
        {
            if (target != source && Contains(m_monitors[target].rect, point))
            {
                return std::none_of(m_monitors[source].edges.begin(), m_monitors[source].edges.end(), [target](const auto& segments) {
                    return std::any_of(segments.begin(), segments.end(), [target](const Segment& s) { return s.target == target; });
                });
            }
        }

        return false;
    }

    bool Engine::IsWindowsConnected(size_t source, Edge edge, int crossing) const noexcept
    {
        const auto& ranges = m_monitors[source].windowsRanges[static_cast<size_t>(edge)];
        return std::any_of(ranges.begin(), ranges.end(), [crossing](const auto& r) { return crossing >= r.first && crossing < r.second; });
    }

    std::optional<Engine::Jump> Engine::Cross(size_t source, Edge edge, int crossing, POINT requested) const
    {
        const auto& segments = m_monitors[source].edges[static_cast<size_t>(edge)];
        if (segments.empty())
        {
            return std::nullopt;
        }

        const RECT& rect = m_monitors[source].rect;
        const bool horizontal = IsHorizontal(edge);
        const int requestedAlong = horizontal ? requested.x : requested.y;
        const auto segment = std::find_if(segments.begin(), segments.end(), [crossing](const Segment& s) {
            return crossing >= s.start && crossing < s.end;
        });
        if (segment == segments.end())
        {
            return std::nullopt;
        }

        if (segment->wall)
        {
            // Stay on the source monitor, but keep the movement along the edge, so the cursor slides along it.
            const POINT inside{ std::clamp(requested.x, rect.left, rect.right - 1), std::clamp(requested.y, rect.top, rect.bottom - 1) };
            return Jump{ inside, source, segment->outer };
        }

        const RECT& target = m_monitors[segment->target].rect;
        const Edge facing = Opposite(edge);
        const int targetStart = EdgeStart(target, facing);

        // Along the edge, map where the move ends, so diagonal movement stays continuous.
        const int along = std::clamp(
            static_cast<int>(std::floor(segment->map.ToB(requestedAlong + 0.5))),
            targetStart,
            targetStart + EdgeLength(target, facing) - 1);

        // Across the edge, keep the overshoot, converted to target pixels.
        const int targetDepth = horizontal ? target.bottom - target.top : target.right - target.left;
        const int depth = std::clamp(
            static_cast<int>(std::lround(Overshoot(rect, edge, requested) * segment->map.scale)),
            1,
            targetDepth);

        int across = 0;
        switch (facing)
        {
        case Edge::Left:
            across = target.left + depth - 1;
            break;
        case Edge::Right:
            across = target.right - depth;
            break;
        case Edge::Top:
            across = target.top + depth - 1;
            break;
        default:
            across = target.bottom - depth;
            break;
        }

        const POINT point = horizontal ? POINT{ along, across } : POINT{ across, along };
        return Jump{ point, segment->target, false };
    }

    bool Engine::IsEmpty() const noexcept
    {
        return std::all_of(m_monitors.begin(), m_monitors.end(), [](const MonitorEdges& m) {
            return std::all_of(m.edges.begin(), m.edges.end(), [](const auto& segments) { return segments.empty(); });
        });
    }

    std::vector<Engine::EdgePart> Engine::EdgeParts(const RECT& monitor, Edge edge) const
    {
        std::vector<EdgePart> parts;
        for (const auto& m : m_monitors)
        {
            if (m.rect.left == monitor.left && m.rect.top == monitor.top && m.rect.right == monitor.right && m.rect.bottom == monitor.bottom)
            {
                for (const auto& segment : m.edges[static_cast<size_t>(edge)])
                {
                    parts.push_back({ segment.start, segment.end, segment.target, segment.wall });
                }

                break;
            }
        }

        return parts;
    }

    bool Engine::IsEdgeClaimed(const RECT& monitor, Edge edge, int along) const noexcept
    {
        const auto source = std::find_if(m_monitors.begin(), m_monitors.end(), [&monitor](const MonitorEdges& m) {
            return m.rect.left == monitor.left && m.rect.top == monitor.top && m.rect.right == monitor.right && m.rect.bottom == monitor.bottom;
        });
        if (source == m_monitors.end())
        {
            return false;
        }

        // Only crossings are claimed; where Seamless just stops the cursor, CursorWrap may still wrap it.
        const auto& segments = source->edges[static_cast<size_t>(edge)];
        return std::any_of(segments.begin(), segments.end(), [along](const Segment& s) { return !s.wall && along >= s.start && along < s.end; });
    }
}
