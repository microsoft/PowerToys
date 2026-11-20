// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#include "pch.h"
#include "CursorBrakeEdges.h"
#include "DetectedEdges.h"
#include "EdgeSettings.h"

#include <optional>
#include <string>
#include <vector>

using namespace Microsoft::VisualStudio::CppUnitTestFramework;
using namespace cursorbrake;

namespace
{
    Monitor Mon(long left, long top, long right, long bottom, unsigned int dpi = 96)
    {
        return Monitor{ RectL{ left, top, right, bottom }, dpi };
    }

    // IDs as PowerDisplay writes them: \\?\DISPLAY#<EDID code>#<instance>.
    const std::wstring DELL = L"\\\\?\\DISPLAY#DELA1B2#5&1a2b3c4d&0&UID4353";
    const std::wstring HP = L"\\\\?\\DISPLAY#HWP3A6E#5&1a2b3c4d&0&UID4352";
    const std::wstring LENOVO = L"\\\\?\\DISPLAY#LEN66F1#5&1a2b3c4d&0&UID4354";

    std::wstring Describe(const InnerEdge& edge)
    {
        return L"{" + std::to_wstring(edge.first) + L"-" + std::to_wstring(edge.second) + (edge.sideBySide ? L" side by side at x " : L" stacked at y ") + std::to_wstring(edge.at) + L", " + std::to_wstring(edge.from) + L".." + std::to_wstring(edge.to) + L"}";
    }

    std::wstring Describe(const EdgeSetting& edge)
    {
        return L"{" + std::to_wstring(edge.first) + (edge.oneWay ? L"->" : L"<->") + std::to_wstring(edge.second) + L" mode " + std::to_wstring(static_cast<int>(edge.mode)) + L" " + std::to_wstring(edge.pushDistance) + L"}";
    }

    std::wstring Describe(const SeenPair& seen)
    {
        return L"{" + seen.first + L", " + seen.second + L" at " + std::to_wstring(seen.lastSeen) + L"}";
    }

    std::wstring Describe(const EdgeOverride& edge)
    {
        return L"{" + edge.first + (edge.oneWay ? L" -> " : L" <-> ") + edge.second + L" mode " + std::to_wstring(static_cast<int>(edge.mode)) + L" " + std::to_wstring(edge.pushDistance) + L"}";
    }

    template<typename T>
    std::wstring DescribeAll(const std::vector<T>& items)
    {
        std::wstring text = L"[";
        for (const T& item : items)
        {
            text += (text.size() > 1 ? L", " : L"") + Describe(item);
        }
        return text + L"]";
    }

    // Collects one line per case whose result differs from what it expects, then fails once.
    template<typename T>
    void Check(std::wstring& failures, const std::wstring& name, const std::vector<T>& expected, const std::vector<T>& actual)
    {
        if (!(expected == actual))
        {
            failures += (failures.empty() ? L"" : L"; ") + name + L": expected " + DescribeAll(expected) + L", got " + DescribeAll(actual);
        }
    }

    void FailIfAny(const std::wstring& failures)
    {
        if (!failures.empty())
        {
            Assert::Fail(failures.c_str());
        }
    }

    EdgeOverride Both(const std::wstring& first, const std::wstring& second, EdgeMode mode, int pushDistance = 300)
    {
        return EdgeOverride{ first, second, false, mode, pushDistance };
    }

    EdgeOverride One(const std::wstring& first, const std::wstring& second, EdgeMode mode, int pushDistance = 300)
    {
        return EdgeOverride{ first, second, true, mode, pushDistance };
    }

    constexpr int64_t NOW = 1'790'000'000;
    constexpr int64_t DAY = 24 * 60 * 60;

    // 2026-10-01T08:12:00Z, the time the seen fixtures carry.
    constexpr int64_t OCTOBER_FIRST = 1'790'842'320;

    // The seen part of a merge from a record with no expired pairs.
    std::vector<SeenPair> Merge(const std::vector<SeenPair>& previous, const std::vector<std::pair<std::wstring, std::wstring>>& present, const std::vector<std::pair<std::wstring, std::wstring>>& named, int64_t now)
    {
        return MergeSeen(SeenRecord{ previous, {} }, present, named, now).seen;
    }

    // One publish as EdgePublisher::Write makes it: read the record, merge, and rebuild the file.
    winrt::Windows::Data::Json::JsonObject Publish(const winrt::Windows::Data::Json::JsonObject& file, const std::vector<std::pair<std::wstring, std::wstring>>& present, const std::vector<std::pair<std::wstring, std::wstring>>& named, int64_t now)
    {
        const SeenRecord record = MergeSeen(SeenRecord{ ReadSeen(file), ReadExpired(file) }, present, named, now);
        return BuildDetectedEdges({}, record.seen, record.expired);
    }

    // Every seen entry of a file, as Settings reads it: all of them, with no limit.
    std::vector<SeenPair> SeenInFile(const winrt::Windows::Data::Json::JsonObject& file)
    {
        std::vector<SeenPair> seen;
        const auto list = file.GetNamedArray(L"seen");
        for (uint32_t i = 0; i < list.Size(); ++i)
        {
            const auto entry = list.GetObjectAt(i);
            const auto ids = entry.GetNamedArray(L"monitors");
            seen.push_back(SeenPair{ std::wstring{ ids.GetStringAt(0) }, std::wstring{ ids.GetStringAt(1) }, ParseUtc(entry.GetNamedString(L"last_seen_utc")).value_or(0) });
        }
        return seen;
    }

    std::optional<int64_t> StampOf(const std::vector<SeenPair>& seen, const std::wstring& first, const std::wstring& second)
    {
        for (const SeenPair& pair : seen)
        {
            if (SamePair(pair.first, pair.second, first, second))
            {
                return pair.lastSeen;
            }
        }
        return std::nullopt;
    }
}

namespace CursorBrakeEdgeTests
{
    TEST_CLASS (InnerEdgeTests)
    {
    public:
        TEST_METHOD (InnerEdgesAreTheSpansMonitorsShare)
        {
            struct EdgeCase
            {
                const wchar_t* name;
                Layout layout;
                std::vector<InnerEdge> expected;
            };

            const EdgeCase cases[] = {
                { L"side by side", Layout{ { Mon(0, 0, 1920, 1080), Mon(1920, 0, 3840, 1080) } }, { { 0, 1, true, 1920, 0, 1080 } } },
                { L"stacked", Layout{ { Mon(0, 0, 1920, 1080), Mon(0, 1080, 1920, 2160) } }, { { 0, 1, false, 1080, 0, 1920 } } },
                { L"listed right to left", Layout{ { Mon(1920, 0, 3840, 1080), Mon(0, 0, 1920, 1080) } }, { { 0, 1, true, 1920, 0, 1080 } } },
                { L"partial overlap", Layout{ { Mon(0, 0, 1920, 1080), Mon(1920, 540, 3840, 1620) } }, { { 0, 1, true, 1920, 540, 1080 } } },
                { L"mixed DPI", Layout{ { Mon(0, 0, 1920, 1080, 96), Mon(1920, 0, 4800, 1620, 144), Mon(4800, 0, 8640, 2160, 192) } }, { { 0, 1, true, 1920, 0, 1080 }, { 1, 2, true, 4800, 0, 1620 } } },
                { L"duplicate rects", Layout{ { Mon(0, 0, 1920, 1080), Mon(0, 0, 1920, 1080) } }, {} },
                { L"a gap", Layout{ { Mon(0, 0, 1920, 1080), Mon(2000, 0, 3920, 1080) } }, {} },
                { L"touching at a corner only", Layout{ { Mon(0, 0, 1920, 1080), Mon(1920, 1080, 3840, 2160) } }, {} },
                { L"a 2x2 grid",
                  Layout{ { Mon(0, 0, 1920, 1080), Mon(1920, 0, 3840, 1080), Mon(0, 1080, 1920, 2160), Mon(1920, 1080, 3840, 2160) } },
                  { { 0, 1, true, 1920, 0, 1080 }, { 0, 2, false, 1080, 0, 1920 }, { 1, 3, false, 1080, 1920, 3840 }, { 2, 3, true, 1920, 1080, 2160 } } },
                { L"a portrait monitor to the right", Layout{ { Mon(0, 0, 1920, 1080), Mon(1920, 0, 3000, 1920) } }, { { 0, 1, true, 1920, 0, 1080 } } },
                { L"the same portrait monitor moved below", Layout{ { Mon(0, 0, 1920, 1080), Mon(0, 1080, 1080, 3000) } }, { { 0, 1, false, 1080, 0, 1080 } } },
                { L"one monitor", Layout{ { Mon(0, 0, 1920, 1080) } }, {} },
            };

            std::wstring failures;
            for (const EdgeCase& c : cases)
            {
                Check(failures, c.name, c.expected, InnerEdges(c.layout));
            }
            FailIfAny(failures);
        }
    };

    TEST_CLASS (MonitorIdTests)
    {
    public:
        TEST_METHOD (IdsTakePowerDisplaysForm)
        {
            Assert::AreEqual(std::wstring(L"\\\\?\\DISPLAY#GSM1388#4&125707d6&0&UID8388688"), MonitorId(L"GSM1388", L"4&125707d6&0&UID8388688"));
            Assert::AreEqual(std::wstring(), MonitorId(L"GSM1388", L""), L"A monitor without an instance has no ID.");
            Assert::AreEqual(std::wstring(), MonitorId(L"", L"4&125707d6&0&UID8388688"), L"A monitor without an EDID code has no ID.");
        }

        TEST_METHOD (IdsCompareWithoutRegardToAsciiCase)
        {
            struct CompareCase
            {
                const wchar_t* name;
                std::wstring a;
                std::wstring b;
                bool expected;
            };

            const CompareCase cases[] = {
                { L"identical", DELL, DELL, true },
                { L"letters in another case", DELL, L"\\\\?\\display#dela1b2#5&1A2B3C4D&0&uid4353", true },
                { L"another instance", DELL, L"\\\\?\\DISPLAY#DELA1B2#5&1a2b3c4d&0&UID4354", false },
                { L"a prefix", DELL, DELL.substr(0, DELL.size() - 1), false },
                { L"letters next to the ASCII range", L"@[`{", L"@[`{", true },
                { L"letters next to the ASCII range differ", L"@", L"`", false },
                { L"non-ASCII letters keep their case", L"\u00C9", L"\u00E9", false },
                { L"both empty", L"", L"", true },
            };

            std::wstring failures;
            for (const CompareCase& c : cases)
            {
                if (SameMonitorId(c.a, c.b) != c.expected || SameMonitorId(c.b, c.a) != c.expected)
                {
                    failures += (failures.empty() ? L"" : L"; ") + std::wstring(c.name);
                }
            }
            FailIfAny(failures);
        }
    };

    TEST_CLASS (ResolveEdgesTests)
    {
    public:
        TEST_METHOD (OverridesResolveToTheMonitorsTheyName)
        {
            // HP (0) | DELL (1), and LENOVO (2) above HP.
            const Layout layout{ { Mon(0, 0, 1920, 1080), Mon(1920, 0, 4480, 1440, 144), Mon(0, -2160, 3840, 0, 192) } };
            const std::vector<std::wstring> ids = { HP, DELL, LENOVO };

            struct ResolveCase
            {
                const wchar_t* name;
                std::vector<EdgeOverride> overrides;
                std::vector<EdgeSetting> expected;
            };

            const ResolveCase cases[] = {
                { L"both ways, named in layout order", { Both(HP, DELL, EdgeMode::Custom, 600) }, { { 0, 1, false, EdgeMode::Custom, 600 } } },
                { L"both ways, named in reverse", { Both(DELL, HP, EdgeMode::Wall) }, { { 1, 0, false, EdgeMode::Wall, 300 } } },
                { L"one way keeps its direction", { One(LENOVO, DELL, EdgeMode::Wall) }, { { 2, 1, true, EdgeMode::Wall, 300 } } },
                { L"IDs in another case", { Both(L"\\\\?\\display#hwp3a6e#5&1A2B3C4D&0&uid4352", DELL, EdgeMode::Off) }, { { 0, 1, false, EdgeMode::Off, 300 } } },
                { L"a disconnected monitor stays dormant", { Both(HP, L"\\\\?\\DISPLAY#LGD0A1B#5&1a2b3c4d&0&UID4355", EdgeMode::Wall), Both(HP, DELL, EdgeMode::Off) }, { { 0, 1, false, EdgeMode::Off, 300 } } },
                { L"order and duplicates are kept", { One(HP, DELL, EdgeMode::Wall), Both(DELL, HP, EdgeMode::Custom, 600), One(HP, DELL, EdgeMode::Off) }, { { 0, 1, true, EdgeMode::Wall, 300 }, { 1, 0, false, EdgeMode::Custom, 600 }, { 0, 1, true, EdgeMode::Off, 300 } } },
                { L"an unknown mode is skipped", { Both(HP, DELL, static_cast<EdgeMode>(3)), Both(HP, LENOVO, EdgeMode::Wall) }, { { 0, 2, false, EdgeMode::Wall, 300 } } },
                { L"the same monitor twice is skipped", { Both(HP, HP, EdgeMode::Wall) }, {} },
                { L"the distance is clamped", { Both(HP, DELL, EdgeMode::Custom, 5000), Both(HP, LENOVO, EdgeMode::Custom, -5) }, { { 0, 1, false, EdgeMode::Custom, 1000 }, { 0, 2, false, EdgeMode::Custom, 0 } } },
            };

            std::wstring failures;
            for (const ResolveCase& c : cases)
            {
                Check(failures, c.name, c.expected, ResolveEdges(layout, ids, c.overrides));
            }
            FailIfAny(failures);
        }

        TEST_METHOD (OnlyMonitorsThatShareAnEdgeResolve)
        {
            // DELL (1) and LENOVO (2) touch only at a corner, HP (0) and LENOVO across a gap.
            const Layout layout{ { Mon(0, 0, 1920, 1080), Mon(1920, 0, 3840, 1080), Mon(3840, 1080, 5760, 2160), Mon(0, 1200, 1920, 2280) } };
            const std::vector<std::wstring> ids = { HP, DELL, LENOVO, L"\\\\?\\DISPLAY#LGD0A1B#5&1a2b3c4d&0&UID4355" };
            std::wstring failures;
            Check(failures, L"corner contact", {}, ResolveEdges(layout, ids, { Both(DELL, LENOVO, EdgeMode::Wall) }));
            Check(failures, L"across a gap", {}, ResolveEdges(layout, ids, { Both(HP, ids[3], EdgeMode::Wall) }));
            Check(failures, L"sharing an edge", { { 0, 1, false, EdgeMode::Wall, 300 } }, ResolveEdges(layout, ids, { Both(HP, DELL, EdgeMode::Wall) }));
            FailIfAny(failures);
        }

        TEST_METHOD (MonitorsWithoutAnIdResolveNothing)
        {
            // DELL has no usable ID, so neither its old ID nor an empty one reaches it.
            const Layout layout{ { Mon(0, 0, 1920, 1080), Mon(1920, 0, 3840, 1080) } };
            const std::vector<std::wstring> ids = { HP, L"" };
            std::wstring failures;
            Check(failures, L"the monitor's former ID", {}, ResolveEdges(layout, ids, { Both(HP, DELL, EdgeMode::Wall) }));
            Check(failures, L"an empty ID", {}, ResolveEdges(layout, ids, { Both(HP, L"", EdgeMode::Wall) }));
            Check(failures, L"fewer IDs than monitors", {}, ResolveEdges(layout, { HP }, { Both(HP, DELL, EdgeMode::Wall) }));
            FailIfAny(failures);
        }

        TEST_METHOD (TheFirstOfTwoEntriesForADirectionWins)
        {
            // Resolved in order, the engine's lookup sees the first entry for each direction.
            const Layout base{ { Mon(0, 0, 1920, 1080), Mon(1920, 0, 3840, 1080) } };
            Layout layout = base;
            layout.edges = ResolveEdges(base, { HP, DELL }, { Both(DELL, HP, EdgeMode::Custom, 600), Both(HP, DELL, EdgeMode::Wall), One(DELL, HP, EdgeMode::Off), One(DELL, HP, EdgeMode::Wall) });
            const Settings settings;
            Assert::IsTrue(RuleFor(layout, settings, 0, 1, true) == EdgeRule{ EdgeMode::Custom, 600 }, L"HP to DELL takes the first both-ways entry.");
            Assert::IsTrue(RuleFor(layout, settings, 1, 0, true) == EdgeRule{ EdgeMode::Off, 300 }, L"DELL to HP takes the first one-way entry.");
        }
    };

    TEST_CLASS (MergeSeenTests)
    {
    public:
        TEST_METHOD (PresentPairsAreStampedNow)
        {
            std::wstring failures;
            Check(failures, L"a first publish", { { HP, DELL, NOW } }, Merge({}, { { HP, DELL } }, {}, NOW));
            Check(failures, L"a recorded pair seen again", { { HP, DELL, NOW } }, Merge({ { HP, DELL, NOW - 10 * DAY } }, { { HP, DELL } }, {}, NOW));
            Check(failures, L"a recorded pair not seen keeps its time", { { HP, DELL, NOW - 10 * DAY }, { HP, LENOVO, NOW } }, Merge({ { HP, DELL, NOW - 10 * DAY } }, { { HP, LENOVO } }, {}, NOW));
            Check(failures, L"named in the other order and case", { { HP, DELL, NOW } }, Merge({ { HP, DELL, NOW - DAY } }, { { L"\\\\?\\display#dela1b2#5&1A2B3C4D&0&uid4353", HP } }, {}, NOW));
            FailIfAny(failures);
        }

        TEST_METHOD (APairAnOverrideNamesStartsItsOwnClock)
        {
            // A pair only an override names, as from a deleted file or another machine, starts at
            // now. One already recorded keeps its time, so naming it never extends it.
            std::wstring failures;
            Check(failures, L"never seen", { { HP, LENOVO, NOW } }, Merge({}, {}, { { HP, LENOVO } }, NOW));
            Check(failures, L"already recorded", { { HP, LENOVO, NOW - 100 * DAY } }, Merge({ { HP, LENOVO, NOW - 100 * DAY } }, {}, { { LENOVO, HP } }, NOW));
            Check(failures, L"present and named", { { HP, LENOVO, NOW } }, Merge({ { HP, LENOVO, NOW - 100 * DAY } }, { { HP, LENOVO } }, { { HP, LENOVO } }, NOW));
            FailIfAny(failures);
        }

        TEST_METHOD (PairsLeaveAfterTheRetention)
        {
            const int64_t retention = SEEN_RETENTION_SECONDS;
            Assert::AreEqual(static_cast<int64_t>(180) * DAY, retention);
            std::wstring failures;
            Check(failures, L"exactly 180 days", { { HP, DELL, NOW - retention } }, Merge({ { HP, DELL, NOW - retention } }, {}, {}, NOW));
            Check(failures, L"a second more", {}, Merge({ { HP, DELL, NOW - retention - 1 } }, {}, {}, NOW));
            Check(failures, L"a second more, but present", { { HP, DELL, NOW } }, Merge({ { HP, DELL, NOW - retention - 1 } }, { { HP, DELL } }, {}, NOW));
            Check(failures, L"a time from the far past", {}, Merge({ { HP, DELL, INT64_MIN } }, {}, {}, NOW));
            Check(failures, L"a time ahead of the clock", { { HP, DELL, NOW + DAY } }, Merge({ { HP, DELL, NOW + DAY } }, {}, {}, NOW));
            FailIfAny(failures);
        }

        TEST_METHOD (ANamedPairExpiresOnceAndStaysExpired)
        {
            // An override names a pair last seen 181 days ago, and the module
            // publishes twice a minute apart. The first publish moves the pair from seen to
            // expired, so Settings prunes its entries. The second, with the entry still named,
            // leaves it there rather than starting a fresh 180 days.
            const std::vector<std::pair<std::wstring, std::wstring>> named{ { HP, LENOVO } };
            const SeenRecord first = MergeSeen(SeenRecord{ { { HP, LENOVO, NOW - 181 * DAY } }, {} }, {}, named, NOW);
            const SeenRecord second = MergeSeen(first, {}, named, NOW + 60);
            const SeenRecord later = MergeSeen(second, {}, { { LENOVO, HP } }, NOW + 30 * DAY);

            std::wstring failures;
            Check(failures, L"publish 1", {}, first.seen);
            Check(failures, L"publish 2, a minute later", {}, second.seen);
            Check(failures, L"a month later, named in the other order", {}, later.seen);
            FailIfAny(failures);
            Assert::IsTrue(first.expired == named, L"Publish 1 records the pair as expired.");
            Assert::IsTrue(second.expired == named && later.expired == named, L"The pair stays expired while it is named.");
        }

        TEST_METHOD (AnExpiredPairLeavesWhenUnnamedAndRestartsWhenSeen)
        {
            const SeenRecord expired{ {}, { { HP, LENOVO } } };
            const SeenRecord unnamed = MergeSeen(expired, {}, {}, NOW);
            const SeenRecord seenAgain = MergeSeen(expired, { { LENOVO, HP } }, { { HP, LENOVO } }, NOW);
            const SeenRecord fresh = MergeSeen(unnamed, {}, { { HP, LENOVO } }, NOW + DAY);

            std::wstring failures;
            Check(failures, L"seen again", { { LENOVO, HP, NOW } }, seenAgain.seen);
            Check(failures, L"named again once Settings dropped it", { { HP, LENOVO, NOW + DAY } }, fresh.seen);
            FailIfAny(failures);
            Assert::IsTrue(unnamed.expired.empty(), L"Settings pruned the entry, so the pair leaves the record.");
            Assert::IsTrue(seenAgain.expired.empty(), L"Seeing the pair again ends its expiry.");
            Assert::IsTrue(fresh.expired.empty());
        }

        TEST_METHOD (EachPairIsRecordedOnce)
        {
            // A hand-edited file can repeat a pair or name one monitor twice. The latest time of a
            // repeated pair stands, and a pair without two distinct IDs is dropped.
            std::wstring failures;
            Check(failures, L"repeated in the record", { { HP, DELL, NOW - DAY } }, Merge({ { HP, DELL, NOW - 5 * DAY }, { DELL, HP, NOW - DAY } }, {}, {}, NOW));
            Check(failures, L"repeated in what is present", { { HP, DELL, NOW } }, Merge({}, { { HP, DELL }, { DELL, HP } }, {}, NOW));
            Check(failures, L"one monitor twice", {}, Merge({ { HP, HP, NOW } }, { { DELL, DELL } }, { { LENOVO, LENOVO } }, NOW));
            Check(failures, L"an empty ID", {}, Merge({ { HP, L"", NOW } }, { { L"", DELL } }, { { LENOVO, L"" } }, NOW));
            FailIfAny(failures);
        }

        TEST_METHOD (AClockBehindTheNewestStampAgesNothing)
        {
            // A clock more than a day behind the newest stamp is wrong, so nothing expires and only
            // present pairs move, to the newest stamp: the latest time the record proves has passed.
            const std::wstring lg = L"\\\\?\\DISPLAY#LGD0A1B#5&1a2b3c4d&0&UID4355";
            const std::vector<SeenPair> previous{ { HP, DELL, NOW - 5 * DAY }, { DELL, lg, NOW }, { HP, LENOVO, NOW - 182 * DAY } };
            const std::vector<std::pair<std::wstring, std::wstring>> named{ { HP, LENOVO }, { DELL, LENOVO } };
            const SeenRecord behind = MergeSeen(SeenRecord{ previous, {} }, { { DELL, HP } }, named, NOW - DAY - 1);
            const SeenRecord withinADay = MergeSeen(SeenRecord{ previous, {} }, { { DELL, HP } }, named, NOW - DAY);

            std::wstring failures;
            Check(failures, L"more than a day behind", { { HP, DELL, NOW }, { DELL, lg, NOW }, { HP, LENOVO, NOW - 182 * DAY }, { DELL, LENOVO, NOW } }, behind.seen);
            Check(failures, L"a day behind", { { HP, DELL, NOW - DAY }, { DELL, lg, NOW }, { DELL, LENOVO, NOW - DAY } }, withinADay.seen);
            FailIfAny(failures);
            Assert::IsTrue(behind.expired.empty(), L"Nothing expires on a clock that runs behind.");
            Assert::IsTrue(withinADay.expired == std::vector<std::pair<std::wstring, std::wstring>>{ { HP, LENOVO } }, L"A day of lag is trusted.");
        }

        TEST_METHOD (TheRecordKeepsNamedAndPresentPairsThenTheNewest)
        {
            // Past MAX_SEEN_ENTRIES the record keeps every named and present pair, then the newest
            // of the rest, in the order they were recorded.
            std::vector<SeenPair> previous;
            for (int i = 0; i < 1100; ++i)
            {
                previous.push_back(SeenPair{ L"F" + std::to_wstring(i), L"G", NOW - i });
            }
            previous.push_back(SeenPair{ HP, LENOVO, NOW - 100 * DAY });
            const std::vector<SeenPair> merged = Merge(previous, { { HP, DELL } }, { { LENOVO, HP } }, NOW);

            Assert::AreEqual(MAX_SEEN_ENTRIES, merged.size());
            Assert::AreEqual(std::wstring(L"F1021"), merged[1021].first, L"The newest 1022 of the rest stay, in order.");
            Assert::IsTrue(merged[1022] == SeenPair{ HP, LENOVO, NOW - 100 * DAY }, L"The named pair stays with its own time.");
            Assert::IsTrue(merged[1023] == SeenPair{ HP, DELL, NOW }, L"The present pair stays.");
        }
    };

    TEST_CLASS (EdgeSettingsParsing)
    {
    public:
        TEST_METHOD (TheDesignsExampleParses)
        {
            const std::wstring json = LR"([
                { "monitors": [ "\\\\?\\DISPLAY#DELA1B2#5&1a2b3c4d&0&UID4353", "\\\\?\\DISPLAY#HWP3A6E#5&1a2b3c4d&0&UID4352" ], "mode": 0, "push_distance": 600 },
                { "monitors": [ "\\\\?\\DISPLAY#LEN66F1#5&1a2b3c4d&0&UID4354", "\\\\?\\DISPLAY#DELA1B2#5&1a2b3c4d&0&UID4353" ], "one_way": true, "mode": 2, "push_distance": 300 }
            ])";
            std::wstring failures;
            Check(failures, L"example", { Both(DELL, HP, EdgeMode::Custom, 600), One(LENOVO, DELL, EdgeMode::Wall, 300) }, ParseEdgeOverrides(winrt::Windows::Data::Json::JsonArray::Parse(json)));
            FailIfAny(failures);
        }

        TEST_METHOD (EachMalformedEntryIsSkippedAlone)
        {
            // Every case puts one bad entry between two good ones, and only the bad one goes.
            const std::wstring good = LR"({ "monitors": [ "\\\\?\\DISPLAY#HWP3A6E#5&1a2b3c4d&0&UID4352", "\\\\?\\DISPLAY#DELA1B2#5&1a2b3c4d&0&UID4353" ], "mode": 1, "push_distance": 300 })";
            const std::wstring longId = std::wstring(L"\\\\\\\\?\\\\DISPLAY#") + std::wstring(501, L'A');
            struct BadCase
            {
                const wchar_t* name;
                std::wstring entry;
            };

            const BadCase cases[] = {
                { L"not an object", L"42" },
                { L"no monitors", LR"({ "mode": 1 })" },
                { L"monitors not a list", LR"({ "monitors": "\\\\?\\DISPLAY#HWP3A6E#1", "mode": 1 })" },
                { L"one ID", LR"({ "monitors": [ "\\\\?\\DISPLAY#HWP3A6E#1" ], "mode": 1 })" },
                { L"three IDs", LR"({ "monitors": [ "\\\\?\\DISPLAY#A#1", "\\\\?\\DISPLAY#B#2", "\\\\?\\DISPLAY#C#3" ], "mode": 1 })" },
                { L"an ID that is not a string", LR"({ "monitors": [ "\\\\?\\DISPLAY#A#1", 7 ], "mode": 1 })" },
                { L"an empty ID", LR"({ "monitors": [ "\\\\?\\DISPLAY#A#1", "" ], "mode": 1 })" },
                { L"the same monitor twice, in another case", LR"({ "monitors": [ "\\\\?\\DISPLAY#AB#1", "\\\\?\\display#ab#1" ], "mode": 1 })" },
                { L"an ID past 512 characters", L"{ \"monitors\": [ \"\\\\\\\\?\\\\DISPLAY#A#1\", \"" + longId + L"\" ], \"mode\": 1 }" },
                { L"no mode", LR"({ "monitors": [ "\\\\?\\DISPLAY#A#1", "\\\\?\\DISPLAY#B#2" ] })" },
                { L"a mode that is a string", LR"({ "monitors": [ "\\\\?\\DISPLAY#A#1", "\\\\?\\DISPLAY#B#2" ], "mode": "wall" })" },
                { L"mode 3", LR"({ "monitors": [ "\\\\?\\DISPLAY#A#1", "\\\\?\\DISPLAY#B#2" ], "mode": 3 })" },
                { L"mode -1", LR"({ "monitors": [ "\\\\?\\DISPLAY#A#1", "\\\\?\\DISPLAY#B#2" ], "mode": -1 })" },
                { L"mode 1.5", LR"({ "monitors": [ "\\\\?\\DISPLAY#A#1", "\\\\?\\DISPLAY#B#2" ], "mode": 1.5 })" },
                { L"one_way a string", LR"({ "monitors": [ "\\\\?\\DISPLAY#A#1", "\\\\?\\DISPLAY#B#2" ], "mode": 2, "one_way": "true" })" },
                { L"one_way a number", LR"({ "monitors": [ "\\\\?\\DISPLAY#A#1", "\\\\?\\DISPLAY#B#2" ], "mode": 2, "one_way": 1 })" },
                { L"push_distance a string", LR"({ "monitors": [ "\\\\?\\DISPLAY#A#1", "\\\\?\\DISPLAY#B#2" ], "mode": 0, "push_distance": "600" })" },
            };

            const std::vector<EdgeOverride> expected = { Both(HP, DELL, EdgeMode::Off), Both(HP, DELL, EdgeMode::Off) };
            std::wstring failures;
            for (const BadCase& c : cases)
            {
                const std::wstring json = L"[" + good + L"," + c.entry + L"," + good + L"]";
                Check(failures, c.name, expected, ParseEdgeOverrides(winrt::Windows::Data::Json::JsonArray::Parse(json)));
            }
            FailIfAny(failures);
        }

        TEST_METHOD (ValuesAreKeptOrClamped)
        {
            const std::wstring ids = LR"("monitors": [ "\\\\?\\DISPLAY#HWP3A6E#5&1a2b3c4d&0&UID4352", "\\\\?\\DISPLAY#DELA1B2#5&1a2b3c4d&0&UID4353" ])";
            const std::wstring longest = std::wstring(512 - 12, L'A');
            struct ValueCase
            {
                const wchar_t* name;
                std::wstring entry;
                EdgeOverride expected;
            };

            const ValueCase cases[] = {
                { L"one_way absent", L"{ " + ids + LR"(, "mode": 2 })", Both(HP, DELL, EdgeMode::Wall) },
                { L"one_way false", L"{ " + ids + LR"(, "mode": 2, "one_way": false })", Both(HP, DELL, EdgeMode::Wall) },
                { L"one_way true", L"{ " + ids + LR"(, "mode": 2, "one_way": true })", One(HP, DELL, EdgeMode::Wall) },
                { L"push_distance absent takes the default", L"{ " + ids + LR"(, "mode": 0 })", Both(HP, DELL, EdgeMode::Custom, 300) },
                { L"push_distance past the top", L"{ " + ids + LR"(, "mode": 0, "push_distance": 5000 })", Both(HP, DELL, EdgeMode::Custom, 1000) },
                { L"push_distance below zero", L"{ " + ids + LR"(, "mode": 0, "push_distance": -3 })", Both(HP, DELL, EdgeMode::Custom, 0) },
                { L"push_distance with a fraction", L"{ " + ids + LR"(, "mode": 0, "push_distance": 999.9 })", Both(HP, DELL, EdgeMode::Custom, 999) },
                { L"push_distance kept under Off", L"{ " + ids + LR"(, "mode": 1, "push_distance": 450 })", Both(HP, DELL, EdgeMode::Off, 450) },
                { L"mode 1.0", L"{ " + ids + LR"(, "mode": 1.0 })", Both(HP, DELL, EdgeMode::Off) },
                { L"unknown fields", L"{ " + ids + LR"(, "mode": 1, "note": "kitchen" })", Both(HP, DELL, EdgeMode::Off) },
                { L"an ID of 512 characters", LR"({ "monitors": [ "\\\\?\\DISPLAY#)" + longest + LR"(", "\\\\?\\DISPLAY#DELA1B2#5&1a2b3c4d&0&UID4353" ], "mode": 1 })", Both(L"\\\\?\\DISPLAY#" + longest, DELL, EdgeMode::Off) },
            };

            std::wstring failures;
            for (const ValueCase& c : cases)
            {
                Check(failures, c.name, { c.expected }, ParseEdgeOverrides(winrt::Windows::Data::Json::JsonArray::Parse(L"[" + c.entry + L"]")));
            }
            FailIfAny(failures);
        }

        TEST_METHOD (OnlyTheFirst256EntriesAreRead)
        {
            // A hand-edited list cannot make the hook thread resolve an unbounded number of
            // entries. Bad entries count toward the cap too, since reading them is the cost.
            winrt::Windows::Data::Json::JsonArray entries;
            const auto entry = [](int i, int mode) {
                return winrt::Windows::Data::Json::JsonObject::Parse(L"{ \"monitors\": [ \"\\\\\\\\?\\\\DISPLAY#A#" + std::to_wstring(i) + L"\", \"\\\\\\\\?\\\\DISPLAY#B#1\" ], \"mode\": " + std::to_wstring(mode) + L" }");
            };
            entries.Append(entry(0, 7));
            for (int i = 1; i < 300; ++i)
            {
                entries.Append(entry(i, 1));
            }
            const std::vector<EdgeOverride> parsed = ParseEdgeOverrides(entries);
            Assert::AreEqual(static_cast<size_t>(255), parsed.size());
            Assert::AreEqual(std::wstring(L"\\\\?\\DISPLAY#A#1"), parsed.front().first);
            Assert::AreEqual(std::wstring(L"\\\\?\\DISPLAY#A#255"), parsed.back().first);
            Assert::AreEqual(static_cast<size_t>(256), MAX_EDGE_ENTRIES);
            Assert::AreEqual(static_cast<size_t>(512), MAX_MONITOR_ID_LENGTH);
        }

        TEST_METHOD (TheEdgesPropertyIsReadFromTheSettings)
        {
            // A file without the property means no entries. A property that cannot be read keeps
            // whatever the module had.
            using winrt::Windows::Data::Json::JsonObject;
            const std::wstring list = LR"([ { "monitors": [ "\\\\?\\DISPLAY#HWP3A6E#5&1a2b3c4d&0&UID4352", "\\\\?\\DISPLAY#DELA1B2#5&1a2b3c4d&0&UID4353" ], "mode": 1 } ])";
            const std::optional<std::vector<EdgeOverride>> absent = ReadEdgesSetting(JsonObject::Parse(LR"({ "push_distance": { "value": 300 } })"));
            const std::optional<std::vector<EdgeOverride>> present = ReadEdgesSetting(JsonObject::Parse(L"{ \"edges\": { \"value\": " + list + L" } }"));
            const std::optional<std::vector<EdgeOverride>> empty = ReadEdgesSetting(JsonObject::Parse(LR"({ "edges": { "value": [] } })"));
            const std::optional<std::vector<EdgeOverride>> notAList = ReadEdgesSetting(JsonObject::Parse(LR"({ "edges": { "value": 5 } })"));
            const std::optional<std::vector<EdgeOverride>> noValue = ReadEdgesSetting(JsonObject::Parse(LR"({ "edges": [] })"));
            Assert::IsTrue(absent.has_value() && absent->empty(), L"No property means no entries.");
            Assert::IsTrue(present.has_value() && *present == std::vector<EdgeOverride>{ Both(HP, DELL, EdgeMode::Off) }, L"The list under value is read.");
            Assert::IsTrue(empty.has_value() && empty->empty(), L"An empty list clears the entries.");
            Assert::IsFalse(notAList.has_value(), L"A value that is not a list keeps the previous entries.");
            Assert::IsFalse(noValue.has_value(), L"A property without a value keeps the previous entries.");
        }
    };

    TEST_CLASS (DevicePathTests)
    {
    public:
        TEST_METHOD (TheIdIsTheDevicePathUpToTheInterfaceClass)
        {
            struct PathCase
            {
                const wchar_t* name;
                std::wstring path;
                std::wstring expected;
            };

            const PathCase cases[] = {
                { L"a monitor interface path", L"\\\\?\\DISPLAY#GSM1388#4&125707d6&0&UID8388688#{e6f07b5f-ee97-4a90-b076-33f57bf4eaa7}", L"\\\\?\\DISPLAY#GSM1388#4&125707d6&0&UID8388688" },
                { L"no interface class", L"\\\\?\\DISPLAY#GSM1388#4&125707d6&0&UID8388688", L"\\\\?\\DISPLAY#GSM1388#4&125707d6&0&UID8388688" },
                { L"an empty instance", L"\\\\?\\DISPLAY#GSM1388##{e6f07b5f-ee97-4a90-b076-33f57bf4eaa7}", L"" },
                { L"no instance", L"\\\\?\\DISPLAY#GSM1388", L"" },
                { L"a GDI device name", L"\\\\.\\DISPLAY1", L"" },
                { L"empty", L"", L"" },
            };

            std::wstring failures;
            for (const PathCase& c : cases)
            {
                const std::wstring actual = MonitorIdFromDevicePath(c.path);
                if (actual != c.expected)
                {
                    failures += (failures.empty() ? L"" : L"; ") + std::wstring(c.name) + L": got " + actual;
                }
            }
            FailIfAny(failures);
        }
    };

    TEST_CLASS (UtcTimeTests)
    {
    public:
        TEST_METHOD (TimesFormatAsUtc)
        {
            Assert::AreEqual(std::wstring(L"1970-01-01T00:00:00Z"), FormatUtc(0));
            Assert::AreEqual(std::wstring(L"2026-10-01T08:12:00Z"), FormatUtc(1790842320));
            Assert::AreEqual(std::wstring(L"2024-02-29T23:59:59Z"), FormatUtc(1709251199));
            Assert::AreEqual(std::wstring(L"1969-12-31T23:59:59Z"), FormatUtc(-1));
        }

        TEST_METHOD (OnlyTheWrittenFormParses)
        {
            struct ParseCase
            {
                const wchar_t* text;
                std::optional<int64_t> expected;
            };

            const ParseCase cases[] = {
                { L"2026-10-01T08:12:00Z", 1790842320 },
                { L"2024-02-29T23:59:59Z", 1709251199 },
                { L"1970-01-01T00:00:00Z", 0 },
                { L"2023-02-29T00:00:00Z", std::nullopt },
                { L"2026-13-01T00:00:00Z", std::nullopt },
                { L"2026-10-01T24:00:00Z", std::nullopt },
                { L"2026-10-01T08:60:00Z", std::nullopt },
                { L"2026-10-01T08:12:60Z", std::nullopt },
                { L"2026-10-01T08:12:00", std::nullopt },
                { L"2026-10-01T08:12:00+02:00", std::nullopt },
                { L"2026-10-01t08:12:00Z", std::nullopt },
                { L"2026-10-01T08:12:00.5Z", std::nullopt },
                { L"2026-1-01T08:12:00Z", std::nullopt },
                { L"+026-10-01T08:12:00Z", std::nullopt },
                { L"", std::nullopt },
            };

            std::wstring failures;
            for (const ParseCase& c : cases)
            {
                if (ParseUtc(c.text) != c.expected)
                {
                    failures += (failures.empty() ? L"" : L"; ") + std::wstring(c.text);
                }
            }
            FailIfAny(failures);
        }
    };

    TEST_CLASS (PairTests)
    {
    public:
        TEST_METHOD (PresentPairsAreTheIdentifiedNeighbors)
        {
            // HP (0) | DELL (1), LENOVO (2) above HP, and an unidentified monitor right of DELL.
            const Layout layout{ { Mon(0, 0, 1920, 1080), Mon(1920, 0, 3840, 1080), Mon(0, -1080, 1920, 0), Mon(3840, 0, 5760, 1080) } };
            const std::vector<std::pair<std::wstring, std::wstring>> expected = { { HP, DELL }, { HP, LENOVO } };
            Assert::IsTrue(expected == PresentPairs(layout, { HP, DELL, LENOVO, L"" }));
            Assert::IsTrue(PresentPairs(layout, { HP }).empty(), L"Monitors past the IDs given count as unidentified.");
        }

        TEST_METHOD (NamedPairsFollowTheOverrides)
        {
            const std::vector<std::pair<std::wstring, std::wstring>> expected = { { HP, DELL }, { LENOVO, HP } };
            Assert::IsTrue(expected == NamedPairs({ Both(HP, DELL, EdgeMode::Wall), One(LENOVO, HP, EdgeMode::Off) }));
        }
    };

    TEST_CLASS (DetectedEdgesTests)
    {
    public:
        TEST_METHOD (TheFileTakesTheDesignedShape)
        {
            using winrt::Windows::Data::Json::JsonObject;
            const std::vector<DetectedMonitor> monitors = {
                { HP, L"HP E24 G5", RectL{ 0, 0, 1920, 1080 }, 96, true, 2, true },
                { DELL, L"Dell U2724D", RectL{ 1920, 0, 4480, 1440 }, 144, false, 3, false },
                { LENOVO, L"Lenovo L32p-30", RectL{ 0, -2160, 3840, 0 }, 192, false, 1, false },
            };
            const JsonObject file = BuildDetectedEdges(monitors, { { HP, L"\\\\?\\DISPLAY#LGD0A1B#5&1a2b3c4d&0&UID4355", 1790842320 } }, { { DELL, LENOVO } });

            Assert::AreEqual(1.0, file.GetNamedNumber(L"version"));
            Assert::IsTrue(file.GetNamedBoolean(L"identified"));

            const auto list = file.GetNamedArray(L"monitors");
            Assert::AreEqual(3u, list.Size());
            const JsonObject dell = list.GetObjectAt(1);
            Assert::AreEqual(DELL, std::wstring(dell.GetNamedString(L"id")));
            Assert::AreEqual(std::wstring(L"Dell U2724D"), std::wstring(dell.GetNamedString(L"name")));
            Assert::AreEqual(144.0, dell.GetNamedNumber(L"dpi"));
            Assert::IsFalse(dell.GetNamedBoolean(L"primary"));
            Assert::IsTrue(list.GetObjectAt(0).GetNamedBoolean(L"primary"));
            Assert::AreEqual(3.0, dell.GetNamedNumber(L"number"), L"Windows' number, not the index.");
            Assert::AreEqual(2.0, list.GetObjectAt(0).GetNamedNumber(L"number"));
            Assert::IsFalse(dell.GetNamedBoolean(L"built_in"));
            Assert::IsTrue(list.GetObjectAt(0).GetNamedBoolean(L"built_in"));
            const auto rect = dell.GetNamedArray(L"rect");
            Assert::AreEqual(4u, rect.Size());
            Assert::AreEqual(1920.0, rect.GetNumberAt(0));
            Assert::AreEqual(0.0, rect.GetNumberAt(1));
            Assert::AreEqual(4480.0, rect.GetNumberAt(2));
            Assert::AreEqual(1440.0, rect.GetNumberAt(3));

            // The fixture's three edges, in InnerEdges order.
            const auto edges = file.GetNamedArray(L"edges");
            Assert::AreEqual(3u, edges.Size());
            const std::wstring expectedEdges[] = {
                LR"({"monitors":[0,1],"axis":"vertical","at":1920,"from":0,"to":1080})",
                LR"({"monitors":[0,2],"axis":"horizontal","at":0,"from":0,"to":1920})",
                LR"({"monitors":[1,2],"axis":"horizontal","at":0,"from":1920,"to":3840})",
            };
            for (uint32_t i = 0; i < 3; ++i)
            {
                const JsonObject edge = edges.GetObjectAt(i);
                const JsonObject expected = JsonObject::Parse(expectedEdges[i]);
                Assert::AreEqual(expected.GetNamedArray(L"monitors").GetNumberAt(0), edge.GetNamedArray(L"monitors").GetNumberAt(0));
                Assert::AreEqual(expected.GetNamedArray(L"monitors").GetNumberAt(1), edge.GetNamedArray(L"monitors").GetNumberAt(1));
                Assert::AreEqual(std::wstring(expected.GetNamedString(L"axis")), std::wstring(edge.GetNamedString(L"axis")));
                Assert::AreEqual(expected.GetNamedNumber(L"at"), edge.GetNamedNumber(L"at"));
                Assert::AreEqual(expected.GetNamedNumber(L"from"), edge.GetNamedNumber(L"from"));
                Assert::AreEqual(expected.GetNamedNumber(L"to"), edge.GetNamedNumber(L"to"));
            }

            const auto seen = file.GetNamedArray(L"seen");
            Assert::AreEqual(1u, seen.Size());
            Assert::AreEqual(std::wstring(L"2026-10-01T08:12:00Z"), std::wstring(seen.GetObjectAt(0).GetNamedString(L"last_seen_utc")));
            Assert::AreEqual(HP, std::wstring(seen.GetObjectAt(0).GetNamedArray(L"monitors").GetStringAt(0)));

            // Pairs whose retention ran out while an entry named them, by ID alone.
            const auto expired = file.GetNamedArray(L"expired");
            Assert::AreEqual(1u, expired.Size());
            Assert::AreEqual(LENOVO, std::wstring(expired.GetObjectAt(0).GetNamedArray(L"monitors").GetStringAt(1)));
            Assert::IsFalse(expired.GetObjectAt(0).HasKey(L"last_seen_utc"));
        }

        TEST_METHOD (AnUnidentifiedMonitorIsPublishedWithoutAnId)
        {
            const auto file = BuildDetectedEdges({ { HP, L"HP E24 G5", RectL{ 0, 0, 1920, 1080 }, 96, true }, { L"", L"", RectL{ 1920, 0, 3840, 1080 }, 96, false } }, {});
            Assert::IsFalse(file.GetNamedBoolean(L"identified"));
            Assert::AreEqual(std::wstring(), std::wstring(file.GetNamedArray(L"monitors").GetObjectAt(1).GetNamedString(L"id")));
            Assert::AreEqual(1u, file.GetNamedArray(L"edges").Size(), L"Its edges are published.");
        }

        TEST_METHOD (TheSeenRecordReadsBack)
        {
            const std::vector<SeenPair> seen = { { HP, DELL, 1790842320 }, { LENOVO, HP, 1709251199 } };
            std::wstring failures;
            Check(failures, L"round trip", seen, ReadSeen(BuildDetectedEdges({}, seen)));
            FailIfAny(failures);
        }

        TEST_METHOD (AMalformedSeenEntryIsSkippedAlone)
        {
            using winrt::Windows::Data::Json::JsonObject;
            const std::wstring good = L"{ \"monitors\": [ \"A\", \"B\" ], \"last_seen_utc\": \"2026-10-01T08:12:00Z\" }";
            const std::wstring bad[] = {
                L"7",
                L"{ \"last_seen_utc\": \"2026-10-01T08:12:00Z\" }",
                L"{ \"monitors\": [ \"A\" ], \"last_seen_utc\": \"2026-10-01T08:12:00Z\" }",
                L"{ \"monitors\": [ \"A\", 4 ], \"last_seen_utc\": \"2026-10-01T08:12:00Z\" }",
                L"{ \"monitors\": [ \"A\", \"B\" ] }",
                L"{ \"monitors\": [ \"A\", \"B\" ], \"last_seen_utc\": 1790842320 }",
                L"{ \"monitors\": [ \"A\", \"B\" ], \"last_seen_utc\": \"yesterday\" }",
            };
            std::wstring failures;
            for (const std::wstring& entry : bad)
            {
                const JsonObject file = JsonObject::Parse(L"{ \"seen\": [ " + good + L", " + entry + L" ] }");
                Check(failures, entry, { { L"A", L"B", 1790842320 } }, ReadSeen(file));
            }
            Check(failures, L"no seen list", {}, ReadSeen(JsonObject::Parse(L"{ \"version\": 1 }")));
            Check(failures, L"seen not a list", {}, ReadSeen(JsonObject::Parse(L"{ \"seen\": {} }")));
            FailIfAny(failures);
        }

        TEST_METHOD (ATimeIsReadUntil2100)
        {
            // A time ahead of the clock is read, because the clock can be the one that is wrong. A
            // hand-edited time far ahead would stop the record from aging, so 2100 bounds it.
            using winrt::Windows::Data::Json::JsonObject;
            const int64_t lastSecond = *ParseUtc(L"2099-12-31T23:59:59Z");
            const JsonObject file = BuildDetectedEdges({}, { { L"A", L"B", OCTOBER_FIRST + 400 * DAY }, { L"A", L"C", lastSecond }, { L"A", L"D", lastSecond + 1 } });
            file.GetNamedArray(L"seen").Append(JsonObject::Parse(L"{ \"monitors\": [ \"A\", \"E\" ], \"last_seen_utc\": \"9999-12-31T00:00:00Z\" }"));
            std::wstring failures;
            Check(failures, L"ahead", { { L"A", L"B", OCTOBER_FIRST + 400 * DAY }, { L"A", L"C", lastSecond } }, ReadSeen(file));
            FailIfAny(failures);
            Assert::AreEqual(lastSecond + 1, SEEN_TIME_LIMIT, L"The limit is 2100-01-01T00:00:00Z.");
        }

        TEST_METHOD (AClockThatRunsBehindLosesNoPair)
        {
            // The clock reads 2009 at logon, as after a CMOS reset, and time
            // sync brings back the right date before the next publish. An entry names A and C, an
            // office monitor last seen next to A ten days ago, and a new entry names A and D.
            using winrt::Windows::Data::Json::JsonObject;
            const int64_t resetClock = *ParseUtc(L"2009-01-01T00:00:00Z");
            const std::vector<std::pair<std::wstring, std::wstring>> present{ { L"A", L"B" } };
            const std::vector<std::pair<std::wstring, std::wstring>> named{ { L"A", L"C" }, { L"A", L"D" } };
            const JsonObject start = BuildDetectedEdges({}, { { L"A", L"B", OCTOBER_FIRST }, { L"A", L"C", OCTOBER_FIRST - 10 * DAY } });

            const JsonObject atReset = Publish(start, present, named, resetClock);
            const JsonObject afterSync = Publish(atReset, present, named, OCTOBER_FIRST + DAY);

            std::wstring failures;
            Check(failures, L"the publish at 2009", { { L"A", L"B", OCTOBER_FIRST }, { L"A", L"C", OCTOBER_FIRST - 10 * DAY }, { L"A", L"D", OCTOBER_FIRST } }, SeenInFile(atReset));
            Check(failures, L"the publish after time sync", { { L"A", L"B", OCTOBER_FIRST + DAY }, { L"A", L"C", OCTOBER_FIRST - 10 * DAY }, { L"A", L"D", OCTOBER_FIRST } }, SeenInFile(afterSync));
            FailIfAny(failures);
            Assert::IsTrue(ReadExpired(afterSync).empty(), L"No pair expires, so Settings prunes no entry.");
        }

        TEST_METHOD (AFileCrowdedPastTheReadLimitStillAgesANamedPair)
        {
            // A file with 1024 recent filler pairs ahead of an aged named pair. The module
            // never reads the aged record, so the pair starts at the first publish. From then on
            // it keeps that time and expires on schedule, and the file stays within the limit.
            using winrt::Windows::Data::Json::JsonObject;
            std::vector<SeenPair> crowded;
            for (int i = 0; i < 1024; ++i)
            {
                crowded.push_back(SeenPair{ L"F" + std::to_wstring(i), L"G", OCTOBER_FIRST });
            }
            crowded.push_back(SeenPair{ L"A", L"C", OCTOBER_FIRST - 200 * DAY });
            const std::vector<std::pair<std::wstring, std::wstring>> present{ { L"A", L"B" } };
            const std::vector<std::pair<std::wstring, std::wstring>> named{ { L"A", L"C" } };

            const JsonObject first = Publish(BuildDetectedEdges({}, crowded), present, named, OCTOBER_FIRST);
            const JsonObject second = Publish(first, present, named, OCTOBER_FIRST + 60);
            const JsonObject later = Publish(second, present, named, OCTOBER_FIRST + 181 * DAY);

            Assert::IsTrue(SeenInFile(first).size() <= MAX_SEEN_ENTRIES, L"The module writes at most the entries it reads.");
            Assert::IsTrue(StampOf(SeenInFile(second), L"A", L"C") == OCTOBER_FIRST, L"A minute later the pair keeps its time.");
            Assert::IsFalse(StampOf(SeenInFile(later), L"A", L"C").has_value(), L"181 days later the pair leaves seen.");
            Assert::IsTrue(ReadExpired(later) == named, L"181 days later the pair is expired.");
        }

        TEST_METHOD (ASeenIdPast512CharactersIsSkipped)
        {
            const std::wstring longest(MAX_MONITOR_ID_LENGTH, L'A');
            const std::wstring tooLong(MAX_MONITOR_ID_LENGTH + 1, L'A');
            std::wstring failures;
            Check(failures, L"ID length", { { longest, L"B", OCTOBER_FIRST } }, ReadSeen(BuildDetectedEdges({}, { { longest, L"B", OCTOBER_FIRST }, { tooLong, L"B", OCTOBER_FIRST }, { L"B", tooLong, OCTOBER_FIRST } })));
            FailIfAny(failures);
        }

        TEST_METHOD (TheExpiredPairsReadBack)
        {
            using winrt::Windows::Data::Json::JsonObject;
            const std::vector<std::pair<std::wstring, std::wstring>> expired{ { HP, LENOVO }, { DELL, L"\\\\?\\DISPLAY#LGD0A1B#5&1a2b3c4d&0&UID4355" } };
            const JsonObject file = BuildDetectedEdges({}, {}, expired);
            Assert::IsTrue(expired == ReadExpired(file), L"Round trip.");

            const JsonObject edited = JsonObject::Parse(L"{ \"expired\": [ 7, { \"monitors\": [ \"A\" ] }, { \"monitors\": [ \"A\", 4 ] }, { \"monitors\": [ \"A\", \"B\" ] } ] }");
            const std::vector<std::pair<std::wstring, std::wstring>> only{ { L"A", L"B" } };
            Assert::IsTrue(only == ReadExpired(edited), L"A malformed entry is skipped alone.");
            Assert::IsTrue(ReadExpired(JsonObject::Parse(L"{ \"version\": 1 }")).empty(), L"A file without the list has none.");
        }

        TEST_METHOD (OnlyTheFirstThousandSeenEntriesAreRead)
        {
            winrt::Windows::Data::Json::JsonArray list;
            for (int i = 0; i < 1100; ++i)
            {
                list.Append(winrt::Windows::Data::Json::JsonObject::Parse(L"{ \"monitors\": [ \"A" + std::to_wstring(i) + L"\", \"B\" ], \"last_seen_utc\": \"2026-10-01T08:12:00Z\" }"));
            }
            winrt::Windows::Data::Json::JsonObject file;
            file.SetNamedValue(L"seen", list);
            Assert::AreEqual(MAX_SEEN_ENTRIES, ReadSeen(file).size());
            Assert::AreEqual(static_cast<size_t>(1024), MAX_SEEN_ENTRIES);
        }
    };

    TEST_CLASS (OverlayHelperTests)
    {
    public:
        TEST_METHOD (MonitorsTakeWindowsNumbersFromEveryAvailableTarget)
        {
            // Each case: each monitor's target, or none, and every available target. The layout:
            // a Hyper-V monitor on adapter 0 and three indirect targets on adapter 1, of which
            // target 1 is detached. Windows Settings shows 1, 2, 4 for the active ones.
            using Target = DisplayTarget;
            struct NumberCase
            {
                const wchar_t* name;
                std::vector<std::optional<Target>> monitors;
                std::vector<Target> available;
                std::vector<int> expected;
            };
            const std::vector<NumberCase> cases = {
                { L"a detached target keeps its number", { Target{ 0, 0 }, Target{ 1, 0 }, Target{ 1, 2 } }, { { 0, 0 }, { 1, 0 }, { 1, 1 }, { 1, 2 } }, { 1, 2, 4 } },
                { L"adapter first, then target id, whatever the listing order", { Target{ 1, 2 }, Target{ 0, 5 }, Target{ 1, 0 } }, { { 1, 2 }, { 1, 0 }, { 0, 5 } }, { 3, 1, 2 } },
                { L"repeated targets count once", { Target{ 0, 0 }, Target{ 0, 1 } }, { { 0, 1 }, { 0, 0 }, { 0, 1 }, { 0, 0 } }, { 1, 2 } },
                { L"a monitor without a target numbers after every target, in monitor order", { std::nullopt, Target{ 0, 0 }, std::nullopt }, { { 0, 0 }, { 0, 1 } }, { 3, 1, 4 } },
                { L"a target that is not available numbers after them", { Target{ 2, 0 } }, { { 0, 0 } }, { 2 } },
                { L"no monitors", {}, { { 0, 0 } }, {} },
            };
            for (const NumberCase& c : cases)
            {
                Assert::IsTrue(c.expected == DisplayNumbers(c.monitors, c.available), c.name);
            }
        }
        TEST_METHOD (OnlyEmbeddedOutputsAreBuiltIn)
        {
            // DISPLAYCONFIG_VIDEO_OUTPUT_TECHNOLOGY values, as wingdi.h defines them.
            struct OutputCase
            {
                const wchar_t* name;
                long technology;
                bool expected;
            };
            const std::vector<OutputCase> cases = {
                { L"internal", static_cast<long>(0x80000000), true },
                { L"embedded DisplayPort", 11, true },
                { L"embedded UDI", 13, true },
                { L"HDMI", 5, false },
                { L"external DisplayPort", 10, false },
                { L"external UDI", 12, false },
                { L"indirect wired", 16, false },
                { L"other", -1, false },
                { L"HD15", 0, false },
            };
            for (const OutputCase& c : cases)
            {
                Assert::AreEqual(c.expected, IsBuiltInOutput(c.technology), c.name);
            }
        }

        TEST_METHOD (TrappedDisplaysCanBeEnteredButNotLeft)
        {
            // HP (0) | DELL (1), LENOVO (2) above both.
            const Layout base{ { Mon(0, 0, 1920, 1080), Mon(1920, 0, 4480, 1440), Mon(0, -2160, 3840, 0) } };
            struct TrapCase
            {
                const wchar_t* name;
                std::vector<EdgeSetting> edges;
                EdgeFilter filter;
                std::vector<size_t> expected;
            };

            const TrapCase cases[] = {
                { L"no entries", {}, EdgeFilter::All, {} },
                { L"every way out of LENOVO walled", { { 2, 0, true, EdgeMode::Wall, 300 }, { 2, 1, true, EdgeMode::Wall, 300 } }, EdgeFilter::All, { 2 } },
                { L"walled both ways", { { 2, 0, false, EdgeMode::Wall, 300 }, { 2, 1, false, EdgeMode::Wall, 300 } }, EdgeFilter::All, {} },
                { L"one way out open", { { 2, 0, true, EdgeMode::Wall, 300 } }, EdgeFilter::All, {} },
                { L"the only way in is Off by the filter", { { 2, 0, true, EdgeMode::Wall, 300 }, { 2, 1, true, EdgeMode::Wall, 300 }, { 0, 2, true, EdgeMode::Wall, 300 } }, EdgeFilter::SideBySide, { 2 } },
            };

            std::wstring failures;
            for (const TrapCase& c : cases)
            {
                Layout layout = base;
                layout.edges = c.edges;
                Settings settings;
                settings.edgeFilter = c.filter;
                if (TrappedMonitors(layout, settings) != c.expected)
                {
                    failures += (failures.empty() ? L"" : L"; ") + std::wstring(c.name);
                }
            }
            FailIfAny(failures);
        }

        TEST_METHOD (TrappedDisplaysCountTheWaysThroughACorner)
        {
            // A (0), B (1) on A's right, C (2) meeting A only at A's bottom-right corner, and D (3)
            // below A. A move through the corner takes the stricter of the two sides it passes, so
            // a corner is a way out unless a wall borders it on one side.
            const auto corner = [](bool shortB, bool withD) {
                Layout layout{ { Mon(0, 0, 1920, 1080), Mon(1920, 0, 3840, shortB ? 800 : 1080), Mon(1920, 1080, 3840, 2160) } };
                if (withD)
                {
                    layout.monitors.push_back(Mon(0, 1080, 1920, 2160));
                }
                return layout;
            };
            struct TrapCase
            {
                const wchar_t* name;
                Layout layout;
                std::vector<EdgeSetting> edges;
                std::vector<size_t> expected;
            };

            const TrapCase cases[] = {
                { L"the wall to B also closes the corner", corner(false, false), { { 0, 1, true, EdgeMode::Wall, 300 } }, { 0 } },
                { L"B ends short of the corner, which stays open", corner(true, false), { { 0, 1, true, EdgeMode::Wall, 300 } }, {} },
                { L"walled both ways on every shared edge, entered through the corner", corner(true, true), { { 0, 1, false, EdgeMode::Wall, 300 }, { 0, 3, false, EdgeMode::Wall, 300 } }, { 0 } },
                { L"the way in through the corner is walled too, which traps C instead", corner(true, true), { { 0, 1, false, EdgeMode::Wall, 300 }, { 0, 3, false, EdgeMode::Wall, 300 }, { 2, 3, true, EdgeMode::Wall, 300 } }, { 2 } },
                { L"a monitor reached only through a corner", Layout{ { Mon(0, 0, 1920, 1080), Mon(1920, 1080, 3840, 2160) } }, {}, {} },
            };

            std::wstring failures;
            for (const TrapCase& c : cases)
            {
                Layout layout = c.layout;
                layout.edges = c.edges;
                const std::vector<size_t> actual = TrappedMonitors(layout, Settings{});
                if (actual != c.expected)
                {
                    std::wstring got;
                    for (size_t index : actual)
                    {
                        got += (got.empty() ? L"" : L", ") + std::to_wstring(index);
                    }
                    failures += (failures.empty() ? L"" : L"; ") + std::wstring(c.name) + L": got [" + got + L"]";
                }
            }
            FailIfAny(failures);
        }

        TEST_METHOD (AMonitorKeepsItsIdentityWhileItsDeviceAndRectStay)
        {
            // While the identities of a new layout are read off the hook thread, a monitor that kept
            // its GDI device and its rect keeps what was read for it, so its walls hold meanwhile.
            const std::vector<std::wstring> before = { L"\\\\.\\DISPLAY1", L"\\\\.\\DISPLAY2" };
            const std::vector<RectL> beforeRects = { RectL{ 0, 0, 1920, 1080 }, RectL{ 1920, 0, 3840, 1080 } };
            struct MatchCase
            {
                const wchar_t* name;
                std::vector<std::wstring> devices;
                std::vector<RectL> rects;
                std::vector<std::optional<size_t>> expected;
            };
            const MatchCase cases[] = {
                { L"unchanged, listed in another order", { L"\\\\.\\DISPLAY2", L"\\\\.\\display1" }, { RectL{ 1920, 0, 3840, 1080 }, RectL{ 0, 0, 1920, 1080 } }, { 1, 0 } },
                { L"a monitor moved", { L"\\\\.\\DISPLAY1", L"\\\\.\\DISPLAY2" }, { RectL{ 0, 0, 1920, 1080 }, RectL{ 1920, 540, 3840, 1620 } }, { 0, std::nullopt } },
                { L"a new device in the old place", { L"\\\\.\\DISPLAY1", L"\\\\.\\DISPLAY3" }, { RectL{ 0, 0, 1920, 1080 }, RectL{ 1920, 0, 3840, 1080 } }, { 0, std::nullopt } },
                { L"a monitor added", { L"\\\\.\\DISPLAY1", L"\\\\.\\DISPLAY2", L"\\\\.\\DISPLAY3" }, { RectL{ 0, 0, 1920, 1080 }, RectL{ 1920, 0, 3840, 1080 }, RectL{ 0, -1080, 1920, 0 } }, { 0, 1, std::nullopt } },
                { L"no previous layout", { L"\\\\.\\DISPLAY1" }, { RectL{ 0, 0, 1920, 1080 } }, { std::nullopt } },
            };
            for (const MatchCase& c : cases)
            {
                const bool fresh = std::wstring(c.name) == L"no previous layout";
                Assert::IsTrue(c.expected == MatchPrevious(c.devices, c.rects, fresh ? std::vector<std::wstring>{} : before, fresh ? std::vector<RectL>{} : beforeRects), c.name);
            }
        }

        TEST_METHOD (AnIdentityReadAppliesOnlyToTheLayoutItWasTakenFor)
        {
            // The layout of generation 7: two monitors side by side.
            const std::vector<std::wstring> devices = { L"\\\\.\\DISPLAY1", L"\\\\.\\DISPLAY2" };
            const std::vector<RectL> rects = { RectL{ 0, 0, 1920, 1080 }, RectL{ 1920, 0, 3840, 1080 } };
            const auto read = [&](uint64_t generation, std::vector<std::wstring> seenDevices, std::vector<RectL> seenRects) {
                Identities identities;
                identities.generation = generation;
                for (size_t i = 0; i < seenDevices.size(); ++i)
                {
                    identities.ids.push_back(i == 0 ? HP : DELL);
                    identities.names.push_back(L"");
                    identities.builtIn.push_back(false);
                    identities.numbers.push_back(static_cast<int>(i) + 1);
                }
                identities.devices = std::move(seenDevices);
                identities.rects = std::move(seenRects);
                return identities;
            };
            Identities failed;
            failed.generation = 7;
            failed.complete = false;
            Identities missingAnId = read(7, devices, rects);
            missingAnId.ids.pop_back();

            struct ReadCase
            {
                const wchar_t* name;
                Identities identities;
                IdentityReadUse expected;
            };
            const ReadCase cases[] = {
                { L"the layout it was taken for", read(7, devices, rects), IdentityReadUse::Apply },
                { L"device names in another case", read(7, { L"\\\\.\\display1", L"\\\\.\\Display2" }, rects), IdentityReadUse::Apply },
                { L"an older layout", read(6, devices, rects), IdentityReadUse::Drop },
                { L"a layout a display change replaced before the rebuild", read(8, devices, rects), IdentityReadUse::Drop },
                { L"a read that failed", failed, IdentityReadUse::Retry },
                { L"a monitor that moved while the read ran", read(7, devices, { rects[0], RectL{ 1920, 540, 3840, 1620 } }), IdentityReadUse::Retry },
                { L"a handle that now shows another device", read(7, { devices[0], L"\\\\.\\DISPLAY3" }, rects), IdentityReadUse::Retry },
                { L"a handle that now shows no monitor", read(7, { devices[0], L"" }, { rects[0], RectL{} }), IdentityReadUse::Retry },
                { L"fewer monitors than the layout", read(7, { devices[0] }, { rects[0] }), IdentityReadUse::Retry },
                { L"fewer IDs than monitors", missingAnId, IdentityReadUse::Retry },
            };
            std::wstring failures;
            for (const ReadCase& c : cases)
            {
                if (JudgeIdentities(c.identities, 7, devices, rects) != c.expected)
                {
                    failures += (failures.empty() ? L"" : L"; ") + std::wstring(c.name);
                }
            }
            FailIfAny(failures);
        }

        TEST_METHOD (OnlySettingsShowsTheOverlayAndAtMostOnceASecond)
        {
            ShowGate gate;
            Assert::IsFalse(gate.Accept(10'000), L"A message no custom action asked for is ignored.");
            gate.Request();
            Assert::IsTrue(gate.Accept(10'000), L"Settings' request shows the overlay.");
            gate.Request();
            Assert::IsFalse(gate.Accept(10'999), L"A second request within a second is ignored.");
            gate.Request();
            Assert::IsTrue(gate.Accept(11'000), L"A request a second later shows it again.");
            Assert::IsFalse(gate.Accept(20'000), L"Each request shows the overlay once.");
        }

        TEST_METHOD (AStripLiesOnTheSideOfTheDisplayItLeaves)
        {
            // A | B side by side at x = 1920 sharing y 0..1080; C below A sharing x 0..1920 at y = 1080.
            const RectL a{ 0, 0, 1920, 1080 };
            const RectL b{ 1920, 0, 3840, 1080 };
            const RectL c{ 0, 1080, 1920, 2160 };
            const InnerEdge ab{ 0, 1, true, 1920, 0, 1080 };
            const InnerEdge ac{ 0, 2, false, 1080, 0, 1920 };
            Assert::IsTrue(RectL{ 1908, 0, 1920, 1080 } == DirectionStrip(a, ab, 12), L"Leaving A to the right.");
            Assert::IsTrue(RectL{ 1920, 0, 1932, 1080 } == DirectionStrip(b, ab, 12), L"Leaving B to the left.");
            Assert::IsTrue(RectL{ 0, 1068, 1920, 1080 } == DirectionStrip(a, ac, 12), L"Leaving A downward.");
            Assert::IsTrue(RectL{ 0, 1080, 1920, 1092 } == DirectionStrip(c, ac, 12), L"Leaving C upward.");
            Assert::IsTrue(RectL{ 1920, 540, 1930, 1080 } == DirectionStrip(RectL{ 1920, 540, 3840, 1620 }, InnerEdge{ 0, 1, true, 1920, 540, 1080 }, 10), L"Only the shared span.");
        }
    };
}
