// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#pragma warning(push)
#pragma warning(disable : 26466)
#include "CppUnitTest.h"
#pragma warning(pop)

#include "../RegionMirror/WindowManagementLogic.h"

#include <limits>

using namespace Microsoft::VisualStudio::CppUnitTestFramework;

namespace RegionMirrorTests
{
    namespace
    {
        constexpr auto LongMinimum = (std::numeric_limits<LONG>::min)();
        constexpr auto LongMaximum = (std::numeric_limits<LONG>::max)();

        void AssertOuter(const RECT& expected, const RECT& desired, const RECT& currentOuter, const RECT& currentVisible)
        {
            const auto result = RegionMirror::ComputeOuterBoundsForVisibleRect(desired, currentOuter, currentVisible);
            Assert::IsTrue(result.has_value());
            Assert::AreEqual(expected.left, result->left, L"left");
            Assert::AreEqual(expected.top, result->top, L"top");
            Assert::AreEqual(expected.right, result->right, L"right");
            Assert::AreEqual(expected.bottom, result->bottom, L"bottom");
        }
    }

    TEST_CLASS (WindowManagementTests)
    {
    public:
        TEST_METHOD (ObserveMaximizedTriggersOnlyOnEntry)
        {
            bool wasMaximized = false;
            Assert::IsFalse(RegionMirror::ObserveMaximized(wasMaximized, false, false));
            Assert::IsFalse(wasMaximized);
            Assert::IsTrue(RegionMirror::ObserveMaximized(wasMaximized, true, false));
            Assert::IsTrue(wasMaximized);
            Assert::IsFalse(RegionMirror::ObserveMaximized(wasMaximized, true, false));
            Assert::IsTrue(wasMaximized);
        }

        TEST_METHOD (ObserveMaximizedRestoreRearmsTheNextEntry)
        {
            bool wasMaximized = true;
            Assert::IsFalse(RegionMirror::ObserveMaximized(wasMaximized, false, false));
            Assert::IsFalse(wasMaximized);
            Assert::IsTrue(RegionMirror::ObserveMaximized(wasMaximized, true, false));
            Assert::IsFalse(RegionMirror::ObserveMaximized(wasMaximized, true, false));
            Assert::IsFalse(RegionMirror::ObserveMaximized(wasMaximized, false, false));
            Assert::IsTrue(RegionMirror::ObserveMaximized(wasMaximized, true, false));
        }

        TEST_METHOD (ObserveMaximizedNormalMoveAndResizeNeverTrigger)
        {
            bool wasMaximized = false;
            for (const bool dragging : { false, true, true, false, false })
            {
                Assert::IsFalse(RegionMirror::ObserveMaximized(wasMaximized, false, dragging));
                Assert::IsFalse(wasMaximized);
            }
        }

        TEST_METHOD (ObserveMaximizedDuringDraggingIsRecordedWithoutDeferredAction)
        {
            bool wasMaximized = false;
            Assert::IsFalse(RegionMirror::ObserveMaximized(wasMaximized, true, true));
            Assert::IsTrue(wasMaximized);
            Assert::IsFalse(RegionMirror::ObserveMaximized(wasMaximized, true, false));
            Assert::IsTrue(wasMaximized);
            Assert::IsFalse(RegionMirror::ObserveMaximized(wasMaximized, false, true));
            Assert::IsFalse(wasMaximized);
            Assert::IsTrue(RegionMirror::ObserveMaximized(wasMaximized, true, false));
        }

        TEST_METHOD (ObserveMaximizedAlreadyMaximizedAndSelfEventsDoNotTrigger)
        {
            bool wasMaximized = true;
            Assert::IsFalse(RegionMirror::ObserveMaximized(wasMaximized, true, false));
            Assert::IsFalse(RegionMirror::ObserveMaximized(wasMaximized, true, true));
            Assert::IsFalse(RegionMirror::ObserveMaximized(wasMaximized, true, false));
            Assert::IsTrue(wasMaximized);
        }

        TEST_METHOD (OuterBoundsUsesMeasuredElevenPixelMaximizedFrame)
        {
            AssertOuter({ 89, 89, 911, 711 }, { 100, 100, 900, 700 }, { -11, -11, 1931, 1091 }, { 0, 0, 1920, 1080 });
        }

        TEST_METHOD (OuterBoundsUsesAsymmetricFrameAtNegativeAndCrossMonitorCoordinates)
        {
            AssertOuter({ -208, -125, 1208, 508 }, { -200, -100, 1200, 500 }, { 92, 75, 1108, 808 }, { 100, 100, 1100, 800 });
        }

        TEST_METHOD (OuterBoundsSupportsSignedNegativeAndMixedFrameMargins)
        {
            AssertOuter({ 110, 210, 690, 590 }, { 100, 200, 700, 600 }, { 10, 20, 1010, 820 }, { 0, 10, 1020, 830 });
            AssertOuter({ -490, -308, 192, 110 }, { -500, -300, 200, 100 }, { 100, 100, 1100, 900 }, { 90, 108, 1108, 890 });
        }

        TEST_METHOD (OuterBoundsPreservesUnframedAndOnePixelRectangles)
        {
            const RECT current{ 50, 70, 1050, 870 };
            AssertOuter({ -500, -200, 300, 400 }, { -500, -200, 300, 400 }, current, current);
            AssertOuter({ -1, -1, 0, 0 }, { -1, -1, 0, 0 }, current, current);
        }

        TEST_METHOD (OuterBoundsAllowsMaximumRegionPlusBoundedFrame)
        {
            AssertOuter(
                { -8448, -8448, 8448, 8448 },
                { -8192, -8192, 8192, 8192 },
                { -256, -256, 16640, 16640 },
                { 0, 0, 16384, 16384 });
            AssertOuter(
                { 256, 256, 768, 768 },
                { 0, 0, 1024, 1024 },
                { 0, 0, 16384, 16384 },
                { -256, -256, 16640, 16640 });
        }

        TEST_METHOD (OuterBoundsRejectsInvalidDesiredRectangles)
        {
            const RECT current{ 0, 0, 1000, 800 };
            const RECT invalid[]{
                {},
                { 0, 0, 0, 100 },
                { 0, 0, 100, 0 },
                { 100, 0, 0, 100 },
                { 0, 100, 100, 0 },
                { 0, 0, 16385, 100 },
                { 0, 0, 100, 16385 },
                { LongMinimum, 0, LongMaximum, 100 }
            };
            for (const auto& desired : invalid)
            {
                Assert::IsFalse(RegionMirror::ComputeOuterBoundsForVisibleRect(desired, current, current).has_value());
            }
        }

        TEST_METHOD (OuterBoundsRejectsInvalidCurrentFrameData)
        {
            const RECT valid{ 0, 0, 1000, 800 };
            const RECT invalid[]{
                {},
                { 0, 0, 0, 100 },
                { 100, 0, 0, 100 },
                { 0, 100, 100, 0 },
                { 0, 0, 16897, 100 },
                { 0, 0, 100, 16897 },
                { LongMinimum, 0, LongMaximum, 100 },
                { 0, LongMinimum, 100, LongMaximum }
            };
            for (const auto& rectangle : invalid)
            {
                Assert::IsFalse(RegionMirror::ComputeOuterBoundsForVisibleRect(valid, rectangle, valid).has_value());
                Assert::IsFalse(RegionMirror::ComputeOuterBoundsForVisibleRect(valid, valid, rectangle).has_value());
            }
        }

        TEST_METHOD (OuterBoundsRejectsPathologicalSignedMarginsOnEveryEdge)
        {
            const RECT desired{ 0, 0, 1000, 800 };
            const RECT outer{ 0, 0, 2000, 2000 };
            const RECT visibleRectangles[]{
                { 257, 0, 2000, 2000 },
                { -257, 0, 2000, 2000 },
                { 0, 257, 2000, 2000 },
                { 0, -257, 2000, 2000 },
                { 0, 0, 1743, 2000 },
                { 0, 0, 2257, 2000 },
                { 0, 0, 2000, 1743 },
                { 0, 0, 2000, 2257 }
            };
            for (const auto& visible : visibleRectangles)
            {
                Assert::IsFalse(RegionMirror::ComputeOuterBoundsForVisibleRect(desired, outer, visible).has_value());
            }
            Assert::IsFalse(RegionMirror::ComputeOuterBoundsForVisibleRect(
                                desired,
                                { LongMinimum, 0, LongMinimum + 1000, 800 },
                                { LongMaximum - 1000, 0, LongMaximum, 800 })
                                .has_value());
        }

        TEST_METHOD (OuterBoundsRejectsEmptyOrInvertedResultsFromNegativeMargins)
        {
            const RECT outer{ 50, 50, 150, 150 };
            const RECT visible{ 0, 0, 200, 200 };
            Assert::IsFalse(RegionMirror::ComputeOuterBoundsForVisibleRect({ 0, 0, 100, 300 }, outer, visible).has_value());
            Assert::IsFalse(RegionMirror::ComputeOuterBoundsForVisibleRect({ 0, 0, 300, 100 }, outer, visible).has_value());
            Assert::IsFalse(RegionMirror::ComputeOuterBoundsForVisibleRect({ 0, 0, 99, 99 }, outer, visible).has_value());
        }

        TEST_METHOD (OuterBoundsRejectsEndpointOverflowOnEveryEdge)
        {
            const RECT outer{ -11, -11, 1011, 811 };
            const RECT visible{ 0, 0, 1000, 800 };
            const RECT desiredRectangles[]{
                { LongMinimum, 0, LongMinimum + 100, 100 },
                { 0, LongMinimum, 100, LongMinimum + 100 },
                { LongMaximum - 100, 0, LongMaximum, 100 },
                { 0, LongMaximum - 100, 100, LongMaximum }
            };
            for (const auto& desired : desiredRectangles)
            {
                Assert::IsFalse(RegionMirror::ComputeOuterBoundsForVisibleRect(desired, outer, visible).has_value());
            }
        }

        TEST_METHOD (OuterBoundsAcceptsResultsExactlyAtLongLimits)
        {
            const RECT outer{ -11, -11, 1011, 811 };
            const RECT visible{ 0, 0, 1000, 800 };
            AssertOuter(
                { LongMinimum, LongMinimum, LongMinimum + 122, LongMinimum + 122 },
                { LongMinimum + 11, LongMinimum + 11, LongMinimum + 111, LongMinimum + 111 },
                outer,
                visible);
            AssertOuter(
                { LongMaximum - 122, LongMaximum - 122, LongMaximum, LongMaximum },
                { LongMaximum - 111, LongMaximum - 111, LongMaximum - 11, LongMaximum - 11 },
                outer,
                visible);
        }
    };
}
