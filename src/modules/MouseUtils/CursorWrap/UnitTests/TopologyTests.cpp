// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

// Unit tests for MonitorTopology, mirroring the Rust test suite in
// src/rust/libs/cursorwrap-core/src/topology.rs.
//
// These are pure-logic tests that construct MonitorInfo vectors and exercise
// topology initialization, outer edge detection, edge adjacency, and wrap
// destination calculation without requiring real monitors.

#include "pch.h"

#pragma warning(push)
#pragma warning(disable : 26466)
#include "CppUnitTest.h"
#pragma warning(pop)

#include "../MonitorTopology.h"
#include "../CursorWrapCore.h"

using namespace Microsoft::VisualStudio::CppUnitTestFramework;

namespace CursorWrapUnitTests
{
    // ── helpers ──────────────────────────────────────────────────────────────

    // Create a MonitorInfo with a fake HMONITOR handle derived from the index.
    static MonitorInfo MakeMonitor(int index, LONG left, LONG top, LONG right, LONG bottom, bool primary = false)
    {
        MonitorInfo mi{};
        mi.hMonitor = reinterpret_cast<HMONITOR>(static_cast<uintptr_t>(index) + 1u);
        mi.rect = { left, top, right, bottom };
        mi.isPrimary = primary;
        mi.monitorId = index;
        return mi;
    }

    static HMONITOR HandleForIndex(int index)
    {
        return reinterpret_cast<HMONITOR>(static_cast<uintptr_t>(index) + 1u);
    }

    // Count outer edges belonging to a specific monitor.
    static int CountOuterEdgesForMonitor(const MonitorTopology& topo, int monitorIndex)
    {
        int count = 0;
        for (const auto& e : topo.GetOuterEdges())
        {
            if (e.monitorIndex == monitorIndex)
                ++count;
        }
        return count;
    }

    // ── test class ──────────────────────────────────────────────────────────

    TEST_CLASS(TopologyTests)
    {
    public:
        // ── Single monitor ──────────────────────────────────────────────

        // Product code: MonitorTopology.h — Initialize(), IdentifyOuterEdges()
        // What: Verifies single monitor has all 4 edges marked as outer (no adjacent monitors)
        // Why: Single-monitor is the base case; if outer-edge detection fails here, wrap logic breaks everywhere
        // Risk: CursorWrap would never wrap (or always wrap incorrectly) on single-monitor setups
        TEST_METHOD(SingleMonitor_AllEdgesAreOuter)
        {
            MonitorTopology topo;
            std::vector<MonitorInfo> monitors = {
                MakeMonitor(0, 0, 0, 1920, 1080, true),
            };
            topo.Initialize(monitors);

            Assert::AreEqual(4, static_cast<int>(topo.GetOuterEdges().size()),
                             L"Single monitor should have 4 outer edges");
        }

        // Product code: MonitorTopology.h — IsOnOuterEdge(), GetWrapDestination()
        // What: Verifies IsOnOuterEdge detects the left edge and GetWrapDestination returns a different point (self-wrap)
        // Why: When no opposite monitor exists, wrapping falls back to same-monitor opposite edge
        // Risk: Cursor would get stuck at edges on single-monitor setups instead of wrapping
        TEST_METHOD(SingleMonitor_NoWrapPartner)
        {
            MonitorTopology topo;
            std::vector<MonitorInfo> monitors = {
                MakeMonitor(0, 0, 0, 1920, 1080, true),
            };
            topo.Initialize(monitors);

            // Cursor at the left edge. IsOnOuterEdge should be true and
            // GetWrapDestination should wrap to the opposite edge of the same
            // monitor when no opposite monitor exists.
            EdgeType edgeType{};
            POINT cursor = { 0, 540 };
            bool isOuter = topo.IsOnOuterEdge(HandleForIndex(0), cursor, edgeType, WrapMode::Both);

            Assert::IsTrue(isOuter, L"Left edge of a single monitor should be detected as an outer edge");

            POINT dest = topo.GetWrapDestination(HandleForIndex(0), cursor, edgeType);
            // With no opposite monitor, wrap destination falls back to same
            // monitor's opposite edge.
            Assert::IsTrue(dest.x != cursor.x || dest.y != cursor.y,
                           L"Wrap destination should differ from source on a self-wrap");
        }

        // ── Two side-by-side monitors ───────────────────────────────────

        // Product code: MonitorTopology.h — Initialize(), IdentifyOuterEdges(), EdgesAreAdjacent()
        // What: [Mon0: 0–1920] [Mon1: 1920–3840] — verifies 6 outer edges (8 total minus 2 shared inner edges)
        // Why: Adjacency detection must correctly identify shared edges to prevent wrapping between touching monitors
        // Risk: Wrap triggers at monitor seams (cursor teleports when moving between side-by-side monitors)
        TEST_METHOD(TwoSideBySide_CorrectOuterEdges)
        {
            MonitorTopology topo;
            std::vector<MonitorInfo> monitors = {
                MakeMonitor(0, 0, 0, 1920, 1080, true),
                MakeMonitor(1, 1920, 0, 3840, 1080),
            };
            topo.Initialize(monitors);

            // 4 edges per monitor = 8 total, minus 2 adjacent (Right of 0, Left of 1) = 6 outer
            Assert::AreEqual(6, static_cast<int>(topo.GetOuterEdges().size()),
                             L"Expected 6 outer edges for two side-by-side monitors");
        }

        // Product code: MonitorTopology.h — IdentifyOuterEdges(), EdgesAreAdjacent()
        // What: Confirms Mon0 has 3 outer edges (Left/Top/Bottom) and Mon1 has 3 (Right/Top/Bottom)
        // Why: Per-monitor outer-edge count verifies RIGHT of Mon0 and LEFT of Mon1 are correctly inner
        // Risk: Wrapping could fire at the wrong edge, sending cursor to unexpected monitor
        TEST_METHOD(TwoSideBySide_SharedEdgesAreInner)
        {
            MonitorTopology topo;
            std::vector<MonitorInfo> monitors = {
                MakeMonitor(0, 0, 0, 1920, 1080, true),
                MakeMonitor(1, 1920, 0, 3840, 1080),
            };
            topo.Initialize(monitors);

            // Mon0 should have 3 outer edges (Left, Top, Bottom) but NOT Right.
            Assert::AreEqual(3, CountOuterEdgesForMonitor(topo, 0),
                             L"Mon0 should have 3 outer edges");
            // Mon1 should have 3 outer edges (Right, Top, Bottom) but NOT Left.
            Assert::AreEqual(3, CountOuterEdgesForMonitor(topo, 1),
                             L"Mon1 should have 3 outer edges");
        }

        // ── Two stacked monitors ────────────────────────────────────────

        // Product code: MonitorTopology.h — Initialize(), IdentifyOuterEdges(), EdgesAreAdjacent()
        // What: [Mon0: 0,0–1920,1080] / [Mon1: 0,1080–1920,2160] — verifies 6 outer edges (Mon0-Bottom/Mon1-Top are inner)
        // Why: Validates vertical adjacency detection (complements horizontal test above)
        // Risk: Cursor wraps vertically between stacked monitors instead of crossing normally
        TEST_METHOD(TwoStacked_CorrectOuterEdges)
        {
            MonitorTopology topo;
            std::vector<MonitorInfo> monitors = {
                MakeMonitor(0, 0, 0, 1920, 1080, true),
                MakeMonitor(1, 0, 1080, 1920, 2160),
            };
            topo.Initialize(monitors);

            // Shared: Mon0 Bottom / Mon1 Top → 6 outer.
            Assert::AreEqual(6, static_cast<int>(topo.GetOuterEdges().size()),
                             L"Expected 6 outer edges for stacked monitors");
        }

        // ── L-shaped layout ─────────────────────────────────────────────

        // Product code: MonitorTopology.h — Initialize(), IdentifyOuterEdges(), EdgesAreAdjacent()
        // What: L-shaped 3-monitor layout — verifies 8 outer edges (12 total minus 4 adjacent)
        // Why: Non-rectangular layouts are common (laptop + 2 externals); adjacency must handle partial-edge overlap
        // Risk: Some outer edges misclassified, causing wrap to fail on non-rectangular multi-monitor setups
        TEST_METHOD(LShaped_CorrectOuterEdges)
        {
            MonitorTopology topo;
            std::vector<MonitorInfo> monitors = {
                MakeMonitor(0, 0, 1080, 1920, 2160),
                MakeMonitor(1, 0, 0, 1920, 1080, true),
                MakeMonitor(2, 1920, 1080, 3840, 2160),
            };
            topo.Initialize(monitors);

            // Mon1 Bottom / Mon0 Top are adjacent.
            // Mon0 Right / Mon2 Left are adjacent.
            // All other edges are outer.
            // Total: 12 - 4 = 8 outer edges.
            int outerCount = static_cast<int>(topo.GetOuterEdges().size());
            Assert::AreEqual(8, outerCount,
                             L"Expected 8 outer edges for L-shaped layout");
        }

        // ── Edge adjacency within tolerance ─────────────────────────────

        // Product code: MonitorTopology.h — EdgesAreAdjacent(tolerance=50)
        // What: Two monitors with 10px gap (within 50px tolerance) are still treated as adjacent → 6 outer edges
        // Why: Windows display settings often leave small gaps between monitors; tolerance prevents false outer edges
        // Risk: Small alignment gaps cause spurious wrapping at monitor seams
        TEST_METHOD(EdgeAdjacency_WithinTolerance)
        {
            MonitorTopology topo;
            // 10px gap between monitors (within 50px tolerance).
            std::vector<MonitorInfo> monitors = {
                MakeMonitor(0, 0, 0, 1920, 1080, true),
                MakeMonitor(1, 1930, 0, 3850, 1080), // 10px gap
            };
            topo.Initialize(monitors);

            // The gap is within tolerance → inner edge.  So 6 outer edges.
            Assert::AreEqual(6, static_cast<int>(topo.GetOuterEdges().size()),
                             L"Small gap within tolerance should still yield 6 outer edges");
        }

        // ── Edge adjacency beyond tolerance ─────────────────────────────

        // Product code: MonitorTopology.h — EdgesAreAdjacent(tolerance=50)
        // What: Two monitors with 100px gap (beyond 50px tolerance) are NOT adjacent → 8 outer edges (all independent)
        // Why: Truly separated monitors must each have full outer edges so wrapping works independently per monitor
        // Risk: Distant monitors incorrectly treated as adjacent, suppressing wrap on their shared sides
        TEST_METHOD(EdgeAdjacency_BeyondTolerance_NoMatch)
        {
            MonitorTopology topo;
            // 100px gap — beyond 50px tolerance.
            std::vector<MonitorInfo> monitors = {
                MakeMonitor(0, 0, 0, 1920, 1080, true),
                MakeMonitor(1, 2020, 0, 3940, 1080), // 100px gap
            };
            topo.Initialize(monitors);

            // No adjacency → each monitor has all 4 outer edges = 8 total.
            Assert::AreEqual(8, static_cast<int>(topo.GetOuterEdges().size()),
                             L"Large gap beyond tolerance → 8 outer edges");
        }

        // ── Wrap destination: horizontal preserves Y ────────────────────

        // Product code: MonitorTopology.h — IsOnOuterEdge(), GetWrapDestination()
        // What: Cursor at Mon0 left edge (0,540) wraps horizontally; Y coordinate (540) is preserved in destination
        // Why: Users expect horizontal wrap to keep vertical position — losing Y makes cursor appear to jump
        // Risk: Cursor teleports to wrong vertical position after horizontal wrap
        TEST_METHOD(WrapDestination_HorizontalWrapPreservesY)
        {
            MonitorTopology topo;
            std::vector<MonitorInfo> monitors = {
                MakeMonitor(0, 0, 0, 1920, 1080, true),
                MakeMonitor(1, 1920, 0, 3840, 1080),
            };
            topo.Initialize(monitors);

            // Cursor on the left outer edge of Mon0.
            POINT cursor = { 0, 540 };
            EdgeType edgeType{};
            bool isOuter = topo.IsOnOuterEdge(HandleForIndex(0), cursor, edgeType, WrapMode::Both);

            Assert::IsTrue(isOuter, L"Left edge of Mon0 should be detected as an outer edge");
            Assert::AreEqual(static_cast<int>(EdgeType::Left), static_cast<int>(edgeType),
                             L"Edge type should be Left for cursor at x=0");

            POINT dest = topo.GetWrapDestination(HandleForIndex(0), cursor, edgeType);
            Assert::AreEqual(static_cast<LONG>(540), dest.y,
                             L"Horizontal wrap should preserve Y coordinate");
        }

        // ── Wrap destination: vertical preserves X ──────────────────────

        // Product code: MonitorTopology.h — IsOnOuterEdge(), GetWrapDestination()
        // What: Cursor at Mon0 top edge (960,0) wraps vertically; X coordinate (960) is preserved in destination
        // Why: Vertical wrap must preserve horizontal position for a smooth user experience
        // Risk: Cursor teleports to wrong horizontal position after vertical wrap
        TEST_METHOD(WrapDestination_VerticalWrapPreservesX)
        {
            MonitorTopology topo;
            std::vector<MonitorInfo> monitors = {
                MakeMonitor(0, 0, 0, 1920, 1080, true),
                MakeMonitor(1, 0, 1080, 1920, 2160),
            };
            topo.Initialize(monitors);

            POINT cursor = { 960, 0 };
            EdgeType edgeType{};
            bool isOuter = topo.IsOnOuterEdge(HandleForIndex(0), cursor, edgeType, WrapMode::Both);

            Assert::IsTrue(isOuter, L"Top edge of Mon0 should be detected as an outer edge");
            Assert::AreEqual(static_cast<int>(EdgeType::Top), static_cast<int>(edgeType),
                             L"Edge type should be Top for cursor at y=0");

            POINT dest = topo.GetWrapDestination(HandleForIndex(0), cursor, edgeType);
            Assert::AreEqual(static_cast<LONG>(960), dest.x,
                             L"Vertical wrap should preserve X coordinate");
        }

        // ── WrapMode filtering: HorizontalOnly ─────────────────────────

        // Product code: MonitorTopology.h — IsOnOuterEdge() with WrapMode::HorizontalOnly
        // What: Top edge (vertical) is not detected as outer when WrapMode is HorizontalOnly
        // Why: Users can restrict wrap to horizontal-only; vertical edges must be filtered out
        // Risk: Cursor wraps vertically even when user configured horizontal-only mode
        TEST_METHOD(WrapMode_HorizontalOnly_IgnoresVerticalEdges)
        {
            MonitorTopology topo;
            std::vector<MonitorInfo> monitors = {
                MakeMonitor(0, 0, 0, 1920, 1080, true),
                MakeMonitor(1, 0, 1080, 1920, 2160),
            };
            topo.Initialize(monitors);

            // Cursor on top outer edge.
            POINT cursor = { 960, 0 };
            EdgeType edgeType{};
            bool isOuter = topo.IsOnOuterEdge(HandleForIndex(0), cursor, edgeType,
                                               WrapMode::HorizontalOnly);
            // With HorizontalOnly, a top edge should not be detected.
            Assert::IsFalse(isOuter,
                            L"HorizontalOnly mode should not detect top edge");
        }

        // ── WrapMode filtering: VerticalOnly ────────────────────────────

        // Product code: MonitorTopology.h — IsOnOuterEdge() with WrapMode::VerticalOnly
        // What: Left edge (horizontal) is not detected as outer when WrapMode is VerticalOnly
        // Why: Users can restrict wrap to vertical-only; horizontal edges must be filtered out
        // Risk: Cursor wraps horizontally even when user configured vertical-only mode
        TEST_METHOD(WrapMode_VerticalOnly_IgnoresHorizontalEdges)
        {
            MonitorTopology topo;
            std::vector<MonitorInfo> monitors = {
                MakeMonitor(0, 0, 0, 1920, 1080, true),
                MakeMonitor(1, 1920, 0, 3840, 1080),
            };
            topo.Initialize(monitors);

            // Cursor on left outer edge.
            POINT cursor = { 0, 540 };
            EdgeType edgeType{};
            bool isOuter = topo.IsOnOuterEdge(HandleForIndex(0), cursor, edgeType,
                                               WrapMode::VerticalOnly);
            Assert::IsFalse(isOuter,
                            L"VerticalOnly mode should not detect left edge");
        }

        // ── WrapMode filtering: Both ────────────────────────────────────

        // Product code: MonitorTopology.h — IsOnOuterEdge() with WrapMode::Both
        // What: Both left and top edges detected as outer when WrapMode is Both (default)
        // Why: Default mode must not filter any direction — ensures full bidirectional wrapping
        // Risk: Default wrap mode silently drops an axis, confusing users who expect both directions
        TEST_METHOD(WrapMode_Both_DetectsAllEdges)
        {
            MonitorTopology topo;
            std::vector<MonitorInfo> monitors = {
                MakeMonitor(0, 0, 0, 1920, 1080, true),
            };
            topo.Initialize(monitors);

            // Left edge.
            POINT leftPt = { 0, 540 };
            EdgeType edgeType{};
            bool leftOuter = topo.IsOnOuterEdge(HandleForIndex(0), leftPt, edgeType, WrapMode::Both);
            Assert::IsTrue(leftOuter, L"Both mode should detect left edge");

            // Top edge.
            POINT topPt = { 960, 0 };
            bool topOuter = topo.IsOnOuterEdge(HandleForIndex(0), topPt, edgeType, WrapMode::Both);
            Assert::IsTrue(topOuter, L"Both mode should detect top edge");
        }

        // ── Threshold: prevents rapid oscillation ───────────────────────

        // Product code: CursorWrapCore.h — WRAP_DISTANCE_THRESHOLD constant
        // What: Verifies the anti-oscillation threshold is exactly 50 pixels
        // Why: Too small → cursor ping-pongs between edges; too large → legitimate wraps are suppressed
        // Risk: Threshold drift causes either jitter (oscillation) or dead zones near edges
        TEST_METHOD(Threshold_ConstantIs50Pixels)
        {
            Assert::AreEqual(50, WRAP_DISTANCE_THRESHOLD,
                             L"WRAP_DISTANCE_THRESHOLD should be 50px");
        }

        // ── Direction tracking ──────────────────────────────────────────

        // Product code: CursorWrapCore.h — CursorDirection struct (IsMovingLeft/Right/Up/Down, IsPrimarilyHorizontal)
        // What: dx=-5,dy=2 → left, down, primarily horizontal (|dx|>=|dy|)
        // Why: Edge priority at corners depends on movement direction; wrong classification picks wrong edge
        // Risk: Cursor wraps to the wrong monitor at corner junctions
        TEST_METHOD(CursorDirection_DxDyTracking)
        {
            CursorDirection dir{};
            dir.dx = -5;
            dir.dy = 2;

            Assert::IsTrue(dir.IsMovingLeft(),
                           L"Negative dx should mean moving left");
            Assert::IsFalse(dir.IsMovingRight());
            Assert::IsFalse(dir.IsMovingUp());
            Assert::IsTrue(dir.IsMovingDown(),
                           L"Positive dy should mean moving down");
            Assert::IsTrue(dir.IsPrimarilyHorizontal(),
                           L"|dx| >= |dy| → primarily horizontal");
        }

        // Product code: CursorWrapCore.h — CursorDirection::IsPrimarilyHorizontal()
        // What: dx=1,dy=-10 → primarily vertical (|dx|<|dy|), moving up
        // Why: Complements the horizontal case; ensures vertical dominance is correctly detected
        // Risk: Vertical movement misclassified as horizontal → wrong edge selected at corners
        TEST_METHOD(CursorDirection_PrimarilyVertical)
        {
            CursorDirection dir{};
            dir.dx = 1;
            dir.dy = -10;

            Assert::IsFalse(dir.IsPrimarilyHorizontal(),
                            L"|dx| < |dy| → primarily vertical");
            Assert::IsTrue(dir.IsMovingUp());
        }
    };
}
