// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#include <Windows.h>
#include <CppUnitTest.h>
#include <fstream>
#include <random>
#include <string>

#include "..\BugReportTool\ReportSanitizer.h"

using namespace Microsoft::VisualStudio::CppUnitTestFramework;

namespace BugReportToolUnitTests
{
    TEST_CLASS (ReportSanitizerTests)
    {
    public:
        TEST_METHOD (SanitizesCurrentAndLegacySettings)
        {
            AssertTextEquals(
                std::string(R"({"properties":{"SecurityKey":"<private_data>","UseService":{"value":true}}})"),
                SanitizeText(R"({"properties":{"SecurityKey":{"value":"current-key"},"UseService":{"value":true}}})"));
            AssertTextEquals(
                std::string(R"({"SecurityKey":"<private_data>"})"),
                SanitizeText(R"({"SecurityKey":"legacy-key"})"));
        }

        TEST_METHOD (SanitizesLogsAndPreservesOtherMessages)
        {
            const std::string prefix = "[2026-11-02 20:30:43.709546] [p-7480] [t-39400] [info] dispatch_received_json: ";
            const std::string suffix = "\r\n[info] another diagnostic\r\n";
            AssertTextEquals(
                prefix + R"({"powertoys":{"MouseWithoutBorders":{"properties":{"SecurityKey":"<private_data>"}}}})" + suffix,
                SanitizeText(prefix + R"({"powertoys":{"MouseWithoutBorders":{"properties":{"SecurityKey":{"value":"historical-key"}}}}})" + suffix));
        }

        TEST_METHOD (SanitizesEmptyAndEscapedValuesWithWhitespace)
        {
            AssertTextEquals(
                std::string("{\"SecurityKey\" : \"<private_data>\",\"other\":{\"SecurityKey\":\"<private_data>\"}}"),
                SanitizeText("{\"SecurityKey\" : {\r\n\"value\" : \"escaped\\\"key\\\\value\"},\"other\":{\"SecurityKey\":\"\"}}"));
        }

        TEST_METHOD (SanitizesValuesInsideNestedObjectsAndArrays)
        {
            AssertTextEquals(
                R"({"entries":[{"properties":{"SecurityKey":"<private_data>"}},{"SecurityKey":"<private_data>"}],"diagnostic":{"SecurityKeyState":"changed"}})",
                SanitizeText(R"({"entries":[{"properties":{"SecurityKey":{"value":"quote\"backslash\\line\nunicode\u263a"}}},{"SecurityKey":"array-fixture"}],"diagnostic":{"SecurityKeyState":"changed"}})"));
        }

        TEST_METHOD (PreservesAlreadySanitizedValuesAndUnrelatedText)
        {
            const std::string text = R"({"SecurityKey":"<private_data>","Name":"test","Enabled":true})";
            AssertTextEquals(text, SanitizeText(text));
            const std::string diagnostic = "[info] \"SecurityKey\" was changed\r\n";
            AssertTextEquals(diagnostic, SanitizeText(diagnostic));
        }

        TEST_METHOD (RemovesRemainingTextWhenValueIsUnexpectedOrIncomplete)
        {
            for (const auto& text : {
                     R"({"SecurityKey":{"unexpected":"unrecognized-key"}})",
                     R"({"SecurityKey":{"value":"truncated-key)",
                     R"({"SecurityKey":{"value":{"nested":"nested-fixture"}}})",
                     R"({"SecurityKey":["array-fixture"]})",
                     R"({"SecurityKey":null})" })
            {
                AssertTextEquals(R"({"SecurityKey":"<private_data>")", SanitizeText(text));
            }
        }

        TEST_METHOD (RemovesRemainingTextWhenValueObjectHasExtraFields)
        {
            AssertTextEquals(
                R"({"SecurityKey":"<private_data>")",
                SanitizeText(R"({"SecurityKey":{"value":"<private_data>","other":"sibling-fixture"}})"));
            AssertTextEquals(
                R"([info] dispatch_received_json: {"SecurityKey":"<private_data>")",
                SanitizeText(R"([info] dispatch_received_json: {"SecurityKey":{"value":"first-fixture","other":{"nested":"sibling-fixture"}}})"));
        }

        TEST_METHOD (DiscardsTextFromFirstPropertyWhenAnyValueIsUnexpected)
        {
            AssertTextEquals(
                R"({"diagnostic":"retained","entries":[{"SecurityKey":"<private_data>")",
                SanitizeText(
                    R"({"diagnostic":"retained","entries":[{"SecurityKey":"first"},{"SecurityKey":{"value":"second"}},{"SecurityKey":"third"},{"SecurityKey":["fourth"]},{"SecurityKey":{"unexpected":"fifth"}}]})"));
        }

        TEST_METHOD (SanitizesReportBackupsAndLogsWithoutChangingOriginalFiles)
        {
            TemporaryDirectory temporary;
            const auto source = temporary.path / L"source" / L"settings.json";
            const auto report = temporary.path / L"report";
            const auto backup = report / L"MouseWithoutBorders.backup" / L"settings.json";
            const auto log = report / L"RunnerLogs" / L"old" / L"runner-log.log";
            const std::string original = R"({"properties":{"SecurityKey":{"value":"fixture-key"}}})";
            Write(source, original);
            Write(backup, original);
            Write(log, "[info] dispatch_received_json: " + original);
            Write(report / L"unrelated.txt", "diagnostic\r\n");

            Assert::IsTrue(ReportSanitizer::Sanitize(report).empty());

            AssertTextEquals(original, Read(source));
            AssertTextEquals(std::string(R"({"properties":{"SecurityKey":"<private_data>"}})"), Read(backup));
            Assert::IsTrue(Read(log).find("fixture-key") == std::string::npos);
            AssertTextEquals(std::string("diagnostic\r\n"), Read(report / L"unrelated.txt"));
        }

        TEST_METHOD (SanitizesReportFilesWithUppercaseOrMixedCaseExtensions)
        {
            TemporaryDirectory temporary;
            for (const auto& extension : { L".JSON", L".Log", L".tXt" })
            {
                Write(temporary.path / (std::wstring(L"settings") + extension), R"({"SecurityKey":"fixture-key"})");
            }

            Assert::IsTrue(ReportSanitizer::Sanitize(temporary.path).empty());

            for (const auto& extension : { L".JSON", L".Log", L".tXt" })
            {
                AssertTextEquals(
                    R"({"SecurityKey":"<private_data>"})",
                    Read(temporary.path / (std::wstring(L"settings") + extension)));
            }
        }

        TEST_METHOD (DeletesUnreadableOrUnwritableReportFilesAndContinuesProcessing)
        {
            TemporaryDirectory temporary;
            const auto file = temporary.path / L"settings.json";
            for (const DWORD sharing : { DWORD{ FILE_SHARE_DELETE }, DWORD{ FILE_SHARE_READ | FILE_SHARE_DELETE } })
            {
                Write(file, R"({"SecurityKey":"fixture-key"})");
                Write(temporary.path / L"before.json", R"({"SecurityKey":"before-fixture"})");
                Write(temporary.path / L"after.log", R"({"SecurityKey":"after-fixture"})");
                const auto handle = CreateFileW(file.c_str(), GENERIC_READ, sharing, nullptr, OPEN_EXISTING, FILE_ATTRIBUTE_NORMAL, nullptr);
                Assert::IsTrue(handle != INVALID_HANDLE_VALUE);
                try
                {
                    const auto removedFiles = ReportSanitizer::Sanitize(temporary.path);
                    Assert::AreEqual(size_t{ 1 }, removedFiles.size());
                    Assert::IsTrue(removedFiles.front() == file);
                    AssertTextEquals(R"({"SecurityKey":"<private_data>"})", Read(temporary.path / L"before.json"));
                    AssertTextEquals(R"({"SecurityKey":"<private_data>"})", Read(temporary.path / L"after.log"));
                }
                catch (...)
                {
                    CloseHandle(handle);
                    throw;
                }
                CloseHandle(handle);
                Assert::IsFalse(std::filesystem::exists(file));
            }
        }

        TEST_METHOD (StopsReportProcessingWhenAnUnprocessedFileCannotBeDeleted)
        {
            TemporaryDirectory temporary;
            const auto file = temporary.path / L"settings.json";
            Write(file, R"({"SecurityKey":"fixture-key"})");
            const auto handle = CreateFileW(file.c_str(), GENERIC_READ, FILE_SHARE_READ, nullptr, OPEN_EXISTING, FILE_ATTRIBUTE_NORMAL, nullptr);
            Assert::IsTrue(handle != INVALID_HANDLE_VALUE);
            try
            {
                Assert::ExpectException<std::filesystem::filesystem_error>([&] {
                    ReportSanitizer::Sanitize(temporary.path);
                });
            }
            catch (...)
            {
                CloseHandle(handle);
                throw;
            }
            CloseHandle(handle);
            AssertTextEquals(R"({"SecurityKey":"fixture-key"})", Read(file));
        }

        TEST_METHOD (SanitizesPropertyNamesSplitAcrossReadChunks)
        {
            TemporaryDirectory temporary;
            const auto file = temporary.path / L"runner.log";
            const std::string prefix(64 * 1024 - 5, 'x');
            Write(file, prefix + R"({"SecurityKey":"fixture-key","other":"retained"})");

            Assert::IsTrue(ReportSanitizer::Sanitize(temporary.path).empty());

            AssertTextEquals(prefix + R"({"SecurityKey":"<private_data>","other":"retained"})", Read(file));
        }

        TEST_METHOD (PreservesContentAtRegexSizeLimitAndTruncatesLargerFiles)
        {
            TemporaryDirectory temporary;
            const auto file = temporary.path / L"runner.log";
            const std::string prefix = "[info] diagnostic prefix\r\n{";
            const std::string originalValue = R"("SecurityKey":"fixture-key","other":"retained"})";
            const std::string replacement = R"("SecurityKey":"<private_data>")";
            for (const auto size : { ReportSanitizer::MaxRegexFileSize, ReportSanitizer::MaxRegexFileSize + 1 })
            {
                const std::string suffix(size - prefix.size() - originalValue.size(), 'x');
                const auto original = prefix + originalValue + suffix;
                const auto expected = size > ReportSanitizer::MaxRegexFileSize ? prefix + replacement : prefix + replacement + R"(,"other":"retained"})" + suffix;
                Write(file, original);

                Assert::IsTrue(ReportSanitizer::Sanitize(temporary.path).empty());

                AssertTextEquals(expected, Read(file));
            }
            const std::string unrelated(ReportSanitizer::MaxRegexFileSize + 1, 'x');
            Write(file, unrelated);
            Assert::IsTrue(ReportSanitizer::Sanitize(temporary.path).empty());
            AssertTextEquals(unrelated, Read(file));
        }

        TEST_METHOD (PreservesLargeFilesWithOnlyPropertyNameMentions)
        {
            TemporaryDirectory temporary;
            const auto file = temporary.path / L"runner.log";
            const auto original = "[info] \"SecurityKey\" was changed\r\n" +
                                  std::string(ReportSanitizer::MaxRegexFileSize, 'x') +
                                  "\r\n\"SecurityKey\"\t\r\n";
            Write(file, original);

            Assert::IsTrue(ReportSanitizer::Sanitize(temporary.path).empty());

            AssertTextEquals(original, Read(file));
        }

        TEST_METHOD (TruncatesLargeFilesFromFirstPropertyAfterOrdinaryNameMentions)
        {
            TemporaryDirectory temporary;
            const auto file = temporary.path / L"runner.log";
            const auto prefix = "[info] \"SecurityKey\" was changed\r\n" +
                                std::string(ReportSanitizer::MaxRegexFileSize, 'x') + "\r\n{";
            const auto original = prefix + "\"SecurityKey\" \t\r\n : \"fixture-key\",\"other\":\"discarded\"}";
            const auto expected = prefix + R"("SecurityKey":"<private_data>")";
            Write(file, original);

            Assert::IsTrue(ReportSanitizer::Sanitize(temporary.path).empty());

            AssertTextEquals(expected, Read(file));
        }

        TEST_METHOD (SanitizesPropertiesWithWhitespaceSpanningReadChunks)
        {
            TemporaryDirectory temporary;
            const auto file = temporary.path / L"runner.log";
            const std::string prefix(ReportSanitizer::MaxRegexFileSize - std::string("\"SecurityKey\"").size(), 'x');
            const auto original = prefix + "\"SecurityKey\"" +
                                  std::string(64 * 1024 + 5, ' ') + "\r\n\t:\"fixture-key\"";
            const auto expected = prefix + R"("SecurityKey":"<private_data>")";
            Write(file, original);

            Assert::IsTrue(ReportSanitizer::Sanitize(temporary.path).empty());

            AssertTextEquals(expected, Read(file));
        }

        TEST_METHOD (RemovesUnreadableReportFolderAndContinuesWithOtherFiles)
        {
            TemporaryDirectory temporary;
            const auto folder = temporary.path / L"unreadable";
            std::filesystem::create_directory(folder);
            Write(temporary.path / L"other.json", R"({"SecurityKey":"fixture-key"})");
            const auto handle = CreateFileW(folder.c_str(), FILE_LIST_DIRECTORY, FILE_SHARE_WRITE | FILE_SHARE_DELETE, nullptr, OPEN_EXISTING, FILE_FLAG_BACKUP_SEMANTICS, nullptr);
            Assert::IsTrue(handle != INVALID_HANDLE_VALUE);
            try
            {
                Assert::ExpectException<std::filesystem::filesystem_error>([&] {
                    std::filesystem::directory_iterator inaccessible(folder);
                });
                const auto removedPaths = ReportSanitizer::Sanitize(temporary.path);
                Assert::AreEqual(size_t{ 1 }, removedPaths.size());
                Assert::IsTrue(removedPaths.front() == folder);
                AssertTextEquals(R"({"SecurityKey":"<private_data>"})", Read(temporary.path / L"other.json"));
            }
            catch (...)
            {
                CloseHandle(handle);
                throw;
            }
            CloseHandle(handle);
            Assert::IsFalse(std::filesystem::exists(folder));
        }

        TEST_METHOD (TemporaryTestDirectoryPreservesExistingDirectoriesAndFiles)
        {
            TemporaryDirectory existing;
            const auto retained = existing.path / L"retained.txt";
            Write(retained, "retained fixture");
            std::filesystem::path created;
            {
                TemporaryDirectory temporary(existing.path);
                created = temporary.path;
            }

            Assert::IsTrue(std::filesystem::exists(retained), L"An existing fixture directory was removed.");
            Assert::IsTrue(created != existing.path, L"An existing fixture directory was reused.");
            AssertTextEquals("retained fixture", Read(retained));
        }

    private:
        static std::string SanitizeText(const std::string& text)
        {
            TemporaryDirectory temporary;
            const auto file = temporary.path / L"report.log";
            Write(file, text);
            Assert::IsTrue(ReportSanitizer::Sanitize(temporary.path).empty());
            return Read(file);
        }

        static void AssertTextEquals(const std::string& expected, const std::string& actual)
        {
            Assert::IsTrue(expected == actual, L"File content comparison failed.");
        }

        struct TemporaryDirectory
        {
            static std::filesystem::path GenerateCandidatePath()
            {
                return std::filesystem::temp_directory_path() / ("BugReportSanitizerTests-" + std::to_string(std::random_device{}()));
            }

            std::filesystem::path path;

            explicit TemporaryDirectory(const std::filesystem::path& candidatePath = GenerateCandidatePath()) :
                path(candidatePath)
            {
                while (!std::filesystem::create_directory(path))
                {
                    path = GenerateCandidatePath();
                }
            }

            ~TemporaryDirectory()
            {
                std::error_code error;
                std::filesystem::remove_all(path, error);
            }
        };

        static void Write(const std::filesystem::path& file, const std::string& text)
        {
            std::filesystem::create_directories(file.parent_path());
            std::ofstream output(file, std::ios::binary);
            output.exceptions(std::ios::failbit | std::ios::badbit);
            output << text;
        }

        static std::string Read(const std::filesystem::path& file)
        {
            std::ifstream input(file, std::ios::binary);
            input.exceptions(std::ios::failbit | std::ios::badbit);
            return { std::istreambuf_iterator<char>{ input }, std::istreambuf_iterator<char>{} };
        }
    };
}
