#include "pch.h"

#include "VersionHelper.h"

using namespace Microsoft::VisualStudio::CppUnitTestFramework;

namespace UnitTestsVersionHelper
{
    const int MAJOR_VERSION_0 = 0;
    const int MINOR_VERSION_12 = 12;
    const int REVISION_VERSION_0 = 0;

    TEST_CLASS (UnitTestsVersionHelper)
    {
    public:
        TEST_METHOD (integerConstructorShouldProperlyInitializationVersionNumbers)
        {
            VersionHelper sut(MAJOR_VERSION_0, MINOR_VERSION_12, REVISION_VERSION_0);

            Assert::AreEqual(MAJOR_VERSION_0, sut.major);
            Assert::AreEqual(MINOR_VERSION_12, sut.minor);
            Assert::AreEqual(REVISION_VERSION_0, sut.revision);
        }
        TEST_METHOD (integerConstructorShouldProperlyInitializationWithDifferentVersionNumbers)
        {
            const int testcaseMajor = 2;
            const int testcaseMinor = 25;
            const int testcaseRevision = 1;
            VersionHelper sut(testcaseMajor, testcaseMinor, testcaseRevision);

            Assert::AreEqual(testcaseMajor, sut.major);
            Assert::AreEqual(testcaseMinor, sut.minor);
            Assert::AreEqual(testcaseRevision, sut.revision);
        }
        TEST_METHOD (stringConstructorShouldProperlyInitializationVersionNumbers)
        {
            VersionHelper sut("v0.12.3");

            Assert::AreEqual(0, sut.major);
            Assert::AreEqual(12, sut.minor);
            Assert::AreEqual(3, sut.revision);
        }
        TEST_METHOD (stringConstructorShouldProperlyInitializationWithDifferentVersionNumbers)
        {
            VersionHelper sut("v2.25.1");

            Assert::AreEqual(2, sut.major);
            Assert::AreEqual(25, sut.minor);
            Assert::AreEqual(1, sut.revision);
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
    };
}
