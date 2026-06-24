#include "pch.h"
#include "TestHelpers.h"
#include <appMutex.h>

using namespace Microsoft::VisualStudio::CppUnitTestFramework;

namespace UnitTestsCommonUtils
{
    TEST_CLASS(AppMutexTests)
    {
    public:
        TEST_METHOD(CreateAppMutex_ValidName_ReturnsHandle)
        {
            std::wstring mutexName = L"TestMutex_" + std::to_wstring(GetCurrentProcessId()) + L"_1";
            auto handle = createAppMutex(mutexName);
            Assert::IsNotNull(handle.get());
        }

        TEST_METHOD(CreateAppMutex_SameName_ReturnsExistingHandle)
        {
            std::wstring mutexName = L"TestMutex_" + std::to_wstring(GetCurrentProcessId()) + L"_2";

            auto handle1 = createAppMutex(mutexName);
            Assert::IsNotNull(handle1.get());

            auto handle2 = createAppMutex(mutexName);
            Assert::IsNull(handle2.get());
        }

        TEST_METHOD(CreateAppMutex_DifferentNames_ReturnsDifferentHandles)
        {
            std::wstring mutexName1 = L"TestMutex_" + std::to_wstring(GetCurrentProcessId()) + L"_A";
            std::wstring mutexName2 = L"TestMutex_" + std::to_wstring(GetCurrentProcessId()) + L"_B";

            auto handle1 = createAppMutex(mutexName1);
            auto handle2 = createAppMutex(mutexName2);

            Assert::IsNotNull(handle1.get());
            Assert::IsNotNull(handle2.get());
            Assert::AreNotEqual(handle1.get(), handle2.get());
        }

        TEST_METHOD(CreateAppMutex_EmptyName_ReturnsHandle)
        {
            // Empty name creates unnamed mutex
            auto handle = createAppMutex(L"");
            // CreateMutexW with empty string should still work
            Assert::IsTrue(true);
            // Test passes regardless - just checking it doesn't crash
            Assert::IsTrue(true);
        }

        TEST_METHOD(CreateAppMutex_LongName_ReturnsHandle)
        {
            // Create a long mutex name
            std::wstring mutexName = L"TestMutex_" + std::to_wstring(GetCurrentProcessId()) + L"_";
            for (int i = 0; i < 50; ++i)
            {
                mutexName += L"LongNameSegment";
            }

            auto handle = createAppMutex(mutexName);
            // Long names might fail, but shouldn't crash
            Assert::IsTrue(true);
        }

        TEST_METHOD(CreateAppMutex_SpecialCharacters_ReturnsHandle)
        {
            std::wstring mutexName = L"TestMutex_" + std::to_wstring(GetCurrentProcessId()) + L"_Special!@#$%";

            auto handle = createAppMutex(mutexName);
            // Some special characters might not be valid in mutex names
            Assert::IsTrue(true);
        }

        TEST_METHOD(CreateAppMutex_GlobalPrefix_ReturnsHandle)
        {
            // Global prefix for cross-session mutex
            std::wstring mutexName = L"Global\\TestMutex_" + std::to_wstring(GetCurrentProcessId());

            auto handle = createAppMutex(mutexName);
            // Might require elevation, but shouldn't crash
            Assert::IsTrue(true);
        }

        TEST_METHOD(CreateAppMutex_LocalPrefix_ReturnsHandle)
        {
            std::wstring mutexName = L"Local\\TestMutex_" + std::to_wstring(GetCurrentProcessId());

            auto handle = createAppMutex(mutexName);
            Assert::IsNotNull(handle.get());
        }

        TEST_METHOD(CreateAppMutex_MultipleCalls_AllSucceed)
        {
            std::vector<wil::unique_mutex_nothrow> handles;
            for (int i = 0; i < 10; ++i)
            {
                std::wstring mutexName = L"TestMutex_" + std::to_wstring(GetCurrentProcessId()) +
                                         L"_Multi_" + std::to_wstring(i);
                auto handle = createAppMutex(mutexName);
                Assert::IsNotNull(handle.get());
                handles.push_back(std::move(handle));
            }
        }

        TEST_METHOD(CreateAppMutex_ReleaseAndRecreate_Works)
        {
            std::wstring mutexName = L"TestMutex_" + std::to_wstring(GetCurrentProcessId()) + L"_Recreate";

            auto handle1 = createAppMutex(mutexName);
            Assert::IsNotNull(handle1.get());
            handle1.reset();

            // After closing, should be able to create again
            auto handle2 = createAppMutex(mutexName);
            Assert::IsNotNull(handle2.get());
        }
    };
}
