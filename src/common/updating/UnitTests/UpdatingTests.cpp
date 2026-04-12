// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#include "pch.h"

#include <filesystem>
#include <fstream>
#include <string>

#include <common/updating/configBackup.h>
#include <common/updating/updateLifecycle.h>

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

    // Simulates what actually happens during a PowerToys upgrade:
    // 1. User has settings from normal use
    // 2. Updater backs up before install (Stage 1)
    // 3. Installer runs and corrupts some files (simulated)
    // 4. Updater restores corrupted files (Stage 2)
    // 5. PT relaunches and finds working configs
    TEST_CLASS(UpgradeSimulationTests)
    {
    public:
        TEST_METHOD(SimulateUpgradeWithCorruption)
        {
            TempDir dir;

            // === User's real config state before upgrade ===
            dir.WriteFile(L"settings.json",
                R"({"startup":true,"theme":"dark","run_elevated":false,"download_updates_automatically":true})");
            dir.WriteFile(L"FancyZones\\settings.json",
                R"({"zones":[{"id":1,"rect":{"x":0,"y":0,"w":960,"h":1080}}]})");
            dir.WriteFile(L"Workspaces\\workspaces.json",
                R"({"workspaces":[{"name":"dev","apps":["code","terminal"]}]})");
            dir.WriteFile(L"KeyboardManager\\default.json",
                R"({"remapKeys":{"inProcess":[{"original":"0x41","new":"0x42"}]}})");
            dir.WriteFile(L"MouseWithoutBorders\\settings.json",
                R"({"machineKey":"abc123","connectToAll":true})");

            // Non-JSON files that should be left alone
            dir.WriteFile(L"update.log", "2026-04-11 update started");

            // === Stage 1: Backup before killing PT ===
            updating::BackupConfigFiles(dir.path());

            // Verify backup was created correctly
            Assert::IsTrue(dir.FileExists(L"ConfigBackup\\settings.json"));
            Assert::IsTrue(dir.FileExists(L"ConfigBackup\\FancyZones\\settings.json"));
            Assert::IsTrue(dir.FileExists(L"ConfigBackup\\Workspaces\\workspaces.json"));
            Assert::IsTrue(dir.FileExists(L"ConfigBackup\\KeyboardManager\\default.json"));
            Assert::IsTrue(dir.FileExists(L"ConfigBackup\\MouseWithoutBorders\\settings.json"));
            Assert::IsFalse(dir.FileExists(L"ConfigBackup\\update.log"));

            // === Installer runs: some files get corrupted (the #46179 scenario) ===
            // Workspaces JSON filled with null bytes
            dir.WriteFileBytes(L"Workspaces\\workspaces.json", std::vector<char>(512, '\0'));
            // Main settings partially corrupted (null bytes injected)
            std::vector<char> partialCorrupt = { '{', '"', 's', '\0', '\0', '\0', '\0', '}' };
            dir.WriteFileBytes(L"settings.json", partialCorrupt);

            // FancyZones, KBM, and MWB survive the install fine
            // (this is realistic - not all files get corrupted)

            // === Stage 2: Restore after install completes ===
            updating::RestoreCorruptedConfigs(dir.path());

            // === Verify: PT relaunches and finds working configs ===

            // Corrupted files should be restored from backup
            Assert::IsFalse(updating::IsJsonFileCorrupted(dir.path() / L"settings.json"));
            Assert::IsFalse(updating::IsJsonFileCorrupted(dir.path() / L"Workspaces\\workspaces.json"));
            Assert::AreEqual(
                std::string(R"({"startup":true,"theme":"dark","run_elevated":false,"download_updates_automatically":true})"),
                dir.ReadFile(L"settings.json"));
            Assert::AreEqual(
                std::string(R"({"workspaces":[{"name":"dev","apps":["code","terminal"]}]})"),
                dir.ReadFile(L"Workspaces\\workspaces.json"));

            // Clean files should be untouched (not overwritten with backup)
            Assert::AreEqual(
                std::string(R"({"zones":[{"id":1,"rect":{"x":0,"y":0,"w":960,"h":1080}}]})"),
                dir.ReadFile(L"FancyZones\\settings.json"));
            Assert::AreEqual(
                std::string(R"({"remapKeys":{"inProcess":[{"original":"0x41","new":"0x42"}]}})"),
                dir.ReadFile(L"KeyboardManager\\default.json"));
            Assert::AreEqual(
                std::string(R"({"machineKey":"abc123","connectToAll":true})"),
                dir.ReadFile(L"MouseWithoutBorders\\settings.json"));
        }

        TEST_METHOD(SimulateUpgradeWithNoCorruption)
        {
            TempDir dir;

            // User configs
            dir.WriteFile(L"settings.json", R"({"theme":"light"})");
            dir.WriteFile(L"FancyZones\\settings.json", R"({"zones":[]})");

            // Backup
            updating::BackupConfigFiles(dir.path());

            // Install succeeds cleanly - no corruption
            // (Maybe user updated a setting between backup and restore)
            dir.WriteFile(L"settings.json", R"({"theme":"dark"})");

            // Restore should NOT overwrite clean files
            updating::RestoreCorruptedConfigs(dir.path());

            // User's post-install change should be preserved
            Assert::AreEqual(std::string(R"({"theme":"dark"})"), dir.ReadFile(L"settings.json"));
            Assert::AreEqual(std::string(R"({"zones":[]})"), dir.ReadFile(L"FancyZones\\settings.json"));
        }

        TEST_METHOD(SimulateUpgradeFromVeryOldVersion)
        {
            TempDir dir;

            // Old version had fewer modules - only settings.json
            dir.WriteFile(L"settings.json", R"({"theme":"dark","powertoys_version":"v0.60.0"})");

            // Backup
            updating::BackupConfigFiles(dir.path());

            // New installer creates new module dirs that didn't exist before
            dir.WriteFile(L"NewModule\\settings.json", R"({"enabled":true})");

            // Old settings get corrupted during upgrade
            dir.WriteFileBytes(L"settings.json", std::vector<char>(100, '\0'));

            // Restore
            updating::RestoreCorruptedConfigs(dir.path());

            // Old settings restored
            Assert::AreEqual(
                std::string(R"({"theme":"dark","powertoys_version":"v0.60.0"})"),
                dir.ReadFile(L"settings.json"));

            // New module settings untouched (no backup existed for them)
            Assert::AreEqual(
                std::string(R"({"enabled":true})"),
                dir.ReadFile(L"NewModule\\settings.json"));
        }
    };

    // Tests for the update lifecycle: argument passing between Stage 1 and Stage 2,
    // relaunch path construction, and the handoff that was broken in #42004/#43011/#44071.
    TEST_CLASS(UpdateLifecycleTests)
    {
    public:
        TEST_METHOD(BuildStage2ArgumentsContainsInstallerAndInstallDir)
        {
            const auto args = updating::BuildStage2Arguments(
                L"-update_now_stage_2",
                L"C:\\Users\\test\\AppData\\Local\\PowerToys\\Updates\\powertoyssetup-x64.exe",
                L"C:\\Program Files\\PowerToys");

            // Must contain the stage 2 flag
            Assert::IsTrue(args.find(L"-update_now_stage_2") != std::wstring::npos);
            // Must contain the installer path (quoted)
            Assert::IsTrue(args.find(L"powertoyssetup-x64.exe") != std::wstring::npos);
            // Must contain the install directory (quoted) — this was MISSING before our fix
            Assert::IsTrue(args.find(L"C:\\Program Files\\PowerToys") != std::wstring::npos);
        }

        TEST_METHOD(BuildStage2ArgumentsQuotesBothPaths)
        {
            const auto args = updating::BuildStage2Arguments(
                L"-update_now_stage_2",
                L"C:\\path with spaces\\installer.exe",
                L"C:\\Program Files\\PowerToys");

            // Count quotes — should have 4 (open/close for each path)
            size_t quoteCount = std::count(args.begin(), args.end(), L'"');
            Assert::AreEqual(size_t{ 4 }, quoteCount);
        }

        TEST_METHOD(BuildPowerToysExePathAppendsExeName)
        {
            const auto path = updating::BuildPowerToysExePath(L"C:\\Program Files\\PowerToys");
            Assert::AreEqual(std::wstring(L"C:\\Program Files\\PowerToys\\PowerToys.exe"), path);
        }

        TEST_METHOD(BuildPowerToysExePathHandlesTrailingBackslash)
        {
            const auto path = updating::BuildPowerToysExePath(L"C:\\Program Files\\PowerToys\\");
            Assert::AreEqual(std::wstring(L"C:\\Program Files\\PowerToys\\PowerToys.exe"), path);
        }

        TEST_METHOD(BuildPowerToysExePathHandlesEmptyString)
        {
            const auto path = updating::BuildPowerToysExePath(L"");
            Assert::AreEqual(std::wstring(L"PowerToys.exe"), path);
        }

        TEST_METHOD(CanRelaunchReturnsTrueWithFourArgs)
        {
            // args[0]=exe, [1]=action, [2]=installer, [3]=installDir
            Assert::IsTrue(updating::CanRelaunchAfterUpdate(4));
            Assert::IsTrue(updating::CanRelaunchAfterUpdate(5));
        }

        TEST_METHOD(CanRelaunchReturnsFalseWithThreeArgs)
        {
            // Old Stage 1 that didn't pass install dir — the pre-fix behavior
            Assert::IsFalse(updating::CanRelaunchAfterUpdate(3));
            Assert::IsFalse(updating::CanRelaunchAfterUpdate(2));
            Assert::IsFalse(updating::CanRelaunchAfterUpdate(1));
            Assert::IsFalse(updating::CanRelaunchAfterUpdate(0));
        }

        TEST_METHOD(Stage2ArgumentsCanBeRoundTrippedThroughCommandLineToArgvW)
        {
            // This tests the EXACT scenario: Stage 1 builds args, Windows parses them
            // in Stage 2 via CommandLineToArgvW. If quoting is wrong, args get mangled.
            const std::wstring installerPath = L"C:\\Users\\test user\\AppData\\Local\\PowerToys\\Updates\\powertoyssetup-0.86.0-x64.exe";
            const std::wstring installDir = L"C:\\Program Files\\PowerToys";

            const auto args = updating::BuildStage2Arguments(L"-update_now_stage_2", installerPath, installDir);

            // Simulate what Windows does: prepend a fake exe name and parse
            std::wstring commandLine = L"PowerToys.Update.exe " + args;

            int argc = 0;
            LPWSTR* argv = CommandLineToArgvW(commandLine.c_str(), &argc);
            Assert::IsNotNull(argv);
            Assert::AreEqual(4, argc);
            Assert::AreEqual(std::wstring(L"-update_now_stage_2"), std::wstring(argv[1]));
            Assert::AreEqual(installerPath, std::wstring(argv[2]));
            Assert::AreEqual(installDir, std::wstring(argv[3]));

            LocalFree(argv);
        }
    };
}
