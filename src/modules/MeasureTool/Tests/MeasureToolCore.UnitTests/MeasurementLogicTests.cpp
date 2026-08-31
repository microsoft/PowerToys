#pragma warning(push)
#pragma warning(disable : 26466)
#include "CppUnitTest.h"
#pragma warning(pop)

#include "MeasurementLogic.h"

using namespace Microsoft::VisualStudio::CppUnitTestFramework;

namespace MeasureToolCoreUnitTests
{
    namespace
    {
        constexpr float Tolerance = 0.0001f;
    }

    TEST_CLASS (MeasurementLogicTests)
    {
    public:
        TEST_METHOD (PixelsAreUnchangedAtEveryScale)
        {
            for (const float dpi : { 96.0f, 120.0f, 144.0f, 192.0f })
            {
                Assert::AreEqual(
                    150.0f,
                    MeasurementLogic::Convert(150.0f, MeasurementLogic::Unit::Pixel, -1.0f, dpi),
                    Tolerance);
            }
        }

        TEST_METHOD (DipConversionUsesMonitorDpi)
        {
            const struct
            {
                float dpi;
                float expected;
            } testCases[] = {
                { 96.0f, 150.0f },
                { 120.0f, 120.0f },
                { 144.0f, 100.0f },
                { 192.0f, 75.0f },
            };

            for (const auto& testCase : testCases)
            {
                Assert::AreEqual(
                    testCase.expected,
                    MeasurementLogic::Convert(150.0f, MeasurementLogic::Unit::Dip, -1.0f, testCase.dpi),
                    Tolerance);
            }
        }

        TEST_METHOD (PhysicalUnitFallbackUses96Dpi)
        {
            Assert::AreEqual(
                1.0f,
                MeasurementLogic::Convert(96.0f, MeasurementLogic::Unit::Inch, -1.0f, 96.0f),
                Tolerance);
            Assert::AreEqual(
                2.54f,
                MeasurementLogic::Convert(96.0f, MeasurementLogic::Unit::Centimetre, -1.0f, 96.0f),
                Tolerance);
            Assert::AreEqual(
                25.4f,
                MeasurementLogic::Convert(96.0f, MeasurementLogic::Unit::Millimetre, -1.0f, 96.0f),
                Tolerance);
        }

        TEST_METHOD (PhysicalMonitorRatioRemainsPreferred)
        {
            Assert::AreEqual(
                1.0f,
                MeasurementLogic::Convert(100.0f, MeasurementLogic::Unit::Inch, 0.254f, 192.0f),
                Tolerance);
            Assert::AreEqual(
                2.54f,
                MeasurementLogic::Convert(100.0f, MeasurementLogic::Unit::Centimetre, 0.254f, 192.0f),
                Tolerance);
            Assert::AreEqual(
                25.4f,
                MeasurementLogic::Convert(100.0f, MeasurementLogic::Unit::Millimetre, 0.254f, 192.0f),
                Tolerance);
        }

        TEST_METHOD (UnitIndexesPreservePersistedMeanings)
        {
            Assert::IsTrue(MeasurementLogic::GetUnitFromIndex(0) == MeasurementLogic::Unit::Pixel);
            Assert::IsTrue(MeasurementLogic::GetUnitFromIndex(1) == MeasurementLogic::Unit::Inch);
            Assert::IsTrue(MeasurementLogic::GetUnitFromIndex(2) == MeasurementLogic::Unit::Centimetre);
            Assert::IsTrue(MeasurementLogic::GetUnitFromIndex(3) == MeasurementLogic::Unit::Millimetre);
            Assert::IsTrue(MeasurementLogic::GetUnitFromIndex(4) == MeasurementLogic::Unit::Dip);
            Assert::IsTrue(MeasurementLogic::GetUnitFromIndex(5) == MeasurementLogic::Unit::Pixel);
        }

    };
}
