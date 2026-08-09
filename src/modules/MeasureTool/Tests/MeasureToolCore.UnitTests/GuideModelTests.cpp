#pragma warning(push)
#pragma warning(disable : 26466)
#include "CppUnitTest.h"
#pragma warning(pop)

#include "GuideModel.h"
#include "GuideVisualMetrics.h"
#include "MeasurementTooltipStyle.h"

using namespace GuideModel;
using namespace Microsoft::VisualStudio::CppUnitTestFramework;

namespace MeasureToolCoreUnitTests
{
    namespace
    {
        constexpr Monitor Primary{ 1, RECT{ 0, 0, 1920, 1080 } };
        constexpr Monitor Left{ 2, RECT{ -1280, 0, 0, 1024 } };
        constexpr Monitor Above{ 3, RECT{ 0, -900, 1600, 0 } };
    }

    TEST_CLASS(GuideModelTests)
    {
    public:
        TEST_METHOD(PlacementCommitAndClearMaintainStableIds)
        {
            Collection collection;
            InteractionController interaction{ collection };

            interaction.BeginPlacement(Orientation::Horizontal, Primary, 120);
            const auto first = interaction.Commit();
            interaction.BeginPlacement(Orientation::Vertical, Primary, 240);
            const auto second = interaction.Commit();

            Assert::IsTrue(first.has_value());
            Assert::IsTrue(second.has_value());
            Assert::IsTrue(*second > *first);
            Assert::AreEqual(2u, static_cast<unsigned int>(collection.Guides().size()));

            collection.Clear();
            Assert::IsTrue(collection.Empty());
        }

        TEST_METHOD(CancelDoesNotMutatePlacementOrDrag)
        {
            Collection collection;
            const auto id = collection.Add(Orientation::Vertical, Primary, 300);
            InteractionController interaction{ collection };

            interaction.BeginPlacement(Orientation::Horizontal, Primary, 200);
            interaction.Cancel();
            Assert::AreEqual(1u, static_cast<unsigned int>(collection.Guides().size()));

            Assert::IsTrue(interaction.BeginDrag(id, Primary));
            interaction.Update(Left, 100, false);
            interaction.Cancel();

            const auto* guide = collection.Find(id);
            Assert::IsNotNull(guide);
            Assert::IsTrue(guide->monitorId == Primary.id);
            Assert::AreEqual(300, guide->coordinate);
        }

        TEST_METHOD(DragCanTransferOrRemoveGuide)
        {
            Collection collection;
            const auto id = collection.Add(Orientation::Vertical, Primary, 300);
            InteractionController interaction{ collection };

            Assert::IsTrue(interaction.BeginDrag(id, Primary));
            interaction.Update(Left, 75, false);
            const auto moved = interaction.Commit();

            Assert::IsTrue(moved.has_value());
            const auto* guide = collection.Find(id);
            Assert::IsNotNull(guide);
            Assert::IsTrue(guide->monitorId == Left.id);
            Assert::AreEqual(75, guide->coordinate);

            Assert::IsTrue(interaction.BeginDrag(id, Left));
            interaction.Update(Left, 0, true);
            Assert::IsFalse(interaction.Commit().has_value());
            Assert::IsNull(collection.Find(id));
        }

        TEST_METHOD(HitTestChoosesNearestAndNewestOverlappingGuide)
        {
            Collection collection;
            const auto first = collection.Add(Orientation::Horizontal, Primary, 100);
            const auto second = collection.Add(Orientation::Horizontal, Primary, 100);
            collection.Add(Orientation::Horizontal, Primary, 110);

            const auto overlap = collection.HitTest(POINT{ 400, 100 }, Primary, 8);
            Assert::IsTrue(overlap.has_value());
            Assert::IsTrue(overlap->id == second);

            const auto nearest = collection.HitTest(POINT{ 400, 108 }, Primary, 8);
            Assert::IsTrue(nearest.has_value());
            Assert::IsTrue(nearest->id != first);
            Assert::AreEqual(2, nearest->distance);
        }

        TEST_METHOD(NearestAxisEdgeUsesOnlyRelevantOrientation)
        {
            constexpr RECT edges{ 10, 20, 110, 220 };

            Assert::AreEqual(20, NearestAxisEdge(Orientation::Horizontal, edges, 30));
            Assert::AreEqual(220, NearestAxisEdge(Orientation::Horizontal, edges, 200));
            Assert::AreEqual(10, NearestAxisEdge(Orientation::Vertical, edges, 25));
            Assert::AreEqual(110, NearestAxisEdge(Orientation::Vertical, edges, 100));
        }

        TEST_METHOD(MagneticSnapAcquiresHoldsReleasesAndSwitchesTargets)
        {
            MagneticSnapController snap{ 8, 14 };

            Assert::IsFalse(snap.UpdateCandidate(100, 109, false).has_value());
            Assert::AreEqual(108, *snap.UpdateCandidate(100, 108, false));
            Assert::AreEqual(108, *snap.Track(122, false));
            Assert::IsFalse(snap.Track(123, false).has_value());

            Assert::AreEqual(130, *snap.UpdateCandidate(123, 130, false));
            Assert::AreEqual(130, *snap.UpdateCandidate(143, 144, false));
            Assert::AreEqual(144, *snap.UpdateCandidate(145, 144, false));
        }

        TEST_METHOD(MagneticSnapBypassImmediatelyReleasesTarget)
        {
            MagneticSnapController snap;

            Assert::AreEqual(200, *snap.UpdateCandidate(196, 200, false));
            Assert::IsFalse(snap.Track(196, true).has_value());
            Assert::IsFalse(snap.Track(196, false).has_value());
            Assert::AreEqual(200, *snap.UpdateCandidate(196, 200, false));
        }

        TEST_METHOD(CoordinatesHandleNegativeVirtualDesktopOrigins)
        {
            constexpr POINT systemPoint{ -1000, 400 };
            Assert::AreEqual(280, ToMonitorCoordinate(Orientation::Vertical, Left.bounds, systemPoint));
            Assert::AreEqual(400, ToMonitorCoordinate(Orientation::Horizontal, Left.bounds, systemPoint));

            const Guide guide{ 1, Orientation::Vertical, Left.id, 280 };
            Assert::AreEqual(-1000, ToSystemCoordinate(guide, Left.bounds));
        }

        TEST_METHOD(DismissalUsesOnlyExposedEdges)
        {
            const std::vector monitors{ Left, Primary, Above };

            Assert::IsFalse(IsEdgeExposed(DismissalEdge::Left, Primary, monitors));
            Assert::IsFalse(IsEdgeExposed(DismissalEdge::Top, Primary, monitors));
            Assert::IsTrue(IsEdgeExposed(DismissalEdge::Right, Primary, monitors));
            Assert::IsTrue(IsEdgeExposed(DismissalEdge::Bottom, Primary, monitors));

            Assert::IsTrue(
                GetDismissalEdge(Orientation::Vertical, POINT{ 1919, 500 }, Primary, monitors, 12) ==
                DismissalEdge::Right);
            Assert::IsTrue(
                GetDismissalEdge(Orientation::Vertical, POINT{ 0, 500 }, Primary, monitors, 12) ==
                DismissalEdge::None);
            Assert::IsTrue(
                GetDismissalEdge(Orientation::Horizontal, POINT{ 500, 1079 }, Primary, monitors, 12) ==
                DismissalEdge::Bottom);
        }

        TEST_METHOD(ClampCoordinateUsesPhysicalMonitorAxis)
        {
            Assert::AreEqual(0, ClampCoordinate(Orientation::Horizontal, Primary.bounds, -20));
            Assert::AreEqual(1079, ClampCoordinate(Orientation::Horizontal, Primary.bounds, 5000));
            Assert::AreEqual(1919, ClampCoordinate(Orientation::Vertical, Primary.bounds, 5000));
        }

        TEST_METHOD(DistanceSegmentsIncludeScreenEdgesAndGuideIntervals)
        {
            const std::vector guides{
                Guide{ 1, Orientation::Vertical, Primary.id, 300 },
                Guide{ 2, Orientation::Vertical, Primary.id, 900 },
                Guide{ 3, Orientation::Horizontal, Primary.id, 120 },
            };

            const auto segments = GetDistanceSegments(guides, Primary);

            Assert::AreEqual(5u, static_cast<unsigned int>(segments.size()));
            Assert::IsTrue(segments[0].orientation == Orientation::Vertical);
            Assert::AreEqual(0, segments[0].startCoordinate);
            Assert::AreEqual(300, segments[0].endCoordinate);
            Assert::AreEqual(300, segments[0].Length());
            Assert::AreEqual(600, segments[1].Length());
            Assert::AreEqual(1020, segments[2].Length());
            Assert::IsTrue(segments[3].orientation == Orientation::Horizontal);
            Assert::AreEqual(120, segments[3].Length());
            Assert::AreEqual(960, segments[4].Length());
        }

        TEST_METHOD(DistanceSegmentsIgnoreOtherMonitorsAndDuplicateCoordinates)
        {
            const std::vector guides{
                Guide{ 1, Orientation::Vertical, Primary.id, 0 },
                Guide{ 2, Orientation::Vertical, Primary.id, 400 },
                Guide{ 3, Orientation::Vertical, Primary.id, 400 },
                Guide{ 4, Orientation::Vertical, Left.id, 200 },
            };

            const auto segments = GetDistanceSegments(guides, Primary);

            Assert::AreEqual(2u, static_cast<unsigned int>(segments.size()));
            Assert::AreEqual(400, segments[0].Length());
            Assert::AreEqual(1520, segments[1].Length());
            Assert::IsTrue(GetDistanceSegments({}, Primary).empty());
        }

        TEST_METHOD(GuideLabelMetricsScaleWithMonitorDpi)
        {
            const auto defaultMetrics = GuideVisualMetrics::LabelForDpi(96);
            Assert::AreEqual(14.0f, defaultMetrics.fontSize, 0.001f);
            Assert::AreEqual(8.0f, defaultMetrics.horizontalTextInset, 0.001f);
            Assert::AreEqual(4.0f, defaultMetrics.verticalTextInset, 0.001f);

            const auto scaledMetrics = GuideVisualMetrics::LabelForDpi(144);
            Assert::AreEqual(1.5f, scaledMetrics.scale, 0.001f);
            Assert::AreEqual(21.0f, scaledMetrics.fontSize, 0.001f);
            Assert::AreEqual(12.0f, scaledMetrics.cornerRadius, 0.001f);

            const auto fallbackMetrics = GuideVisualMetrics::LabelForDpi(0);
            Assert::AreEqual(defaultMetrics.fontSize, fallbackMetrics.fontSize, 0.001f);
        }

        TEST_METHOD(GuideLabelMetricsSizeToContent)
        {
            const auto defaultMetrics = GuideVisualMetrics::SizeToContent(
                GuideVisualMetrics::LabelForDpi(96),
                52.0f,
                16.0f);
            Assert::AreEqual(68.0f, defaultMetrics.width, 0.001f);
            Assert::AreEqual(24.0f, defaultMetrics.height, 0.001f);

            const auto scaledMetrics = GuideVisualMetrics::SizeToContent(
                GuideVisualMetrics::LabelForDpi(144),
                78.0f,
                24.0f);
            Assert::AreEqual(102.0f, scaledMetrics.width, 0.001f);
            Assert::AreEqual(36.0f, scaledMetrics.height, 0.001f);
        }

        TEST_METHOD(GuideLabelPaletteMatchesMeasurementTooltips)
        {
            const auto light = MeasurementTooltipStyle::PaletteForTheme(false);
            Assert::AreEqual(0.0f, light.foreground.red, 0.001f);
            Assert::AreEqual(light.foreground.red, light.secondaryForeground.red, 0.001f);
            Assert::AreEqual(
                MeasurementTooltipStyle::SecondaryTextOpacity,
                light.secondaryForeground.alpha,
                0.001f);
            Assert::AreEqual(0.96f, light.background.red, 0.001f);
            Assert::AreEqual(0.93f, light.background.alpha, 0.001f);
            Assert::AreEqual(0.44f, light.border.red, 0.001f);
            Assert::AreEqual(0.4f, light.border.alpha, 0.001f);

            const auto dark = MeasurementTooltipStyle::PaletteForTheme(true);
            Assert::AreEqual(1.0f, dark.foreground.red, 0.001f);
            Assert::AreEqual(dark.foreground.red, dark.secondaryForeground.red, 0.001f);
            Assert::AreEqual(
                MeasurementTooltipStyle::SecondaryTextOpacity,
                dark.secondaryForeground.alpha,
                0.001f);
            Assert::AreEqual(0.17f, dark.background.red, 0.001f);
            Assert::AreEqual(0.93f, dark.background.alpha, 0.001f);
            Assert::AreEqual(light.border.red, dark.border.red, 0.001f);
            Assert::AreEqual(light.border.alpha, dark.border.alpha, 0.001f);
        }

        TEST_METHOD(MeasurementTooltipPrefersTopOutsideSelection)
        {
            const auto placement = MeasurementTooltipStyle::PlaceOutsideSelection(
                { 400.0f, 300.0f, 600.0f, 500.0f },
                { 120.0f, 32.0f },
                { 1000.0f, 800.0f });

            Assert::IsTrue(placement.side == MeasurementTooltipStyle::PlacementSide::Top);
            Assert::AreEqual(500.0f, placement.center.x, 0.001f);
            Assert::AreEqual(276.0f, placement.center.y, 0.001f);
        }

        TEST_METHOD(MeasurementTooltipFallsBackToVisibleSides)
        {
            const auto bottomPlacement = MeasurementTooltipStyle::PlaceOutsideSelection(
                { 400.0f, 10.0f, 600.0f, 210.0f },
                { 120.0f, 32.0f },
                { 1000.0f, 800.0f });
            Assert::IsTrue(bottomPlacement.side == MeasurementTooltipStyle::PlacementSide::Bottom);

            const auto rightPlacement = MeasurementTooltipStyle::PlaceOutsideSelection(
                { 300.0f, 10.0f, 500.0f, 790.0f },
                { 120.0f, 32.0f },
                { 1000.0f, 800.0f });
            Assert::IsTrue(rightPlacement.side == MeasurementTooltipStyle::PlacementSide::Right);
        }

        TEST_METHOD(MeasurementTooltipClampsAlongViewportEdge)
        {
            const auto placement = MeasurementTooltipStyle::PlaceOutsideSelection(
                { 0.0f, 200.0f, 30.0f, 300.0f },
                { 120.0f, 32.0f },
                { 1000.0f, 800.0f });

            Assert::IsTrue(placement.side == MeasurementTooltipStyle::PlacementSide::Top);
            Assert::AreEqual(60.0f, placement.center.x, 0.001f);
        }

        TEST_METHOD(MeasurementTooltipRemainsVisibleWhenNoOutsideSideFits)
        {
            const auto placement = MeasurementTooltipStyle::PlaceOutsideSelection(
                { 0.0f, 0.0f, 1000.0f, 800.0f },
                { 120.0f, 32.0f },
                { 1000.0f, 800.0f });

            Assert::AreEqual(16.0f, placement.center.y, 0.001f);
        }
    };
}
