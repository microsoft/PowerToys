#include <CppUnitTest.h>

#include "ZoomAnimation.h"

using namespace Microsoft::VisualStudio::CppUnitTestFramework;

namespace ZoomAnimationTests
{
    TEST_CLASS(ZoomAnimationStateTests)
    {
    public:
        TEST_METHOD(CompletesAtExactTarget)
        {
            ZoomAnimation animation;
            animation.Start(1.0f, 4.0f, 100, 180);

            Assert::AreEqual(1.0f, animation.Sample(100));
            Assert::AreEqual(4.0f, animation.Sample(280));
            Assert::IsFalse(animation.IsActive());
        }

        TEST_METHOD(ZoomInStartsAtOneWhileTargetRemainsAboveOne)
        {
            ZoomAnimation animation;
            animation.Start(1.0f, 2.0f, 100, 280);

            Assert::AreEqual(1.0f, animation.Sample(100));
            Assert::AreEqual(2.0f, animation.Target());
            Assert::IsTrue(animation.IsActive());
        }

        TEST_METHOD(ZoomInAndOutAreMonotonic)
        {
            ZoomAnimation zoomIn;
            zoomIn.Start(1.0f, 8.0f, 0, 180);
            float previousZoom = zoomIn.Sample(0);
            for (std::uint64_t time = 10; time <= 180; time += 10)
            {
                const auto zoom = zoomIn.Sample(time);
                Assert::IsTrue(zoom >= previousZoom);
                Assert::IsTrue(zoom <= 8.0f);
                previousZoom = zoom;
            }

            ZoomAnimation zoomOut;
            zoomOut.Start(8.0f, 1.0f, 0, 180);
            previousZoom = zoomOut.Sample(0);
            for (std::uint64_t time = 10; time <= 180; time += 10)
            {
                const auto zoom = zoomOut.Sample(time);
                Assert::IsTrue(zoom <= previousZoom);
                Assert::IsTrue(zoom >= 1.0f);
                previousZoom = zoom;
            }
        }

        TEST_METHOD(DelayedSampleCompletesWithoutOvershoot)
        {
            ZoomAnimation animation;
            animation.Start(2.0f, 32.0f, 500, 180);

            Assert::AreEqual(32.0f, animation.Sample(1000));
            Assert::IsFalse(animation.IsActive());
        }

        TEST_METHOD(RetargetingContinuesFromDisplayedZoom)
        {
            ZoomAnimation animation;
            animation.Start(1.0f, 8.0f, 0, 180);
            const auto displayedZoom = animation.Sample(60);

            animation.Start(displayedZoom, 2.0f, 60, 180);

            Assert::AreEqual(displayedZoom, animation.Sample(60));
            Assert::AreEqual(2.0f, animation.Sample(240));
        }

        TEST_METHOD(AdditionalZoomInsAnimateToHigherMagnification)
        {
            ZoomAnimation animation;
            animation.Start(1.0f, 2.0f, 0, 280);
            Assert::AreEqual(2.0f, animation.Sample(280));

            animation.Start(2.0f, 4.0f, 280, 320);
            const auto higherZoomIntermediateFrame = animation.Sample(320);

            Assert::IsTrue(higherZoomIntermediateFrame > 2.0f);
            Assert::IsTrue(higherZoomIntermediateFrame < 4.0f);
            Assert::IsTrue(animation.IsActive());
            Assert::AreEqual(4.0f, animation.Sample(600));
        }

        TEST_METHOD(InFlightAdditionalZoomInAnimatesFromDisplayedFrame)
        {
            ZoomAnimation animation;
            animation.Start(1.0f, 2.0f, 0, 280);
            const auto displayedZoom = animation.Sample(80);

            animation.Start(displayedZoom, 4.0f, 80, 320);
            Assert::AreEqual(displayedZoom, animation.Sample(80));

            const auto higherZoomIntermediateFrame = animation.Sample(120);
            Assert::IsTrue(higherZoomIntermediateFrame > displayedZoom);
            Assert::IsTrue(higherZoomIntermediateFrame < 4.0f);
            Assert::AreEqual(4.0f, animation.Sample(400));
        }

        TEST_METHOD(ConfiguredZoomLevelsRemainExactEndpoints)
        {
            constexpr float zoomLevels[] = { 1.25f, 1.50f, 1.75f, 2.00f, 3.00f, 4.00f };

            for (const auto zoomLevel : zoomLevels)
            {
                ZoomAnimation animation;
                animation.Start(1.0f, zoomLevel, 0, 180);

                Assert::AreEqual(zoomLevel, animation.Target());
                Assert::AreEqual(zoomLevel, animation.Sample(180));
            }
        }

        TEST_METHOD(OriginalZoomInDurationsArePreserved)
        {
            Assert::AreEqual<std::uint64_t>(40, ZoomAnimation::OriginalDuration(1.0f, 1.25f, true));
            Assert::AreEqual<std::uint64_t>(80, ZoomAnimation::OriginalDuration(1.0f, 1.50f, true));
            Assert::AreEqual<std::uint64_t>(100, ZoomAnimation::OriginalDuration(1.0f, 1.75f, true));
            Assert::AreEqual<std::uint64_t>(140, ZoomAnimation::OriginalDuration(1.0f, 2.00f, true));
            Assert::AreEqual<std::uint64_t>(220, ZoomAnimation::OriginalDuration(1.0f, 3.00f, true));
            Assert::AreEqual<std::uint64_t>(280, ZoomAnimation::OriginalDuration(1.0f, 4.00f, true));
        }

        TEST_METHOD(OriginalZoomOutDurationIsPreserved)
        {
            Assert::AreEqual<std::uint64_t>(140, ZoomAnimation::OriginalDuration(4.0f, 1.0f, false));
        }

        TEST_METHOD(OriginalLiveZoomTimingGateIsPreserved)
        {
            Assert::AreEqual<std::uint64_t>(280, ZoomAnimation::OriginalDuration(1.0f, 2.0f, true, 40));
            Assert::AreEqual<std::uint64_t>(160, ZoomAnimation::OriginalDuration(2.0f, 1.0f, false, 40));
        }

        TEST_METHOD(SmoothAnimationDurationIsExtendedByFiftyPercent)
        {
            Assert::AreEqual<std::uint64_t>(420, ZoomAnimation::Duration(1.0f, 2.0f, true));
            Assert::AreEqual<std::uint64_t>(420, ZoomAnimation::Duration(4.0f, 1.0f, false));
            Assert::AreEqual<std::uint64_t>(240, ZoomAnimation::Duration(2.0f, 1.0f, false));
        }
    };
}