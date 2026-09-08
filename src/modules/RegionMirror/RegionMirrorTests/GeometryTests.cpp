// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#pragma warning(push)
#pragma warning(disable : 26466)
#include "CppUnitTest.h"
#pragma warning(pop)

#include "../RegionMirror/Geometry.h"

#include <limits>
#include <string_view>

using namespace Microsoft::VisualStudio::CppUnitTestFramework;
using namespace std::literals;

namespace RegionMirrorTests
{
    namespace
    {
        constexpr auto LongMinimum = (std::numeric_limits<LONG>::min)();
        constexpr auto LongMaximum = (std::numeric_limits<LONG>::max)();

        void AssertRectEqual(const RECT& expected, const RECT& actual)
        {
            Assert::AreEqual(expected.left, actual.left, L"left");
            Assert::AreEqual(expected.top, actual.top, L"top");
            Assert::AreEqual(expected.right, actual.right, L"right");
            Assert::AreEqual(expected.bottom, actual.bottom, L"bottom");
        }

        void AssertParses(std::wstring_view text, const RECT& expected)
        {
            RECT result{};
            Assert::IsTrue(RegionMirror::TryParseRegion(text, result));
            AssertRectEqual(expected, result);
            Assert::IsTrue(RegionMirror::IsValidRegion(result));
        }

        void AssertRejected(std::wstring_view text)
        {
            const RECT sentinel{ -17, -19, 123, 321 };
            RECT result = sentinel;
            Assert::IsFalse(RegionMirror::TryParseRegion(text, result));
            AssertRectEqual(sentinel, result);
        }
    }

    TEST_CLASS (GeometryTests)
    {
    public:
        TEST_METHOD (NormalizeSelectionHandlesAllDragDirectionsAndNegativeCoordinates)
        {
            const RECT expected{ -1600, -700, -200, 300 };
            AssertRectEqual(expected, RegionMirror::NormalizeSelection({ -1600, -700 }, { -200, 300 }));
            AssertRectEqual(expected, RegionMirror::NormalizeSelection({ -200, 300 }, { -1600, -700 }));
            AssertRectEqual(expected, RegionMirror::NormalizeSelection({ -1600, 300 }, { -200, -700 }));
            AssertRectEqual(expected, RegionMirror::NormalizeSelection({ -200, -700 }, { -1600, 300 }));
        }

        TEST_METHOD (NormalizeSelectionPreservesDegenerateAndExtremeInputWithoutOverflow)
        {
            AssertRectEqual({ 20, 30, 20, 30 }, RegionMirror::NormalizeSelection({ 20, 30 }, { 20, 30 }));
            const auto extreme = RegionMirror::NormalizeSelection({ LongMaximum, LongMinimum }, { LongMinimum, LongMaximum });
            AssertRectEqual({ LongMinimum, LongMinimum, LongMaximum, LongMaximum }, extreme);
            Assert::IsFalse(RegionMirror::IsValidRegion(extreme));
        }

        TEST_METHOD (IsValidRegionAcceptsOnePixelAndMaximumCaptureDimensions)
        {
            Assert::IsTrue(RegionMirror::IsValidRegion({ -1, -1, 0, 0 }));
            Assert::IsTrue(RegionMirror::IsValidRegion({ -8192, -8192, 8192, 8192 }));
            Assert::IsTrue(RegionMirror::IsValidRegion({ LongMinimum, LongMinimum, LongMinimum + 16384, LongMinimum + 16384 }));
            Assert::IsTrue(RegionMirror::IsValidRegion({ LongMaximum - 1, LongMaximum - 1, LongMaximum, LongMaximum }));
        }

        TEST_METHOD (IsValidRegionRejectsEmptyInvertedOversizedAndOverflowingDimensions)
        {
            const RECT invalidRegions[]{
                { 0, 0, 0, 100 },
                { 0, 0, 100, 0 },
                { 1, 0, 0, 100 },
                { 0, 1, 100, 0 },
                { 0, 0, 16385, 100 },
                { 0, 0, 100, 16385 },
                { LongMinimum, 0, LongMaximum, 100 },
                { 0, LongMinimum, 100, LongMaximum },
                { LongMaximum, 0, LongMinimum, 100 }
            };
            for (const auto& region : invalidRegions)
            {
                Assert::IsFalse(RegionMirror::IsValidRegion(region));
            }
        }

        TEST_METHOD (IsContainedAcceptsMonitorEdgesAndRejectsCrossMonitorRegions)
        {
            const RECT monitor{ -1920, -200, 0, 880 };
            Assert::IsTrue(RegionMirror::IsContained(monitor, monitor));
            Assert::IsTrue(RegionMirror::IsContained({ -1, 879, 0, 880 }, monitor));
            Assert::IsFalse(RegionMirror::IsContained({ -10, 0, 10, 100 }, monitor));
            Assert::IsFalse(RegionMirror::IsContained({ -1921, 0, -1800, 100 }, monitor));
            Assert::IsFalse(RegionMirror::IsContained({ -100, -201, 0, 100 }, monitor));
            Assert::IsFalse(RegionMirror::IsContained({ -100, 0, 0, 881 }, monitor));
        }

        TEST_METHOD (IsContainedRejectsInvalidRectanglesAndAllowsLargeBounds)
        {
            Assert::IsFalse(RegionMirror::IsContained({ 0, 0, 0, 10 }, { -100, -100, 100, 100 }));
            Assert::IsFalse(RegionMirror::IsContained({ 0, 0, 10, 10 }, { 100, 100, -100, -100 }));
            Assert::IsFalse(RegionMirror::IsContained({ 0, 0, 10, 10 }, { 0, 0, 0, 0 }));
            Assert::IsTrue(RegionMirror::IsContained({ -100, -100, 100, 100 }, { LongMinimum, LongMinimum, LongMaximum, LongMaximum }));
        }

        TEST_METHOD (FitAspectLetterboxesWideContent)
        {
            AssertRectEqual({ 0, 75, 800, 525 }, RegionMirror::FitAspect({ 0, 0, 1920, 1080 }, { 0, 0, 800, 600 }));
        }

        TEST_METHOD (FitAspectPillarboxesTallContent)
        {
            AssertRectEqual({ 175, 0, 625, 600 }, RegionMirror::FitAspect({ 0, 0, 600, 800 }, { 0, 0, 800, 600 }));
        }

        TEST_METHOD (FitAspectPreservesTargetWhenAspectRatiosMatch)
        {
            const RECT target{ -1000, -600, -200, -150 };
            AssertRectEqual(target, RegionMirror::FitAspect({ 120, 150, 2040, 1230 }, target));
        }

        TEST_METHOD (FitAspectCentersOddPaddingAtNegativeScreenOrigins)
        {
            AssertRectEqual({ -50, -50, 350, 350 }, RegionMirror::FitAspect({ -200, 0, 0, 200 }, { -100, -50, 401, 350 }));
        }

        TEST_METHOD (FitAspectHandlesLongBoundaryCoordinates)
        {
            const RECT content{ LongMinimum, LongMinimum, LongMinimum + 16384, LongMinimum + 8192 };
            const RECT target{ LongMaximum - 16384, LongMaximum - 16384, LongMaximum, LongMaximum };
            AssertRectEqual({ LongMaximum - 16384, LongMaximum - 12288, LongMaximum, LongMaximum - 4096 }, RegionMirror::FitAspect(content, target));
        }

        TEST_METHOD (FitAspectRejectsInvalidInputAndSubpixelFits)
        {
            const RECT valid{ 0, 0, 800, 600 };
            AssertRectEqual({}, RegionMirror::FitAspect({}, valid));
            AssertRectEqual({}, RegionMirror::FitAspect(valid, {}));
            AssertRectEqual({}, RegionMirror::FitAspect({ 0, 0, 16385, 10 }, valid));
            AssertRectEqual({}, RegionMirror::FitAspect(valid, { LongMinimum, 0, LongMaximum, 100 }));
            AssertRectEqual({}, RegionMirror::FitAspect({ 0, 0, 16384, 1 }, { 0, 0, 1, 1 }));
            AssertRectEqual({}, RegionMirror::FitAspect({ 0, 0, 1, 16384 }, { 0, 0, 1, 1 }));
        }

        TEST_METHOD (TryParseRegionAcceptsPhysicalPixelCoordinatesAndNegativeOrigins)
        {
            AssertParses(L"120,80,1920,1080", { 120, 80, 2040, 1160 });
            AssertParses(L"-1920,-200,1920,1080", { -1920, -200, 0, 880 });
            AssertParses(L"0000,-0001,0001,0001", { 0, -1, 1, 0 });
        }

        TEST_METHOD (TryParseRegionAcceptsLimitCoordinatesWithoutEndpointOverflow)
        {
            AssertParses(L"-2147483648,-2147483648,16384,16384", { LongMinimum, LongMinimum, LongMinimum + 16384, LongMinimum + 16384 });
            AssertParses(L"2147483646,2147483646,1,1", { LongMaximum - 1, LongMaximum - 1, LongMaximum, LongMaximum });
            AssertParses(L"-8192,-8192,16384,16384", { -8192, -8192, 8192, 8192 });
        }

        TEST_METHOD (TryParseRegionRejectsMissingExtraAndNondecimalComponents)
        {
            const std::wstring_view invalidInputs[]{
                L"",
                L"0",
                L"0,0,10",
                L"0,0,10,10,20",
                L",0,10,10",
                L"0,,10,10",
                L"0,0,,10",
                L"0,0,10,",
                L"0,0,10,10,",
                L"-,0,10,10",
                L"--1,0,10,10",
                L"0x10,0,10,10",
                L"0,0,1.0,10",
                L"0,0,1e2,10",
                L"0,0,10,10px"
            };
            for (const auto input : invalidInputs)
            {
                AssertRejected(input);
            }
        }

        TEST_METHOD (TryParseRegionRejectsWhitespacePlusUnicodeAndEmbeddedNulls)
        {
            const std::wstring_view invalidInputs[]{
                L" 0,0,10,10",
                L"0, 0,10,10",
                L"0,0,10,10 ",
                L"0,0,10,10\n",
                L"\t0,0,10,10",
                L"+0,0,10,10",
                L"0,0,+10,10",
                L"\u22121,0,10,10",
                L"\uFF10,0,10,10",
                L"0,0,10,10\0ignored"sv,
                L"0,0,1\0,10"sv
            };
            for (const auto input : invalidInputs)
            {
                AssertRejected(input);
            }
        }

        TEST_METHOD (TryParseRegionRejectsInvalidDimensionsAndKeepsOutputUnchanged)
        {
            const std::wstring_view invalidInputs[]{
                L"0,0,0,10",
                L"0,0,10,0",
                L"0,0,-1,10",
                L"0,0,10,-1",
                L"0,0,16385,10",
                L"0,0,10,16385",
                L"0,0,2147483647,10"
            };
            for (const auto input : invalidInputs)
            {
                AssertRejected(input);
            }
        }

        TEST_METHOD (TryParseRegionRejectsIntegerAndEndpointOverflow)
        {
            const std::wstring_view invalidInputs[]{
                L"2147483648,0,1,1",
                L"-2147483649,0,1,1",
                L"0,2147483648,1,1",
                L"0,-2147483649,1,1",
                L"2147483647,0,1,1",
                L"0,2147483647,1,1",
                L"2147483640,0,8,1",
                L"0,2147483640,1,8",
                L"0,0,2147483648,1",
                L"99999999999999999999999999999999999999999999999999,0,1,1",
                L"0,0,1,-99999999999999999999999999999999999999999999999999"
            };
            for (const auto input : invalidInputs)
            {
                AssertRejected(input);
            }
        }
    };
}
