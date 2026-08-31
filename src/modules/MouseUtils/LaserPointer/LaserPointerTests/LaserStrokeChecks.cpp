// Standalone exercise of the Laser Pointer stroke model.
#include "LaserStroke.h"

#include <cmath>
#include <cstdio>
#include <vector>

using namespace LaserPointerCore;

static int failures = 0;

static void Check(bool condition, const char* what)
{
    std::printf("%s  %s\n", condition ? "[ ok ]" : "[FAIL]", what);
    if (!condition)
    {
        ++failures;
    }
}

// Signed area; also tells us the polygon is closed and non-degenerate.
static float PolygonArea(const std::vector<Vec2>& p)
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

static LaserStroke MakeHorizontalStroke(uint64_t startMs, int samples, float step, const StrokeOptions& opts)
{
    LaserStroke s;
    s.SetOptions(opts);
    for (int i = 0; i < samples; ++i)
    {
        s.AddPoint(100.0f + step * i, 200.0f, startMs + static_cast<uint64_t>(i) * 8);
    }
    return s;
}

int main()
{
    StrokeOptions opts;
    opts.size = 10.0f;
    opts.streamline = 0.0f; // no smoothing, so geometry is exactly predictable
    opts.decayTimeMs = 1000;
    opts.decayLength = 50;
    opts.minDistance = 0.0f;

    // --- empty stroke draws nothing
    {
        LaserStroke s;
        s.SetOptions(opts);
        std::vector<Vec2> outline;
        Check(s.BuildOutline(1000, outline) == 0, "empty stroke produces no outline");
        Check(s.Empty(), "empty stroke reports Empty()");
    }

    // --- single point renders as a dot
    {
        LaserStroke s;
        s.SetOptions(opts);
        s.AddPoint(50.0f, 50.0f, 1000);
        std::vector<Vec2> outline;
        const size_t n = s.BuildOutline(1000, outline);
        Check(n >= 8, "single point produces a round dot");
        const float area = PolygonArea(outline);
        // Full head width is opts.size, so radius 5 -> area ~ pi*25 = 78.5
        Check(area > 60.0f && area < 85.0f, "dot area is close to a circle of radius size/2");
    }

    // --- taper: head is widest, tail collapses to nothing
    {
        LaserStroke s = MakeHorizontalStroke(1000, 80, 3.0f, opts);
        const uint64_t now = 1000 + 79 * 8;
        const size_t count = s.PointCount();
        const float headHalf = s.HalfWidthAt(count - 1, now);
        const float midHalf = s.HalfWidthAt(count - 25, now);
        const float tailHalf = s.HalfWidthAt(0, now);

        Check(std::abs(headHalf - 5.0f) < 0.01f, "head half width equals size/2");
        Check(midHalf > 0.0f && midHalf < headHalf, "width tapers between head and tail");
        Check(tailHalf == 0.0f, "sample older than decayLength has zero width");

        std::vector<Vec2> outline;
        Check(s.BuildOutline(now, outline) > 3, "tapered stroke produces an outline");
        Check(PolygonArea(outline) > 0.0f, "outline encloses a positive area");
    }

    // --- time decay: the same stroke shrinks as it ages, then vanishes
    {
        LaserStroke s = MakeHorizontalStroke(1000, 40, 3.0f, opts);
        const uint64_t fresh = 1000 + 39 * 8;
        const float freshHalf = s.HalfWidthAt(s.PointCount() - 1, fresh);
        const float agedHalf = s.HalfWidthAt(s.PointCount() - 1, fresh + 700);
        Check(agedHalf < freshHalf, "a sample narrows as it ages");
        Check(s.HalfWidthAt(s.PointCount() - 1, fresh + 5000) == 0.0f, "a sample past decayTime has zero width");

        Check(s.Prune(fresh), "stroke survives pruning while fresh");
        Check(!s.Prune(fresh + 5000), "stroke is fully pruned once every sample expired");
        Check(s.Empty(), "pruned stroke is empty");
    }

    // --- widthScale drives the glow / core passes off the same centerline
    {
        LaserStroke s = MakeHorizontalStroke(1000, 40, 3.0f, opts);
        const uint64_t now = 1000 + 39 * 8;
        std::vector<Vec2> normal, glow, core;
        s.BuildOutline(now, normal, 1.0f);
        s.BuildOutline(now, glow, 2.4f);
        s.BuildOutline(now, core, 0.38f);
        const float an = PolygonArea(normal), ag = PolygonArea(glow), ac = PolygonArea(core);
        Check(ag > an && an > ac, "glow pass is wider than the body, body wider than the core");
    }

    // --- streamline smooths towards the previous point
    {
        StrokeOptions smooth = opts;
        smooth.streamline = 0.5f;
        LaserStroke s;
        s.SetOptions(smooth);
        s.AddPoint(0.0f, 0.0f, 1000);
        s.AddPoint(100.0f, 0.0f, 1008);
        std::vector<Vec2> outline;
        s.BuildOutline(1008, outline);
        // With streamline 0.5 the second sample lands halfway to the raw input.
        Check(s.PointCount() == 2, "smoothed sample was accepted");
        bool nearFifty = false;
        for (const auto& p : outline)
        {
            if (std::abs(p.x - 50.0f) < 6.0f)
            {
                nearFifty = true;
            }
        }
        Check(nearFifty, "streamline 0.5 places the second sample near the midpoint");
    }

    // --- duplicate / sub-threshold samples are rejected
    {
        StrokeOptions guard = opts;
        guard.minDistance = 2.0f;
        LaserStroke s;
        s.SetOptions(guard);
        s.AddPoint(10.0f, 10.0f, 1000);
        for (int i = 0; i < 20; ++i)
        {
            s.AddPoint(10.0f, 10.0f, 1000 + i); // pointer standing still
        }
        Check(s.PointCount() == 1, "a stationary pointer does not pile up samples");
    }

    // --- Finish() stops accepting points but keeps what is there
    {
        LaserStroke s = MakeHorizontalStroke(1000, 10, 3.0f, opts);
        const size_t before = s.PointCount();
        s.Finish();
        s.AddPoint(999.0f, 999.0f, 1100);
        Check(s.PointCount() == before, "a finished stroke ignores new points");
        Check(s.Finished(), "Finish() is observable");
        s.Clear();
        Check(!s.Finished() && s.Empty(), "Clear() resets the finished flag");
    }

    // --- a stroke that doubles back on itself still yields a sane outline
    {
        LaserStroke s;
        s.SetOptions(opts);
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
        Check(s.BuildOutline(now, outline) > 3, "doubling back still produces an outline");
        bool allFinite = true;
        for (const auto& p : outline)
        {
            if (!std::isfinite(p.x) || !std::isfinite(p.y))
            {
                allFinite = false;
            }
        }
        Check(allFinite, "outline contains no NaN/inf vertices");
    }

    std::printf("\n%s (%d failure(s))\n", failures == 0 ? "ALL CHECKS PASSED" : "CHECKS FAILED", failures);
    return failures == 0 ? 0 : 1;
}
