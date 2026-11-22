#pragma warning(push)
#pragma warning(disable : 26466)
#include "CppUnitTest.h"
#pragma warning(pop)

#include <cstdint>
#include <cstdlib>

#include "SmoothingCalculator.h"

using namespace Microsoft::VisualStudio::CppUnitTestFramework;
using ScrollAcceleration::SmoothingCalculator;
using ScrollAcceleration::SmoothingSettings;

namespace ScrollAccelerationUnitTests
{
    TEST_CLASS(SmoothingCalculatorTests)
    {
        static constexpr SmoothingSettings Enabled()
        {
            SmoothingSettings s;
            s.enabled = true;
            s.durationMs = 120;
            return s;
        }

        // Runs the timer until the queue is empty; returns the sum of what was delivered.
        static int Drain(SmoothingCalculator& m, uint32_t start, int* maxStep = nullptr, int* steps = nullptr)
        {
            int sum = 0;
            int count = 0;
            uint32_t t = start;
            for (int i = 0; i < 1000; ++i)
            {
                t += 16;
                const auto r = m.Step(t);
                sum += r.delta;
                if (maxStep && std::abs(r.delta) > *maxStep)
                {
                    *maxStep = std::abs(r.delta);
                }
                ++count;
                if (!r.active)
                {
                    break;
                }
            }
            if (steps)
            {
                *steps = count;
            }
            return sum;
        }

    public:
        TEST_METHOD(Disabled_QueuesNothing)
        {
            SmoothingSettings s = Enabled();
            s.enabled = false;
            SmoothingCalculator m(s);
            m.Add(480, 0);
            Assert::IsFalse(m.HasPending());
            Assert::AreEqual(0, m.Step(16).delta);
        }

        TEST_METHOD(TotalDeliveredEqualsQueued)
        {
            SmoothingCalculator m(Enabled());
            m.Add(480, 0);
            Assert::AreEqual(480, Drain(m, 0));
            Assert::IsFalse(m.HasPending());
        }

        TEST_METHOD(NegativeDirection_IsDeliveredFully)
        {
            SmoothingCalculator m(Enabled());
            m.Add(-360, 0);
            Assert::AreEqual(-360, Drain(m, 0));
        }

        TEST_METHOD(BigTick_IsSplitIntoSeveralSteps)
        {
            SmoothingCalculator m(Enabled());
            m.Add(960, 0);
            int maxStep = 0;
            int steps = 0;
            Drain(m, 0, &maxStep, &steps);
            Assert::IsTrue(steps >= 4);
            Assert::IsTrue(maxStep < 960);
        }

        TEST_METHOD(StepsDecayOverTime)
        {
            SmoothingCalculator m(Enabled());
            m.Add(960, 0);
            const int first = m.Step(16).delta;
            const int second = m.Step(32).delta;
            Assert::IsTrue(first > second);
            Assert::IsTrue(second > 0);
        }

        TEST_METHOD(AccumulatesWhileGliding)
        {
            SmoothingCalculator m(Enabled());
            m.Add(240, 0);
            const int first = m.Step(16).delta;
            m.Add(240, 20);
            int sum = first;
            sum += Drain(m, 20);
            Assert::AreEqual(480, sum);
        }

        TEST_METHOD(OppositeTick_DropsQueuedRemainder)
        {
            SmoothingCalculator m(Enabled());
            m.Add(960, 0);
            const int delivered = m.Step(16).delta;
            Assert::IsTrue(delivered > 0);
            m.Add(-120, 20);
            const int rest = Drain(m, 20);
            Assert::AreEqual(-120, rest);
        }

        TEST_METHOD(SmallDelta_IsStillDelivered)
        {
            SmoothingCalculator m(Enabled());
            m.Add(5, 0);
            Assert::AreEqual(5, Drain(m, 0));
        }

        TEST_METHOD(NeverExceedsInt16PerStep)
        {
            SmoothingCalculator m(Enabled());
            for (int i = 0; i < 20; ++i)
            {
                m.Add(32767, 0);
            }
            const auto r = m.Step(1000); // huge dt
            Assert::IsTrue(r.delta <= 32767 && r.delta >= -32767);
        }

        TEST_METHOD(HandlesTimerWrapAround)
        {
            SmoothingCalculator m(Enabled());
            m.Add(240, 0xFFFFFFF0u);
            Assert::IsTrue(m.Step(0xFFFFFFF8u).delta > 0);
            Assert::IsTrue(m.Step(8).delta >= 0);
        }

        TEST_METHOD(NowBehindLast_DoesNotCrash)
        {
            SmoothingCalculator m(Enabled());
            m.Add(240, 100);
            const auto r = m.Step(90);
            Assert::AreEqual(0, r.delta);
            Assert::IsTrue(r.active);
        }

        TEST_METHOD(SettingsAreSanitized)
        {
            SmoothingSettings s = Enabled();
            s.durationMs = 0;
            SmoothingCalculator m(s);
            m.Add(120, 0);
            Assert::AreEqual(120, Drain(m, 0)); // must not divide by zero
        }
    };
}
