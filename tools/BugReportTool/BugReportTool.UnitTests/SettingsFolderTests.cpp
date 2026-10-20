// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#define WIN32_LEAN_AND_MEAN
#include <Windows.h>

#include <filesystem>
#include <fstream>
#include <iterator>
#include <string>
#include <system_error>

#include <CppUnitTest.h>

#include "..\BugReportTool\SettingsFolder.h"

using namespace Microsoft::VisualStudio::CppUnitTestFramework;

namespace
{
    class TemporaryDirectory
    {
    public:
        TemporaryDirectory()
        {
            wchar_t temporaryRoot[MAX_PATH]{};
            const DWORD rootLength = GetTempPathW(ARRAYSIZE(temporaryRoot), temporaryRoot);
            Assert::IsTrue(rootLength > 0 && rootLength < ARRAYSIZE(temporaryRoot), L"Could not locate the temporary directory.");

            wchar_t temporaryFile[MAX_PATH]{};
            Assert::IsTrue(GetTempFileNameW(temporaryRoot, L"PBR", 0, temporaryFile) != 0, L"Could not reserve a temporary path.");
            m_path = temporaryFile;
            Assert::IsTrue(DeleteFileW(temporaryFile) != FALSE, L"Could not remove the temporary placeholder file.");
            Assert::IsTrue(CreateDirectoryW(temporaryFile, nullptr) != FALSE, L"Could not create the temporary directory.");
        }

        TemporaryDirectory(const TemporaryDirectory&) = delete;
        TemporaryDirectory& operator=(const TemporaryDirectory&) = delete;

        ~TemporaryDirectory()
        {
            std::error_code error;
            std::filesystem::remove_all(m_path, error);
        }

        const std::filesystem::path& Path() const noexcept
        {
            return m_path;
        }

    private:
        std::filesystem::path m_path;
    };

    class ExclusiveFile
    {
    public:
        explicit ExclusiveFile(const std::filesystem::path& path) :
            m_handle{ CreateFileW(path.c_str(), GENERIC_READ | GENERIC_WRITE, 0, nullptr, OPEN_ALWAYS, FILE_ATTRIBUTE_NORMAL, nullptr) }
        {
            Assert::IsTrue(m_handle != INVALID_HANDLE_VALUE, L"Could not hold the file exclusively.");
        }

        ExclusiveFile(const ExclusiveFile&) = delete;
        ExclusiveFile& operator=(const ExclusiveFile&) = delete;

        ~ExclusiveFile()
        {
            if (m_handle != INVALID_HANDLE_VALUE)
            {
                CloseHandle(m_handle);
            }
        }

    private:
        HANDLE m_handle = INVALID_HANDLE_VALUE;
    };

    void WriteFile(const std::filesystem::path& path, const std::string& contents)
    {
        std::ofstream stream{ path, std::ios::binary };
        stream << contents;
        Assert::IsTrue(stream.good(), L"Could not write the test fixture.");
    }

    std::string ReadFile(const std::filesystem::path& path)
    {
        std::ifstream stream{ path, std::ios::binary };
        Assert::IsTrue(stream.is_open(), L"The expected diagnostic file was not copied.");
        return { std::istreambuf_iterator<char>{ stream }, std::istreambuf_iterator<char>{} };
    }

    void VerifyLockedRootFileIsExcluded(const wchar_t* lockFileName)
    {
        TemporaryDirectory temporaryDirectory;
        const auto source = temporaryDirectory.Path() / L"settings";
        const auto destination = temporaryDirectory.Path() / L"report";
        std::filesystem::create_directories(source / L"FancyZones" / L"Logs");
        WriteFile(source / L"settings.json", R"({"enabled":true})");
        WriteFile(source / L"FancyZones" / L"Logs" / L"log.txt", "diagnostic contents\r\n");

        // Runner holds this handle with no sharing for its entire lifetime.
        // Declare it after the directory so assertions also release it before cleanup.
        const ExclusiveFile lock{ source / lockFileName };
        BugReportTool::CopySettingsFolder(source, destination);

        Assert::AreEqual(std::string{ R"({"enabled":true})" }, ReadFile(destination / L"settings.json"));
        Assert::AreEqual(std::string{ "diagnostic contents\r\n" }, ReadFile(destination / L"FancyZones" / L"Logs" / L"log.txt"));
        Assert::IsFalse(std::filesystem::exists(destination / lockFileName), L"The Runner lock must not be included in the report.");
        Assert::IsTrue(std::filesystem::exists(source / lockFileName), L"Report generation must preserve the source lock file.");
    }
}

namespace BugReportToolUnitTests
{
    TEST_CLASS(SettingsFolderTests)
    {
    public:
        TEST_METHOD(CopiesSettingsWhileRunnerLockIsHeld)
        {
            VerifyLockedRootFileIsExcluded(L"settings.json.cli-lock");
        }

        TEST_METHOD(ExcludesCaseVariantRunnerLock)
        {
            VerifyLockedRootFileIsExcluded(L"SETTINGS.JSON.CLI-LOCK");
        }

        TEST_METHOD(CopiesFilesAndEmptyDirectoriesWithoutRunnerLock)
        {
            TemporaryDirectory temporaryDirectory;
            const auto source = temporaryDirectory.Path() / L"settings";
            const auto destination = temporaryDirectory.Path() / L"report";
            std::filesystem::create_directories(source / L"EmptyModule");
            WriteFile(source / L"settings.json", R"({"enabled":false})");
            WriteFile(source / L"diagnostic.cli-lock", "other diagnostic file");

            // Main.cpp passes the settings root with a trailing directory separator.
            BugReportTool::CopySettingsFolder(source / L"", destination);

            Assert::AreEqual(std::string{ R"({"enabled":false})" }, ReadFile(destination / L"settings.json"));
            Assert::AreEqual(std::string{ "other diagnostic file" }, ReadFile(destination / L"diagnostic.cli-lock"));
            Assert::IsTrue(std::filesystem::is_directory(destination / L"EmptyModule"));
            Assert::IsTrue(std::filesystem::is_empty(destination / L"EmptyModule"));
        }

        TEST_METHOD(CreatesDestinationForEmptySource)
        {
            TemporaryDirectory temporaryDirectory;
            const auto source = temporaryDirectory.Path() / L"settings";
            const auto destination = temporaryDirectory.Path() / L"report";
            std::filesystem::create_directory(source);

            BugReportTool::CopySettingsFolder(source, destination);

            Assert::IsTrue(std::filesystem::is_directory(destination));
            Assert::IsTrue(std::filesystem::is_empty(destination));
        }

        TEST_METHOD(CopiesSameNamedFilesBelowSettingsRoot)
        {
            TemporaryDirectory temporaryDirectory;
            const auto source = temporaryDirectory.Path() / L"settings";
            const auto destination = temporaryDirectory.Path() / L"report";
            std::filesystem::create_directories(source / L"Module");
            WriteFile(source / L"Module" / L"settings.json.cli-lock", "module diagnostic contents");

            BugReportTool::CopySettingsFolder(source, destination);

            Assert::AreEqual(std::string{ "module diagnostic contents" }, ReadFile(destination / L"Module" / L"settings.json.cli-lock"));
        }

        TEST_METHOD(ReportsSharingViolationForOtherFiles)
        {
            TemporaryDirectory temporaryDirectory;
            const auto source = temporaryDirectory.Path() / L"settings";
            const auto destination = temporaryDirectory.Path() / L"report";
            std::filesystem::create_directory(source);
            WriteFile(source / L"settings.json", "required diagnostic contents");

            const ExclusiveFile lock{ source / L"settings.json" };
            Assert::ExpectException<std::filesystem::filesystem_error>([&]() {
                BugReportTool::CopySettingsFolder(source, destination);
            });
        }
    };
}
