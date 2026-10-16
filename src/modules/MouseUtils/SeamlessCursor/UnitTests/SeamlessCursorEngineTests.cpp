#pragma warning(push)
#pragma warning(disable : 26466)
#include "CppUnitTest.h"
#pragma warning(pop)

#include "..\SeamlessCursorEngine.h"

#include <array>
#include <utility>

using namespace Microsoft::VisualStudio::CppUnitTestFramework;
using namespace seamless_cursor;

namespace
{
    // Bottom monitor A, 4000 px wide. Monitor B above it is 2000 px wide but physically as wide as A:
    // B's pixels are twice as big, and B is centered over A.
    const RECT wideBottom{ 0, 0, 4000, 2000 };
    const RECT narrowTop{ 1000, -1000, 3000, 0 };

    BorderCalibration TopCalibration()
    {
        // A: 1000 and 3000; B: 1500 and 2500. Scale 0.5.
        return { L"A", L"B", Edge::Top, true, { MarkerPair{ 0.25, 0.25 }, MarkerPair{ 0.75, 0.75 } } };
    }

    Engine BuildEngine(const std::vector<Monitor>& monitors, const std::vector<BorderCalibration>& calibrations)
    {
        Engine engine;
        engine.Build(monitors, calibrations);
        return engine;
    }

    std::optional<POINT> Resolve(Engine& engine, POINT cursor, POINT requested)
    {
        const auto remap = engine.Resolve(cursor, requested);
        return remap ? std::optional<POINT>{ remap->point } : std::nullopt;
    }

    void AssertPoint(LONG x, LONG y, const std::optional<POINT>& actual)
    {
        Assert::IsTrue(actual.has_value(), L"Expected a jump");
        Assert::AreEqual(x, actual->x, L"x");
        Assert::AreEqual(y, actual->y, L"y");
    }
}

namespace SeamlessCursorUnitTests
{
    TEST_CLASS (SharedBorderTests)
    {
    public:
        TEST_METHOD (FindsEveryTouchingPairOnce)
        {
            // Layout like the Windows display settings screenshot: 2 and 1 sit on top of 3.
            const std::vector<RECT> monitors{
                { 0, 0, 3840, 2160 }, // 3
                { 500, -1080, 2420, 0 }, // 2
                { 3000, -800, 3840, 0 }, // 1
            };

            const auto borders = FindSharedBorders(monitors);

            Assert::AreEqual(size_t{ 2 }, borders.size());
            Assert::IsTrue(borders[0].monitorA == 0 && borders[0].monitorB == 1 && borders[0].edgeOfA == Edge::Top);
            Assert::AreEqual(500, borders[0].start);
            Assert::AreEqual(2420, borders[0].end);
            Assert::IsTrue(borders[1].monitorA == 0 && borders[1].monitorB == 2 && borders[1].edgeOfA == Edge::Top);
            Assert::AreEqual(3000, borders[1].start);
            Assert::AreEqual(3840, borders[1].end);
        }

        TEST_METHOD (IgnoresCornerContactAndMirroredMonitors)
        {
            const std::vector<RECT> monitors{
                { 0, 0, 100, 100 },
                { 100, 100, 200, 200 }, // Touches only at a corner.
                { 0, 0, 100, 100 }, // Mirror of the first.
            };

            Assert::AreEqual(size_t{ 0 }, FindSharedBorders(monitors).size());
        }

        TEST_METHOD (FindsGlueCandidateAcrossGap)
        {
            // Real layout: BenQ and Dell both stand on the Odyssey, with a gap between them that Windows cannot close.
            const std::vector<RECT> monitors{
                { 0, 0, 7680, 2160 }, // Odyssey
                { 1204, -2160, 5044, 0 }, // BenQ
                { 5858, -1440, 8418, 0 }, // Dell
            };

            const auto candidates = FindGlueCandidates(monitors);

            Assert::AreEqual(size_t{ 1 }, candidates.size());
            Assert::IsTrue(candidates[0].monitorA == 1 && candidates[0].monitorB == 2 && candidates[0].edgeOfA == Edge::Right);
            Assert::AreEqual(-1440, candidates[0].start);
            Assert::AreEqual(0, candidates[0].end);
        }

        TEST_METHOD (FindsGlueCandidatesOnEveryEdge)
        {
            const RECT a{ 0, 0, 1000, 1000 };
            const std::array<std::pair<RECT, Edge>, 4> others{ {
                { { -1500, 0, -500, 1000 }, Edge::Left },
                { { 1500, 0, 2500, 1000 }, Edge::Right },
                { { 0, -1500, 1000, -500 }, Edge::Top },
                { { 0, 1500, 1000, 2500 }, Edge::Bottom },
            } };

            for (const auto& [other, edge] : others)
            {
                const auto candidates = FindGlueCandidates({ a, other });

                Assert::AreEqual(size_t{ 1 }, candidates.size());
                Assert::IsTrue(candidates[0].monitorA == 0 && candidates[0].monitorB == 1 && candidates[0].edgeOfA == edge);
                Assert::AreEqual(0, candidates[0].start);
                Assert::AreEqual(1000, candidates[0].end);
            }
        }

        TEST_METHOD (NoGlueCandidateAcrossAnotherMonitor)
        {
            // A row of three: the outer two face each other, but the middle one is in between.
            const std::vector<RECT> monitors{ { 0, 0, 100, 100 }, { 100, 0, 200, 100 }, { 200, 0, 300, 100 } };

            Assert::AreEqual(size_t{ 0 }, FindGlueCandidates(monitors).size());
        }

        TEST_METHOD (NoGlueCandidateWithoutOverlap)
        {
            // Diagonal neighbors do not face each other along any edge.
            const std::vector<RECT> monitors{ { 0, 0, 100, 100 }, { 200, 200, 300, 300 } };

            Assert::AreEqual(size_t{ 0 }, FindGlueCandidates(monitors).size());
        }

        TEST_METHOD (FindsSideBySideBorder)
        {
            const std::vector<RECT> monitors{ { 0, 0, 1920, 1080 }, { 1920, -200, 4480, 1240 } };

            const auto borders = FindSharedBorders(monitors);

            Assert::AreEqual(size_t{ 1 }, borders.size());
            Assert::IsTrue(borders[0].edgeOfA == Edge::Right);
            Assert::AreEqual(0, borders[0].start);
            Assert::AreEqual(1080, borders[0].end);
        }
    };

    TEST_CLASS (ValidationTests)
    {
    public:
        TEST_METHOD (AcceptsWellSpreadMarkers)
        {
            Assert::IsTrue(Validate(TopCalibration(), wideBottom, narrowTop) == CalibrationIssue::None);
        }

        TEST_METHOD (ReportsReversedOrder)
        {
            auto calibration = TopCalibration();
            calibration.pairs[1].b = 0.1;

            Assert::IsTrue(Validate(calibration, wideBottom, narrowTop) == CalibrationIssue::OrderReversed);
        }

        TEST_METHOD (ReportsMarkersAtSameSpot)
        {
            auto calibration = TopCalibration();
            calibration.pairs[1].a = calibration.pairs[0].a;

            Assert::IsTrue(Validate(calibration, wideBottom, narrowTop) == CalibrationIssue::InvalidMarkers);
        }

        TEST_METHOD (ReportsMarkersOutsideEdge)
        {
            auto calibration = TopCalibration();
            calibration.pairs[0].b = -0.1;

            Assert::IsTrue(Validate(calibration, wideBottom, narrowTop) == CalibrationIssue::InvalidMarkers);
        }

        TEST_METHOD (ReportsImplausibleScale)
        {
            auto calibration = TopCalibration();
            calibration.pairs[0] = { 0.0, 0.5 };
            calibration.pairs[1] = { 1.0, 0.52 }; // 4000 A pixels against 40 B pixels.

            Assert::IsTrue(Validate(calibration, wideBottom, narrowTop) == CalibrationIssue::ScaleOutOfRange);
        }

        TEST_METHOD (CloseMarkersAreUsableWithWarning)
        {
            // 400 A pixels apart, while both monitors share 2000 A pixels physically.
            auto calibration = TopCalibration();
            calibration.pairs[1] = { 0.35, 0.45 };

            const auto issue = Validate(calibration, wideBottom, narrowTop);

            Assert::IsTrue(issue == CalibrationIssue::MarkersTooClose);
            Assert::IsTrue(IsUsable(issue));
        }

        TEST_METHOD (SeparationIsJudgedAgainstPhysicallySharedSpan)
        {
            // A very wide monitor under a narrow one: the markers span little of A's edge but most of the shared part.
            const RECT ultrawide{ 0, 0, 7680, 2160 };
            const RECT corner{ 5860, -1440, 8420, 0 };
            const BorderCalibration calibration{ L"A", L"B", Edge::Top, true, { MarkerPair{ 0.80, 0.10 }, MarkerPair{ 0.95, 0.50 } } };

            Assert::IsTrue(Validate(calibration, ultrawide, corner) == CalibrationIssue::None);
        }

        TEST_METHOD (MapRoundTripsThroughInverse)
        {
            const auto map = MakeBorderMap(TopCalibration(), wideBottom, narrowTop);

            Assert::AreEqual(0.5, map.scale, 1e-9);
            Assert::AreEqual(2000.0, map.ToB(2000.0), 1e-9);
            Assert::AreEqual(1234.5, map.Inverse().ToB(map.ToB(1234.5)), 1e-9);
        }

        TEST_METHOD (MarkerFractionRoundTrips)
        {
            Assert::AreEqual(0.3, MarkerFraction(narrowTop, Edge::Bottom, MarkerPosition(narrowTop, Edge::Bottom, 0.3)), 1e-12);
            Assert::AreEqual(1600.0, MarkerPosition(narrowTop, Edge::Bottom, 0.3), 1e-9);
        }
    };

    TEST_CLASS (EngineTests)
    {
    public:
        TEST_METHOD (MoveInsideMonitorIsNotRemapped)
        {
            auto engine = BuildEngine({ { L"A", wideBottom }, { L"B", narrowTop } }, { TopCalibration() });

            Assert::IsFalse(Resolve(engine, { 2000, 1000 }, { 2010, 990 }).has_value());
        }

        TEST_METHOD (CrossingUpMapsPhysicalPosition)
        {
            auto engine = BuildEngine({ { L"A", wideBottom }, { L"B", narrowTop } }, { TopCalibration() });

            // A pixel 3000 is B pixel 2500; Windows alone would keep x = 3000.
            AssertPoint(2500, -1, Resolve(engine, { 3000, 0 }, { 3000, -1 }));
        }

        TEST_METHOD (CrossingDownMapsBack)
        {
            auto engine = BuildEngine({ { L"A", wideBottom }, { L"B", narrowTop } }, { TopCalibration() });

            // One B pixel past the edge is two A pixels.
            AssertPoint(3001, 1, Resolve(engine, { 2500, -1 }, { 2500, 0 }));
        }

        TEST_METHOD (OvershootIsScaledToTargetPixels)
        {
            auto engine = BuildEngine({ { L"A", wideBottom }, { L"B", narrowTop } }, { TopCalibration() });

            // 10 A pixels past the edge are 5 B pixels.
            AssertPoint(2000, -5, Resolve(engine, { 2000, 0 }, { 2000, -10 }));
        }

        TEST_METHOD (OuterEdgeIsTakenOverWhereNeighborIsPhysicallyAdjacent)
        {
            auto engine = BuildEngine({ { L"A", wideBottom }, { L"B", narrowTop } }, { TopCalibration() });

            // x = 100 is an outer edge for Windows, but B is physically above it.
            AssertPoint(1050, -1, Resolve(engine, { 100, 0 }, { 100, -1 }));
        }

        TEST_METHOD (OffTargetCrossingStopsAtEdge)
        {
            // Same pixel size, but B is physically only as wide as A's middle half.
            const RECT a{ 0, 0, 2000, 1000 };
            const RECT b{ 0, -1000, 2000, 0 };
            const BorderCalibration calibration{ L"A", L"B", Edge::Top, true, { MarkerPair{ 0.25, 0.0 }, MarkerPair{ 0.75, 1.0 } } };
            auto engine = BuildEngine({ { L"A", a }, { L"B", b } }, { calibration });

            // Windows would move these into B, but nothing is physically above: the cursor stays on A.
            AssertPoint(100, 0, Resolve(engine, { 100, 0 }, { 100, -1 }));
            AssertPoint(1900, 0, Resolve(engine, { 1900, 0 }, { 1900, -1 }));

            // Along the edge, the cursor keeps sliding.
            AssertPoint(105, 0, Resolve(engine, { 100, 0 }, { 105, -3 }));

            // Where B is physically above, the cursor crosses.
            AssertPoint(1001, -2, Resolve(engine, { 1000, 0 }, { 1000, -1 }));

            // Only crossings are claimed from CursorWrap; it never wraps at inner edges anyway.
            Assert::IsFalse(engine.IsEdgeClaimed(a, Edge::Top, 100));
            Assert::IsTrue(engine.IsEdgeClaimed(a, Edge::Top, 1000));
        }

        TEST_METHOD (WithoutStoppingOffTargetCrossingGoesToNearestCorner)
        {
            const RECT a{ 0, 0, 2000, 1000 };
            const RECT b{ 0, -1000, 2000, 0 };
            const BorderCalibration calibration{ L"A", L"B", Edge::Top, true, { MarkerPair{ 0.25, 0.0 }, MarkerPair{ 0.75, 1.0 } } };
            Engine engine;
            engine.Build({ { L"A", a }, { L"B", b } }, { calibration }, EngineOptions{ false });

            // Windows connects A's top edge to B, but B is physically only over A's middle half.
            AssertPoint(0, -2, Resolve(engine, { 100, 0 }, { 100, -1 }));
            AssertPoint(1999, -2, Resolve(engine, { 1900, 0 }, { 1900, -1 }));
            AssertPoint(1001, -2, Resolve(engine, { 1000, 0 }, { 1000, -1 }));
        }

        TEST_METHOD (CornerFollowsEdgeCrossedFirst)
        {
            // B above and C to the right both physically cover A's edges, so the order decides the target.
            const RECT a{ 0, 0, 1000, 1000 };
            const RECT b{ 0, -1000, 1000, 0 };
            const RECT c{ 1000, 0, 2000, 1000 };
            const BorderCalibration top{ L"A", L"B", Edge::Top, true, { MarkerPair{ 0.2, 0.2 }, MarkerPair{ 0.8, 0.8 } } };
            const BorderCalibration right{ L"A", L"C", Edge::Right, true, { MarkerPair{ 0.2, 0.2 }, MarkerPair{ 0.8, 0.8 } } };
            auto engine = BuildEngine({ { L"A", a }, { L"B", b }, { L"C", c } }, { top, right });

            // The top is crossed at a quarter of the move, the right edge at three quarters.
            AssertPoint(999, -2, Resolve(engine, { 995, 0 }, { 1001, -2 }));
        }

        TEST_METHOD (CornerIntoUncalibratedWindowsNeighbor)
        {
            // A's top is a wall except [200, 700); C to the right is connected by Windows but not calibrated. B spans
            // above A and C, so a move through A's top-right corner can ask for a point in B, far from the wall.
            const RECT a{ 0, 0, 1000, 1000 };
            const RECT b{ 0, -1000, 2000, 0 };
            const RECT c{ 1000, 0, 2000, 1000 };
            const BorderCalibration top{ L"A", L"B", Edge::Top, true, { MarkerPair{ 0.2, 0.0 }, MarkerPair{ 0.7, 1.0 } } };
            auto engine = BuildEngine({ { L"A", a }, { L"B", b }, { L"C", c } }, { top });

            // Top wall first, then into C: the wall does not hold the cursor in the corner, and it goes straight across
            // A's right edge into C, not into B. The wall is an inner one, so other cursor tools do not act first.
            const auto intoC = engine.Resolve({ 998, 0 }, { 1001, -3 });
            Assert::IsTrue(intoC.has_value() && !intoC->outerWall);
            AssertPoint(1001, 0, intoC->point);

            // Into C first: from there on, the move is Windows' business, as C and B are not calibrated.
            Assert::IsFalse(Resolve(engine, { 999, 5 }, { 1003, -1 }).has_value());

            // Across the right edge alone, Windows handles the move.
            Assert::IsFalse(Resolve(engine, { 999, 5 }, { 1003, 3 }).has_value());

            // Where C reaches past the corner, the requested point lies in C itself, so Windows handles the move.
            const RECT narrowB{ 0, -1000, 1000, 0 };
            const RECT tallC{ 1000, -500, 2000, 500 };
            auto tall = BuildEngine({ { L"A", a }, { L"B", narrowB }, { L"C", tallC } }, { top });
            Assert::IsFalse(Resolve(tall, { 998, 0 }, { 1001, -3 }).has_value());
        }

        TEST_METHOD (OuterWallOnSecondEdgeLetsOtherToolsActFirst)
        {
            // A's top is an inner wall at x < 500. On A's left, D is physically beside y >= 500 only; above that,
            // nothing is across in the calibration or in Windows, so it is an outer wall.
            const RECT a{ 0, 0, 2000, 1000 };
            const RECT b{ 0, -1000, 2000, 0 };
            const RECT d{ -1000, 500, 0, 1500 };
            const BorderCalibration top{ L"A", L"B", Edge::Top, true, { MarkerPair{ 0.25, 0.0 }, MarkerPair{ 0.75, 1.0 } } };
            const BorderCalibration left{ L"A", L"D", Edge::Left, true, { MarkerPair{ 0.6, 0.1 }, MarkerPair{ 0.9, 0.4 } } };
            auto engine = BuildEngine({ { L"A", a }, { L"B", b }, { L"D", d } }, { top, left });

            const auto remap = engine.Resolve({ 5, 3 }, { -1, -1 });

            Assert::IsTrue(remap.has_value() && remap->outerWall);
            Assert::AreEqual(0L, remap->point.x);
            Assert::AreEqual(0L, remap->point.y);
        }

        TEST_METHOD (CornerRemapStaysInConnectedMonitor)
        {
            // As in CornerIntoUncalibratedWindowsNeighbor, but a long flick: past the corner and far beyond C's right edge.
            const RECT a{ 0, 0, 1000, 1000 };
            const RECT b{ 0, -1000, 2000, 0 };
            const RECT c{ 1000, 0, 2000, 1000 };
            const BorderCalibration top{ L"A", L"B", Edge::Top, true, { MarkerPair{ 0.2, 0.0 }, MarkerPair{ 0.7, 1.0 } } };
            auto engine = BuildEngine({ { L"A", a }, { L"B", b }, { L"C", c } }, { top });

            AssertPoint(1999, 0, Resolve(engine, { 998, 0 }, { 2100, -400 }));
        }

        TEST_METHOD (MapJumpPrefersFewestBorders)
        {
            // A on the left, D on the right. Above, B links them through two borders (D y = A y + 100); below, C and F
            // through three (D y = A y - 100). The long chain is calibrated first.
            const RECT a{ 0, 0, 2000, 2000 };
            const RECT b{ 2000, 0, 4000, 1000 };
            const RECT c{ 2000, 1000, 3000, 2000 };
            const RECT f{ 3000, 1000, 4000, 2000 };
            const RECT d{ 4000, 0, 6000, 2000 };
            const BorderCalibration ac{ L"A", L"C", Edge::Right, true, { MarkerPair{ 0.6, 0.2 }, MarkerPair{ 0.9, 0.8 } } };
            const BorderCalibration cf{ L"C", L"F", Edge::Right, true, { MarkerPair{ 0.2, 0.2 }, MarkerPair{ 0.8, 0.8 } } };
            const BorderCalibration fd{ L"F", L"D", Edge::Right, true, { MarkerPair{ 0.2, 0.55 }, MarkerPair{ 0.8, 0.85 } } };
            const BorderCalibration ab{ L"A", L"B", Edge::Right, true, { MarkerPair{ 0.1, 0.2 }, MarkerPair{ 0.4, 0.8 } } };
            const BorderCalibration bd{ L"B", L"D", Edge::Right, true, { MarkerPair{ 0.2, 0.15 }, MarkerPair{ 0.8, 0.45 } } };
            auto engine = BuildEngine({ { L"A", a }, { L"B", b }, { L"C", c }, { L"F", f }, { L"D", d } }, { ac, cf, fd, ab, bd });

            Assert::AreEqual(600, engine.MapJump(a, Edge::Left, 500, d).value_or(-1));
        }

        TEST_METHOD (CornerFromOuterWallIntoUncalibratedNeighbor)
        {
            // B is calibrated above A's left half only, so A's top is an outer wall from x 1000. C to the right is
            // connected by Windows but not calibrated.
            const RECT a{ 0, 0, 2000, 1000 };
            const RECT b{ 0, -1000, 1000, 0 };
            const RECT c{ 2000, 0, 3000, 1000 };
            const BorderCalibration top{ L"A", L"B", Edge::Top, true, { MarkerPair{ 0.1, 0.2 }, MarkerPair{ 0.4, 0.8 } } };
            auto engine = BuildEngine({ { L"A", a }, { L"B", b }, { L"C", c } }, { top });

            // Up and to the right through the corner: straight across into C, but other cursor tools act first, as at
            // the outer wall itself.
            const auto remap = engine.Resolve({ 1998, 0 }, { 2001, -3 });

            Assert::IsTrue(remap.has_value() && remap->outerWall);
            Assert::AreEqual(2001L, remap->point.x);
            Assert::AreEqual(0L, remap->point.y);
        }

        TEST_METHOD (CornerWallBesideOuterEdgeLetsOtherToolsActFirst)
        {
            // A's top is an inner wall at x < 500; A's left is an outer desktop edge without any calibrated border.
            const RECT a{ 0, 0, 2000, 1000 };
            const RECT b{ 0, -1000, 2000, 0 };
            const BorderCalibration calibration{ L"A", L"B", Edge::Top, true, { MarkerPair{ 0.25, 0.0 }, MarkerPair{ 0.75, 1.0 } } };
            auto engine = BuildEngine({ { L"A", a }, { L"B", b } }, { calibration });

            // Up and to the left through the corner: the top wall stops the cursor, but the move also leaves the
            // desktop across the left edge, where CursorWrap may wrap it.
            const auto remap = engine.Resolve({ 1, 0 }, { -1, -1 });

            Assert::IsTrue(remap.has_value() && remap->outerWall);
            Assert::AreEqual(0L, remap->point.x);
            Assert::AreEqual(0L, remap->point.y);

            // Straight up, it is an inner wall only.
            const auto inner = engine.Resolve({ 100, 0 }, { 100, -1 });

            Assert::IsTrue(inner.has_value() && !inner->outerWall);
        }

        TEST_METHOD (MapJumpFollowsSideBorder)
        {
            // B has twice the pixel density of A: A y 200 and 800 line up with B y 200 and 1400.
            const RECT a{ 0, 0, 1000, 1000 };
            const RECT b{ 1000, 0, 3000, 2000 };
            const BorderCalibration right{ L"A", L"B", Edge::Right, true, { MarkerPair{ 0.2, 0.1 }, MarkerPair{ 0.8, 0.7 } } };
            auto engine = BuildEngine({ { L"A", a }, { L"B", b } }, { right });

            // Wrapping out of A's left edge into B, and back out of B's right edge into A.
            Assert::AreEqual(801, engine.MapJump(a, Edge::Left, 500, b).value_or(-1));
            Assert::AreEqual(500, engine.MapJump(b, Edge::Right, 801, a).value_or(-1));

            // Within one monitor nothing changes.
            Assert::AreEqual(500, engine.MapJump(a, Edge::Left, 500, a).value_or(-1));
        }

        TEST_METHOD (MapJumpChainsSideBordersOnly)
        {
            // A, B and C side by side. The same height is 100 px further down on B than on A (A y 200 lines up with
            // B y 300); C lines up with B.
            const RECT a{ 0, 0, 1000, 1000 };
            const RECT b{ 1000, 0, 2000, 1000 };
            const RECT c{ 2000, 0, 3000, 1000 };
            const BorderCalibration ab{ L"A", L"B", Edge::Right, true, { MarkerPair{ 0.2, 0.3 }, MarkerPair{ 0.8, 0.9 } } };
            const BorderCalibration bc{ L"B", L"C", Edge::Right, true, { MarkerPair{ 0.2, 0.2 }, MarkerPair{ 0.8, 0.8 } } };
            auto engine = BuildEngine({ { L"A", a }, { L"B", b }, { L"C", c } }, { ab, bc });

            Assert::AreEqual(600, engine.MapJump(a, Edge::Left, 500, c).value_or(-1));
            Assert::AreEqual(500, engine.MapJump(c, Edge::Right, 600, a).value_or(-1));

            // A vertical wrap maps x, which these borders do not tell.
            Assert::IsFalse(engine.MapJump(a, Edge::Top, 500, c).has_value());
        }

        TEST_METHOD (MapJumpAcrossTopBorderMapsXOnly)
        {
            // B above A is calibrated over A's middle half: B x = 2 * (A x - 250).
            // C to the right of A is connected by Windows but not calibrated.
            const RECT a{ 0, 0, 1000, 1000 };
            const RECT b{ 0, -1000, 1000, 0 };
            const RECT c{ 1000, 0, 2000, 1000 };
            const BorderCalibration top{ L"A", L"B", Edge::Top, true, { MarkerPair{ 0.25, 0.0 }, MarkerPair{ 0.75, 1.0 } } };
            auto engine = BuildEngine({ { L"A", a }, { L"B", b }, { L"C", c } }, { top });

            Assert::AreEqual(501, engine.MapJump(a, Edge::Bottom, 500, b).value_or(-1));

            // The gap between the screens is unknown, so the height cannot be mapped.
            Assert::IsFalse(engine.MapJump(a, Edge::Left, 500, b).has_value());

            // Neither a monitor without a calibrated border nor an unknown one is mapped.
            Assert::IsFalse(engine.MapJump(a, Edge::Left, 500, c).has_value());
            const RECT unknown{ 5000, 0, 6000, 1000 };
            Assert::IsFalse(engine.MapJump(a, Edge::Bottom, 500, unknown).has_value());
        }

        TEST_METHOD (OverlappingNeighborsSplitByDistanceToTheirCenters)
        {
            // B and C stand side by side on A, and the calibration says both are physically over A's [400, 600). The
            // edge is cut where the Windows layout changes (at 500), and each part goes to the neighbor whose mapped
            // middle of the part lies nearer that neighbor's center: [400, 500) to B, [500, 600) to C.
            const RECT a{ 0, 0, 1000, 1000 };
            const RECT b{ 0, -300, 500, 0 };
            const RECT c{ 500, -300, 1000, 0 };
            const BorderCalibration ab{ L"A", L"B", Edge::Top, true, { MarkerPair{ 0.2, 1.0 / 3 }, MarkerPair{ 0.5, 5.0 / 6 } } };
            const BorderCalibration ac{ L"A", L"C", Edge::Top, true, { MarkerPair{ 0.5, 1.0 / 6 }, MarkerPair{ 0.8, 2.0 / 3 } } };
            auto engine = BuildEngine({ { L"A", a }, { L"B", b }, { L"C", c } }, { ab, ac });

            AssertPoint(375, -1, Resolve(engine, { 450, 0 }, { 450, -1 }));
            AssertPoint(625, -1, Resolve(engine, { 550, 0 }, { 550, -1 }));
        }

        TEST_METHOD (FirstUsableRecordPerPairWins)
        {
            // An unusable record, then a usable one stored the other way around (B above A, from B's side: A x = B x +
            // 100), then a record that would shift B by 200 px. Used the wrong way round, the reversed record would give
            // 600.
            const RECT a{ 0, 0, 1000, 1000 };
            const RECT b{ 0, -1000, 1000, 0 };
            const BorderCalibration unusable{ L"A", L"B", Edge::Top, true, { MarkerPair{ 0.5, 0.5 }, MarkerPair{ 0.5, 0.5 } } };
            const BorderCalibration reversed{ L"B", L"A", Edge::Bottom, true, { MarkerPair{ 0.2, 0.3 }, MarkerPair{ 0.8, 0.9 } } };
            const BorderCalibration shifted{ L"A", L"B", Edge::Top, true, { MarkerPair{ 0.2, 0.4 }, MarkerPair{ 0.6, 0.8 } } };
            auto engine = BuildEngine({ { L"A", a }, { L"B", b } }, { unusable, reversed, shifted });

            AssertPoint(400, -1, Resolve(engine, { 500, 0 }, { 500, -1 }));
        }

        TEST_METHOD (FastMoveAcrossThinMonitorStaysInIt)
        {
            const RECT a{ 0, 0, 1000, 1000 };
            const RECT thin{ 0, -10, 1000, 0 };
            const BorderCalibration top{ L"A", L"B", Edge::Top, true, { MarkerPair{ 0.2, 0.2 }, MarkerPair{ 0.8, 0.8 } } };
            auto engine = BuildEngine({ { L"A", a }, { L"B", thin } }, { top });

            // 50 px past A's top, but B is only 10 px tall: its top row.
            AssertPoint(500, -10, Resolve(engine, { 500, 0 }, { 500, -50 }));
        }

        TEST_METHOD (MapJumpChainsThroughGluedPair)
        {
            // A, then B glued across a gap (B y = A y - 100), then C touching B with twice its pixel density (C y = 2 B y).
            const RECT a{ 0, 0, 1000, 1000 };
            const RECT b{ 1500, 0, 2500, 1000 };
            const RECT c{ 2500, 0, 4500, 2000 };
            const BorderCalibration glued{ L"A", L"B", Edge::Right, true, { MarkerPair{ 0.2, 0.1 }, MarkerPair{ 0.7, 0.6 } } };
            const BorderCalibration bc{ L"B", L"C", Edge::Right, true, { MarkerPair{ 0.2, 0.2 }, MarkerPair{ 0.8, 0.8 } } };
            auto engine = BuildEngine({ { L"A", a }, { L"B", b }, { L"C", c } }, { glued, bc });

            Assert::AreEqual(801, engine.MapJump(a, Edge::Left, 500, c).value_or(-1));
            Assert::AreEqual(500, engine.MapJump(c, Edge::Right, 801, a).value_or(-1));
        }

        TEST_METHOD (CrossingIsMeasuredThroughPixelCenters)
        {
            // Wall on A's top up to x 500, B from there. The move crosses the edge at x 500.25.
            const RECT a{ 0, 0, 2000, 1000 };
            const RECT b{ 0, -1000, 2000, 0 };
            const BorderCalibration calibration{ L"A", L"B", Edge::Top, true, { MarkerPair{ 0.25, 0.0 }, MarkerPair{ 0.75, 1.0 } } };
            auto engine = BuildEngine({ { L"A", a }, { L"B", b } }, { calibration });

            AssertPoint(5, -4, Resolve(engine, { 499, 0 }, { 502, -2 }));
        }

        TEST_METHOD (WallDoesNotHideCrossingThroughCorner)
        {
            // B is to the right of A and physically covers all of it. C above A is physically over A's [200, 700) only,
            // so the top-right part of A's top edge is a wall.
            const RECT a{ 0, 0, 1000, 1000 };
            const RECT b{ 1000, 0, 2000, 1000 };
            const RECT c{ 0, -1000, 1000, 0 };
            const BorderCalibration right{ L"A", L"B", Edge::Right, true, { MarkerPair{ 0.2, 0.2 }, MarkerPair{ 0.8, 0.8 } } };
            const BorderCalibration top{ L"A", L"C", Edge::Top, true, { MarkerPair{ 0.2, 0.0 }, MarkerPair{ 0.7, 1.0 } } };
            auto engine = BuildEngine({ { L"A", a }, { L"B", b }, { L"C", c } }, { right, top });

            // Leaving through the top-right corner, the move crosses the top first, where it hits the wall.
            const auto jump = Resolve(engine, { 998, 0 }, { 1001, -3 });

            Assert::IsTrue(jump.has_value());
            Assert::AreEqual(1001L, jump->x);
            Assert::AreEqual(0L, jump->y);
        }

        TEST_METHOD (UncalibratedAndDisabledBordersUseWindowsBehavior)
        {
            auto disabled = TopCalibration();
            disabled.enabled = false;

            auto uncalibrated = BuildEngine({ { L"A", wideBottom }, { L"B", narrowTop } }, {});
            auto switchedOff = BuildEngine({ { L"A", wideBottom }, { L"B", narrowTop } }, { disabled });

            Assert::IsFalse(Resolve(uncalibrated, { 3000, 0 }, { 3000, -1 }).has_value());
            Assert::IsFalse(Resolve(switchedOff, { 3000, 0 }, { 3000, -1 }).has_value());
            Assert::IsTrue(uncalibrated.IsEmpty());
        }

        TEST_METHOD (CalibrationIgnoredWhenMonitorMissingOrMovedToOtherSide)
        {
            auto missing = BuildEngine({ { L"A", wideBottom }, { L"C", narrowTop } }, { TopCalibration() });
            auto otherSide = BuildEngine({ { L"A", wideBottom }, { L"B", { 4000, 0, 6000, 1000 } } }, { TopCalibration() });

            Assert::IsTrue(missing.IsEmpty());
            Assert::IsTrue(otherSide.IsEmpty());
        }

        TEST_METHOD (CalibrationIgnoredWhenMonitorRotated)
        {
            // B turned to portrait; it still touches A's top edge in the Windows layout.
            auto engine = BuildEngine({ { L"A", wideBottom }, { L"B", { 1000, -2000, 2000, 0 } } }, { TopCalibration() });

            Assert::IsTrue(engine.IsEmpty());
        }

        TEST_METHOD (RearrangingInWindowsKeepsPhysicalMapping)
        {
            // B moved 500 px to the right in display settings; it did not move on the desk.
            const RECT shifted{ 1500, -1000, 3500, 0 };
            auto engine = BuildEngine({ { L"A", wideBottom }, { L"B", shifted } }, { TopCalibration() });

            AssertPoint(3000, -1, Resolve(engine, { 3000, 0 }, { 3000, -1 }));
        }

        TEST_METHOD (WorksWhenMonitorsAreListedInReverse)
        {
            auto engine = BuildEngine({ { L"B", narrowTop }, { L"A", wideBottom } }, { TopCalibration() });

            AssertPoint(2500, -1, Resolve(engine, { 3000, 0 }, { 3000, -1 }));
        }

        TEST_METHOD (SideBySideBorderMapsVertically)
        {
            // The right monitor has twice the pixel density of the left one.
            const RECT left{ 0, 0, 1000, 1000 };
            const RECT right{ 1000, 0, 3000, 2000 };
            const BorderCalibration calibration{ L"L", L"R", Edge::Right, true, { MarkerPair{ 0.2, 0.15 }, MarkerPair{ 0.8, 0.75 } } };
            auto engine = BuildEngine({ { L"L", left }, { L"R", right } }, { calibration });

            // L pixel 500 maps through its center: 300 + 2 * (500.5 - 200) = 901. One L pixel past the edge is two R pixels.
            AssertPoint(1001, 901, Resolve(engine, { 999, 500 }, { 1000, 500 }));
        }

        TEST_METHOD (DiagonalCornerUsesEdgeCrossedFirst)
        {
            // Nothing is to the left of A, so a move through the top-left corner still crosses the top edge.
            auto engine = BuildEngine({ { L"A", wideBottom }, { L"B", narrowTop } }, { TopCalibration() });

            const auto jump = Resolve(engine, { 1, 0 }, { -2, -3 });

            Assert::IsTrue(jump.has_value());
            Assert::AreEqual(1000L, jump->x); // Clamped to B's left edge.
            Assert::AreEqual(-2L, jump->y);
        }

        TEST_METHOD (NoBounceAfterJump)
        {
            auto engine = BuildEngine({ { L"A", wideBottom }, { L"B", narrowTop } }, { TopCalibration() });

            const auto jump = Resolve(engine, { 3000, 0 }, { 3000, -1 });
            Assert::IsTrue(jump.has_value());

            // The next event starts inside B; a sideways move there must not jump again.
            Assert::IsFalse(Resolve(engine, *jump, { jump->x + 1, jump->y }).has_value());
        }

        TEST_METHOD (CursorMovedByAnotherToolIsTracked)
        {
            auto engine = BuildEngine({ { L"A", wideBottom }, { L"B", narrowTop } }, { TopCalibration() });
            Resolve(engine, { 2000, 1000 }, { 2001, 1000 });

            // The cursor is now in B (moved by SetCursorPos elsewhere) and leaves B downward.
            AssertPoint(3001, 1, Resolve(engine, { 2500, -1 }, { 2500, 0 }));
        }

        TEST_METHOD (ClaimsOnlyRemappedPartsOfEdges)
        {
            // B is physically above all of A's top edge, but Windows only connects A's [1000, 3000).
            auto engine = BuildEngine({ { L"A", wideBottom }, { L"B", narrowTop } }, { TopCalibration() });

            Assert::IsTrue(engine.IsEdgeClaimed(wideBottom, Edge::Top, 100)); // Outer edge for Windows.
            Assert::IsTrue(engine.IsEdgeClaimed(wideBottom, Edge::Top, 2000)); // Shared with B.
            Assert::IsFalse(engine.IsEdgeClaimed(wideBottom, Edge::Left, 100)); // Nothing across.
            Assert::IsFalse(engine.IsEdgeClaimed(wideBottom, Edge::Top, 4000)); // Beyond the edge.
            Assert::IsTrue(engine.IsEdgeClaimed(narrowTop, Edge::Bottom, 2000));
            Assert::IsFalse(engine.IsEdgeClaimed({ 0, 0, 10, 10 }, Edge::Top, 5)); // Unknown monitor.
        }

        TEST_METHOD (UncalibratedLayoutClaimsNothing)
        {
            auto engine = BuildEngine({ { L"A", wideBottom }, { L"B", narrowTop } }, {});

            Assert::IsFalse(engine.IsEdgeClaimed(wideBottom, Edge::Top, 100));
        }

        TEST_METHOD (GluedMonitorsCrossTheGap)
        {
            // Same physical size side by side, but Windows keeps an 800 px gap between them.
            const RECT left{ 0, 0, 1000, 1000 };
            const RECT right{ 1800, 0, 2800, 1000 };
            const BorderCalibration glue{ L"L", L"R", Edge::Right, true, { MarkerPair{ 0.2, 0.2 }, MarkerPair{ 0.8, 0.8 } } };
            auto engine = BuildEngine({ { L"L", left }, { L"R", right } }, { glue });

            AssertPoint(1800, 500, Resolve(engine, { 999, 500 }, { 1000, 500 }));
            AssertPoint(999, 500, Resolve(engine, { 1800, 500 }, { 1799, 500 }));
            Assert::IsTrue(engine.IsEdgeClaimed(left, Edge::Right, 500));
        }

        TEST_METHOD (UngluedMonitorsKeepWindowsBehavior)
        {
            const RECT left{ 0, 0, 1000, 1000 };
            const RECT right{ 1800, 0, 2800, 1000 };
            BorderCalibration glue{ L"L", L"R", Edge::Right, true, { MarkerPair{ 0.2, 0.2 }, MarkerPair{ 0.8, 0.8 } } };
            glue.enabled = false;
            auto engine = BuildEngine({ { L"L", left }, { L"R", right } }, { glue });

            Assert::IsTrue(engine.IsEmpty());
        }

        TEST_METHOD (RealLayoutStopsWhereNothingIsAbove)
        {
            // A real three-monitor layout and calibration: BenQ and Dell stand on the Odyssey and are glued together.
            const RECT odyssey{ 0, 0, 7680, 2160 };
            const RECT benq{ 1204, -2160, 5044, 0 };
            const RECT dell{ 5858, -1440, 8418, 0 };
            const BorderCalibration benqOdyssey{ L"BenQ", L"Odyssey", Edge::Bottom, true, { MarkerPair{ 0.25, 0.26018973214285712 }, MarkerPair{ 0.75, 0.46337797619047622 } } };
            const BorderCalibration odysseyDell{ L"Odyssey", L"Dell", Edge::Top, true, { MarkerPair{ 0.71050495256696422, 0.158507435179369 }, MarkerPair{ 0.9200381091889881, 0.68254701240577875 } } };
            const BorderCalibration benqDell{ L"BenQ", L"Dell", Edge::Right, true, { MarkerPair{ 0.30277777777777776, 0.20833333333333334 }, MarkerPair{ 0.76620370370370372, 0.69722222222222219 } } };
            auto engine = BuildEngine({ { L"Odyssey", odyssey }, { L"BenQ", benq }, { L"Dell", dell } }, { benqOdyssey, odysseyDell, benqDell });

            // Windows connects Odyssey x 4339..4970 to BenQ, but the calibration puts nothing above it.
            AssertPoint(4600, 0, Resolve(engine, { 4600, 0 }, { 4600, -1 }));

            // Physically under BenQ and under Dell: the cursor crosses.
            const auto toBenq = Resolve(engine, { 3000, 0 }, { 3000, -1 });
            Assert::IsTrue(toBenq.has_value() && toBenq->x >= benq.left && toBenq->x < benq.right && toBenq->y < 0);
            const auto toDell = Resolve(engine, { 5400, 0 }, { 5400, -1 });
            Assert::IsTrue(toDell.has_value() && toDell->x >= dell.left && toDell->x < dell.right && toDell->y < 0);

            // Left of BenQ, nothing is above in the calibration or in Windows. Seamless stops the cursor itself, because
            // "Ease cursor movement between displays" would move it up to BenQ; other cursor tools get the move first.
            const auto outer = engine.Resolve({ 500, 0 }, { 500, -1 });
            Assert::IsTrue(outer.has_value() && outer->outerWall);
            AssertPoint(500, 0, Resolve(engine, { 500, 0 }, { 500, -1 }));
            Assert::IsFalse(engine.IsEdgeClaimed(odyssey, Edge::Top, 500));
            Assert::IsFalse(engine.Resolve({ 4600, 0 }, { 4600, -1 })->outerWall);
        }

        TEST_METHOD (EdgePartsDescribeCrossingsAndWalls)
        {
            // B is physically only as wide as A's middle half; Windows connects all of A's top edge to it.
            const RECT a{ 0, 0, 2000, 1000 };
            const RECT b{ 0, -1000, 2000, 0 };
            const BorderCalibration calibration{ L"A", L"B", Edge::Top, true, { MarkerPair{ 0.25, 0.0 }, MarkerPair{ 0.75, 1.0 } } };
            auto engine = BuildEngine({ { L"A", a }, { L"B", b } }, { calibration });

            const auto parts = engine.EdgeParts(a, Edge::Top);

            Assert::AreEqual(size_t{ 3 }, parts.size());
            Assert::IsTrue(parts[0].start == 0 && parts[0].end == 500 && parts[0].wall);
            Assert::IsTrue(parts[1].start == 500 && parts[1].end == 1500 && !parts[1].wall && parts[1].target == 1);
            Assert::IsTrue(parts[2].start == 1500 && parts[2].end == 2000 && parts[2].wall);
            Assert::IsTrue(engine.EdgeParts(a, Edge::Left).empty());
        }

        TEST_METHOD (EdgesWithoutCalibrationAreLeftToWindows)
        {
            auto engine = BuildEngine({ { L"A", wideBottom }, { L"B", narrowTop } }, { TopCalibration() });

            // A's left edge has no calibrated border, so Windows (and its own easing) stays in charge.
            Assert::IsFalse(Resolve(engine, { 0, 500 }, { -1, 500 }).has_value());
        }

        TEST_METHOD (WithoutStoppingOuterEdgesAreLeftToWindows)
        {
            // The real layout again, with the option off: Windows (and its easing) decides left of BenQ.
            const RECT odyssey{ 0, 0, 7680, 2160 };
            const RECT benq{ 1204, -2160, 5044, 0 };
            const RECT dell{ 5858, -1440, 8418, 0 };
            const BorderCalibration benqOdyssey{ L"BenQ", L"Odyssey", Edge::Bottom, true, { MarkerPair{ 0.25, 0.26018973214285712 }, MarkerPair{ 0.75, 0.46337797619047622 } } };
            Engine engine;
            engine.Build({ { L"Odyssey", odyssey }, { L"BenQ", benq }, { L"Dell", dell } }, { benqOdyssey }, EngineOptions{ false });

            Assert::IsFalse(Resolve(engine, { 500, 0 }, { 500, -1 }).has_value());
        }

        TEST_METHOD (CalibratedNeighborWinsOverUncalibratedWindowsNeighbor)
        {
            // Two monitors above A. Only B is calibrated, and B is physically wider than its pixel range.
            const RECT a{ 0, 0, 4000, 2000 };
            const RECT b{ 0, -1000, 1000, 0 };
            const RECT c{ 1000, -1000, 4000, 0 };
            const BorderCalibration calibration{ L"A", L"B", Edge::Top, true, { MarkerPair{ 0.0, 0.0 }, MarkerPair{ 0.5, 1.0 } } };
            auto engine = BuildEngine({ { L"A", a }, { L"B", b }, { L"C", c } }, { calibration });

            // A pixel 1500 is physically under B (scale 0.5), although Windows puts C there.
            AssertPoint(750, -1, Resolve(engine, { 1500, 0 }, { 1500, -1 }));

            // Beyond B's physical range, C keeps the Windows behavior.
            Assert::IsFalse(Resolve(engine, { 2500, 0 }, { 2500, -1 }).has_value());
        }
    };
}
