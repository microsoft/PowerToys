// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#include "pch.h"

#include <filesystem>
#include <fstream>
#include <string>

#include <common/updating/configBackup.h>
#include <common/version/helper.h>

using namespace Microsoft::VisualStudio::CppUnitTestFramework;

namespace fs = std::filesystem;

namespace UpdatingUnitTests
{
    // Helper to create a temp directory for test isolation
    class TempDir
    {
    public:
        TempDir()
        {
            wchar_t tempPath[MAX_PATH + 1];
            GetTempPathW(MAX_PATH, tempPath);

            m_path = fs::path(tempPath) / L"PowerToysUpdateTests";

            // Ensure clean state
            std::error_code ec;
            fs::remove_all(m_path, ec);
            fs::create_directories(m_path, ec);
        }

        ~TempDir()
        {
            std::error_code ec;
            fs::remove_all(m_path, ec);
        }

        const fs::path& path() const { return m_path; }

        // Write a file with the given content
        void WriteFile(const fs::path& relativePath, const std::string& content)
        {
            auto fullPath = m_path / relativePath;
            fs::create_directories(fullPath.parent_path());
            std::ofstream file(fullPath, std::ios::binary);
            file.write(content.data(), content.size());
        }

        // Write a file with raw bytes (including null bytes for corruption testing)
        void WriteFileBytes(const fs::path& relativePath, const std::vector<char>& bytes)
        {
            auto fullPath = m_path / relativePath;
            fs::create_directories(fullPath.parent_path());
            std::ofstream file(fullPath, std::ios::binary);
            file.write(bytes.data(), bytes.size());
        }

        // Read file content as string
        std::string ReadFile(const fs::path& relativePath)
        {
            auto fullPath = m_path / relativePath;
            std::ifstream file(fullPath, std::ios::binary);
            return std::string(std::istreambuf_iterator<char>(file), std::istreambuf_iterator<char>());
        }

        bool FileExists(const fs::path& relativePath)
        {
            return fs::exists(m_path / relativePath);
        }

    private:
        fs::path m_path;
    };

    TEST_CLASS(IsJsonFileCorruptedTests)
    {
    public:
        TEST_METHOD(CleanJsonFileIsNotCorrupted)
        {
            TempDir dir;
            dir.WriteFile(L"settings.json", R"({"theme":"dark","startup":true})");

            Assert::IsFalse(updating::IsJsonFileCorrupted(dir.path() / L"settings.json"));
        }

        TEST_METHOD(EmptyFileIsNotCorrupted)
        {
            TempDir dir;
            dir.WriteFile(L"empty.json", "");

            Assert::IsFalse(updating::IsJsonFileCorrupted(dir.path() / L"empty.json"));
        }

        TEST_METHOD(FileWithNullBytesIsCorrupted)
        {
            TempDir dir;
            std::vector<char> corrupted = { '{', '"', 'a', '"', ':', '\0', '\0', '\0', '}' };
            dir.WriteFileBytes(L"corrupted.json", corrupted);

            Assert::IsTrue(updating::IsJsonFileCorrupted(dir.path() / L"corrupted.json"));
        }

        TEST_METHOD(FileFilledWithNullBytesIsCorrupted)
        {
            // Reproduces the exact bug from #46179 - file filled with 0x00 bytes
            TempDir dir;
            std::vector<char> allNulls(1024, '\0');
            dir.WriteFileBytes(L"workspaces.json", allNulls);

            Assert::IsTrue(updating::IsJsonFileCorrupted(dir.path() / L"workspaces.json"));
        }

        TEST_METHOD(NonExistentFileIsNotCorrupted)
        {
            TempDir dir;

            Assert::IsFalse(updating::IsJsonFileCorrupted(dir.path() / L"missing.json"));
        }

        TEST_METHOD(LargeCleanFileIsNotCorrupted)
        {
            TempDir dir;
            // Create a file larger than the 4096 read chunk to test multi-chunk reading
            std::string largeContent(8192, 'x');
            dir.WriteFile(L"large.json", largeContent);

            Assert::IsFalse(updating::IsJsonFileCorrupted(dir.path() / L"large.json"));
        }

        TEST_METHOD(NullByteAtEndOfLargeFileIsDetected)
        {
            TempDir dir;
            // Null byte after the first chunk boundary
            std::string content(5000, 'x');
            content[4999] = '\0';
            std::vector<char> bytes(content.begin(), content.end());
            dir.WriteFileBytes(L"sneaky.json", bytes);

            Assert::IsTrue(updating::IsJsonFileCorrupted(dir.path() / L"sneaky.json"));
        }
    };

    TEST_CLASS(BackupConfigFilesTests)
    {
    public:
        TEST_METHOD(BackupCreatesConfigBackupDirectory)
        {
            TempDir dir;
            dir.WriteFile(L"settings.json", R"({"theme":"dark"})");

            updating::BackupConfigFiles(dir.path());

            Assert::IsTrue(dir.FileExists(L"ConfigBackup"));
        }

        TEST_METHOD(BackupCopiesRootJsonFiles)
        {
            TempDir dir;
            dir.WriteFile(L"settings.json", R"({"theme":"dark"})");
            dir.WriteFile(L"UpdateState.json", R"({"state":0})");

            updating::BackupConfigFiles(dir.path());

            Assert::IsTrue(dir.FileExists(L"ConfigBackup\\settings.json"));
            Assert::IsTrue(dir.FileExists(L"ConfigBackup\\UpdateState.json"));
            Assert::AreEqual(std::string(R"({"theme":"dark"})"), dir.ReadFile(L"ConfigBackup\\settings.json"));
        }

        TEST_METHOD(BackupCopiesModuleJsonFiles)
        {
            TempDir dir;
            dir.WriteFile(L"settings.json", R"({"theme":"dark"})");
            dir.WriteFile(L"FancyZones\\settings.json", R"({"zones":[]})");
            dir.WriteFile(L"Workspaces\\workspaces.json", R"({"workspaces":[]})");

            updating::BackupConfigFiles(dir.path());

            Assert::IsTrue(dir.FileExists(L"ConfigBackup\\FancyZones\\settings.json"));
            Assert::IsTrue(dir.FileExists(L"ConfigBackup\\Workspaces\\workspaces.json"));
        }

        TEST_METHOD(BackupSkipsNonJsonFiles)
        {
            TempDir dir;
            dir.WriteFile(L"settings.json", R"({"theme":"dark"})");
            dir.WriteFile(L"debug.log", "log data");

            updating::BackupConfigFiles(dir.path());

            Assert::IsTrue(dir.FileExists(L"ConfigBackup\\settings.json"));
            Assert::IsFalse(dir.FileExists(L"ConfigBackup\\debug.log"));
        }

        TEST_METHOD(BackupSkipsUpdatesDirectory)
        {
            TempDir dir;
            dir.WriteFile(L"settings.json", R"({"theme":"dark"})");
            dir.WriteFile(L"Updates\\installer.exe", "fake exe");

            updating::BackupConfigFiles(dir.path());

            Assert::IsFalse(dir.FileExists(L"ConfigBackup\\Updates"));
        }

        TEST_METHOD(BackupOverwritesPreviousBackup)
        {
            TempDir dir;
            dir.WriteFile(L"settings.json", R"({"version":1})");
            updating::BackupConfigFiles(dir.path());

            // Update the original
            dir.WriteFile(L"settings.json", R"({"version":2})");
            updating::BackupConfigFiles(dir.path());

            Assert::AreEqual(std::string(R"({"version":2})"), dir.ReadFile(L"ConfigBackup\\settings.json"));
        }
    };

    TEST_CLASS(RestoreCorruptedConfigsTests)
    {
    public:
        TEST_METHOD(RestoreFixesCorruptedRootFile)
        {
            TempDir dir;
            const std::string goodContent = R"({"theme":"dark"})";
            dir.WriteFile(L"settings.json", goodContent);

            // Backup
            updating::BackupConfigFiles(dir.path());

            // Corrupt the original
            std::vector<char> corrupted(goodContent.size(), '\0');
            dir.WriteFileBytes(L"settings.json", corrupted);
            Assert::IsTrue(updating::IsJsonFileCorrupted(dir.path() / L"settings.json"));

            // Restore
            updating::RestoreCorruptedConfigs(dir.path());

            Assert::IsFalse(updating::IsJsonFileCorrupted(dir.path() / L"settings.json"));
            Assert::AreEqual(goodContent, dir.ReadFile(L"settings.json"));
        }

        TEST_METHOD(RestoreFixesCorruptedModuleFile)
        {
            TempDir dir;
            const std::string goodContent = R"({"workspaces":[]})";
            dir.WriteFile(L"Workspaces\\workspaces.json", goodContent);
            dir.WriteFile(L"settings.json", R"({})");

            updating::BackupConfigFiles(dir.path());

            // Corrupt the module file
            std::vector<char> corrupted(goodContent.size(), '\0');
            dir.WriteFileBytes(L"Workspaces\\workspaces.json", corrupted);

            updating::RestoreCorruptedConfigs(dir.path());

            Assert::AreEqual(goodContent, dir.ReadFile(L"Workspaces\\workspaces.json"));
        }

        TEST_METHOD(RestoreLeavesCleanFilesUntouched)
        {
            TempDir dir;
            dir.WriteFile(L"settings.json", R"({"version":1})");

            updating::BackupConfigFiles(dir.path());

            // Modify original (but keep it clean JSON)
            dir.WriteFile(L"settings.json", R"({"version":2})");

            updating::RestoreCorruptedConfigs(dir.path());

            // Should NOT have been restored since it's not corrupted
            Assert::AreEqual(std::string(R"({"version":2})"), dir.ReadFile(L"settings.json"));
        }

        TEST_METHOD(RestoreHandlesMissingBackupDirectory)
        {
            TempDir dir;
            dir.WriteFile(L"settings.json", R"({"theme":"dark"})");

            // No backup was created - restore should silently do nothing
            updating::RestoreCorruptedConfigs(dir.path());

            Assert::AreEqual(std::string(R"({"theme":"dark"})"), dir.ReadFile(L"settings.json"));
        }

        TEST_METHOD(FullBackupAndRestoreRoundTrip)
        {
            TempDir dir;

            // Set up a realistic config structure
            dir.WriteFile(L"settings.json", R"({"startup":true,"theme":"dark"})");
            dir.WriteFile(L"FancyZones\\settings.json", R"({"zones":[{"id":1}]})");
            dir.WriteFile(L"Workspaces\\workspaces.json", R"({"workspaces":[{"name":"dev"}]})");
            dir.WriteFile(L"KeyboardManager\\default.json", R"({"remaps":[]})");

            // Backup
            updating::BackupConfigFiles(dir.path());

            // Corrupt some files (simulating #46179 scenario)
            dir.WriteFileBytes(L"Workspaces\\workspaces.json", std::vector<char>(100, '\0'));
            dir.WriteFileBytes(L"settings.json", std::vector<char>(50, '\0'));
            // Leave FancyZones and KBM clean

            // Restore
            updating::RestoreCorruptedConfigs(dir.path());

            // Corrupted files should be restored
            Assert::AreEqual(std::string(R"({"startup":true,"theme":"dark"})"), dir.ReadFile(L"settings.json"));
            Assert::AreEqual(std::string(R"({"workspaces":[{"name":"dev"}]})"), dir.ReadFile(L"Workspaces\\workspaces.json"));

            // Clean files should be unchanged
            Assert::AreEqual(std::string(R"({"zones":[{"id":1}]})"), dir.ReadFile(L"FancyZones\\settings.json"));
            Assert::AreEqual(std::string(R"({"remaps":[]})"), dir.ReadFile(L"KeyboardManager\\default.json"));
        }
    };

    TEST_CLASS(VersionHelperUpdateTests)
    {
    public:
        TEST_METHOD(NewerVersionIsDetected)
        {
            VersionHelper current(0, 85, 0);
            VersionHelper newer(0, 86, 0);

            Assert::IsTrue(newer > current);
            Assert::IsFalse(current > newer);
        }

        TEST_METHOD(SameVersionIsNotNewer)
        {
            VersionHelper current(0, 85, 1);
            VersionHelper same(0, 85, 1);

            Assert::IsFalse(same > current);
            Assert::IsFalse(current > same);
        }

        TEST_METHOD(PatchVersionIsDetected)
        {
            VersionHelper current(0, 85, 0);
            VersionHelper patch(0, 85, 1);

            Assert::IsTrue(patch > current);
        }

        TEST_METHOD(VersionParsedFromGitHubTag)
        {
            auto version = VersionHelper::fromString(L"v0.85.1");

            Assert::IsTrue(version.has_value());
            Assert::AreEqual(0ull, version->major);
            Assert::AreEqual(85ull, version->minor);
            Assert::AreEqual(1ull, version->revision);
        }
    };
}
