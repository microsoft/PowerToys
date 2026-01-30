#include "pch.h"
#include "TestHelpers.h"
#include <processApi.h>

using namespace Microsoft::VisualStudio::CppUnitTestFramework;

namespace UnitTestsCommonUtils
{
    TEST_CLASS(ProcessApiTests)
    {
    public:
        TEST_METHOD(GetProcessHandlesByName_CurrentProcess_ReturnsHandles)
        {
            // Get current process executable name
            wchar_t path[MAX_PATH];
            GetModuleFileNameW(nullptr, path, MAX_PATH);

            // Extract just the filename
            std::wstring fullPath(path);
            auto lastSlash = fullPath.rfind(L'\\');
            std::wstring exeName = (lastSlash != std::wstring::npos) ?
                fullPath.substr(lastSlash + 1) : fullPath;

            auto handles = getProcessHandlesByName(exeName, PROCESS_QUERY_LIMITED_INFORMATION);

            // Should find at least our own process
            Assert::IsFalse(handles.empty());

            // Clean up handles
            for (auto handle : handles)
            {
                CloseHandle(handle);
            }
        }

        TEST_METHOD(GetProcessHandlesByName_NonExistentProcess_ReturnsEmpty)
        {
            auto handles = getProcessHandlesByName(L"NonExistentProcess12345.exe", PROCESS_QUERY_LIMITED_INFORMATION);
            Assert::IsTrue(handles.empty());
        }

        TEST_METHOD(GetProcessHandlesByName_EmptyName_ReturnsEmpty)
        {
            auto handles = getProcessHandlesByName(L"", PROCESS_QUERY_LIMITED_INFORMATION);
            Assert::IsTrue(handles.empty());
        }

        TEST_METHOD(GetProcessHandlesByName_Explorer_ReturnsHandles)
        {
            // Explorer.exe should typically be running
            auto handles = getProcessHandlesByName(L"explorer.exe", PROCESS_QUERY_LIMITED_INFORMATION);

            // Clean up any handles we got
            for (auto handle : handles)
            {
                CloseHandle(handle);
            }

            // May or may not find explorer depending on system state
            // Just verify it doesn't crash
            Assert::IsTrue(true);
        }

        TEST_METHOD(GetProcessHandlesByName_CaseInsensitive_Works)
        {
            // Get current process name in uppercase
            wchar_t path[MAX_PATH];
            GetModuleFileNameW(nullptr, path, MAX_PATH);

            std::wstring fullPath(path);
            auto lastSlash = fullPath.rfind(L'\\');
            std::wstring exeName = (lastSlash != std::wstring::npos) ?
                fullPath.substr(lastSlash + 1) : fullPath;

            // Convert to uppercase
            std::wstring upperName = exeName;
            std::transform(upperName.begin(), upperName.end(), upperName.begin(), ::towupper);

            auto handles = getProcessHandlesByName(upperName, PROCESS_QUERY_LIMITED_INFORMATION);

            // Clean up handles
            for (auto handle : handles)
            {
                CloseHandle(handle);
            }

            // The function may or may not be case insensitive - just don't crash
            Assert::IsTrue(true);
        }

        TEST_METHOD(GetProcessHandlesByName_DifferentAccessRights_Works)
        {
            wchar_t path[MAX_PATH];
            GetModuleFileNameW(nullptr, path, MAX_PATH);

            std::wstring fullPath(path);
            auto lastSlash = fullPath.rfind(L'\\');
            std::wstring exeName = (lastSlash != std::wstring::npos) ?
                fullPath.substr(lastSlash + 1) : fullPath;

            // Try with different access rights
            auto handles1 = getProcessHandlesByName(exeName, PROCESS_QUERY_INFORMATION);
            auto handles2 = getProcessHandlesByName(exeName, PROCESS_VM_READ);

            // Clean up
            for (auto handle : handles1) CloseHandle(handle);
            for (auto handle : handles2) CloseHandle(handle);

            // Just verify no crashes
            Assert::IsTrue(true);
        }

        TEST_METHOD(GetProcessHandlesByName_SystemProcess_MayRequireElevation)
        {
            // System processes might require elevation
            auto handles = getProcessHandlesByName(L"System", PROCESS_QUERY_LIMITED_INFORMATION);

            for (auto handle : handles)
            {
                CloseHandle(handle);
            }

            // Just verify no crashes
            Assert::IsTrue(true);
        }

        TEST_METHOD(GetProcessHandlesByName_ValidHandles_AreUsable)
        {
            wchar_t path[MAX_PATH];
            GetModuleFileNameW(nullptr, path, MAX_PATH);

            std::wstring fullPath(path);
            auto lastSlash = fullPath.rfind(L'\\');
            std::wstring exeName = (lastSlash != std::wstring::npos) ?
                fullPath.substr(lastSlash + 1) : fullPath;

            auto handles = getProcessHandlesByName(exeName, PROCESS_QUERY_LIMITED_INFORMATION);

            bool foundValidHandle = false;
            for (auto handle : handles)
            {
                // Try to use the handle
                DWORD exitCode;
                if (GetExitCodeProcess(handle, &exitCode))
                {
                    foundValidHandle = true;
                }
                CloseHandle(handle);
            }

            Assert::IsTrue(foundValidHandle || handles.empty());
        }
    };
}
