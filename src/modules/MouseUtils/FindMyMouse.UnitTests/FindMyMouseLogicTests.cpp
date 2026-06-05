#include "pch.h"

#include <FindMyMouseLogic.h>

#include <vector>

using namespace Microsoft::VisualStudio::CppUnitTestFramework;

namespace FindMyMouseUnitTests
{
    TEST_CLASS(FindMyMouseLogicTests)
    {
    public:
        TEST_METHOD(ShouldActivateFromShake_ActivatesForBackAndForthMovementButNotStraightMovement)
        {
            using FindMyMouseLogic::PointerRecentMovement;

            constexpr int minimumShakeDistance = 1000;
            constexpr int shakeFactorPercent = 400;
            const std::vector<PointerRecentMovement> backAndForthMovement{
                { { 500, 0 }, 0 },
                { { -500, 0 }, 1 },
                { { 500, 0 }, 2 },
                { { -500, 0 }, 3 },
                { { 500, 0 }, 4 },
            };
            const std::vector<PointerRecentMovement> straightLineMovement{
                { { 2500, 0 }, 0 },
            };

            Assert::IsTrue(FindMyMouseLogic::ShouldActivateFromShake(backAndForthMovement, minimumShakeDistance, shakeFactorPercent));
            Assert::IsFalse(FindMyMouseLogic::ShouldActivateFromShake(straightLineMovement, minimumShakeDistance, shakeFactorPercent));
        }
    };
}
