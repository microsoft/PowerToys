// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#pragma once

#include "CursorBrakeCore.h"

#include <algorithm>
#include <atomic>
#include <chrono>
#include <compare>
#include <cstdint>
#include <format>
#include <functional>
#include <numeric>
#include <optional>
#include <string>
#include <string_view>
#include <unordered_map>
#include <unordered_set>
#include <utility>
#include <vector>

// Per-edge helpers around the CursorBrake engine, Win32-free like CursorBrakeCore.h so the unit
// tests reach them. They turn the layout into the edges the module publishes, match the monitor
// IDs a settings entry names to the layout's monitors, and keep the record of when each pair of
// monitors was last seen next to each other.
namespace cursorbrake
{
    // An edge two monitors share. `sideBySide` edges run along x = at, stacked ones along y = at,
    // and the shared span is [from, to) along that line.
    struct InnerEdge
    {
        size_t first = 0;
        size_t second = 0;
        bool sideBySide = true;
        long at = 0;
        long from = 0;
        long to = 0;

        bool operator==(const InnerEdge&) const = default;
    };

    // The edge two rects share, if they share one: `b` borders a side of `a`, and the shared span
    // holds at least a pixel. Rects meeting only at a corner, and identical rects, share none.
    inline std::optional<InnerEdge> SharedEdge(const RectL& a, const RectL& b)
    {
        const long overlapTop = (std::max)(a.top, b.top);
        const long overlapBottom = (std::min)(a.bottom, b.bottom);
        const long overlapLeft = (std::max)(a.left, b.left);
        const long overlapRight = (std::min)(a.right, b.right);
        if ((Borders(a, b, Side::Right) || Borders(a, b, Side::Left)) && overlapTop < overlapBottom)
        {
            return InnerEdge{ 0, 0, true, a.right == b.left ? a.right : a.left, overlapTop, overlapBottom };
        }
        if ((Borders(a, b, Side::Bottom) || Borders(a, b, Side::Top)) && overlapLeft < overlapRight)
        {
            return InnerEdge{ 0, 0, false, a.bottom == b.top ? a.bottom : a.top, overlapLeft, overlapRight };
        }
        return std::nullopt;
    }

    // Every edge between two monitors of the layout, ordered by the first monitor's index, then
    // the second's, with first < second. This is what the module publishes for Settings.
    inline std::vector<InnerEdge> InnerEdges(const Layout& layout)
    {
        std::vector<InnerEdge> edges;
        for (size_t i = 0; i < layout.monitors.size(); ++i)
        {
            for (size_t j = i + 1; j < layout.monitors.size(); ++j)
            {
                if (std::optional<InnerEdge> edge = SharedEdge(layout.monitors[i].rect, layout.monitors[j].rect))
                {
                    edge->first = i;
                    edge->second = j;
                    edges.push_back(*edge);
                }
            }
        }
        return edges;
    }

    // A display target, the connector a monitor hangs off: its adapter, by the order adapters first
    // appear in the display configuration, and its id on that adapter.
    struct DisplayTarget
    {
        size_t adapterOrder = 0;
        uint32_t id = 0;

        auto operator<=>(const DisplayTarget&) const = default;
    };

    // The number Windows Settings gives each monitor, by index. Settings numbers every available
    // target, active or not, by adapter and then by target id, so a monitor detached from the
    // desktop keeps its number and the ones after it keep theirs. `monitors` holds each monitor's
    // target, or none when no active path shows it, and `available` every available target, in any
    // order and with repeats. A monitor without a target numbers after every available target, in
    // monitor order, so it never takes a number Windows gives another display.
    inline std::vector<int> DisplayNumbers(const std::vector<std::optional<DisplayTarget>>& monitors, std::vector<DisplayTarget> available)
    {
        std::sort(available.begin(), available.end());
        available.erase(std::unique(available.begin(), available.end()), available.end());
        std::vector<int> numbers(monitors.size(), 0);
        int next = static_cast<int>(available.size());
        for (size_t i = 0; i < monitors.size(); ++i)
        {
            const auto found = monitors[i] ? std::lower_bound(available.begin(), available.end(), *monitors[i]) : available.end();
            numbers[i] = found != available.end() && *found == *monitors[i] ? static_cast<int>(found - available.begin()) + 1 : ++next;
        }
        return numbers;
    }
    // Whether a display path's output technology (DISPLAYCONFIG_VIDEO_OUTPUT_TECHNOLOGY) is a panel
    // built into the device: internal, embedded DisplayPort or embedded UDI.
    constexpr bool IsBuiltInOutput(long technology)
    {
        constexpr long OUTPUT_INTERNAL = static_cast<long>(0x80000000);
        constexpr long OUTPUT_DISPLAYPORT_EMBEDDED = 11;
        constexpr long OUTPUT_UDI_EMBEDDED = 13;
        return technology == OUTPUT_INTERNAL || technology == OUTPUT_DISPLAYPORT_EMBEDDED || technology == OUTPUT_UDI_EMBEDDED;
    }

    // Whether a move from `from` into `to`, two monitors that meet only at a corner, gets through
    // without the cross key. The engine judges such a move by the stricter of the rules the two
    // sides of `from` have at that corner, so a wall on either side holds it. None when the two do
    // not meet at exactly a corner.
    inline std::optional<bool> CornerOpen(const Layout& layout, const Settings& settings, size_t from, size_t to)
    {
        const RectL& a = layout.monitors[from].rect;
        const RectL& b = layout.monitors[to].rect;
        if (a.right <= a.left || a.bottom <= a.top || b.right <= b.left || b.bottom <= b.top)
        {
            return std::nullopt;
        }
        const bool right = b.left == a.right;
        const bool below = b.top == a.bottom;
        if (!(right || b.right == a.left) || !(below || b.bottom == a.top))
        {
            return std::nullopt;
        }
        const long x = right ? a.right - 1 : a.left;
        const long y = below ? a.bottom - 1 : a.top;
        const EdgeRule sideBySide = RuleFor(layout, settings, from, NeighborAcross(layout, from, right ? Side::Right : Side::Left, y), true);
        const EdgeRule stacked = RuleFor(layout, settings, from, NeighborAcross(layout, from, below ? Side::Bottom : Side::Top, x), false);
        return Stricter(sideBySide, stacked).mode != EdgeMode::Wall;
    }

    // The monitors that can be entered without the cross key but left only with it: every way out,
    // over a shared edge or through a corner, is a wall, and at least one way in is not.
    inline std::vector<size_t> TrappedMonitors(const Layout& layout, const Settings& settings)
    {
        const std::vector<InnerEdge> edges = InnerEdges(layout);
        std::vector<size_t> trapped;
        for (size_t monitor = 0; monitor < layout.monitors.size(); ++monitor)
        {
            bool anyOut = false;
            bool walledOut = true;
            bool openIn = false;
            for (const InnerEdge& edge : edges)
            {
                if (edge.first != monitor && edge.second != monitor)
                {
                    continue;
                }
                const size_t other = edge.first == monitor ? edge.second : edge.first;
                anyOut = true;
                walledOut = walledOut && RuleFor(layout, settings, monitor, other, edge.sideBySide).mode == EdgeMode::Wall;
                openIn = openIn || RuleFor(layout, settings, other, monitor, edge.sideBySide).mode != EdgeMode::Wall;
            }
            for (size_t other = 0; other < layout.monitors.size(); ++other)
            {
                const std::optional<bool> out = other == monitor ? std::nullopt : CornerOpen(layout, settings, monitor, other);
                if (out)
                {
                    anyOut = true;
                    walledOut = walledOut && !*out;
                    openIn = openIn || CornerOpen(layout, settings, other, monitor).value_or(false);
                }
            }
            if (anyOut && walledOut && openIn)
            {
                trapped.push_back(monitor);
            }
        }
        return trapped;
    }

    // Where "Show on screens" draws the direction leaving `source` over `edge`: a band `thickness`
    // pixels deep inside the source, along the shared span.
    inline RectL DirectionStrip(const RectL& source, const InnerEdge& edge, long thickness)
    {
        if (edge.sideBySide)
        {
            const bool rightSide = source.right == edge.at;
            return rightSide ? RectL{ edge.at - thickness, edge.from, edge.at, edge.to } : RectL{ edge.at, edge.from, edge.at + thickness, edge.to };
        }
        const bool bottomSide = source.bottom == edge.at;
        return bottomSide ? RectL{ edge.from, edge.at - thickness, edge.to, edge.at } : RectL{ edge.from, edge.at, edge.to, edge.at + thickness };
    }

    // A monitor's ID in PowerDisplay's Monitor.Id form, \\?\DISPLAY#<EDID code>#<instance>. A
    // monitor without an instance has no usable ID, so it gets an empty one.
    inline std::wstring MonitorId(std::wstring_view edidId, std::wstring_view instanceId)
    {
        if (edidId.empty() || instanceId.empty())
        {
            return {};
        }
        std::wstring id = L"\\\\?\\DISPLAY#";
        id.append(edidId);
        id.push_back(L'#');
        id.append(instanceId);
        return id;
    }

    // The ID in a monitor's device interface path, \\?\DISPLAY#<EDID code>#<instance>#{class},
    // as QueryDisplayConfig and EnumDisplayDevices report it. Empty when the path has no EDID code
    // or no instance.
    inline std::wstring MonitorIdFromDevicePath(std::wstring_view path)
    {
        const size_t codeStart = path.find(L'#');
        if (codeStart == std::wstring_view::npos)
        {
            return {};
        }
        const size_t instanceStart = path.find(L'#', codeStart + 1);
        if (instanceStart == std::wstring_view::npos)
        {
            return {};
        }
        const size_t instanceEnd = path.find(L'#', instanceStart + 1);
        const std::wstring_view code = path.substr(codeStart + 1, instanceStart - codeStart - 1);
        const std::wstring_view instance = path.substr(instanceStart + 1, instanceEnd == std::wstring_view::npos ? std::wstring_view::npos : instanceEnd - instanceStart - 1);
        return MonitorId(code, instance);
    }

    // A character with ASCII letters lowercased and every other character as it is.
    constexpr wchar_t FoldAscii(wchar_t c)
    {
        return c >= L'A' && c <= L'Z' ? static_cast<wchar_t>(c - L'A' + L'a') : c;
    }

    // Monitor IDs compare without regard to ASCII case, as Settings compares them. Device paths
    // are ASCII, so folding ASCII letters only matches StringComparer.OrdinalIgnoreCase on them.
    inline bool SameMonitorId(std::wstring_view a, std::wstring_view b)
    {
        if (a.size() != b.size())
        {
            return false;
        }
        for (size_t i = 0; i < a.size(); ++i)
        {
            if (FoldAscii(a[i]) != FoldAscii(b[i]))
            {
                return false;
            }
        }
        return true;
    }

    // For each monitor of a new layout, by index, the monitor of the previous layout with the same
    // GDI device and the same rect, or none. Identities are read off the hook thread, so until a
    // read returns, a monitor that did not move keeps what was read for it. Device names are ASCII,
    // so they compare without regard to ASCII case.
    inline std::vector<std::optional<size_t>> MatchPrevious(const std::vector<std::wstring>& devices, const std::vector<RectL>& rects, const std::vector<std::wstring>& previousDevices, const std::vector<RectL>& previousRects)
    {
        std::vector<std::optional<size_t>> matches(devices.size());
        for (size_t i = 0; i < devices.size() && i < rects.size(); ++i)
        {
            for (size_t j = 0; j < previousDevices.size() && j < previousRects.size(); ++j)
            {
                if (!devices[i].empty() && SameMonitorId(devices[i], previousDevices[j]) && rects[i] == previousRects[j])
                {
                    matches[i] = j;
                    break;
                }
            }
        }
        return matches;
    }

    // What a read found for each monitor of one enumeration, by index: its ID (empty when unknown),
    // friendly name, whether it is a panel built into the device, and Windows' display number.
    // `devices` and `rects` hold the GDI device and rect each monitor had when the read finished.
    // `complete` is false when DisplayUtils could not read every display. A read that failed
    // outright is incomplete and holds no monitors.
    struct Identities
    {
        uint64_t generation = 0;
        bool complete = true;
        std::vector<std::wstring> ids;
        std::vector<std::wstring> names;
        std::vector<bool> builtIn;
        std::vector<int> numbers;
        std::vector<std::wstring> devices;
        std::vector<RectL> rects;
    };

    // What the hook thread does with a finished identity read.
    enum class IdentityReadUse
    {
        // The read was asked for a layout that is gone, and a newer read or rebuild follows.
        Drop,
        // The read failed, or a display change overtook it: the monitors keep the identities they
        // have, and the read runs again.
        Retry,
        Apply,
    };

    // Judges a read against the current layout: its generation, and each monitor's GDI device and
    // rect, by index.
    inline IdentityReadUse JudgeIdentities(const Identities& read, uint64_t generation, const std::vector<std::wstring>& devices, const std::vector<RectL>& rects)
    {
        if (read.generation != generation)
        {
            return IdentityReadUse::Drop;
        }
        const size_t count = devices.size();
        if (rects.size() != count || read.devices.size() != count || read.rects.size() != count || read.ids.size() != count || read.names.size() != count || read.builtIn.size() != count || read.numbers.size() != count)
        {
            return IdentityReadUse::Retry;
        }
        for (size_t i = 0; i < count; ++i)
        {
            if (!SameMonitorId(read.devices[i], devices[i]) || read.rects[i] != rects[i])
            {
                return IdentityReadUse::Retry;
            }
        }
        return IdentityReadUse::Apply;
    }

    // Lets a "show on screens" request through to the overlay only when Settings' custom action
    // asked for it, and at most once every SHOW_INTERVAL_MS, so a stream of window messages from
    // another process cannot make the hook thread tear down and rebuild the overlay. Request runs
    // on the runner thread, Accept on the hook thread.
    class ShowGate
    {
    public:
        static constexpr int64_t SHOW_INTERVAL_MS = 1000;

        void Request()
        {
            m_requested.store(true);
        }

        // Whether to show the overlay at `nowMs`, a steady clock in milliseconds. Each request is
        // used up, shown or not.
        bool Accept(int64_t nowMs)
        {
            if (!m_requested.exchange(false) || (m_lastShownMs && nowMs - *m_lastShownMs < SHOW_INTERVAL_MS))
            {
                return false;
            }
            m_lastShownMs = nowMs;
            return true;
        }

    private:
        std::atomic<bool> m_requested{ false };
        std::optional<int64_t> m_lastShownMs;
    };

    // An entry of the `edges` setting, naming its monitors by ID.
    struct EdgeOverride
    {
        std::wstring first;
        std::wstring second;
        bool oneWay = false;
        EdgeMode mode = EdgeMode::Custom;
        int pushDistance = DEFAULT_PUSH_DISTANCE;

        bool operator==(const EdgeOverride&) const = default;
    };

    // The engine's per-edge settings for the layout: each override whose two IDs name two
    // monitors that share an edge, in the overrides' order, so the engine's first-match rule
    // keeps the first of two entries for one direction. `ids` holds each monitor's ID by index,
    // empty for a monitor without one, which no override reaches. An override naming a monitor
    // that is not connected stays dormant until it is.
    inline std::vector<EdgeSetting> ResolveEdges(const Layout& layout, const std::vector<std::wstring>& ids, const std::vector<EdgeOverride>& overrides)
    {
        const auto indexOf = [&](const std::wstring& id) -> std::optional<size_t> {
            for (size_t i = 0; i < ids.size() && i < layout.monitors.size(); ++i)
            {
                if (!ids[i].empty() && SameMonitorId(ids[i], id))
                {
                    return i;
                }
            }
            return std::nullopt;
        };

        std::vector<EdgeSetting> edges;
        for (const EdgeOverride& entry : overrides)
        {
            const std::optional<size_t> first = indexOf(entry.first);
            const std::optional<size_t> second = indexOf(entry.second);
            if (!first || !second || *first == *second || !SharedEdge(layout.monitors[*first].rect, layout.monitors[*second].rect))
            {
                continue;
            }
            if (!ChoiceOf<EdgeMode>(static_cast<int>(entry.mode), EDGE_MODE_COUNT))
            {
                continue;
            }
            edges.push_back(EdgeSetting{ *first, *second, entry.oneWay, entry.mode, ClampPushDistance(entry.pushDistance) });
        }
        return edges;
    }

    // The pairs of identified monitors that share an edge, as their IDs, in InnerEdges order.
    // `ids` holds each monitor's ID by index, empty for a monitor without one.
    inline std::vector<std::pair<std::wstring, std::wstring>> PresentPairs(const Layout& layout, const std::vector<std::wstring>& ids)
    {
        std::vector<std::pair<std::wstring, std::wstring>> pairs;
        for (const InnerEdge& edge : InnerEdges(layout))
        {
            if (edge.second < ids.size() && !ids[edge.first].empty() && !ids[edge.second].empty())
            {
                pairs.emplace_back(ids[edge.first], ids[edge.second]);
            }
        }
        return pairs;
    }

    // The pairs the overrides name, in their order.
    inline std::vector<std::pair<std::wstring, std::wstring>> NamedPairs(const std::vector<EdgeOverride>& overrides)
    {
        std::vector<std::pair<std::wstring, std::wstring>> pairs;
        for (const EdgeOverride& entry : overrides)
        {
            pairs.emplace_back(entry.first, entry.second);
        }
        return pairs;
    }

    // A time in seconds since the Unix epoch as ISO 8601 UTC to the second, the form
    // detected-edges.json carries: 2026-10-01T08:12:00Z.
    inline std::wstring FormatUtc(int64_t seconds)
    {
        const std::chrono::sys_seconds time{ std::chrono::seconds{ seconds } };
        return std::format(L"{:%Y-%m-%dT%H:%M:%SZ}", time);
    }

    // The time FormatUtc wrote, or none for any other text, an impossible date among it.
    inline std::optional<int64_t> ParseUtc(std::wstring_view text)
    {
        // YYYY-MM-DDTHH:MM:SSZ, digits everywhere else.
        constexpr std::wstring_view shape = L"0000-00-00T00:00:00Z";
        if (text.size() != shape.size())
        {
            return std::nullopt;
        }
        for (size_t i = 0; i < shape.size(); ++i)
        {
            const bool digit = text[i] >= L'0' && text[i] <= L'9';
            if (shape[i] == L'0' ? !digit : text[i] != shape[i])
            {
                return std::nullopt;
            }
        }
        const auto number = [&](size_t start, size_t length) {
            int value = 0;
            for (size_t i = start; i < start + length; ++i)
            {
                value = value * 10 + (text[i] - L'0');
            }
            return value;
        };

        using namespace std::chrono;
        const year_month_day date{ year{ number(0, 4) }, month{ static_cast<unsigned>(number(5, 2)) }, day{ static_cast<unsigned>(number(8, 2)) } };
        const int hour = number(11, 2);
        const int minute = number(14, 2);
        const int second = number(17, 2);
        if (!date.ok() || hour > 23 || minute > 59 || second > 59)
        {
            return std::nullopt;
        }
        return sys_days{ date }.time_since_epoch() / 1s + hour * 3600LL + minute * 60LL + second;
    }

    // How long a pair of monitors not seen together keeps its entries, in seconds: 180 days.
    // Settings' CursorBrakeEdgeModel.SeenRetentionDays holds the same span.
    constexpr int64_t SEEN_RETENTION_SECONDS = 180LL * 24 * 60 * 60;

    // Bound on the seen pairs the module reads back from detected-edges.json and writes to it.
    constexpr size_t MAX_SEEN_ENTRIES = 1024;

    // How far behind the newest seen time the clock may read and still be trusted to age the
    // record, in seconds: a day.
    constexpr int64_t SEEN_CLOCK_SLACK_SECONDS = 24LL * 60 * 60;

    // 2100-01-01T00:00:00Z. A seen time at or past it is not read, so a hand-edited time cannot
    // keep the record from aging beyond then.
    constexpr int64_t SEEN_TIME_LIMIT = 4'102'444'800;

    // When the module last saw a pair of monitors next to each other, in seconds since the Unix
    // epoch. The pair is unordered.
    struct SeenPair
    {
        std::wstring first;
        std::wstring second;
        int64_t lastSeen = 0;

        bool operator==(const SeenPair&) const = default;
    };

    inline bool SamePair(const std::wstring& a1, const std::wstring& a2, const std::wstring& b1, const std::wstring& b2)
    {
        return (SameMonitorId(a1, b1) && SameMonitorId(a2, b2)) || (SameMonitorId(a1, b2) && SameMonitorId(a2, b1));
    }

    // A pair of monitor IDs as a hash key: ASCII case folded and the two in order, so two pairs
    // have equal keys exactly when SamePair holds.
    using PairKey = std::pair<std::wstring, std::wstring>;

    inline PairKey KeyOfPair(std::wstring_view first, std::wstring_view second)
    {
        const auto fold = [](std::wstring_view id) {
            std::wstring folded{ id };
            std::transform(folded.begin(), folded.end(), folded.begin(), FoldAscii);
            return folded;
        };
        PairKey key{ fold(first), fold(second) };
        if (key.second < key.first)
        {
            std::swap(key.first, key.second);
        }
        return key;
    }

    struct PairKeyHash
    {
        size_t operator()(const PairKey& key) const noexcept
        {
            const size_t first = std::hash<std::wstring>{}(key.first);
            return first ^ (std::hash<std::wstring>{}(key.second) + 0x9e3779b97f4a7c15 + (first << 6) + (first >> 2));
        }
    };

    using PairKeySet = std::unordered_set<PairKey, PairKeyHash>;

    // What detected-edges.json remembers about pairs of monitors: when each was last seen
    // together, and the pairs whose retention ran out while an override still named them.
    struct SeenRecord
    {
        std::vector<SeenPair> seen;
        std::vector<std::pair<std::wstring, std::wstring>> expired;

        bool operator==(const SeenRecord&) const = default;
    };

    // The record after a publish at `now`:
    //  - Every pair in `present` (adjacent before or after the change being published) is stamped
    //    now, and leaves `expired`.
    //  - Every pair in `named` (named by an override) is added at now unless already recorded or
    //    expired, so a new entry starts its own retention once and naming never extends it.
    //  - A pair last seen more than `retention` seconds ago is dropped, and moves to `expired` while
    //    an override names it. Settings prunes entries for pairs not in `seen`, and an expired pair
    //    leaves `expired` once no override names it.
    //  - A clock more than SEEN_CLOCK_SLACK_SECONDS behind the newest recorded time is wrong, as
    //    after a firmware reset or a restored checkpoint. That publish drops and expires nothing,
    //    and stamps with the newest recorded time instead of now: the latest time known to have
    //    passed.
    //  - Past MAX_SEEN_ENTRIES pairs, the present and named ones stay, then the most recently seen
    //    of the rest.
    // Recorded pairs keep their order, new ones follow in the order given, and a pair is recorded
    // once.
    inline SeenRecord MergeSeen(const SeenRecord& previous, const std::vector<std::pair<std::wstring, std::wstring>>& present, const std::vector<std::pair<std::wstring, std::wstring>>& named, int64_t now, int64_t retention = SEEN_RETENTION_SECONDS)
    {
        const auto usable = [](const std::wstring& first, const std::wstring& second) {
            return !first.empty() && !second.empty() && !SameMonitorId(first, second);
        };

        std::optional<int64_t> newest;
        for (const SeenPair& seen : previous.seen)
        {
            if (usable(seen.first, seen.second) && (!newest || seen.lastSeen > *newest))
            {
                newest = seen.lastSeen;
            }
        }
        // Unsigned, because a hand-edited time and a test's clock can lie far enough apart that
        // the signed difference overflows.
        const bool trusted = !newest || *newest <= now || static_cast<uint64_t>(*newest) - static_cast<uint64_t>(now) <= static_cast<uint64_t>(SEEN_CLOCK_SLACK_SECONDS);
        const int64_t stamp = trusted ? now : *newest;

        std::vector<SeenPair> merged;
        std::unordered_map<PairKey, size_t, PairKeyHash> recorded;
        for (const SeenPair& seen : previous.seen)
        {
            if (!usable(seen.first, seen.second))
            {
                continue;
            }
            const auto [slot, added] = recorded.try_emplace(KeyOfPair(seen.first, seen.second), merged.size());
            if (added)
            {
                merged.push_back(seen);
            }
            else
            {
                merged[slot->second].lastSeen = (std::max)(merged[slot->second].lastSeen, seen.lastSeen);
            }
        }

        PairKeySet kept;
        for (const auto& [first, second] : present)
        {
            if (!usable(first, second))
            {
                continue;
            }
            PairKey key = KeyOfPair(first, second);
            const auto [slot, added] = recorded.try_emplace(key, merged.size());
            if (added)
            {
                merged.push_back(SeenPair{ first, second, stamp });
            }
            else
            {
                merged[slot->second].lastSeen = stamp;
            }
            kept.insert(std::move(key));
        }

        PairKeySet wasExpired;
        for (const auto& [first, second] : previous.expired)
        {
            if (usable(first, second))
            {
                wasExpired.insert(KeyOfPair(first, second));
            }
        }
        PairKeySet namedKeys;
        for (const auto& [first, second] : named)
        {
            if (!usable(first, second))
            {
                continue;
            }
            PairKey key = KeyOfPair(first, second);
            if (!wasExpired.contains(key) && recorded.try_emplace(key, merged.size()).second)
            {
                merged.push_back(SeenPair{ first, second, stamp });
            }
            kept.insert(key);
            namedKeys.insert(std::move(key));
        }

        // An expired pair stays expired while it is named and not seen again, without repeats.
        std::vector<std::pair<std::wstring, std::wstring>> expired;
        PairKeySet expiredKeys;
        for (const auto& [first, second] : previous.expired)
        {
            if (!usable(first, second))
            {
                continue;
            }
            PairKey key = KeyOfPair(first, second);
            if (namedKeys.contains(key) && !recorded.contains(key) && expiredKeys.insert(std::move(key)).second)
            {
                expired.emplace_back(first, second);
            }
        }

        if (trusted)
        {
            // Compared against the cutoff rather than as a difference, because a hand-edited file
            // can carry a time so far back that the difference overflows.
            const int64_t cutoff = now - retention;
            for (const SeenPair& seen : merged)
            {
                if (seen.lastSeen >= cutoff)
                {
                    continue;
                }
                PairKey key = KeyOfPair(seen.first, seen.second);
                if (namedKeys.contains(key) && expiredKeys.insert(std::move(key)).second)
                {
                    expired.emplace_back(seen.first, seen.second);
                }
            }
            std::erase_if(merged, [&](const SeenPair& seen) { return seen.lastSeen < cutoff; });
        }

        if (merged.size() > MAX_SEEN_ENTRIES)
        {
            std::vector<bool> priority(merged.size());
            for (size_t i = 0; i < merged.size(); ++i)
            {
                priority[i] = kept.contains(KeyOfPair(merged[i].first, merged[i].second));
            }
            std::vector<size_t> order(merged.size());
            std::iota(order.begin(), order.end(), size_t{ 0 });
            std::stable_sort(order.begin(), order.end(), [&](size_t a, size_t b) {
                return priority[a] != priority[b] ? priority[a] : merged[a].lastSeen > merged[b].lastSeen;
            });
            std::vector<bool> stays(merged.size(), false);
            for (size_t i = 0; i < MAX_SEEN_ENTRIES; ++i)
            {
                stays[order[i]] = true;
            }
            std::vector<SeenPair> capped;
            capped.reserve(MAX_SEEN_ENTRIES);
            for (size_t i = 0; i < merged.size(); ++i)
            {
                if (stays[i])
                {
                    capped.push_back(std::move(merged[i]));
                }
            }
            merged = std::move(capped);
        }
        return SeenRecord{ std::move(merged), std::move(expired) };
    }
}
