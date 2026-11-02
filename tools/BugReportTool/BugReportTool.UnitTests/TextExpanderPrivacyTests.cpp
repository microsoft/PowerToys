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

        // A snippets database as the editor saves it.
        const char Database[] = "{\n  \"version\": 1,\n  \"snippets\": [\n    {\n      \"trigger\": \"!team\",\n"
                                "      \"replacement\": \"private team snippet\"\n    }\n  ]\n}";
    }

    TEST_CLASS(TextExpanderPrivacyTests)
    {
        fs::path root;
        fs::path settings;
        fs::path report;
        fs::path moduleFolder;
        fs::path configuredFolder;
        fs::path namedFolder;

        // A folder configured as the snippet location, holding the snippets file and a file with a name
        // of its own. It's outside the TextExpander folder, where a report keeps nothing else anyway.
        void CreateConfiguredFolder()
        {
            Assert::IsTrue(WriteText(configuredFolder / L"PowerToysTextExpanderSnippets.json", "private configured-folder snippet") &&
                               WriteText(configuredFolder / L"my snippets.json", "private configured file with its own name"),
                           L"Could not create configured-folder snippet fixture");
        }

        fs::path CreateLinkedFolder()
        {
            fs::path linkedFolder = root / L"linked";
            Assert::IsTrue(WriteText(linkedFolder / L"PowerToysTextExpanderSnippets.json", "private snippets behind a link"),
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
        // files, and keeps every one of kept, given relative to the report.
        void AssertExcluded(const std::vector<SettingsCase>& cases, const std::vector<fs::path>& files, const wchar_t* failure, const std::vector<fs::path>& kept = {})
        {
            for (const SettingsCase& testCase : cases)
            {
                std::wstring message = std::wstring(failure) + L": " + testCase.name;
                Assert::IsTrue(MakeReport(testCase.properties), message.c_str());
                for (const fs::path& file : files)
                {
                    Assert::IsFalse(fs::exists(report / file), (message + L": " + file.wstring() + L" was in the report").c_str());
                }

                for (const fs::path& file : kept)
                {
                    Assert::IsTrue(fs::exists(report / file), (message + L": " + file.wstring() + L" was left out").c_str());
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
            configuredFolder = settings / L"Shared";
            namedFolder = settings / L"Named";
            Assert::IsTrue(WriteText(moduleFolder / L"PowerToysTextExpanderSnippets.json", "{\"snippets\":[{\"trigger\":\"!private\"}]}"), L"Could not create default snippet fixture");
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
            Assert::IsTrue(TextExpanderPrivacy::ExcludeCopiedSnippetFile(settings, report, settings / L"textexpander" / L"POWERTOYSTEXTEXPANDERSNIPPETS.JSON"), message);
            Assert::IsFalse(fs::exists(report / L"TextExpander" / L"PowerToysTextExpanderSnippets.json"), message);
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

        // The configured location must resolve exactly as the engine resolves it. The snippets file is
        // excluded wherever the engine could look.
        TEST_METHOD(ConfiguredLocationIsExcluded)
        {
            CreateConfiguredFolder();
            SetEnvironmentVariableW(RootVariable, settings.c_str());
            std::wstring folder = configuredFolder.wstring();
            const std::vector<SettingsCase> cases{
                { L"wrapped folder", "{\"snippets_path\":{\"value\":" + JsonString(folder) + "}}" },
                { L"padded and quoted folder", "{\"snippets_path\":{\"value\":" + JsonString(L"  \"" + folder + L"\"  ") + "}}" },
                { L"unbalanced leading quote", "{\"snippets_path\":{\"value\":" + JsonString(L"\"" + folder) + "}}" },
                { L"environment variable", "{\"snippets_path\":{\"value\":" + JsonString(L"%POWERTOYS_TE_PRIVACY_TEST_ROOT%\\Shared") + "}}" },
                { L"direct file path", "{\"snippets_path\":{\"value\":" + JsonString((configuredFolder / L"PowerToysTextExpanderSnippets.json").wstring()) + "}}" },
                { L"direct file with its own name", "{\"snippets_path\":{\"value\":" + JsonString((configuredFolder / L"my snippets.json").wstring()) + "}}" },
                { L"unwrapped string", "{\"snippets_path\":" + JsonString(folder) + "}" },
            };

            AssertExcluded(cases,
                           { L"Shared\\PowerToysTextExpanderSnippets.json" },
                           L"Configured snippet file was not excluded");
        }

        TEST_METHOD(ConfiguredFileIsExcludedWhateverItsName)
        {
            CreateConfiguredFolder();
            const wchar_t* message = L"A configured snippet file with its own name was not excluded";
            Assert::IsTrue(MakeReport("{\"snippets_path\":{\"value\":" + JsonString((configuredFolder / L"my snippets.json").wstring()) + "}}"), message);
            Assert::IsFalse(fs::exists(report / L"Shared" / L"my snippets.json"), message);
        }

        // The engine falls back to the default folder for these, so the snippets file there must be
        // excluded.
        TEST_METHOD(DefaultFolderIsExcludedWhenNoLocationApplies)
        {
            const std::vector<SettingsCase> cases{
                { L"no snippets_path", "{}" },
                { L"empty value", "{\"snippets_path\":{\"value\":\"\"}}" },
                { L"relative value", "{\"snippets_path\":{\"value\":\"custom\"}}" },
                { L"root-relative value", "{\"snippets_path\":{\"value\":\"\\\\custom\"}}" },
            };

            AssertExcluded(cases,
                           { L"TextExpander\\PowerToysTextExpanderSnippets.json" },
                           L"Default snippet file was not excluded");
        }

        // When the configured location cannot be determined, report preparation must stop.
        TEST_METHOD(AmbiguousSettingsFailClosed)
        {
            const std::vector<SettingsCase> cases{
                { L"missing wrapped value", "{\"snippets_path\":{}}" },
                { L"non-string value", "{\"snippets_path\":{\"value\":42}}" },
                { L"null value", "{\"snippets_path\":{\"value\":null}}" },
                { L"non-object properties", "\"invalid\"" },
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

        // Of the TextExpander folder, a report keeps only the settings, the engine's diagnostics and
        // the logs. A snippets file of any name, such as one Move snippets database left behind, its
        // backups, keep-both copies and temporary files, and earlier snippet folders all stay out.
        TEST_METHOD(TextExpanderFolderKeepsOnlySettingsDiagnosticsAndLogs)
        {
            const wchar_t* excluded[] = {
                L"team.json",
                L"team.json.backup-20261029-145200",
                L"team.json.tmp-0123456789abcdef0123456789abcdef",
                L"team (LAPTOP).json",
                L"PowerToysTextExpanderSnippets.json.backup-20261029-145200",
                L".PowerToysTextExpanderSnippets.txt.0123456789abcdef0123456789abcdef.tmp",
                L"snippets.backup (2).txt",
                L"snippetsmith.json",
                L"old-folder\\PowerToysTextExpanderSnippets.txt",
                L"old-folder\\notes.txt",
                L"older\\nested\\snippets.txt",
                L"Editor\\work.json",
            };
            const wchar_t* kept[] = {
                L"diagnostics.txt",
                L"Logs\\1.0.0\\log.txt",
                L"Editor\\Logs\\0.0.1.0\\Log_2026-10-29.txt",
                L"ModuleInterface\\Logs\\v0.0.1\\log.log",
            };
            for (const wchar_t* name : excluded)
            {
                Assert::IsTrue(WriteText(moduleFolder / name, "private snippets"), (std::wstring(L"Could not create ") + name).c_str());
            }

            for (const wchar_t* name : kept)
            {
                Assert::IsTrue(WriteText(moduleFolder / name, "snippet_count=3"), (std::wstring(L"Could not create ") + name).c_str());
            }

            // The snippets in use are somewhere else now.
            Assert::IsTrue(MakeReport("{\"snippets_path\":{\"value\":" + JsonString((root / L"elsewhere" / L"work.json").wstring()) + "}}"),
                           L"The TextExpander folder could not be left out");

            for (const wchar_t* name : excluded)
            {
                Assert::IsFalse(fs::exists(report / L"TextExpander" / name), (std::wstring(L"A file that isn't kept was in the report: ") + name).c_str());
            }

            for (const wchar_t* name : kept)
            {
                Assert::IsTrue(fs::exists(report / L"TextExpander" / name), (std::wstring(L"A file that's kept was left out: ") + name).c_str());
            }

            Assert::IsTrue(fs::exists(report / L"TextExpander" / L"settings.json") &&
                               !fs::exists(report / L"TextExpander" / L"PowerToysTextExpanderSnippets.json") &&
                               !fs::exists(report / L"TextExpander" / L"old-folder"),
                           L"The settings were left out, or the default snippets or an earlier folder were kept");
        }

        // Move snippets database and Sync from another PC leave the database they used where it was, here
        // outside the TextExpander folder, with its backups. The settings point somewhere else now, so they're
        // found by their content, and other JSON stays.
        TEST_METHOD(EarlierDatabaseOutsideTextExpanderFolderIsExcluded)
        {
            Assert::IsTrue(WriteText(configuredFolder / L"team.json", Database) &&
                               WriteText(configuredFolder / L"team.json.backup-20261031", (std::string("// Before Sync from another PC\n") + Database).c_str()) &&
                               WriteText(configuredFolder / L"other.json", "{\"a\":1}") &&
                               WriteText(settings / L"FancyZones" / L"settings.json", "{\"name\":\"FancyZones\",\"properties\":{\"fancyzones_shiftDrag\":{\"value\":true}}}"),
                           L"Could not create the earlier database fixture");

            const std::vector<SettingsCase> cases{
                { L"new database elsewhere", "{\"snippets_path\":{\"value\":" + JsonString((root / L"elsewhere" / L"work.json").wstring()) + "}}" },
                { L"default database", "{}" },
            };

            AssertExcluded(cases,
                           { L"Shared\\team.json", L"Shared\\team.json.backup-20261031" },
                           L"An earlier snippets database was in the report",
                           { L"Shared\\other.json", L"FancyZones\\settings.json", L"TextExpander\\settings.json" });
        }

        // A sync service's keep-both copy of an earlier database, deeper in the settings, in a form the engine also
        // reads: a byte order mark, keys in any case and trailing commas.
        TEST_METHOD(EarlierDatabaseCopyInSubfolderIsExcluded)
        {
            Assert::IsTrue(WriteText(configuredFolder / L"Sub" / L"team (LAPTOP).json",
                                     "\xEF\xBB\xBF{\"Snippets\":[{\"TRIGGER\":\"!team\",\"replacement\":\"private team snippet\",},],}"),
                           L"Could not create the keep-both copy fixture");

            const std::vector<SettingsCase> cases{ { L"default database", "{}" } };
            AssertExcluded(cases, { L"Shared\\Sub\\team (LAPTOP).json" }, L"A keep-both copy of an earlier database was in the report");
        }

        // Sync from another PC uses a snippets file whatever its name, so an earlier database named work.txt, its
        // backup, a sync service's keep-both copy and a file with no extension are found by their content too. Files
        // with only one of the keys stay.
        TEST_METHOD(EarlierDatabaseOfAnyNameIsExcluded)
        {
            Assert::IsTrue(WriteText(configuredFolder / L"work.txt", Database) &&
                               WriteText(configuredFolder / L"work.txt.backup-20261031", Database) &&
                               WriteText(configuredFolder / L"work (LAPTOP).txt", Database) &&
                               WriteText(configuredFolder / L"snippets", Database) &&
                               WriteText(configuredFolder / L"notes.txt", "The \"snippets\" list moved to another database.") &&
                               WriteText(settings / L"RunnerLogs" / L"runner-log_2026-10-31.log", "[09:15:00] Text Expander: \"trigger\" typed, snippets loaded\n") &&
                               WriteText(settings / L"FancyZones" / L"settings.json", "{\"name\":\"FancyZones\",\"properties\":{\"fancyzones_shiftDrag\":{\"value\":true}}}") &&
                               WriteText(moduleFolder / L"diagnostics.txt", "snippets_location=custom\nsnippets_file_exists=true\nsnippet_count=1\nwatching=true\n"),
                           L"Could not create the earlier database fixture");

            const std::vector<SettingsCase> cases{
                { L"new database elsewhere", "{\"snippets_path\":{\"value\":" + JsonString((root / L"elsewhere" / L"current.json").wstring()) + "}}" },
                { L"default database", "{}" },
            };

            AssertExcluded(cases,
                           { L"Shared\\work.txt", L"Shared\\work.txt.backup-20261031", L"Shared\\work (LAPTOP).txt", L"Shared\\snippets" },
                           L"An earlier snippets database with a name of its own was in the report",
                           { L"Shared\\notes.txt", L"RunnerLogs\\runner-log_2026-10-31.log", L"FancyZones\\settings.json", L"TextExpander\\settings.json", L"TextExpander\\diagnostics.txt" });
        }

        // A key split between two of the 64 KB reads is still found, wherever it's split.
        TEST_METHOD(KeySplitBetweenReadsIsFound)
        {
            std::vector<fs::path> files;
            for (size_t padding = 64 * 1024 - 24; padding < 64 * 1024; ++padding)
            {
                fs::path file = fs::path(L"Shared") / (L"team-" + std::to_wstring(padding) + L".json");
                std::string contents = std::string(padding, ' ') + "{\"snippets\":[{\"trigger\":\"!team\"}]}";
                Assert::IsTrue(WriteText(settings / file, contents.c_str()), L"Could not create the split key fixture");
                files.push_back(file);
            }

            const std::vector<SettingsCase> cases{ { L"default database", "{}" } };
            AssertExcluded(cases, files, L"A database with a key split between two reads was in the report");
        }

        // Every file is read, a piece at a time, so a log of a few MB with only one of the keys is read to the end,
        // kept whole, and quickly.
        TEST_METHOD(LargeLogIsKeptQuickly)
        {
            const std::string line = "[09:15:00] Text Expander loaded the \"snippets\" list, and a trigger was typed\n";
            std::string contents;
            while (contents.size() < 8 * 1024 * 1024)
            {
                contents += line;
            }

            const fs::path log = fs::path(L"RunnerLogs") / L"runner-log_2026-10-31.log";
            Assert::IsTrue(WriteText(settings / log, contents.c_str()) && WriteSettings(settings, "{}") && ResetReport(settings, report),
                           L"Could not create the large log fixture");

            const auto start = std::chrono::steady_clock::now();
            const bool checked = TextExpanderPrivacy::ExcludeConfiguredSnippetFile(settings, report);
            const auto elapsed = std::chrono::duration_cast<std::chrono::milliseconds>(std::chrono::steady_clock::now() - start);
            Logger::WriteMessage((L"Checked the settings with an 8 MB log in " + std::to_wstring(elapsed.count()) + L" ms").c_str());

            std::error_code error;
            Assert::IsTrue(checked && fs::file_size(report / log, error) == contents.size() && !error, L"A large log without snippets was not kept whole");
            Assert::IsTrue(elapsed < std::chrono::seconds(5), L"Checking a large log for snippets took too long");
        }

        // The module's settings have "snippets_path", which isn't "snippets", so they stay.
        TEST_METHOD(TextExpanderSettingsAreKept)
        {
            const std::vector<SettingsCase> cases{
                { L"configured database", "{\"snippets_path\":{\"value\":" + JsonString((namedFolder / L"snippets.json").wstring()) + "},\"case_sensitive\":{\"value\":false}}" },
            };

            AssertExcluded(cases, {}, L"The Text Expander settings were left out", { L"TextExpander\\settings.json" });
        }

        // A file that can't be read can't be checked, so it's left out, whatever its name.
        TEST_METHOD(UnreadableFileIsExcluded)
        {
            Assert::IsTrue(WriteText(configuredFolder / L"locked.txt", "{\"a\":1}") && WriteSettings(settings, "{}") && ResetReport(settings, report),
                           L"Could not create the unreadable file fixture");

            // Shared for deleting only, so it can't be read but can still be left out.
            fs::path locked = report / L"Shared" / L"locked.txt";
            HANDLE file = CreateFileW(locked.c_str(), GENERIC_READ, FILE_SHARE_DELETE, nullptr, OPEN_EXISTING, FILE_ATTRIBUTE_NORMAL, nullptr);
            bool excluded = file != INVALID_HANDLE_VALUE && TextExpanderPrivacy::ExcludeConfiguredSnippetFile(settings, report);
            if (file != INVALID_HANDLE_VALUE)
            {
                CloseHandle(file);
            }

            Assert::IsTrue(excluded && !fs::exists(locked), L"A file that couldn't be read was in the report");
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

        // A configured file with its own name: its backups and temporary files go, in its own folder only, so
        // a name like settings.txt never takes the module's settings.json with it.
        TEST_METHOD(ConfiguredFileCopiesAreExcludedOnlyInItsFolder)
        {
            Assert::IsTrue(WriteText(namedFolder / L"settings.txt", "private configured file") &&
                               WriteText(namedFolder / L"settings.txt.tmp-1a2b3c4d", "private temporary copy") &&
                               WriteText(namedFolder / L"settings.txt.backup-20261029-124512", "private backup") &&
                               MakeReport("{\"snippets_path\":{\"value\":" + JsonString((namedFolder / L"settings.txt").wstring()) + "}}") &&
                               !fs::exists(report / L"Named" / L"settings.txt") &&
                               !fs::exists(report / L"Named" / L"settings.txt.tmp-1a2b3c4d") &&
                               !fs::exists(report / L"Named" / L"settings.txt.backup-20261029-124512") &&
                               fs::exists(report / L"TextExpander" / L"settings.json"),
                           L"A configured file's copies were not excluded, or the settings went with them");
        }

        // A configured file of any name that was moved, renamed or deleted is still a file, not a folder: the
        // backups and temporary files left beside it go, and the folder's other files stay.
        TEST_METHOD(MissingConfiguredFileCopiesAreExcluded)
        {
            for (const wchar_t* name : { L"work.json", L"work.txt", L"work" })
            {
                std::wstring file = name;
                std::wstring message = L"A missing configured file's copies were not excluded, or other files went with them: " + file;
                Assert::IsTrue(WriteText(namedFolder / (file + L".backup-20261029-124512"), "private backup") &&
                                   WriteText(namedFolder / (file + L".tmp-1a2b3c4d"), "private temporary copy") &&
                                   WriteText(namedFolder / L"notes.txt", "not a snippet file") &&
                                   MakeReport("{\"snippets_path\":{\"value\":" + JsonString((namedFolder / file).wstring()) + "}}") &&
                                   !fs::exists(report / L"Named" / (file + L".backup-20261029-124512")) &&
                                   !fs::exists(report / L"Named" / (file + L".tmp-1a2b3c4d")) &&
                                   fs::exists(report / L"Named" / L"notes.txt"),
                               message.c_str());
            }
        }

        // A configured .json file of any name, with its backups and temporary files, and the default-named
        // snippets file beside it.
        TEST_METHOD(ConfiguredJsonFileIsExcludedWithItsCopies)
        {
            const wchar_t* privateFiles[] = {
                L"work snippets.json",
                L"work snippets.json.backup-20261029-145200",
                L"work snippets.json.tmp-1a2b3c4d",
                L"PowerToysTextExpanderSnippets.json",
            };
            for (const wchar_t* name : privateFiles)
            {
                Assert::IsTrue(WriteText(namedFolder / name, "private snippets"), L"Could not create the configured .json fixture");
            }

            Assert::IsTrue(WriteText(namedFolder / L"other.json", "not a snippet file") &&
                               MakeReport("{\"snippets_path\":{\"value\":" + JsonString((namedFolder / L"work snippets.json").wstring()) + "}}"),
                           L"A configured .json file could not be excluded");
            for (const wchar_t* name : privateFiles)
            {
                Assert::IsFalse(fs::exists(report / L"Named" / name), (std::wstring(L"A snippets file was not excluded: ") + name).c_str());
            }

            Assert::IsTrue(fs::exists(report / L"Named" / L"other.json") && fs::exists(report / L"TextExpander" / L"settings.json"),
                           L"Files that aren't snippets were excluded with a configured .json file");
        }

        // A configured file whose name starts with a dot goes with its backups, temporary files and
        // keep-both copies, and a file named the same but for the dot stays.
        TEST_METHOD(DotNamedConfiguredFileIsExcludedWithItsCopies)
        {
            const wchar_t* privateFiles[] = {
                L".private.json",
                L".private.json.backup-20261029-145200",
                L".private.json.tmp-0123456789abcdef0123456789abcdef",
                L".private (LAPTOP).json",
                L"..private.json.0123456789abcdef0123456789abcdef.tmp",
            };
            for (const wchar_t* name : privateFiles)
            {
                Assert::IsTrue(WriteText(namedFolder / name, "private snippets"), L"Could not create the dot-named fixture");
            }

            Assert::IsTrue(WriteText(namedFolder / L"private.json", "not the snippets file") &&
                               MakeReport("{\"snippets_path\":{\"value\":" + JsonString((namedFolder / L".private.json").wstring()) + "}}"),
                           L"A dot-named configured file could not be excluded");
            for (const wchar_t* name : privateFiles)
            {
                Assert::IsFalse(fs::exists(report / L"Named" / name), (std::wstring(L"A snippets file was not excluded: ") + name).c_str());
            }

            Assert::IsTrue(fs::exists(report / L"Named" / L"private.json"), L"A file named like the snippets file but for the dot was excluded");
        }

        // A junction inside the settings that leads to the snippet folder is not followed, so the
        // linked snippets never reach the report, while ordinary folders are still copied.
        TEST_METHOD(DirectoryJunctionIsNotFollowed)
        {
            fs::path linkedFolder = CreateLinkedFolder();
            fs::path junction = settings / L"SnippetLink";
            std::wstring makeJunction = L"cmd /c mklink /J \"" + junction.wstring() + L"\" \"" + linkedFolder.wstring() + L"\" >nul 2>nul";
            bool created = _wsystem(makeJunction.c_str()) == 0 && fs::exists(junction / L"PowerToysTextExpanderSnippets.json");

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
