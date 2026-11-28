// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#include <Windows.h>
#include <CppUnitTest.h>
#include <filesystem>
#include <fstream>
#include <sstream>
#include <string>

#include <common/updating/configBackup.h>

#include "..\BugReportTool\ReportPrivateData.h"
#include "..\BugReportTool\ZipTools\ZipFolder.h"

using namespace Microsoft::VisualStudio::CppUnitTestFramework;
namespace fs = std::filesystem;

namespace BugReportToolUnitTests
{
    // How the report treats private data in a copy of the settings folder, the updater's copy (ConfigBackup)
    // included. Each test works in a folder of its own under %TEMP%.
    TEST_CLASS (ReportPrivateDataTests)
    {
    private:
        fs::path workDir;

        // A Keyboard Manager profile with a key sequence (S, E, C, R, E, T), a text remap and a program remap
        static constexpr const char* ProfileJson =
            R"({"remapKeys":{"inProcess":[],"inProcessSequence":[{"originalKeys":"112","newRemapKeys":"83;69;67;82;69;84"}]},)"
            R"("remapKeysToText":{"inProcess":[{"originalKeys":"113","unicodeText":"private-text-fixture"}]},)"
            R"("remapShortcuts":{"global":[{"originalKeys":"17;65","newRemapKeys":"","runProgramFilePath":"C:\\private-program-fixture.exe"}],"appSpecific":[]},)"
            R"("remapShortcutsToText":{"global":[],"appSpecific":[]}})";

        static void WriteFile(const fs::path& file, const std::string& text)
        {
            fs::create_directories(file.parent_path());
            std::ofstream out{ file, std::ios::binary };
            out << text;
        }

        static std::string ReadFile(const fs::path& file)
        {
            std::ifstream in{ file, std::ios::binary };
            std::stringstream text;
            text << in.rdbuf();
            return text.str();
        }

        static bool Contains(const std::string& text, const std::string& part)
        {
            return text.find(part) != std::string::npos;
        }

        // A settings folder as PowerToys keeps it, with the updater's copy made by the updater's own code
        fs::path MakeSettingsFolderWithUpdaterCopy()
        {
            const fs::path settings = workDir / L"settings";
            WriteFile(settings / L"settings.json", R"({"enabled":{}})");
            WriteFile(settings / L"Keyboard Manager" / L"settings.json", R"({"properties":{"activeConfiguration":{"value":"default"}}})");
            WriteFile(settings / L"Keyboard Manager" / L"default.json", ProfileJson);
            WriteFile(settings / L"AdvancedPaste" / L"settings.json", R"({"properties":{"custom-actions":{"value":[{"name":"private-name-fixture","prompt":"private-prompt-fixture"}]}}})");

            const auto backup = updating::BackupConfigFiles(settings);
            Assert::AreEqual(0, backup.errors);
            Assert::IsTrue(fs::exists(settings / L"ConfigBackup" / L"Keyboard Manager" / L"default.json"));
            return settings;
        }

        // The report's working copy of the settings folder, as the tool makes it
        fs::path CopyForReport(const fs::path& settings)
        {
            const fs::path report = workDir / L"report";
            fs::copy(settings, report, fs::copy_options::recursive);
            return report;
        }

        static void AssertProfileCleaned(const std::string& profile)
        {
            Assert::IsFalse(Contains(profile, "83;69;67;82;69;84"), L"sequence targets");
            Assert::IsFalse(Contains(profile, "private-text-fixture"), L"text remap");
            Assert::IsFalse(Contains(profile, "private-program-fixture"), L"program path");
            Assert::IsTrue(Contains(profile, "<private_data>"));
            Assert::IsTrue(Contains(profile, R"("originalKeys":"112")"), L"the source key stays, for diagnosis");
        }

    public:
        TEST_METHOD_INITIALIZE(CreateWorkDir)
        {
            wchar_t name[64];
            swprintf_s(name, L"BugReportToolTests-%lu-%llu", GetCurrentProcessId(), GetTickCount64());
            workDir = fs::temp_directory_path() / name;
            fs::create_directories(workDir);
        }

        TEST_METHOD_CLEANUP(RemoveWorkDir)
        {
            std::error_code err;
            fs::remove_all(workDir, err);
        }

        // The final ZIP: the updater's copy of a profile is cleaned like the live one, and so are the updater's
        // copies of the other modules' files that the tool cleans.
        TEST_METHOD (FinalZip_UpdaterCopyIsCleanedLikeTheLiveSettings)
        {
            const fs::path report = CopyForReport(MakeSettingsFolderWithUpdaterCopy());
            Assert::IsTrue(HideUserPrivateInfo(report));

            const fs::path zipDir = workDir / L"zip";
            fs::create_directories(zipDir);
            ZipFolder(zipDir, report);
            fs::path zip;
            for (const auto& entry : fs::directory_iterator(zipDir))
            {
                if (entry.path().extension() == L".zip")
                {
                    zip = entry.path();
                }
            }
            Assert::IsFalse(zip.empty(), L"no ZIP was made");

            const fs::path extracted = workDir / L"extracted";
            fs::create_directories(extracted);
            wchar_t windowsDir[MAX_PATH];
            GetWindowsDirectoryW(windowsDir, MAX_PATH);
            std::wstring commandLine = L"\"" + (fs::path(windowsDir) / L"System32\\tar.exe").wstring() + L"\" -x -f \"" + zip.wstring() + L"\" -C \"" + extracted.wstring() + L"\"";
            STARTUPINFOW si{ .cb = sizeof(si) };
            PROCESS_INFORMATION pi{};
            Assert::IsTrue(CreateProcessW(nullptr, commandLine.data(), nullptr, nullptr, FALSE, CREATE_NO_WINDOW, nullptr, nullptr, &si, &pi) != FALSE);
            const DWORD waited = WaitForSingleObject(pi.hProcess, 60000);
            DWORD tarExitCode = 1;
            if (waited == WAIT_OBJECT_0)
            {
                GetExitCodeProcess(pi.hProcess, &tarExitCode);
            }
            else
            {
                TerminateProcess(pi.hProcess, 1);
            }
            CloseHandle(pi.hThread);
            CloseHandle(pi.hProcess);
            Assert::AreEqual((DWORD)WAIT_OBJECT_0, waited, L"tar did not finish extracting the ZIP");
            Assert::AreEqual((DWORD)0, tarExitCode, L"tar could not extract the ZIP");

            AssertProfileCleaned(ReadFile(extracted / L"Keyboard Manager" / L"default.json"));
            AssertProfileCleaned(ReadFile(extracted / L"ConfigBackup" / L"Keyboard Manager" / L"default.json"));

            const std::string advancedPaste = ReadFile(extracted / L"ConfigBackup" / L"AdvancedPaste" / L"settings.json");
            Assert::IsFalse(advancedPaste.empty());
            Assert::IsFalse(Contains(advancedPaste, "private-prompt-fixture"));
            Assert::IsFalse(Contains(advancedPaste, "private-name-fixture"));
        }

        // Copies that are not profile files: a json file in a subfolder is cleaned, a file of another kind is
        // taken out (it cannot be searched for private data), a log is kept.
        TEST_METHOD (OtherCopiesInTheKeyboardManagerFolder_AreCleanedOrTakenOut)
        {
            const fs::path report = CopyForReport(MakeSettingsFolderWithUpdaterCopy());
            const fs::path backupFolder = report / L"ConfigBackup" / L"Keyboard Manager";
            WriteFile(backupFolder / L"default.json.bak", ProfileJson);
            WriteFile(report / L"Keyboard Manager" / L"old copies" / L"Work.json", ProfileJson);
            WriteFile(report / L"Keyboard Manager" / L"old copies" / L"Work.txt", "private-text-fixture");
            WriteFile(report / L"Keyboard Manager" / L"Logs" / L"engine.log", "log line");

            Assert::IsTrue(HideUserPrivateInfo(report));

            Assert::IsFalse(fs::exists(backupFolder / L"default.json.bak"));
            AssertProfileCleaned(ReadFile(report / L"Keyboard Manager" / L"old copies" / L"Work.json"));
            Assert::IsFalse(fs::exists(report / L"Keyboard Manager" / L"old copies" / L"Work.txt"));
            Assert::IsTrue(fs::exists(report / L"Keyboard Manager" / L"Logs" / L"engine.log"));
            Assert::IsTrue(fs::exists(report / L"Keyboard Manager" / L"settings.json"), L"kept as it is");
        }

        // A profile copy that can be neither cleaned nor removed (here: held open without sharing) makes the
        // tool report failure, so that no archive is made.
        TEST_METHOD (ProfileThatCanBeNeitherCleanedNorRemoved_FailsTheReport)
        {
            const fs::path report = CopyForReport(MakeSettingsFolderWithUpdaterCopy());
            const fs::path locked = report / L"ConfigBackup" / L"Keyboard Manager" / L"default.json";
            HANDLE handle = CreateFileW(locked.c_str(), GENERIC_READ, 0, nullptr, OPEN_EXISTING, FILE_ATTRIBUTE_NORMAL, nullptr);
            Assert::IsTrue(handle != INVALID_HANDLE_VALUE);

            const bool hidden = HideUserPrivateInfo(report);
            CloseHandle(handle);

            Assert::IsFalse(hidden);
            Assert::IsTrue(Contains(ReadFile(locked), "83;69;67;82;69;84"), L"the file really was left as it was");
        }

        // A profile copy that cannot be read as JSON is replaced by a note: its content is gone from the report.
        TEST_METHOD (ProfileThatIsNotJson_IsReplacedByANote)
        {
            const fs::path report = CopyForReport(MakeSettingsFolderWithUpdaterCopy());
            const fs::path broken = report / L"ConfigBackup" / L"Keyboard Manager" / L"broken.json";
            WriteFile(broken, "{ not json 83;69;67;82;69;84");

            Assert::IsTrue(HideUserPrivateInfo(report));

            const std::string note = ReadFile(broken);
            Assert::IsTrue(Contains(note, "bugReportToolNote"));
            Assert::IsFalse(Contains(note, "83;69;67;82;69;84"));
        }
    };
}
