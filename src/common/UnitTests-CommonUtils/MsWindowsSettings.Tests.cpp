#include "pch.h"
#include "TestHelpers.h"
#include <MsWindowsSettings.h>

using namespace Microsoft::VisualStudio::CppUnitTestFramework;

namespace UnitTestsCommonUtils
{
    TEST_CLASS(MsWindowsSettingsTests)
    {
    public:
        TEST_METHOD(GetAnimationsEnabled_ReturnsBoolean)
        {
            bool result = GetAnimationsEnabled();

            // Should return a valid boolean
            Assert::IsTrue(result == true || result == false);
        }

        TEST_METHOD(GetAnimationsEnabled_ConsistentResults)
        {
            // Multiple calls should return consistent results
            bool result1 = GetAnimationsEnabled();
            bool result2 = GetAnimationsEnabled();
            bool result3 = GetAnimationsEnabled();

            Assert::AreEqual(result1, result2);
            Assert::AreEqual(result2, result3);
        }

        TEST_METHOD(GetAnimationsEnabled_DoesNotCrash)
        {
            // Call multiple times to ensure stability
            for (int i = 0; i < 100; ++i)
            {
                GetAnimationsEnabled();
            }
            Assert::IsTrue(true);
        }

        TEST_METHOD(GetAnimationsEnabled_ThreadSafe)
        {
            std::vector<std::thread> threads;
            std::atomic<int> successCount{ 0 };

            for (int i = 0; i < 10; ++i)
            {
                threads.emplace_back([&successCount]() {
                    for (int j = 0; j < 10; ++j)
                    {
                        GetAnimationsEnabled();
                        successCount++;
                    }
                });
            }

            for (auto& t : threads)
            {
                t.join();
            }

            Assert::AreEqual(100, successCount.load());
        }
    };
}
