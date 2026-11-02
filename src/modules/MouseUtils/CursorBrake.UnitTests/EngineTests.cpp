// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#include "pch.h"
#include "CursorBrakeCore.h"

#include <algorithm>
#include <cstdint>
#include <optional>
#include <string>
#include <utility>
#include <vector>

using namespace Microsoft::VisualStudio::CppUnitTestFramework;
using namespace cursorbrake;

namespace
{
    constexpr unsigned int DPI_100 = 96;
    constexpr unsigned int DPI_150 = 144;
    constexpr unsigned int DPI_200 = 192;

    Monitor Mon(long left, long top, long right, long bottom, unsigned int dpi = DPI_100)
    {
        return Monitor{ RectL{ left, top, right, bottom }, dpi };
    }

    // Rects are physical pixels and half-open like a Win32 RECT, so in every layout below the
    // last column of the left monitor is x = 1919 and the first column of the right one is 1920.

    // A | B, both 1920x1080 at 100%.
    Layout SideBySide()
    {
        return Layout{ { Mon(0, 0, 1920, 1080), Mon(1920, 0, 3840, 1080) } };
    }

    // A above B, both 1920x1080 at 100%. The shared edge is y = 1080.
    Layout Stacked()
    {
        return Layout{ { Mon(0, 0, 1920, 1080), Mon(0, 1080, 1920, 2160) } };
    }

    // B sits 540 px lower than A, so the A|B edge is shared only for y in [540, 1080).
    Layout Offset()
    {
        return Layout{ { Mon(0, 0, 1920, 1080), Mon(1920, 540, 3840, 1620) } };
    }

    // A (100%) | B (150%) | C (200%). Edges at x = 1920 and x = 4800.
    Layout MixedDpi()
    {
        return Layout{ { Mon(0, 0, 1920, 1080, DPI_100), Mon(1920, 0, 4800, 1620, DPI_150), Mon(4800, 0, 8640, 2160, DPI_200) } };
    }

    Layout Single()
    {
        return Layout{ { Mon(0, 0, 1920, 1080) } };
    }

    // Top-left, top-right, bottom-left, bottom-right, meeting at (1920, 1080).
    Layout Junction()
    {
        return Layout{ { Mon(0, 0, 1920, 1080), Mon(1920, 0, 3840, 1080), Mon(0, 1080, 1920, 2160), Mon(1920, 1080, 3840, 2160) } };
    }

    // A and B with an 80 px strip between them that belongs to no monitor.
    Layout Gap()
    {
        return Layout{ { Mon(0, 0, 1920, 1080), Mon(2000, 0, 3920, 1080) } };
    }

    // A beside a taller B, top-aligned, so nothing lies below A and its bottom edge is outer.
    Layout TallNeighbor()
    {
        return Layout{ { Mon(0, 0, 1920, 1080), Mon(1920, 0, 4480, 1440) } };
    }

    // A tall A with B above C on its right. A's right edge borders B for y < 1080 and C below.
    Layout TwoOnTheRight()
    {
        return Layout{ { Mon(0, 0, 1920, 2160), Mon(1920, 0, 3840, 1080), Mon(1920, 1080, 3840, 2160) } };
    }

    // A with a neighbor on its right, one below and one above.
    Layout RightBelowAbove()
    {
        return Layout{ { Mon(0, 0, 1920, 1080), Mon(1920, 0, 3840, 1080), Mon(0, 1080, 1920, 2160), Mon(0, -1080, 1920, 0) } };
    }

    // A above B on the left, a tall C on the right bordering both.
    Layout TwoOnTheLeft()
    {
        return Layout{ { Mon(0, 0, 1920, 1080), Mon(0, 1080, 1920, 2160), Mon(1920, 0, 3840, 2160) } };
    }

    // A primary with a monitor above, one to the right and one to the left, in an order
    // EnumDisplayMonitors can report: top, right, primary (the rect at 0,0), left. The right
    // monitor's DPI varies by case.
    Layout FourAround(unsigned int rightDpi)
    {
        return Layout{ { Mon(0, -1080, 1920, 0), Mon(1920, 0, 3840, 1080, rightDpi), Mon(0, 0, 1920, 1080), Mon(-1920, 0, 0, 1080) } };
    }

    // The per-move cap and the free-return window in physical pixels on a monitor at `dpi`, from
    // the source scaling every threshold uses: 10 and 100 at 100%, 15 and 150 at 150%.
    int CapAt(unsigned int dpi)
    {
        return MOVE_CAP * static_cast<int>(dpi) / static_cast<int>(DPI_100);
    }

    // The baseline most tests run under: a 25 px push, which a few 10 px moves (the cap at 100%)
    // reach, and the free return on. The shipping defaults (Settings{}) have their own tests.
    Settings Defaults()
    {
        Settings s;
        s.pushDistance = 25;
        s.freeReturn = true;
        return s;
    }

    Settings WithFreeReturn(bool freeReturn)
    {
        Settings s = Defaults();
        s.freeReturn = freeReturn;
        return s;
    }

    // A 500 px push.
    Settings SlowPushSettings()
    {
        Settings s = Defaults();
        s.pushDistance = 500;
        return s;
    }

    Settings WithDistance(int pushDistance)
    {
        Settings s = Defaults();
        s.pushDistance = pushDistance;
        return s;
    }

    Settings WithFilter(EdgeFilter filter)
    {
        Settings s = Defaults();
        s.edgeFilter = filter;
        return s;
    }

    Settings WithDragDisable()
    {
        Settings s = Defaults();
        s.disableWhileDragging = true;
        return s;
    }

    Settings WithModifier(CrossModifier modifier)
    {
        Settings s = Defaults();
        s.crossModifier = modifier;
        return s;
    }

    MoveEvent Move(PointL from, PointL to)
    {
        MoveEvent move;
        move.current = from;
        move.proposed = to;
        return move;
    }

    MoveEvent ModifierMove(PointL from, PointL to)
    {
        MoveEvent move = Move(from, to);
        move.crossModifierHeld = true;
        return move;
    }

    MoveEvent AbsoluteMove(PointL from, PointL to)
    {
        MoveEvent move = Move(from, to);
        move.input = InputKind::Absolute;
        return move;
    }

    MoveEvent DragMove(PointL from, PointL to)
    {
        MoveEvent move = Move(from, to);
        move.buttonDown = true;
        return move;
    }

    MoveEvent DragModifierMove(PointL from, PointL to)
    {
        MoveEvent move = DragMove(from, to);
        move.crossModifierHeld = true;
        return move;
    }

    MoveEvent ClippedMove(PointL from, PointL to, RectL clip)
    {
        MoveEvent move = Move(from, to);
        move.clip = clip;
        return move;
    }

    Decision Allow(PointL point)
    {
        return Decision{ DecisionKind::Allow, point };
    }

    Decision Hold(PointL point)
    {
        return Decision{ DecisionKind::Hold, point };
    }

    struct Step
    {
        MoveEvent move;
        Decision expected;
        // Replaces the settings or layout from this step on, the way a settings save or a display
        // change reaches the hook between two moves.
        std::optional<Settings> settings;
        std::optional<Layout> layout;
    };

    Step Expect(MoveEvent move, Decision expected)
    {
        return Step{ move, expected, std::nullopt, std::nullopt };
    }

    Step ExpectAfter(const Settings& settings, MoveEvent move, Decision expected)
    {
        return Step{ move, expected, settings, std::nullopt };
    }

    Step ExpectAfter(const Layout& layout, MoveEvent move, Decision expected)
    {
        return Step{ move, expected, std::nullopt, layout };
    }

    // Pushes A's right edge by 10 px from the edge column at y = 500. Two of these leave a push of
    // 20, five short of the 25 px threshold at 100%.
    Step PushTen()
    {
        return Expect(Move({ 1919, 500 }, { 1929, 500 }), Hold({ 1919, 500 }));
    }

    std::vector<Step> Concat(std::vector<Step> first, const std::vector<Step>& second)
    {
        first.insert(first.end(), second.begin(), second.end());
        return first;
    }

    // The same steps with every move carrying `clip`.
    std::vector<Step> UnderClip(std::vector<Step> steps, RectL clip)
    {
        for (Step& step : steps)
        {
            step.move.clip = clip;
        }
        return steps;
    }

    // The smallest rect around every monitor: what GetClipCursor reports when no app confines the
    // cursor.
    RectL BoundingRect(const Layout& layout)
    {
        RectL bounds = layout.monitors.front().rect;
        for (const Monitor& monitor : layout.monitors)
        {
            bounds.left = (std::min)(bounds.left, monitor.rect.left);
            bounds.top = (std::min)(bounds.top, monitor.rect.top);
            bounds.right = (std::max)(bounds.right, monitor.rect.right);
            bounds.bottom = (std::max)(bounds.bottom, monitor.rect.bottom);
        }
        return bounds;
    }

    // Crosses from A to B under the baseline: 10 + 10 + 5 reaches 25, landing at x = 1924.
    std::vector<Step> CrossToB()
    {
        return { PushTen(), PushTen(), Expect(Move({ 1919, 500 }, { 1924, 500 }), Allow({ 1924, 500 })) };
    }

    // Slow pushes from `edge` (the source monitor's edge pixel) in the direction (dx, dy), with
    // step sizes cycling through `sizes`. Each step adds its overshoot, at most `cap`
    // (the per-move cap in physical pixels on the source), and holds at `edge` until the sum
    // reaches `threshold`; the step that reaches it is allowed.
    std::vector<Step> SlowPush(PointL edge, int dx, int dy, int threshold, std::vector<int> sizes, int cap = CapAt(DPI_100))
    {
        std::vector<Step> steps;
        int pushed = 0;
        for (size_t i = 0; pushed < threshold; ++i)
        {
            const int size = sizes[i % sizes.size()];
            pushed += size < cap ? size : cap;
            const PointL proposed{ edge.x + dx * size, edge.y + dy * size };
            steps.push_back(Expect(Move(edge, proposed), pushed >= threshold ? Allow(proposed) : Hold(edge)));
        }
        return steps;
    }

    // `count` equal hand steps of `size` px from `edge`: every one before the last holds, and the
    // last crosses.
    std::vector<Step> CrossOnStep(PointL edge, int dx, int dy, int size, int count)
    {
        std::vector<Step> steps;
        const PointL proposed{ edge.x + dx * size, edge.y + dy * size };
        for (int i = 1; i <= count; ++i)
        {
            steps.push_back(Expect(Move(edge, proposed), i == count ? Allow(proposed) : Hold(edge)));
        }
        return steps;
    }

    // `count` moves off the `source` rect from `start`, cycling through `deltas`. Every move before
    // the last holds on the source's edge with its along-edge motion kept, the next move starts
    // there, and the last move crosses.
    std::vector<Step> CycleFrom(const RectL& source, PointL start, const std::vector<PointL>& deltas, int count)
    {
        std::vector<Step> steps;
        PointL cursor = start;
        for (int i = 0; i < count; ++i)
        {
            const PointL delta = deltas[static_cast<size_t>(i) % deltas.size()];
            const PointL proposed{ cursor.x + delta.x, cursor.y + delta.y };
            const PointL edge{ std::clamp(proposed.x, source.left, source.right - 1), std::clamp(proposed.y, source.top, source.bottom - 1) };
            steps.push_back(Expect(Move(cursor, proposed), i + 1 == count ? Allow(proposed) : Hold(edge)));
            cursor = edge;
        }
        return steps;
    }

    // One recorded low-level hook event: the cursor before the move and the point it proposed.
    struct TracedMove
    {
        PointL cursor;
        PointL pt;
    };

    // Replays traced moves from the `source` rect. A move that stays on the source is allowed,
    // every other move holds on the source's edge with its along-edge motion kept, and the last
    // move crosses. The replay ends at the crossing, because the trace's later cursor positions
    // follow holds the engine under test does not make.
    std::vector<Step> Replay(const std::vector<TracedMove>& moves, const RectL& source)
    {
        std::vector<Step> steps;
        for (size_t i = 0; i < moves.size(); ++i)
        {
            const PointL pt = moves[i].pt;
            const bool onSource = pt.x >= source.left && pt.x < source.right && pt.y >= source.top && pt.y < source.bottom;
            const PointL edge{ std::clamp(pt.x, source.left, source.right - 1), std::clamp(pt.y, source.top, source.bottom - 1) };
            steps.push_back(Expect(Move(moves[i].cursor, pt), onSource || i + 1 == moves.size() ? Allow(pt) : Hold(edge)));
        }
        return steps;
    }

    struct Case
    {
        std::wstring name;
        Layout layout;
        Settings settings;
        std::vector<Step> steps;
    };

    std::wstring Describe(const Decision& decision)
    {
        const wchar_t* kind = decision.kind == DecisionKind::Allow ? L"Allow" : L"Hold";
        return std::wstring(kind) + L"(" + std::to_wstring(decision.point.x) + L", " + std::to_wstring(decision.point.y) + L")";
    }

    const wchar_t* Name(InputTreatment treatment)
    {
        switch (treatment)
        {
        case InputTreatment::Pass:
            return L"Pass";
        case InputTreatment::Judge:
        default:
            return L"Judge";
        }
    }

    // Each case gets a fresh engine. Every step checks both the decision kind and the point, so an
    // Allow that moves the cursor and a Hold at the wrong place both fail. A case stops at its first
    // mismatch, since later steps assume the earlier decisions.
    std::optional<std::wstring> Run(const Case& c)
    {
        Engine engine;
        Layout layout = c.layout;
        Settings settings = c.settings;
        for (size_t i = 0; i < c.steps.size(); ++i)
        {
            const Step& step = c.steps[i];
            if (step.settings)
            {
                settings = *step.settings;
            }
            if (step.layout)
            {
                layout = *step.layout;
            }

            const Decision actual = engine.OnMove(step.move, layout, settings);
            if (!(actual == step.expected))
            {
                return c.name + L", step " + std::to_wstring(i + 1) + L": expected " + Describe(step.expected) + L", got " + Describe(actual);
            }
        }
        return std::nullopt;
    }

    // Runs every case before failing, so one report names every case that broke.
    void RunAll(const std::vector<Case>& cases)
    {
        std::wstring failures;
        for (const Case& c : cases)
        {
            if (const std::optional<std::wstring> failure = Run(c))
            {
                failures += (failures.empty() ? L"" : L"; ") + *failure;
            }
        }
        if (!failures.empty())
        {
            Assert::Fail(failures.c_str());
        }
    }

    // Runs the cases as written, with no clip known, and again with every move carrying the
    // layout's bounding rect, the clip Windows reports when no app confines the cursor.
    void RunAllWithAndWithoutTheBoundingRect(const std::vector<Case>& cases)
    {
        std::vector<Case> all = cases;
        for (const Case& c : cases)
        {
            all.push_back({ c.name + L" (bounding rect)", c.layout, c.settings, UnderClip(c.steps, BoundingRect(c.layout)) });
        }
        RunAll(all);
    }

    // Per-edge settings. Monitor indices are into the layout the setting is added to.
    EdgeSetting BothWays(size_t first, size_t second, EdgeMode mode, int pushDistance = 25)
    {
        return EdgeSetting{ first, second, false, mode, pushDistance };
    }

    EdgeSetting OneWay(size_t first, size_t second, EdgeMode mode, int pushDistance = 25)
    {
        return EdgeSetting{ first, second, true, mode, pushDistance };
    }

    Layout WithEdges(Layout layout, std::vector<EdgeSetting> edges)
    {
        layout.edges = std::move(edges);
        return layout;
    }

    // Small monitors, so a walk of hand-sized steps meets an edge on most moves. TL (100%), TR
    // (150%), BL (100%) and BR (200%), meeting at (48, 32).
    Layout SmallJunction()
    {
        return Layout{ { Mon(0, 0, 48, 32, DPI_100), Mon(48, 0, 96, 32, DPI_150), Mon(0, 32, 48, 64, DPI_100), Mon(48, 32, 96, 64, DPI_200) } };
    }

    // Three in a row at 100%, 150% and 200%, the middle one 8 px lower, so both edges are
    // shared only in part.
    Layout SmallRow()
    {
        return Layout{ { Mon(0, 0, 40, 40, DPI_100), Mon(40, 8, 80, 48, DPI_150), Mon(80, 0, 120, 40, DPI_200) } };
    }

    // A tall A with B above C on its right.
    Layout SmallTwoOnTheRight()
    {
        return Layout{ { Mon(0, 0, 40, 64), Mon(40, 0, 80, 32), Mon(40, 32, 80, 64, DPI_150) } };
    }

    // Whether two rects share an edge, and on which axis: side by side (true) or stacked (false).
    std::optional<bool> SharedEdgeAxis(const RectL& a, const RectL& b)
    {
        const bool overlapX = a.left < b.right && b.left < a.right;
        const bool overlapY = a.top < b.bottom && b.top < a.bottom;
        if ((a.right == b.left || b.right == a.left) && overlapY)
        {
            return true;
        }
        if ((a.bottom == b.top || b.bottom == a.top) && overlapX)
        {
            return false;
        }
        return std::nullopt;
    }

    // One both-ways entry per pair of monitors that share an edge on an axis `onAxis` accepts.
    template<typename AxisTest>
    std::vector<EdgeSetting> EverySharedEdge(const Layout& layout, EdgeMode mode, int pushDistance, AxisTest onAxis)
    {
        std::vector<EdgeSetting> edges;
        for (size_t i = 0; i < layout.monitors.size(); ++i)
        {
            for (size_t j = i + 1; j < layout.monitors.size(); ++j)
            {
                const std::optional<bool> sideBySide = SharedEdgeAxis(layout.monitors[i].rect, layout.monitors[j].rect);
                if (sideBySide && onAxis(*sideBySide))
                {
                    edges.push_back(BothWays(i, j, mode, pushDistance));
                }
            }
        }
        return edges;
    }

    // A fixed pseudo-random walk, the same on every run.
    struct Walk
    {
        uint32_t state;

        uint32_t Next()
        {
            state = state * 1664525u + 1013904223u;
            return state >> 8;
        }

        long Step()
        {
            return static_cast<long>(Next() % 25) - 12;
        }
    };

    // Feeds the same walk to two engines, each with its own layout and settings, and names the
    // first move where their decisions differ. Every move starts where the decisions put the
    // cursor, so a walk keeps pushing at whatever edge it reaches. One move in sixteen holds the
    // cross key and one in sixteen is a drag, so bypasses and their resets are in the walk too.
    std::optional<std::wstring> FirstDifference(const Layout& first, const Settings& firstSettings, const Layout& second, const Settings& secondSettings, uint32_t seed, int count)
    {
        const RectL bounds = BoundingRect(first);
        Engine a;
        Engine b;
        Walk walk{ seed };
        PointL cursor{ (bounds.left + bounds.right) / 2, (bounds.top + bounds.bottom) / 2 };
        for (int i = 0; i < count; ++i)
        {
            const PointL proposed{ std::clamp(cursor.x + walk.Step(), bounds.left, bounds.right - 1), std::clamp(cursor.y + walk.Step(), bounds.top, bounds.bottom - 1) };
            MoveEvent move = Move(cursor, proposed);
            const uint32_t kind = walk.Next() % 16;
            move.crossModifierHeld = kind == 0;
            move.buttonDown = kind == 1;

            const Decision fromFirst = a.OnMove(move, first, firstSettings);
            const Decision fromSecond = b.OnMove(move, second, secondSettings);
            if (!(fromFirst == fromSecond))
            {
                return L"seed " + std::to_wstring(seed) + L", move " + std::to_wstring(i + 1) + L" from (" + std::to_wstring(cursor.x) + L", " + std::to_wstring(cursor.y) + L"): " + Describe(fromFirst) + L" against " + Describe(fromSecond);
            }
            cursor = fromFirst.point;
        }
        return std::nullopt;
    }

    struct Equivalence
    {
        std::wstring name;
        Layout first;
        Settings firstSettings;
        Layout second;
        Settings secondSettings;
    };

    // Runs every pair over 20 walks of 2,000 moves before failing.
    void RunEquivalences(const std::vector<Equivalence>& pairs)
    {
        std::wstring failures;
        for (const Equivalence& pair : pairs)
        {
            for (uint32_t seed = 1; seed <= 20; ++seed)
            {
                if (const std::optional<std::wstring> difference = FirstDifference(pair.first, pair.firstSettings, pair.second, pair.secondSettings, seed, 2000))
                {
                    failures += (failures.empty() ? L"" : L"; ") + pair.name + L", " + *difference;
                    break;
                }
            }
        }
        if (!failures.empty())
        {
            Assert::Fail(failures.c_str());
        }
    }

    // `count` pushes of `size` px from `edge` in the direction (dx, dy), every one held at `edge`.
    std::vector<Step> HeldPushes(PointL edge, int dx, int dy, int size, int count)
    {
        std::vector<Step> steps;
        const PointL proposed{ edge.x + dx * size, edge.y + dy * size };
        for (int i = 0; i < count; ++i)
        {
            steps.push_back(Expect(Move(edge, proposed), Hold(edge)));
        }
        return steps;
    }

    // HeldPushes with a mouse button down on every move.
    std::vector<Step> HeldDrags(PointL edge, int dx, int dy, int size, int count)
    {
        std::vector<Step> steps = HeldPushes(edge, dx, dy, size, count);
        for (Step& step : steps)
        {
            step.move.buttonDown = true;
        }
        return steps;
    }

    std::wstring Describe(const EdgeRule& rule)
    {
        switch (rule.mode)
        {
        case EdgeMode::Custom:
            return L"Custom " + std::to_wstring(rule.pushDistance);
        case EdgeMode::Off:
            return L"Off";
        case EdgeMode::Wall:
            return L"Wall";
        default:
            return L"mode " + std::to_wstring(static_cast<int>(rule.mode));
        }
    }

    // The same steps with every move naming `monitor` as the full-screen wall, or none.
    std::vector<Step> OnWall(std::vector<Step> steps, std::optional<size_t> monitor)
    {
        for (Step& step : steps)
        {
            step.move.wallMonitor = monitor;
        }
        return steps;
    }

    Step OnWall(Step step, std::optional<size_t> monitor)
    {
        step.move.wallMonitor = monitor;
        return step;
    }
}

namespace CursorBrakeEngineTests
{
    TEST_CLASS (SettingsDefaults)
    {
    public:
        TEST_METHOD (DefaultSettingsAreTheShippingValues)
        {
            const Settings s;
            Assert::AreEqual(300, s.pushDistance);
            Assert::IsTrue(s.freeReturn);
            Assert::IsTrue(s.edgeFilter == EdgeFilter::All);
            Assert::IsFalse(s.disableWhileDragging);
        }

        TEST_METHOD (ConstantsAreTheShippingValues)
        {
            Assert::AreEqual(10, MOVE_CAP);
            Assert::AreEqual(100, FREE_RETURN_LIMIT);
            Assert::AreEqual(0, MIN_PUSH_DISTANCE);
            Assert::AreEqual(1000, MAX_PUSH_DISTANCE);
        }
    };

    TEST_CLASS (PushDistanceClamp)
    {
    public:
        TEST_METHOD (SettingsValuesAreClampedToZeroThroughOneThousand)
        {
            struct ClampCase
            {
                const wchar_t* name;
                double raw;
                int expected;
            };

            const ClampCase cases[] = {
                { L"default", 300, 300 },
                { L"zero", 0, 0 },
                { L"upper bound kept", 1000, 1000 },
                { L"one past the upper bound", 1001, 1000 },
                { L"just past the upper bound", 1000.5, 1000 },
                { L"far past the upper bound", 1e12, 1000 },
                { L"just under the upper bound truncates", 999.9, 999 },
                { L"negative", -1, 0 },
                { L"far negative", -1e12, 0 },
            };

            std::wstring failures;
            for (const ClampCase& c : cases)
            {
                const int actual = ClampPushDistance(c.raw);
                if (actual != c.expected)
                {
                    failures += (failures.empty() ? L"" : L"; ") + std::wstring(c.name) + L": expected " + std::to_wstring(c.expected) + L", got " + std::to_wstring(actual);
                }
            }
            if (!failures.empty())
            {
                Assert::Fail(failures.c_str());
            }
        }
    };

    TEST_CLASS (SourceScaledThreshold)
    {
    public:
        TEST_METHOD (ScaledThresholdIsTheDistanceTimesTheSourceDpi)
        {
            // In physical pixels times 96: distance * dpi. A negative distance counts as 0.
            struct ThresholdCase
            {
                const wchar_t* name;
                int distance;
                unsigned int dpi;
                uint64_t expected;
            };

            const ThresholdCase cases[] = {
                { L"25 at 100%", 25, DPI_100, 25 * 96 },
                { L"25 at 150%", 25, DPI_150, 25 * 144 },
                { L"25 at 200%", 25, DPI_200, 25 * 192 },
                { L"the cap at 150%", MOVE_CAP, DPI_150, 10 * 144 },
                { L"the free-return window at 150%", FREE_RETURN_LIMIT, DPI_150, 100 * 144 },
                { L"zero", 0, DPI_200, 0 },
                { L"negative", -5, DPI_150, 0 },
            };

            std::wstring failures;
            for (const ThresholdCase& c : cases)
            {
                const uint64_t actual = ScaledThreshold(c.distance, c.dpi);
                if (actual != c.expected)
                {
                    failures += (failures.empty() ? L"" : L"; ") + std::wstring(c.name) + L": expected " + std::to_wstring(c.expected) + L", got " + std::to_wstring(actual);
                }
            }
            if (!failures.empty())
            {
                Assert::Fail(failures.c_str());
            }
        }
    };

    TEST_CLASS (InMonitorMoves)
    {
    public:
        TEST_METHOD (MovesWithinOneMonitorAreAllowedUnchanged)
        {
            RunAll({
                { L"interior move", SideBySide(), Defaults(), { Expect(Move({ 100, 100 }, { 200, 150 }), Allow({ 200, 150 })) } },
                { L"up to the edge column", SideBySide(), Defaults(), { Expect(Move({ 1900, 500 }, { 1919, 500 }), Allow({ 1919, 500 })) } },
                { L"along the shared edge", SideBySide(), Defaults(), { Expect(Move({ 1919, 100 }, { 1919, 900 }), Allow({ 1919, 900 })) } },
                { L"on the right monitor", SideBySide(), Defaults(), { Expect(Move({ 2000, 500 }, { 3000, 800 }), Allow({ 3000, 800 })) } },
                { L"stacked, up to the edge row", Stacked(), Defaults(), { Expect(Move({ 500, 1000 }, { 500, 1079 }), Allow({ 500, 1079 })) } },
            });
        }
    };

    TEST_CLASS (Push)
    {
    public:
        TEST_METHOD (PushBelowThresholdHoldsAtTheEdge)
        {
            RunAll({
                { L"straight push", SideBySide(), Defaults(), { Expect(Move({ 1919, 500 }, { 1929, 500 }), Hold({ 1919, 500 })) } },
                { L"push keeps the along-edge motion", SideBySide(), Defaults(), { Expect(Move({ 1919, 500 }, { 1929, 510 }), Hold({ 1919, 510 })) } },
                { L"leftward push", SideBySide(), Defaults(), { Expect(Move({ 1920, 500 }, { 1910, 500 }), Hold({ 1920, 500 })) } },
                { L"downward push", Stacked(), Defaults(), { Expect(Move({ 500, 1079 }, { 500, 1091 }), Hold({ 500, 1079 })) } },
                { L"upward push", Stacked(), Defaults(), { Expect(Move({ 500, 1080 }, { 500, 1056 }), Hold({ 500, 1080 })) } },
            });
        }

        TEST_METHOD (CumulativePushCrossesOnReachingTheThreshold)
        {
            RunAll({
                { L"rightward, 10 + 10 + 4 + 1",
                  SideBySide(),
                  Defaults(),
                  {
                      PushTen(),
                      PushTen(),
                      Expect(Move({ 1919, 500 }, { 1923, 500 }), Hold({ 1919, 500 })),
                      Expect(Move({ 1919, 500 }, { 1920, 500 }), Allow({ 1920, 500 })),
                  } },
                { L"leftward, 10 + 10 + 5",
                  SideBySide(),
                  Defaults(),
                  {
                      Expect(Move({ 1920, 500 }, { 1910, 500 }), Hold({ 1920, 500 })),
                      Expect(Move({ 1920, 500 }, { 1910, 500 }), Hold({ 1920, 500 })),
                      Expect(Move({ 1920, 500 }, { 1915, 500 }), Allow({ 1915, 500 })),
                  } },
                { L"downward, 10 + 10 + 5",
                  Stacked(),
                  Defaults(),
                  {
                      Expect(Move({ 500, 1079 }, { 500, 1089 }), Hold({ 500, 1079 })),
                      Expect(Move({ 500, 1079 }, { 500, 1089 }), Hold({ 500, 1079 })),
                      Expect(Move({ 500, 1079 }, { 500, 1084 }), Allow({ 500, 1084 })),
                  } },
                { L"upward, 10 + 10 + 5",
                  Stacked(),
                  Defaults(),
                  {
                      Expect(Move({ 500, 1080 }, { 500, 1070 }), Hold({ 500, 1080 })),
                      Expect(Move({ 500, 1080 }, { 500, 1070 }), Hold({ 500, 1080 })),
                      Expect(Move({ 500, 1080 }, { 500, 1075 }), Allow({ 500, 1075 })),
                  } },
            });
        }

        TEST_METHOD (OnlyTheOvershootPastTheEdgeCounts)
        {
            // From x = 1900 to 1924 travels 24 px but only 5 of them lie past the edge column, so the
            // push is 5, 15, 24 and crosses at 25. Counting the travel would cross a move earlier.
            RunAll({
                { L"approach from inside",
                  SideBySide(),
                  Defaults(),
                  {
                      Expect(Move({ 1900, 500 }, { 1924, 500 }), Hold({ 1919, 500 })),
                      Expect(Move({ 1919, 500 }, { 1929, 500 }), Hold({ 1919, 500 })),
                      Expect(Move({ 1919, 500 }, { 1928, 500 }), Hold({ 1919, 500 })),
                      Expect(Move({ 1919, 500 }, { 1920, 500 }), Allow({ 1920, 500 })),
                  } },
            });
        }

        TEST_METHOD (MoveAwayFromTheEdgeDrainsThePush)
        {
            // A move's component away from the pushed edge comes off the push, in physical
            // pixels. Each case backs off part of a 20 px push and crosses on exactly what is left
            // short of 25. A reset would leave the last step holding.
            RunAll({
                { L"one pixel back costs one pixel",
                  SideBySide(),
                  Defaults(),
                  {
                      PushTen(),
                      PushTen(),
                      Expect(Move({ 1919, 500 }, { 1918, 500 }), Allow({ 1918, 500 })),
                      Expect(Move({ 1918, 500 }, { 1924, 500 }), Hold({ 1919, 500 })),
                      Expect(Move({ 1919, 500 }, { 1920, 500 }), Allow({ 1920, 500 })),
                  } },
                { L"three steps back cost their sum",
                  SideBySide(),
                  Defaults(),
                  {
                      PushTen(),
                      PushTen(),
                      Expect(Move({ 1919, 500 }, { 1916, 500 }), Allow({ 1916, 500 })),
                      Expect(Move({ 1916, 500 }, { 1913, 500 }), Allow({ 1913, 500 })),
                      Expect(Move({ 1913, 500 }, { 1910, 500 }), Allow({ 1910, 500 })),
                      Expect(Move({ 1910, 500 }, { 1929, 500 }), Hold({ 1919, 500 })),
                      Expect(Move({ 1919, 500 }, { 1923, 500 }), Allow({ 1923, 500 })),
                  } },
                { L"diagonal with a component away",
                  SideBySide(),
                  Defaults(),
                  {
                      PushTen(),
                      PushTen(),
                      Expect(Move({ 1919, 500 }, { 1918, 520 }), Allow({ 1918, 520 })),
                      Expect(Move({ 1918, 520 }, { 1924, 520 }), Hold({ 1919, 520 })),
                      Expect(Move({ 1919, 520 }, { 1920, 520 }), Allow({ 1920, 520 })),
                  } },
                { L"leftward, one pixel back",
                  SideBySide(),
                  Defaults(),
                  {
                      Expect(Move({ 1920, 500 }, { 1910, 500 }), Hold({ 1920, 500 })),
                      Expect(Move({ 1920, 500 }, { 1910, 500 }), Hold({ 1920, 500 })),
                      Expect(Move({ 1920, 500 }, { 1921, 500 }), Allow({ 1921, 500 })),
                      Expect(Move({ 1921, 500 }, { 1915, 500 }), Hold({ 1920, 500 })),
                      Expect(Move({ 1920, 500 }, { 1919, 500 }), Allow({ 1919, 500 })),
                  } },
                { L"stacked, one pixel up",
                  Stacked(),
                  Defaults(),
                  {
                      Expect(Move({ 500, 1079 }, { 500, 1089 }), Hold({ 500, 1079 })),
                      Expect(Move({ 500, 1079 }, { 500, 1089 }), Hold({ 500, 1079 })),
                      Expect(Move({ 500, 1079 }, { 500, 1078 }), Allow({ 500, 1078 })),
                      Expect(Move({ 500, 1078 }, { 500, 1084 }), Hold({ 500, 1079 })),
                      Expect(Move({ 500, 1079 }, { 500, 1080 }), Allow({ 500, 1080 })),
                  } },
                // Leaving B (150%) the threshold is 37.5 px and the cap 15: 15 + 15 - 6 + 13 = 37
                // holds and 1 more crosses. Draining 6 logical px, 9 physical, would leave 35.
                { L"150% source drains physical pixels",
                  MixedDpi(),
                  Defaults(),
                  {
                      Expect(Move({ 1920, 500 }, { 1900, 500 }), Hold({ 1920, 500 })),
                      Expect(Move({ 1920, 500 }, { 1900, 500 }), Hold({ 1920, 500 })),
                      Expect(Move({ 1920, 500 }, { 1926, 500 }), Allow({ 1926, 500 })),
                      Expect(Move({ 1926, 500 }, { 1907, 500 }), Hold({ 1920, 500 })),
                      Expect(Move({ 1920, 500 }, { 1919, 500 }), Allow({ 1919, 500 })),
                  } },
            });
        }

        TEST_METHOD (BackingOffAsFarAsThePushDrainsItToZero)
        {
            // The drain stops at zero, so the next push needs the full 25. A push that went
            // negative would hold past 25, and one that wrapped around would cross at once.
            RunAll({
                { L"back exactly the push",
                  SideBySide(),
                  Defaults(),
                  {
                      PushTen(),
                      PushTen(),
                      Expect(Move({ 1919, 500 }, { 1899, 500 }), Allow({ 1899, 500 })),
                      Expect(Move({ 1899, 500 }, { 1919, 500 }), Allow({ 1919, 500 })),
                      PushTen(),
                      PushTen(),
                      Expect(Move({ 1919, 500 }, { 1924, 500 }), Allow({ 1924, 500 })),
                  } },
                { L"back farther than the push",
                  SideBySide(),
                  Defaults(),
                  {
                      PushTen(),
                      PushTen(),
                      Expect(Move({ 1919, 500 }, { 1889, 500 }), Allow({ 1889, 500 })),
                      Expect(Move({ 1889, 500 }, { 1919, 500 }), Allow({ 1919, 500 })),
                      PushTen(),
                      PushTen(),
                      Expect(Move({ 1919, 500 }, { 1924, 500 }), Allow({ 1924, 500 })),
                  } },
                { L"far back across the monitor",
                  SideBySide(),
                  Defaults(),
                  {
                      PushTen(),
                      PushTen(),
                      Expect(Move({ 1919, 500 }, { 0, 500 }), Allow({ 0, 500 })),
                      Expect(Move({ 0, 500 }, { 1919, 500 }), Allow({ 1919, 500 })),
                      PushTen(),
                      PushTen(),
                      Expect(Move({ 1919, 500 }, { 1924, 500 }), Allow({ 1924, 500 })),
                  } },
            });
        }

        TEST_METHOD (MoveAlongTheEdgeKeepsThePush)
        {
            RunAll({
                { L"slide along, then finish the push",
                  SideBySide(),
                  Defaults(),
                  {
                      PushTen(),
                      PushTen(),
                      Expect(Move({ 1919, 500 }, { 1919, 540 }), Allow({ 1919, 540 })),
                      Expect(Move({ 1919, 540 }, { 1924, 540 }), Allow({ 1924, 540 })),
                  } },
            });
        }

        TEST_METHOD (TangentialMovesWhilePinnedKeepThePush)
        {
            // Only a component away from the pushed edge drains the push. A move straight
            // along the edge has none, so 20 px of push survives it and 5 more cross.
            RunAll({
                { L"right edge, slide up",
                  SideBySide(),
                  Defaults(),
                  {
                      PushTen(),
                      PushTen(),
                      Expect(Move({ 1919, 500 }, { 1919, 450 }), Allow({ 1919, 450 })),
                      Expect(Move({ 1919, 450 }, { 1924, 450 }), Allow({ 1924, 450 })),
                  } },
                { L"right edge, two slides",
                  SideBySide(),
                  Defaults(),
                  {
                      PushTen(),
                      PushTen(),
                      Expect(Move({ 1919, 500 }, { 1919, 600 }), Allow({ 1919, 600 })),
                      Expect(Move({ 1919, 600 }, { 1919, 580 }), Allow({ 1919, 580 })),
                      Expect(Move({ 1919, 580 }, { 1924, 580 }), Allow({ 1924, 580 })),
                  } },
                { L"left edge, slide down",
                  SideBySide(),
                  Defaults(),
                  {
                      Expect(Move({ 1920, 500 }, { 1910, 500 }), Hold({ 1920, 500 })),
                      Expect(Move({ 1920, 500 }, { 1910, 500 }), Hold({ 1920, 500 })),
                      Expect(Move({ 1920, 500 }, { 1920, 560 }), Allow({ 1920, 560 })),
                      Expect(Move({ 1920, 560 }, { 1915, 560 }), Allow({ 1915, 560 })),
                  } },
                { L"bottom edge, slide left",
                  Stacked(),
                  Defaults(),
                  {
                      Expect(Move({ 500, 1079 }, { 500, 1089 }), Hold({ 500, 1079 })),
                      Expect(Move({ 500, 1079 }, { 500, 1089 }), Hold({ 500, 1079 })),
                      Expect(Move({ 500, 1079 }, { 440, 1079 }), Allow({ 440, 1079 })),
                      Expect(Move({ 440, 1079 }, { 440, 1084 }), Allow({ 440, 1084 })),
                  } },
                { L"top edge, slide right",
                  Stacked(),
                  Defaults(),
                  {
                      Expect(Move({ 500, 1080 }, { 500, 1070 }), Hold({ 500, 1080 })),
                      Expect(Move({ 500, 1080 }, { 500, 1070 }), Hold({ 500, 1080 })),
                      Expect(Move({ 500, 1080 }, { 560, 1080 }), Allow({ 560, 1080 })),
                      Expect(Move({ 560, 1080 }, { 560, 1075 }), Allow({ 560, 1075 })),
                  } },
            });
        }

        TEST_METHOD (PushOnAnotherEdgeStartsOver)
        {
            // A push on a new side carries the old amount less the cursor's distance from the
            // corner pixel joining the two sides, so far from that corner it starts over.
            //  - 20 px against the right edge at y = 1000 is 79 px from the corner, so the bottom
            //    push starts at 5 and crosses at 25, two moves later than a carried push would.
            //  - 4 px from the corner, 16 of the 20 carry: the bottom push reaches 21, and 4 more
            //    cross.
            RunAll({
                { L"right, then bottom, far from the corner",
                  Junction(),
                  Defaults(),
                  {
                      Expect(Move({ 1919, 1000 }, { 1929, 1000 }), Hold({ 1919, 1000 })),
                      Expect(Move({ 1919, 1000 }, { 1929, 1000 }), Hold({ 1919, 1000 })),
                      Expect(Move({ 1919, 1000 }, { 1919, 1084 }), Hold({ 1919, 1079 })),
                      Expect(Move({ 1919, 1079 }, { 1919, 1089 }), Hold({ 1919, 1079 })),
                      Expect(Move({ 1919, 1079 }, { 1919, 1089 }), Allow({ 1919, 1089 })),
                  } },
                { L"right, then bottom, 4 px from the corner",
                  Junction(),
                  Defaults(),
                  {
                      Expect(Move({ 1919, 1075 }, { 1929, 1075 }), Hold({ 1919, 1075 })),
                      Expect(Move({ 1919, 1075 }, { 1929, 1075 }), Hold({ 1919, 1075 })),
                      Expect(Move({ 1919, 1075 }, { 1919, 1084 }), Hold({ 1919, 1079 })),
                      Expect(Move({ 1919, 1079 }, { 1919, 1083 }), Allow({ 1919, 1083 })),
                  } },
            });
        }

        TEST_METHOD (PushTowardAnotherNeighborOnTheSameSideKeepsThePush)
        {
            // The push belongs to the source monitor and the side. B and C both lie past
            // A's right side, so a push toward one counts toward the other.
            RunAll({
                { L"slide from B's rows to C's",
                  TwoOnTheRight(),
                  Defaults(),
                  {
                      PushTen(),
                      PushTen(),
                      Expect(Move({ 1919, 500 }, { 1919, 1500 }), Allow({ 1919, 1500 })),
                      Expect(Move({ 1919, 1500 }, { 1924, 1500 }), Allow({ 1924, 1500 })),
                  } },
                { L"alternating between B and C at their shared corner",
                  TwoOnTheRight(),
                  Defaults(),
                  {
                      Expect(Move({ 1919, 1079 }, { 1929, 1079 }), Hold({ 1919, 1079 })),
                      Expect(Move({ 1919, 1079 }, { 1929, 1080 }), Hold({ 1919, 1080 })),
                      Expect(Move({ 1919, 1080 }, { 1924, 1079 }), Allow({ 1924, 1079 })),
                  } },
            });
        }

        TEST_METHOD (MoveIntoAGapResetsThePush)
        {
            // The move into the gap leaves Windows to clamp the cursor at A's edge; the push toward B
            // starts over after it.
            RunAll({
                { L"push, slide up, push into the gap, slide back",
                  Offset(),
                  Defaults(),
                  {
                      Expect(Move({ 1919, 600 }, { 1929, 600 }), Hold({ 1919, 600 })),
                      Expect(Move({ 1919, 600 }, { 1929, 600 }), Hold({ 1919, 600 })),
                      Expect(Move({ 1919, 600 }, { 1919, 200 }), Allow({ 1919, 200 })),
                      Expect(Move({ 1919, 200 }, { 1949, 200 }), Allow({ 1949, 200 })),
                      Expect(Move({ 1919, 200 }, { 1919, 600 }), Allow({ 1919, 600 })),
                      Expect(Move({ 1919, 600 }, { 1924, 600 }), Hold({ 1919, 600 })),
                  } },
            });
        }

        TEST_METHOD (ReachingAnotherMonitorResetsThePush)
        {
            // The stacked edges are filtered out, so the cursor changes monitor without a braked
            // crossing. Both cases push the right side before and after, and the second one the same
            // neighbor too, so only the monitor change can reset the push.
            RunAll({
                { L"junction, toward a different neighbor",
                  Junction(),
                  WithFilter(EdgeFilter::SideBySide),
                  {
                      Expect(Move({ 1919, 1000 }, { 1929, 1000 }), Hold({ 1919, 1000 })),
                      Expect(Move({ 1919, 1000 }, { 1929, 1000 }), Hold({ 1919, 1000 })),
                      Expect(Move({ 1919, 1000 }, { 1919, 1200 }), Allow({ 1919, 1200 })),
                      Expect(Move({ 1919, 1200 }, { 1924, 1200 }), Hold({ 1919, 1200 })),
                  } },
                { L"toward the same neighbor",
                  TwoOnTheLeft(),
                  WithFilter(EdgeFilter::SideBySide),
                  {
                      Expect(Move({ 1919, 1000 }, { 1929, 1000 }), Hold({ 1919, 1000 })),
                      Expect(Move({ 1919, 1000 }, { 1929, 1000 }), Hold({ 1919, 1000 })),
                      Expect(Move({ 1919, 1000 }, { 1919, 1200 }), Allow({ 1919, 1200 })),
                      Expect(Move({ 1919, 1200 }, { 1924, 1200 }), Hold({ 1919, 1200 })),
                  } },
            });
        }
    };

    TEST_CLASS (MoveCapRule)
    {
    public:
        TEST_METHOD (EachMoveAddsAtMostTheCap)
        {
            // Baseline push 25 with the 10 px cap at 100%: a 25 px overshoot adds 10, so the push
            // crosses on the third move (10 + 10 + 5), not the first.
            RunAll({
                { L"overshoot past the cap",
                  SideBySide(),
                  Defaults(),
                  {
                      Expect(Move({ 1900, 500 }, { 1944, 500 }), Hold({ 1919, 500 })),
                      Expect(Move({ 1919, 500 }, { 1944, 500 }), Hold({ 1919, 500 })),
                      Expect(Move({ 1919, 500 }, { 1924, 500 }), Allow({ 1924, 500 })),
                  } },
                { L"overshoot equal to the cap", SideBySide(), Defaults(), SlowPush({ 1919, 500 }, 1, 0, 25, { 10 }) },
                { L"stacked, overshoot past the cap", Stacked(), Defaults(), SlowPush({ 500, 1079 }, 0, 1, 25, { 40 }) },
                { L"leftward, overshoot past the cap", SideBySide(), Defaults(), SlowPush({ 1920, 500 }, -1, 0, 25, { 30 }) },
            });
        }

        TEST_METHOD (AThousandPixelFlingHoldsAtTheShippingDefaults)
        {
            // Settings{}: push 300, cap 10. A 1000 px move adds 10, so it holds; crossing takes 30
            // such moves.
            RunAll({
                { L"from the edge pixel", SideBySide(), Settings{}, { Expect(Move({ 1919, 500 }, { 2919, 500 }), Hold({ 1919, 500 })) } },
                { L"from 1000 px inside", SideBySide(), Settings{}, { Expect(Move({ 919, 500 }, { 2919, 500 }), Hold({ 1919, 500 })) } },
                { L"stacked, downward", Stacked(), Settings{}, { Expect(Move({ 500, 1079 }, { 500, 2079 }), Hold({ 500, 1079 })) } },
                { L"thirty flings to cross", SideBySide(), Settings{}, SlowPush({ 1919, 500 }, 1, 0, 300, { 1000 }) },
            });
        }

        TEST_METHOD (SlowPushIsUnchangedByTheCap)
        {
            // Steps no larger than the cap add their whole overshoot, so the push crosses exactly at
            // 300.
            RunAll({
                { L"1-10 px steps", SideBySide(), Settings{}, SlowPush({ 1919, 500 }, 1, 0, 300, { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 }) },
                { L"5 px steps, stacked", Stacked(), Settings{}, SlowPush({ 500, 1079 }, 0, 1, 300, { 5 }) },
            });
        }

        TEST_METHOD (TheCapScalesLikeTheThreshold)
        {
            // MixedDpi, push 25. Leaving B (150%), the threshold is 37.5 px and the cap 15 px: 40 px
            // moves add 15, 30, 45, crossing on the third. An unscaled 10 px cap would need four.
            RunAll({
                { L"B to C",
                  MixedDpi(),
                  Defaults(),
                  {
                      Expect(Move({ 4799, 800 }, { 4839, 800 }), Hold({ 4799, 800 })),
                      Expect(Move({ 4799, 800 }, { 4839, 800 }), Hold({ 4799, 800 })),
                      Expect(Move({ 4799, 800 }, { 4839, 800 }), Allow({ 4839, 800 })),
                  } },
            });
        }
    };

    TEST_CLASS (Bypass)
    {
    public:
        TEST_METHOD (BypassedMovesCrossFreely)
        {
            RunAll({
                { L"cross modifier held", SideBySide(), Defaults(), { Expect(ModifierMove({ 1919, 500 }, { 1925, 500 }), Allow({ 1925, 500 })) } },
                { L"absolute input", SideBySide(), Defaults(), { Expect(AbsoluteMove({ 1919, 500 }, { 1925, 500 }), Allow({ 1925, 500 })) } },
                { L"absolute jump across monitors", SideBySide(), Defaults(), { Expect(AbsoluteMove({ 100, 100 }, { 3000, 900 }), Allow({ 3000, 900 })) } },
                { L"dragging with disable-while-dragging on", SideBySide(), WithDragDisable(), { Expect(DragMove({ 1919, 500 }, { 1925, 500 }), Allow({ 1925, 500 })) } },
            });
        }

        TEST_METHOD (BypassedMovesResetThePush)
        {
            // After the bypassed slide along the edge, a 5 px push holds; with the push kept it
            // would reach 25 and cross.
            RunAll({
                { L"cross modifier held",
                  SideBySide(),
                  Defaults(),
                  {
                      PushTen(),
                      PushTen(),
                      Expect(ModifierMove({ 1919, 500 }, { 1919, 501 }), Allow({ 1919, 501 })),
                      Expect(Move({ 1919, 501 }, { 1924, 501 }), Hold({ 1919, 501 })),
                  } },
                { L"absolute input",
                  SideBySide(),
                  Defaults(),
                  {
                      PushTen(),
                      PushTen(),
                      Expect(AbsoluteMove({ 1919, 500 }, { 1919, 501 }), Allow({ 1919, 501 })),
                      Expect(Move({ 1919, 501 }, { 1924, 501 }), Hold({ 1919, 501 })),
                  } },
                { L"dragging with disable-while-dragging on",
                  SideBySide(),
                  WithDragDisable(),
                  {
                      PushTen(),
                      PushTen(),
                      Expect(DragMove({ 1919, 500 }, { 1919, 501 }), Allow({ 1919, 501 })),
                      Expect(Move({ 1919, 501 }, { 1924, 501 }), Hold({ 1919, 501 })),
                  } },
            });
        }

        TEST_METHOD (DraggingBrakesUnlessTheSettingIsOn)
        {
            RunAll({
                { L"dragging with the setting off", SideBySide(), Defaults(), { Expect(DragMove({ 1919, 500 }, { 1925, 500 }), Hold({ 1919, 500 })) } },
                { L"not dragging with the setting on", SideBySide(), WithDragDisable(), { Expect(Move({ 1919, 500 }, { 1925, 500 }), Hold({ 1919, 500 })) } },
            });
        }

        TEST_METHOD (SingleMonitorAllowsEverything)
        {
            RunAll({
                { L"past the right edge", Single(), Defaults(), { Expect(Move({ 1919, 500 }, { 1925, 500 }), Allow({ 1925, 500 })) } },
                { L"past the corner", Single(), Defaults(), { Expect(Move({ 0, 0 }, { -5, -5 }), Allow({ -5, -5 })) } },
                { L"interior", Single(), Defaults(), { Expect(Move({ 100, 100 }, { 50, 60 }), Allow({ 50, 60 })) } },
            });
        }
    };

    TEST_CLASS (EdgeFilterTests)
    {
    public:
        TEST_METHOD (FilterBrakesOnlyItsOwnAxis)
        {
            RunAll({
                { L"side-by-side filter, side-by-side edge", SideBySide(), WithFilter(EdgeFilter::SideBySide), { Expect(Move({ 1919, 500 }, { 1925, 500 }), Hold({ 1919, 500 })) } },
                { L"side-by-side filter, stacked edge", Stacked(), WithFilter(EdgeFilter::SideBySide), { Expect(Move({ 500, 1079 }, { 500, 1085 }), Allow({ 500, 1085 })) } },
                { L"stacked filter, stacked edge", Stacked(), WithFilter(EdgeFilter::Stacked), { Expect(Move({ 500, 1079 }, { 500, 1085 }), Hold({ 500, 1079 })) } },
                { L"stacked filter, side-by-side edge", SideBySide(), WithFilter(EdgeFilter::Stacked), { Expect(Move({ 1919, 500 }, { 1925, 500 }), Allow({ 1925, 500 })) } },
                { L"all filter, both edges",
                  Junction(),
                  WithFilter(EdgeFilter::All),
                  {
                      Expect(Move({ 1919, 500 }, { 1925, 500 }), Hold({ 1919, 500 })),
                      Expect(Move({ 1919, 500 }, { 1910, 500 }), Allow({ 1910, 500 })),
                      Expect(Move({ 500, 1079 }, { 500, 1085 }), Hold({ 500, 1079 })),
                  } },
            });
        }
    };

    TEST_CLASS (UnknownSettingValues)
    {
    public:
        TEST_METHOD (UnknownFilterValueBrakesBothAxes)
        {
            // A hand-edited settings file can carry any number; the engine reads it as All.
            RunAll({
                { L"filter value 7",
                  Junction(),
                  WithFilter(static_cast<EdgeFilter>(7)),
                  {
                      Expect(Move({ 1919, 500 }, { 1925, 500 }), Hold({ 1919, 500 })),
                      Expect(Move({ 1919, 500 }, { 1910, 500 }), Allow({ 1910, 500 })),
                      Expect(Move({ 500, 1079 }, { 500, 1085 }), Hold({ 500, 1079 })),
                  } },
            });
        }
    };

    TEST_CLASS (Dpi)
    {
    public:
        TEST_METHOD (ThresholdScalesWithTheSourceMonitorDpi)
        {
            // 25 logical px is 25 physical px at 100%, 37.5 at 150% and 50 at 200%. 5 px steps hold
            // until their sum reaches that, and the cap (10, 15, 20 px) never limits them.
            RunAll({
                { L"100% source (A to B)", MixedDpi(), Defaults(), SlowPush({ 1919, 500 }, 1, 0, 25, { 5 }, CapAt(DPI_100)) },
                { L"150% source (B to C)", MixedDpi(), Defaults(), SlowPush({ 4799, 800 }, 1, 0, 38, { 5 }, CapAt(DPI_150)) },
                { L"150% source (B to A)", MixedDpi(), Defaults(), SlowPush({ 1920, 500 }, -1, 0, 38, { 5 }, CapAt(DPI_150)) },
                { L"200% source (C to B)", MixedDpi(), Defaults(), SlowPush({ 4800, 800 }, -1, 0, 50, { 5 }, CapAt(DPI_200)) },
                { L"200% source, 10 px setting (C to B)", MixedDpi(), WithDistance(10), SlowPush({ 4800, 800 }, -1, 0, 20, { 5 }, CapAt(DPI_200)) },
            });
        }

        TEST_METHOD (HandTravelMatchesBothWaysOverEveryMixedDpiEdge)
        {
            // Windows moves the pointer about dpi / 96 px per mouse count, so a hand step of two
            // counts is 2 px at 100%, 3 px at 150% and 4 px at 200%. With the threshold scaled by
            // the source, 25 logical px takes 13 such steps from either side of every edge:
            // 13 x 2 = 26 >= 25, 13 x 3 = 39 >= 37.5, 13 x 4 = 52 >= 50, and 12 steps fall short.
            RunAll({
                { L"A (100%) to B (150%)", MixedDpi(), Defaults(), CrossOnStep({ 1919, 500 }, 1, 0, 2, 13) },
                { L"B (150%) to A (100%)", MixedDpi(), Defaults(), CrossOnStep({ 1920, 500 }, -1, 0, 3, 13) },
                { L"B (150%) to C (200%)", MixedDpi(), Defaults(), CrossOnStep({ 4799, 800 }, 1, 0, 3, 13) },
                { L"C (200%) to B (150%)", MixedDpi(), Defaults(), CrossOnStep({ 4800, 800 }, -1, 0, 4, 13) },
                { L"primary (100%) to right (150%)", FourAround(DPI_150), Defaults(), CrossOnStep({ 1919, 500 }, 1, 0, 2, 13) },
                { L"right (150%) to primary (100%)", FourAround(DPI_150), Defaults(), CrossOnStep({ 1920, 500 }, -1, 0, 3, 13) },
            });
        }
    };

    TEST_CLASS (OuterEdgesAndGaps)
    {
    public:
        TEST_METHOD (MovesOutsideEveryMonitorAreUntouched)
        {
            RunAllWithAndWithoutTheBoundingRect({
                { L"outer right edge", SideBySide(), Defaults(), { Expect(Move({ 3839, 500 }, { 3850, 500 }), Allow({ 3850, 500 })) } },
                { L"outer left edge", SideBySide(), Defaults(), { Expect(Move({ 0, 500 }, { -10, 500 }), Allow({ -10, 500 })) } },
                { L"outer top edge", SideBySide(), Defaults(), { Expect(Move({ 500, 0 }, { 500, -5 }), Allow({ 500, -5 })) } },
                { L"outer bottom edge", SideBySide(), Defaults(), { Expect(Move({ 2500, 1079 }, { 2500, 1090 }), Allow({ 2500, 1090 })) } },
                { L"repeated pushes at an outer edge",
                  SideBySide(),
                  Defaults(),
                  {
                      Expect(Move({ 3839, 500 }, { 3849, 500 }), Allow({ 3849, 500 })),
                      Expect(Move({ 3839, 500 }, { 3849, 500 }), Allow({ 3849, 500 })),
                      Expect(Move({ 3839, 500 }, { 3849, 500 }), Allow({ 3849, 500 })),
                  } },
            });
        }

        TEST_METHOD (CloneMonitorsHaveNoInnerEdge)
        {
            // Two identical rects, as duplicated displays report. A move off the rect lands on no
            // monitor (an outer edge), and every point inside belongs to both, so no move crosses
            // from one monitor onto another.
            const Layout clone{ { Mon(0, 0, 1920, 1080), Mon(0, 0, 1920, 1080) } };
            RunAllWithAndWithoutTheBoundingRect({
                { L"repeated pushes past the right edge",
                  clone,
                  Defaults(),
                  {
                      Expect(Move({ 1919, 500 }, { 1929, 500 }), Allow({ 1929, 500 })),
                      Expect(Move({ 1919, 500 }, { 1929, 500 }), Allow({ 1929, 500 })),
                      Expect(Move({ 1919, 500 }, { 1929, 500 }), Allow({ 1929, 500 })),
                  } },
                { L"past the left edge", clone, Defaults(), { Expect(Move({ 0, 500 }, { -10, 500 }), Allow({ -10, 500 })) } },
                { L"past the top edge", clone, Defaults(), { Expect(Move({ 500, 0 }, { 500, -10 }), Allow({ 500, -10 })) } },
                { L"past the bottom edge", clone, Defaults(), { Expect(Move({ 500, 1079 }, { 500, 1089 }), Allow({ 500, 1089 })) } },
                { L"interior move", clone, Defaults(), { Expect(Move({ 100, 100 }, { 1800, 1000 }), Allow({ 1800, 1000 })) } },
            });
        }

        TEST_METHOD (MovesLandingInAGapAreUntouched)
        {
            RunAllWithAndWithoutTheBoundingRect({
                { L"beside the offset monitor", Offset(), Defaults(), { Expect(Move({ 1919, 200 }, { 1930, 200 }), Allow({ 1930, 200 })) } },
                { L"below the upper monitor", Offset(), Defaults(), { Expect(Move({ 1925, 1500 }, { 1910, 1500 }), Allow({ 1910, 1500 })) } },
                { L"into the strip, rightward", Gap(), Defaults(), { Expect(Move({ 1919, 500 }, { 1930, 500 }), Allow({ 1930, 500 })) } },
                { L"into the strip, leftward", Gap(), Defaults(), { Expect(Move({ 2000, 500 }, { 1990, 500 }), Allow({ 1990, 500 })) } },
            });
        }
    };

    TEST_CLASS (FreeReturn)
    {
    public:
        TEST_METHOD (CrossingBackRightAfterACrossingIsFree)
        {
            // Without the free return each step back would push 5 (or 1) and hold at B's edge.
            RunAll({
                { L"five pixels back", SideBySide(), Defaults(), Concat(CrossToB(), { Expect(Move({ 1924, 500 }, { 1915, 500 }), Allow({ 1915, 500 })) }) },
                { L"one pixel back", SideBySide(), Defaults(), Concat(CrossToB(), { Expect(Move({ 1924, 500 }, { 1919, 500 }), Allow({ 1919, 500 })) }) },
                { L"after a downward crossing", Stacked(), Defaults(), Concat(SlowPush({ 500, 1079 }, 0, 1, 25, { 10 }), { Expect(Move({ 500, 1089 }, { 500, 1075 }), Allow({ 500, 1075 })) }) },
            });
        }

        TEST_METHOD (FreeReturnIsUsedOnce)
        {
            RunAll({
                { L"the return does not re-arm itself",
                  SideBySide(),
                  Defaults(),
                  Concat(CrossToB(),
                         {
                             Expect(Move({ 1924, 500 }, { 1915, 500 }), Allow({ 1915, 500 })),
                             Expect(Move({ 1915, 500 }, { 1925, 500 }), Hold({ 1919, 500 })),
                         }) },
                { L"a second return over the same edge brakes",
                  SideBySide(),
                  Defaults(),
                  Concat(CrossToB(),
                         {
                             Expect(Move({ 1924, 500 }, { 1915, 500 }), Allow({ 1915, 500 })),
                             Expect(ModifierMove({ 1915, 500 }, { 1925, 500 }), Allow({ 1925, 500 })),
                             Expect(Move({ 1925, 500 }, { 1915, 500 }), Hold({ 1920, 500 })),
                         }) },
            });
        }

        TEST_METHOD (FreeReturnCoversOnlyTheEdgeJustCrossed)
        {
            RunAll({
                { L"crossing a different edge right after brakes",
                  Junction(),
                  Defaults(),
                  Concat(SlowPush({ 1919, 1000 }, 1, 0, 25, { 10 }),
                         {
                             Expect(Move({ 1929, 1000 }, { 1944, 1079 }), Allow({ 1944, 1079 })),
                             Expect(Move({ 1944, 1079 }, { 1944, 1085 }), Hold({ 1944, 1079 })),
                         }) },
            });
        }

        TEST_METHOD (FreeReturnExpiresByDistance)
        {
            // The window is FREE_RETURN_LIMIT logical px, 100 px at 100%. The allowance survives at
            // exactly 100 px from the edge and is gone at 101, and coming back closer does not
            // revive it. CrossToB lands at x = 1924.
            RunAll({
                { L"exactly at the limit", SideBySide(), Defaults(), Concat(CrossToB(), { Expect(Move({ 1924, 500 }, { 2020, 500 }), Allow({ 2020, 500 })), Expect(Move({ 2020, 500 }, { 1915, 500 }), Allow({ 1915, 500 })) }) },
                { L"one pixel past the limit", SideBySide(), Defaults(), Concat(CrossToB(), { Expect(Move({ 1924, 500 }, { 2021, 500 }), Allow({ 2021, 500 })), Expect(Move({ 2021, 500 }, { 1915, 500 }), Hold({ 1920, 500 })) }) },
                { L"out past the limit and back in",
                  SideBySide(),
                  Defaults(),
                  Concat(CrossToB(),
                         {
                             Expect(Move({ 1924, 500 }, { 2021, 500 }), Allow({ 2021, 500 })),
                             Expect(Move({ 2021, 500 }, { 1930, 500 }), Allow({ 1930, 500 })),
                             Expect(Move({ 1930, 500 }, { 1915, 500 }), Hold({ 1920, 500 })),
                         }) },
                { L"along the edge does not count as away", SideBySide(), Defaults(), Concat(CrossToB(), { Expect(Move({ 1924, 500 }, { 1924, 900 }), Allow({ 1924, 900 })), Expect(Move({ 1924, 900 }, { 1915, 900 }), Allow({ 1915, 900 })) }) },
            });
        }

        TEST_METHOD (FreeReturnDistanceUsesTheDestinationDpi)
        {
            // B is 150%, so the 100 logical px window measured on B is 150 px.
            RunAll({
                { L"150 px into B keeps it", MixedDpi(), Defaults(), Concat(CrossToB(), { Expect(Move({ 1924, 500 }, { 2070, 500 }), Allow({ 2070, 500 })), Expect(Move({ 2070, 500 }, { 1915, 500 }), Allow({ 1915, 500 })) }) },
                { L"151 px into B expires it", MixedDpi(), Defaults(), Concat(CrossToB(), { Expect(Move({ 1924, 500 }, { 2071, 500 }), Allow({ 2071, 500 })), Expect(Move({ 2071, 500 }, { 1915, 500 }), Hold({ 1920, 500 })) }) },
            });
        }

        TEST_METHOD (FreeReturnExpiresOnAnotherCrossing)
        {
            // The stacked edges are filtered out, so the trip down to BR and back moves no closer or
            // farther from the TL|TR edge; only the two crossings can end the allowance.
            RunAll({
                { L"down and back up over edges the filter ignores",
                  Junction(),
                  WithFilter(EdgeFilter::SideBySide),
                  Concat(SlowPush({ 1919, 1000 }, 1, 0, 25, { 10 }),
                         {
                             Expect(Move({ 1929, 1000 }, { 1929, 1085 }), Allow({ 1929, 1085 })),
                             Expect(Move({ 1929, 1085 }, { 1929, 1075 }), Allow({ 1929, 1075 })),
                             Expect(Move({ 1929, 1075 }, { 1915, 1075 }), Hold({ 1920, 1075 })),
                         }) },
            });
        }

        TEST_METHOD (BypassedMovesClearTheFreeReturn)
        {
            // The bypassed trip goes far from the edge and comes back within reach, which only the
            // clearing itself can catch.
            RunAll({
                { L"cross modifier held",
                  SideBySide(),
                  Defaults(),
                  Concat(CrossToB(),
                         {
                             Expect(ModifierMove({ 1924, 500 }, { 3800, 500 }), Allow({ 3800, 500 })),
                             Expect(ModifierMove({ 3800, 500 }, { 1925, 500 }), Allow({ 1925, 500 })),
                             Expect(Move({ 1925, 500 }, { 1915, 500 }), Hold({ 1920, 500 })),
                         }) },
                { L"absolute input",
                  SideBySide(),
                  Defaults(),
                  Concat(CrossToB(),
                         {
                             Expect(AbsoluteMove({ 1924, 500 }, { 3800, 500 }), Allow({ 3800, 500 })),
                             Expect(AbsoluteMove({ 3800, 500 }, { 1925, 500 }), Allow({ 1925, 500 })),
                             Expect(Move({ 1925, 500 }, { 1915, 500 }), Hold({ 1920, 500 })),
                         }) },
                { L"dragging with disable-while-dragging on",
                  SideBySide(),
                  WithDragDisable(),
                  Concat(CrossToB(),
                         {
                             Expect(DragMove({ 1924, 500 }, { 3800, 500 }), Allow({ 3800, 500 })),
                             Expect(DragMove({ 3800, 500 }, { 1925, 500 }), Allow({ 1925, 500 })),
                             Expect(Move({ 1925, 500 }, { 1915, 500 }), Hold({ 1920, 500 })),
                         }) },
            });
        }
    };

    TEST_CLASS (FreeReturnSetting)
    {
    public:
        TEST_METHOD (OffBrakesTheReturn)
        {
            RunAll({
                { L"five pixels back", SideBySide(), WithFreeReturn(false), Concat(CrossToB(), { Expect(Move({ 1924, 500 }, { 1915, 500 }), Hold({ 1920, 500 })) }) },
                { L"one pixel back", SideBySide(), WithFreeReturn(false), Concat(CrossToB(), { Expect(Move({ 1924, 500 }, { 1919, 500 }), Hold({ 1920, 500 })) }) },
            });
        }

        TEST_METHOD (TheWindowIsFixedRegardlessOfPushDistance)
        {
            // With a 200 px push the window is still 100 px. The slow crossings land at x = 1929.
            const std::vector<Step> crossWith200 = SlowPush({ 1919, 500 }, 1, 0, 200, { 10 });
            const std::vector<Step> crossWith300 = SlowPush({ 1919, 500 }, 1, 0, 300, { 10 });
            RunAll({
                { L"push 200, 100 px keeps it", SideBySide(), WithDistance(200), Concat(crossWith200, { Expect(Move({ 1929, 500 }, { 2020, 500 }), Allow({ 2020, 500 })), Expect(Move({ 2020, 500 }, { 1915, 500 }), Allow({ 1915, 500 })) }) },
                { L"push 200, 101 px expires it", SideBySide(), WithDistance(200), Concat(crossWith200, { Expect(Move({ 1929, 500 }, { 2021, 500 }), Allow({ 2021, 500 })), Expect(Move({ 2021, 500 }, { 1915, 500 }), Hold({ 1920, 500 })) }) },
                { L"shipping defaults, 100 px keeps it", SideBySide(), Settings{}, Concat(crossWith300, { Expect(Move({ 1929, 500 }, { 2020, 500 }), Allow({ 2020, 500 })), Expect(Move({ 2020, 500 }, { 1915, 500 }), Allow({ 1915, 500 })) }) },
                { L"shipping defaults, 101 px expires it", SideBySide(), Settings{}, Concat(crossWith300, { Expect(Move({ 1929, 500 }, { 2021, 500 }), Allow({ 2021, 500 })), Expect(Move({ 2021, 500 }, { 1915, 500 }), Hold({ 1920, 500 })) }) },
            });
        }
    };

    TEST_CLASS (CornerCases)
    {
    public:
        TEST_METHOD (DiagonalThroughAFourMonitorJunction)
        {
            // From TL's corner pixel straight through the junction into BR. The move leaves through
            // the right and bottom sides at once, so the hold sits in TL's corner.
            RunAll({
                { L"short diagonals hold, the push crosses at 25",
                  Junction(),
                  Defaults(),
                  {
                      Expect(Move({ 1919, 1079 }, { 1925, 1085 }), Hold({ 1919, 1079 })),
                      Expect(Move({ 1919, 1079 }, { 1929, 1089 }), Hold({ 1919, 1079 })),
                      Expect(Move({ 1919, 1079 }, { 1928, 1088 }), Allow({ 1928, 1088 })),
                  } },
                { L"side-by-side filter still brakes it", Junction(), WithFilter(EdgeFilter::SideBySide), { Expect(Move({ 1919, 1079 }, { 1925, 1085 }), Hold({ 1919, 1079 })) } },
                { L"stacked filter still brakes it", Junction(), WithFilter(EdgeFilter::Stacked), { Expect(Move({ 1919, 1079 }, { 1925, 1085 }), Hold({ 1919, 1079 })) } },
                { L"steep diagonal pushes on the larger overshoot",
                  Junction(),
                  Defaults(),
                  {
                      Expect(Move({ 1919, 1079 }, { 1922, 1089 }), Hold({ 1919, 1079 })),
                      Expect(Move({ 1919, 1079 }, { 1922, 1089 }), Hold({ 1919, 1079 })),
                      Expect(Move({ 1919, 1079 }, { 1920, 1084 }), Allow({ 1920, 1084 })),
                  } },
            });
        }

        TEST_METHOD (CrossedSideIsTheOneBorderingTheDestination)
        {
            // From (1915, 1075) to (1921, 1085) the move reaches A's bottom row first, but nothing
            // lies below A. Only A's right side borders B, so that is the crossed edge: the push is
            // 2, then 10 + 10 + 3 along the row crosses.
            RunAll({
                { L"all edges",
                  TallNeighbor(),
                  Defaults(),
                  {
                      Expect(Move({ 1915, 1075 }, { 1921, 1085 }), Hold({ 1919, 1079 })),
                      Expect(Move({ 1919, 1079 }, { 1929, 1079 }), Hold({ 1919, 1079 })),
                      Expect(Move({ 1919, 1079 }, { 1929, 1079 }), Hold({ 1919, 1079 })),
                      Expect(Move({ 1919, 1079 }, { 1922, 1079 }), Allow({ 1922, 1079 })),
                  } },
                { L"side-by-side filter", TallNeighbor(), WithFilter(EdgeFilter::SideBySide), { Expect(Move({ 1915, 1075 }, { 1921, 1085 }), Hold({ 1919, 1079 })) } },
                { L"stacked filter", TallNeighbor(), WithFilter(EdgeFilter::Stacked), { Expect(Move({ 1915, 1075 }, { 1921, 1085 }), Allow({ 1921, 1085 })) } },
            });
        }

        TEST_METHOD (DiagonalLeavesThroughTheSideItReachesFirst)
        {
            // From (1900, 1070) to (1930, 1090) the move reaches TL's bottom row before its right
            // column. Neither side borders BR, so the first-reached side, the stacked one, is the
            // crossed edge, and the move meets the stricter rule of the two sides it passes: under
            // the side-by-side filter the stacked side is Off, so the side-by-side brake holds.
            RunAll({
                { L"all edges", Junction(), Defaults(), { Expect(Move({ 1900, 1070 }, { 1930, 1090 }), Hold({ 1919, 1079 })) } },
                { L"side-by-side filter", Junction(), WithFilter(EdgeFilter::SideBySide), { Expect(Move({ 1900, 1070 }, { 1930, 1090 }), Hold({ 1919, 1079 })) } },
                { L"stacked filter", Junction(), WithFilter(EdgeFilter::Stacked), { Expect(Move({ 1900, 1070 }, { 1930, 1090 }), Hold({ 1919, 1079 })) } },
            });
        }

        TEST_METHOD (CrossingsAtTheEndsOfAPartialOverlap)
        {
            RunAll({
                { L"A to B on the first shared row", Offset(), Defaults(), { Expect(Move({ 1919, 540 }, { 1925, 540 }), Hold({ 1919, 540 })) } },
                { L"A to B one row above the shared segment", Offset(), Defaults(), { Expect(Move({ 1919, 539 }, { 1925, 539 }), Allow({ 1925, 539 })) } },
                { L"A to B on the last shared row", Offset(), Defaults(), { Expect(Move({ 1919, 1079 }, { 1925, 1079 }), Hold({ 1919, 1079 })) } },
                { L"B to A on the last shared row", Offset(), Defaults(), { Expect(Move({ 1920, 1079 }, { 1915, 1079 }), Hold({ 1920, 1079 })) } },
                { L"B to A one row below the shared segment", Offset(), Defaults(), { Expect(Move({ 1920, 1080 }, { 1915, 1080 }), Allow({ 1915, 1080 })) } },
                { L"diagonal from above the segment into B", Offset(), Defaults(), { Expect(Move({ 1919, 530 }, { 1925, 545 }), Hold({ 1919, 545 })) } },
                { L"diagonal from B out past the segment's end", Offset(), Defaults(), { Expect(Move({ 1920, 1075 }, { 1914, 1085 }), Allow({ 1914, 1085 })) } },
            });
        }

        TEST_METHOD (AWobblingDiagonalAtAJunctionKeepsItsSide)
        {
            // Held at TL's corner pixel, a hand pushing into the junction must cross however its
            // moves split between the axes.
            //  - A diagonal into BR crosses both sides at once and neither borders BR, so the push
            //    keeps the side it started on and counts only the overshoot past that side: 3 and
            //    2 alternating reach 25 on the 10th move, where the larger overshoot would reach
            //    27 on the 9th.
            //  - A slow diagonal reports one axis per event, landing on TR and BL in turn. Right
            //    after a hold, a side change carries the amount less the cursor's distance from
            //    the corner pixel, so 6 px moves from the corner cross on the 5th and 1 px moves
            //    on the 300th at the shipping 300.
            //  - A wobble of w px across the other axis is charged once: as an away drain when the
            //    move leaves the side being pushed, or as drift off the corner on the next side
            //    change when the move only slid along the pushed side. The hand's return trip of w
            //    px lands inside TL, which no push counts.
            //    - (+6,-1)/(0,+6): 6, 10, 15, 20, 25, so the 5th move crosses 25; 10 per pair
            //      reaches 300 on the 60th.
            //    - (+6,0)/(-1,+6): 6, 11, 16, 21, 26, the 5th; 301 on the 60th.
            //    - (+6,-4)/(0,+6): 6, 4, 6, 8, 10, then 4 per pair, so 26 on the 13th and exactly
            //      300 on the 150th.
            //    - (+6,0)/(-4,+6): 6, 8, 10, 12, then 4 per pair, so 26 on the 11th and exactly
            //      300 on the 148th.
            const RectL tl = Junction().monitors[0].rect;
            const PointL corner{ 1919, 1079 };
            RunAll({
                { L"right first: 10 + 10 + 10 crosses",
                  Junction(),
                  Defaults(),
                  {
                      Expect(Move({ 1919, 1079 }, { 1931, 1089 }), Hold({ 1919, 1079 })),
                      Expect(Move({ 1919, 1079 }, { 1929, 1091 }), Hold({ 1919, 1079 })),
                      Expect(Move({ 1919, 1079 }, { 1931, 1089 }), Allow({ 1931, 1089 })),
                  } },
                { L"bottom first: 10 + 10 + 10 crosses",
                  Junction(),
                  Defaults(),
                  {
                      Expect(Move({ 1919, 1079 }, { 1929, 1091 }), Hold({ 1919, 1079 })),
                      Expect(Move({ 1919, 1079 }, { 1931, 1089 }), Hold({ 1919, 1079 })),
                      Expect(Move({ 1919, 1079 }, { 1929, 1091 }), Allow({ 1929, 1091 })),
                  } },
                { L"shipping defaults: the 30th capped move crosses", Junction(), Settings{}, CycleFrom(tl, corner, { { 12, 10 }, { 10, 12 } }, 30) },
                { L"uncapped diagonal: only the pushed side's overshoot counts", Junction(), Defaults(), CycleFrom(tl, corner, { { 3, 2 }, { 2, 3 } }, 10) },
                { L"one axis per event, 6 px: crosses on the 5th", Junction(), Defaults(), CycleFrom(tl, corner, { { 6, 0 }, { 0, 6 } }, 5) },
                { L"one axis per event, 1 px at the shipping 300", Junction(), Settings{}, CycleFrom(tl, corner, { { 1, 0 }, { 0, 1 } }, 300) },
                { L"(+6,-1)/(0,+6) at 25: crosses on the 5th", Junction(), Defaults(), CycleFrom(tl, corner, { { 6, -1 }, { 0, 6 } }, 5) },
                { L"(+6,-1)/(0,+6) at the shipping 300: crosses on the 60th", Junction(), Settings{}, CycleFrom(tl, corner, { { 6, -1 }, { 0, 6 } }, 60) },
                { L"(+6,0)/(-1,+6) at 25: crosses on the 5th", Junction(), Defaults(), CycleFrom(tl, corner, { { 6, 0 }, { -1, 6 } }, 5) },
                { L"(+6,0)/(-1,+6) at the shipping 300: crosses on the 60th", Junction(), Settings{}, CycleFrom(tl, corner, { { 6, 0 }, { -1, 6 } }, 60) },
                { L"(+6,-4)/(0,+6) at 25: crosses on the 13th", Junction(), Defaults(), CycleFrom(tl, corner, { { 6, -4 }, { 0, 6 } }, 13) },
                { L"(+6,-4)/(0,+6) at the shipping 300: crosses on the 150th", Junction(), Settings{}, CycleFrom(tl, corner, { { 6, -4 }, { 0, 6 } }, 150) },
                { L"(+6,0)/(-4,+6) at 25: crosses on the 11th", Junction(), Defaults(), CycleFrom(tl, corner, { { 6, 0 }, { -4, 6 } }, 11) },
                { L"(+6,0)/(-4,+6) at the shipping 300: crosses on the 148th", Junction(), Settings{}, CycleFrom(tl, corner, { { 6, 0 }, { -4, 6 } }, 148) },
            });
        }

        TEST_METHOD (TheCarryChargesTheFartherOfTheCursorAndTheLastHold)
        {
            // 20 px against TL's right side, then a push down from a cursor that is not where the
            // last hold put it, as after another module moves the cursor. Either point 19 px from
            // the corner leaves 1 of the 20, so the push down holds.
            RunAll({
                { L"held 19 px up, cursor on the corner",
                  Junction(),
                  Defaults(),
                  {
                      Expect(Move({ 1919, 1060 }, { 1929, 1060 }), Hold({ 1919, 1060 })),
                      Expect(Move({ 1919, 1060 }, { 1929, 1060 }), Hold({ 1919, 1060 })),
                      Expect(Move({ 1919, 1079 }, { 1919, 1084 }), Hold({ 1919, 1079 })),
                  } },
                { L"held on the corner, cursor 19 px up",
                  Junction(),
                  Defaults(),
                  {
                      Expect(Move({ 1919, 1079 }, { 1929, 1079 }), Hold({ 1919, 1079 })),
                      Expect(Move({ 1919, 1079 }, { 1929, 1079 }), Hold({ 1919, 1079 })),
                      Expect(Move({ 1919, 1060 }, { 1919, 1085 }), Hold({ 1919, 1079 })),
                  } },
            });
        }

        TEST_METHOD (ADrainCountsOnlyTowardTheSideItWasMeasuredAgainst)
        {
            // Push 1000. 99 pushes build 990 against A's bottom at the bottom-right corner. A move
            // 540 up and 5 right drains 540 against the bottom, carries 450 to the right side and
            // adds 5. A move straight up to the top then carries to the top side from 539 px
            // below the top-right corner. That drift was paid against the bottom, not toward the
            // top, so all 539 count: nothing carries, the overshoot adds 1, and the push up crosses
            // on the 100th move of 10, as from a fresh start.
            std::vector<Step> steps;
            for (int i = 0; i < 99; ++i)
            {
                steps.push_back(Expect(Move({ 1919, 1079 }, { 1919, 1089 }), Hold({ 1919, 1079 })));
            }
            steps.push_back(Expect(Move({ 1919, 1079 }, { 1924, 539 }), Hold({ 1919, 539 })));
            steps.push_back(Expect(Move({ 1919, 539 }, { 1919, -1 }), Hold({ 1919, 0 })));
            for (int i = 1; i <= 100; ++i)
            {
                steps.push_back(Expect(Move({ 1919, 0 }, { 1919, -10 }), i == 100 ? Allow({ 1919, -10 }) : Hold({ 1919, 0 })));
            }
            RunAll({ { L"bottom, then right, then top", RightBelowAbove(), WithDistance(1000), steps } });
        }

        TEST_METHOD (AFastMoveAlongTheOtherEdgeDrainsBeforeTheCarry)
        {
            // 20 px against TL's right side at the corner, then one fast move 500 px left and 6
            // down. Its component away from the right side drains the push before the side change,
            // so the push down starts over at 6 and 4 more hold at 10.
            RunAll({
                { L"500 px along the bottom edge",
                  Junction(),
                  Defaults(),
                  {
                      Expect(Move({ 1919, 1079 }, { 1929, 1079 }), Hold({ 1919, 1079 })),
                      Expect(Move({ 1919, 1079 }, { 1929, 1079 }), Hold({ 1919, 1079 })),
                      Expect(Move({ 1919, 1079 }, { 1419, 1085 }), Hold({ 1419, 1079 })),
                      Expect(Move({ 1419, 1079 }, { 1419, 1083 }), Hold({ 1419, 1079 })),
                  } },
            });
        }

        TEST_METHOD (APushSlidAlongTheEdgeDoesNotCarryAroundTheCorner)
        {
            // 20 px against TL's right side at y = 500, then a slide down the edge column to the
            // corner pixel. The slide keeps the push on the right side, but the move after it was
            // not held, so a push down the bottom side starts over: 5, 15, 25.
            RunAll({
                { L"slide to the corner, then push down",
                  Junction(),
                  Defaults(),
                  {
                      Expect(Move({ 1919, 500 }, { 1929, 500 }), Hold({ 1919, 500 })),
                      Expect(Move({ 1919, 500 }, { 1929, 500 }), Hold({ 1919, 500 })),
                      Expect(Move({ 1919, 500 }, { 1919, 1079 }), Allow({ 1919, 1079 })),
                      Expect(Move({ 1919, 1079 }, { 1919, 1084 }), Hold({ 1919, 1079 })),
                      Expect(Move({ 1919, 1079 }, { 1919, 1089 }), Hold({ 1919, 1079 })),
                      Expect(Move({ 1919, 1079 }, { 1919, 1089 }), Allow({ 1919, 1089 })),
                  } },
            });
        }

        TEST_METHOD (TheBorderingSideOutranksThePushedSide)
        {
            // B lies below A and C to the right of both. After 20 px against A's bottom at x = 1900,
            // a diagonal landing on C crosses A's right side, which borders C. The cursor is 19 px
            // from the corner pixel, so 1 px of the push carries to the right side and the move
            // makes it 7, which holds; keeping the bottom side would reach 26 and cross.
            RunAll({
                { L"bottom push, then a diagonal onto the right neighbor",
                  TwoOnTheLeft(),
                  Defaults(),
                  {
                      Expect(Move({ 1900, 1079 }, { 1900, 1089 }), Hold({ 1900, 1079 })),
                      Expect(Move({ 1900, 1079 }, { 1900, 1089 }), Hold({ 1900, 1079 })),
                      Expect(Move({ 1900, 1079 }, { 1925, 1085 }), Hold({ 1919, 1079 })),
                  } },
            });
        }
    };

    TEST_CLASS (SettingsAndLayoutChanges)
    {
    public:
        TEST_METHOD (SettingsChangeMidPushResetsThePush)
        {
            // A push of 20, then a change, then 5 more: a kept push would reach 25 and cross.
            const Settings changes[] = {
                WithDistance(24),
                WithFilter(EdgeFilter::SideBySide),
                WithModifier(CrossModifier::Shift),
                WithDragDisable(),
                WithFreeReturn(false),
            };
            const wchar_t* names[] = { L"distance 25 to 24", L"filter all to side-by-side", L"modifier Ctrl to Shift", L"disable while dragging on", L"free return off" };

            std::vector<Case> cases;
            for (size_t i = 0; i < std::size(changes); ++i)
            {
                cases.push_back({ names[i], SideBySide(), Defaults(), { PushTen(), PushTen(), ExpectAfter(changes[i], Move({ 1919, 500 }, { 1924, 500 }), Hold({ 1919, 500 })) } });
            }
            RunAll(cases);
        }

        TEST_METHOD (LayoutChangeMidPushResetsThePush)
        {
            RunAll({
                { L"B's DPI changes", SideBySide(), Defaults(), { PushTen(), PushTen(), ExpectAfter(Layout{ { Mon(0, 0, 1920, 1080), Mon(1920, 0, 3840, 1080, DPI_150) } }, Move({ 1919, 500 }, { 1924, 500 }), Hold({ 1919, 500 })) } },
                { L"a third monitor appears", SideBySide(), Defaults(), { PushTen(), PushTen(), ExpectAfter(Layout{ { Mon(0, 0, 1920, 1080), Mon(1920, 0, 3840, 1080), Mon(3840, 0, 5760, 1080) } }, Move({ 1919, 500 }, { 1924, 500 }), Hold({ 1919, 500 })) } },
                { L"B moves down", SideBySide(), Defaults(), { PushTen(), PushTen(), ExpectAfter(Layout{ { Mon(0, 0, 1920, 1080), Mon(1920, 100, 3840, 1180) } }, Move({ 1919, 500 }, { 1924, 500 }), Hold({ 1919, 500 })) } },
            });
        }

        TEST_METHOD (EqualSettingsAndLayoutKeepThePush)
        {
            // A fresh object with the same values is not a change.
            RunAll({
                { L"same settings", SideBySide(), Defaults(), { PushTen(), PushTen(), ExpectAfter(Defaults(), Move({ 1919, 500 }, { 1924, 500 }), Allow({ 1924, 500 })) } },
                { L"same layout", SideBySide(), Defaults(), { PushTen(), PushTen(), ExpectAfter(SideBySide(), Move({ 1919, 500 }, { 1924, 500 }), Allow({ 1924, 500 })) } },
            });
        }

        TEST_METHOD (ChangeClearsTheFreeReturn)
        {
            RunAll({
                { L"settings change", SideBySide(), Defaults(), Concat(CrossToB(), { ExpectAfter(WithDistance(24), Move({ 1924, 500 }, { 1915, 500 }), Hold({ 1920, 500 })) }) },
                { L"layout change", SideBySide(), Defaults(), Concat(CrossToB(), { ExpectAfter(Layout{ { Mon(0, 0, 1920, 1080), Mon(1920, 0, 3840, 1080), Mon(3840, 0, 5760, 1080) } }, Move({ 1924, 500 }, { 1915, 500 }), Hold({ 1920, 500 })) }) },
            });
        }
    };

    // A clip from GetClipCursor, which Windows applies after the hook. In SideBySide, A is
    // 0..1919 and B is 1920..3839. With no game's clip, Windows reports the virtual screen's
    // bounding rect, here 0, 0, 3840, 1080.
    TEST_CLASS (CursorClip)
    {
    public:
        TEST_METHOD (AClipOnTheSourceMonitorNeverHolds)
        {
            // A full-screen game confines the cursor to A. Every push toward B lands back on A, so
            // each is a move within A, allowed at the point the hook saw.
            const RectL a{ 0, 0, 1920, 1080 };
            std::vector<Step> steps;
            for (int i = 0; i < 10; ++i)
            {
                steps.push_back(Expect(ClippedMove({ 1919, 500 }, { 1929, 500 }, a), Allow({ 1929, 500 })));
            }
            RunAll({ { L"ten 10 px pushes under A's clip", SideBySide(), Defaults(), steps } });
        }

        TEST_METHOD (TheBoundingRectChangesNothingForMovesInsideIt)
        {
            const RectL screen = BoundingRect(SideBySide());
            RunAll({
                { L"cross to B", SideBySide(), Defaults(), UnderClip(CrossToB(), screen) },
                { L"free return", SideBySide(), Defaults(), UnderClip(Concat(CrossToB(), { Expect(Move({ 1924, 500 }, { 1915, 500 }), Allow({ 1915, 500 })) }), screen) },
            });
        }

        TEST_METHOD (ADiagonalPastAnOuterEdgeAtAnInnerCornerHolds)
        {
            // From A's bottom-right pixel, a move past the outer bottom edge onto B's column. The
            // bounding rect clamps it to (1929, 1079) on B, where Windows puts the cursor, so it is
            // a crossing and holds. With no clip known it lands on no monitor and goes through.
            RunAll({
                { L"bounding rect", SideBySide(), Defaults(), { Expect(ClippedMove({ 1919, 1079 }, { 1929, 1090 }, BoundingRect(SideBySide())), Hold({ 1919, 1079 })) } },
                { L"no clip known", SideBySide(), Defaults(), { Expect(Move({ 1919, 1079 }, { 1929, 1090 }), Allow({ 1929, 1090 })) } },
            });
        }

        TEST_METHOD (APushSurvivesAnExcursionPastAnOuterEdge)
        {
            // 20 banked, then a slide up past A's outer top edge. The bounding rect clamps it to
            // (1919, 0) on A, a move along the edge, so the push stays and 5 more cross. With no
            // clip known the slide lands on no monitor and the push starts over.
            const std::vector<Step> slide = {
                PushTen(),
                PushTen(),
                Expect(Move({ 1919, 500 }, { 1919, -8 }), Allow({ 1919, -8 })),
            };
            RunAll({
                { L"bounding rect", SideBySide(), Defaults(), UnderClip(Concat(slide, { Expect(Move({ 1919, 0 }, { 1924, 0 }), Allow({ 1924, 0 })) }), BoundingRect(SideBySide())) },
                { L"no clip known", SideBySide(), Defaults(), Concat(slide, { Expect(Move({ 1919, 0 }, { 1924, 0 }), Hold({ 1919, 0 })) }) },
            });
        }

        TEST_METHOD (AClipAcrossTheEdgeBrakesAtTheClampedOvershoot)
        {
            // The clip reaches 5 px into B. Each 31 px move lands at x = 1924 and adds 5, not the
            // 10 px cap, so the fifth move reaches 25 and crosses, allowed at the point the hook saw.
            const RectL reach{ 0, 0, 1925, 1080 };
            std::vector<Step> steps;
            for (int i = 1; i <= 5; ++i)
            {
                steps.push_back(Expect(ClippedMove({ 1919, 500 }, { 1950, 500 }, reach), i == 5 ? Allow({ 1950, 500 }) : Hold({ 1919, 500 })));
            }
            RunAll({ { L"31 px moves under a clip 5 px into B", SideBySide(), Defaults(), steps } });
        }

        TEST_METHOD (AGameConfiningTheCursorMidPushResetsThePush)
        {
            // 20 banked under the bounding rect. A game clips the cursor to A, which closes B, then
            // lets it go. The banked push is gone: 5 more hold, and the full 25 is needed again. A
            // push cannot build while A's clip holds, so the reset at its start is the one that
            // shows.
            const RectL screen = BoundingRect(SideBySide());
            const RectL a{ 0, 0, 1920, 1080 };
            RunAll({
                { L"confined, released, 5 more",
                  SideBySide(),
                  Defaults(),
                  Concat(Concat(UnderClip({ PushTen(), PushTen() }, screen), { Expect(ClippedMove({ 1919, 500 }, { 1929, 500 }, a), Allow({ 1929, 500 })) }),
                         UnderClip({ Expect(Move({ 1919, 500 }, { 1924, 500 }), Hold({ 1919, 500 })) }, screen)) },
                { L"confined, released, full push",
                  SideBySide(),
                  Defaults(),
                  Concat(Concat(UnderClip({ PushTen(), PushTen() }, screen), { Expect(ClippedMove({ 1919, 500 }, { 1929, 500 }, a), Allow({ 1929, 500 })) }), UnderClip(CrossToB(), screen)) },
            });
        }

        TEST_METHOD (AClipThatKeepsTheEdgeOpenStillCrosses)
        {
            // The shipping 300 px push with 10 px moves crosses on move 30 with no clip. Clips that
            // change between moves but keep B reachable must cross on the same move. The cases:
            // two rects alternating, a failing GetClipCursor (no clip on
            // every other move), and a clip that follows the cursor with 1 px of jitter.
            const auto pushes = [](RectL (*clipFor)(int), bool failEveryOther) {
                std::vector<Step> steps;
                for (int i = 1; i <= 30; ++i)
                {
                    MoveEvent move = Move({ 1919, 500 }, { 1929, 500 });
                    if (!(failEveryOther && i % 2 == 0))
                    {
                        move.clip = clipFor(i);
                    }
                    steps.push_back(Expect(move, i == 30 ? Allow({ 1929, 500 }) : Hold({ 1919, 500 })));
                }
                return steps;
            };
            RunAll({
                { L"no clip known", SideBySide(), Settings{}, CrossOnStep({ 1919, 500 }, 1, 0, 10, 30) },
                { L"bounding rect", SideBySide(), Settings{}, UnderClip(CrossOnStep({ 1919, 500 }, 1, 0, 10, 30), BoundingRect(SideBySide())) },
                { L"3000 and 3001 wide in turn", SideBySide(), Settings{}, pushes([](int i) { return RectL{ 0, 0, 3000 + (i & 1), 1080 }; }, false) },
                { L"read fails every other move", SideBySide(), Settings{}, pushes([](int) { return RectL{ 0, 0, 3840, 1080 }; }, true) },
                { L"follows the cursor with jitter", SideBySide(), Settings{}, pushes([](int i) { return RectL{ 1819, 400 + (i & 1), 2019, 600 }; }, false) },
            });
        }

        TEST_METHOD (AClipClosingTheWayBackClearsTheFreeReturn)
        {
            // Crossed to B. A game then clips the cursor to B, which closes the way back to A, and
            // lets it go. The return straight back brakes; a kept free return would let it through.
            const RectL screen = BoundingRect(SideBySide());
            const RectL b{ 1920, 0, 3840, 1080 };
            RunAll({
                { L"confined to B, released, back to A",
                  SideBySide(),
                  Defaults(),
                  Concat(Concat(UnderClip(CrossToB(), screen), { Expect(ClippedMove({ 1924, 500 }, { 1930, 500 }, b), Allow({ 1930, 500 })) }),
                         UnderClip({ Expect(Move({ 1930, 500 }, { 1915, 500 }), Hold({ 1920, 500 })) }, screen)) },
            });
        }

        TEST_METHOD (AFailedReadKeepsTheLastClip)
        {
            // Under A's clip, a move that carries no clip is still judged inside A's clip.
            const RectL a{ 0, 0, 1920, 1080 };
            RunAll({
                { L"clip to A, then no clip read",
                  SideBySide(),
                  Defaults(),
                  {
                      Expect(ClippedMove({ 1919, 500 }, { 1929, 500 }, a), Allow({ 1929, 500 })),
                      Expect(Move({ 1919, 500 }, { 1929, 500 }), Allow({ 1929, 500 })),
                      Expect(Move({ 1919, 500 }, { 1929, 500 }), Allow({ 1929, 500 })),
                  } },
            });
        }

        TEST_METHOD (TheDrainIsMeasuredAtTheClampedPoint)
        {
            // The clip starts at x = 1905 and spans B, so pushes pass through it. 20 banked. A move
            // back to 1890 lands at 1905, 14 px away rather than 29, which leaves 6. The cursor
            // returns to the edge, which drains nothing, and two more 10 px pushes reach 26 and
            // cross. A drain at the unclamped point would leave 0 and hold the second push at 20.
            const RectL fromNineteenOhFive{ 1905, 0, 3840, 1080 };
            RunAll({
                { L"push, clamped back-off, push",
                  SideBySide(),
                  Defaults(),
                  UnderClip({
                                PushTen(),
                                PushTen(),
                                Expect(Move({ 1919, 500 }, { 1890, 500 }), Allow({ 1890, 500 })),
                                Expect(Move({ 1905, 500 }, { 1919, 500 }), Allow({ 1919, 500 })),
                                PushTen(),
                                Expect(Move({ 1919, 500 }, { 1929, 500 }), Allow({ 1929, 500 })),
                            },
                            fromNineteenOhFive) },
            });
        }

        TEST_METHOD (ACurrentPointOutsideTheClipIsJudgedInsideIt)
        {
            // GetCursorPos can read the cursor on A just before a clip to B takes effect. Windows
            // puts it on B, so a 5 px move that the hook sees inside A is a move within B.
            RunAll({
                { L"cursor on A, clip to B", SideBySide(), Defaults(), { Expect(ClippedMove({ 1900, 500 }, { 1905, 500 }, RectL{ 1920, 0, 3840, 1080 }), Allow({ 1905, 500 })) } },
            });
        }

        TEST_METHOD (AnEmptyOrInvertedClipIsIgnored)
        {
            RunAll({
                { L"zero width", SideBySide(), Defaults(), UnderClip(CrossToB(), RectL{ 1000, 0, 1000, 1080 }) },
                { L"zero height", SideBySide(), Defaults(), UnderClip(CrossToB(), RectL{ 0, 500, 3840, 500 }) },
                { L"inverted", SideBySide(), Defaults(), UnderClip(CrossToB(), RectL{ 3840, 1080, 0, 0 }) },
            });
        }

        TEST_METHOD (AnEmptyClipEndsTheLastClip)
        {
            // An empty clip is a read that succeeded with nothing to apply, so it ends A's clip:
            // the next push toward B brakes.
            RunAll({
                { L"clip to A, then an empty clip",
                  SideBySide(),
                  Defaults(),
                  {
                      Expect(ClippedMove({ 1919, 500 }, { 1929, 500 }, RectL{ 0, 0, 1920, 1080 }), Allow({ 1929, 500 })),
                      Expect(ClippedMove({ 1919, 500 }, { 1929, 500 }, RectL{ 1000, 0, 1000, 1080 }), Hold({ 1919, 500 })),
                  } },
            });
        }

        TEST_METHOD (AnEmptyClipLeavesTheEdgeOpen)
        {
            // An empty clip confines nothing, so B stays reachable and the push of 20 banked under
            // the bounding rect carries on: 5 more cross.
            RunAll({
                { L"bounding rect, then an empty clip",
                  SideBySide(),
                  Defaults(),
                  Concat(UnderClip({ PushTen(), PushTen() }, BoundingRect(SideBySide())), { Expect(ClippedMove({ 1919, 500 }, { 1924, 500 }, RectL{ 1000, 0, 1000, 1080 }), Allow({ 1924, 500 })) }) },
            });
        }

        TEST_METHOD (BypassesUnderAClipAllowTheUnclampedPoint)
        {
            const RectL a{ 0, 0, 1920, 1080 };
            MoveEvent modifier = ModifierMove({ 1919, 500 }, { 1950, 500 });
            modifier.clip = a;
            MoveEvent absolute = AbsoluteMove({ 1919, 500 }, { 1950, 500 });
            absolute.clip = a;
            RunAll({
                { L"cross key", SideBySide(), Defaults(), { Expect(modifier, Allow({ 1950, 500 })) } },
                { L"absolute input", SideBySide(), Defaults(), { Expect(absolute, Allow({ 1950, 500 })) } },
            });
        }
    };

    TEST_CLASS (InputClassification)
    {
    public:
        TEST_METHOD (EachRuleDecidesWhetherTheEngineJudgesTheMove)
        {
            // LLMHF_LOWER_IL_INJECTED, which Windows sets alongside the injected flag.
            constexpr uint32_t LOWER_IL_INJECTED = 0x00000002;
            // MouseButtonLock's tag on its own injected events.
            constexpr uint64_t MOUSE_BUTTON_LOCK_TAG = 0x57494E4D;

            struct InputCase
            {
                const wchar_t* name;
                uint32_t flags;
                uint64_t extraInfo;
                bool remoteSession;
                InputTreatment expected;
            };

            const InputCase cases[] = {
                // Rule 1: the pen or touch signature passes, injected or not.
                { L"pen signature", 0, 0xFF515700, false, InputTreatment::Pass },
                { L"touch signature (0x80 set)", 0, 0xFF515780, false, InputTreatment::Pass },
                { L"pen signature with a low byte", 0, 0xFF515701, false, InputTreatment::Pass },
                { L"pen signature, sign-extended to 64 bits", 0, 0xFFFFFFFFFF515700, false, InputTreatment::Pass },
                { L"pen signature on an injected move", INJECTED_FLAG, 0xFF515700, false, InputTreatment::Pass },
                { L"near miss: third byte off", 0, 0xFF515800, false, InputTreatment::Judge },
                { L"near miss: top byte off", 0, 0xFE515700, false, InputTreatment::Judge },

                // Rule 2: a remote session stands down for every move.
                { L"remote session, hardware move", 0, 0, true, InputTreatment::Pass },
                { L"remote session overrides the marker", INJECTED_FLAG, TEST_MOVE_MARKER, true, InputTreatment::Pass },
                { L"remote session overrides a non-injected marker", 0, TEST_MOVE_MARKER, true, InputTreatment::Pass },

                // Rule 3: injected moves pass unless they carry the marker without the lower
                // integrity flag.
                { L"injected, no extra info", INJECTED_FLAG, 0, false, InputTreatment::Pass },
                { L"injected from a lower integrity level", INJECTED_FLAG | LOWER_IL_INJECTED, 0, false, InputTreatment::Pass },
                { L"injected with MouseButtonLock's tag", INJECTED_FLAG, MOUSE_BUTTON_LOCK_TAG, false, InputTreatment::Pass },
                { L"injected with the marker", INJECTED_FLAG, TEST_MOVE_MARKER, false, InputTreatment::Judge },
                { L"lower integrity injected with the marker", INJECTED_FLAG | LOWER_IL_INJECTED, TEST_MOVE_MARKER, false, InputTreatment::Pass },
                { L"near-miss marker: plus one", INJECTED_FLAG, TEST_MOVE_MARKER + 1, false, InputTreatment::Pass },
                { L"near-miss marker: top bit of the low word set", INJECTED_FLAG, TEST_MOVE_MARKER | 0x80000000, false, InputTreatment::Pass },
                { L"near-miss marker: a bit above 32", INJECTED_FLAG, TEST_MOVE_MARKER | 0x100000000, false, InputTreatment::Pass },

                // Rule 4: everything else is judged like hardware.
                { L"hardware move", 0, 0, false, InputTreatment::Judge },
                { L"marker on a non-injected move", 0, TEST_MOVE_MARKER, false, InputTreatment::Judge },
                { L"lower-IL bit alone, without the injected flag", LOWER_IL_INJECTED, 0, false, InputTreatment::Judge },
                { L"lower-IL bit with the marker, without the injected flag", LOWER_IL_INJECTED, TEST_MOVE_MARKER, false, InputTreatment::Judge },
            };

            std::wstring failures;
            for (const InputCase& c : cases)
            {
                const InputTreatment actual = ClassifyInput(c.flags, c.extraInfo, c.remoteSession);
                if (actual != c.expected)
                {
                    failures += (failures.empty() ? L"" : L"; ") + std::wstring(c.name) + L": expected " + Name(c.expected) + L", got " + Name(actual);
                }
            }
            if (!failures.empty())
            {
                Assert::Fail(failures.c_str());
            }
        }

        TEST_METHOD (MarkerCannotBeMistakenForAnotherSignal)
        {
            Assert::IsTrue((TEST_MOVE_MARKER & PEN_OR_TOUCH_SIGNATURE_MASK) != PEN_OR_TOUCH_SIGNATURE, L"the marker matches the pen and touch signature");
            Assert::IsTrue(TEST_MOVE_MARKER != 0x57494E4D, L"the marker equals MouseButtonLock's tag");
            Assert::IsTrue(TEST_MOVE_MARKER != 0, L"the marker equals the default extra info");
        }
    };

    TEST_CLASS (FourMonitorLayout)
    {
    public:
        TEST_METHOD (PushesIntoThePrimaryBrake)
        {
            // Push 500 logical px: 500 physical px from a 100% source, 750 from the 150% one. 5 px
            // steps never reach the cap (10 or 15 px).
            const PointL rightEdge{ 1920, 500 };
            const PointL leftEdge{ -1, 500 };
            const PointL topEdge{ 500, -1 };
            RunAll({
                { L"150% right into primary, 5 px steps", FourAround(DPI_150), SlowPushSettings(), SlowPush(rightEdge, -1, 0, 750, { 5 }, CapAt(DPI_150)) },
                { L"150% right into primary, 1-5 px steps", FourAround(DPI_150), SlowPushSettings(), SlowPush(rightEdge, -1, 0, 750, { 1, 2, 3, 4, 5 }, CapAt(DPI_150)) },
                { L"100% right into primary", FourAround(DPI_100), SlowPushSettings(), SlowPush(rightEdge, -1, 0, 500, { 1, 2, 3, 4, 5 }) },
                { L"left into primary", FourAround(DPI_150), SlowPushSettings(), SlowPush(leftEdge, 1, 0, 500, { 5 }) },
                { L"top into primary", FourAround(DPI_150), SlowPushSettings(), SlowPush(topEdge, 0, 1, 500, { 5 }) },
                { L"primary into 150% right", FourAround(DPI_150), SlowPushSettings(), SlowPush({ 1919, 500 }, 1, 0, 500, { 5 }) },
            });
        }

        TEST_METHOD (PushesIntoThePrimaryBrakeAfterTheFreeReturnExpires)
        {
            // Out of the primary by a slow push that lands at x = 1929, then far right on the 150%
            // monitor (1910 px from the edge, past its 150 px window), back to the edge pixel, then
            // the slow push. The expired allowance gives no free crossing.
            const PointL rightEdge{ 1920, 500 };
            const std::vector<Step> outAndBack = Concat(SlowPush({ 1919, 500 }, 1, 0, 500, { 10 }),
                                                        {
                                                            Expect(Move({ 1929, 500 }, { 3830, 500 }), Allow({ 3830, 500 })),
                                                            Expect(Move({ 3830, 500 }, { 1920, 500 }), Allow({ 1920, 500 })),
                                                        });
            RunAll({
                { L"150% right into primary", FourAround(DPI_150), SlowPushSettings(), Concat(outAndBack, SlowPush(rightEdge, -1, 0, 750, { 5 }, CapAt(DPI_150))) },
            });
        }

        TEST_METHOD (FreeReturnInsideTheLimitIsFreeOnTheFourMonitorLayout)
        {
            // Within the window: out of the primary by a slow push that lands 4 px past the
            // edge, back to the edge pixel, and the first step into the primary is free.
            RunAll({
                { L"150% right",
                  FourAround(DPI_150),
                  SlowPushSettings(),
                  Concat(SlowPush({ 1919, 500 }, 1, 0, 500, { 5 }),
                         {
                             Expect(Move({ 1924, 500 }, { 1920, 500 }), Allow({ 1920, 500 })),
                             Expect(Move({ 1920, 500 }, { 1915, 500 }), Allow({ 1915, 500 })),
                         }) },
            });
        }
    };

    // Hard pushes recorded by the hook on a four-monitor layout (all monitors at 100%) at the
    // shipping defaults, replayed move for move.
    TEST_CLASS (HardPushTraces)
    {
    public:
        TEST_METHOD (AReboundAfterAHardStrokeCostsOnlyItsDistance)
        {
            // Two hard strokes from the left monitor into the primary. The first adds 7 and then
            // the cap 17 times, 177 in all. The hand then rebounds 6 px back onto the left monitor,
            // which leaves 171, and steps 1 px toward the edge. The second stroke adds 2 and then
            // the cap 12 times to reach 293, and its next move crosses at 303.
            const std::vector<TracedMove> strokes = {
                { { -8, 205 }, { 6, 206 } },
                { { -1, 206 }, { 27, 208 } },
                { { -1, 208 }, { 56, 213 } },
                { { -1, 213 }, { 99, 221 } },
                { { -1, 221 }, { 119, 229 } },
                { { -1, 229 }, { 142, 238 } },
                { { -1, 238 }, { 159, 250 } },
                { { -1, 250 }, { 173, 265 } },
                { { -1, 265 }, { 173, 279 } },
                { { -1, 279 }, { 174, 291 } },
                { { -1, 291 }, { 165, 298 } },
                { { -1, 298 }, { 158, 301 } },
                { { -1, 301 }, { 141, 303 } },
                { { -1, 303 }, { 126, 307 } },
                { { -1, 307 }, { 99, 312 } },
                { { -1, 312 }, { 76, 318 } },
                { { -1, 318 }, { 46, 325 } },
                { { -1, 325 }, { 18, 331 } },
                { { -1, 331 }, { -7, 337 } },
                { { -7, 337 }, { -6, 337 } },
                { { -6, 337 }, { 1, 336 } },
                { { -1, 336 }, { 23, 336 } },
                { { -1, 336 }, { 55, 337 } },
                { { -1, 337 }, { 91, 340 } },
                { { -1, 340 }, { 118, 342 } },
                { { -1, 342 }, { 140, 346 } },
                { { -1, 346 }, { 153, 353 } },
                { { -1, 353 }, { 163, 361 } },
                { { -1, 361 }, { 163, 367 } },
                { { -1, 367 }, { 160, 369 } },
                { { -1, 369 }, { 156, 369 } },
                { { -1, 369 }, { 150, 366 } },
                { { -1, 366 }, { 132, 364 } },
                { { -1, 364 }, { 111, 364 } },
            };
            const Layout layout = FourAround(DPI_100);
            RunAll({ { L"left monitor into the primary", layout, Settings{}, Replay(strokes, layout.monitors[3].rect) } });
        }

        TEST_METHOD (ACornerPushKeepsItsPushAcrossBothNeighbors)
        {
            // A hard push up and left from the right monitor's top-left corner. The primary lies
            // past its left side, and the top monitor touches it only at the corner, so the
            // first-reached side is the left one for both. The moves land on the primary for 141,
            // on the top monitor for 120 more, and back on the primary for 30: 291 against the left
            // side. The recording ends there with the hand moving away; the last move, added
            // here, pushes the remaining 9 px and crosses.
            const std::vector<TracedMove> push = {
                { { 1950, 407 }, { 1878, 400 } },
                { { 1920, 400 }, { 1788, 386 } },
                { { 1920, 386 }, { 1739, 366 } },
                { { 1920, 366 }, { 1690, 338 } },
                { { 1920, 338 }, { 1658, 303 } },
                { { 1920, 303 }, { 1622, 268 } },
                { { 1920, 268 }, { 1605, 234 } },
                { { 1920, 234 }, { 1592, 200 } },
                { { 1920, 200 }, { 1606, 163 } },
                { { 1920, 163 }, { 1622, 120 } },
                { { 1920, 120 }, { 1650, 79 } },
                { { 1920, 79 }, { 1685, 45 } },
                { { 1920, 45 }, { 1723, 19 } },
                { { 1920, 19 }, { 1765, 0 } },
                { { 1920, 0 }, { 1919, 0 } },
                { { 1920, 0 }, { 1903, -2 } },
                { { 1920, 0 }, { 1837, -11 } },
                { { 1920, 0 }, { 1756, -18 } },
                { { 1920, 0 }, { 1716, -19 } },
                { { 1920, 0 }, { 1682, -25 } },
                { { 1920, 0 }, { 1670, -30 } },
                { { 1920, 0 }, { 1664, -32 } },
                { { 1920, 0 }, { 1673, -29 } },
                { { 1920, 0 }, { 1696, -23 } },
                { { 1920, 0 }, { 1718, -18 } },
                { { 1920, 0 }, { 1751, -15 } },
                { { 1920, 0 }, { 1781, -7 } },
                { { 1920, 0 }, { 1816, 0 } },
                { { 1920, 0 }, { 1859, 10 } },
                { { 1920, 10 }, { 1899, 28 } },
                { { 1920, 28 }, { 1911, 28 } },
            };
            const Layout layout = FourAround(DPI_100);
            RunAll({ { L"right monitor's top-left corner", layout, Settings{}, Replay(push, layout.monitors[1].rect) } });
        }
    };

    // Per-edge settings. In SideBySide A is 0 and B is 1; in Junction TL, TR, BL and BR are 0 to 3;
    // in TwoOnTheRight the tall A is 0, B above is 1 and C below is 2. Defaults() brakes every
    // edge left at Default with 25 px.
    TEST_CLASS (EdgeRuleLookup)
    {
    public:
        TEST_METHOD (RuleForChecksOneWayThenBothWaysThenTheDefault)
        {
            struct LookupCase
            {
                const wchar_t* name;
                std::vector<EdgeSetting> edges;
                EdgeFilter filter;
                size_t source;
                std::optional<size_t> neighbor;
                bool sideBySide;
                EdgeRule expected;
            };

            const LookupCase cases[] = {
                { L"no entries, all edges", {}, EdgeFilter::All, 0, 1, true, { EdgeMode::Custom, 25 } },
                { L"no entries, side-by-side axis under the stacked filter", {}, EdgeFilter::Stacked, 0, 1, true, { EdgeMode::Off, 0 } },
                { L"no entries, stacked axis under the side-by-side filter", {}, EdgeFilter::SideBySide, 0, 1, false, { EdgeMode::Off, 0 } },
                { L"no entries, stacked axis under the stacked filter", {}, EdgeFilter::Stacked, 0, 1, false, { EdgeMode::Custom, 25 } },
                { L"no entries, unknown filter value", {}, static_cast<EdgeFilter>(7), 0, 1, false, { EdgeMode::Custom, 25 } },
                { L"an entry for the pair, but no neighbor", { BothWays(0, 1, EdgeMode::Wall) }, EdgeFilter::All, 0, std::nullopt, true, { EdgeMode::Custom, 25 } },
                { L"both ways, named in order", { BothWays(0, 1, EdgeMode::Custom, 600) }, EdgeFilter::All, 0, 1, true, { EdgeMode::Custom, 600 } },
                { L"both ways, named in reverse", { BothWays(1, 0, EdgeMode::Custom, 600) }, EdgeFilter::All, 0, 1, true, { EdgeMode::Custom, 600 } },
                { L"one way, its direction", { OneWay(0, 1, EdgeMode::Wall) }, EdgeFilter::All, 0, 1, true, { EdgeMode::Wall, 25 } },
                { L"one way, the other direction", { OneWay(1, 0, EdgeMode::Wall) }, EdgeFilter::All, 0, 1, true, { EdgeMode::Custom, 25 } },
                { L"one way listed after both ways", { BothWays(0, 1, EdgeMode::Custom, 600), OneWay(0, 1, EdgeMode::Wall) }, EdgeFilter::All, 0, 1, true, { EdgeMode::Wall, 25 } },
                { L"both ways where the one way runs the other direction", { BothWays(0, 1, EdgeMode::Custom, 600), OneWay(1, 0, EdgeMode::Wall) }, EdgeFilter::All, 0, 1, true, { EdgeMode::Custom, 600 } },
                { L"two both-ways entries, the first wins", { BothWays(0, 1, EdgeMode::Custom, 600), BothWays(1, 0, EdgeMode::Off) }, EdgeFilter::All, 0, 1, true, { EdgeMode::Custom, 600 } },
                { L"two one-way entries, the first wins", { OneWay(0, 1, EdgeMode::Off), OneWay(0, 1, EdgeMode::Wall) }, EdgeFilter::All, 0, 1, true, { EdgeMode::Off, 25 } },
                { L"an entry on a filtered axis", { BothWays(0, 1, EdgeMode::Custom, 40) }, EdgeFilter::Stacked, 0, 1, true, { EdgeMode::Custom, 40 } },
                { L"an entry for another pair", { BothWays(0, 2, EdgeMode::Wall), OneWay(1, 0, EdgeMode::Wall) }, EdgeFilter::All, 0, 1, true, { EdgeMode::Custom, 25 } },
            };

            std::wstring failures;
            for (const LookupCase& c : cases)
            {
                Layout layout = WithEdges(TwoOnTheRight(), c.edges);
                Settings settings = WithFilter(c.filter);
                const EdgeRule actual = RuleFor(layout, settings, c.source, c.neighbor, c.sideBySide);
                if (!(actual == c.expected))
                {
                    failures += (failures.empty() ? L"" : L"; ") + std::wstring(c.name) + L": expected " + Describe(c.expected) + L", got " + Describe(actual);
                }
            }
            if (!failures.empty())
            {
                Assert::Fail(failures.c_str());
            }
        }

        TEST_METHOD (TheRuleComesFromTheNeighborTheMoveLandsOn)
        {
            // A's right side borders B above y = 1080 and C below it. The wall from A to B holds
            // at y = 1075 however long the push, while 2 px lower the push reaches C and crosses.
            const Layout layout = WithEdges(TwoOnTheRight(), { OneWay(0, 1, EdgeMode::Wall) });
            RunAll({
                { L"into B, walled", layout, Defaults(), HeldPushes({ 1919, 1075 }, 1, 0, 10, 100) },
                { L"into C, braked", layout, Defaults(), SlowPush({ 1919, 1085 }, 1, 0, 25, { 10 }) },
            });
        }
    };

    TEST_CLASS (EdgeEquivalence)
    {
    public:
        TEST_METHOD (ExplicitDefaultsOnEveryEdgeMatchNoEntries)
        {
            // Under the All filter, Custom at the global distance on every shared edge is the
            // default spelled out, so every move of every walk decides the same.
            const auto any = [](bool) { return true; };
            const std::pair<const wchar_t*, Layout> layouts[] = { { L"junction", SmallJunction() }, { L"row", SmallRow() }, { L"two on the right", SmallTwoOnTheRight() } };
            const std::pair<const wchar_t*, Settings> settings[] = { { L"25 px", Defaults() }, { L"free return off", WithFreeReturn(false) }, { L"60 px", WithDistance(60) } };
            std::vector<Equivalence> pairs;
            for (const auto& [layoutName, layout] : layouts)
            {
                for (const auto& [settingsName, s] : settings)
                {
                    pairs.push_back({ std::wstring(layoutName) + L", " + settingsName, layout, s, WithEdges(layout, EverySharedEdge(layout, EdgeMode::Custom, s.pushDistance, any)), s });
                }
            }
            RunEquivalences(pairs);
        }

        TEST_METHOD (TwoOneWayEntriesMatchOneBothWaysEntry)
        {
            const std::vector<EdgeSetting> bothWays = { OneWay(2, 3, EdgeMode::Wall), BothWays(0, 1, EdgeMode::Wall), BothWays(0, 2, EdgeMode::Custom, 40), BothWays(1, 3, EdgeMode::Off), BothWays(2, 3, EdgeMode::Custom, 5) };
            const std::vector<EdgeSetting> oneWay = {
                OneWay(2, 3, EdgeMode::Wall),
                OneWay(0, 1, EdgeMode::Wall),
                OneWay(1, 0, EdgeMode::Wall),
                OneWay(2, 0, EdgeMode::Custom, 40),
                OneWay(0, 2, EdgeMode::Custom, 40),
                OneWay(1, 3, EdgeMode::Off),
                OneWay(3, 1, EdgeMode::Off),
                OneWay(3, 2, EdgeMode::Custom, 5),
            };
            RunEquivalences({
                { L"junction", WithEdges(SmallJunction(), bothWays), Defaults(), WithEdges(SmallJunction(), oneWay), Defaults() },
                { L"junction, free return off", WithEdges(SmallJunction(), bothWays), WithFreeReturn(false), WithEdges(SmallJunction(), oneWay), WithFreeReturn(false) },
            });
        }

        TEST_METHOD (ExplicitOffOnFilteredEdgesMatchesTheFilter)
        {
            // In these layouts every crossing lands on a monitor that shares the crossed edge, so
            // the filter's Off always has an edge to stand for.
            const auto sideBySide = [](bool axis) { return axis; };
            const auto stacked = [](bool axis) { return !axis; };
            const std::pair<const wchar_t*, Layout> layouts[] = { { L"junction", SmallJunction() }, { L"two on the right", SmallTwoOnTheRight() } };
            std::vector<Equivalence> pairs;
            for (const auto& [name, layout] : layouts)
            {
                pairs.push_back({ std::wstring(name) + L", side-by-side filter", layout, WithFilter(EdgeFilter::SideBySide), WithEdges(layout, EverySharedEdge(layout, EdgeMode::Off, 25, stacked)), Defaults() });
                pairs.push_back({ std::wstring(name) + L", stacked filter", layout, WithFilter(EdgeFilter::Stacked), WithEdges(layout, EverySharedEdge(layout, EdgeMode::Off, 25, sideBySide)), Defaults() });
            }
            RunEquivalences(pairs);
        }
    };

    TEST_CLASS (PerEdgeDistance)
    {
    public:
        TEST_METHOD (EachEdgeCrossesAtItsOwnDistance)
        {
            const Layout layout = WithEdges(TwoOnTheRight(), { BothWays(0, 1, EdgeMode::Custom, 50) });
            RunAll({
                { L"A to B at its 50", layout, Defaults(), SlowPush({ 1919, 500 }, 1, 0, 50, { 10 }) },
                { L"B to A at its 50", layout, Defaults(), SlowPush({ 1920, 500 }, -1, 0, 50, { 10 }) },
                { L"A to C at the global 25", layout, Defaults(), SlowPush({ 1919, 1500 }, 1, 0, 25, { 10 }) },
                { L"C to A at the global 25", layout, Defaults(), SlowPush({ 1920, 1500 }, -1, 0, 25, { 10 }) },
            });
        }

        TEST_METHOD (APerEdgeDistanceScalesByTheSourceDpi)
        {
            // 40 logical px is 40 physical px from A (100%) and 60 from B (150%).
            const Layout layout = WithEdges(MixedDpi(), { BothWays(0, 1, EdgeMode::Custom, 40) });
            RunAll({
                { L"A (100%) to B", layout, Defaults(), SlowPush({ 1919, 500 }, 1, 0, 40, { 5 }, CapAt(DPI_100)) },
                { L"B (150%) to A", layout, Defaults(), SlowPush({ 1920, 500 }, -1, 0, 60, { 5 }, CapAt(DPI_150)) },
                { L"B (150%) to C at the global 25", layout, Defaults(), SlowPush({ 4799, 800 }, 1, 0, 38, { 5 }, CapAt(DPI_150)) },
            });
        }

        TEST_METHOD (HandTravelMatchesBothWaysOverAPerEdgeDistance)
        {
            // A hand step of two counts is 2 px at 100%, 3 at 150% and 4 at 200%. 40 px on A|B
            // takes 20 steps from either side (40 and 60 px), 60 px on B|C takes 30 (90 and 120).
            const Layout layout = WithEdges(MixedDpi(), { BothWays(0, 1, EdgeMode::Custom, 40), BothWays(1, 2, EdgeMode::Custom, 60) });
            RunAll({
                { L"A to B", layout, Defaults(), CrossOnStep({ 1919, 500 }, 1, 0, 2, 20) },
                { L"B to A", layout, Defaults(), CrossOnStep({ 1920, 500 }, -1, 0, 3, 20) },
                { L"B to C", layout, Defaults(), CrossOnStep({ 4799, 800 }, 1, 0, 3, 30) },
                { L"C to B", layout, Defaults(), CrossOnStep({ 4800, 800 }, -1, 0, 4, 30) },
            });
        }
    };

    TEST_CLASS (OneWayValues)
    {
    public:
        TEST_METHOD (AOneWayDistanceAppliesInItsDirectionOnly)
        {
            const Layout layout = WithEdges(SideBySide(), { OneWay(0, 1, EdgeMode::Custom, 50) });
            RunAll({
                { L"A to B at 50", layout, Defaults(), SlowPush({ 1919, 500 }, 1, 0, 50, { 10 }) },
                { L"B to A at the global 25", layout, Defaults(), SlowPush({ 1920, 500 }, -1, 0, 25, { 10 }) },
            });
        }

        TEST_METHOD (AOneWayWallHoldsOnlyItsDirection)
        {
            const Layout layout = WithEdges(SideBySide(), { OneWay(0, 1, EdgeMode::Wall) });
            RunAll({
                { L"A to B, a thousand capped pushes", layout, Settings{}, HeldPushes({ 1919, 500 }, 1, 0, 30, 1000) },
                { L"B to A at the global 25", layout, Defaults(), SlowPush({ 1920, 500 }, -1, 0, 25, { 10 }) },
            });
        }

        TEST_METHOD (AOneWayOffFreesOnlyItsDirection)
        {
            // Off grants no free return, so the way straight back brakes.
            const Layout layout = WithEdges(SideBySide(), { OneWay(0, 1, EdgeMode::Off) });
            RunAll({
                { L"A to B at once, then straight back",
                  layout,
                  Defaults(),
                  {
                      Expect(Move({ 1919, 500 }, { 1925, 500 }), Allow({ 1925, 500 })),
                      Expect(Move({ 1925, 500 }, { 1915, 500 }), Hold({ 1920, 500 })),
                  } },
                { L"B to A at the global 25", layout, Defaults(), SlowPush({ 1920, 500 }, -1, 0, 25, { 10 }) },
            });
        }

        TEST_METHOD (AOneWayEntryOutranksABothWaysEntryInItsDirectionOnly)
        {
            const std::vector<EdgeSetting> bothFirst = { BothWays(0, 1, EdgeMode::Custom, 50), OneWay(1, 0, EdgeMode::Wall) };
            const std::vector<EdgeSetting> oneWayFirst = { OneWay(1, 0, EdgeMode::Wall), BothWays(0, 1, EdgeMode::Custom, 50) };
            RunAll({
                { L"both ways listed first, A to B at 50", WithEdges(SideBySide(), bothFirst), Defaults(), SlowPush({ 1919, 500 }, 1, 0, 50, { 10 }) },
                { L"both ways listed first, B to A walled", WithEdges(SideBySide(), bothFirst), Defaults(), HeldPushes({ 1920, 500 }, -1, 0, 10, 100) },
                { L"one way listed first, A to B at 50", WithEdges(SideBySide(), oneWayFirst), Defaults(), SlowPush({ 1919, 500 }, 1, 0, 50, { 10 }) },
                { L"one way listed first, B to A walled", WithEdges(SideBySide(), oneWayFirst), Defaults(), HeldPushes({ 1920, 500 }, -1, 0, 10, 100) },
            });
        }

        TEST_METHOD (OnlyAOneWayEntryDependsOnTheOrderOfItsMonitors)
        {
            RunAll({
                { L"both ways named B, A: A to B at 50", WithEdges(SideBySide(), { BothWays(1, 0, EdgeMode::Custom, 50) }), Defaults(), SlowPush({ 1919, 500 }, 1, 0, 50, { 10 }) },
                { L"both ways named B, A: B to A at 50", WithEdges(SideBySide(), { BothWays(1, 0, EdgeMode::Custom, 50) }), Defaults(), SlowPush({ 1920, 500 }, -1, 0, 50, { 10 }) },
                { L"one way B to A: A to B at the global 25", WithEdges(SideBySide(), { OneWay(1, 0, EdgeMode::Wall) }), Defaults(), SlowPush({ 1919, 500 }, 1, 0, 25, { 10 }) },
                { L"one way B to A: B to A walled", WithEdges(SideBySide(), { OneWay(1, 0, EdgeMode::Wall) }), Defaults(), HeldPushes({ 1920, 500 }, -1, 0, 10, 100) },
            });
        }
    };

    TEST_CLASS (FreeReturnAndWalls)
    {
    public:
        TEST_METHOD (AFreeReturnIntoAWalledDirectionHolds)
        {
            // A to B is braked at 25, B to A is a wall. The return straight back finds the wall,
            // keeps holding, and only the cross key gets through.
            const Layout layout = WithEdges(SideBySide(), { OneWay(1, 0, EdgeMode::Wall) });
            RunAll({
                { L"straight back after the crossing",
                  layout,
                  Defaults(),
                  Concat(Concat(CrossToB(), { Expect(Move({ 1924, 500 }, { 1915, 500 }), Hold({ 1920, 500 })) }),
                         Concat(HeldPushes({ 1920, 500 }, -1, 0, 10, 100), { Expect(ModifierMove({ 1920, 500 }, { 1915, 500 }), Allow({ 1915, 500 })) })) },
            });
        }

        TEST_METHOD (AFreeReturnIntoABrakedDirectionStaysFree)
        {
            // The way back carries its own 50, which the free return passes as in a layout with no
            // entries.
            RunAll({
                { L"B to A at 50", WithEdges(SideBySide(), { OneWay(1, 0, EdgeMode::Custom, 50) }), Defaults(), Concat(CrossToB(), { Expect(Move({ 1924, 500 }, { 1915, 500 }), Allow({ 1915, 500 })) }) },
                { L"both ways at 25 spelled out", WithEdges(SideBySide(), { BothWays(0, 1, EdgeMode::Custom, 25) }), Defaults(), Concat(CrossToB(), { Expect(Move({ 1924, 500 }, { 1915, 500 }), Allow({ 1915, 500 })) }) },
            });
        }
    };

    TEST_CLASS (TwoNeighborsOnOneSide)
    {
    public:
        TEST_METHOD (APushSurvivesAWobbleOntoAWall)
        {
            // 20 px against B, a move onto the wall to C holds and leaves the 20, and 5 more
            // against B cross.
            const Layout layout = WithEdges(TwoOnTheRight(), { OneWay(0, 2, EdgeMode::Wall) });
            RunAll({
                { L"B, then C, then B",
                  layout,
                  Defaults(),
                  {
                      Expect(Move({ 1919, 1075 }, { 1929, 1075 }), Hold({ 1919, 1075 })),
                      Expect(Move({ 1919, 1075 }, { 1929, 1075 }), Hold({ 1919, 1075 })),
                      Expect(Move({ 1919, 1075 }, { 1929, 1085 }), Hold({ 1919, 1085 })),
                      Expect(Move({ 1919, 1085 }, { 1924, 1075 }), Allow({ 1924, 1075 })),
                  } },
            });
        }

        TEST_METHOD (ALeanOnAWallAddsNothing)
        {
            // Three 10 px pushes against the wall to C add nothing, so the push against B starts
            // from 0 and needs 10 + 10 + 5.
            const Layout layout = WithEdges(TwoOnTheRight(), { OneWay(0, 2, EdgeMode::Wall) });
            RunAll({
                { L"C, then B",
                  layout,
                  Defaults(),
                  Concat(HeldPushes({ 1919, 1085 }, 1, 0, 10, 3),
                         {
                             Expect(Move({ 1919, 1085 }, { 1929, 1075 }), Hold({ 1919, 1075 })),
                             Expect(Move({ 1919, 1075 }, { 1929, 1075 }), Hold({ 1919, 1075 })),
                             Expect(Move({ 1919, 1075 }, { 1924, 1075 }), Allow({ 1924, 1075 })),
                         }) },
            });
        }

        TEST_METHOD (APushBankedAgainstAStrongNeighborCrossesAWeakOne)
        {
            // 20 px against B's 50 holds; one more pixel onto C reaches C's 10.
            const Layout layout = WithEdges(TwoOnTheRight(), { BothWays(0, 1, EdgeMode::Custom, 50), BothWays(0, 2, EdgeMode::Custom, 10) });
            RunAll({
                { L"B, then C",
                  layout,
                  Defaults(),
                  {
                      Expect(Move({ 1919, 1075 }, { 1929, 1075 }), Hold({ 1919, 1075 })),
                      Expect(Move({ 1919, 1075 }, { 1929, 1075 }), Hold({ 1919, 1075 })),
                      Expect(Move({ 1919, 1075 }, { 1920, 1085 }), Allow({ 1920, 1085 })),
                  } },
            });
        }
    };

    TEST_CLASS (Walls)
    {
    public:
        TEST_METHOD (AWallHoldsEveryCappedPushBothWays)
        {
            const Layout layout = WithEdges(SideBySide(), { BothWays(0, 1, EdgeMode::Wall) });
            RunAll({
                { L"A to B", layout, Settings{}, HeldPushes({ 1919, 500 }, 1, 0, 30, 1000) },
                { L"B to A", layout, Settings{}, HeldPushes({ 1920, 500 }, -1, 0, 30, 1000) },
                { L"a slow push", layout, Defaults(), HeldPushes({ 1919, 500 }, 1, 0, 1, 1000) },
            });
        }

        TEST_METHOD (BypassesCrossAWall)
        {
            const Layout layout = WithEdges(SideBySide(), { BothWays(0, 1, EdgeMode::Wall) });
            RunAll({
                { L"cross modifier held", layout, Defaults(), { Expect(ModifierMove({ 1919, 500 }, { 1925, 500 }), Allow({ 1925, 500 })) } },
                { L"absolute input", layout, Defaults(), { Expect(AbsoluteMove({ 1919, 500 }, { 1925, 500 }), Allow({ 1925, 500 })) } },
                { L"dragging with disable-while-dragging on", layout, WithDragDisable(), { Expect(DragMove({ 1919, 500 }, { 1925, 500 }), Allow({ 1925, 500 })) } },
                { L"dragging with the setting off holds", layout, Defaults(), { Expect(DragMove({ 1919, 500 }, { 1925, 500 }), Hold({ 1919, 500 })) } },
            });
        }

        TEST_METHOD (WallsAtAJunctionHoldADiagonal)
        {
            // Both of TL's ways out are walls, so no move toward BR gets past either side.
            const Layout layout = WithEdges(Junction(), { OneWay(0, 1, EdgeMode::Wall), OneWay(0, 2, EdgeMode::Wall) });
            const RectL tl = Junction().monitors[0].rect;
            std::vector<Step> alternating;
            for (int i = 0; i < 100; ++i)
            {
                alternating.push_back(Expect(Move({ 1919, 1079 }, i % 2 == 0 ? PointL{ 1925, 1079 } : PointL{ 1919, 1085 }), Hold({ 1919, 1079 })));
            }
            std::vector<Step> wobbling = CycleFrom(tl, { 1919, 1079 }, { { 6, -1 }, { 0, 6 } }, 101);
            wobbling.pop_back();
            RunAll({
                { L"through the corner", layout, Defaults(), HeldPushes({ 1919, 1079 }, 1, 1, 10, 100) },
                { L"reaching the bottom row first", layout, Defaults(), { Expect(Move({ 1900, 1070 }, { 1930, 1090 }), Hold({ 1919, 1079 })) } },
                { L"one axis per event", layout, Defaults(), alternating },
                { L"wobbling", layout, Defaults(), wobbling },
            });
        }
    };

    TEST_CLASS (FilterAndEdgeValues)
    {
    public:
        TEST_METHOD (TheFilterAppliesToDefaultDirectionsOnly)
        {
            RunAll({
                { L"Default on a filtered axis is free", WithEdges(Junction(), { BothWays(0, 1, EdgeMode::Custom, 50) }), WithFilter(EdgeFilter::SideBySide), { Expect(Move({ 500, 1079 }, { 500, 1085 }), Allow({ 500, 1085 })) } },
                { L"Custom on a filtered axis brakes", WithEdges(Stacked(), { BothWays(0, 1, EdgeMode::Custom, 25) }), WithFilter(EdgeFilter::SideBySide), SlowPush({ 500, 1079 }, 0, 1, 25, { 10 }) },
                { L"a wall on a filtered axis holds", WithEdges(Stacked(), { BothWays(0, 1, EdgeMode::Wall) }), WithFilter(EdgeFilter::SideBySide), HeldPushes({ 500, 1079 }, 0, 1, 10, 100) },
                { L"Off on a braked axis is free",
                  WithEdges(SideBySide(), { BothWays(0, 1, EdgeMode::Off) }),
                  Defaults(),
                  {
                      Expect(Move({ 1919, 500 }, { 1925, 500 }), Allow({ 1925, 500 })),
                      Expect(Move({ 1925, 500 }, { 1915, 500 }), Allow({ 1915, 500 })),
                  } },
            });
        }

        TEST_METHOD (OffGrantsNoFreeReturnWhereZeroDoes)
        {
            // Off takes the direction out of braking, as the filter does. Custom 0 brakes with a
            // zero threshold, so its crossing grants a free return like any other.
            RunAll({
                { L"Off", WithEdges(SideBySide(), { OneWay(0, 1, EdgeMode::Off) }), Defaults(), { Expect(Move({ 1919, 500 }, { 1925, 500 }), Allow({ 1925, 500 })), Expect(Move({ 1925, 500 }, { 1915, 500 }), Hold({ 1920, 500 })) } },
                { L"Custom 0", WithEdges(SideBySide(), { OneWay(0, 1, EdgeMode::Custom, 0) }), Defaults(), { Expect(Move({ 1919, 500 }, { 1925, 500 }), Allow({ 1925, 500 })), Expect(Move({ 1925, 500 }, { 1915, 500 }), Allow({ 1915, 500 })) } },
            });
        }
    };

    TEST_CLASS (CornersAndEdgeValues)
    {
    public:
        TEST_METHOD (AWallOnlyPushCarriesNothingAroundTheCorner)
        {
            // Pushes against the wall to TR banked nothing, so the push down at the corner starts
            // from its own 6 and needs 10 four more times before 56 crosses 50. A carried push would
            // cross on the second.
            const Layout layout = WithEdges(Junction(), { OneWay(0, 1, EdgeMode::Wall) });
            RunAll({
                { L"right against the wall, then down",
                  layout,
                  WithDistance(50),
                  Concat(Concat(HeldPushes({ 1919, 1075 }, 1, 0, 10, 3), { Expect(Move({ 1919, 1075 }, { 1919, 1085 }), Hold({ 1919, 1079 })) }),
                         Concat(HeldPushes({ 1919, 1079 }, 0, 1, 10, 4), { Expect(Move({ 1919, 1079 }, { 1919, 1089 }), Allow({ 1919, 1089 })) })) },
            });
        }

        TEST_METHOD (AnEntryForMonitorsMeetingAtACornerChangesNothing)
        {
            // TL and BR touch only at a corner, so no crossing looks their pair up: the diagonal
            // behaves as with no entries.
            RunAll({
                { L"a wall between TL and BR",
                  WithEdges(Junction(), { BothWays(0, 3, EdgeMode::Wall) }),
                  Defaults(),
                  {
                      Expect(Move({ 1919, 1079 }, { 1925, 1085 }), Hold({ 1919, 1079 })),
                      Expect(Move({ 1919, 1079 }, { 1929, 1089 }), Hold({ 1919, 1079 })),
                      Expect(Move({ 1919, 1079 }, { 1928, 1088 }), Allow({ 1928, 1088 })),
                  } },
                { L"the bottom side reached first, TL to BL walled", WithEdges(Junction(), { OneWay(0, 2, EdgeMode::Wall) }), Defaults(), { Expect(Move({ 1900, 1070 }, { 1930, 1090 }), Hold({ 1919, 1079 })) } },
                { L"the bottom side reached first, TL to BL off, still meets TR's brake", WithEdges(Junction(), { OneWay(0, 2, EdgeMode::Off) }), Defaults(), { Expect(Move({ 1900, 1070 }, { 1930, 1090 }), Hold({ 1919, 1079 })) } },
            });
        }
    };

    // A monitor that touches the source only at a corner. The move into it passes two sides of the
    // source, and takes the stricter of the rules those sides have at the corner.
    TEST_CLASS (CornerOnlyNeighbors)
    {
    public:
        // A, B on A's right, and C meeting A only at A's bottom-right corner. `shortB` ends B at
        // y = 800, so nothing borders A's right side at the corner. `withD` adds D below A.
        static Layout Corner(bool shortB, bool withD)
        {
            Layout layout{ { Mon(0, 0, 1920, 1080), Mon(1920, 0, 3840, shortB ? 800 : 1080), Mon(1920, 1080, 3840, 2160) } };
            if (withD)
            {
                layout.monitors.push_back(Mon(0, 1080, 1920, 2160));
            }
            return layout;
        }

        // `count` moves of (dx, dy) from A's corner pixel, every one held there.
        static std::vector<Step> HeldDiagonal(long dx, long dy, int count)
        {
            std::vector<Step> steps;
            for (int i = 0; i < count; ++i)
            {
                steps.push_back(Expect(Move({ 1919, 1079 }, { 1919 + dx, 1079 + dy }), Hold({ 1919, 1079 })));
            }
            return steps;
        }

        TEST_METHOD (AWallOnEitherSideHoldsTheCorner)
        {
            // At the shipping 300 px, each case would reach C on move 30 if the corner did not
            // take the stricter side.
            RunAll({
                { L"only A to B walled, the diagonal down", WithEdges(Corner(false, false), { OneWay(0, 1, EdgeMode::Wall) }), Settings{}, HeldDiagonal(2, 10, 400) },
                { L"A to B and A to D walled", WithEdges(Corner(false, true), { OneWay(0, 1, EdgeMode::Wall), OneWay(0, 3, EdgeMode::Wall) }), Settings{}, HeldDiagonal(2, 10, 400) },
                { L"every shared edge of A walled both ways, B short", WithEdges(Corner(true, true), { BothWays(0, 1, EdgeMode::Wall), BothWays(0, 3, EdgeMode::Wall) }), Settings{}, HeldDiagonal(10, 2, 400) },
                { L"a wall on the side with no part in the move's lead", WithEdges(Corner(false, true), { OneWay(0, 3, EdgeMode::Wall) }), Settings{}, HeldDiagonal(10, 2, 400) },
            });
        }

        TEST_METHOD (TheCrossKeyStillCrossesAWalledCorner)
        {
            RunAll({ { L"only A to B walled, with the cross key", WithEdges(Corner(false, false), { OneWay(0, 1, EdgeMode::Wall) }), Settings{}, { Expect(ModifierMove({ 1919, 1079 }, { 1921, 1089 }), Allow({ 1921, 1089 })) } } });
        }

        TEST_METHOD (WithoutAWallTheCornerBrakesAtTheLargerDistance)
        {
            // Off ranks below any brake, so Off on one side leaves the other side's brake. Two
            // brakes give the larger distance: 50 needs five capped 10 px pushes, where 10 would
            // cross on the first.
            RunAll({
                { L"no entries brake at the global 25", Corner(false, true), Defaults(), CycleFrom(Corner(false, true).monitors[0].rect, { 1919, 1079 }, { { 10, 10 } }, 3) },
                { L"Off below, Default right", WithEdges(Corner(false, true), { OneWay(0, 3, EdgeMode::Off) }), Defaults(), CycleFrom(Corner(false, true).monitors[0].rect, { 1919, 1079 }, { { 10, 10 } }, 3) },
                { L"Off on both sides lets it through", WithEdges(Corner(false, true), { OneWay(0, 1, EdgeMode::Off), OneWay(0, 3, EdgeMode::Off) }), Defaults(), { Expect(Move({ 1919, 1079 }, { 1929, 1089 }), Allow({ 1929, 1089 })) } },
                { L"Custom 50 right, Custom 10 below", WithEdges(Corner(false, true), { OneWay(0, 1, EdgeMode::Custom, 50), OneWay(0, 3, EdgeMode::Custom, 10) }), Defaults(), CycleFrom(Corner(false, true).monitors[0].rect, { 1919, 1079 }, { { 10, 10 } }, 5) },
                { L"Custom 10 right, Custom 50 below", WithEdges(Corner(false, true), { OneWay(0, 1, EdgeMode::Custom, 10), OneWay(0, 3, EdgeMode::Custom, 50) }), Defaults(), CycleFrom(Corner(false, true).monitors[0].rect, { 1919, 1079 }, { { 10, 10 } }, 5) },
                { L"Custom 10 right, Default 25 below", WithEdges(Corner(false, true), { OneWay(0, 1, EdgeMode::Custom, 10) }), Defaults(), CycleFrom(Corner(false, true).monitors[0].rect, { 1919, 1079 }, { { 10, 10 } }, 3) },
            });
        }

        TEST_METHOD (AFreeReturnThroughAWalledCornerHolds)
        {
            // A to C through the corner crosses at the global 25. On the way back, C's top side at
            // the corner borders B, and C to B is a wall, so the free return holds.
            const Layout layout = WithEdges(Corner(false, false), { OneWay(2, 1, EdgeMode::Wall) });
            RunAll({
                { L"back through the corner",
                  layout,
                  Defaults(),
                  Concat(CycleFrom(layout.monitors[0].rect, { 1919, 1079 }, { { 10, 10 } }, 3), { Expect(Move({ 1929, 1089 }, { 1915, 1075 }), Hold({ 1920, 1080 })) }) },
                { L"the same return with no wall is free",
                  Corner(false, false),
                  Defaults(),
                  Concat(CycleFrom(layout.monitors[0].rect, { 1919, 1079 }, { { 10, 10 } }, 3), { Expect(Move({ 1929, 1089 }, { 1915, 1075 }), Allow({ 1915, 1075 })) }) },
            });
        }
    };

    // A move whose proposed point lies on no monitor. Windows puts such a cursor on a monitor of
    // its own choosing, the walled neighbor among them, so a source with a wall out keeps it.
    TEST_CLASS (GapsBesideAWall)
    {
    public:
        // A, B right of A and 400 px higher so a gap borders A's
        // right side below y = 680, C meeting A only at A's bottom-right corner, and D below A.
        static Layout Gap()
        {
            return Layout{ { Mon(0, 0, 1920, 1080), Mon(1920, -400, 3840, 680), Mon(1920, 1080, 3840, 2160), Mon(-1920, 1080, 1920, 3240) } };
        }

        TEST_METHOD (AMoveIntoAGapStaysOnASourceWithAWallOut)
        {
            // From (1919,900), +10 proposes (1929,900) on no monitor, where Windows can place the
            // cursor on B, through the wall from A to B.
            const Step intoGap = Expect(Move({ 1919, 900 }, { 1929, 900 }), Hold({ 1919, 900 }));
            RunAll({
                { L"walls from A to B and A to D", WithEdges(Gap(), { OneWay(0, 1, EdgeMode::Wall), OneWay(0, 3, EdgeMode::Wall) }), Settings{}, { intoGap } },
                { L"a wall on another side of A", WithEdges(Gap(), { OneWay(0, 3, EdgeMode::Wall) }), Settings{}, { intoGap } },
                { L"a both-ways wall", WithEdges(Gap(), { BothWays(1, 0, EdgeMode::Wall) }), Settings{}, { intoGap } },
                { L"past an outer edge", WithEdges(Gap(), { OneWay(0, 1, EdgeMode::Wall) }), Settings{}, { Expect(Move({ 0, 500 }, { -10, 500 }), Hold({ 0, 500 })) } },
            });
        }

        TEST_METHOD (WithoutAWallOutAGapMoveIsLeftToWindows)
        {
            RunAll({
                { L"no entries", Gap(), Settings{}, { Expect(Move({ 1919, 900 }, { 1929, 900 }), Allow({ 1929, 900 })) } },
                { L"a wall into A only", WithEdges(Gap(), { OneWay(1, 0, EdgeMode::Wall), OneWay(3, 0, EdgeMode::Wall) }), Settings{}, { Expect(Move({ 1919, 900 }, { 1929, 900 }), Allow({ 1929, 900 })) } },
                { L"Off and Custom out of A", WithEdges(Gap(), { OneWay(0, 1, EdgeMode::Off), OneWay(0, 3, EdgeMode::Custom, 600) }), Settings{}, { Expect(Move({ 1919, 900 }, { 1929, 900 }), Allow({ 1929, 900 })) } },
                { L"a wall out of A, overridden by an earlier one-way Off", WithEdges(Gap(), { OneWay(0, 1, EdgeMode::Off), BothWays(0, 1, EdgeMode::Wall) }), Settings{}, { Expect(Move({ 1919, 900 }, { 1929, 900 }), Allow({ 1929, 900 })) } },
                { L"the cross key", WithEdges(Gap(), { OneWay(0, 1, EdgeMode::Wall) }), Settings{}, { Expect(ModifierMove({ 1919, 900 }, { 1929, 900 }), Allow({ 1929, 900 })) } },
            });
        }
    };

    TEST_CLASS (EdgeChanges)
    {
    public:
        TEST_METHOD (AnEdgeChangeMidPushResetsThePush)
        {
            // A push of 20, then a change to the entries, then 5 more: a kept push would cross.
            RunAll({
                { L"the default spelled out", SideBySide(), Defaults(), { PushTen(), PushTen(), ExpectAfter(WithEdges(SideBySide(), { BothWays(0, 1, EdgeMode::Custom, 25) }), Move({ 1919, 500 }, { 1924, 500 }), Hold({ 1919, 500 })) } },
                { L"a value changed", WithEdges(SideBySide(), { BothWays(0, 1, EdgeMode::Custom, 25) }), Defaults(), { PushTen(), PushTen(), ExpectAfter(WithEdges(SideBySide(), { BothWays(0, 1, EdgeMode::Custom, 24) }), Move({ 1919, 500 }, { 1924, 500 }), Hold({ 1919, 500 })) } },
                { L"an entry for the other direction", SideBySide(), Defaults(), { PushTen(), PushTen(), ExpectAfter(WithEdges(SideBySide(), { OneWay(1, 0, EdgeMode::Wall) }), Move({ 1919, 500 }, { 1924, 500 }), Hold({ 1919, 500 })) } },
            });
        }
    };

    // A full-screen app fills the monitor MoveEvent::wallMonitor names, and the user chose a wall
    // for full-screen apps: every way out of that monitor is a wall.
    TEST_CLASS (FullScreenWall)
    {
    public:
        TEST_METHOD (RuleForChecksTheFullScreenWallFirst)
        {
            const Layout layout = WithEdges(SideBySide(), { OneWay(0, 1, EdgeMode::Off) });
            const Settings settings = Defaults();
            Assert::IsTrue(RuleFor(layout, settings, 0, 1, true, 0) == EdgeRule{ EdgeMode::Wall, 0 }, L"A full-screen wall on A outranks A's one-way Off.");
            Assert::IsTrue(RuleFor(layout, settings, 0, std::nullopt, false, 0) == EdgeRule{ EdgeMode::Wall, 0 }, L"A full-screen wall covers a side with no neighbor.");
            Assert::IsTrue(RuleFor(layout, settings, 1, 0, true, 0) == EdgeRule{ EdgeMode::Custom, 25 }, L"The way into the full-screen monitor keeps its rule.");
            Assert::IsTrue(RuleFor(layout, settings, 0, 1, true, 1) == EdgeRule{ EdgeMode::Off, 25 }, L"A wall on another monitor leaves A's entry in force.");
            Assert::IsTrue(RuleFor(layout, settings, 0, 1, true) == EdgeRule{ EdgeMode::Off, 25 }, L"No full-screen wall leaves A's entry in force.");
        }

        TEST_METHOD (HasWallOutCountsTheFullScreenWall)
        {
            // A and B do not touch in Gap(), so only the full-screen wall gives A a wall out.
            Assert::IsTrue(HasWallOut(Gap(), Defaults(), 0, 0), L"A full-screen A has a wall out.");
            Assert::IsFalse(HasWallOut(Gap(), Defaults(), 0, 1), L"A wall on B gives A none.");
            Assert::IsFalse(HasWallOut(Gap(), Defaults(), 0), L"No wall at all.");
        }

        TEST_METHOD (AFullScreenWallHoldsEveryPush)
        {
            RunAllWithAndWithoutTheBoundingRect({
                { L"1000 capped pushes at the shipping defaults", SideBySide(), Settings{}, OnWall(HeldPushes({ 1919, 500 }, 1, 0, 30, 1000), 0) },
                { L"1000 slow pushes", SideBySide(), Defaults(), OnWall(HeldPushes({ 1919, 500 }, 1, 0, 1, 1000), 0) },
                { L"the right monitor full screen", SideBySide(), Settings{}, OnWall(HeldPushes({ 1920, 500 }, -1, 0, 30, 1000), 1) },
                { L"down from the top-left of a junction", Junction(), Defaults(), OnWall(HeldPushes({ 500, 1079 }, 0, 1, 10, 100), 0) },
                { L"right from the top-left of a junction", Junction(), Defaults(), OnWall(HeldPushes({ 1919, 500 }, 1, 0, 10, 100), 0) },
                { L"through the corner of a junction", Junction(), Defaults(), OnWall(HeldPushes({ 1919, 1079 }, 1, 1, 10, 100), 0) },
            });
        }

        TEST_METHOD (BypassesCrossAFullScreenWall)
        {
            RunAll({
                { L"cross modifier held", SideBySide(), Defaults(), { OnWall(Expect(ModifierMove({ 1919, 500 }, { 1925, 500 }), Allow({ 1925, 500 })), 0) } },
                { L"cross modifier held while dragging", SideBySide(), WithDragDisable(), { OnWall(Expect(DragModifierMove({ 1919, 500 }, { 1925, 500 }), Allow({ 1925, 500 })), 0) } },
                { L"absolute input", SideBySide(), Defaults(), { OnWall(Expect(AbsoluteMove({ 1919, 500 }, { 1925, 500 }), Allow({ 1925, 500 })), 0) } },
            });
        }

        TEST_METHOD (DraggingNeverCrossesAFullScreenWall)
        {
            // A game holds a button down most of the time, so a drag must not open the wall that
            // only the cross key crosses.
            RunAllWithAndWithoutTheBoundingRect({
                { L"dragging with disable-while-dragging on", SideBySide(), WithDragDisable(), OnWall(HeldDrags({ 1919, 500 }, 1, 0, 30, 100), 0) },
                { L"dragging with the setting off", SideBySide(), Defaults(), OnWall(HeldDrags({ 1919, 500 }, 1, 0, 30, 100), 0) },
                { L"dragging through the corner of a junction", Junction(), WithDragDisable(), OnWall(HeldDrags({ 1919, 1079 }, 1, 1, 10, 100), 0) },
                { L"dragging into a gap", Gap(), WithDragDisable(), { OnWall(Expect(DragMove({ 1919, 900 }, { 1929, 900 }), Hold({ 1919, 900 })), 0) } },
            });
        }

        TEST_METHOD (DraggingStillCrossesEveryOtherWay)
        {
            // Only the way out of the full-screen monitor ignores the drag. The way in and a
            // per-edge wall between two other monitors keep the drag bypass, as without a
            // full-screen app.
            RunAll({
                { L"into the full-screen monitor", SideBySide(), WithDragDisable(), { OnWall(Expect(DragMove({ 1920, 500 }, { 1915, 500 }), Allow({ 1915, 500 })), 0) } },
                { L"a per-edge wall between two other monitors", WithEdges(Junction(), { BothWays(2, 3, EdgeMode::Wall) }), WithDragDisable(), { OnWall(Expect(DragMove({ 1919, 1500 }, { 1925, 1500 }), Allow({ 1925, 1500 })), 0) } },
                { L"a per-edge wall with no full-screen app", WithEdges(SideBySide(), { BothWays(0, 1, EdgeMode::Wall) }), WithDragDisable(), { Expect(DragMove({ 1919, 500 }, { 1925, 500 }), Allow({ 1925, 500 })) } },
            });
        }

        TEST_METHOD (AKeyedCrossingOpensNoFreeReturn)
        {
            // The way back into the full-screen monitor brakes as usual: 5 of 25 holds.
            RunAll({
                { L"out with the key, straight back",
                  SideBySide(),
                  Defaults(),
                  OnWall({ Expect(ModifierMove({ 1919, 500 }, { 1925, 500 }), Allow({ 1925, 500 })), Expect(Move({ 1925, 500 }, { 1915, 500 }), Hold({ 1920, 500 })) }, 0) },
            });
        }

        TEST_METHOD (AFreeReturnIntoTheFullScreenWallHolds)
        {
            // B is full screen. The crossing from A into B brakes as usual and grants a free
            // return, which the wall out of B refuses when it is used.
            RunAllWithAndWithoutTheBoundingRect({
                { L"straight back after the crossing",
                  SideBySide(),
                  Defaults(),
                  OnWall(Concat(Concat(CrossToB(), { Expect(Move({ 1924, 500 }, { 1915, 500 }), Hold({ 1920, 500 })) }),
                                Concat(HeldPushes({ 1920, 500 }, -1, 0, 10, 100), { Expect(ModifierMove({ 1920, 500 }, { 1915, 500 }), Allow({ 1915, 500 })) })),
                         1) },
            });
        }

        TEST_METHOD (AFullScreenWallAddsNothingToThePush)
        {
            // 100 pushes against the wall, then the full-screen app leaves the front. The next push
            // starts from 0 and needs 10 + 10 + 5.
            RunAll({
                { L"lean, clear, push",
                  SideBySide(),
                  Defaults(),
                  Concat(OnWall(HeldPushes({ 1919, 500 }, 1, 0, 10, 100), 0), { PushTen(), PushTen(), Expect(Move({ 1919, 500 }, { 1924, 500 }), Allow({ 1924, 500 })) }) },
            });
        }

        TEST_METHOD (AWallMonitorChangeResetsThePushBothWays)
        {
            // Junction, pushing TL's right edge at y = 500 under a 25 px push. A kept push of 20
            // would cross on the next 5.
            const Step along = Expect(Move({ 1919, 500 }, { 1919, 501 }), Allow({ 1919, 501 }));
            const Step five = Expect(Move({ 1919, 501 }, { 1924, 501 }), Hold({ 1919, 501 }));
            RunAllWithAndWithoutTheBoundingRect({
                { L"set on the source mid-push, then cleared",
                  Junction(),
                  Defaults(),
                  Concat(Concat({ PushTen(), PushTen() }, OnWall(HeldPushes({ 1919, 500 }, 1, 0, 5, 5), 0)), { PushTen(), PushTen(), Expect(Move({ 1919, 500 }, { 1924, 500 }), Allow({ 1924, 500 })) }) },
                { L"set on another monitor and cleared, away from the pushed edge",
                  Junction(),
                  Defaults(),
                  { PushTen(), PushTen(), OnWall(along, 3), five } },
                { L"cleared after a push banked under a wall elsewhere",
                  Junction(),
                  Defaults(),
                  Concat(OnWall({ PushTen(), PushTen(), along }, 3), { five }) },
                { L"moved from one monitor to another",
                  Junction(),
                  Defaults(),
                  Concat(OnWall({ PushTen(), PushTen() }, 3), { OnWall(along, 2), OnWall(five, 2) }) },
            });
        }

        TEST_METHOD (AnUnchangedWallMonitorKeepsThePush)
        {
            // A wall named on every move, here on BR, resets nothing: 20 and 5 cross.
            RunAll({
                { L"held across moves inside the source",
                  Junction(),
                  Defaults(),
                  OnWall({ PushTen(), PushTen(), Expect(Move({ 1919, 500 }, { 1919, 501 }), Allow({ 1919, 501 })), Expect(Move({ 1919, 501 }, { 1919, 500 }), Allow({ 1919, 500 })), Expect(Move({ 1919, 500 }, { 1924, 500 }), Allow({ 1924, 500 })) }, 3) },
            });
        }

        TEST_METHOD (AWallMonitorChangeClearsTheFreeReturn)
        {
            RunAll({
                { L"a wall elsewhere appears before the return", Junction(), Defaults(), Concat(CrossToB(), { OnWall(Expect(Move({ 1924, 500 }, { 1915, 500 }), Hold({ 1920, 500 })), 3) }) },
                { L"an unchanged wall elsewhere keeps it", Junction(), Defaults(), OnWall(Concat(CrossToB(), { Expect(Move({ 1924, 500 }, { 1915, 500 }), Allow({ 1915, 500 })) }), 3) },
            });
        }

        TEST_METHOD (TheFullScreenWallCoversOnlyTheWayOut)
        {
            // TL is full screen. Every other crossing brakes at the global 25: three 10 px steps.
            RunAllWithAndWithoutTheBoundingRect({
                { L"TR into TL", Junction(), Defaults(), OnWall(CrossOnStep({ 1920, 500 }, -1, 0, 10, 3), 0) },
                { L"BL into TL", Junction(), Defaults(), OnWall(CrossOnStep({ 500, 1080 }, 0, -1, 10, 3), 0) },
                { L"TR into BR", Junction(), Defaults(), OnWall(CrossOnStep({ 2500, 1079 }, 0, 1, 10, 3), 0) },
                { L"BL into BR", Junction(), Defaults(), OnWall(CrossOnStep({ 1919, 1500 }, 1, 0, 10, 3), 0) },
            });
        }

        TEST_METHOD (TheFullScreenWallOutranksEveryEdgeValue)
        {
            RunAllWithAndWithoutTheBoundingRect({
                { L"a both-ways Off", WithEdges(SideBySide(), { BothWays(0, 1, EdgeMode::Off) }), Defaults(), OnWall(HeldPushes({ 1919, 500 }, 1, 0, 10, 100), 0) },
                { L"a one-way Custom 0", WithEdges(SideBySide(), { OneWay(0, 1, EdgeMode::Custom, 0) }), Defaults(), OnWall(HeldPushes({ 1919, 500 }, 1, 0, 10, 100), 0) },
                { L"a one-way Custom 600", WithEdges(SideBySide(), { OneWay(0, 1, EdgeMode::Custom, 600) }), Defaults(), OnWall(HeldPushes({ 1919, 500 }, 1, 0, 10, 100), 0) },
                { L"an axis the filter leaves out", SideBySide(), WithFilter(EdgeFilter::Stacked), OnWall(HeldPushes({ 1919, 500 }, 1, 0, 10, 100), 0) },
                { L"an entry into the full-screen monitor keeps its 50", WithEdges(Junction(), { OneWay(1, 0, EdgeMode::Custom, 50) }), Defaults(), OnWall(CrossOnStep({ 1920, 500 }, -1, 0, 10, 5), 0) },
                { L"a wall between two other monitors still holds", WithEdges(Junction(), { BothWays(1, 3, EdgeMode::Wall) }), Defaults(), OnWall(HeldPushes({ 2500, 1079 }, 0, 1, 10, 100), 0) },
            });
        }

        TEST_METHOD (AFullScreenWallKeepsAGapMove)
        {
            // In Gap(), A shares no edge, so only the full-screen wall keeps a move into the gap.
            RunAllWithAndWithoutTheBoundingRect({
                { L"A shares no edge", Gap(), Defaults(), { OnWall(Expect(Move({ 1919, 500 }, { 1929, 500 }), Hold({ 1919, 500 })), 0) } },
                { L"A shares edges elsewhere", GapsBesideAWall::Gap(), Settings{}, { OnWall(Expect(Move({ 1919, 900 }, { 1929, 900 }), Hold({ 1919, 900 })), 0) } },
                { L"the cross key", Gap(), Defaults(), { OnWall(Expect(ModifierMove({ 1919, 500 }, { 1929, 500 }), Allow({ 1929, 500 })), 0) } },
                { L"a wall on the other monitor", Gap(), Defaults(), { OnWall(Expect(Move({ 1919, 500 }, { 1929, 500 }), Allow({ 1929, 500 })), 1) } },
            });
        }

        TEST_METHOD (AGameClipOnTheFullScreenMonitorNeverHolds)
        {
            // The game confines the cursor to A, so Windows clamps every move back onto A and the
            // wall has nothing to hold.
            std::vector<Step> steps;
            for (int i = 0; i < 100; ++i)
            {
                steps.push_back(Expect(Move({ 1919, 500 }, { 1929, 500 }), Allow({ 1929, 500 })));
            }
            RunAll({ { L"a clip on A", SideBySide(), Settings{}, OnWall(UnderClip(steps, SideBySide().monitors[0].rect), 0) } });
        }

        TEST_METHOD (AWallMonitorPastTheLayoutChangesNothing)
        {
            RunAll({ { L"index 7 of 2", SideBySide(), Defaults(), OnWall(CrossToB(), 7) } });
        }
    };
}
