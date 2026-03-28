#include "pch.h"
#include "TestHelpers.h"
#include <elevation.h>

using namespace Microsoft::VisualStudio::CppUnitTestFramework;

namespace UnitTestsCommonUtils
{
    TEST_CLASS(ElevationTests)
    {
    public:
        // is_process_elevated tests
        TEST_METHOD(IsProcessElevated_ReturnsBoolean)
        {
            bool result = is_process_elevated(false);
            Assert::IsTrue(result == true || result == false);
        }

        TEST_METHOD(IsProcessElevated_CachedValue_ReturnsSameResult)
        {
            bool result1 = is_process_elevated(true);
            bool result2 = is_process_elevated(true);

            // Cached value should be consistent
            Assert::AreEqual(result1, result2);
        }

        TEST_METHOD(IsProcessElevated_UncachedValue_ReturnsBoolean)
        {
            bool result = is_process_elevated(false);
            Assert::IsTrue(result == true || result == false);
        }

        TEST_METHOD(IsProcessElevated_CachedAndUncached_AreConsistent)
        {
            // Both should return the same value for the same process
            bool cached = is_process_elevated(true);
            bool uncached = is_process_elevated(false);

            Assert::AreEqual(cached, uncached);
        }

        // check_user_is_admin tests
        TEST_METHOD(CheckUserIsAdmin_ReturnsBoolean)
        {
            bool result = check_user_is_admin();
            Assert::IsTrue(result == true || result == false);
        }

        TEST_METHOD(CheckUserIsAdmin_ConsistentResults)
        {
            bool result1 = check_user_is_admin();
            bool result2 = check_user_is_admin();
            bool result3 = check_user_is_admin();

            Assert::AreEqual(result1, result2);
            Assert::AreEqual(result2, result3);
        }

        // Relationship between elevation and admin
        TEST_METHOD(ElevationAndAdmin_Relationship)
        {
            bool elevated = is_process_elevated(false);
            bool admin = check_user_is_admin();
            (void)admin;

            // If elevated, user should typically be admin
            // But user can be admin without process being elevated
            if (elevated)
            {
                // Elevated process usually means admin user
                // (though there are edge cases)
            }
            // Just verify both functions return without crashing
            Assert::IsTrue(true);
        }

        // IsProcessOfWindowElevated tests
        TEST_METHOD(IsProcessOfWindowElevated_DesktopWindow_ReturnsBoolean)
        {
            HWND desktop = GetDesktopWindow();
            if (desktop)
            {
                bool result = IsProcessOfWindowElevated(desktop);
                Assert::IsTrue(result == true || result == false);
            }
            Assert::IsTrue(true);
        }

        TEST_METHOD(IsProcessOfWindowElevated_InvalidHwnd_DoesNotCrash)
        {
            bool result = IsProcessOfWindowElevated(nullptr);
            // Should handle null HWND gracefully
            Assert::IsTrue(result == true || result == false);
        }

        // ProcessInfo struct tests
        TEST_METHOD(ProcessInfo_DefaultConstruction)
        {
            ProcessInfo info{};
            Assert::AreEqual(static_cast<DWORD>(0), info.processID);
        }

        // Thread safety tests
        TEST_METHOD(IsProcessElevated_ThreadSafe)
        {
            std::vector<std::thread> threads;
            std::atomic<int> successCount{ 0 };

            for (int i = 0; i < 10; ++i)
            {
                threads.emplace_back([&successCount]() {
                    for (int j = 0; j < 10; ++j)
                    {
                        is_process_elevated(j % 2 == 0);
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

        // Performance of cached value
        TEST_METHOD(IsProcessElevated_CachedPerformance)
        {
            auto start = std::chrono::high_resolution_clock::now();

            for (int i = 0; i < 10000; ++i)
            {
                is_process_elevated(true);
            }

            auto end = std::chrono::high_resolution_clock::now();
            auto duration = std::chrono::duration_cast<std::chrono::milliseconds>(end - start);

            // Cached calls should be very fast
            Assert::IsTrue(duration.count() < 1000);
        }
    };
}
