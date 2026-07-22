#pragma warning(push)
#pragma warning(disable : 26466)
#include "CppUnitTest.h"
#pragma warning(pop)

#include "..\AltWindowCycleLogic.h"

using namespace Microsoft::VisualStudio::CppUnitTestFramework;

namespace AltWindowCycleUnitTests
{
    namespace
    {
        void AssertRectEqual(const RECT& expected, const RECT& actual)
        {
            Assert::AreEqual(expected.left, actual.left, L"left");
            Assert::AreEqual(expected.top, actual.top, L"top");
            Assert::AreEqual(expected.right, actual.right, L"right");
            Assert::AreEqual(expected.bottom, actual.bottom, L"bottom");
        }

        AltWindowCycleLogic::CandidateWindow MakeCandidate(
            unsigned long long id,
            const std::wstring& processKey,
            bool visible = true,
            bool cloaked = false,
            bool representative = true,
            bool toolWindow = false)
        {
            AltWindowCycleLogic::CandidateWindow candidate;
            candidate.id = id;
            candidate.processKey = processKey;
            candidate.eligibility.isVisible = visible;
            candidate.eligibility.isCloaked = cloaked;
            candidate.eligibility.isAltTabRepresentative = representative;
            candidate.eligibility.isToolWindow = toolWindow;
            return candidate;
        }

        void AssertIds(std::initializer_list<unsigned long long> expected, const std::vector<unsigned long long>& actual)
        {
            Assert::AreEqual(static_cast<int>(expected.size()), static_cast<int>(actual.size()), L"cycle set size");
            size_t i = 0;
            for (const unsigned long long id : expected)
            {
                Assert::IsTrue(id == actual[i], L"cycle set id/order mismatch");
                ++i;
            }
        }
    }

    TEST_CLASS(AltWindowCycleLogicTests)
    {
    public:
        TEST_METHOD(ComputeOverlayLayoutUsesExpectedUnscaledGeometry)
        {
            const RECT work = { 0, 0, 1920, 1080 };

            const auto layout = AltWindowCycleLogic::ComputeOverlayLayout(work, 4, 1.0);

            Assert::AreEqual(32, layout.pad);
            Assert::AreEqual(26, layout.gap);
            Assert::AreEqual(270, layout.tileW);
            Assert::AreEqual(48, layout.headerH);
            Assert::AreEqual(142, layout.previewH);
            Assert::AreEqual(6, layout.inner);
            Assert::AreEqual(10, layout.radius);
            Assert::AreEqual(16, layout.iconSize);
            Assert::AreEqual(202, layout.tileH);
            Assert::AreEqual(4, layout.cols);
            Assert::AreEqual(1, layout.rows);
            Assert::AreEqual(1222, layout.panelW);
            Assert::AreEqual(266, layout.panelH);
            Assert::AreEqual(349, layout.panelX);
            Assert::AreEqual(407, layout.panelY);
        }

        TEST_METHOD(ComputeOverlayLayoutUsesRoundedScaledGeometry)
        {
            const RECT work = { 0, 0, 1920, 1080 };

            const auto layout = AltWindowCycleLogic::ComputeOverlayLayout(work, 6, 1.5);

            Assert::AreEqual(48, layout.pad);
            Assert::AreEqual(39, layout.gap);
            Assert::AreEqual(405, layout.tileW);
            Assert::AreEqual(72, layout.headerH);
            Assert::AreEqual(213, layout.previewH);
            Assert::AreEqual(9, layout.inner);
            Assert::AreEqual(15, layout.radius);
            Assert::AreEqual(24, layout.iconSize);
            Assert::AreEqual(303, layout.tileH);
            Assert::AreEqual(4, layout.cols);
            Assert::AreEqual(2, layout.rows);
            Assert::AreEqual(1833, layout.panelW);
            Assert::AreEqual(741, layout.panelH);
            Assert::AreEqual(43, layout.panelX);
            Assert::AreEqual(169, layout.panelY);
        }

        TEST_METHOD(ComputeOverlayLayoutHandlesEmptyWindowCount)
        {
            const RECT work = { 10, 20, 810, 620 };

            const auto layout = AltWindowCycleLogic::ComputeOverlayLayout(work, 0, 1.0);

            Assert::AreEqual(1, layout.cols);
            Assert::AreEqual(0, layout.rows);
        }

        TEST_METHOD(TilePreviewAndHeaderRectsUsePanelRelativeInsetViewport)
        {
            const RECT work = { 0, 0, 1920, 1080 };
            const auto layout = AltWindowCycleLogic::ComputeOverlayLayout(work, 4, 1.0);

            const RECT tile = AltWindowCycleLogic::TileRect(layout, 0);
            AssertRectEqual({ 32, 32, 302, 234 }, tile);

            const RECT preview = AltWindowCycleLogic::PreviewRect(layout, tile);
            AssertRectEqual({ 33, 80, 301, 233 }, preview);

            const RECT header = AltWindowCycleLogic::HeaderRect(layout, tile);
            AssertRectEqual({ 44, 32, 290, 80 }, header);
        }

        TEST_METHOD(CoverSourceCropsWideSourceToDestinationAspectRatio)
        {
            const RECT dest = { 0, 0, 100, 100 };
            const RECT avail = { 0, 0, 400, 200 };

            const RECT source = AltWindowCycleLogic::CoverSource(dest, avail);

            AssertRectEqual({ 100, 0, 300, 200 }, source);
        }

        TEST_METHOD(CoverSourceCropsTallSourceToDestinationAspectRatio)
        {
            const RECT dest = { 0, 0, 100, 100 };
            const RECT avail = { 0, 0, 200, 400 };

            const RECT source = AltWindowCycleLogic::CoverSource(dest, avail);

            AssertRectEqual({ 0, 100, 200, 300 }, source);
        }

        TEST_METHOD(CoverSourceReturnsAvailableRegionForInvalidInputs)
        {
            const RECT dest = { 0, 0, 0, 100 };
            const RECT avail = { 1, 2, 3, 4 };

            const RECT source = AltWindowCycleLogic::CoverSource(dest, avail);

            AssertRectEqual(avail, source);
        }

        TEST_METHOD(WrapIndexWrapsForwardAndBackward)
        {
            Assert::AreEqual(0, AltWindowCycleLogic::WrapIndex(3, 3));
            Assert::AreEqual(2, AltWindowCycleLogic::WrapIndex(-1, 3));
            Assert::AreEqual(1, AltWindowCycleLogic::WrapIndex(4, 3));
            Assert::AreEqual(0, AltWindowCycleLogic::WrapIndex(4, 0));
        }

        TEST_METHOD(StableHoldModifiersPreferNonShiftModifiers)
        {
            Assert::AreEqual(AltWindowCycleLogic::ModifierAlt, AltWindowCycleLogic::StableHoldModifiers(AltWindowCycleLogic::ModifierAlt | AltWindowCycleLogic::ModifierShift));
            Assert::AreEqual(AltWindowCycleLogic::ModifierCtrl, AltWindowCycleLogic::StableHoldModifiers(AltWindowCycleLogic::ModifierCtrl | AltWindowCycleLogic::ModifierShift));
            Assert::AreEqual(AltWindowCycleLogic::ModifierWin, AltWindowCycleLogic::StableHoldModifiers(AltWindowCycleLogic::ModifierWin));
            Assert::AreEqual(AltWindowCycleLogic::ModifierShift, AltWindowCycleLogic::StableHoldModifiers(AltWindowCycleLogic::ModifierShift));
            Assert::AreEqual(0u, AltWindowCycleLogic::StableHoldModifiers(0));
        }

        TEST_METHOD(AreRequiredModifiersDownRequiresEveryHeldModifier)
        {
            Assert::IsFalse(AltWindowCycleLogic::AreRequiredModifiersDown(0, AltWindowCycleLogic::ModifierAlt));
            Assert::IsFalse(AltWindowCycleLogic::AreRequiredModifiersDown(AltWindowCycleLogic::ModifierAlt, 0));
            Assert::IsFalse(AltWindowCycleLogic::AreRequiredModifiersDown(AltWindowCycleLogic::ModifierAlt | AltWindowCycleLogic::ModifierCtrl, AltWindowCycleLogic::ModifierAlt));
            Assert::IsTrue(AltWindowCycleLogic::AreRequiredModifiersDown(AltWindowCycleLogic::ModifierAlt, AltWindowCycleLogic::ModifierAlt | AltWindowCycleLogic::ModifierShift));
            Assert::IsTrue(AltWindowCycleLogic::AreRequiredModifiersDown(AltWindowCycleLogic::ModifierAlt | AltWindowCycleLogic::ModifierCtrl, AltWindowCycleLogic::ModifierAlt | AltWindowCycleLogic::ModifierCtrl | AltWindowCycleLogic::ModifierShift));
        }

        TEST_METHOD(BeginCycleSelectsNextOrPreviousAndShowsOverlay)
        {
            const auto forward = AltWindowCycleLogic::BeginCycle(0, 3, true);
            Assert::AreEqual(1, forward.selected);
            Assert::IsTrue(forward.action == AltWindowCycleLogic::FirstHotkeyAction::ShowOverlay);

            const auto backward = AltWindowCycleLogic::BeginCycle(0, 3, false);
            Assert::AreEqual(2, backward.selected);
            Assert::IsTrue(backward.action == AltWindowCycleLogic::FirstHotkeyAction::ShowOverlay);

            const auto ignored = AltWindowCycleLogic::BeginCycle(0, 1, true);
            Assert::AreEqual(0, ignored.selected);
            Assert::IsTrue(ignored.action == AltWindowCycleLogic::FirstHotkeyAction::Ignore);
        }

        // =================== Same-process window selection ===================

        TEST_METHOD(IsAltTabEligibleAcceptsVisibleRepresentativeNonToolWindow)
        {
            Assert::IsTrue(AltWindowCycleLogic::IsAltTabEligible({ true, false, true, false }));
        }

        TEST_METHOD(IsAltTabEligibleRejectsEachDisqualifyingCondition)
        {
            // Invisible.
            Assert::IsFalse(AltWindowCycleLogic::IsAltTabEligible({ false, false, true, false }));
            // Cloaked (e.g. a background virtual-desktop window).
            Assert::IsFalse(AltWindowCycleLogic::IsAltTabEligible({ true, true, true, false }));
            // Owned / not the representative window of its owner chain.
            Assert::IsFalse(AltWindowCycleLogic::IsAltTabEligible({ true, false, false, false }));
            // Tool window.
            Assert::IsFalse(AltWindowCycleLogic::IsAltTabEligible({ true, false, true, true }));
        }

        TEST_METHOD(ProcessKeyEqualsIsCaseInsensitiveAndRejectsEmptyCandidate)
        {
            Assert::IsTrue(AltWindowCycleLogic::ProcessKeyEquals(L"C:\\Apps\\Foo.exe", L"c:\\apps\\foo.EXE"));
            Assert::IsFalse(AltWindowCycleLogic::ProcessKeyEquals(L"C:\\Apps\\Foo.exe", L"C:\\Apps\\Bar.exe"));
            Assert::IsFalse(AltWindowCycleLogic::ProcessKeyEquals(L"", L"C:\\Apps\\Foo.exe"));
        }

        TEST_METHOD(SelectCycleWindowsKeepsOnlyForegroundProcessInEnumerationOrder)
        {
            const std::wstring fg = L"C:\\Apps\\Editor.exe";
            const std::vector<AltWindowCycleLogic::CandidateWindow> candidates = {
                MakeCandidate(10, L"C:\\Apps\\Editor.exe"),
                MakeCandidate(20, L"C:\\Apps\\Browser.exe"),
                MakeCandidate(30, L"c:\\apps\\editor.EXE"), // same app, different casing
                MakeCandidate(40, L"C:\\Apps\\Browser.exe"),
                MakeCandidate(50, L"C:\\Apps\\Editor.exe"),
            };

            AssertIds({ 10, 30, 50 }, AltWindowCycleLogic::SelectCycleWindows(fg, candidates));
        }

        TEST_METHOD(SelectCycleWindowsExcludesIneligibleWindowsOfTheSameApp)
        {
            const std::wstring fg = L"C:\\Apps\\Editor.exe";
            const std::vector<AltWindowCycleLogic::CandidateWindow> candidates = {
                MakeCandidate(1, fg), // eligible
                MakeCandidate(2, fg, /*visible*/ false),
                MakeCandidate(3, fg, /*visible*/ true, /*cloaked*/ true),
                MakeCandidate(4, fg, /*visible*/ true, /*cloaked*/ false, /*representative*/ false),
                MakeCandidate(5, fg, /*visible*/ true, /*cloaked*/ false, /*representative*/ true, /*tool*/ true),
                MakeCandidate(6, fg), // eligible
            };

            AssertIds({ 1, 6 }, AltWindowCycleLogic::SelectCycleWindows(fg, candidates));
        }

        TEST_METHOD(SelectCycleWindowsReturnsEmptyWhenForegroundKeyUnknown)
        {
            const std::vector<AltWindowCycleLogic::CandidateWindow> candidates = {
                MakeCandidate(1, L"C:\\Apps\\Editor.exe"),
            };

            Assert::AreEqual(0u, static_cast<unsigned int>(AltWindowCycleLogic::SelectCycleWindows(L"", candidates).size()));
        }

        TEST_METHOD(SelectCycleWindowsHandlesZeroAndSingleCandidate)
        {
            const std::wstring fg = L"C:\\Apps\\Editor.exe";

            Assert::AreEqual(0u, static_cast<unsigned int>(AltWindowCycleLogic::SelectCycleWindows(fg, {}).size()));

            const std::vector<AltWindowCycleLogic::CandidateWindow> single = { MakeCandidate(99, fg) };
            AssertIds({ 99 }, AltWindowCycleLogic::SelectCycleWindows(fg, single));
        }

        TEST_METHOD(SelectCycleWindowsIncludesEveryWindowOfATiedProcess)
        {
            // Several windows of the same app (the real grouping key is the resolved
            // process image path, so multiple windows / duplicate pids all cycle).
            const std::wstring fg = L"C:\\Apps\\Editor.exe";
            const std::vector<AltWindowCycleLogic::CandidateWindow> candidates = {
                MakeCandidate(7, fg),
                MakeCandidate(8, fg),
                MakeCandidate(9, fg),
            };

            AssertIds({ 7, 8, 9 }, AltWindowCycleLogic::SelectCycleWindows(fg, candidates));
        }

        TEST_METHOD(SelectCycleWindowsDropsCandidatesWithNoResolvedProcess)
        {
            const std::wstring fg = L"C:\\Apps\\Editor.exe";
            const std::vector<AltWindowCycleLogic::CandidateWindow> candidates = {
                MakeCandidate(1, fg),
                MakeCandidate(2, L""), // eligible flags but process could not be resolved
                MakeCandidate(3, fg),
            };

            AssertIds({ 1, 3 }, AltWindowCycleLogic::SelectCycleWindows(fg, candidates));
        }

        // =================== Layout / index edge cases ===================

        TEST_METHOD(ComputeOverlayLayoutClampsColumnsToWindowCount)
        {
            const RECT work = { 0, 0, 1920, 1080 };

            const auto layout = AltWindowCycleLogic::ComputeOverlayLayout(work, 1, 1.0);

            Assert::AreEqual(1, layout.cols);
            Assert::AreEqual(1, layout.rows);
        }

        TEST_METHOD(ComputeOverlayLayoutRespectsMaxColumns)
        {
            const RECT work = { 0, 0, 1920, 1080 };

            const auto layout = AltWindowCycleLogic::ComputeOverlayLayout(work, 10, 1.0, 3);

            Assert::AreEqual(3, layout.cols);
            Assert::AreEqual(4, layout.rows); // ceil(10 / 3)
        }

        TEST_METHOD(ComputeOverlayLayoutClampsColumnsToNarrowWorkArea)
        {
            const RECT work = { 0, 0, 640, 480 };

            const auto layout = AltWindowCycleLogic::ComputeOverlayLayout(work, 8, 1.0);

            // (640 - 64 + 26) / (270 + 26) = 2 columns fit the work area.
            Assert::AreEqual(2, layout.cols);
            Assert::AreEqual(4, layout.rows);
        }

        TEST_METHOD(ComputeOverlayLayoutScalesPaddingAcrossDpi)
        {
            const RECT work = { 0, 0, 1920, 1080 };

            Assert::AreEqual(32, AltWindowCycleLogic::ComputeOverlayLayout(work, 4, 1.0).pad);
            Assert::AreEqual(40, AltWindowCycleLogic::ComputeOverlayLayout(work, 4, 1.25).pad);
            Assert::AreEqual(48, AltWindowCycleLogic::ComputeOverlayLayout(work, 4, 1.5).pad);
            Assert::AreEqual(64, AltWindowCycleLogic::ComputeOverlayLayout(work, 4, 2.0).pad);
        }

        TEST_METHOD(TileRectAdvancesAcrossColumnsAndRows)
        {
            const RECT work = { 0, 0, 1920, 1080 };
            const auto layout = AltWindowCycleLogic::ComputeOverlayLayout(work, 8, 1.0);

            Assert::AreEqual(6, layout.cols); // sanity: second row exists

            // Second column, first row.
            AssertRectEqual({ 328, 32, 598, 234 }, AltWindowCycleLogic::TileRect(layout, 1));
            // First column, second row.
            AssertRectEqual({ 32, 260, 302, 462 }, AltWindowCycleLogic::TileRect(layout, 6));
        }

        TEST_METHOD(WrapIndexNormalizesNegativeAndOversizedIndices)
        {
            Assert::AreEqual(2, AltWindowCycleLogic::WrapIndex(-4, 3));
            Assert::AreEqual(1, AltWindowCycleLogic::WrapIndex(7, 3));
            Assert::AreEqual(0, AltWindowCycleLogic::WrapIndex(0, 5));
            Assert::AreEqual(0, AltWindowCycleLogic::WrapIndex(-3, 3));
        }

        TEST_METHOD(BeginCycleClampsNegativeCurrentIndex)
        {
            const auto forward = AltWindowCycleLogic::BeginCycle(-1, 3, true);
            Assert::AreEqual(1, forward.selected);
            Assert::IsTrue(forward.action == AltWindowCycleLogic::FirstHotkeyAction::ShowOverlay);

            const auto backward = AltWindowCycleLogic::BeginCycle(-5, 3, false);
            Assert::AreEqual(2, backward.selected);
        }

        TEST_METHOD(StableHoldModifiersStripsUnknownBitsAndPrefersNonShift)
        {
            const unsigned int expected =
                AltWindowCycleLogic::ModifierAlt | AltWindowCycleLogic::ModifierCtrl | AltWindowCycleLogic::ModifierWin;
            Assert::AreEqual(expected, AltWindowCycleLogic::StableHoldModifiers(0xFFu));
        }
    };
}
