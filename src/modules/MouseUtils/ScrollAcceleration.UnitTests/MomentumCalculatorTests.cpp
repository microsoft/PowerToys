#pragma warning(push)
#pragma warning(disable : 26466)
#include "CppUnitTest.h"
#pragma warning(pop)

#include <cstdint>

#include "MomentumCalculator.h"

using namespace Microsoft::VisualStudio::CppUnitTestFramework;
using ScrollAcceleration::MomentumCalculator;
using ScrollAcceleration::MomentumSettings;

namespace ScrollAccelerationUnitTests
{
    TEST_CLASS(MomentumCalculatorTests)
    {
        static constexpr MomentumSettings Enabled()
        {
            MomentumSettings s;
            s.enabled = true;
            s.durationMs = 600;
            s.startDelayMs = 100;
            return s;
        }

        // Three fast ticks, 40 ms apart, ending at t = 80.
        static void SpinFast(MomentumCalculator& m, int delta = 240)
        {
            m.OnTick(delta, 0);
            m.OnTick(delta, 40);
            m.OnTick(delta, 80);
        }

    public:
        TEST_METHOD(Disabled_NeverCoasts)
        {
            MomentumSettings s = Enabled();
            s.enabled = false;
            MomentumCalculator m(s);
            SpinFast(m);
            const auto r = m.Step(500);
            Assert::AreEqual(0, r.delta);
            Assert::IsFalse(r.active);
        }

        TEST_METHOD(SlowTicks_DoNotCoast)
        {
            MomentumCalculator m(Enabled());
            m.OnTick(120, 0);
            m.OnTick(120, 400);
            m.OnTick(120, 800);
            Assert::IsFalse(m.Step(1000).active);
        }

        TEST_METHOD(TwoFastTicks_NotEnough)
        {
            MomentumCalculator m(Enabled());
            m.OnTick(120, 0);
            m.OnTick(120, 40);
            Assert::IsFalse(m.Step(300).active);
        }

        TEST_METHOD(WaitsForStartDelay_ThenCoastsAndDecays)
        {
            MomentumCalculator m(Enabled());
            SpinFast(m);

            auto r = m.Step(100); // 20 ms after the last tick: still waiting
            Assert::IsTrue(r.active);
            Assert::AreEqual(0, r.delta);

            r = m.Step(200); // delay elapsed: coasting starts
            Assert::IsTrue(r.active);
            Assert::IsTrue(m.IsCoasting());

            r = m.Step(216);
            Assert::IsTrue(r.delta > 0);

            const int early = r.delta;
            int late = 0;
            for (uint32_t t = 232; t <= 560; t += 16)
            {
                late = m.Step(t).delta;
            }
            Assert::IsTrue(late < early);
        }

        TEST_METHOD(EventuallyStops)
        {
            MomentumCalculator m(Enabled());
            SpinFast(m);
            bool active = true;
            uint32_t t = 200;
            for (int i = 0; i < 200 && active; ++i, t += 16)
            {
                active = m.Step(t).active;
            }
            Assert::IsFalse(active);
            Assert::IsFalse(m.IsCoasting());
            Assert::AreEqual(0, m.Step(t + 16).delta);
        }

        TEST_METHOD(DirectionIsPreserved)
        {
            MomentumCalculator m(Enabled());
            SpinFast(m, -240);
            m.Step(200);
            Assert::IsTrue(m.Step(216).delta < 0);
        }

        TEST_METHOD(NewTick_CancelsCoasting)
        {
            MomentumCalculator m(Enabled());
            SpinFast(m);
            m.Step(200);
            m.Step(216);
            Assert::IsTrue(m.IsCoasting());
            m.OnTick(120, 230);
            Assert::IsFalse(m.IsCoasting());
            Assert::AreEqual(0, m.Step(240).delta);
        }

        TEST_METHOD(Cancel_StopsCoasting)
        {
            MomentumCalculator m(Enabled());
            SpinFast(m);
            m.Step(200);
            m.Cancel();
            Assert::IsFalse(m.Step(216).active);
        }

        TEST_METHOD(DirectionChange_ResetsVelocity)
        {
            MomentumCalculator m(Enabled());
            SpinFast(m);
            m.OnTick(-120, 100);
            Assert::IsFalse(m.Step(400).active);
        }

        TEST_METHOD(HandlesTimerWrapAround)
        {
            MomentumCalculator m(Enabled());
            m.OnTick(240, 0xFFFFFFE0u);
            m.OnTick(240, 8);
            m.OnTick(240, 48);
            Assert::IsTrue(m.Step(60).active);
            m.Step(200);
            Assert::IsTrue(m.Step(216).delta > 0);
        }

        TEST_METHOD(NowBehindLastTick_DoesNotCrashOrCoastEarly)
        {
            MomentumCalculator m(Enabled());
            SpinFast(m);
            const auto r = m.Step(78); // timer source a couple of ms behind the hook's timestamp
            Assert::IsTrue(r.active);
            Assert::AreEqual(0, r.delta);
            Assert::IsFalse(m.IsCoasting());
        }

        TEST_METHOD(DeltaNeverExceedsInt16)
        {
            MomentumCalculator m(Enabled());
            m.OnTick(32767, 0);
            m.OnTick(32767, 1);
            m.OnTick(32767, 2);
            m.Step(200);
            const auto r = m.Step(1000); // huge dt
            Assert::IsTrue(r.delta <= 32767 && r.delta >= -32767);
        }

        TEST_METHOD(SettingsAreSanitized)
        {
            MomentumSettings s = Enabled();
            s.durationMs = 0;
            s.startDelayMs = 0;
            MomentumCalculator m(s);
            SpinFast(m);
            m.Step(500);
            m.Step(516); // must not divide by zero or hang
            Assert::IsTrue(true);
        }
    };
}
