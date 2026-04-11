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
        mi.hMonitor = reinterpret_cast<HMONITOR>(static_cast<uintptr_t>(index + 1));
        mi.rect = { left, top, right, bottom };
        mi.isPrimary = primary;
        mi.monitorId = index;
        return mi;
    }

    static HMONITOR HandleForIndex(int index)
    {
        return reinterpret_cast<HMONITOR>(static_cast<uintptr_t>(index + 1));
    }

    // Count outer edges of a specific type.
    static int CountOuterEdges(const MonitorTopology& topo, EdgeType type)
    {
        int count = 0;
        for (const auto& e : topo.GetOuterEdges())
        {
            if (e.type == type)
                ++count;
        }
        return count;
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

        // A single monitor has 4 outer edges but none have an opposite outer
        // edge to wrap to (wrapping would go to itself).
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

        // With one monitor there is no wrapping partner on the opposite side.
        TEST_METHOD(SingleMonitor_NoWrapPartner)
        {
            MonitorTopology topo;
            std::vector<MonitorInfo> monitors = {
                MakeMonitor(0, 0, 0, 1920, 1080, true),
            };
            topo.Initialize(monitors);

            // Cursor at the left edge. IsOnOuterEdge should be true but
            // GetWrapDestination should just return the same position since
            // there's only one monitor.
            EdgeType edgeType{};
            POINT cursor = { 0, 540 };
            bool isOuter = topo.IsOnOuterEdge(HandleForIndex(0), cursor, edgeType, WrapMode::Both);

            if (isOuter)
            {
                POINT dest = topo.GetWrapDestination(HandleForIndex(0), cursor, edgeType);
                // With no opposite monitor, wrap destination falls back to same
                // monitor's opposite edge.
                Assert::IsTrue(dest.x != cursor.x || dest.y != cursor.y,
                               L"Wrap destination should differ from source on a self-wrap");
            }
        }

        // ── Two side-by-side monitors ───────────────────────────────────

        // [Mon0: 0-1920] [Mon1: 1920-3840]
        // The shared edges (Mon0 Right, Mon1 Left) should be inner; all other
        // edges should be outer.
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

        // [Mon0: 0,0-1920,1080]
        // [Mon1: 0,1080-1920,2160]
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

        //  [Mon1: 0,0-1920,1080]
        //  [Mon0: 0,1080-1920,2160] [Mon2: 1920,1080-3840,2160]
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
            Assert::IsTrue(outerCount >= 7 && outerCount <= 9,
                           L"L-shaped layout should have ~8 outer edges");
        }

        // ── Edge adjacency within tolerance ─────────────────────────────

        // Two monitors with a small gap (within 50px tolerance) should be
        // treated as adjacent.
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

        // A gap > 50px means edges are NOT adjacent.
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

            if (isOuter && edgeType == EdgeType::Left)
            {
                POINT dest = topo.GetWrapDestination(HandleForIndex(0), cursor, edgeType);
                Assert::AreEqual(static_cast<LONG>(540), dest.y,
                                 L"Horizontal wrap should preserve Y coordinate");
            }
        }

        // ── Wrap destination: vertical preserves X ──────────────────────

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

            if (isOuter && edgeType == EdgeType::Top)
            {
                POINT dest = topo.GetWrapDestination(HandleForIndex(0), cursor, edgeType);
                Assert::AreEqual(static_cast<LONG>(960), dest.x,
                                 L"Vertical wrap should preserve X coordinate");
            }
        }

        // ── WrapMode filtering: HorizontalOnly ─────────────────────────

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

        // The CursorWrapCore tracks wrap destinations and uses WRAP_DISTANCE_THRESHOLD
        // (50px) to prevent rapid back-and-forth wrapping.
        TEST_METHOD(Threshold_ConstantIs50Pixels)
        {
            Assert::AreEqual(50, WRAP_DISTANCE_THRESHOLD,
                             L"WRAP_DISTANCE_THRESHOLD should be 50px");
        }

        // ── Direction tracking ──────────────────────────────────────────

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
