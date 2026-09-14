// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#include <CppUnitTest.h>

#include "VirtualMirrorLayout.h"

#include <limits>

using namespace Microsoft::VisualStudio::CppUnitTestFramework;

namespace VirtualMirrorTests
{
    using namespace zoomit_mirror;

    namespace
    {
        MonitorSnapshot Source()
        {
            return { reinterpret_cast<HMONITOR>(static_cast<UINT_PTR>(1)), { -2560, -200, 0, 1240 }, L"DISPLAY1", L"physical-source" };
        }

        MonitorSnapshot Target()
        {
            return { reinterpret_cast<HMONITOR>(static_cast<UINT_PTR>(2)), { 1920, 0, 3840, 1080 }, L"DISPLAY3", L"owned-virtual-target" };
        }
    }

    TEST_CLASS(VirtualMirrorLayoutTests)
    {
    public:
        TEST_METHOD(AcceptsRegionOnNegativeCoordinateMonitor)
        {
            const auto source = Source();
            const RECT region{ -2400, -100, -1440, 440 };
            Assert::IsTrue(IsMirrorLayoutValid(source, source, Target(), region));
            Assert::IsTrue(IsRegionWithinMonitor(source.bounds, source.bounds));
        }

        TEST_METHOD(RejectsRegionSpanningSourceBoundary)
        {
            const auto source = Source();
            Assert::IsFalse(IsMirrorLayoutValid(source, source, Target(), { -100, 0, 100, 540 }));
            Assert::IsFalse(IsRegionWithinMonitor({ -2561, 0, -2000, 540 }, source.bounds));
            Assert::IsFalse(IsRegionWithinMonitor({ -2000, -201, -1440, 440 }, source.bounds));
            Assert::IsFalse(IsRegionWithinMonitor({ -2000, 1000, -1440, 1241 }, source.bounds));
        }

        TEST_METHOD(RejectsEmptyOrInvertedRegion)
        {
            const auto source = Source();
            Assert::IsFalse(IsMirrorLayoutValid(source, source, Target(), { -100, 0, -100, 200 }));
            Assert::IsFalse(IsMirrorLayoutValid(source, source, Target(), { -100, 200, -200, 0 }));
        }

        TEST_METHOD(ChecksExtremeCoordinateBoundsWithoutOverflow)
        {
            constexpr LONG lowest = (std::numeric_limits<LONG>::min)();
            constexpr LONG highest = (std::numeric_limits<LONG>::max)();
            const RECT bounds{ lowest, lowest, highest, highest };
            Assert::IsTrue(IsRegionWithinMonitor({ lowest, lowest, lowest + 1, lowest + 1 }, bounds));
            Assert::IsTrue(IsRegionWithinMonitor({ highest - 1, highest - 1, highest, highest }, bounds));
            Assert::IsFalse(IsRegionWithinMonitor({ highest, highest, lowest, lowest }, bounds));
        }

        TEST_METHOD(RejectsSourceReplacementWithSameGdiNameAndBounds)
        {
            const auto original = Source();
            auto replacement = original;
            replacement.identity = L"different-physical-monitor";
            Assert::IsFalse(IsMirrorLayoutValid(original, replacement, Target(), original.bounds));
            replacement.identity.clear();
            Assert::IsFalse(IsMirrorLayoutValid(original, replacement, Target(), original.bounds));
        }

        TEST_METHOD(RejectsSourceMovementOrResolutionChange)
        {
            const auto original = Source();
            auto moved = original;
            ++moved.bounds.left;
            ++moved.bounds.right;
            Assert::IsFalse(IsMirrorLayoutValid(original, moved, Target(), { -2400, 0, -1440, 540 }));
            auto resized = original;
            ++resized.bounds.bottom;
            Assert::IsFalse(IsMirrorLayoutValid(original, resized, Target(), { -2400, 0, -1440, 540 }));
        }

        TEST_METHOD(AllowsGdiRenumberingAndFreshHandleDuringCreation)
        {
            const auto original = Source();
            auto current = original;
            current.deviceName = L"DISPLAY4";
            current.monitor = reinterpret_cast<HMONITOR>(static_cast<UINT_PTR>(4));
            Assert::IsTrue(IsMirrorLayoutValid(original, current, Target(), original.bounds));
            Assert::IsFalse(IsSameMonitorSnapshot(original, current));
        }

        TEST_METHOD(RejectsTargetOverlappingSourceButAllowsTouchingEdges)
        {
            const auto source = Source();
            auto target = Target();
            target.bounds = { -1, 0, 1919, 1080 };
            Assert::IsFalse(IsMirrorLayoutValid(source, source, target, source.bounds));
            target.bounds = { 0, 0, 1920, 1080 };
            Assert::IsTrue(IsMirrorLayoutValid(source, source, target, source.bounds));
        }

        TEST_METHOD(RejectsSourceAsTargetAndMissingTarget)
        {
            const auto source = Source();
            auto target = Target();
            target.identity = source.identity;
            Assert::IsFalse(IsMirrorLayoutValid(source, source, target, source.bounds));
            target = Target();
            target.monitor = source.monitor;
            Assert::IsFalse(IsMirrorLayoutValid(source, source, target, source.bounds));
            target.monitor = nullptr;
            Assert::IsFalse(IsMirrorLayoutValid(source, source, target, source.bounds));
            target = Target();
            target.bounds = {};
            Assert::IsFalse(IsMirrorLayoutValid(source, source, target, source.bounds));
        }

        TEST_METHOD(RejectsActiveTargetMovementReplacementOrHandleChange)
        {
            const auto expected = Target();
            auto current = expected;
            ++current.bounds.right;
            Assert::IsFalse(IsSameMonitorSnapshot(expected, current));
            current = expected;
            current.identity = L"replacement-target";
            Assert::IsFalse(IsSameMonitorSnapshot(expected, current));
            current = expected;
            current.monitor = reinterpret_cast<HMONITOR>(static_cast<UINT_PTR>(5));
            Assert::IsFalse(IsSameMonitorSnapshot(expected, current));
        }
    };
}
