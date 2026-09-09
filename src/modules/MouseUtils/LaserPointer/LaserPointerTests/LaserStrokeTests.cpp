// Unit tests for the Laser Pointer stroke model.
//
// LaserStroke decides where the trail is, how wide it is at each sample, and when it
// disappears. It is deliberately free of Windows dependencies, so it is exercised here
// on its own - no overlay, no mouse hook, no GPU.

#pragma warning(push)
#pragma warning(disable : 26466)
#include "CppUnitTest.h"
#pragma warning(pop)

#include "..\LaserStroke.h"

#include <cmath>
#include <vector>

using namespace Microsoft::VisualStudio::CppUnitTestFramework;
using namespace LaserPointerCore;

namespace LaserPointerUnitTests
{
    namespace
    {
        // Signed area; also tells us the polygon is closed and non-degenerate.
        float PolygonArea(const std::vector<Vec2>& p)
        {
            double area = 0.0;
            for (size_t i = 0; i < p.size(); ++i)
            {
                const Vec2& a = p[i];
                const Vec2& b = p[(i + 1) % p.size()];
                area += static_cast<double>(a.x) * b.y - static_cast<double>(b.x) * a.y;
            }
            return static_cast<float>(std::abs(area) * 0.5);
        }

        // No smoothing and no minimum distance, so the geometry each test asks about
        // follows exactly from the points it fed in.
        constexpr StrokeOptions DefaultOptions()
        {
            StrokeOptions opts;
            opts.size = 10.0f;
            opts.streamline = 0.0f;
            opts.decayTimeMs = 1000;
            opts.decayLength = 50;
            opts.minDistance = 0.0f;
            return opts;
        }

        LaserStroke MakeHorizontalStroke(uint64_t startMs, int samples, float step, const StrokeOptions& opts)
        {
            LaserStroke s;
            s.SetOptions(opts);
            for (int i = 0; i < samples; ++i)
            {
                s.AddPoint(100.0f + step * i, 200.0f, startMs + static_cast<uint64_t>(i) * 8);
            }
            return s;
        }
    }

    TEST_CLASS(LaserStrokeTests)
    {
    public:
        TEST_METHOD(EmptyStrokeProducesNoOutline)
        {
            LaserStroke s;
            s.SetOptions(DefaultOptions());

            std::vector<Vec2> outline;
            Assert::AreEqual(static_cast<size_t>(0), s.BuildOutline(1000, outline), L"empty stroke produces no outline");
            Assert::IsTrue(s.Empty(), L"empty stroke reports Empty()");
        }

        TEST_METHOD(SinglePointRendersAsARoundDot)
        {
            LaserStroke s;
            s.SetOptions(DefaultOptions());
            s.AddPoint(50.0f, 50.0f, 1000);

            std::vector<Vec2> outline;
            const size_t n = s.BuildOutline(1000, outline);
            Assert::IsTrue(n >= 8, L"single point produces a round dot");

            // Full head width is opts.size, so radius 5 gives an area of about pi*25 = 78.5.
            const float area = PolygonArea(outline);
            Assert::IsTrue(area > 60.0f && area < 85.0f, L"dot area is close to a circle of radius size/2");
        }

        TEST_METHOD(WidthTapersFromHeadToTail)
        {
            LaserStroke s = MakeHorizontalStroke(1000, 80, 3.0f, DefaultOptions());
            const uint64_t now = 1000 + 79 * 8;
            const size_t count = s.PointCount();

            const float headHalf = s.HalfWidthAt(count - 1, now);
            const float midHalf = s.HalfWidthAt(count - 25, now);
            const float tailHalf = s.HalfWidthAt(0, now);

            Assert::IsTrue(std::abs(headHalf - 5.0f) < 0.01f, L"head half width equals size/2");
            Assert::IsTrue(midHalf > 0.0f && midHalf < headHalf, L"width tapers between head and tail");
            Assert::AreEqual(0.0f, tailHalf, L"sample older than decayLength has zero width");

            std::vector<Vec2> outline;
            Assert::IsTrue(s.BuildOutline(now, outline) > 3, L"tapered stroke produces an outline");
            Assert::IsTrue(PolygonArea(outline) > 0.0f, L"outline encloses a positive area");
        }

        TEST_METHOD(SamplesNarrowWithAgeAndArePrunedAway)
        {
            LaserStroke s = MakeHorizontalStroke(1000, 40, 3.0f, DefaultOptions());
            const uint64_t fresh = 1000 + 39 * 8;

            const float freshHalf = s.HalfWidthAt(s.PointCount() - 1, fresh);
            const float agedHalf = s.HalfWidthAt(s.PointCount() - 1, fresh + 700);
            Assert::IsTrue(agedHalf < freshHalf, L"a sample narrows as it ages");
            Assert::AreEqual(0.0f, s.HalfWidthAt(s.PointCount() - 1, fresh + 5000), L"a sample past decayTime has zero width");

            Assert::IsTrue(s.Prune(fresh), L"stroke survives pruning while fresh");
            Assert::IsFalse(s.Prune(fresh + 5000), L"stroke is fully pruned once every sample expired");
            Assert::IsTrue(s.Empty(), L"pruned stroke is empty");
        }

        TEST_METHOD(WidthScaleDrivesGlowAndCorePasses)
        {
            LaserStroke s = MakeHorizontalStroke(1000, 40, 3.0f, DefaultOptions());
            const uint64_t now = 1000 + 39 * 8;

            std::vector<Vec2> normal, glow, core;
            s.BuildOutline(now, normal, 1.0f);
            s.BuildOutline(now, glow, 2.4f);
            s.BuildOutline(now, core, 0.38f);

            const float an = PolygonArea(normal);
            const float ag = PolygonArea(glow);
            const float ac = PolygonArea(core);
            Assert::IsTrue(ag > an && an > ac, L"glow pass is wider than the body, body wider than the core");
        }

        TEST_METHOD(StreamlineSmoothsTowardsThePreviousPoint)
        {
            StrokeOptions smooth = DefaultOptions();
            smooth.streamline = 0.5f;

            LaserStroke s;
            s.SetOptions(smooth);
            s.AddPoint(0.0f, 0.0f, 1000);
            s.AddPoint(100.0f, 0.0f, 1008);

            std::vector<Vec2> outline;
            s.BuildOutline(1008, outline);

            // With streamline 0.5 the second sample lands halfway to the raw input.
            Assert::AreEqual(static_cast<size_t>(2), s.PointCount(), L"smoothed sample was accepted");

            bool nearFifty = false;
            for (const auto& p : outline)
            {
                if (std::abs(p.x - 50.0f) < 6.0f)
                {
                    nearFifty = true;
                }
            }
            Assert::IsTrue(nearFifty, L"streamline 0.5 places the second sample near the midpoint");
        }

        TEST_METHOD(StationaryPointerDoesNotPileUpSamples)
        {
            StrokeOptions guard = DefaultOptions();
            guard.minDistance = 2.0f;

            LaserStroke s;
            s.SetOptions(guard);
            s.AddPoint(10.0f, 10.0f, 1000);
            for (int i = 0; i < 20; ++i)
            {
                s.AddPoint(10.0f, 10.0f, 1000 + static_cast<uint64_t>(i));
            }

            Assert::AreEqual(static_cast<size_t>(1), s.PointCount(), L"a stationary pointer does not pile up samples");
        }

        TEST_METHOD(FinishStopsAcceptingPointsAndClearResets)
        {
            LaserStroke s = MakeHorizontalStroke(1000, 10, 3.0f, DefaultOptions());
            const size_t before = s.PointCount();

            s.Finish();
            s.AddPoint(999.0f, 999.0f, 1100);
            Assert::AreEqual(before, s.PointCount(), L"a finished stroke ignores new points");
            Assert::IsTrue(s.Finished(), L"Finish() is observable");

            s.Clear();
            Assert::IsFalse(s.Finished(), L"Clear() resets the finished flag");
            Assert::IsTrue(s.Empty(), L"Clear() empties the stroke");
        }

        TEST_METHOD(DoublingBackStillProducesASaneOutline)
        {
            LaserStroke s;
            s.SetOptions(DefaultOptions());
            for (int i = 0; i < 30; ++i)
            {
                s.AddPoint(100.0f + 4.0f * i, 200.0f, 1000 + static_cast<uint64_t>(i) * 8);
            }
            for (int i = 0; i < 30; ++i)
            {
                s.AddPoint(216.0f - 4.0f * i, 200.0f, 1240 + static_cast<uint64_t>(i) * 8);
            }

            std::vector<Vec2> outline;
            const uint64_t now = 1240 + 29 * 8;
            Assert::IsTrue(s.BuildOutline(now, outline) > 3, L"doubling back still produces an outline");

            for (const auto& p : outline)
            {
                Assert::IsTrue(std::isfinite(p.x) && std::isfinite(p.y), L"outline contains no NaN/inf vertices");
            }
        }
    };
}
