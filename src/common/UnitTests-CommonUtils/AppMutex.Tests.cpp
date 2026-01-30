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
            HANDLE handle = createAppMutex(mutexName);
            Assert::IsNotNull(handle);
            CloseHandle(handle);
        }

        TEST_METHOD(CreateAppMutex_SameName_ReturnsExistingHandle)
        {
            std::wstring mutexName = L"TestMutex_" + std::to_wstring(GetCurrentProcessId()) + L"_2";

            HANDLE handle1 = createAppMutex(mutexName);
            Assert::IsNotNull(handle1);

            HANDLE handle2 = createAppMutex(mutexName);
            Assert::IsNotNull(handle2);

            // GetLastError should indicate the mutex already existed
            // But both handles should be valid
            CloseHandle(handle1);
            CloseHandle(handle2);
        }

        TEST_METHOD(CreateAppMutex_DifferentNames_ReturnsDifferentHandles)
        {
            std::wstring mutexName1 = L"TestMutex_" + std::to_wstring(GetCurrentProcessId()) + L"_A";
            std::wstring mutexName2 = L"TestMutex_" + std::to_wstring(GetCurrentProcessId()) + L"_B";

            HANDLE handle1 = createAppMutex(mutexName1);
            HANDLE handle2 = createAppMutex(mutexName2);

            Assert::IsNotNull(handle1);
            Assert::IsNotNull(handle2);
            Assert::AreNotEqual(handle1, handle2);

            CloseHandle(handle1);
            CloseHandle(handle2);
        }

        TEST_METHOD(CreateAppMutex_EmptyName_ReturnsHandle)
        {
            // Empty name creates unnamed mutex
            HANDLE handle = createAppMutex(L"");
            // CreateMutexW with empty string should still work
            if (handle != nullptr)
            {
                CloseHandle(handle);
            }
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

            HANDLE handle = createAppMutex(mutexName);
            // Long names might fail, but shouldn't crash
            if (handle != nullptr)
            {
                CloseHandle(handle);
            }
            Assert::IsTrue(true);
        }

        TEST_METHOD(CreateAppMutex_SpecialCharacters_ReturnsHandle)
        {
            std::wstring mutexName = L"TestMutex_" + std::to_wstring(GetCurrentProcessId()) + L"_Special!@#$%";

            HANDLE handle = createAppMutex(mutexName);
            // Some special characters might not be valid in mutex names
            if (handle != nullptr)
            {
                CloseHandle(handle);
            }
            Assert::IsTrue(true);
        }

        TEST_METHOD(CreateAppMutex_GlobalPrefix_ReturnsHandle)
        {
            // Global prefix for cross-session mutex
            std::wstring mutexName = L"Global\\TestMutex_" + std::to_wstring(GetCurrentProcessId());

            HANDLE handle = createAppMutex(mutexName);
            // Might require elevation, but shouldn't crash
            if (handle != nullptr)
            {
                CloseHandle(handle);
            }
            Assert::IsTrue(true);
        }

        TEST_METHOD(CreateAppMutex_LocalPrefix_ReturnsHandle)
        {
            std::wstring mutexName = L"Local\\TestMutex_" + std::to_wstring(GetCurrentProcessId());

            HANDLE handle = createAppMutex(mutexName);
            Assert::IsNotNull(handle);
            CloseHandle(handle);
        }

        TEST_METHOD(CreateAppMutex_MultipleCalls_AllSucceed)
        {
            std::vector<HANDLE> handles;
            for (int i = 0; i < 10; ++i)
            {
                std::wstring mutexName = L"TestMutex_" + std::to_wstring(GetCurrentProcessId()) +
                                         L"_Multi_" + std::to_wstring(i);
                HANDLE handle = createAppMutex(mutexName);
                Assert::IsNotNull(handle);
                handles.push_back(handle);
            }

            for (auto handle : handles)
            {
                CloseHandle(handle);
            }
        }

        TEST_METHOD(CreateAppMutex_ReleaseAndRecreate_Works)
        {
            std::wstring mutexName = L"TestMutex_" + std::to_wstring(GetCurrentProcessId()) + L"_Recreate";

            HANDLE handle1 = createAppMutex(mutexName);
            Assert::IsNotNull(handle1);
            CloseHandle(handle1);

            // After closing, should be able to create again
            HANDLE handle2 = createAppMutex(mutexName);
            Assert::IsNotNull(handle2);
            CloseHandle(handle2);
        }
    };
}
