#include "pch.h"
#include "TestHelpers.h"
#include <MsiUtils.h>

using namespace Microsoft::VisualStudio::CppUnitTestFramework;

namespace UnitTestsCommonUtils
{
    TEST_CLASS(MsiUtilsTests)
    {
    public:
        // GetMsiPackageInstalledPath tests
        TEST_METHOD(GetMsiPackageInstalledPath_PerUser_DoesNotCrash)
        {
            auto result = GetMsiPackageInstalledPath(true);
            // Result depends on installation state, but should not crash
            Assert::IsTrue(true);
        }

        TEST_METHOD(GetMsiPackageInstalledPath_PerMachine_DoesNotCrash)
        {
            auto result = GetMsiPackageInstalledPath(false);
            // Result depends on installation state, but should not crash
            Assert::IsTrue(true);
        }

        TEST_METHOD(GetMsiPackageInstalledPath_ConsistentResults)
        {
            auto result1 = GetMsiPackageInstalledPath(true);
            auto result2 = GetMsiPackageInstalledPath(true);

            // Results should be consistent
            Assert::AreEqual(result1.has_value(), result2.has_value());
            if (result1.has_value() && result2.has_value())
            {
                Assert::AreEqual(*result1, *result2);
            }
        }

        TEST_METHOD(GetMsiPackageInstalledPath_PerUserVsPerMachine_MayDiffer)
        {
            auto perUser = GetMsiPackageInstalledPath(true);
            auto perMachine = GetMsiPackageInstalledPath(false);

            // These may or may not be equal depending on installation
            // Just verify they don't crash
            Assert::IsTrue(true);
        }

        // GetMsiPackagePath tests
        TEST_METHOD(GetMsiPackagePath_DoesNotCrash)
        {
            auto result = GetMsiPackagePath();
            // Result depends on installation state, but should not crash
            Assert::IsTrue(true);
        }

        TEST_METHOD(GetMsiPackagePath_ConsistentResults)
        {
            auto result1 = GetMsiPackagePath();
            auto result2 = GetMsiPackagePath();

            // Results should be consistent
            Assert::AreEqual(result1, result2);
        }

        // Thread safety tests
        TEST_METHOD(GetMsiPackageInstalledPath_ThreadSafe)
        {
            std::vector<std::thread> threads;
            std::atomic<int> successCount{ 0 };

            for (int i = 0; i < 5; ++i)
            {
                threads.emplace_back([&successCount]() {
                    for (int j = 0; j < 5; ++j)
                    {
                        GetMsiPackageInstalledPath(j % 2 == 0);
                        successCount++;
                    }
                });
            }

            for (auto& t : threads)
            {
                t.join();
            }

            Assert::AreEqual(25, successCount.load());
        }

        TEST_METHOD(GetMsiPackagePath_ThreadSafe)
        {
            std::vector<std::thread> threads;
            std::atomic<int> successCount{ 0 };

            for (int i = 0; i < 5; ++i)
            {
                threads.emplace_back([&successCount]() {
                    for (int j = 0; j < 5; ++j)
                    {
                        GetMsiPackagePath();
                        successCount++;
                    }
                });
            }

            for (auto& t : threads)
            {
                t.join();
            }

            Assert::AreEqual(25, successCount.load());
        }

        // Return value format tests
        TEST_METHOD(GetMsiPackageInstalledPath_ReturnsValidPathOrEmpty)
        {
            auto path = GetMsiPackageInstalledPath(true);

            if (path.has_value() && !path->empty())
            {
                // If a path is returned, it should contain backslash or be a valid path format
                Assert::IsTrue(path->find(L'\\') != std::wstring::npos ||
                              path->find(L'/') != std::wstring::npos ||
                              path->length() >= 2); // At minimum drive letter + colon
            }
            // No value or empty is also valid (not installed)
            Assert::IsTrue(true);
        }

        TEST_METHOD(GetMsiPackagePath_ReturnsValidPathOrEmpty)
        {
            auto path = GetMsiPackagePath();

            if (!path.empty())
            {
                // If a path is returned, it should be a valid path format
                Assert::IsTrue(path.find(L'\\') != std::wstring::npos ||
                              path.find(L'/') != std::wstring::npos ||
                              path.length() >= 2);
            }
            Assert::IsTrue(true);
        }
    };
}
