// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#pragma once

#include <algorithm>
#include <cstdint>
#include <optional>
#include <vector>

// The CursorBrake decision engine, decoupled from Win32 so it can be unit tested. The caller (the
// module's low-level mouse hook) describes each move, the monitor layout and a settings snapshot,
// and acts on the returned decision. The engine never enumerates monitors or reads input state.
//
// Coordinates are physical pixels and rects are half-open like a Win32 RECT, so right and bottom
// are the first coordinates outside. The push distance is in logical pixels and scales by the DPI
// of the monitor the cursor is leaving.
namespace cursorbrake
{
    // The DPI of 100% scaling.
    constexpr unsigned int BASE_DPI = 96;

    // Accepted push distance in logical pixels. Zero removes the resistance without disabling the
    // module.
    constexpr int MIN_PUSH_DISTANCE = 0;
    constexpr int MAX_PUSH_DISTANCE = 1000;
    constexpr int DEFAULT_PUSH_DISTANCE = 300;

    struct PointL
    {
        long x = 0;
        long y = 0;

        bool operator==(const PointL&) const = default;
    };

    struct RectL
    {
        long left = 0;
        long top = 0;
        long right = 0;
        long bottom = 0;

        bool operator==(const RectL&) const = default;
    };

    struct Monitor
    {
        RectL rect;
        unsigned int dpi = BASE_DPI;

        bool operator==(const Monitor&) const = default;
    };

    enum class Side
    {
        Left,
        Top,
        Right,
        Bottom,
    };

    constexpr bool IsSideBySide(Side side)
    {
        return side == Side::Left || side == Side::Right;
    }

    // Whether `other` lies directly across the given side of `rect`: its opposite side is on the
    // same line, and the two spans along that line overlap.
    constexpr bool Borders(const RectL& rect, const RectL& other, Side side)
    {
        const bool overlapX = other.left < rect.right && rect.left < other.right;
        const bool overlapY = other.top < rect.bottom && rect.top < other.bottom;
        switch (side)
        {
        case Side::Left:
            return other.right == rect.left && overlapY;
        case Side::Top:
            return other.bottom == rect.top && overlapX;
        case Side::Right:
            return other.left == rect.right && overlapY;
        case Side::Bottom:
        default:
            return other.top == rect.bottom && overlapX;
        }
    }

    // How an edge treats a crossing in one direction.
    enum class EdgeMode
    {
        // Brakes with the setting's own push distance.
        Custom,
        // Lets the cursor through, like an edge the Apply to filter leaves out.
        Off,
        // Holds the cursor until a bypass, such as the cross key, lets it through.
        Wall,
    };

    constexpr int EDGE_MODE_COUNT = 3;

    // An edge the user set explicitly, between two monitors of the layout it belongs to. Without
    // oneWay it covers both directions, with it only the crossing from `first` to `second`.
    struct EdgeSetting
    {
        size_t first = 0;
        size_t second = 0;
        bool oneWay = false;
        EdgeMode mode = EdgeMode::Custom;
        int pushDistance = DEFAULT_PUSH_DISTANCE;

        bool operator==(const EdgeSetting&) const = default;
    };

    struct Layout
    {
        std::vector<Monitor> monitors;
        // Indices into `monitors`, so they only mean something against this layout.
        std::vector<EdgeSetting> edges;

        bool operator==(const Layout&) const = default;
    };

    enum class InputKind
    {
        Relative,
        Absolute,
    };

    struct MoveEvent
    {
        PointL current;
        PointL proposed;
        InputKind input = InputKind::Relative;
        // Whether the modifier chosen in Settings::crossModifier is down; the caller reads the key.
        bool crossModifierHeld = false;
        bool buttonDown = false;
        // The rect the cursor is confined to (GetClipCursor), when the caller knows it. Windows
        // clamps the cursor into it after the hook, so the hook's proposed point can lie outside.
        std::optional<RectL> clip;
        // The monitor a full-screen app fills while full-screen apps get a wall, by index into the
        // layout. Every way out of it is a wall. Unlike the clip, no value means no such monitor.
        std::optional<size_t> wallMonitor;
    };

    enum class EdgeFilter
    {
        All,
        SideBySide,
        Stacked,
    };

    constexpr int EDGE_FILTER_COUNT = 3;

    enum class CrossModifier
    {
        Ctrl,
        Shift,
    };

    constexpr int CROSS_MODIFIER_COUNT = 2;

    // The value of a choice setting stored as a number, when the number is exactly one of the
    // `count` values the choice has, 0 to count - 1. The comparisons reject NaN, fractions and
    // every number outside the range, so a hand-edited file cannot name a value the enum lacks.
    template<typename Choice>
    constexpr std::optional<Choice> ChoiceOf(double raw, int count)
    {
        for (int choice = 0; choice < count; ++choice)
        {
            if (raw == choice)
            {
                return static_cast<Choice>(choice);
            }
        }
        return std::nullopt;
    }

    // The push threshold for a monitor at `dpi`, in physical pixels times BASE_DPI: `distance`
    // logical pixels on the monitor the cursor is leaving. Windows moves the pointer about
    // dpi / 96 physical pixels per mouse count, so scaling by the source keeps the hand travel the
    // same in both directions over an edge. Keeping the factor of BASE_DPI keeps it exact: at 150%
    // the 37.5 px threshold needs a push of 38.
    constexpr uint64_t ScaledThreshold(int distance, unsigned int dpi)
    {
        const uint64_t logical = distance > 0 ? static_cast<uint64_t>(distance) : 0;
        return logical * dpi;
    }

    // The most one move adds to the push, in logical pixels. A fling or a shake then adds one cap
    // per event instead of crossing in one or two, while a push slower than this is unaffected.
    constexpr int MOVE_CAP = 10;

    // How far from the crossed edge, in logical pixels, the cursor may wander and still return free.
    constexpr int FREE_RETURN_LIMIT = 100;

    // A settings file can carry any number, so a finite value is clamped into range before the
    // cast, which keeps the cast defined. The caller rejects non-finite values.
    constexpr int ClampPushDistance(double raw)
    {
        if (raw < MIN_PUSH_DISTANCE)
        {
            return MIN_PUSH_DISTANCE;
        }
        if (raw > MAX_PUSH_DISTANCE)
        {
            return MAX_PUSH_DISTANCE;
        }
        return static_cast<int>(raw);
    }

    struct Settings
    {
        int pushDistance = DEFAULT_PUSH_DISTANCE;
        bool freeReturn = true;
        EdgeFilter edgeFilter = EdgeFilter::All;
        bool disableWhileDragging = false;
        CrossModifier crossModifier = CrossModifier::Ctrl;

        bool operator==(const Settings&) const = default;
    };

    // What a crossing in one direction meets: the mode, and for Custom the push distance in
    // logical pixels.
    struct EdgeRule
    {
        EdgeMode mode = EdgeMode::Custom;
        int pushDistance = 0;

        bool operator==(const EdgeRule&) const = default;
    };

    // The first monitor containing the point. Real layouts never overlap, so the order only
    // matters for malformed ones.
    inline std::optional<size_t> MonitorAt(const Layout& layout, PointL point)
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
    // The rule for a crossing from `source` into `neighbor` over a side on the given axis. It
    // checks, in order: a full-screen wall on the source (`wallMonitor`), a one-way entry from
    // source to neighbor, a both-ways entry naming the two in either order, and the default. The
    // default is the global push distance when the Apply to filter brakes the axis, and Off when
    // it does not. Without a neighbor only the full-screen wall and the default apply. The first
    // match wins.
    inline EdgeRule RuleFor(const Layout& layout, const Settings& settings, size_t source, std::optional<size_t> neighbor, bool sideBySide, std::optional<size_t> wallMonitor = std::nullopt)
    {
        if (wallMonitor && *wallMonitor == source)
        {
            return EdgeRule{ EdgeMode::Wall, 0 };
        }
        if (neighbor)
        {
            for (const EdgeSetting& edge : layout.edges)
            {
                if (edge.oneWay && edge.first == source && edge.second == *neighbor)
                {
                    return EdgeRule{ edge.mode, edge.pushDistance };
                }
            }
            for (const EdgeSetting& edge : layout.edges)
            {
                const bool forward = edge.first == source && edge.second == *neighbor;
                const bool backward = edge.first == *neighbor && edge.second == source;
                if (!edge.oneWay && (forward || backward))
                {
                    return EdgeRule{ edge.mode, edge.pushDistance };
                }
            }
        }

        // Any filter value other than the two single-axis ones, including one a hand-edited
        // settings file carries, brakes both axes like EdgeFilter::All.
        const bool braked = sideBySide ? settings.edgeFilter != EdgeFilter::Stacked : settings.edgeFilter != EdgeFilter::SideBySide;
        return braked ? EdgeRule{ EdgeMode::Custom, settings.pushDistance } : EdgeRule{ EdgeMode::Off, 0 };
    }

    // The stricter of the rules of two sides a move passes at once, through a corner: a wall
    // first, then the larger push distance, then Off. The default is a Custom rule at the global
    // distance, so the distance alone ranks a Custom entry against it, and a corner never asks
    // less of the push than either side does.
    constexpr EdgeRule Stricter(const EdgeRule& a, const EdgeRule& b)
    {
        const auto rank = [](const EdgeRule& rule) { return rule.mode == EdgeMode::Wall ? 2 : (rule.mode == EdgeMode::Off ? 0 : 1); };
        if (rank(a) != rank(b))
        {
            return rank(a) > rank(b) ? a : b;
        }
        return rank(a) == 1 && b.pushDistance > a.pushDistance ? b : a;
    }

    // The monitor across one side of `source` at `along`, the coordinate along that side: the first
    // that borders that side and whose span holds `along`.
    inline std::optional<size_t> NeighborAcross(const Layout& layout, size_t source, Side side, long along)
    {
        const RectL& rect = layout.monitors[source].rect;
        for (size_t i = 0; i < layout.monitors.size(); ++i)
        {
            const RectL& other = layout.monitors[i].rect;
            if (i == source)
            {
                continue;
            }
            const bool spans = IsSideBySide(side) ? along >= other.top && along < other.bottom : along >= other.left && along < other.right;
            if (Borders(rect, other, side) && spans)
            {
                return i;
            }
        }
        return std::nullopt;
    }

    // Whether `source` meets a wall on the way out of it: a full-screen wall on it, which covers
    // every way out whatever it borders, or a one-way or both-ways entry toward a monitor sharing
    // an edge with it.
    inline bool HasWallOut(const Layout& layout, const Settings& settings, size_t source, std::optional<size_t> wallMonitor = std::nullopt)
    {
        if (wallMonitor && *wallMonitor == source)
        {
            return true;
        }
        const RectL& rect = layout.monitors[source].rect;
        for (size_t i = 0; i < layout.monitors.size(); ++i)
        {
            const RectL& other = layout.monitors[i].rect;
            if (i == source)
            {
                continue;
            }
            for (const Side side : { Side::Left, Side::Top, Side::Right, Side::Bottom })
            {
                if (Borders(rect, other, side) && RuleFor(layout, settings, source, i, IsSideBySide(side)).mode == EdgeMode::Wall)
                {
                    return true;
                }
            }
        }
        return false;
    }

    enum class DecisionKind
    {
        Allow,
        Hold,
    };

    // Pen and touch input promoted to mouse messages carries this signature in its extra info
    // ("System Events and Mouse Messages" in the Win32 documentation); touch also sets 0x80. The
    // mask clears the upper half, as the documented check on a 64-bit LPARAM does.
    constexpr uint64_t PEN_OR_TOUCH_SIGNATURE = 0xFF515700;
    constexpr uint64_t PEN_OR_TOUCH_SIGNATURE_MASK = 0xFFFFFF00;

    // LLMHF_INJECTED in MSLLHOOKSTRUCT::flags: the move came from SendInput or a similar call.
    constexpr uint32_t INJECTED_FLAG = 0x00000001;

    // LLMHF_LOWER_IL_INJECTED: the injecting process ran at a lower integrity level, such as a
    // sandboxed renderer. Windows sets it alongside INJECTED_FLAG.
    constexpr uint32_t LOWER_IL_INJECTED_FLAG = 0x00000002;

    // Extra info that makes an injected move count as hardware, so scripted tests and UI tests can
    // drive the brake with SendInput. ASCII "CBRK". It stays clear of the pen and touch signature,
    // MouseButtonLock's 0x57494E4D, the keyboard hook flags 0x1 and 0x110, and GrabAndMove's
    // 0x474D in the top 16 bits.
    constexpr uint64_t TEST_MOVE_MARKER = 0x4342524B;
    static_assert((TEST_MOVE_MARKER & PEN_OR_TOUCH_SIGNATURE_MASK) != PEN_OR_TOUCH_SIGNATURE);

    enum class InputTreatment
    {
        // The move goes through. The engine sees it as absolute input, which it never holds, so
        // the push and the free return reset as for any other bypass.
        Pass,
        // The engine decides.
        Judge,
    };

    // How a hook event reaches the engine. A low-level hook cannot see whether a move is absolute,
    // so its source stands in, checked in order:
    //  1. Pen or touch: absolute by nature.
    //  2. A remote session: the client draws the pointer and sends absolute positions, so a hold on
    //     this side would be overwritten by the next event rather than resist.
    //  3. An injected move: remote-control tools and teleports such as Mouse Jump, which a hold
    //     would fight. Only the test marker brings one back to the engine, and only when Windows
    //     does not flag the move as injected at a lower integrity level, so a sandboxed process
    //     cannot make the brake hold.
    //  4. Anything else counts as a hardware mouse, absolute hardware devices included, because the
    //     hook cannot tell them apart.
    constexpr InputTreatment ClassifyInput(uint32_t flags, uint64_t extraInfo, bool remoteSession)
    {
        if ((extraInfo & PEN_OR_TOUCH_SIGNATURE_MASK) == PEN_OR_TOUCH_SIGNATURE)
        {
            return InputTreatment::Pass;
        }
        if (remoteSession)
        {
            return InputTreatment::Pass;
        }
        if ((flags & INJECTED_FLAG) != 0)
        {
            const bool lowerIntegrity = (flags & LOWER_IL_INJECTED_FLAG) != 0;
            return extraInfo == TEST_MOVE_MARKER && !lowerIntegrity ? InputTreatment::Judge : InputTreatment::Pass;
        }
        return InputTreatment::Judge;
    }

    // A clip Windows can apply: GetClipCursor never reports an empty one, so an empty or inverted
    // rect from a caller counts as no clip.
    constexpr bool IsUsableClip(const RectL& clip)
    {
        return clip.right > clip.left && clip.bottom > clip.top;
    }

    // Where Windows puts a point under the clip: clamped into it, or unchanged without one.
    constexpr PointL ClampToClip(PointL point, const std::optional<RectL>& clip)
    {
        if (!clip || !IsUsableClip(*clip))
        {
            return point;
        }
        return PointL{ std::clamp(point.x, clip->left, clip->right - 1), std::clamp(point.y, clip->top, clip->bottom - 1) };
    }

    // Allow carries the proposed point unchanged. Hold carries the point to place the cursor at,
    // on the source monitor's last pixel before the crossed edge.
    struct Decision
    {
        DecisionKind kind = DecisionKind::Allow;
        PointL point;

        bool operator==(const Decision&) const = default;
    };

    // Holds the cursor at an inner monitor edge until the push past that edge adds up to the push
    // distance. Only the hook thread calls OnMove, so the engine takes no lock.
    class Engine
    {
    public:
        Decision OnMove(const MoveEvent& move, const Layout& layout, const Settings& settings)
        {
            // Monitor indices, thresholds and the braked axes can all differ after a change, so
            // nothing accumulated under the old values carries over. A full-screen wall appearing,
            // leaving or moving to another monitor resets the same way, in either direction.
            if (!(settings == m_settings) || !(layout == m_layout) || move.wallMonitor != m_wallMonitor)
            {
                m_settings = settings;
                m_layout = layout;
                m_wallMonitor = move.wallMonitor;
                m_push.reset();
                m_freeReturn.reset();
            }

            const Decision allow{ DecisionKind::Allow, move.proposed };

            // A move without a clip leaves the last one in effect: the caller could not read it.
            if (move.clip)
            {
                m_clip = IsUsableClip(*move.clip) ? move.clip : std::nullopt;
            }

            // A bypassed move goes wherever it likes without the engine judging it, so neither the
            // push nor the free return survives it. A drag is no bypass on the full-screen wall's
            // monitor, since a game holds a button down most of the time; only the cross key
            // crosses that wall.
            const bool onWallMonitor = m_wallMonitor && MonitorAt(layout, ClampToClip(move.current, m_clip)) == m_wallMonitor;
            const bool dragBypass = move.buttonDown && settings.disableWhileDragging && !onWallMonitor;
            if (layout.monitors.size() < 2 || move.input == InputKind::Absolute || move.crossModifierHeld || dragBypass)
            {
                m_push.reset();
                m_freeReturn.reset();
                return allow;
            }

            // Windows clamps the cursor into its clip after the hook, whatever the hook decides, so
            // the move is judged from and to where the cursor will be. A game's clip on its own
            // monitor then never holds. An Allow still carries the point the hook saw.
            MoveEvent judged = move;
            judged.current = ClampToClip(move.current, m_clip);
            judged.proposed = ClampToClip(move.proposed, m_clip);

            const std::optional<size_t> source = MonitorAt(layout, judged.current);
            if (!source)
            {
                m_push.reset();
                return allow;
            }
            const Monitor& from = layout.monitors[*source];

            ExpireFreeReturn(layout, *source, judged.current);

            if (m_push && m_push->monitor != *source)
            {
                m_push.reset();
            }

            // The push and the free return start over when the clip starts or stops letting the
            // cursor through where they lead, as when a game confines the cursor or lets it go. A
            // clip that changes without changing that, such as one that follows the cursor or two
            // apps setting slightly different rects, keeps them, so the push still crosses.
            if (m_push && Reaches(m_clip, from.rect, m_push->side, judged.current, true) != m_push->open)
            {
                m_push.reset();
            }
            if (m_freeReturn && Reaches(m_clip, layout.monitors[m_freeReturn->to].rect, m_freeReturn->side, judged.current, false) != m_freeReturn->open)
            {
                m_freeReturn.reset();
            }

            // A move back from the edge costs only the distance it moves away, so the small
            // rebound at the end of a hard stroke keeps most of the push, while backing off
            // deliberately drains it to zero.
            uint64_t away = 0;
            std::optional<Side> awaySide;
            if (m_push)
            {
                away = AwayDistance(judged, m_push->side);
                awaySide = m_push->side;
                const uint64_t drain = away * BASE_DPI;
                m_push->amount = drain < m_push->amount ? m_push->amount - drain : 0;
            }

            // Where the previous move was held, if it was, and how far that move went away from
            // the side then pushed. Only the move right after a hold can carry the push to a new
            // side.
            std::optional<PointL> previousHold;
            uint64_t previousAway = 0;
            std::optional<Side> previousAwaySide;
            if (m_push)
            {
                previousHold = m_push->held;
                previousAway = m_push->awayOnHold;
                previousAwaySide = m_push->awaySide;
                m_push->held.reset();
            }

            // Landing outside every monitor is a gap, or past an outer edge when no clip is known.
            // Without a game's clip, Windows reports the virtual screen's bounding rect as the
            // clip, so a move past an outer edge is judged where that rect clamps it: back on the
            // source monitor, or on a neighbor past an inner corner. A push toward a neighbor does
            // not carry across a gap. Windows puts a cursor in a gap on a monitor of its own
            // choosing, not always the nearest and sometimes a neighbor past a wall, so a source
            // with a wall out keeps the cursor on its own last pixel. Any other source leaves the
            // move to Windows.
            const std::optional<size_t> destination = MonitorAt(layout, judged.proposed);
            if (!destination)
            {
                m_push.reset();
                if (HasWallOut(layout, settings, *source, m_wallMonitor))
                {
                    return Decision{ DecisionKind::Hold, ClampInto(judged.proposed, from.rect) };
                }
                return allow;
            }
            if (*destination == *source)
            {
                return allow;
            }

            const std::optional<Side> pushed = m_push ? std::optional<Side>{ m_push->side } : std::nullopt;
            const std::optional<Crossing> crossing = CrossedSide(layout, settings, m_wallMonitor, *source, *destination, judged.current, judged.proposed, pushed);
            if (!crossing)
            {
                return allow;
            }
            const Side side = crossing->side;
            const EdgeRule& rule = crossing->rule;

            // The free return is checked against the way back as it stands when it is used, so a
            // wall in that direction holds the return like any other move, through a corner too.
            if (m_freeReturn && m_freeReturn->from == *source && m_freeReturn->to == *destination)
            {
                const bool walled = rule.mode == EdgeMode::Wall;
                m_freeReturn.reset();
                if (!walled)
                {
                    m_push.reset();
                    return allow;
                }
            }

            // Any neighbor past the pushed side continues the push, so a hard push into a corner
            // that lands on two neighbors in turn keeps adding up. A new side starts over, except
            // right after a hold near the corner pixel joining the two sides. A slow diagonal into a
            // junction reports one axis per event and lands past each side in turn, and each hold
            // keeps its along-edge motion, so the cursor wobbles off the exact corner. There the
            // push carries over, less the distance from the corner. When the new side is the one
            // the previous move's away drain was measured against, that drain already paid for the
            // part of the distance it covers, so it is not charged again and each pixel of wobble
            // costs 1 px once. A drain against any other side moved the cursor along the new side,
            // not away from it, so it earns no credit.
            if (!m_push || m_push->side != side)
            {
                uint64_t carried = 0;
                const std::optional<PointL> corner = m_push ? CornerPixel(from.rect, m_push->side, side) : std::nullopt;
                if (corner && previousHold)
                {
                    const uint64_t fromCursor = Distance(judged.current, *corner);
                    const uint64_t fromHold = Distance(*previousHold, *corner);
                    const uint64_t farther = fromCursor > fromHold ? fromCursor : fromHold;
                    const uint64_t credit = previousAwaySide == side ? previousAway : 0;
                    const uint64_t drift = (farther > credit ? farther - credit : 0) * BASE_DPI;
                    carried = drift < m_push->amount ? m_push->amount - drift : 0;
                }
                m_push = Push{ *source, side, carried };
                m_push->open = Reaches(m_clip, from.rect, side, judged.proposed, true);
            }

            // A wall holds without adding to the push, so leaning on it banks nothing for a braked
            // neighbor on the same side.
            if (rule.mode != EdgeMode::Wall)
            {
                // The cap scales like the threshold, so crossing takes at least pushDistance /
                // MOVE_CAP moves on every edge, whatever the DPI or direction.
                const uint64_t cap = ScaledThreshold(MOVE_CAP, from.dpi);
                const uint64_t overshoot = static_cast<uint64_t>(Overshoot(from.rect, judged.proposed, side)) * BASE_DPI;
                m_push->amount += overshoot < cap ? overshoot : cap;

                if (m_push->amount >= ScaledThreshold(rule.pushDistance, from.dpi))
                {
                    m_push.reset();
                    if (settings.freeReturn)
                    {
                        m_freeReturn = FreeReturn{ *destination, *source, side };
                        m_freeReturn->open = Reaches(m_clip, from.rect, side, judged.proposed, false);
                    }
                    return allow;
                }
            }

            m_push->held = ClampInto(judged.proposed, from.rect);
            m_push->awayOnHold = away;
            m_push->awaySide = awaySide;
            return Decision{ DecisionKind::Hold, *m_push->held };
        }

        // The clip in effect: the last usable one a move carried, or none.
        const std::optional<RectL>& Clip() const
        {
            return m_clip;
        }

    private:
        // A push belongs to one side of the source monitor, whichever neighbor lies across it.
        struct Push
        {
            size_t monitor = 0;
            Side side = Side::Left;
            // Physical pixels past the edge times BASE_DPI, each move capped, summed over the held
            // moves, less the distance moved away from the edge since.
            uint64_t amount = 0;
            // Where the engine held the last move, when that move was held.
            std::optional<PointL> held;
            // How far, in physical pixels, that held move went away from the side pushed before
            // it, which its own drain already charged, and which side that was.
            uint64_t awayOnHold = 0;
            std::optional<Side> awaySide;
            // Whether the clip let the cursor past the side where the push builds.
            bool open = true;
        };

        // Lets the cursor go back from `from` to `to` without a push, right after it crossed the
        // given side of `to`.
        struct FreeReturn
        {
            size_t from = 0;
            size_t to = 0;
            Side side = Side::Left;
            // Whether the clip let the cursor back onto `to` across the side.
            bool open = true;
        };


        // How far past the given side of the rect the point lies, counted from the rect's last
        // pixel on that side: 1 for the first pixel outside, zero or less for a point inside.
        // Computed in 64 bits because the subtraction can span the whole 32-bit range.
        static int64_t Overshoot(const RectL& rect, PointL point, Side side)
        {
            switch (side)
            {
            case Side::Left:
                return static_cast<int64_t>(rect.left) - point.x;
            case Side::Top:
                return static_cast<int64_t>(rect.top) - point.y;
            case Side::Right:
                return static_cast<int64_t>(point.x) - (static_cast<int64_t>(rect.right) - 1);
            case Side::Bottom:
            default:
                return static_cast<int64_t>(point.y) - (static_cast<int64_t>(rect.bottom) - 1);
            }
        }

        // The rect's last pixel on both sides, where two adjacent sides meet; none for two sides
        // on the same axis.
        static constexpr std::optional<PointL> CornerPixel(const RectL& rect, Side first, Side second)
        {
            const bool firstSideBySide = first == Side::Left || first == Side::Right;
            const bool secondSideBySide = second == Side::Left || second == Side::Right;
            if (firstSideBySide == secondSideBySide)
            {
                return std::nullopt;
            }
            const Side sideBySide = firstSideBySide ? first : second;
            const Side stacked = firstSideBySide ? second : first;
            return PointL{ sideBySide == Side::Left ? rect.left : rect.right - 1, stacked == Side::Top ? rect.top : rect.bottom - 1 };
        }

        // The distance between two points along the axes, in physical pixels.
        static constexpr uint64_t Distance(PointL a, PointL b)
        {
            const int64_t dx = static_cast<int64_t>(a.x) - b.x;
            const int64_t dy = static_cast<int64_t>(a.y) - b.y;
            return static_cast<uint64_t>(dx < 0 ? -dx : dx) + static_cast<uint64_t>(dy < 0 ? -dy : dy);
        }

        // How many physical pixels the move travels away from the given side, zero for a move
        // toward it or along it.
        static uint64_t AwayDistance(const MoveEvent& move, Side side)
        {
            int64_t away = 0;
            switch (side)
            {
            case Side::Left:
                away = static_cast<int64_t>(move.proposed.x) - move.current.x;
                break;
            case Side::Top:
                away = static_cast<int64_t>(move.proposed.y) - move.current.y;
                break;
            case Side::Right:
                away = static_cast<int64_t>(move.current.x) - move.proposed.x;
                break;
            case Side::Bottom:
            default:
                away = static_cast<int64_t>(move.current.y) - move.proposed.y;
                break;
            }
            return away > 0 ? static_cast<uint64_t>(away) : 0;
        }

        // The rule for leaving `source` over `side` on a move to `to`. The neighbor is the
        // destination when it borders that side. Otherwise it is the monitor across the side at
        // the point's coordinate along it, clamped into the source's span, which for a move
        // through a corner is the corner pixel.
        static EdgeRule RuleAt(const Layout& layout, const Settings& settings, std::optional<size_t> wallMonitor, size_t source, size_t destination, Side side, PointL to)
        {
            const RectL& rect = layout.monitors[source].rect;
            std::optional<size_t> neighbor;
            if (Borders(rect, layout.monitors[destination].rect, side))
            {
                neighbor = destination;
            }
            else
            {
                const long along = IsSideBySide(side) ? std::clamp(to.y, rect.top, rect.bottom - 1) : std::clamp(to.x, rect.left, rect.right - 1);
                neighbor = NeighborAcross(layout, source, side, along);
            }
            return RuleFor(layout, settings, source, neighbor, IsSideBySide(side), wallMonitor);
        }

        // A side a move leaves the source through, with the rule it meets there.
        struct Crossing
        {
            Side side = Side::Left;
            EdgeRule rule;
        };

        // The side of the source monitor a move into `destination` leaves through, unless its
        // rule is Off. A move that ends past two sides crossed the one that borders the
        // destination. When neither borders it and one of them is the side already being pushed
        // (`pushed`), the push keeps that side, so a diagonal into a corner neighbor cannot flip
        // sides and start over with every wobble of the hand. Otherwise, when both or neither
        // border it, the move crossed the side it reached first, which is the side whose
        // overshoot is the larger share of the move's travel along that axis. A move exactly
        // through the corner offers both sides; the harder-pushed one wins, the side-by-side one
        // on a tie.
        //
        // A destination that borders neither side meets the source only at the corner, so the
        // move passes both sides there and meets the stricter of their rules (Stricter). The side
        // is chosen as above and carries the push; only Off on both sides lets the move through.
        // A wall on either side of the corner then holds, so a monitor walled on every side it
        // shares cannot be left through a corner.
        static std::optional<Crossing> CrossedSide(const Layout& layout, const Settings& settings, std::optional<size_t> wallMonitor, size_t source, size_t destination, PointL from, PointL to, std::optional<Side> pushed)
        {
            const RectL& rect = layout.monitors[source].rect;
            const RectL& target = layout.monitors[destination].rect;

            std::optional<Side> sideX;
            if (Overshoot(rect, to, Side::Right) > 0)
            {
                sideX = Side::Right;
            }
            else if (Overshoot(rect, to, Side::Left) > 0)
            {
                sideX = Side::Left;
            }

            std::optional<Side> sideY;
            if (Overshoot(rect, to, Side::Bottom) > 0)
            {
                sideY = Side::Bottom;
            }
            else if (Overshoot(rect, to, Side::Top) > 0)
            {
                sideY = Side::Top;
            }

            bool offerX = sideX.has_value();
            bool offerY = sideY.has_value();
            bool corner = false;
            if (offerX && offerY)
            {
                const bool bordersX = Borders(rect, target, *sideX);
                const bool bordersY = Borders(rect, target, *sideY);
                corner = !bordersX && !bordersY;
                if (bordersX != bordersY)
                {
                    offerX = bordersX;
                    offerY = bordersY;
                }
                else if (!bordersX && pushed && (*pushed == *sideX || *pushed == *sideY))
                {
                    offerX = *pushed == *sideX;
                    offerY = *pushed == *sideY;
                }
                else
                {
                    // Compares overX / travelX with overY / travelY by cross-multiplying. Double
                    // keeps the products of two 33-bit spans from overflowing; real moves are exact.
                    const double overX = static_cast<double>(Overshoot(rect, to, *sideX));
                    const double overY = static_cast<double>(Overshoot(rect, to, *sideY));
                    const double travelX = overX - static_cast<double>(Overshoot(rect, from, *sideX));
                    const double travelY = overY - static_cast<double>(Overshoot(rect, from, *sideY));
                    offerX = overX * travelY >= overY * travelX;
                    offerY = overY * travelX >= overX * travelY;
                }
            }

            if (corner)
            {
                const EdgeRule rule = Stricter(RuleAt(layout, settings, wallMonitor, source, destination, *sideX, to), RuleAt(layout, settings, wallMonitor, source, destination, *sideY, to));
                if (rule.mode == EdgeMode::Off)
                {
                    return std::nullopt;
                }
                const bool takeY = offerY && (!offerX || Overshoot(rect, to, *sideY) > Overshoot(rect, to, *sideX));
                return Crossing{ takeY ? *sideY : *sideX, rule };
            }

            std::optional<Crossing> crossX;
            if (offerX)
            {
                crossX = Crossing{ *sideX, RuleAt(layout, settings, wallMonitor, source, destination, *sideX, to) };
                if (crossX->rule.mode == EdgeMode::Off)
                {
                    crossX.reset();
                }
            }
            std::optional<Crossing> crossY;
            if (offerY)
            {
                crossY = Crossing{ *sideY, RuleAt(layout, settings, wallMonitor, source, destination, *sideY, to) };
                if (crossY->rule.mode == EdgeMode::Off)
                {
                    crossY.reset();
                }
            }

            if (crossX && crossY)
            {
                return Overshoot(rect, to, *sideY) > Overshoot(rect, to, *sideX) ? crossY : crossX;
            }
            return crossX ? crossX : crossY;
        }

        // Whether the clip lets the cursor reach the pixel just past (`outward`) or just inside the
        // given side of the rect, at `at`'s coordinate along that side. No clip lets it reach any
        // pixel. 64-bit, since the pixel past an extreme rect lies outside the 32-bit range.
        static bool Reaches(const std::optional<RectL>& clip, const RectL& rect, Side side, PointL at, bool outward)
        {
            if (!clip)
            {
                return true;
            }
            int64_t x = at.x;
            int64_t y = at.y;
            switch (side)
            {
            case Side::Left:
                x = outward ? static_cast<int64_t>(rect.left) - 1 : rect.left;
                break;
            case Side::Top:
                y = outward ? static_cast<int64_t>(rect.top) - 1 : rect.top;
                break;
            case Side::Right:
                x = outward ? rect.right : static_cast<int64_t>(rect.right) - 1;
                break;
            case Side::Bottom:
            default:
                y = outward ? rect.bottom : static_cast<int64_t>(rect.bottom) - 1;
                break;
            }
            return x >= clip->left && x < clip->right && y >= clip->top && y < clip->bottom;
        }

        static PointL ClampInto(PointL point, const RectL& rect)
        {
            return PointL{ std::clamp(point.x, rect.left, rect.right - 1), std::clamp(point.y, rect.top, rect.bottom - 1) };
        }

        // The free return lasts while the cursor stays on the monitor it crossed onto, no farther
        // from the crossed edge than FREE_RETURN_LIMIT, scaled like the return crossing's threshold.
        // Leaving that monitor over any other edge ends it, so no timer is needed.
        void ExpireFreeReturn(const Layout& layout, size_t monitor, PointL point)
        {
            if (!m_freeReturn)
            {
                return;
            }

            const int64_t away = Overshoot(layout.monitors[m_freeReturn->to].rect, point, m_freeReturn->side) - 1;
            const uint64_t distance = away > 0 ? static_cast<uint64_t>(away) : 0;
            const uint64_t limit = ScaledThreshold(FREE_RETURN_LIMIT, layout.monitors[m_freeReturn->from].dpi);
            if (monitor != m_freeReturn->from || distance * BASE_DPI > limit)
            {
                m_freeReturn.reset();
            }
        }

        Settings m_settings;
        Layout m_layout;
        // The full-screen wall of the last move, compared on every move like the settings.
        std::optional<size_t> m_wallMonitor;
        // The clip in effect, from the last move that carried one.
        std::optional<RectL> m_clip;
        std::optional<Push> m_push;
        std::optional<FreeReturn> m_freeReturn;
    };
}
