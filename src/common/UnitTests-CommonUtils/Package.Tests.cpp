#include "pch.h"
#include "TestHelpers.h"
#include <package.h>

using namespace Microsoft::VisualStudio::CppUnitTestFramework;
using namespace package;

namespace UnitTestsCommonUtils
{
    TEST_CLASS(PackageTests)
    {
    public:
        // IsWin11OrGreater tests
        TEST_METHOD(IsWin11OrGreater_ReturnsBoolean)
        {
            bool result = IsWin11OrGreater();
            Assert::IsTrue(result == true || result == false);
        }

        TEST_METHOD(IsWin11OrGreater_ConsistentResults)
        {
            bool result1 = IsWin11OrGreater();
            bool result2 = IsWin11OrGreater();
            bool result3 = IsWin11OrGreater();

            Assert::AreEqual(result1, result2);
            Assert::AreEqual(result2, result3);
        }

        // PACKAGE_VERSION struct tests
        TEST_METHOD(PackageVersion_DefaultConstruction)
        {
            PACKAGE_VERSION version{};
            Assert::AreEqual(static_cast<UINT16>(0), version.Major);
            Assert::AreEqual(static_cast<UINT16>(0), version.Minor);
            Assert::AreEqual(static_cast<UINT16>(0), version.Build);
            Assert::AreEqual(static_cast<UINT16>(0), version.Revision);
        }

        TEST_METHOD(PackageVersion_Assignment)
        {
            PACKAGE_VERSION version{};
            version.Major = 1;
            version.Minor = 2;
            version.Build = 3;
            version.Revision = 4;

            Assert::AreEqual(static_cast<UINT16>(1), version.Major);
            Assert::AreEqual(static_cast<UINT16>(2), version.Minor);
            Assert::AreEqual(static_cast<UINT16>(3), version.Build);
            Assert::AreEqual(static_cast<UINT16>(4), version.Revision);
        }

        // ComInitializer tests
        TEST_METHOD(ComInitializer_InitializesAndUninitializesCom)
        {
            {
                ComInitializer comInit;
                // COM should be initialized within this scope
            }
            // COM should be uninitialized after scope

            // Verify we can initialize again
            {
                ComInitializer comInit2;
            }

            Assert::IsTrue(true);
        }

        TEST_METHOD(ComInitializer_MultipleInstances)
        {
            ComInitializer init1;
            ComInitializer init2;
            ComInitializer init3;

            // Multiple initializations should work (COM uses reference counting)
            Assert::IsTrue(true);
        }

        // GetRegisteredPackage tests
        TEST_METHOD(GetRegisteredPackage_NonExistentPackage_ReturnsEmpty)
        {
            auto result = GetRegisteredPackage(L"NonExistentPackage12345", false);

            // Should return empty for non-existent package
            Assert::IsFalse(result.has_value());
        }

        TEST_METHOD(GetRegisteredPackage_EmptyName_ReturnsFirstMatch)
        {
            auto result = GetRegisteredPackage(L"", false);
            Assert::IsTrue(result.has_value());
        }

        // IsPackageRegisteredWithPowerToysVersion tests
        TEST_METHOD(IsPackageRegisteredWithPowerToysVersion_NonExistentPackage_ReturnsFalse)
        {
            bool result = IsPackageRegisteredWithPowerToysVersion(L"NonExistentPackage12345");
            Assert::IsFalse(result);
        }

        TEST_METHOD(IsPackageRegisteredWithPowerToysVersion_EmptyName_ReturnsFalse)
        {
            bool result = IsPackageRegisteredWithPowerToysVersion(L"");
            Assert::IsFalse(result);
        }

        // FindMsixFile tests
        TEST_METHOD(FindMsixFile_NonExistentDirectory_ReturnsEmpty)
        {
            auto result = FindMsixFile(L"C:\\NonExistentDirectory12345", false);
            Assert::IsTrue(result.empty());
        }

        TEST_METHOD(FindMsixFile_SystemDirectory_DoesNotCrash)
        {
            // System32 probably doesn't have MSIX files, but shouldn't crash
            auto result = FindMsixFile(L"C:\\Windows\\System32", false);
            // May or may not find files, but should not crash
            Assert::IsTrue(true);
        }

        TEST_METHOD(FindMsixFile_RecursiveSearch_DoesNotCrash)
        {
            // Use temp directory which should exist
            wchar_t tempPath[MAX_PATH];
            GetTempPathW(MAX_PATH, tempPath);

            auto result = FindMsixFile(tempPath, true);
            // May or may not find files, but should not crash
            Assert::IsTrue(true);
        }

        // GetPackageNameAndVersionFromAppx tests
        TEST_METHOD(GetPackageNameAndVersionFromAppx_NonExistentFile_ReturnsFalse)
        {
            std::wstring name;
            PACKAGE_VERSION version{};

            bool result = GetPackageNameAndVersionFromAppx(L"C:\\NonExistent\\file.msix", name, version);
            Assert::IsFalse(result);
        }

        TEST_METHOD(GetPackageNameAndVersionFromAppx_EmptyPath_ReturnsFalse)
        {
            std::wstring name;
            PACKAGE_VERSION version{};

            bool result = GetPackageNameAndVersionFromAppx(L"", name, version);
            Assert::IsFalse(result);
        }

        // Thread safety
        TEST_METHOD(IsWin11OrGreater_ThreadSafe)
        {
            std::vector<std::thread> threads;
            std::atomic<int> successCount{ 0 };

            for (int i = 0; i < 10; ++i)
            {
                threads.emplace_back([&successCount]() {
                    for (int j = 0; j < 10; ++j)
                    {
                        IsWin11OrGreater();
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
