#include "pch.h"

#include <common/version/helper.h>

using namespace Microsoft::VisualStudio::CppUnitTestFramework;

namespace Microsoft::VisualStudio::CppUnitTestFramework
{
    template<>
    inline std::wstring ToString<VersionHelper>(const VersionHelper& v)
    {
        return v.toWstring();
    }
}

namespace UnitTestsVersionHelper
{
    const size_t MAJOR_VERSION_0 = 0;
    const size_t MINOR_VERSION_12 = 12;
    const size_t REVISION_VERSION_0 = 0;

    TEST_CLASS (UnitTestsVersionHelper)
    {
    public:
        TEST_METHOD (integerConstructorShouldProperlyInitializationVersionNumbers)
        {
            VersionHelper sut(MAJOR_VERSION_0, MINOR_VERSION_12, REVISION_VERSION_0);

            Assert::AreEqual(MAJOR_VERSION_0, sut.major);
            Assert::AreEqual(MINOR_VERSION_12, sut.minor);
            Assert::AreEqual(REVISION_VERSION_0, sut.revision);
            Assert::AreEqual(0ull, sut.build);
        }
        TEST_METHOD (integerConstructorShouldProperlyInitializationWithDifferentVersionNumbers)
        {
            const size_t testcaseMajor = 2;
            const size_t testcaseMinor = 25;
            const size_t testcaseRevision = 1;
            VersionHelper sut(testcaseMajor, testcaseMinor, testcaseRevision);

            Assert::AreEqual(testcaseMajor, sut.major);
            Assert::AreEqual(testcaseMinor, sut.minor);
            Assert::AreEqual(testcaseRevision, sut.revision);
            Assert::AreEqual(0ull, sut.build);
        }
        TEST_METHOD (integerConstructorShouldProperlyInitializeBuildVersionNumber)
        {
            VersionHelper sut(0, 100, 2607, 8001);

            Assert::AreEqual(0ull, sut.major);
            Assert::AreEqual(100ull, sut.minor);
            Assert::AreEqual(2607ull, sut.revision);
            Assert::AreEqual(8001ull, sut.build);
        }
        TEST_METHOD (stringConstructorShouldProperlyInitializationVersionNumbers)
        {
            auto sut = VersionHelper::fromString("v0.12.3");
            Assert::IsTrue(sut.has_value());
            Assert::AreEqual(0ull, sut->major);
            Assert::AreEqual(12ull, sut->minor);
            Assert::AreEqual(3ull, sut->revision);
            Assert::AreEqual(0ull, sut->build);
        }
        TEST_METHOD (stringConstructorShouldProperlyInitializationFourPartVersionNumbers)
        {
            auto sut = VersionHelper::fromString("v0.100.2607.08001");
            Assert::IsTrue(sut.has_value());
            Assert::AreEqual(0ull, sut->major);
            Assert::AreEqual(100ull, sut->minor);
            Assert::AreEqual(2607ull, sut->revision);
            Assert::AreEqual(8001ull, sut->build);
        }
        TEST_METHOD (stringConstructorShouldIgnorePreviewSuffix)
        {
            auto sut = VersionHelper::fromString("v0.100.2607.08001-preview");
            Assert::IsTrue(sut.has_value());
            Assert::AreEqual(0ull, sut->major);
            Assert::AreEqual(100ull, sut->minor);
            Assert::AreEqual(2607ull, sut->revision);
            Assert::AreEqual(8001ull, sut->build);
        }
        TEST_METHOD (stringConstructorShouldProperlyInitializationWithDifferentVersionNumbers)
        {
            auto sut = VersionHelper::fromString(L"v2.25.1");
            Assert::IsTrue(sut.has_value());

            Assert::AreEqual(2ull, sut->major);
            Assert::AreEqual(25ull, sut->minor);
            Assert::AreEqual(1ull, sut->revision);
        }
        TEST_METHOD (stringConstructorShouldProperlyInitializationVersionNumbersWithUppercaseV)
        {
            auto sut = VersionHelper::fromString(L"V2.25.1");
            Assert::IsTrue(sut.has_value());

            Assert::AreEqual(2ull, sut->major);
            Assert::AreEqual(25ull, sut->minor);
            Assert::AreEqual(1ull, sut->revision);
        }
        TEST_METHOD (emptyStringNotAccepted)
        {
            auto sut = VersionHelper::fromString("");

            Assert::IsFalse(sut.has_value());
        }
        TEST_METHOD (invalidStringNotAccepted1)
        {
            auto sut = VersionHelper::fromString(L"v2a.");

            Assert::IsFalse(sut.has_value());
        }
        TEST_METHOD (invalidStringNotAccepted2)
        {
            auto sut = VersionHelper::fromString(L"12abc2vv.0");

            Assert::IsFalse(sut.has_value());
        }
        TEST_METHOD (invalidStringNotAccepted3)
        {
            auto sut = VersionHelper::fromString("123");

            Assert::IsFalse(sut.has_value());
        }
        TEST_METHOD (invalidStringNotAccepted4)
        {
            auto sut = VersionHelper::fromString(L"v1v2v3");

            Assert::IsFalse(sut.has_value());
        }
        TEST_METHOD (invalidStringNotAccepted5)
        {
            auto sut = VersionHelper::fromString("v.1.2.3v");

            Assert::IsFalse(sut.has_value());
        }
        TEST_METHOD (partialVersionStringNotAccepted1)
        {
            auto sut = VersionHelper::fromString(L"v1.");

            Assert::IsFalse(sut.has_value());
        }
        TEST_METHOD (partialVersionStringNotAccepted2)
        {
            auto sut = VersionHelper::fromString("v1.2");

            Assert::IsFalse(sut.has_value());
        }
        TEST_METHOD (partialVersionStringNotAccepted3)
        {
            auto sut = VersionHelper::fromString(L"v1.2.");

            Assert::IsFalse(sut.has_value());
        }
        TEST_METHOD (tooManyVersionPartsNotAccepted)
        {
            auto sut = VersionHelper::fromString(L"v1.2.3.4.5");

            Assert::IsFalse(sut.has_value());
        }
        TEST_METHOD (parsedWithoutLeadingV)
        {
            VersionHelper expected{ 12ull, 13ull, 111ull };
            auto actual = VersionHelper::fromString(L"12.13.111");

            Assert::IsTrue(actual.has_value());
            Assert::AreEqual(*actual, expected);
        }
        TEST_METHOD (parsedFourPartVersionWithoutLeadingV)
        {
            VersionHelper expected{ 12ull, 13ull, 111ull, 5ull };
            auto actual = VersionHelper::fromString(L"12.13.111.5");

            Assert::IsTrue(actual.has_value());
            Assert::AreEqual(*actual, expected);
        }
        TEST_METHOD (whenMajorVersionIsGreaterComparisonOperatorShouldReturnProperValue)
        {
            VersionHelper lhs(MAJOR_VERSION_0 + 1, MINOR_VERSION_12, REVISION_VERSION_0);
            VersionHelper rhs(MAJOR_VERSION_0, MINOR_VERSION_12, REVISION_VERSION_0);

            Assert::IsTrue(lhs > rhs);
        }
        TEST_METHOD (whenMajorVersionIsLesserComparisonOperatorShouldReturnProperValue)
        {
            VersionHelper lhs(MAJOR_VERSION_0, MINOR_VERSION_12, REVISION_VERSION_0);
            VersionHelper rhs(MAJOR_VERSION_0 + 1, MINOR_VERSION_12, REVISION_VERSION_0);

            Assert::IsFalse(lhs > rhs);
        }
        TEST_METHOD (whenMajorVersionIsEqualComparisonOperatorShouldCompareMinorVersionValue)
        {
            VersionHelper lhs(MAJOR_VERSION_0, MINOR_VERSION_12, REVISION_VERSION_0);
            VersionHelper rhs(MAJOR_VERSION_0, MINOR_VERSION_12 - 1, REVISION_VERSION_0);

            Assert::IsTrue(lhs > rhs);
        }
        TEST_METHOD (whenMajorVersionIsEqualComparisonOperatorShouldCompareMinorVersionValue2)
        {
            VersionHelper lhs(MAJOR_VERSION_0, MINOR_VERSION_12 - 1, REVISION_VERSION_0);
            VersionHelper rhs(MAJOR_VERSION_0, MINOR_VERSION_12, REVISION_VERSION_0);

            Assert::IsFalse(lhs > rhs);
        }

        TEST_METHOD (whenMajorAndMinorVersionIsEqualComparisonOperatorShouldCompareRevisionValue)
        {
            VersionHelper lhs(MAJOR_VERSION_0, MINOR_VERSION_12, REVISION_VERSION_0 + 1);
            VersionHelper rhs(MAJOR_VERSION_0, MINOR_VERSION_12, REVISION_VERSION_0);

            Assert::IsTrue(lhs > rhs);
        }
        TEST_METHOD (whenMajorAndMinorVersionIsEqualComparisonOperatorShouldCompareRevisionValue2)
        {
            VersionHelper lhs(MAJOR_VERSION_0, MINOR_VERSION_12, REVISION_VERSION_0);
            VersionHelper rhs(MAJOR_VERSION_0, MINOR_VERSION_12, REVISION_VERSION_0 + 1);

            Assert::IsFalse(lhs > rhs);
        }
        TEST_METHOD (whenMajorMinorAndRevisionIsEqualGreaterThanOperatorShouldReturnFalse)
        {
            VersionHelper lhs(MAJOR_VERSION_0, MINOR_VERSION_12, REVISION_VERSION_0);
            VersionHelper rhs(MAJOR_VERSION_0, MINOR_VERSION_12, REVISION_VERSION_0);

            Assert::IsFalse(lhs > rhs);
        }
        TEST_METHOD (whenMajorMinorAndRevisionAreEqualComparisonOperatorShouldCompareBuildValue)
        {
            VersionHelper lhs(MAJOR_VERSION_0, MINOR_VERSION_12, REVISION_VERSION_0, 1);
            VersionHelper rhs(MAJOR_VERSION_0, MINOR_VERSION_12, REVISION_VERSION_0);

            Assert::IsTrue(lhs > rhs);
        }
        TEST_METHOD (zeroBuildVersionShouldFormatAsThreePartVersion)
        {
            VersionHelper sut(MAJOR_VERSION_0, MINOR_VERSION_12, REVISION_VERSION_0);

            Assert::AreEqual(std::wstring(L"v0.12.0"), sut.toWstring());
        }
        TEST_METHOD (nonZeroBuildVersionShouldFormatAsFourPartVersion)
        {
            VersionHelper sut(MAJOR_VERSION_0, MINOR_VERSION_12, REVISION_VERSION_0, 1);

            Assert::AreEqual(std::wstring(L"v0.12.0.1"), sut.toWstring());
        }
    };
}
