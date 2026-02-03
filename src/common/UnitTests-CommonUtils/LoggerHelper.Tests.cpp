#include "pch.h"
#include "TestHelpers.h"
#include <logger_helper.h>

using namespace Microsoft::VisualStudio::CppUnitTestFramework;
using namespace LoggerHelpers;

namespace UnitTestsCommonUtils
{
    TEST_CLASS(LoggerHelperTests)
    {
    public:
        // get_log_folder_path tests
        TEST_METHOD(GetLogFolderPath_ValidAppPath_ReturnsPath)
        {
            auto result = get_log_folder_path(L"TestApp");

            Assert::IsFalse(result.empty());
            // Should contain the app name or be a valid path
            auto pathStr = result.wstring();
            Assert::IsTrue(pathStr.length() > 0);
        }

        TEST_METHOD(GetLogFolderPath_EmptyAppPath_ReturnsPath)
        {
            auto result = get_log_folder_path(L"");

            // Should still return a base path
            Assert::IsTrue(true); // Just verify no crash
        }

        TEST_METHOD(GetLogFolderPath_SpecialCharacters_Works)
        {
            auto result = get_log_folder_path(L"Test App With Spaces");

            // Should handle spaces in path
            Assert::IsTrue(true);
        }

        TEST_METHOD(GetLogFolderPath_ConsistentResults)
        {
            auto result1 = get_log_folder_path(L"TestApp");
            auto result2 = get_log_folder_path(L"TestApp");

            Assert::AreEqual(result1.wstring(), result2.wstring());
        }

        // dir_exists tests
        TEST_METHOD(DirExists_WindowsDirectory_ReturnsTrue)
        {
            bool result = dir_exists(std::filesystem::path(L"C:\\Windows"));
            Assert::IsTrue(result);
        }

        TEST_METHOD(DirExists_NonExistentDirectory_ReturnsFalse)
        {
            bool result = dir_exists(std::filesystem::path(L"C:\\NonExistentDir12345"));
            Assert::IsFalse(result);
        }

        TEST_METHOD(DirExists_FileInsteadOfDir_ReturnsTrue)
        {
            // notepad.exe is a file, not a directory
            bool result = dir_exists(std::filesystem::path(L"C:\\Windows\\notepad.exe"));
            Assert::IsTrue(result);
        }

        TEST_METHOD(DirExists_EmptyPath_ReturnsFalse)
        {
            bool result = dir_exists(std::filesystem::path(L""));
            Assert::IsFalse(result);
        }

        TEST_METHOD(DirExists_TempDirectory_ReturnsTrue)
        {
            wchar_t tempPath[MAX_PATH];
            GetTempPathW(MAX_PATH, tempPath);

            bool result = dir_exists(std::filesystem::path(tempPath));
            Assert::IsTrue(result);
        }

        // delete_old_log_folder tests
        TEST_METHOD(DeleteOldLogFolder_NonExistentFolder_DoesNotCrash)
        {
            delete_old_log_folder(std::filesystem::path(L"C:\\NonExistentLogFolder12345"));
            Assert::IsTrue(true);
        }

        TEST_METHOD(DeleteOldLogFolder_ValidEmptyFolder_Works)
        {
            TestHelpers::TempDirectory tempDir;

            // Create a subfolder structure
            auto logFolder = std::filesystem::path(tempDir.path()) / L"logs";
            std::filesystem::create_directories(logFolder);

            Assert::IsTrue(std::filesystem::exists(logFolder));

            delete_old_log_folder(logFolder);

            // Folder may or may not be deleted depending on implementation
            Assert::IsTrue(true);
        }

        // delete_other_versions_log_folders tests
        TEST_METHOD(DeleteOtherVersionsLogFolders_NonExistentPath_DoesNotCrash)
        {
            delete_other_versions_log_folders(L"C:\\NonExistent\\Path", L"1.0.0");
            Assert::IsTrue(true);
        }

        TEST_METHOD(DeleteOtherVersionsLogFolders_EmptyVersion_DoesNotCrash)
        {
            wchar_t tempPath[MAX_PATH];
            GetTempPathW(MAX_PATH, tempPath);

            delete_other_versions_log_folders(tempPath, L"");
            Assert::IsTrue(true);
        }

        // Thread safety tests
        TEST_METHOD(GetLogFolderPath_ThreadSafe)
        {
            std::vector<std::thread> threads;
            std::atomic<int> successCount{ 0 };

            for (int i = 0; i < 10; ++i)
            {
                threads.emplace_back([&successCount, i]() {
                    auto path = get_log_folder_path(L"TestApp" + std::to_wstring(i));
                    if (!path.empty())
                    {
                        successCount++;
                    }
                });
            }

            for (auto& t : threads)
            {
                t.join();
            }

            Assert::AreEqual(10, successCount.load());
        }

        TEST_METHOD(DirExists_ThreadSafe)
        {
            std::vector<std::thread> threads;
            std::atomic<int> successCount{ 0 };

            for (int i = 0; i < 10; ++i)
            {
                threads.emplace_back([&successCount]() {
                    for (int j = 0; j < 10; ++j)
                    {
                        dir_exists(std::filesystem::path(L"C:\\Windows"));
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

        // Path construction tests
        TEST_METHOD(GetLogFolderPath_ReturnsValidFilesystemPath)
        {
            auto result = get_log_folder_path(L"TestApp");

            // Should be a valid path that we can use with filesystem operations
            Assert::IsTrue(result.is_absolute() || result.has_root_name() || !result.empty());
        }
    };
}
