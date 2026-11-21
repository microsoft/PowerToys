#include "pch.h"
#include "TestHelpers.h"
#include <WindowBorder/BorderGeometry.h>

using namespace Microsoft::VisualStudio::CppUnitTestFramework;
using namespace PowerToysWindowBorder;

namespace UnitTestsCommonUtils
{
    namespace
    {
        constexpr float Tolerance = 0.001f;

        constexpr LayoutInput Input(Placement placement, float width, float height, float radius, float thickness, bool halo = false, bool highlight = false, float scale = 1.f)
        {
            return LayoutInput{
                .frameWidth = width,
                .frameHeight = height,
                .cornerRadius = radius,
                .thickness = thickness,
                .scale = scale,
                .placement = placement,
                .halo = halo,
                .highlight = highlight,
            };
        }

        // Outer edge of a stroke, measured from the host's left edge.
        constexpr float OuterLeft(const Stroke& stroke)
        {
            return stroke.rect.left - (stroke.thickness / 2.f);
        }

        constexpr float OuterRadius(const Stroke& stroke)
        {
            return stroke.rect.radius + (stroke.thickness / 2.f);
        }

        constexpr float InnerRadius(const Stroke& stroke)
        {
            return stroke.rect.radius - (stroke.thickness / 2.f);
        }
    }

    TEST_CLASS(WindowBorderGeometryTests)
    {
    public:
        TEST_METHOD(Outside_BandHugsFrameWithConcentricCorners)
        {
            const auto layout = ComputeLayout(Input(Placement::Outside, 200.f, 100.f, 8.f, 4.f));

            Assert::AreEqual(4, layout.inflate);
            Assert::AreEqual(208, layout.hostWidth);
            Assert::AreEqual(108, layout.hostHeight);
            Assert::IsTrue(layout.band.IsVisible());
            Assert::AreEqual(0.f, OuterLeft(layout.band), Tolerance);
            Assert::AreEqual(204.f, layout.band.rect.width, Tolerance);
            // Inner edge matches the window's radius; outer edge grows by the band thickness.
            Assert::AreEqual(8.f, InnerRadius(layout.band), Tolerance);
            Assert::AreEqual(12.f, OuterRadius(layout.band), Tolerance);
        }

        TEST_METHOD(Outside_SquareWindowKeepsSquareBand)
        {
            const auto layout = ComputeLayout(Input(Placement::Outside, 200.f, 100.f, 0.f, 6.f));

            Assert::AreEqual(0.f, layout.band.rect.radius, Tolerance);
            Assert::AreEqual(6, layout.inflate);
        }

        TEST_METHOD(Inside_BandStaysWithinVisibleFrame)
        {
            const auto layout = ComputeLayout(Input(Placement::Inside, 200.f, 100.f, 8.f, 4.f));

            Assert::AreEqual(0, layout.inflate);
            Assert::AreEqual(200, layout.hostWidth);
            Assert::AreEqual(0.f, OuterLeft(layout.band), Tolerance);
            Assert::AreEqual(196.f, layout.band.rect.width, Tolerance);
            Assert::AreEqual(8.f, OuterRadius(layout.band), Tolerance);
            Assert::AreEqual(4.f, InnerRadius(layout.band), Tolerance);
        }

        TEST_METHOD(Inside_ThicknessClampedForTinyFrames)
        {
            const auto layout = ComputeLayout(Input(Placement::Inside, 6.f, 100.f, 0.f, 10.f));

            Assert::AreEqual(3.f, layout.band.thickness, Tolerance);
            Assert::IsFalse(layout.band.rect.IsEmpty());
        }

        TEST_METHOD(Halo_ExtendsHostAndFadesOutward)
        {
            const auto layout = ComputeLayout(Input(Placement::Outside, 200.f, 100.f, 8.f, 4.f, true, false, 1.5f));

            Assert::AreEqual(20, layout.inflate); // ceil(4 + 10 DIP * 1.5 + stroke overlap)
            float previousAlpha = 2.f;
            float previousRadius = OuterRadius(layout.band);
            for (const auto& stroke : layout.halo)
            {
                Assert::IsTrue(stroke.IsVisible());
                Assert::IsTrue(stroke.alpha < previousAlpha);
                Assert::IsTrue(stroke.rect.radius > previousRadius - stroke.thickness);
                // Never reaches outside the host window.
                Assert::IsTrue(OuterLeft(stroke) >= -Tolerance);
                previousAlpha = stroke.alpha;
                previousRadius = stroke.rect.radius;
            }
        }

        TEST_METHOD(Halo_Disabled_LeavesHaloEmpty)
        {
            const auto layout = ComputeLayout(Input(Placement::Outside, 200.f, 100.f, 8.f, 4.f));

            for (const auto& stroke : layout.halo)
            {
                Assert::IsFalse(stroke.IsVisible());
            }
        }

        TEST_METHOD(Highlight_SitsOnBandOuterEdge)
        {
            const auto layout = ComputeLayout(Input(Placement::Outside, 200.f, 100.f, 8.f, 4.f, false, true));

            Assert::IsTrue(layout.highlight.IsVisible());
            Assert::AreEqual(1.f, layout.highlight.thickness, Tolerance);
            Assert::AreEqual(OuterLeft(layout.band), OuterLeft(layout.highlight), Tolerance);
            Assert::AreEqual(OuterRadius(layout.band), OuterRadius(layout.highlight), Tolerance);
        }

        TEST_METHOD(Highlight_SkippedForThinBand)
        {
            const auto layout = ComputeLayout(Input(Placement::Outside, 200.f, 100.f, 8.f, 2.f, false, true));

            Assert::IsFalse(layout.highlight.IsVisible());
        }

        TEST_METHOD(EmptyFrame_ProducesNothing)
        {
            const auto layout = ComputeLayout(Input(Placement::Outside, 0.f, 100.f, 8.f, 4.f, true, true));

            Assert::AreEqual(0, layout.hostWidth);
            Assert::IsFalse(layout.band.IsVisible());
        }

        TEST_METHOD(ZeroThickness_DrawsNoBand)
        {
            const auto layout = ComputeLayout(Input(Placement::Outside, 200.f, 100.f, 8.f, 0.f, true, true));

            Assert::IsFalse(layout.band.IsVisible());
            Assert::IsFalse(layout.highlight.IsVisible());
        }

        TEST_METHOD(Lighten_BlendsTowardWhite)
        {
            Assert::AreEqual(static_cast<int>(128), static_cast<int>(Lighten(0, 0.5f)));
            Assert::AreEqual(static_cast<int>(255), static_cast<int>(Lighten(255, 0.5f)));
            Assert::AreEqual(static_cast<int>(100), static_cast<int>(Lighten(100, 0.f)));
        }
    };
}
