#include "../BugReportTool/TextExpanderPrivacy.h"

#include <chrono>
#include <cstdlib>
#include <filesystem>
#include <fstream>
#include <string>
#include <vector>

#include <CppUnitTest.h>

using namespace Microsoft::VisualStudio::CppUnitTestFramework;

namespace fs = std::filesystem;

namespace BugReportToolUnitTests
{
    namespace
    {
        const wchar_t RootVariable[] = L"POWERTOYS_TE_PRIVACY_TEST_ROOT";

        bool WriteText(const fs::path& path, const char* contents)
        {
            fs::create_directories(path.parent_path());
            std::ofstream file(path, std::ios::binary);
            file << contents;
            return file.good();
        }

        std::string JsonString(const std::wstring& value)
        {
            std::wstring escaped;
            for (wchar_t ch : value)
            {
                if (ch == L'\\' || ch == L'"')
                {
                    escaped.push_back(L'\\');
                }

                escaped.push_back(ch);
            }

            return "\"" + winrt::to_string(escaped) + "\"";
        }

        bool WriteSettings(const fs::path& settingsRoot, const std::string& properties)
        {
            std::string contents = "{\"name\":\"TextExpander\",\"properties\":" + properties + "}";
            return WriteText(settingsRoot / L"TextExpander" / L"settings.json", contents.c_str());
        }

        bool ResetReport(const fs::path& settingsRoot, const fs::path& reportRoot)
        {
            std::error_code error;
            fs::remove_all(reportRoot, error);
            return TextExpanderPrivacy::CopyWithoutDirectoryLinks(settingsRoot, reportRoot);
        }

        struct SettingsCase
        {
            const wchar_t* name;
            std::string properties;
        };
    }

    TEST_CLASS(TextExpanderPrivacyTests)
    {
        fs::path root;
        fs::path settings;
        fs::path report;
        fs::path moduleFolder;
        fs::path configuredFolder;

        // A folder configured as the snippet location, holding both names and a file with a name
        // of its own.
        void CreateConfiguredFolder()
        {
            Assert::IsTrue(WriteText(configuredFolder / L"snippets.txt", "private configured-folder snippet") &&
                               WriteText(configuredFolder / L"PowerToysTextExpanderSnippets.txt", "private configured-folder snippet, current name") &&
                               WriteText(configuredFolder / L"my snippets.txt", "private configured file with its own name"),
                           L"Could not create configured-folder snippet fixture");
        }

        // The default folder already holds snippets.txt; this adds the current name beside it.
        void CreateCurrentDefaultSnippet()
        {
            Assert::IsTrue(WriteText(moduleFolder / L"PowerToysTextExpanderSnippets.txt", "private default snippet, current name"),
                           L"Could not create the current-name default snippet fixture");
        }

        fs::path CreateLinkedFolder()
        {
            fs::path linkedFolder = root / L"linked";
            Assert::IsTrue(WriteText(linkedFolder / L"PowerToysTextExpanderSnippets.txt", "private snippets behind a link"),
                           L"Could not create the linked snippet fixture");
            return linkedFolder;
        }

        // Copies the settings with std::filesystem, so ExcludeCopiedSnippetFile is checked on its own.
        void CopySettingsToReport()
        {
            fs::remove_all(report);
            fs::copy(settings, report, fs::copy_options::recursive);
        }

        bool MakeReport(const std::string& properties)
        {
            return WriteSettings(settings, properties) && ResetReport(settings, report) &&
                   TextExpanderPrivacy::ExcludeConfiguredSnippetFile(settings, report);
        }

        // Makes a report with each case's settings and checks that it leaves out every one of the
        // files, given relative to the report.
        void AssertExcluded(const std::vector<SettingsCase>& cases, const std::vector<fs::path>& files, const wchar_t* failure)
        {
            for (const SettingsCase& testCase : cases)
            {
                std::wstring message = std::wstring(failure) + L": " + testCase.name;
                Assert::IsTrue(MakeReport(testCase.properties), message.c_str());
                for (const fs::path& file : files)
                {
                    Assert::IsFalse(fs::exists(report / file), message.c_str());
                }
            }
        }

    public:
        TEST_METHOD_INITIALIZE(CreateSettings)
        {
            root = fs::temp_directory_path() /
                   (L"PowerToysTextExpanderPrivacy-" + std::to_wstring(std::chrono::steady_clock::now().time_since_epoch().count()));
            settings = root / L"settings";
            report = root / L"report";
            moduleFolder = settings / L"TextExpander";
            configuredFolder = moduleFolder / L"custom";
            Assert::IsTrue(WriteText(moduleFolder / L"snippets.txt", "private default snippet"), L"Could not create default snippet fixture");
        }

        TEST_METHOD_CLEANUP(RemoveSettings)
        {
            SetEnvironmentVariableW(RootVariable, nullptr);
            std::error_code error;
            fs::remove_all(root, error);
        }

        TEST_METHOD(CopyLeavesOutRunnerSettingsLock)
        {
            fs::path cliLock = settings / L"settings.json.cli-lock";
            Assert::IsTrue(WriteText(cliLock, "runner lock") && ResetReport(settings, report), L"Could not copy the settings with the runner lock");
            Assert::IsFalse(fs::exists(report / cliLock.filename()), L"Runner settings lock was copied into the report");
        }

        TEST_METHOD(CopiedSnippetFileIsExcludedThroughCaseVariantPath)
        {
            CopySettingsToReport();
            const wchar_t* message = L"Default snippet file was not excluded using a case-variant source path";
            Assert::IsTrue(TextExpanderPrivacy::ExcludeCopiedSnippetFile(settings, report, settings / L"textexpander" / L"SNIPPETS.TXT"), message);
            Assert::IsFalse(fs::exists(report / L"TextExpander" / L"snippets.txt"), message);
        }

        TEST_METHOD(CopiedSnippetFileIsExcludedFromSubfolder)
        {
            fs::path customSnippet = moduleFolder / L"shared" / L"snippets.txt";
            Assert::IsTrue(WriteText(customSnippet, "private configured snippet"), L"Could not create configured snippet fixture");
            CopySettingsToReport();
            const wchar_t* message = L"Configured snippet file was not excluded from the copied settings tree";
            Assert::IsTrue(TextExpanderPrivacy::ExcludeCopiedSnippetFile(settings, report, customSnippet), message);
            Assert::IsFalse(fs::exists(report / L"TextExpander" / L"shared" / L"snippets.txt"), message);
        }

        TEST_METHOD(SnippetFileOutsideSettingsDoesNotStopReport)
        {
            fs::path externalSnippet = root / L"external" / L"snippets.txt";
            Assert::IsTrue(WriteText(externalSnippet, "outside the copied settings tree"), L"Could not create external snippet fixture");
            CopySettingsToReport();
            Assert::IsTrue(TextExpanderPrivacy::ExcludeCopiedSnippetFile(settings, report, externalSnippet),
                           L"An external snippet path should not prevent report preparation");
        }

        // The configured location must resolve exactly as the engine resolves it. Both names are
        // excluded wherever the engine could look, whichever of them it would read.
        TEST_METHOD(ConfiguredLocationExcludesBothNames)
        {
            CreateConfiguredFolder();
            SetEnvironmentVariableW(RootVariable, settings.c_str());
            std::wstring folder = configuredFolder.wstring();
            const std::vector<SettingsCase> cases{
                { L"wrapped folder", "{\"snippets_path\":{\"value\":" + JsonString(folder) + "}}" },
                { L"padded and quoted folder", "{\"snippets_path\":{\"value\":" + JsonString(L"  \"" + folder + L"\"  ") + "}}" },
                { L"unbalanced leading quote", "{\"snippets_path\":{\"value\":" + JsonString(L"\"" + folder) + "}}" },
                { L"environment variable", "{\"snippets_path\":{\"value\":" + JsonString(L"%POWERTOYS_TE_PRIVACY_TEST_ROOT%\\TextExpander\\custom") + "}}" },
                { L"direct file path", "{\"snippets_path\":{\"value\":" + JsonString((configuredFolder / L"snippets.txt").wstring()) + "}}" },
                { L"direct file with its own name", "{\"snippets_path\":{\"value\":" + JsonString((configuredFolder / L"my snippets.txt").wstring()) + "}}" },
                { L"unwrapped string", "{\"snippets_path\":" + JsonString(folder) + "}" },
            };

            AssertExcluded(cases,
                           { L"TextExpander\\custom\\snippets.txt", L"TextExpander\\custom\\PowerToysTextExpanderSnippets.txt" },
                           L"Configured snippet file was not excluded");
        }

        TEST_METHOD(ConfiguredFileIsExcludedWhateverItsName)
        {
            CreateConfiguredFolder();
            const wchar_t* message = L"A configured snippet file with its own name was not excluded";
            Assert::IsTrue(MakeReport("{\"snippets_path\":{\"value\":" + JsonString((configuredFolder / L"my snippets.txt").wstring()) + "}}"), message);
            Assert::IsFalse(fs::exists(report / L"TextExpander" / L"custom" / L"my snippets.txt"), message);
        }

        // The engine falls back to the default folder for these, so both names there must be
        // excluded.
        TEST_METHOD(DefaultFolderIsExcludedWhenNoLocationApplies)
        {
            CreateCurrentDefaultSnippet();
            const std::vector<SettingsCase> cases{
                { L"no snippets_path", "{}" },
                { L"empty value", "{\"snippets_path\":{\"value\":\"\"}}" },
                { L"relative value", "{\"snippets_path\":{\"value\":\"custom\"}}" },
                { L"root-relative value", "{\"snippets_path\":{\"value\":\"\\\\custom\"}}" },
                { L"empty remembered sync folder", "{\"last_sync_folder\":{\"value\":\"\"}}" },
                { L"relative remembered sync folder", "{\"last_sync_folder\":{\"value\":\"custom\"}}" },
            };

            AssertExcluded(cases,
                           { L"TextExpander\\snippets.txt", L"TextExpander\\PowerToysTextExpanderSnippets.txt" },
                           L"Default snippet file was not excluded");
        }

        // The OneDrive folder Settings remembers for turning sync back on (last_sync_folder) holds
        // the same snippets, so both names there are excluded too, together with the folder in use.
        TEST_METHOD(RememberedSyncFolderIsExcluded)
        {
            CreateConfiguredFolder();
            CreateCurrentDefaultSnippet();
            SetEnvironmentVariableW(RootVariable, settings.c_str());
            std::wstring folder = configuredFolder.wstring();
            const std::vector<SettingsCase> cases{
                { L"remembered folder with the PowerToys folder in use", "{\"snippets_path\":{\"value\":\"\"},\"last_sync_folder\":{\"value\":" + JsonString(folder) + "}}" },
                { L"remembered folder only", "{\"last_sync_folder\":{\"value\":" + JsonString(folder) + "}}" },
                { L"remembered folder through a variable", "{\"last_sync_folder\":{\"value\":" + JsonString(L"%POWERTOYS_TE_PRIVACY_TEST_ROOT%\\TextExpander\\custom") + "}}" },
                { L"padded and quoted remembered folder", "{\"last_sync_folder\":{\"value\":" + JsonString(L"  \"" + folder + L"\"  ") + "}}" },
                { L"unwrapped remembered folder", "{\"last_sync_folder\":" + JsonString(folder) + "}" },
                { L"remembered file", "{\"last_sync_folder\":{\"value\":" + JsonString((configuredFolder / L"snippets.txt").wstring()) + "}}" },
            };

            AssertExcluded(cases,
                           { L"TextExpander\\custom\\snippets.txt",
                             L"TextExpander\\custom\\PowerToysTextExpanderSnippets.txt",
                             L"TextExpander\\snippets.txt",
                             L"TextExpander\\PowerToysTextExpanderSnippets.txt" },
                           L"Remembered sync folder snippet file was not excluded");
        }

        // Both keys at once: the folder in use and the remembered one are both left out.
        TEST_METHOD(FolderInUseAndRememberedSyncFolderAreBothExcluded)
        {
            CreateConfiguredFolder();
            fs::path otherFolder = moduleFolder / L"other";
            Assert::IsTrue(WriteText(otherFolder / L"snippets.txt", "private snippet in the folder in use") &&
                               WriteText(otherFolder / L"PowerToysTextExpanderSnippets.txt", "private snippet in the folder in use, current name"),
                           L"Could not create the folder-in-use snippet fixture");

            AssertExcluded({ { L"both keys",
                               "{\"snippets_path\":{\"value\":" + JsonString(otherFolder.wstring()) +
                                   "},\"last_sync_folder\":{\"value\":" + JsonString(configuredFolder.wstring()) + "}}" } },
                           { L"TextExpander\\other\\snippets.txt",
                             L"TextExpander\\other\\PowerToysTextExpanderSnippets.txt",
                             L"TextExpander\\custom\\snippets.txt",
                             L"TextExpander\\custom\\PowerToysTextExpanderSnippets.txt" },
                           L"The folder in use and the remembered sync folder were not both excluded");
        }

        // When the configured location cannot be determined, report preparation must stop.
        TEST_METHOD(AmbiguousSettingsFailClosed)
        {
            const std::vector<SettingsCase> cases{
                { L"missing wrapped value", "{\"snippets_path\":{}}" },
                { L"non-string value", "{\"snippets_path\":{\"value\":42}}" },
                { L"null value", "{\"snippets_path\":{\"value\":null}}" },
                { L"non-object properties", "\"invalid\"" },
                { L"missing wrapped remembered sync folder", "{\"last_sync_folder\":{}}" },
                { L"non-string remembered sync folder", "{\"last_sync_folder\":{\"value\":42}}" },
                { L"null remembered sync folder", "{\"snippets_path\":{\"value\":\"\"},\"last_sync_folder\":{\"value\":null}}" },
            };

            for (const SettingsCase& testCase : cases)
            {
                std::wstring message = std::wstring(L"Ambiguous settings did not fail closed: ") + testCase.name;
                Assert::IsTrue(WriteSettings(settings, testCase.properties) && ResetReport(settings, report), message.c_str());
                Assert::IsFalse(TextExpanderPrivacy::ExcludeConfiguredSnippetFile(settings, report), message.c_str());
            }
        }

        TEST_METHOD(MalformedSettingsFailClosed)
        {
            Assert::IsTrue(WriteText(moduleFolder / L"settings.json", "{\"properties\":{\"snippets_path\":{\"value\":\"x\"},}}") &&
                               ResetReport(settings, report),
                           L"Could not create the malformed settings fixture");
            Assert::IsFalse(TextExpanderPrivacy::ExcludeConfiguredSnippetFile(settings, report), L"Malformed settings did not fail closed");
        }

        // Copies Text Expander makes of a snippet file are left out too: Stop syncing's backups,
        // keep-both copies and temporary files from saving. The module's other files stay.
        TEST_METHOD(SnippetCopiesAreExcludedAndOtherFilesKept)
        {
            const wchar_t* familyFiles[] = {
                L"PowerToysTextExpanderSnippets.backup.txt",
                L"snippets.backup.txt",
                L"snippets.backup (2).txt",
                L"snippets (LAPTOP).txt",
                L".PowerToysTextExpanderSnippets.txt.0123456789abcdef0123456789abcdef.tmp",
                L"PowerToysTextExpanderSnippets.txt.tmp-1a2b3c4d",
            };
            for (const wchar_t* name : familyFiles)
            {
                Assert::IsTrue(WriteText(moduleFolder / name, "private copy of the snippets"),
                               (std::wstring(L"Could not create the snippet copy fixture ") + name).c_str());
            }

            Assert::IsTrue(WriteText(moduleFolder / L"diagnostics.txt", "snippet_count=3") &&
                               WriteText(moduleFolder / L"snippetsmith.txt", "not a snippet file") && MakeReport("{}"),
                           L"Snippet copies could not be excluded");

            for (const wchar_t* name : familyFiles)
            {
                Assert::IsFalse(fs::exists(report / L"TextExpander" / name), (std::wstring(L"A copy of the snippets was not excluded: ") + name).c_str());
            }

            Assert::IsTrue(fs::exists(report / L"TextExpander" / L"settings.json") &&
                               fs::exists(report / L"TextExpander" / L"diagnostics.txt") &&
                               fs::exists(report / L"TextExpander" / L"snippetsmith.txt"),
                           L"Files that aren't snippets were excluded");
        }

        // An earlier snippet folder keeps its files, and the backups made there, after the location
        // changes. With the default folder in use again, they are left out wherever they are in the
        // TextExpander folder; other files stay.
        TEST_METHOD(SnippetsInEarlierFoldersAreExcluded)
        {
            CreateCurrentDefaultSnippet();
            const fs::path earlierFiles[] = {
                moduleFolder / L"old-folder" / L"PowerToysTextExpanderSnippets.txt",
                moduleFolder / L"old-folder" / L"PowerToysTextExpanderSnippets.txt.backup-20261028-135850",
                moduleFolder / L"older" / L"nested" / L"snippets.txt",
            };
            for (const fs::path& file : earlierFiles)
            {
                Assert::IsTrue(WriteText(file, "private snippets in an earlier folder"), L"Could not create the earlier-folder snippet fixture");
            }

            fs::path earlierNotes = moduleFolder / L"old-folder" / L"notes.txt";
            fs::path moduleLog = moduleFolder / L"Logs" / L"1.0.0" / L"log.txt";
            Assert::IsTrue(WriteText(earlierNotes, "not a snippet file") && WriteText(moduleLog, "snippet_count=3") &&
                               MakeReport("{\"snippets_path\":{\"value\":\"\"}}"),
                           L"Snippets in an earlier folder could not be excluded");

            for (const fs::path& file : earlierFiles)
            {
                fs::path relative = file.lexically_relative(settings);
                Assert::IsFalse(fs::exists(report / relative), (L"Snippets in an earlier folder were not excluded: " + relative.wstring()).c_str());
            }

            Assert::IsTrue(!fs::exists(report / L"TextExpander" / L"PowerToysTextExpanderSnippets.txt") &&
                               fs::exists(report / L"TextExpander" / L"settings.json") &&
                               fs::exists(report / earlierNotes.lexically_relative(settings)) &&
                               fs::exists(report / moduleLog.lexically_relative(settings)),
                           L"The snippets in use were kept, or files that aren't snippets were excluded, alongside an earlier folder");
        }

        // Without a TextExpander folder there is nothing to leave out, and the report is still made.
        TEST_METHOD(ReportIsMadeWithoutTextExpanderFolder)
        {
            fs::path otherSettings = root / L"settings-without-text-expander";
            Assert::IsTrue(WriteText(otherSettings / L"settings.json", "{}") && ResetReport(otherSettings, report) &&
                               TextExpanderPrivacy::ExcludeConfiguredSnippetFile(otherSettings, report) &&
                               fs::exists(report / L"settings.json"),
                           L"A report without a TextExpander folder was not made");
        }

        // A configured file with its own name: its temporary file goes, in its own folder only, so
        // a name like settings.txt never takes the module's settings.json with it.
        TEST_METHOD(ConfiguredFileCopiesAreExcludedOnlyInItsFolder)
        {
            fs::path namedFolder = moduleFolder / L"named";
            Assert::IsTrue(WriteText(namedFolder / L"settings.txt", "private configured file") &&
                               WriteText(namedFolder / L"settings.txt.tmp-1a2b3c4d", "private temporary copy") &&
                               MakeReport("{\"snippets_path\":{\"value\":" + JsonString((namedFolder / L"settings.txt").wstring()) + "}}") &&
                               !fs::exists(report / L"TextExpander" / L"named" / L"settings.txt") &&
                               !fs::exists(report / L"TextExpander" / L"named" / L"settings.txt.tmp-1a2b3c4d") &&
                               fs::exists(report / L"TextExpander" / L"settings.json"),
                           L"A configured file's copies were not excluded, or the settings went with them");
        }

        // A junction inside the settings that leads to the snippet folder is not followed, so the
        // linked snippets never reach the report, while ordinary folders are still copied.
        TEST_METHOD(DirectoryJunctionIsNotFollowed)
        {
            fs::path linkedFolder = CreateLinkedFolder();
            fs::path junction = settings / L"SnippetLink";
            std::wstring makeJunction = L"cmd /c mklink /J \"" + junction.wstring() + L"\" \"" + linkedFolder.wstring() + L"\" >nul 2>nul";
            bool created = _wsystem(makeJunction.c_str()) == 0 && fs::exists(junction / L"PowerToysTextExpanderSnippets.txt");

            bool linkLeftOut = created &&
                               MakeReport("{\"snippets_path\":{\"value\":" + JsonString(linkedFolder.wstring()) + "}}") &&
                               !fs::exists(report / L"SnippetLink") && fs::exists(report / L"TextExpander" / L"settings.json");

            RemoveDirectoryW(junction.c_str());
            Assert::IsTrue(created, L"Could not create the junction fixture");
            Assert::IsTrue(linkLeftOut, L"A directory link brought snippets into the report");
        }

        // A directory symbolic link too, where this account may create one.
        TEST_METHOD(DirectorySymbolicLinkIsNotFollowed)
        {
            fs::path linkedFolder = CreateLinkedFolder();
            fs::path symlink = settings / L"SnippetSymlink";
            std::error_code symlinkError;
            fs::create_directory_symlink(linkedFolder, symlink, symlinkError);
            if (symlinkError)
            {
                Logger::WriteMessage(L"Directory symbolic link case skipped: no privilege");
                return;
            }

            bool linkLeftOut = ResetReport(settings, report) && !fs::exists(report / L"SnippetSymlink");

            RemoveDirectoryW(symlink.c_str());
            Assert::IsTrue(linkLeftOut, L"A directory link brought snippets into the report");
        }
    };
}
