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
    };
}
