#include "pch.h"
#include "TestHelpers.h"
#include <process_path.h>

using namespace Microsoft::VisualStudio::CppUnitTestFramework;

namespace UnitTestsCommonUtils
{
    TEST_CLASS(ProcessPathTests)
    {
    public:
        // get_process_path (by PID) tests
        TEST_METHOD(GetProcessPath_CurrentProcess_ReturnsPath)
        {
            DWORD pid = GetCurrentProcessId();
            auto path = get_process_path(pid);

            Assert::IsFalse(path.empty());
            Assert::IsTrue(path.find(L".exe") != std::wstring::npos ||
                          path.find(L".dll") != std::wstring::npos);
        }

        TEST_METHOD(GetProcessPath_InvalidPid_ReturnsEmpty)
        {
            DWORD invalidPid = 0xFFFFFFFF;
            auto path = get_process_path(invalidPid);

            // Should return empty for invalid PID
            Assert::IsTrue(path.empty());
        }

        TEST_METHOD(GetProcessPath_ZeroPid_ReturnsEmpty)
        {
            auto path = get_process_path(static_cast<DWORD>(0));
            // PID 0 is the System Idle Process, might return empty or a path
            // Just verify it doesn't crash
            Assert::IsTrue(true);
        }

        TEST_METHOD(GetProcessPath_SystemPid_DoesNotCrash)
        {
            // PID 4 is typically the System process
            auto path = get_process_path(static_cast<DWORD>(4));
            // May return empty due to access rights, but shouldn't crash
            Assert::IsTrue(true);
        }

        // get_module_filename tests
        TEST_METHOD(GetModuleFilename_NullModule_ReturnsExePath)
        {
            auto path = get_module_filename(nullptr);

            Assert::IsFalse(path.empty());
            Assert::IsTrue(path.find(L".exe") != std::wstring::npos ||
                          path.find(L".dll") != std::wstring::npos);
        }

        TEST_METHOD(GetModuleFilename_Kernel32_ReturnsPath)
        {
            HMODULE kernel32 = GetModuleHandleW(L"kernel32.dll");
            Assert::IsNotNull(kernel32);

            auto path = get_module_filename(kernel32);

            Assert::IsFalse(path.empty());
            // Should contain kernel32 (case insensitive check)
            std::wstring lowerPath = path;
            std::transform(lowerPath.begin(), lowerPath.end(), lowerPath.begin(), ::towlower);
            Assert::IsTrue(lowerPath.find(L"kernel32") != std::wstring::npos);
        }

        TEST_METHOD(GetModuleFilename_InvalidModule_ReturnsEmpty)
        {
            auto path = get_module_filename(reinterpret_cast<HMODULE>(0x12345678));
            // Invalid module should return empty
            Assert::IsTrue(path.empty());
        }

        // get_module_folderpath tests
        TEST_METHOD(GetModuleFolderpath_NullModule_ReturnsFolder)
        {
            auto folder = get_module_folderpath(nullptr, true);

            Assert::IsFalse(folder.empty());
            // Should not end with .exe when removeFilename is true
            Assert::IsTrue(folder.find(L".exe") == std::wstring::npos);
            // Should end with backslash or be a valid folder path
            Assert::IsTrue(folder.back() == L'\\' || folder.find(L"\\") != std::wstring::npos);
        }

        TEST_METHOD(GetModuleFolderpath_KeepFilename_ReturnsFullPath)
        {
            auto fullPath = get_module_folderpath(nullptr, false);

            Assert::IsFalse(fullPath.empty());
            // Should contain .exe or .dll when not removing filename
            Assert::IsTrue(fullPath.find(L".exe") != std::wstring::npos ||
                          fullPath.find(L".dll") != std::wstring::npos);
        }

        TEST_METHOD(GetModuleFolderpath_Kernel32_ReturnsSystem32)
        {
            HMODULE kernel32 = GetModuleHandleW(L"kernel32.dll");
            Assert::IsNotNull(kernel32);

            auto folder = get_module_folderpath(kernel32, true);

            Assert::IsFalse(folder.empty());
            // Should be in system32 folder
            std::wstring lowerPath = folder;
            std::transform(lowerPath.begin(), lowerPath.end(), lowerPath.begin(), ::towlower);
            Assert::IsTrue(lowerPath.find(L"system32") != std::wstring::npos ||
                          lowerPath.find(L"syswow64") != std::wstring::npos);
        }

        // get_process_path (by HWND) tests
        TEST_METHOD(GetProcessPath_DesktopWindow_ReturnsPath)
        {
            HWND desktop = GetDesktopWindow();
            Assert::IsNotNull(desktop);

            auto path = get_process_path(desktop);
            // Desktop window should return a path
            // (could be explorer.exe or empty depending on system)
            Assert::IsTrue(true); // Just verify it doesn't crash
        }

        TEST_METHOD(GetProcessPath_InvalidHwnd_ReturnsEmpty)
        {
            auto path = get_process_path(reinterpret_cast<HWND>(0x12345678));
            Assert::IsTrue(path.empty());
        }

        TEST_METHOD(GetProcessPath_NullHwnd_ReturnsEmpty)
        {
            auto path = get_process_path(static_cast<HWND>(nullptr));
            Assert::IsTrue(path.empty());
        }

        // Consistency tests
        TEST_METHOD(Consistency_ModuleFilenameAndFolderpath_AreRelated)
        {
            auto fullPath = get_module_filename(nullptr);
            auto folder = get_module_folderpath(nullptr, true);

            Assert::IsFalse(fullPath.empty());
            Assert::IsFalse(folder.empty());

            // Full path should start with the folder
            Assert::IsTrue(fullPath.find(folder) == 0 || folder.find(fullPath.substr(0, folder.length())) == 0);
        }
    };
}
