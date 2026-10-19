// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#include "pch.h"
#include "CursorBrakeFullScreen.h"
#include "FullScreenSettings.h"

#include <optional>
#include <string>
#include <vector>

using namespace Microsoft::VisualStudio::CppUnitTestFramework;
using namespace cursorbrake;

namespace
{
    // A 1920x1080 monitor at the origin, and its right-hand neighbor.
    constexpr RectL MONITOR{ 0, 0, 1920, 1080 };
    constexpr RectL RIGHT_MONITOR{ 1920, 0, 3840, 1080 };

    std::wstring Describe(const std::optional<size_t>& value)
    {
        return value ? std::to_wstring(*value) : L"none";
    }

    std::wstring Describe(const FullScreenTreatment& treatment)
    {
        return std::wstring(treatment.pass ? L"pass" : L"judge") + L", wall " + Describe(treatment.wallMonitor);
    }

    std::wstring Join(const std::vector<std::wstring>& items)
    {
        std::wstring text;
        for (const std::wstring& item : items)
        {
            text += (text.empty() ? L"[" : L", ") + item;
        }
        return text.empty() ? L"[]" : text + L"]";
    }

    void Fail(const std::wstring& failures)
    {
        if (!failures.empty())
        {
            Assert::Fail(failures.c_str());
        }
    }

    void Note(std::wstring& failures, const std::wstring& failure)
    {
        failures += (failures.empty() ? L"" : L"; ") + failure;
    }
}

namespace CursorBrakeFullScreenTests
{
    TEST_CLASS (FullScreenDetection)
    {
    public:
        TEST_METHOD (OnlyACaptionlessWindowCoveringItsMonitorIsFullScreen)
        {
            struct Case
            {
                const wchar_t* name;
                RectL window;
                RectL monitor;
                bool hasCaption;
                bool isShellOrDesktop;
                bool expected;
            };
            const std::vector<Case> cases = {
                { L"an exact cover", MONITOR, MONITOR, false, false, true },
                { L"an overhang past every side, as exclusive mode can", { -8, -8, 1928, 1088 }, MONITOR, false, false, true },
                { L"an exact cover on a monitor left of the primary", { -1920, 0, 0, 1080 }, { -1920, 0, 0, 1080 }, false, false, true },
                { L"a maximized window with a caption under an auto-hidden taskbar", MONITOR, MONITOR, true, false, false },
                { L"the shell or the desktop", MONITOR, MONITOR, false, true, false },
                { L"a window filling another monitor", RIGHT_MONITOR, MONITOR, false, false, false },
                { L"a window one pixel short at the bottom", { 0, 0, 1920, 1079 }, MONITOR, false, false, false },
                { L"a window one pixel short at the left", { 1, 0, 1920, 1080 }, MONITOR, false, false, false },
                { L"a zero rect", {}, MONITOR, false, false, false },
                { L"a zero monitor rect", {}, {}, false, false, false },
            };
            std::wstring failures;
            for (const Case& c : cases)
            {
                if (IsFullScreen(c.window, c.monitor, c.hasCaption, c.isShellOrDesktop) != c.expected)
                {
                    Note(failures, std::wstring(c.name) + L": expected " + (c.expected ? L"full screen" : L"not full screen"));
                }
            }
            Fail(failures);
        }

        TEST_METHOD (AWindowOverMoreThanOneMonitorOrAnOverlayIsNoFullScreenApp)
        {
            // Three monitors in a row, the middle one the primary.
            const Layout layout{ { Monitor{ RectL{ -1920, 0, 0, 1080 } }, Monitor{ MONITOR }, Monitor{ RIGHT_MONITOR } } };
            struct Case
            {
                const wchar_t* name;
                ForegroundWindow window;
                bool overlay;
                std::optional<size_t> expected;
            };
            const std::vector<Case> cases = {
                { L"an exact cover of the primary", { MONITOR, MONITOR }, false, 1 },
                { L"an exact cover of the right monitor", { RIGHT_MONITOR, RIGHT_MONITOR }, false, 2 },
                { L"an overhang onto part of the next monitor", { { -8, -8, 2000, 1088 }, MONITOR }, false, 1 },
                { L"one window over two monitors, named by the left one", { { 0, 0, 3840, 1080 }, MONITOR }, false, std::nullopt },
                { L"one window over two monitors, named by the right one", { { 0, 0, 3840, 1080 }, RIGHT_MONITOR }, false, std::nullopt },
                { L"one window over every monitor", { { -1920, 0, 3840, 1080 }, MONITOR }, false, std::nullopt },
                { L"a tool window, as Screen Ruler's overlays are", { MONITOR, MONITOR, false, false, true }, false, std::nullopt },
                { L"a PowerToys overlay that is no tool window, as Text Extractor's are", { MONITOR, MONITOR }, true, std::nullopt },
                { L"a captioned window", { MONITOR, MONITOR, true }, false, std::nullopt },
                { L"the shell", { MONITOR, MONITOR, false, true }, false, std::nullopt },
                { L"a monitor the layout does not hold", { { 0, 0, 1280, 720 }, { 0, 0, 1280, 720 } }, false, std::nullopt },
            };
            std::wstring failures;
            for (const Case& c : cases)
            {
                const std::optional<size_t> actual = FullScreenMonitorOf(layout, c.window, [&] { return c.overlay; });
                if (actual != c.expected)
                {
                    Note(failures, std::wstring(c.name) + L": expected " + Describe(c.expected) + L", got " + Describe(actual));
                }
            }
            Fail(failures);
        }

        TEST_METHOD (TheOverlayCheckRunsOnlyForAWindowOtherwiseFullScreen)
        {
            // The check opens the window's process, so a window the shape already rules out is
            // never looked up.
            const Layout layout{ { Monitor{ MONITOR }, Monitor{ RIGHT_MONITOR } } };
            int lookups = 0;
            const auto overlay = [&] {
                ++lookups;
                return false;
            };
            (void)FullScreenMonitorOf(layout, { { 0, 0, 1920, 1079 }, MONITOR }, overlay);
            (void)FullScreenMonitorOf(layout, { MONITOR, MONITOR, true }, overlay);
            (void)FullScreenMonitorOf(layout, { MONITOR, MONITOR, false, false, true }, overlay);
            (void)FullScreenMonitorOf(layout, { { 0, 0, 3840, 1080 }, MONITOR }, overlay);
            Assert::AreEqual(0, lookups, L"No window ruled out by its shape is looked up.");
            Assert::IsTrue(FullScreenMonitorOf(layout, { MONITOR, MONITOR }, overlay) == std::optional<size_t>{ 0 }, L"A full-screen app that is no overlay is found.");
            Assert::AreEqual(1, lookups, L"A window full screen by its shape is looked up once.");
        }

        TEST_METHOD (OnlyPowerToysOverlaysAreKnownOverlays)
        {
            struct Case
            {
                const wchar_t* path;
                bool expected;
            };
            const std::vector<Case> cases = {
                { L"C:\\PROGRAM FILES\\POWERTOYS\\POWERTOYS.MEASURETOOLUI.EXE", true },
                { L"C:\\PROGRAM FILES\\POWERTOYS\\POWERTOYS.POWEROCR.EXE", true },
                { L"C:\\PROGRAM FILES\\POWERTOYS\\WINUI3APPS\\POWERTOYS.WORKSPACESEDITOR.EXE", true },
                { L"POWERTOYS.POWEROCR.EXE", true },
                { L"C:\\GAMES\\GAME.EXE", false },
                { L"C:\\GAMES\\MY.POWERTOYS.POWEROCR.EXE", false },
                { L"C:\\POWERTOYS.POWEROCR.EXE\\GAME.EXE", false },
                { L"C:\\PROGRAM FILES\\POWERTOYS\\POWERTOYS.EXE", false },
                { L"", false },
            };
            std::wstring failures;
            for (const Case& c : cases)
            {
                if (IsPowerToysOverlay(c.path) != c.expected)
                {
                    Note(failures, std::wstring(L"[") + c.path + L"]: expected " + (c.expected ? L"an overlay" : L"no overlay"));
                }
            }
            Fail(failures);
        }

        TEST_METHOD (MonitorWithRectFindsTheExactRect)
        {
            const Layout layout{ { Monitor{ MONITOR }, Monitor{ RIGHT_MONITOR } } };
            Assert::IsTrue(MonitorWithRect(layout, RIGHT_MONITOR) == std::optional<size_t>{ 1 }, L"The right monitor is index 1.");
            Assert::IsTrue(MonitorWithRect(layout, MONITOR) == std::optional<size_t>{ 0 }, L"The primary is index 0.");
            Assert::IsFalse(MonitorWithRect(layout, RectL{ 0, 0, 1920, 1079 }).has_value(), L"A rect matching no monitor exactly finds none.");
            Assert::IsFalse(MonitorWithRect(Layout{}, MONITOR).has_value(), L"An empty layout finds none.");
        }
    };

    TEST_CLASS (FullScreenTreatments)
    {
    public:
        TEST_METHOD (EachBehaviorTreatsEachSource)
        {
            struct Case
            {
                const wchar_t* name;
                FullScreenBehavior behavior;
                std::optional<size_t> fullScreen;
                std::optional<size_t> source;
                FullScreenTreatment expected;
            };
            const std::vector<Case> cases = {
                { L"Same, leaving the full-screen monitor", FullScreenBehavior::Same, 0, 0, { false, std::nullopt } },
                { L"Same, elsewhere", FullScreenBehavior::Same, 0, 1, { false, std::nullopt } },
                { L"Wall, leaving the full-screen monitor", FullScreenBehavior::Wall, 0, 0, { false, 0 } },
                { L"Wall, elsewhere: the engine walls only the way out of 0", FullScreenBehavior::Wall, 0, 1, { false, 0 } },
                { L"Wall, no full-screen app", FullScreenBehavior::Wall, std::nullopt, 0, { false, std::nullopt } },
                { L"No resistance, leaving the full-screen monitor", FullScreenBehavior::NoResistance, 0, 0, { true, std::nullopt } },
                { L"No resistance, elsewhere", FullScreenBehavior::NoResistance, 0, 1, { false, std::nullopt } },
                { L"No resistance, the source on no monitor", FullScreenBehavior::NoResistance, 0, std::nullopt, { false, std::nullopt } },
                { L"No resistance, no full-screen app", FullScreenBehavior::NoResistance, std::nullopt, 0, { false, std::nullopt } },
                { L"No resistance, neither known", FullScreenBehavior::NoResistance, std::nullopt, std::nullopt, { false, std::nullopt } },
            };
            std::wstring failures;
            for (const Case& c : cases)
            {
                const FullScreenTreatment actual = TreatFullScreen(c.behavior, c.fullScreen, c.source);
                if (!(actual == c.expected))
                {
                    Note(failures, std::wstring(c.name) + L": expected " + Describe(c.expected) + L", got " + Describe(actual));
                }
            }
            Fail(failures);
        }
    };

    TEST_CLASS (FullScreenTracking)
    {
    public:
        TEST_METHOD (OnlyACrossingMoveLooksAgain)
        {
            FullScreenTracker tracker;
            int probes = 0;
            std::optional<size_t> now = 1;
            const auto probe = [&] {
                ++probes;
                return now;
            };

            Assert::IsFalse(tracker.Observe(false, probe).has_value(), L"Nothing is known before the first crossing.");
            Assert::AreEqual(0, probes, L"A move inside a monitor does not look.");
            Assert::IsTrue(tracker.Observe(true, probe) == std::optional<size_t>{ 1 }, L"A crossing move looks.");
            Assert::AreEqual(1, probes);

            now = std::nullopt;
            for (int i = 0; i < 50; ++i)
            {
                Assert::IsTrue(tracker.Observe(false, probe) == std::optional<size_t>{ 1 }, L"Moves between crossings keep the last answer.");
            }
            Assert::AreEqual(1, probes, L"No move between crossings looks.");
            Assert::IsFalse(tracker.Observe(true, probe).has_value(), L"The next crossing sees the app gone.");
            Assert::AreEqual(2, probes);

            now = 0;
            (void)tracker.Observe(true, probe);
            tracker.Reset();
            Assert::IsFalse(tracker.Observe(false, probe).has_value(), L"A reset forgets the monitor.");
        }
    };

    TEST_CLASS (ForegroundCaching)
    {
    public:
        TEST_METHOD (TheCacheFollowsTheForegroundAndKeepsItsAnswerWithoutOne)
        {
            ForegroundCache cache;
            int judged = 0;
            const auto excluded = [&](uintptr_t) {
                ++judged;
                return true;
            };
            const auto included = [&](uintptr_t) {
                ++judged;
                return false;
            };

            Assert::IsFalse(cache.Excluded(), L"Nothing is excluded before the first adoption.");
            cache.Adopt(0x10, ForegroundReport::Query, true, excluded);
            Assert::IsTrue(cache.Excluded(), L"An excluded app in front at start is adopted.");
            cache.Adopt(0, ForegroundReport::Event, true, included);
            Assert::IsTrue(cache.Excluded(), L"A foreground event naming no window keeps the last answer.");
            Assert::AreEqual(1, judged, L"No window is not judged.");
            cache.Adopt(0x20, ForegroundReport::Event, true, included);
            Assert::IsFalse(cache.Excluded(), L"A foreground change updates the answer.");
            cache.Adopt(0x20, ForegroundReport::Query, true, excluded);
            Assert::IsTrue(cache.Excluded(), L"The same window is judged again, as after a settings change.");
            Assert::AreEqual(3, judged);
        }

        TEST_METHOD (NoWindowInFrontOnAQueryIsNotExcluded)
        {
            // A settings apply asks Windows for the foreground. No window then is no switch in
            // progress, so nothing excluded is in front.
            ForegroundCache cache;
            const auto excluded = [](uintptr_t) { return true; };
            cache.Adopt(0x10, ForegroundReport::Event, true, excluded);
            cache.Adopt(0, ForegroundReport::Query, true, excluded);
            Assert::IsFalse(cache.Excluded(), L"A query that finds no window clears the answer.");
        }

        TEST_METHOD (AnEmptyListExcludesNothingWhateverIsInFront)
        {
            ForegroundCache cache;
            int judged = 0;
            const auto excluded = [&](uintptr_t) {
                ++judged;
                return true;
            };
            struct Case
            {
                const wchar_t* name;
                uintptr_t window;
                ForegroundReport report;
            };
            const std::vector<Case> cases = {
                { L"a window, from an event", 0x10, ForegroundReport::Event },
                { L"no window, from an event", 0, ForegroundReport::Event },
                { L"a window, from a query", 0x10, ForegroundReport::Query },
                { L"no window, from a query", 0, ForegroundReport::Query },
            };
            std::wstring failures;
            for (const Case& c : cases)
            {
                cache.Adopt(0x20, ForegroundReport::Event, true, excluded);
                cache.Adopt(c.window, c.report, false, excluded);
                if (cache.Excluded())
                {
                    Note(failures, std::wstring(c.name) + L": an emptied list left the app excluded");
                }
            }
            Fail(failures);
            Assert::AreEqual(4, judged, L"An empty list judges no window, so no process is opened.");
        }
    };

    TEST_CLASS (FullScreenSettingsParse)
    {
    public:
        TEST_METHOD (OnlyAWholeChoiceFromZeroToTwoIsRead)
        {
            using winrt::Windows::Data::Json::JsonObject;
            struct Case
            {
                const wchar_t* json;
                std::optional<FullScreenBehavior> expected;
            };
            const std::vector<Case> cases = {
                { LR"({ "fullscreen_behavior": { "value": 0 } })", FullScreenBehavior::Same },
                { LR"({ "fullscreen_behavior": { "value": 1 } })", FullScreenBehavior::Wall },
                { LR"({ "fullscreen_behavior": { "value": 2 } })", FullScreenBehavior::NoResistance },
                { LR"({ "push_distance": { "value": 300 } })", std::nullopt },
                { LR"({ "fullscreen_behavior": { "value": 3 } })", std::nullopt },
                { LR"({ "fullscreen_behavior": { "value": -1 } })", std::nullopt },
                { LR"({ "fullscreen_behavior": { "value": 1.5 } })", std::nullopt },
                { LR"({ "fullscreen_behavior": { "value": "1" } })", std::nullopt },
                { LR"({ "fullscreen_behavior": { "value": true } })", std::nullopt },
                { LR"({ "fullscreen_behavior": { "value": null } })", std::nullopt },
                { LR"({ "fullscreen_behavior": { } })", std::nullopt },
                { LR"({ "fullscreen_behavior": 1 })", std::nullopt },
                { LR"({ "fullscreen_behavior": { "value": 1e300 } })", std::nullopt },
            };
            std::wstring failures;
            for (const Case& c : cases)
            {
                const std::optional<FullScreenBehavior> actual = ReadFullScreenBehavior(JsonObject::Parse(c.json));
                if (actual != c.expected)
                {
                    Note(failures, std::wstring(c.json) + L": expected " + (c.expected ? std::to_wstring(static_cast<int>(*c.expected)) : L"none") + L", got " + (actual ? std::to_wstring(static_cast<int>(*actual)) : L"none"));
                }
            }
            Fail(failures);
        }

        TEST_METHOD (ExcludedAppsAreOnePerLineTrimmedAndUppercased)
        {
            struct Case
            {
                const wchar_t* name;
                std::wstring text;
                std::vector<std::wstring> expected;
            };
            const std::vector<Case> cases = {
                { L"empty", L"", {} },
                { L"only blank lines and spaces", L" \r\n\t\n\n  ", {} },
                { L"LF", L"game.exe\noutlook.exe", { L"GAME.EXE", L"OUTLOOK.EXE" } },
                { L"CRLF", L"game.exe\r\noutlook.exe\r\n", { L"GAME.EXE", L"OUTLOOK.EXE" } },
                { L"a lone CR", L"game.exe\routlook.exe", { L"GAME.EXE", L"OUTLOOK.EXE" } },
                { L"blank lines between", L"\r\n\r\ngame.exe\r\n\r\n\r\noutlook.exe\n\n", { L"GAME.EXE", L"OUTLOOK.EXE" } },
                { L"surrounding whitespace", L"  game.exe \t\r\n\toutlook.exe  ", { L"GAME.EXE", L"OUTLOOK.EXE" } },
                { L"mixed case", L"Game.Exe\nOutLook", { L"GAME.EXE", L"OUTLOOK" } },
                { L"inner spaces kept", L"My Game.exe", { L"MY GAME.EXE" } },
                { L"a lone backslash, which would match every path", L"\\", {} },
                { L"separators only", L"\\\\\n/\n \\/ \ngame.exe", { L"GAME.EXE" } },
                { L"a leading backslash kept", L"\\game.exe", { L"\\GAME.EXE" } },
            };
            std::wstring failures;
            for (const Case& c : cases)
            {
                const std::vector<std::wstring> actual = ParseExcludedApps(c.text);
                if (actual != c.expected)
                {
                    Note(failures, std::wstring(c.name) + L": expected " + Join(c.expected) + L", got " + Join(actual));
                }
            }
            Fail(failures);
        }

        TEST_METHOD (ExcludedAppsAreReadOnlyFromAString)
        {
            using winrt::Windows::Data::Json::JsonObject;
            const std::optional<std::vector<std::wstring>> present = ReadExcludedApps(JsonObject::Parse(LR"({ "excluded_apps": { "value": "Game.exe\r\n\r\n outlook.exe " } })"));
            Assert::IsTrue(present == std::optional<std::vector<std::wstring>>{ { L"GAME.EXE", L"OUTLOOK.EXE" } }, L"A string value is parsed.");
            Assert::IsTrue(ReadExcludedApps(JsonObject::Parse(LR"({ "excluded_apps": { "value": "" } })")) == std::optional<std::vector<std::wstring>>{ std::vector<std::wstring>{} }, L"An empty string clears the list.");
            Assert::IsFalse(ReadExcludedApps(JsonObject::Parse(LR"({ "push_distance": { "value": 300 } })")).has_value(), L"A missing property keeps the list.");
            Assert::IsFalse(ReadExcludedApps(JsonObject::Parse(LR"({ "excluded_apps": { "value": 5 } })")).has_value(), L"A number keeps the list.");
            Assert::IsFalse(ReadExcludedApps(JsonObject::Parse(LR"({ "excluded_apps": { "value": [ "game.exe" ] } })")).has_value(), L"A list keeps the list.");
            Assert::IsFalse(ReadExcludedApps(JsonObject::Parse(LR"({ "excluded_apps": "game.exe" })")).has_value(), L"A bare string keeps the list.");
        }
    };
}
