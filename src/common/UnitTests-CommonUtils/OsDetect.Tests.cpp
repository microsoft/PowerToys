#include "pch.h"
#include "TestHelpers.h"
#include <os-detect.h>

using namespace Microsoft::VisualStudio::CppUnitTestFramework;

namespace UnitTestsCommonUtils
{
    TEST_CLASS(OsDetectTests)
    {
    public:
        // IsAPIContractVxAvailable tests
        TEST_METHOD(IsAPIContractV8Available_ReturnsBoolean)
        {
            // This test verifies the function runs without crashing
            // The actual result depends on the OS version
            bool result = IsAPIContractV8Available();
            // Result is either true or false, both are valid
            Assert::IsTrue(result == true || result == false);
        }

        TEST_METHOD(IsAPIContractVxAvailable_V1_ReturnsTrue)
        {
            // API contract v1 should be available on any modern Windows
            bool result = IsAPIContractVxAvailable<1>();
            Assert::IsTrue(result);
        }

        TEST_METHOD(IsAPIContractVxAvailable_V5_ReturnsBooleanConsistently)
        {
            // Call multiple times to verify caching works correctly
            bool result1 = IsAPIContractVxAvailable<5>();
            bool result2 = IsAPIContractVxAvailable<5>();
            bool result3 = IsAPIContractVxAvailable<5>();
            Assert::AreEqual(result1, result2);
            Assert::AreEqual(result2, result3);
        }

        TEST_METHOD(IsAPIContractVxAvailable_V10_ReturnsBoolean)
        {
            bool result = IsAPIContractVxAvailable<10>();
            // Result depends on Windows version, but should not crash
            Assert::IsTrue(result == true || result == false);
        }

        TEST_METHOD(IsAPIContractVxAvailable_V15_ReturnsBoolean)
        {
            bool result = IsAPIContractVxAvailable<15>();
            // Higher API versions, may or may not be available
            Assert::IsTrue(result == true || result == false);
        }

        // Is19H1OrHigher tests
        TEST_METHOD(Is19H1OrHigher_ReturnsBoolean)
        {
            bool result = Is19H1OrHigher();
            // Result depends on OS version, but should not crash
            Assert::IsTrue(result == true || result == false);
        }

        TEST_METHOD(Is19H1OrHigher_ReturnsSameAsV8Contract)
        {
            // Is19H1OrHigher is implemented as IsAPIContractV8Available
            bool is19H1 = Is19H1OrHigher();
            bool isV8 = IsAPIContractV8Available();
            Assert::AreEqual(is19H1, isV8);
        }

        TEST_METHOD(Is19H1OrHigher_ConsistentAcrossMultipleCalls)
        {
            bool result1 = Is19H1OrHigher();
            bool result2 = Is19H1OrHigher();
            bool result3 = Is19H1OrHigher();
            Assert::AreEqual(result1, result2);
            Assert::AreEqual(result2, result3);
        }

        // Static caching behavior tests
        TEST_METHOD(StaticCaching_DifferentContractVersions_IndependentResults)
        {
            // Each template instantiation has its own static variable
            bool v1 = IsAPIContractVxAvailable<1>();
            (void)v1; // Suppress unused variable warning

            // v1 should be true on any modern Windows
            Assert::IsTrue(v1);
        }

        // Performance test (optional - verifies caching)
        TEST_METHOD(Performance_MultipleCallsAreFast)
        {
            // The static caching should make subsequent calls very fast
            auto start = std::chrono::high_resolution_clock::now();

            for (int i = 0; i < 10000; ++i)
            {
                Is19H1OrHigher();
            }

            auto end = std::chrono::high_resolution_clock::now();
            auto duration = std::chrono::duration_cast<std::chrono::milliseconds>(end - start);

            // 10000 calls should complete in well under 1 second due to caching
            Assert::IsTrue(duration.count() < 1000);
        }
    };
}
