#pragma warning(push)
#pragma warning(disable : 26466)
#include "CppUnitTest.h"
#pragma warning(pop)

#include <cstdint>
#include <limits>

#include "AccelerationCalculator.h"

using namespace Microsoft::VisualStudio::CppUnitTestFramework;
using ScrollAcceleration::AccelerationCalculator;
using ScrollAcceleration::AccelerationSettings;

namespace ScrollAccelerationUnitTests
{
    TEST_CLASS(AccelerationCalculatorTests)
    {
        static constexpr AccelerationSettings MakeSettings()
        {
            AccelerationSettings s;
            s.timeThresholdMs = 100;
            s.acceleration = 0.5;
            s.maxMultiplier = 4.0;
            return s;
        }

    public:
        TEST_METHOD(FirstTick_IsNotAccelerated)
        {
            AccelerationCalculator calc(MakeSettings());
            Assert::AreEqual(120, calc.Scale(120, 1000));
        }

        static constexpr AccelerationSettings MakePreciseSettings()
        {
            AccelerationSettings s;
            s.timeThresholdMs = 100;
            s.acceleration = 0.5;
            s.maxMultiplier = 4.0;
            s.baseMultiplier = 0.25;
            return s;
        }

        TEST_METHOD(BaseMultiplierBelowOne_MakesSlowTickPrecise)
        {
            AccelerationCalculator calc(MakePreciseSettings());
            Assert::AreEqual(30, calc.Scale(120, 1000)); // 120 * 0.25
            Assert::AreEqual(30, calc.Scale(120, 1500)); // still a slow tick
            Assert::AreEqual(-30, calc.Scale(-120, 3000));
        }

        TEST_METHOD(BaseMultiplier_RampsUpWithFastTicks)
        {
            AccelerationCalculator calc(MakePreciseSettings());
            Assert::AreEqual(30, calc.Scale(120, 1000)); // 0.25
            Assert::AreEqual(90, calc.Scale(120, 1050)); // 0.75
            Assert::AreEqual(150, calc.Scale(120, 1100)); // 1.25
            Assert::AreEqual(210, calc.Scale(120, 1150)); // 1.75
        }

        TEST_METHOD(BaseMultiplier_NeverDropsATickToZero)
        {
            AccelerationSettings s = MakePreciseSettings();
            s.baseMultiplier = 0.01;
            AccelerationCalculator calc(s);
            Assert::AreEqual(1, calc.Scale(5, 1000));
            Assert::AreEqual(-1, calc.Scale(-5, 5000));
        }

        TEST_METHOD(BaseMultiplier_InvalidValuesFallBackToOne)
        {
            AccelerationSettings s = MakeSettings();
            s.baseMultiplier = std::numeric_limits<double>::quiet_NaN();
            AccelerationCalculator calc(s);
            Assert::AreEqual(120, calc.Scale(120, 1000));
            s.baseMultiplier = -3.0;
            calc.SetSettings(s);
            Assert::AreEqual(120, calc.Scale(120, 5000));
        }

        TEST_METHOD(SlowTicks_AreNotAccelerated)
        {
            AccelerationCalculator calc(MakeSettings());
            Assert::AreEqual(120, calc.Scale(120, 1000));
            Assert::AreEqual(120, calc.Scale(120, 1500));
            Assert::AreEqual(120, calc.Scale(120, 2200));
        }

        TEST_METHOD(FastTicks_AccelerateProgressively)
        {
            AccelerationCalculator calc(MakeSettings());
            Assert::AreEqual(120, calc.Scale(120, 1000)); // streak 0 -> x1.0
            Assert::AreEqual(180, calc.Scale(120, 1050)); // streak 1 -> x1.5
            Assert::AreEqual(240, calc.Scale(120, 1100)); // streak 2 -> x2.0
        }

        TEST_METHOD(Multiplier_IsCappedAtMax)
        {
            AccelerationCalculator calc(MakeSettings());
            int last = 0;
            for (uint32_t t = 1000; t < 1000 + 50 * 40; t += 50)
            {
                last = calc.Scale(120, t);
            }
            Assert::AreEqual(480, last); // 120 * 4.0
        }

        TEST_METHOD(DirectionChange_ResetsStreak)
        {
            AccelerationCalculator calc(MakeSettings());
            calc.Scale(120, 1000);
            calc.Scale(120, 1050);
            Assert::AreEqual(-120, calc.Scale(-120, 1100));
            Assert::AreEqual(-180, calc.Scale(-120, 1150));
        }

        TEST_METHOD(Pause_ResetsStreak)
        {
            AccelerationCalculator calc(MakeSettings());
            calc.Scale(120, 1000);
            calc.Scale(120, 1050);
            Assert::AreEqual(120, calc.Scale(120, 1400));
        }

        TEST_METHOD(NegativeDelta_ScalesSymmetrically)
        {
            AccelerationCalculator calc(MakeSettings());
            calc.Scale(-120, 1000);
            Assert::AreEqual(-180, calc.Scale(-120, 1050));
        }

        TEST_METHOD(TimerWraparound_IsHandled)
        {
            AccelerationCalculator calc(MakeSettings());
            calc.Scale(120, 0xFFFFFFF0u);
            // 0x20 ms later, after the 32-bit counter wrapped.
            Assert::AreEqual(180, calc.Scale(120, 0x10u));
        }

        TEST_METHOD(ZeroDelta_IsReturnedUnchanged)
        {
            AccelerationCalculator calc(MakeSettings());
            Assert::AreEqual(0, calc.Scale(0, 1000));
        }

        TEST_METHOD(ZeroAcceleration_DisablesScaling)
        {
            AccelerationSettings s = MakeSettings();
            s.acceleration = 0.0;
            AccelerationCalculator calc(s);
            calc.Scale(120, 1000);
            Assert::AreEqual(120, calc.Scale(120, 1010));
            Assert::AreEqual(120, calc.Scale(120, 1020));
        }

        TEST_METHOD(ScaledDelta_IsClampedToInt16)
        {
            AccelerationSettings s = MakeSettings();
            s.acceleration = 1000.0;
            s.maxMultiplier = 5000.0;
            AccelerationCalculator calc(s);
            calc.Scale(120, 1000);
            Assert::AreEqual(32767, calc.Scale(120, 1010));

            AccelerationCalculator negative(s);
            negative.Scale(-120, 1000);
            Assert::AreEqual(-32767, negative.Scale(-120, 1010));
        }

        TEST_METHOD(SetSettings_ResetsState)
        {
            AccelerationCalculator calc(MakeSettings());
            calc.Scale(120, 1000);
            calc.Scale(120, 1050);
            calc.SetSettings(MakeSettings());
            Assert::AreEqual(120, calc.Scale(120, 1100));
        }

        TEST_METHOD(MaxMultiplierBelowOne_IsTreatedAsOne)
        {
            AccelerationSettings s = MakeSettings();
            s.maxMultiplier = 0.2;
            AccelerationCalculator calc(s);
            calc.Scale(120, 1000);
            Assert::AreEqual(120, calc.Scale(120, 1050));
        }

        TEST_METHOD(NonFiniteSettings_NeverProduceNaNOrOverflow)
        {
            // Found by the fuzzer: acceleration = +inf with streak 0 evaluated inf * 0 = NaN.
            constexpr double inf = std::numeric_limits<double>::infinity();
            constexpr double nan = std::numeric_limits<double>::quiet_NaN();
            for (double accel : { inf, -inf, nan, 1.0e300 })
            {
                for (double maxMul : { inf, -inf, nan, 1.0e300 })
                {
                    AccelerationSettings s = MakeSettings();
                    s.acceleration = accel;
                    s.maxMultiplier = maxMul;
                    AccelerationCalculator calc(s);
                    for (uint32_t t = 1000; t < 1000 + 50 * 10; t += 50)
                    {
                        const int scaled = calc.Scale(120, t);
                        Assert::IsTrue(scaled >= 120 && scaled <= 32767, L"positive delta must stay positive and in range");
                    }
                }
            }
        }
    };
}
