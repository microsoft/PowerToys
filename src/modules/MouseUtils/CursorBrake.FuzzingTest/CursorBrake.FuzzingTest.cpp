// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

// libFuzzer target for the CursorBrake engine.
//
// CursorBrake is a user-input module, so PowerToys policy requires a fuzz target (AGENTS.md: "New
// modules handling file I/O or user input must implement fuzzing tests"). The decision logic
// lives in the Win32-free cursorbrake::Engine (CursorBrakeCore.h), so it runs here under ASan with
// no OS state. The fuzzer's bytes become monitor layouts with per-edge settings, settings
// snapshots, cursor clips, full-screen walls and a sequence of moves, including degenerate and
// overlapping rects, full-screen walls naming no monitor or one past the layout,
// entries naming missing monitors, empty and inverted clips, extreme coordinates and DPI values,
// and settings a hand-edited settings.json could carry, out-of-range enum values among them.
//
// Every decision is checked against the engine's contract. Allow returns the proposed point
// unchanged, and never takes a move across a shared edge whose rule in that direction is Wall, nor
// through a corner where either side the move passes is a wall, nor onto no monitor from a monitor
// with a wall out, nor off the monitor a full-screen wall names, unless the move is bypassed. Hold
// happens only for a move that lands on another monitor once the clip clamps it, or on no monitor
// from a monitor with a wall out, the full-screen wall's monitor included. It returns a
// point inside the monitor the cursor started on, and inside the clip when the cursor started
// inside it. A bypassed move is never held. Spelled-out forms of the same rules decide every move
// alike (see Equivalent). A broken contract aborts, which libFuzzer reports as a crash.

#include <algorithm>
#include <crtdbg.h>
#include <cstddef>
#include <cstdint>
#include <cstdio>
#include <cstdlib>
#include <filesystem>
#include <fstream>
#include <iterator>
#include <optional>
#include <utility>
#include <vector>

#include "CursorBrakeCore.h"

using namespace cursorbrake;

namespace
{
    // Bounds-checked reader over the fuzzer buffer: reads past the end yield 0 so the harness
    // never reads out of range (ASan would flag it otherwise).
    struct Reader
    {
        const uint8_t* data;
        size_t size;
        size_t pos = 0;

        explicit Reader(const uint8_t* d, size_t n) :
            data(d), size(n) {}

        bool done() const { return pos >= size; }

        uint8_t u8()
        {
            return pos < size ? data[pos++] : static_cast<uint8_t>(0);
        }

        uint32_t u32()
        {
            uint32_t v = 0;
            for (int i = 0; i < 4; ++i)
            {
                v = (v << 8) | u8();
            }
            return v;
        }

        // Mostly multiples of 8 near the origin, so decoded monitors often share edges; a wide
        // value covers the full 32-bit range instead.
        long Edge(bool wide)
        {
            if (wide)
            {
                return static_cast<long>(static_cast<int32_t>(u32()));
            }
            return static_cast<long>(static_cast<int8_t>(u8())) * 8;
        }

        // An edge-like value with a few pixels of jitter, so decoded points often sit on or just
        // past a monitor edge.
        long Point(bool wide)
        {
            const long edge = Edge(wide);
            if (wide)
            {
                return edge;
            }
            return edge + static_cast<long>(u8() % 17) - 8;
        }
    };

    Layout ReadLayout(Reader& r)
    {
        static constexpr unsigned int DPI_VALUES[] = { 0, 96, 120, 144, 192 };

        Layout layout;
        const uint8_t header = r.u8();
        const bool wide = (header & 0x80u) != 0u;
        const size_t count = header % 6u;
        for (size_t i = 0; i < count; ++i)
        {
            Monitor monitor;
            monitor.rect.left = r.Edge(wide);
            monitor.rect.top = r.Edge(wide);
            monitor.rect.right = r.Edge(wide);
            monitor.rect.bottom = r.Edge(wide);
            const uint8_t dpi = r.u8();
            monitor.dpi = dpi < std::size(DPI_VALUES) ? DPI_VALUES[dpi] : (dpi == 0xFFu ? r.u32() : BASE_DPI);
            layout.monitors.push_back(monitor);
        }

        // Per-edge settings, one-way and both-ways, with indices that can name a missing monitor
        // or the same monitor twice, and a mode past the known ones.
        const size_t edgeCount = r.u8() % 9u;
        for (size_t i = 0; i < edgeCount; ++i)
        {
            EdgeSetting edge;
            edge.first = r.u8() % (count + 1u);
            edge.second = r.u8() % (count + 1u);
            const uint8_t flags = r.u8();
            edge.oneWay = (flags & 0x01u) != 0u;
            edge.mode = static_cast<EdgeMode>((flags >> 1) & 0x03u);
            edge.pushDistance = (flags & 0x80u) != 0u ? static_cast<int>(r.u32()) : static_cast<int>(r.u8() % 64u);
            layout.edges.push_back(edge);
        }
        return layout;
    }

    // The engine's own rule for which monitor holds a point: the first containing it.
    std::optional<size_t> IndexAt(const Layout& layout, PointL point)
    {
        for (size_t i = 0; i < layout.monitors.size(); ++i)
        {
            const RectL& rect = layout.monitors[i].rect;
            if (point.x >= rect.left && point.x < rect.right && point.y >= rect.top && point.y < rect.bottom)
            {
                return i;
            }
        }
        return std::nullopt;
    }

    // The axis of the edge two rects share, side by side (true) or stacked (false), if they
    // share one. Two rects can share at most one edge.
    std::optional<bool> SharedAxis(const RectL& a, const RectL& b)
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

    // Spelled-out forms of the same rules, which the engine must treat move for move like the
    // layout they come from.
    //  - Every pair sharing an edge set to Custom at the global distance, both ways, under a
    //    different global distance, against no entries. Exact while the filter brakes both axes,
    //    for moves onto a monitor that shares the crossed edge; a crossing onto any other monitor
    //    has no pair and takes the global distance.
    //  - Every both-ways entry split into two one-way entries placed after the existing one-way
    //    entries, against the entries as given. Always exact.
    //  - The filter replaced by All plus an Off entry for every pair sharing an edge on a
    //    filtered axis, against the filter. Exact for moves onto a monitor that shares the
    //    crossed edge, like the first.
    Layout ExplicitDefaults(const Layout& layout, const Settings& settings)
    {
        Layout spelled{ layout.monitors, {} };
        for (size_t i = 0; i < layout.monitors.size(); ++i)
        {
            for (size_t j = i + 1; j < layout.monitors.size(); ++j)
            {
                if (SharedAxis(layout.monitors[i].rect, layout.monitors[j].rect))
                {
                    spelled.edges.push_back(EdgeSetting{ i, j, false, EdgeMode::Custom, settings.pushDistance });
                }
            }
        }
        return spelled;
    }

    Layout SplitBothWays(const Layout& layout)
    {
        Layout split{ layout.monitors, {} };
        for (const EdgeSetting& edge : layout.edges)
        {
            if (edge.oneWay)
            {
                split.edges.push_back(edge);
            }
        }
        for (const EdgeSetting& edge : layout.edges)
        {
            if (!edge.oneWay)
            {
                split.edges.push_back(EdgeSetting{ edge.first, edge.second, true, edge.mode, edge.pushDistance });
                split.edges.push_back(EdgeSetting{ edge.second, edge.first, true, edge.mode, edge.pushDistance });
            }
        }
        return split;
    }

    Layout FilterAsOff(const Layout& layout, const Settings& settings)
    {
        Layout spelled = layout;
        for (size_t i = 0; i < layout.monitors.size(); ++i)
        {
            for (size_t j = i + 1; j < layout.monitors.size(); ++j)
            {
                const std::optional<bool> sideBySide = SharedAxis(layout.monitors[i].rect, layout.monitors[j].rect);
                if (sideBySide && RuleFor(Layout{}, settings, i, std::nullopt, *sideBySide).mode == EdgeMode::Off)
                {
                    spelled.edges.push_back(EdgeSetting{ i, j, false, EdgeMode::Off, 0 });
                }
            }
        }
        return spelled;
    }

    // A global distance other than the one given, so an entry's distance and the global one differ.
    Settings WithOtherDistance(Settings settings)
    {
        settings.pushDistance = settings.pushDistance / 2 + 1;
        return settings;
    }

    Settings WithAllEdges(Settings settings)
    {
        settings.edgeFilter = EdgeFilter::All;
        return settings;
    }

    bool BrakesBothAxes(const Settings& settings)
    {
        return RuleFor(Layout{}, settings, 0, std::nullopt, true).mode != EdgeMode::Off && RuleFor(Layout{}, settings, 0, std::nullopt, false).mode != EdgeMode::Off;
    }

    Settings ReadSettings(Reader& r)
    {
        Settings settings;
        const uint8_t flags = r.u8();
        settings.pushDistance = (flags & 0x80u) != 0u ? static_cast<int>(r.u32()) : static_cast<int>(r.u8() % 64u);
        // Two bits give the three filters plus one value past them; a flag bit reads any byte.
        settings.edgeFilter = static_cast<EdgeFilter>((flags & 0x40u) != 0u ? r.u8() : flags & 0x03u);
        settings.disableWhileDragging = (flags & 0x04u) != 0u;
        settings.crossModifier = static_cast<CrossModifier>((flags >> 4) & 0x03u);
        settings.freeReturn = (flags & 0x08u) == 0u;
        return settings;
    }

    // A clip from the clip op's header byte: bit 0 clear means the hook could not read one. Bit 1
    // clips to one of the layout's monitors, the way a full-screen game confines the cursor;
    // otherwise bit 7 picks wide coordinates for a free rect, which can be empty or inverted, as a
    // hostile caller could pass.
    std::optional<RectL> ReadClip(Reader& r, const Layout& layout, uint8_t header)
    {
        if ((header & 0x01u) == 0u)
        {
            return std::nullopt;
        }
        if ((header & 0x02u) != 0u && !layout.monitors.empty())
        {
            return layout.monitors[r.u8() % layout.monitors.size()].rect;
        }
        const bool wide = (header & 0x80u) != 0u;
        RectL clip;
        clip.left = r.Edge(wide);
        clip.top = r.Edge(wide);
        clip.right = r.Edge(wide);
        clip.bottom = r.Edge(wide);
        return clip;
    }

    // The full-screen wall from the clip op's header byte, which bits 0, 1 and 7 leave free for
    // it: bit 2 set names a wall from bits 3 to 6, and bit 2 clear keeps the wall in effect. The
    // value picks a monitor, no wall, or an index past the layout, which must change nothing but
    // the reset a change brings.
    std::optional<std::optional<size_t>> ReadWall(uint8_t header, const Layout& layout)
    {
        if ((header & 0x04u) == 0u)
        {
            return std::nullopt;
        }
        const size_t count = layout.monitors.size();
        const size_t value = static_cast<size_t>((header >> 3) & 0x0Fu) % (count + 2u);
        if (value == count)
        {
            return std::optional<size_t>{};
        }
        return std::optional<size_t>{ value };
    }

    // Where Windows puts a point: clamped into the clip in effect, if there is one.
    PointL Landing(PointL point, const std::optional<RectL>& clip)
    {
        if (!clip)
        {
            return point;
        }
        return PointL{ std::clamp(point.x, clip->left, clip->right - 1), std::clamp(point.y, clip->top, clip->bottom - 1) };
    }

    // The engine's own rule: the first monitor containing the point is the one the point is on.
    const Monitor* ContainingMonitor(const Layout& layout, PointL point)
    {
        for (const Monitor& monitor : layout.monitors)
        {
            const RectL& rect = monitor.rect;
            if (point.x >= rect.left && point.x < rect.right && point.y >= rect.top && point.y < rect.bottom)
            {
                return &monitor;
            }
        }
        return nullptr;
    }

    bool Contains(const RectL& rect, PointL point)
    {
        return point.x >= rect.left && point.x < rect.right && point.y >= rect.top && point.y < rect.bottom;
    }

    // Whether a move that leaves `source` past both a side-by-side side and a stacked side, onto a
    // monitor that shares no edge with it, meets a wall at that corner. Each side's rule comes from
    // the first monitor across it that spans the corner pixel, or the default without one.
    bool CornerWalled(const Layout& layout, const Settings& settings, size_t source, PointL proposed)
    {
        const RectL& rect = layout.monitors[source].rect;
        const bool right = proposed.x >= rect.right;
        const bool below = proposed.y >= rect.bottom;
        const long cornerX = right ? rect.right - 1 : rect.left;
        const long cornerY = below ? rect.bottom - 1 : rect.top;
        std::optional<size_t> across;
        std::optional<size_t> stackedOn;
        for (size_t i = 0; i < layout.monitors.size(); ++i)
        {
            const RectL& other = layout.monitors[i].rect;
            if (i == source)
            {
                continue;
            }
            const bool overlapY = other.top < rect.bottom && rect.top < other.bottom;
            const bool overlapX = other.left < rect.right && rect.left < other.right;
            if (!across && (right ? other.left == rect.right : other.right == rect.left) && overlapY && cornerY >= other.top && cornerY < other.bottom)
            {
                across = i;
            }
            if (!stackedOn && (below ? other.top == rect.bottom : other.bottom == rect.top) && overlapX && cornerX >= other.left && cornerX < other.right)
            {
                stackedOn = i;
            }
        }
        return RuleFor(layout, settings, source, across, true).mode == EdgeMode::Wall || RuleFor(layout, settings, source, stackedOn, false).mode == EdgeMode::Wall;
    }

    // Whether some monitor sharing an edge with `source` meets a Wall on the way out of it.
    bool WalledOut(const Layout& layout, const Settings& settings, size_t source)
    {
        for (size_t i = 0; i < layout.monitors.size(); ++i)
        {
            if (i == source)
            {
                continue;
            }
            const std::optional<bool> sideBySide = SharedAxis(layout.monitors[source].rect, layout.monitors[i].rect);
            if (sideBySide && RuleFor(layout, settings, source, i, *sideBySide).mode == EdgeMode::Wall)
            {
                return true;
            }
        }
        return false;
    }

    // A drag is a bypass everywhere but on the monitor a full-screen wall names, whose way out
    // only the cross key crosses. `clip` is the clip in effect, as for CheckContract.
    bool Bypassed(const MoveEvent& move, const std::optional<RectL>& clip, const Layout& layout, const Settings& settings)
    {
        const bool onWallMonitor = move.wallMonitor && IndexAt(layout, Landing(move.current, clip)) == move.wallMonitor;
        return layout.monitors.size() < 2 || move.input == InputKind::Absolute || move.crossModifierHeld || (move.buttonDown && settings.disableWhileDragging && !onWallMonitor);
    }

    // Whether `source` has a wall out, from an entry or from the full-screen wall the move names.
    bool WalledOut(const Layout& layout, const Settings& settings, size_t source, const MoveEvent& move)
    {
        return move.wallMonitor == source || WalledOut(layout, settings, source);
    }

    // `clip` is the clip in effect: the last usable one a move carried, or none.
    void CheckContract(const MoveEvent& move, const std::optional<RectL>& clip, const Layout& layout, const Settings& settings, const Decision& decision)
    {
        const bool bypassed = Bypassed(move, clip, layout, settings);
        if (decision.kind == DecisionKind::Allow)
        {
            if (!(decision.point == move.proposed))
            {
                std::abort();
            }

            // A move off a monitor onto one sharing the crossed edge, in a direction whose rule is
            // Wall, gets through only by a bypass. The free return is the only other way to an
            // Allow, so this covers it too.
            const PointL current = Landing(move.current, clip);
            const PointL proposed = Landing(move.proposed, clip);
            const std::optional<size_t> source = IndexAt(layout, current);
            const std::optional<size_t> destination = IndexAt(layout, proposed);

            // A move onto no monitor leaves the source only when no way out of it is a wall,
            // since Windows may put the cursor on any monitor from there.
            if (!bypassed && source && !destination && WalledOut(layout, settings, *source, move))
            {
                std::abort();
            }

            // Every way out of the monitor a full-screen wall names is a wall.
            if (!bypassed && source && destination && *source != *destination && move.wallMonitor == source && !Contains(layout.monitors[*source].rect, proposed))
            {
                std::abort();
            }
            if (!bypassed && source && destination && *source != *destination && !Contains(layout.monitors[*source].rect, proposed))
            {
                const RectL& from = layout.monitors[*source].rect;
                const std::optional<bool> sideBySide = SharedAxis(from, layout.monitors[*destination].rect);
                if (sideBySide && RuleFor(layout, settings, *source, *destination, *sideBySide).mode == EdgeMode::Wall)
                {
                    std::abort();
                }

                // A move onto a monitor sharing no edge, past both axes of the source, goes through
                // a corner, and a wall on either side there holds it.
                const bool pastX = proposed.x < from.left || proposed.x >= from.right;
                const bool pastY = proposed.y < from.top || proposed.y >= from.bottom;
                if (!sideBySide && pastX && pastY && CornerWalled(layout, settings, *source, proposed))
                {
                    std::abort();
                }
            }
            return;
        }

        const Monitor* source = ContainingMonitor(layout, Landing(move.current, clip));
        const Monitor* destination = ContainingMonitor(layout, Landing(move.proposed, clip));
        const bool intoGap = source != nullptr && destination == nullptr && WalledOut(layout, settings, static_cast<size_t>(source - layout.monitors.data()), move);
        if (bypassed || source == nullptr || (destination == nullptr && !intoGap) || destination == source || !Contains(source->rect, decision.point))
        {
            std::abort();
        }
        // A hold from inside the clip stays inside it, so Windows leaves the cursor where it is put.
        if (clip && Contains(*clip, move.current) && !Contains(*clip, decision.point))
        {
            std::abort();
        }
    }

    // Two engines fed the same moves under inputs that must decide alike. An engine starts over
    // when its settings or layout change, so the two stay comparable only while they start over
    // together; one starting over alone ends the comparison until both next do.
    struct Equivalent
    {
        Engine first;
        Engine second;
        Layout firstLayout;
        Settings firstSettings;
        Layout secondLayout;
        Settings secondSettings;
        bool comparable = true;

        void Update(Layout layoutA, const Settings& settingsA, Layout layoutB, const Settings& settingsB)
        {
            const bool changedA = !(layoutA == firstLayout) || !(settingsA == firstSettings);
            const bool changedB = !(layoutB == secondLayout) || !(settingsB == secondSettings);
            if (changedA != changedB)
            {
                comparable = false;
            }
            else if (changedA)
            {
                comparable = true;
            }
            firstLayout = std::move(layoutA);
            firstSettings = settingsA;
            secondLayout = std::move(layoutB);
            secondSettings = settingsB;
        }

        // Decides the move in both engines and aborts when they differ while `compare` holds.
        void Decide(const MoveEvent& move, bool compare)
        {
            const Decision a = first.OnMove(move, firstLayout, firstSettings);
            const Decision b = second.OnMove(move, secondLayout, secondSettings);
            if (compare && comparable && !(a == b))
            {
                std::abort();
            }
        }
    };
}

extern "C" int LLVMFuzzerTestOneInput(const uint8_t* data, size_t size)
{
    Reader r(data, size);

    Engine engine;
    Layout layout = ReadLayout(r);
    Settings settings = ReadSettings(r);
    // The clip the hook reads on every move, and the one in effect: a usable read replaces it,
    // an empty or inverted one ends it, and a failed read leaves it.
    std::optional<RectL> reported;
    std::optional<RectL> clip;
    // The full-screen wall the hook passes on every move until it works out another.
    std::optional<size_t> wall;
    PointL cursor{};

    Equivalent defaults;
    Equivalent split;
    Equivalent filter;
    bool changed = true;

    // Bound the number of moves so any single input terminates regardless of size.
    for (int step = 0; step < 4096 && !r.done(); ++step)
    {
        const uint8_t op = r.u8();

        // Occasionally swap the layout or settings mid-sequence, the way a display change or a
        // settings save reaches the hook between two moves.
        if ((op & 0xC0u) == 0xC0u)
        {
            layout = ReadLayout(r);
            changed = true;
        }
        else if ((op & 0xC0u) == 0x80u)
        {
            settings = ReadSettings(r);
            changed = true;
        }
        if (changed)
        {
            defaults.Update(Layout{ layout.monitors, {} }, settings, ExplicitDefaults(layout, settings), WithOtherDistance(settings));
            split.Update(layout, settings, SplitBothWays(layout), settings);
            filter.Update(layout, settings, FilterAsOff(layout, settings), WithAllEdges(settings));
            changed = false;
        }
        bool readFailed = false;
        if ((op & 0xC0u) == 0x40u)
        {
            // A clip set or lifted between moves, the way a game confines and frees the cursor, or
            // a read that fails for this move. The same header can set or clear the full-screen
            // wall, the way a full-screen app comes to the front or leaves it.
            const uint8_t header = r.u8();
            if (const std::optional<std::optional<size_t>> read = ReadWall(header, layout))
            {
                wall = *read;
            }
            const std::optional<RectL> read = ReadClip(r, layout, header);
            readFailed = !read;
            if (read)
            {
                reported = read;
                const bool usable = read->right > read->left && read->bottom > read->top;
                clip = usable ? read : std::nullopt;
            }
        }

        MoveEvent move;
        move.clip = readFailed ? std::nullopt : reported;
        move.wallMonitor = wall;
        move.input = (op & 0x01u) != 0u ? InputKind::Absolute : InputKind::Relative;
        move.crossModifierHeld = (op & 0x02u) != 0u;
        move.buttonDown = (op & 0x04u) != 0u;

        // Mostly continue from where the last decision put the cursor, so pushes accumulate the
        // way they do under a real hand; otherwise start from an arbitrary point.
        const bool wide = (op & 0x20u) != 0u;
        move.current = (op & 0x08u) != 0u ? PointL{ r.Point(wide), r.Point(wide) } : cursor;
        if ((op & 0x10u) != 0u)
        {
            move.proposed = PointL{ r.Point(wide), r.Point(wide) };
        }
        else
        {
            // A small relative step, computed in 64 bits and clamped so it cannot overflow long.
            const int64_t x = static_cast<int64_t>(move.current.x) + static_cast<int8_t>(r.u8());
            const int64_t y = static_cast<int64_t>(move.current.y) + static_cast<int8_t>(r.u8());
            move.proposed = PointL{ static_cast<long>(std::clamp<int64_t>(x, INT32_MIN, INT32_MAX)), static_cast<long>(std::clamp<int64_t>(y, INT32_MIN, INT32_MAX)) };
        }

        const Decision decision = engine.OnMove(move, layout, settings);
        CheckContract(move, clip, layout, settings, decision);
        cursor = decision.point;

        // An entry has a pair to stand for the default only where the move lands on a monitor that
        // shares the crossed edge.
        const PointL landedFrom = Landing(move.current, clip);
        const PointL landedTo = Landing(move.proposed, clip);
        const std::optional<size_t> source = IndexAt(layout, landedFrom);
        const std::optional<size_t> destination = IndexAt(layout, landedTo);
        if (!Bypassed(move, clip, layout, settings) && source && destination && *source != *destination && !Contains(layout.monitors[*source].rect, landedTo) && !SharedAxis(layout.monitors[*source].rect, layout.monitors[*destination].rect))
        {
            defaults.comparable = false;
            filter.comparable = false;
        }

        defaults.Decide(move, BrakesBothAxes(settings));
        split.Decide(move, true);
        filter.Decide(move, true);
    }

    return 0;
}

#ifndef DISABLE_FOR_FUZZING

namespace
{
    // Runs one input between two debug-heap checkpoints. The engine and every layout it copies
    // live only inside LLVMFuzzerTestOneInput, so a block still allocated afterwards is a leak.
    bool RunWithoutLeak(const std::vector<uint8_t>& input)
    {
        _CrtMemState before;
        _CrtMemState after;
        _CrtMemState difference;
        _CrtMemCheckpoint(&before);
        (void)LLVMFuzzerTestOneInput(input.data(), input.size());
        _CrtMemCheckpoint(&after);
        if (_CrtMemDifference(&difference, &before, &after))
        {
            _CrtMemDumpStatistics(&difference);
            return false;
        }
        return true;
    }

    bool ReadInput(const std::filesystem::path& path, std::vector<uint8_t>& input)
    {
        std::ifstream file(path, std::ios::binary | std::ios::ate);
        if (!file)
        {
            return false;
        }
        input.resize(static_cast<size_t>(file.tellg()));
        file.seekg(0);
        return static_cast<bool>(file.read(reinterpret_cast<char*>(input.data()), static_cast<std::streamsize>(input.size())));
    }
}

// Plain entry point for the non-fuzzing (Debug) configuration, so the project runs without the
// libFuzzer driver. Arguments name input files or folders, such as a libFuzzer corpus, to replay.
// With none it runs one canned input: two side-by-side monitors, a run of rightward pushes that
// crosses their shared edge, a fling straight back that takes the free return, a push under a clip
// to the left monitor, a diagonal held inside a clip across the edge, and pushes against a
// full-screen wall on the left monitor that only the cross key crosses. Every input runs under the
// debug heap's leak check.
int main(int argc, char** argv)
{
    _CrtSetDbgFlag(_CrtSetDbgFlag(_CRTDBG_REPORT_FLAG) | _CRTDBG_ALLOC_MEM_DF | _CRTDBG_LEAK_CHECK_DF);

    // Reports, a contract abort among them, go to stderr so an unattended replay never stops at a
    // dialog.
    for (const int reportType : { _CRT_WARN, _CRT_ERROR, _CRT_ASSERT })
    {
        _CrtSetReportMode(reportType, _CRTDBG_MODE_FILE);
        _CrtSetReportFile(reportType, _CRTDBG_FILE_STDERR);
    }

    std::vector<std::filesystem::path> paths;
    for (int i = 1; i < argc; ++i)
    {
        const std::filesystem::path argument(argv[i]);
        if (std::filesystem::is_directory(argument))
        {
            for (const std::filesystem::directory_entry& entry : std::filesystem::directory_iterator(argument))
            {
                if (entry.is_regular_file())
                {
                    paths.push_back(entry.path());
                }
            }
        }
        else
        {
            paths.push_back(argument);
        }
    }

    if (argc > 1)
    {
        std::vector<uint8_t> input;
        for (const std::filesystem::path& path : paths)
        {
            if (!ReadInput(path, input))
            {
                std::fwprintf(stderr, L"Cannot read input %ls\n", path.c_str());
                return 2;
            }
            if (!RunWithoutLeak(input))
            {
                std::fwprintf(stderr, L"Input %ls leaked heap blocks\n", path.c_str());
                return 1;
            }
        }
        std::printf("Replayed %zu inputs with no leak\n", paths.size());
        return 0;
    }

    const std::vector<uint8_t> seed = {
        // Two monitors, narrow coordinates.
        2,
        // Left 0, top 0, right 128, bottom 64, 96 DPI.
        0,
        0,
        16,
        8,
        1,
        // Left 128, top 0, right 256, bottom 64, 144 DPI.
        16,
        0,
        32,
        8,
        3,
        // No per-edge settings.
        0,
        // Side-by-side filter, 25 px.
        1,
        25,
        // From (127, 32) to (129, 32): held.
        0x18,
        16,
        7,
        4,
        8,
        16,
        9,
        4,
        8,
        // Three more pushes of 3, still held.
        0x00,
        3,
        0,
        0x00,
        3,
        0,
        0x00,
        3,
        0,
        // A push of 10, the per-move cap, still held at 21.
        0x00,
        10,
        0,
        // Another 10 reaches 31 and crosses to (137, 32).
        0x00,
        10,
        0,
        // A fling 128 px back left lands on A at (9, 32) and takes the free return.
        0x00,
        0x80,
        0,
        // A clip to A, then a push from (127, 32) to (129, 32). The clip lands it on A, so it is
        // allowed, never held.
        0x58,
        0x03,
        0,
        16,
        7,
        4,
        8,
        16,
        9,
        4,
        8,
        // A clip 0, 16, 256, 48 across the edge, then a diagonal from (127, 40) to (135, 60). It
        // lands at (135, 47) on B and holds at (127, 47), inside the clip.
        0x58,
        0x01,
        0,
        2,
        32,
        6,
        16,
        7,
        5,
        8,
        17,
        7,
        7,
        12,
        // A clip to the bounding rect 0, 0, 256, 64 whose header also puts a full-screen wall on A
        // (bit 2, value 0), then a push from (127, 32) to (129, 32): held.
        0x58,
        0x05,
        0,
        0,
        32,
        8,
        16,
        7,
        4,
        8,
        16,
        9,
        4,
        8,
        // Two pushes of 10 against the wall, still held.
        0x00,
        10,
        0,
        0x00,
        10,
        0,
        // The cross key crosses to (137, 32).
        0x02,
        10,
        0,
        // Straight back 20 px meets B's brake into A, as the keyed crossing opened no free
        // return: held at (128, 32).
        0x00,
        0xEC,
        0,
        // The same clip, with the wall cleared (value 2, the monitor count), then no movement.
        0x40,
        0x15,
        0,
        0,
        32,
        8,
        0,
        0,
    };
    return RunWithoutLeak(seed) ? 0 : 1;
}

#endif
