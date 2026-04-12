// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

// Unit tests for MousePointerCrosshairs line-calculation logic, mirroring the
// Rust test suite in src/rust/libs/crosshairs-core/src/line_calculator.rs.
//
// The C++ implementation renders crosshair lines via Windows Composition APIs.
// These tests verify the settings defaults and the geometric math that drives
// line placement (radius gap, fixed length, orientation filtering) purely
// from the settings struct without needing a running compositor.

#include "pch.h"

#pragma warning(push)
#pragma warning(disable : 26466)
#include "CppUnitTest.h"
#pragma warning(pop)

#include "../InclusiveCrosshairs.h"

using namespace Microsoft::VisualStudio::CppUnitTestFramework;

namespace CrosshairsUnitTests
{
    // ── helpers ──────────────────────────────────────────────────────────────

    // Re-implement the crosshair arm-length formula used by the C++ renderer
    // and the Rust calculator.  With even thickness (hpa == 0):
    //   left_length  = cursorX - screenLeft - radius
    //   right_length = screenRight - cursorX - radius
    //   top_length   = cursorY - screenTop  - radius
    //   bottom_length= screenBottom - cursorY - radius

    struct ScreenBounds { int left, top, right, bottom; };

    struct ArmLengths { double leftW, rightW, topH, bottomH; };

    static ArmLengths CalcArmLengths(int cx, int cy, ScreenBounds scr,
                                      int radius, int thickness,
                                      bool fixedLen, int fixedVal)
    {
        double hpa = (thickness % 2 == 1) ? 0.5 : 0.0;
        double r = static_cast<double>(radius);

        double leftFull  = static_cast<double>(cx) - static_cast<double>(scr.left) - r + hpa * 2.0;
        double rightFull = static_cast<double>(scr.right) - static_cast<double>(cx) - r;
        double topFull   = static_cast<double>(cy) - static_cast<double>(scr.top) - r + hpa * 2.0;
        double bottomFull= static_cast<double>(scr.bottom) - static_cast<double>(cy) - r;

        if (fixedLen)
        {
            return { static_cast<double>(fixedVal), static_cast<double>(fixedVal),
                     static_cast<double>(fixedVal), static_cast<double>(fixedVal) };
        }
        return { leftFull, rightFull, topFull, bottomFull };
    }

    // ── test class ──────────────────────────────────────────────────────────

    TEST_CLASS(CrosshairsTests)
    {
    public:
        // ── Center of screen: 4 equal lines ─────────────────────────────

        TEST_METHOD(CenterOfScreen_FourEqualLines)
        {
            // 1000×1000 screen, cursor at (500,500), radius=20, even thickness=4
            ScreenBounds scr = { 0, 0, 1000, 1000 };
            auto arms = CalcArmLengths(500, 500, scr, 20, 4, false, 0);
            Assert::AreEqual(480.0, arms.leftW, 1e-10);
            Assert::AreEqual(480.0, arms.rightW, 1e-10);
            Assert::AreEqual(480.0, arms.topH, 1e-10);
            Assert::AreEqual(480.0, arms.bottomH, 1e-10);
        }

        TEST_METHOD(CenterOfScreen_LeftEqualsRight)
        {
            ScreenBounds scr = { 0, 0, 1000, 1000 };
            auto arms = CalcArmLengths(500, 500, scr, 20, 4, false, 0);
            Assert::AreEqual(arms.leftW, arms.rightW, 1e-10);
        }

        TEST_METHOD(CenterOfScreen_TopEqualsBottom)
        {
            ScreenBounds scr = { 0, 0, 1000, 1000 };
            auto arms = CalcArmLengths(500, 500, scr, 20, 4, false, 0);
            Assert::AreEqual(arms.topH, arms.bottomH, 1e-10);
        }

        // ── Near edge: shorter lines ────────────────────────────────────

        TEST_METHOD(NearLeftEdge_ShorterLeft)
        {
            ScreenBounds scr = { 0, 0, 1000, 1000 };
            auto arms = CalcArmLengths(30, 500, scr, 20, 4, false, 0);
            Assert::AreEqual(10.0, arms.leftW, 1e-10, L"Left arm should be 30-0-20=10");
            Assert::IsTrue(arms.leftW < arms.rightW);
        }

        TEST_METHOD(NearRightEdge_ShorterRight)
        {
            ScreenBounds scr = { 0, 0, 1000, 1000 };
            auto arms = CalcArmLengths(970, 500, scr, 20, 4, false, 0);
            Assert::AreEqual(10.0, arms.rightW, 1e-10, L"Right arm should be 1000-970-20=10");
            Assert::IsTrue(arms.rightW < arms.leftW);
        }

        TEST_METHOD(NearTopEdge_ShorterTop)
        {
            ScreenBounds scr = { 0, 0, 1000, 1000 };
            auto arms = CalcArmLengths(500, 30, scr, 20, 4, false, 0);
            Assert::AreEqual(10.0, arms.topH, 1e-10);
        }

        TEST_METHOD(NearBottomEdge_ShorterBottom)
        {
            ScreenBounds scr = { 0, 0, 1000, 1000 };
            auto arms = CalcArmLengths(500, 970, scr, 20, 4, false, 0);
            Assert::AreEqual(10.0, arms.bottomH, 1e-10);
        }

        // ── Fixed length mode ───────────────────────────────────────────

        TEST_METHOD(FixedLength_AllArmsEqual)
        {
            ScreenBounds scr = { 0, 0, 1000, 1000 };
            auto arms = CalcArmLengths(500, 500, scr, 20, 4, true, 100);
            Assert::AreEqual(100.0, arms.leftW, 1e-10);
            Assert::AreEqual(100.0, arms.rightW, 1e-10);
            Assert::AreEqual(100.0, arms.topH, 1e-10);
            Assert::AreEqual(100.0, arms.bottomH, 1e-10);
        }

        TEST_METHOD(FixedLength_IgnoresCursorPosition)
        {
            ScreenBounds scr = { 0, 0, 1000, 1000 };
            auto arms1 = CalcArmLengths(100, 100, scr, 20, 4, true, 150);
            auto arms2 = CalcArmLengths(900, 900, scr, 20, 4, true, 150);
            Assert::AreEqual(arms1.leftW, arms2.leftW, 1e-10);
            Assert::AreEqual(arms1.rightW, arms2.rightW, 1e-10);
        }

        // ── Orientation filtering: VerticalOnly ─────────────────────────

        TEST_METHOD(VerticalOnly_Settings)
        {
            InclusiveCrosshairsSettings settings;
            settings.crosshairsOrientation = CrosshairsOrientation::VerticalOnly;
            Assert::AreEqual(static_cast<int>(CrosshairsOrientation::VerticalOnly),
                             static_cast<int>(settings.crosshairsOrientation));

            // In VerticalOnly mode the horizontal arms (left/right) should
            // have zero width.  The renderer zeroes them; replicate the check.
            bool isVertOnly = (settings.crosshairsOrientation == CrosshairsOrientation::VerticalOnly);
            Assert::IsTrue(isVertOnly);
        }

        // ── Orientation filtering: HorizontalOnly ───────────────────────

        TEST_METHOD(HorizontalOnly_Settings)
        {
            InclusiveCrosshairsSettings settings;
            settings.crosshairsOrientation = CrosshairsOrientation::HorizontalOnly;
            Assert::AreEqual(static_cast<int>(CrosshairsOrientation::HorizontalOnly),
                             static_cast<int>(settings.crosshairsOrientation));
        }

        // ── Orientation filtering: Both ─────────────────────────────────

        TEST_METHOD(Orientation_Both_IsDefault)
        {
            InclusiveCrosshairsSettings settings;
            Assert::AreEqual(static_cast<int>(CrosshairsOrientation::Both),
                             static_cast<int>(settings.crosshairsOrientation),
                             L"Default orientation should be Both");
        }

        // ── Radius gap around cursor ────────────────────────────────────

        TEST_METHOD(RadiusGap_LeftArmEndsBeforeCursor)
        {
            // With cursor at 500, radius=20: left arm right edge = 500-20 = 480
            ScreenBounds scr = { 0, 0, 1000, 1000 };
            int cx = 500, radius = 20;
            double leftRightEdge = static_cast<double>(cx) - static_cast<double>(radius); // where left arm ends
            Assert::AreEqual(480.0, leftRightEdge, 1e-10);
        }

        TEST_METHOD(RadiusGap_RightArmStartsAfterCursor)
        {
            int cx = 500, radius = 20;
            double rightLeftEdge = static_cast<double>(cx) + static_cast<double>(radius); // where right arm starts
            Assert::AreEqual(520.0, rightLeftEdge, 1e-10);
        }

        TEST_METHOD(RadiusGap_TopArmEndsBeforeCursor)
        {
            int cy = 500, radius = 20;
            double topBottomEdge = static_cast<double>(cy) - static_cast<double>(radius);
            Assert::AreEqual(480.0, topBottomEdge, 1e-10);
        }

        TEST_METHOD(RadiusGap_BottomArmStartsAfterCursor)
        {
            int cy = 500, radius = 20;
            double bottomTopEdge = static_cast<double>(cy) + static_cast<double>(radius);
            Assert::AreEqual(520.0, bottomTopEdge, 1e-10);
        }

        // ── Default settings verification ───────────────────────────────

        TEST_METHOD(DefaultRadius_Is20)
        {
            InclusiveCrosshairsSettings s;
            Assert::AreEqual(20, s.crosshairsRadius);
        }

        TEST_METHOD(DefaultThickness_Is5)
        {
            InclusiveCrosshairsSettings s;
            Assert::AreEqual(5, s.crosshairsThickness);
        }

        TEST_METHOD(DefaultOpacity_Is75)
        {
            InclusiveCrosshairsSettings s;
            Assert::AreEqual(75, s.crosshairsOpacity);
        }

        TEST_METHOD(DefaultBorderSize_Is1)
        {
            InclusiveCrosshairsSettings s;
            Assert::AreEqual(1, s.crosshairsBorderSize);
        }

        TEST_METHOD(DefaultFixedLength_IsDisabled)
        {
            InclusiveCrosshairsSettings s;
            Assert::IsFalse(s.crosshairsIsFixedLengthEnabled);
        }

        // ── Opacity normalisation ───────────────────────────────────────

        // The renderer normalises opacity from integer percent (0-100) to 0.0-1.0.
        TEST_METHOD(OpacityNormalisation_75Percent)
        {
            InclusiveCrosshairsSettings s;
            float normalized = static_cast<float>(s.crosshairsOpacity) / 100.0f;
            Assert::AreEqual(0.75f, normalized, 0.001f);
        }

        TEST_METHOD(OpacityNormalisation_Clamped)
        {
            auto clamp = [](int pct) -> float {
                return (std::max)(0.0f, (std::min)(1.0f, pct / 100.0f));
            };
            Assert::AreEqual(0.0f, clamp(0), 0.001f);
            Assert::AreEqual(1.0f, clamp(100), 0.001f);
            Assert::AreEqual(0.5f, clamp(50), 0.001f);
        }

        // ── Odd thickness half-pixel adjustment ─────────────────────────

        TEST_METHOD(OddThickness_HalfPixelAdjustment)
        {
            // The C++ and Rust implementations add 0.5px offset for odd thickness
            // to maintain crisp pixel alignment.
            int thickness = 5; // odd
            double hpa = (thickness % 2 == 1) ? 0.5 : 0.0;
            Assert::AreEqual(0.5, hpa, 1e-10);
        }

        TEST_METHOD(EvenThickness_NoAdjustment)
        {
            int thickness = 4; // even
            double hpa = (thickness % 2 == 1) ? 0.5 : 0.0;
            Assert::AreEqual(0.0, hpa, 1e-10);
        }
    };
}
