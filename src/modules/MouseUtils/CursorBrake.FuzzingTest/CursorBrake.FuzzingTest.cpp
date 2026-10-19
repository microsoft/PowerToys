// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

// libFuzzer target for the CursorBrake engine.
//
// CursorBrake is a user-input module, so PowerToys policy requires a fuzz target (AGENTS.md: "New
// modules handling file I/O or user input must implement fuzzing tests"). The decision logic
// lives in the Win32-free cursorbrake::Engine (CursorBrakeCore.h), so it runs here under ASan with
// no OS state. The fuzzer's bytes become monitor layouts, settings snapshots, cursor clips and a
// sequence of moves, including degenerate and overlapping rects, empty and inverted clips, extreme
// coordinates and DPI values, and settings a hand-edited settings.json could carry, out-of-range
// enum values among them.
//
// Every decision is checked against the engine's contract. Allow returns the proposed point
// unchanged. Hold happens only for a move that lands on another monitor once the clip clamps it,
// and returns a point inside the monitor the cursor started on, and inside the clip when the
// cursor started inside it. A bypassed move is never held. A broken contract aborts, which
// libFuzzer reports as a crash.

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
        return layout;
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

    // A clip from a header byte: bit 0 clear means the hook could not read one. Bit 1 clips to one
    // of the layout's monitors, the way a full-screen game confines the cursor; otherwise bit 7
    // picks wide coordinates for a free rect, which can be empty or inverted, as a hostile caller
    // could pass.
    std::optional<RectL> ReadClip(Reader& r, const Layout& layout)
    {
        const uint8_t header = r.u8();
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
    const Monitor* MonitorAt(const Layout& layout, PointL point)
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

    // `clip` is the clip in effect: the last usable one a move carried, or none.
    void CheckContract(const MoveEvent& move, const std::optional<RectL>& clip, const Layout& layout, const Settings& settings, const Decision& decision)
    {
        if (decision.kind == DecisionKind::Allow)
        {
            if (!(decision.point == move.proposed))
            {
                std::abort();
            }
            return;
        }

        const bool bypassed = layout.monitors.size() < 2 || move.input == InputKind::Absolute || move.crossModifierHeld || (move.buttonDown && settings.disableWhileDragging);
        const Monitor* source = MonitorAt(layout, Landing(move.current, clip));
        const Monitor* destination = MonitorAt(layout, Landing(move.proposed, clip));
        if (bypassed || source == nullptr || destination == nullptr || destination == source || !Contains(source->rect, decision.point))
        {
            std::abort();
        }
        // A hold from inside the clip stays inside it, so Windows leaves the cursor where it is put.
        if (clip && Contains(*clip, move.current) && !Contains(*clip, decision.point))
        {
            std::abort();
        }
    }
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
    PointL cursor{};

    // Bound the number of moves so any single input terminates regardless of size.
    for (int step = 0; step < 4096 && !r.done(); ++step)
    {
        const uint8_t op = r.u8();

        // Occasionally swap the layout or settings mid-sequence, the way a display change or a
        // settings save reaches the hook between two moves.
        if ((op & 0xC0u) == 0xC0u)
        {
            layout = ReadLayout(r);
        }
        else if ((op & 0xC0u) == 0x80u)
        {
            settings = ReadSettings(r);
        }
        bool readFailed = false;
        if ((op & 0xC0u) == 0x40u)
        {
            // A clip set or lifted between moves, the way a game confines and frees the cursor, or
            // a read that fails for this move.
            const std::optional<RectL> read = ReadClip(r, layout);
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
// to the left monitor, and a diagonal held inside a clip across the edge. Every input runs under
// the debug heap's leak check.
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
    };
    return RunWithoutLeak(seed) ? 0 : 1;
}

#endif
