#include "pch.h"
#include "CppUnitTest.h"

#define FILELOCKSMITH_LIB_STATIC
#include "FileLocksmithLibInterop/NtdllExtensions.h"
#include "FileLocksmithLib/FileLocksmith.h"

#include <algorithm>
#include <chrono>

using namespace Microsoft::VisualStudio::CppUnitTestFramework;

namespace FileLocksmithCLIUnitTests
{
    namespace
    {
        struct HeldTempFile
        {
            std::wstring path;
            HANDLE handle = INVALID_HANDLE_VALUE;

            HeldTempFile()
            {
                wchar_t dir[MAX_PATH + 1];
                wchar_t file[MAX_PATH + 1];
                Assert::AreNotEqual(0UL, GetTempPathW(MAX_PATH + 1, dir));
                Assert::AreNotEqual(0U, GetTempFileNameW(dir, L"fls", 0, file));
                path = file;
                handle = CreateFileW(path.c_str(), GENERIC_READ, FILE_SHARE_READ | FILE_SHARE_WRITE | FILE_SHARE_DELETE, nullptr, OPEN_EXISTING, FILE_FLAG_DELETE_ON_CLOSE, nullptr);
                Assert::IsTrue(handle != INVALID_HANDLE_VALUE);
            }

            ~HeldTempFile()
            {
                CloseHandle(handle);
            }
        };

        bool current_process_found(const std::vector<std::wstring>& paths)
        {
            const auto results = find_processes_recursive(paths);
            return std::any_of(results.begin(), results.end(), [](const ProcessResult& result) {
                return result.pid == GetCurrentProcessId();
            });
        }
    }

    TEST_CLASS(NtdllExtensionsTests)
    {
    public:
        TEST_CLASS_INITIALIZE(PinTestModule)
        {
            // A worker abandoned by the stall watchdog can still be blocked in this module's code when the
            // test host unloads the DLL, and crashes the host as soon as it wakes. Keep the code mapped until
            // the process exits, as it is in the File Locksmith executable.
            HMODULE module = nullptr;
            Assert::IsTrue(GetModuleHandleExW(
                               GET_MODULE_HANDLE_EX_FLAG_FROM_ADDRESS | GET_MODULE_HANDLE_EX_FLAG_PIN,
                               reinterpret_cast<LPCWSTR>(&current_process_found),
                               &module) == TRUE);
        }

        TEST_METHOD(FindsFileHeldByCurrentProcess)
        {
            HeldTempFile file;
            Assert::IsTrue(current_process_found({ file.path }));
        }

        // Regression test for https://github.com/microsoft/PowerToys/issues/45158:
        // a worker abandoned by the stall watchdog used to keep writing to handles()'s
        // stack after it returned, crashing File Locksmith with an access violation.
        TEST_METHOD(HandlesSurvivesAbandonedWorkers)
        {
            HeldTempFile file;
            NtdllExtensions nt_ext;

            for (int i = 0; i < 10; ++i)
            {
                nt_ext.handles(std::chrono::milliseconds{ 1 });
            }

            // Let any abandoned workers resume after their scans have returned.
            Sleep(500);

            Assert::IsTrue(current_process_found({ file.path }));
        }
    };
}
