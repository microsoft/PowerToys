// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#include "pch.h"

#include <algorithm>
#include <filesystem>
#include <fstream>
#include <iterator>
#include <string>
#include <vector>

#include <common/updating/configBackup.h>
#include <common/updating/updateLifecycle.h>

using namespace Microsoft::VisualStudio::CppUnitTestFramework;

namespace fs = std::filesystem;

namespace UpdatingUnitTests
{
    // Helper to create a temp directory for test isolation.
    // Each instance gets a unique subdirectory to prevent test interference.
    class TempDir
    {
    public:
        TempDir()
        {
            wchar_t tempPath[MAX_PATH + 1];
            GetTempPathW(MAX_PATH, tempPath);
            static std::atomic<int> counter{0};
            m_path = fs::path(tempPath) / (L"PowerToysUpdateTests_" + std::to_wstring(counter++));

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
        // Tests IsJsonFileCorrupted: valid JSON with no null bytes returns false.
        // Covers: configBackup.h IsJsonFileCorrupted — happy path, full file scan.
        TEST_METHOD(CleanJsonFileIsNotCorrupted)
        {
            TempDir dir;
            dir.WriteFile(L"settings.json", R"({"theme":"dark","startup":true})");

            Assert::IsFalse(updating::IsJsonFileCorrupted(dir.path() / L"settings.json"));
        }

        // Tests IsJsonFileCorrupted: zero-length file returns false (empty is not corrupted).
        // Covers: configBackup.h IsJsonFileCorrupted — file.read returns 0 bytes immediately.
        TEST_METHOD(EmptyFileIsNotCorrupted)
        {
            TempDir dir;
            dir.WriteFile(L"empty.json", "");

            Assert::IsFalse(updating::IsJsonFileCorrupted(dir.path() / L"empty.json"));
        }

        // Tests IsJsonFileCorrupted: file containing embedded null bytes returns true.
        // Covers: configBackup.h IsJsonFileCorrupted — null byte detection within buffer.
        TEST_METHOD(FileWithNullBytesIsCorrupted)
        {
            TempDir dir;
            std::vector<char> corrupted = { '{', '"', 'a', '"', ':', '\0', '\0', '\0', '}' };
            dir.WriteFileBytes(L"corrupted.json", corrupted);

            Assert::IsTrue(updating::IsJsonFileCorrupted(dir.path() / L"corrupted.json"));
        }

        // Tests IsJsonFileCorrupted: file entirely filled with 0x00 bytes returns true.
        // Reproduces the exact bug from #46179 where installer zeroed out JSON files.
        // Covers: configBackup.h IsJsonFileCorrupted — first byte is null.
        TEST_METHOD(FileFilledWithNullBytesIsCorrupted)
        {
            TempDir dir;
            std::vector<char> allNulls(1024, '\0');
            dir.WriteFileBytes(L"workspaces.json", allNulls);

            Assert::IsTrue(updating::IsJsonFileCorrupted(dir.path() / L"workspaces.json"));
        }

        // Tests IsJsonFileCorrupted: path that does not exist returns false.
        // Covers: configBackup.h IsJsonFileCorrupted — file.is_open() check.
        TEST_METHOD(NonExistentFileIsNotCorrupted)
        {
            TempDir dir;

            Assert::IsFalse(updating::IsJsonFileCorrupted(dir.path() / L"missing.json"));
        }

        // Tests IsJsonFileCorrupted: file larger than the 4096-byte read chunk
        // with no null bytes returns false.
        // Covers: configBackup.h IsJsonFileCorrupted — multi-chunk while loop.
        TEST_METHOD(LargeCleanFileIsNotCorrupted)
        {
            TempDir dir;
            std::string largeContent(8192, 'x');
            dir.WriteFile(L"large.json", largeContent);

            Assert::IsFalse(updating::IsJsonFileCorrupted(dir.path() / L"large.json"));
        }

        // Tests IsJsonFileCorrupted: null byte placed after the first 4096-byte
        // chunk boundary is still detected.
        // Covers: configBackup.h IsJsonFileCorrupted — second chunk scan.
        TEST_METHOD(NullByteAtEndOfLargeFileIsDetected)
        {
            TempDir dir;
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
        // Tests BackupConfigFiles: root-level .json files are copied to ConfigBackup.
        // Covers: configBackup.h BackupConfigFiles — root directory_iterator,
        //         is_regular_file && extension == ".json" branch.
        // Setup: Two root-level JSON files.
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

        // Tests BackupConfigFiles: .json files inside module subdirectories are
        // copied to ConfigBackup/<module>/.
        // Covers: configBackup.h BackupConfigFiles — is_directory branch,
        //         module directory_iterator with extension filter.
        // Setup: Root JSON + two module directories with JSON files.
        TEST_METHOD(BackupCopiesModuleJsonFiles)
        {
            TempDir dir;
            dir.WriteFile(L"settings.json", R"({"theme":"dark"})");
            dir.WriteFile(L"FancyZones\\settings.json", R"({"zones":[]})");
            dir.WriteFile(L"Workspaces\\workspaces.json", R"({"workspaces":[]})");

            updating::BackupConfigFiles(dir.path());

            Assert::IsTrue(dir.FileExists(L"ConfigBackup\\FancyZones\\settings.json"));
            Assert::IsTrue(dir.FileExists(L"ConfigBackup\\Workspaces\\workspaces.json"));
            Assert::AreEqual(std::string(R"({"zones":[]})"),
                dir.ReadFile(L"ConfigBackup\\FancyZones\\settings.json"));
        }

        // Tests BackupConfigFiles: non-.json files at root level are not copied.
        // Covers: configBackup.h BackupConfigFiles — extension filter excludes .log.
        // Setup: One JSON file + one .log file at root.
        TEST_METHOD(BackupSkipsNonJsonFiles)
        {
            TempDir dir;
            dir.WriteFile(L"settings.json", R"({"theme":"dark"})");
            dir.WriteFile(L"debug.log", "log data");

            updating::BackupConfigFiles(dir.path());

            Assert::IsTrue(dir.FileExists(L"ConfigBackup\\settings.json"));
            Assert::IsFalse(dir.FileExists(L"ConfigBackup\\debug.log"));
        }

        // Tests BackupConfigFiles: the "Updates" directory is explicitly skipped.
        // Covers: configBackup.h BackupConfigFiles — dirName == L"Updates" continue.
        // Setup: Root JSON + Updates directory containing a file.
        TEST_METHOD(BackupSkipsUpdatesDirectory)
        {
            TempDir dir;
            dir.WriteFile(L"settings.json", R"({"theme":"dark"})");
            dir.WriteFile(L"Updates\\installer.exe", "fake exe");

            updating::BackupConfigFiles(dir.path());

            Assert::IsFalse(dir.FileExists(L"ConfigBackup\\Updates"));
        }

        // Tests BackupConfigFiles: running backup twice overwrites the previous
        // backup with current file content.
        // Covers: configBackup.h BackupConfigFiles — fs::remove_all(backupDir) +
        //         copy_options::overwrite_existing.
        // Setup: Backup, modify original, backup again.
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

        // Tests BackupConfigFiles: non-.json files inside module subdirectories
        // (e.g., FancyZones/zones.dat) should NOT be backed up.
        // Covers: configBackup.h BackupConfigFiles — extension filter in module loop.
        TEST_METHOD(BackupSkipsNonJsonFilesInModuleDirs)
        {
            TempDir dir;
            dir.WriteFile(L"settings.json", R"({})");
            dir.WriteFile(L"FancyZones\\settings.json", R"({"zones":[]})");
            dir.WriteFile(L"FancyZones\\zones.dat", "binary data");

            updating::BackupConfigFiles(dir.path());

            Assert::IsTrue(dir.FileExists(L"ConfigBackup\\FancyZones\\settings.json"));
            Assert::IsFalse(dir.FileExists(L"ConfigBackup\\FancyZones\\zones.dat"));
        }

        // Tests BackupConfigFiles: empty root directory with no files produces
        // an empty ConfigBackup dir without errors.
        // Covers: configBackup.h BackupConfigFiles — empty directory_iterator.
        TEST_METHOD(BackupEmptyRootDirSucceeds)
        {
            TempDir dir;
            // Root dir exists but has no files

            updating::BackupConfigFiles(dir.path());

            Assert::IsTrue(dir.FileExists(L"ConfigBackup"));
        }
    };

    TEST_CLASS(RestoreCorruptedConfigsTests)
    {
    public:
        // Tests RestoreCorruptedConfigs: corrupted root-level JSON file is restored
        // from the good backup copy.
        // Covers: configBackup.h RestoreCorruptedConfigs — root file restore branch,
        //         fs::exists + IsJsonFileCorrupted + backup integrity check.
        // Setup: Good file -> backup -> corrupt original -> restore.
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

        // Tests RestoreCorruptedConfigs: corrupted module-level JSON file is restored
        // from the good backup copy.
        // Covers: configBackup.h RestoreCorruptedConfigs — module directory branch,
        //         moduleBackupEntry restore with integrity check.
        // Setup: Module file + root file -> backup -> corrupt module file -> restore.
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

        // Tests RestoreCorruptedConfigs: clean (non-corrupted) files are NOT
        // overwritten by backup — preserves user changes made after backup.
        // Covers: configBackup.h RestoreCorruptedConfigs — IsJsonFileCorrupted
        //         returns false, copy_file is skipped.
        // Setup: File -> backup -> modify (but keep valid) -> restore.
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

        // Tests RestoreCorruptedConfigs: when no ConfigBackup directory exists,
        // restore silently does nothing (no crash, no data loss).
        // Covers: configBackup.h RestoreCorruptedConfigs — !fs::exists(backupDir)
        //         early return.
        // Setup: File with no prior backup.
        TEST_METHOD(RestoreHandlesMissingBackupDirectory)
        {
            TempDir dir;
            dir.WriteFile(L"settings.json", R"({"theme":"dark"})");

            // No backup was created - restore should silently do nothing
            updating::RestoreCorruptedConfigs(dir.path());

            Assert::AreEqual(std::string(R"({"theme":"dark"})"), dir.ReadFile(L"settings.json"));
        }

        // Tests RestoreCorruptedConfigs: end-to-end scenario with multiple modules,
        // some corrupted and some clean, verifying selective restore.
        // Covers: configBackup.h RestoreCorruptedConfigs — both root and module
        //         branches, selective restore based on corruption status.
        // Setup: 4 modules -> backup -> corrupt 2 -> restore -> verify all 4.
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

        // Tests RestoreCorruptedConfigs: when the original file has been deleted
        // (not corrupted), restore should NOT recreate it from backup. The installer
        // may have intentionally removed obsolete config files.
        // Covers: configBackup.h RestoreCorruptedConfigs — fs::exists guard.
        TEST_METHOD(RestoreSkipsDeletedOriginals)
        {
            TempDir dir;
            dir.WriteFile(L"obsolete.json", R"({"old":true})");
            updating::BackupConfigFiles(dir.path());

            // Installer deletes the file
            std::error_code ec;
            fs::remove(dir.path() / L"obsolete.json", ec);

            updating::RestoreCorruptedConfigs(dir.path());

            // Should NOT be recreated
            Assert::IsFalse(dir.FileExists(L"obsolete.json"));
        }

        // Tests RestoreCorruptedConfigs: when the backup file itself is corrupted
        // (e.g., disk error during backup), restore should NOT copy corrupted
        // backup over the original — that would make things worse.
        // Covers: configBackup.h RestoreCorruptedConfigs — backup integrity check (B2 fix).
        TEST_METHOD(RestoreSkipsCorruptedBackup)
        {
            TempDir dir;
            dir.WriteFile(L"settings.json", R"({"theme":"dark"})");
            updating::BackupConfigFiles(dir.path());

            // Corrupt BOTH the original AND the backup
            std::vector<char> nulls(50, '\0');
            dir.WriteFileBytes(L"settings.json", nulls);
            dir.WriteFileBytes(L"ConfigBackup\\settings.json", nulls);

            updating::RestoreCorruptedConfigs(dir.path());

            // Original should still be corrupted — we don't restore from bad backup
            Assert::IsTrue(updating::IsJsonFileCorrupted(dir.path() / L"settings.json"));
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
        // Tests full upgrade simulation: backup -> installer corrupts files -> restore.
        // Verifies that corrupted files are restored and clean files are untouched.
        // Covers: configBackup.h BackupConfigFiles + RestoreCorruptedConfigs —
        //         end-to-end with 5 modules, 2 corrupted, 3 clean.
        // Setup: Realistic config structure with multiple modules.
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

        // Tests upgrade from an old version that has fewer modules than the new version.
        // Verifies that new module configs (created by the installer) are not touched
        // by restore, while corrupted old configs are restored.
        // Covers: configBackup.h RestoreCorruptedConfigs — module dir in root that
        //         has no corresponding backup entry.
        // Setup: Old version with 1 module -> backup -> new installer adds module -> corrupt old -> restore.
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
        // Tests BuildStage2Arguments: output contains the stage 2 flag, installer path,
        // and install directory — all three components needed for Stage 2.
        // Covers: updateLifecycle.h BuildStage2Arguments — concatenation logic.
        // Setup: Typical paths with spaces (Program Files).
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

        // Tests BuildStage2Arguments: both paths are wrapped in double quotes to
        // survive CommandLineToArgvW parsing when paths contain spaces.
        // Covers: updateLifecycle.h BuildStage2Arguments — quote wrapping.
        // Setup: Installer path with spaces.
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

        // Tests BuildPowerToysExePath: appends "PowerToys.exe" to the install dir.
        // Covers: updateLifecycle.h BuildPowerToysExePath — fs::path / operator.
        // Setup: Standard install path without trailing backslash.
        TEST_METHOD(BuildPowerToysExePathAppendsExeName)
        {
            const auto path = updating::BuildPowerToysExePath(L"C:\\Program Files\\PowerToys");
            Assert::AreEqual(std::wstring(L"C:\\Program Files\\PowerToys\\PowerToys.exe"), path);
        }

        // Tests BuildPowerToysExePath: trailing backslash does not produce double
        // backslash (e.g., "...PowerToys\\PowerToys.exe").
        // Covers: updateLifecycle.h BuildPowerToysExePath — fs::path normalizes separators.
        // Setup: Install path with trailing backslash.
        TEST_METHOD(BuildPowerToysExePathHandlesTrailingBackslash)
        {
            const auto path = updating::BuildPowerToysExePath(L"C:\\Program Files\\PowerToys\\");
            Assert::AreEqual(std::wstring(L"C:\\Program Files\\PowerToys\\PowerToys.exe"), path);
        }

        // Tests BuildPowerToysExePath: empty string produces just "PowerToys.exe".
        // Covers: updateLifecycle.h BuildPowerToysExePath — fs::path with empty input.
        // Setup: Empty install directory string.
        TEST_METHOD(BuildPowerToysExePathHandlesEmptyString)
        {
            const auto path = updating::BuildPowerToysExePath(L"");
            Assert::AreEqual(std::wstring(L"PowerToys.exe"), path);
        }

        // Tests CanRelaunchAfterUpdate: returns true when Stage 2 receives
        // the install directory (argCount >= 4), false otherwise.
        // This is the gate that prevents relaunch when using an old Stage 1
        // that didn't pass the install dir (#42004/#43011/#44071).
        // Covers: updateLifecycle.h CanRelaunchAfterUpdate.
        TEST_METHOD(CanRelaunchReflectsArgCount)
        {
            // Old Stage 1 (pre-fix): only passed action + installer = 3 args
            Assert::IsFalse(updating::CanRelaunchAfterUpdate(0));
            Assert::IsFalse(updating::CanRelaunchAfterUpdate(1));
            Assert::IsFalse(updating::CanRelaunchAfterUpdate(2));
            Assert::IsFalse(updating::CanRelaunchAfterUpdate(3));

            // New Stage 1 (post-fix): passes action + installer + installDir = 4 args
            Assert::IsTrue(updating::CanRelaunchAfterUpdate(4));
            Assert::IsTrue(updating::CanRelaunchAfterUpdate(5));
        }

        // Tests BuildStage2Arguments + CommandLineToArgvW round-trip: the exact
        // scenario where Stage 1 builds args and Windows parses them in Stage 2.
        // Verifies quoting is correct so paths with spaces survive the round trip.
        // Covers: updateLifecycle.h BuildStage2Arguments — quote correctness.
        // Setup: Realistic paths with spaces and version numbers.
        TEST_METHOD(Stage2ArgumentsCanBeRoundTrippedThroughCommandLineToArgvW)
        {
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