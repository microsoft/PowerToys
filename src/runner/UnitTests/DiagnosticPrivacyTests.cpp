// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#include "pch.h"
#include "CppUnitTest.h"

#include "../../../tools/BugReportTool/BugReportTool/DiagnosticPrivacy.h"
#include "../../../tools/BugReportTool/BugReportTool/ZipTools/ZipFolder.h"

using namespace Microsoft::VisualStudio::CppUnitTestFramework;

namespace RunnerUnitTests
{
    TEST_CLASS (DiagnosticPrivacyTests)
    {
    public:
        TEST_METHOD_INITIALIZE (CreateFixture)
        {
            winrt::init_apartment(winrt::apartment_type::single_threaded);
            wchar_t directory[MAX_PATH]{};
            const auto length = GetTempPathW(ARRAYSIZE(directory), directory);
            Assert::IsTrue(length > 0 && length < ARRAYSIZE(directory));
            wchar_t path[MAX_PATH]{};
            Assert::IsTrue(GetTempFileNameW(directory, L"ptp", 0, path) != 0);
            std::filesystem::remove(path);
            m_directory = path;
            std::filesystem::create_directory(m_directory);
        }

        TEST_METHOD_CLEANUP (RemoveFixture)
        {
            std::filesystem::remove_all(m_directory);
            winrt::uninit_apartment();
        }

        TEST_METHOD (PackagedReportOmitsSensitiveLogsAndRedactsSettings)
        {
            const auto original = m_directory / L"original";
            const auto report = m_directory / L"report";
            const auto output = m_directory / L"output";
            const auto extracted = m_directory / L"extracted";
            const std::string settings = R"({"properties":{"SecurityKey":{"value":"synthetic-current-key"},"UseService":{"value":false}}})";
            const std::string backup = R"({"properties":{"SecurityKey":{"value":"synthetic-backup-key"},"UseService":{"value":true}}})";
            const std::string logs = "[info] diagnostics before\r\n"
                                     R"([info] dispatch_received_json: {"powertoys":{"MouseWithoutBorders":{"properties":{"SecurityKey":{"value":"synthetic-old-log-key"}}}}})"
                                     "\r\n"
                                     R"([info] dispatch_received_json: {"SecurityKey":{"value":"synthetic-current-key"}})"
                                     "\r\n[info] diagnostics after\r\n";
            const std::string legacy = "[debug] GETSECKEY: Key was already loaded/set: synthetic-legacy-key\r\n";
            const std::string otherLogs = "[info] unrelated module diagnostics\r\n";
            WriteFile(original / L"MouseWithoutBorders" / L"settings.json", settings);
            WriteFile(original / L"MouseWithoutBorders.backup" / L"settings.json", backup);
            WriteFile(original / L"RunnerLogs" / L"runner-log.log", logs);
            WriteFile(original / L"MouseWithoutBorders" / L"Logs" / L"legacy.log", legacy);
            WriteFile(original / L"FancyZones" / L"Logs" / L"diagnostics.log", otherLogs);
            std::filesystem::copy(original, report, std::filesystem::copy_options::recursive);

            diagnostic_privacy::sanitize_report(report);
            std::filesystem::create_directory(output);
            ZipFolder(output, report);
            std::filesystem::create_directory(extracted);
            const auto archive = std::filesystem::directory_iterator{ output }->path();
            Assert::AreEqual(std::wstring{ L".zip" }, archive.extension().wstring());
            ExtractArchive(archive, extracted);

            const auto packagedSettings = ReadFile(extracted / L"MouseWithoutBorders" / L"settings.json");
            const auto packagedBackup = ReadFile(extracted / L"MouseWithoutBorders.backup" / L"settings.json");
            Assert::IsFalse(std::filesystem::exists(extracted / L"RunnerLogs" / L"runner-log.log"));
            Assert::IsFalse(std::filesystem::exists(extracted / L"MouseWithoutBorders" / L"Logs" / L"legacy.log"));
            Assert::IsTrue(packagedSettings.find("synthetic-current-key") == std::string::npos);
            Assert::IsTrue(packagedBackup.find("synthetic-backup-key") == std::string::npos);
            Assert::AreEqual(otherLogs, ReadFile(extracted / L"FancyZones" / L"Logs" / L"diagnostics.log"));
            const auto parsed = winrt::Windows::Data::Json::JsonObject::Parse(winrt::to_hstring(packagedSettings));
            Assert::IsFalse(parsed.GetNamedObject(L"properties").GetNamedObject(L"UseService").GetNamedBoolean(L"value"));
            Assert::AreEqual(settings, ReadFile(original / L"MouseWithoutBorders" / L"settings.json"));
            Assert::AreEqual(backup, ReadFile(original / L"MouseWithoutBorders.backup" / L"settings.json"));
            Assert::AreEqual(logs, ReadFile(original / L"RunnerLogs" / L"runner-log.log"));
            Assert::AreEqual(legacy, ReadFile(original / L"MouseWithoutBorders" / L"Logs" / L"legacy.log"));
            Assert::AreEqual(otherLogs, ReadFile(original / L"FancyZones" / L"Logs" / L"diagnostics.log"));
        }

        TEST_METHOD (RedactsEscapedValuesServiceBlobsAndAlreadyPrivateValues)
        {
            std::string contents = "\xef\xbb\xbf"
                                   R"({"SecurityKey":{"value":"synthetic-\"-{escaped}\\key"},"other":{"SecurityKey":{"value":"mwb-dpapi:v1:synthetic-service-blob"}}})";
            Assert::IsTrue(diagnostic_privacy::redact_security_keys(contents));
            Assert::IsTrue(contents.starts_with("\xef\xbb\xbf"));
            Assert::IsTrue(contents.find("synthetic-") == std::string::npos);
            const auto once = contents;
            Assert::IsTrue(diagnostic_privacy::redact_security_keys(contents));
            Assert::AreEqual(once, contents);
        }

        TEST_METHOD (RedactsEscapedPropertyNamesInNestedBackupSettings)
        {
            const auto file = m_directory / L"MouseWithoutBorders.backup" / L"settings.json";
            WriteFile(file, R"({"nested":[{"\u0053ecurityKey":{"value":"synthetic-escaped-name-key"}}],"diagnostic":true})");

            diagnostic_privacy::sanitize_report(m_directory);

            const auto contents = ReadFile(file);
            Assert::IsTrue(contents.find("synthetic-") == std::string::npos);
            Assert::IsTrue(winrt::Windows::Data::Json::JsonObject::Parse(winrt::to_hstring(contents)).GetNamedBoolean(L"diagnostic"));
        }

        TEST_METHOD (RejectsIncompleteJsonInsteadOfRecoveringFragments)
        {
            const std::vector<std::string> inputs{
                "[error] malformed json:\n"
                R"({"\u0053ecurityKey": {"value": "synthetic-escaped-log-key"}, "other":)",
                R"({"SecurityKey":{"value":"synthetic-secret"},"other":)",
                R"({"SecurityKey":{"value":"synthetic-incomplete-key)",
                std::string{ "S\0e\0c\0u\0r\0i\0t\0y\0K\0e\0y\0", 22 }
            };
            for (size_t index = 0; index < inputs.size(); ++index)
            {
                auto contents = inputs[index];
                Assert::IsFalse(diagnostic_privacy::redact_security_keys(contents));
                WriteFile(m_directory / (std::to_wstring(index) + L".json"), inputs[index]);
            }

            diagnostic_privacy::sanitize_report(m_directory);

            Assert::IsTrue(std::filesystem::is_empty(m_directory));
        }

        TEST_METHOD (RiskLogSourcesAreOmittedAndOtherDiagnosticsRemain)
        {
            const std::vector<std::filesystem::path> logs{
                L"RunnerLogs\\runner-log.log",
                L"RunnerLogs\\rotated.data",
                L"MouseWithoutBorders\\Logs\\0.103.3052.0\\Log_2026-11-06.log",
                L"MouseWithoutBorders.backup\\Logs\\0.102.0.0\\legacy.txt",
                L"Backups\\rUnNeRlOgS\\archive.json",
                L"Backups\\mOuSeWiThOuTbOrDeRs.backup\\lOgS\\archive.json"
            };
            for (const auto& log : logs)
            {
                WriteFile(m_directory / log, "synthetic-secret-without-a-field-marker");
            }
            const std::string otherLogs = R"([info] unrelated diagnostics with "quotes" and \u0075)";
            const std::string otherJson = R"({"message":"\u0075"})";
            WriteFile(m_directory / L"FancyZones" / L"Logs" / L"diagnostics.log", otherLogs);
            WriteFile(m_directory / L"diagnostics.json", otherJson);

            diagnostic_privacy::sanitize_report(m_directory);

            for (const auto& log : logs)
            {
                Assert::IsFalse(std::filesystem::exists(m_directory / log));
            }
            Assert::AreEqual(otherLogs, ReadFile(m_directory / L"FancyZones" / L"Logs" / L"diagnostics.log"));
            Assert::AreEqual(otherJson, ReadFile(m_directory / L"diagnostics.json"));
        }

        TEST_METHOD (EscapedAndMalformedPayloadsAreOmittedWithRiskLogs)
        {
            const std::vector<std::string> logs{
                R"([info] payload="{ "\u0053ecurityKey":{"value":"synthetic-secret"}}")",
                "[info] context started \"\r\n"
                R"([info] payload={"\u0053ecurityKey":{"value":"synthetic-secret"}})"
                "\r\n[info] context ended \"\r\n",
                R"({"payload":"{\"\\u0053ecurityKey\":{\"value\":\"synthetic-secret\"}}"})",
                R"({"payload":"{\"\u005cu0053ecurityKey\":{\"value\":\"synthetic-secret\"}}"})",
                R"({"payload":"{\"\\\u00750053ecurityKey\":{\"value\":\"synthetic-secret\"}}"})",
                R"({"payload":"{\"\\u\u0030053ecurityKey\":{\"value\":\"synthetic-secret\"}}"})",
                R"([info] payload="{ "\u0053\u0065\u0063\u0075\u0072\u0069\u0074\u0079\u004B\u0065\u0079":{"value":"synthetic-secret"}}")"
            };
            for (size_t index = 0; index < logs.size(); ++index)
            {
                WriteFile(m_directory / L"RunnerLogs" / (std::to_wstring(index) + L".log"), logs[index]);
            }

            diagnostic_privacy::sanitize_report(m_directory);

            for (size_t index = 0; index < logs.size(); ++index)
            {
                Assert::IsFalse(std::filesystem::exists(m_directory / L"RunnerLogs" / (std::to_wstring(index) + L".log")));
            }
        }

        TEST_METHOD (LockedReportFileFailsClosed)
        {
            const auto file = m_directory / L"settings.json";
            WriteFile(file, R"({"SecurityKey":{"value":"synthetic-locked-key"}})");
            wil::unique_handle lock{ CreateFileW(file.c_str(), GENERIC_READ, 0, nullptr, OPEN_EXISTING, FILE_ATTRIBUTE_NORMAL, nullptr) };
            Assert::IsTrue(lock.is_valid());

            Assert::ExpectException<std::ios_base::failure>([&]() { diagnostic_privacy::sanitize_report(m_directory); });
        }

        TEST_METHOD (LockedRiskLogFailsClosed)
        {
            const auto file = m_directory / L"RunnerLogs" / L"runner-log.log";
            WriteFile(file, "synthetic-secret-without-a-field-marker");
            wil::unique_handle lock{ CreateFileW(file.c_str(), GENERIC_READ, 0, nullptr, OPEN_EXISTING, FILE_ATTRIBUTE_NORMAL, nullptr) };
            Assert::IsTrue(lock.is_valid());

            Assert::ExpectException<std::filesystem::filesystem_error>([&]() { diagnostic_privacy::sanitize_report(m_directory); });
        }

    private:
        std::filesystem::path m_directory;

        static void WriteFile(const std::filesystem::path& path, const std::string& contents)
        {
            std::filesystem::create_directories(path.parent_path());
            std::ofstream file{ path, std::ios::binary | std::ios::trunc };
            file.exceptions(std::ios::failbit | std::ios::badbit);
            file << contents;
            file.close();
        }

        static std::string ReadFile(const std::filesystem::path& path)
        {
            std::ifstream file{ path, std::ios::binary };
            file.exceptions(std::ios::failbit | std::ios::badbit);
            return { std::istreambuf_iterator<char>{ file }, std::istreambuf_iterator<char>{} };
        }

        static void ExtractArchive(const std::filesystem::path& archive, const std::filesystem::path& destination)
        {
            wchar_t systemDirectory[MAX_PATH]{};
            Assert::IsTrue(GetSystemDirectoryW(systemDirectory, ARRAYSIZE(systemDirectory)) != 0);
            const auto tar = std::filesystem::path{ systemDirectory } / L"tar.exe";
            auto command = L"\"" + tar.wstring() + L"\" -xf \"" + archive.wstring() + L"\" -C \"" + destination.wstring() + L"\"";
            STARTUPINFOW startup{ .cb = sizeof(STARTUPINFOW) };
            wil::unique_process_information process;
            Assert::IsTrue(CreateProcessW(tar.c_str(), command.data(), nullptr, nullptr, FALSE, CREATE_NO_WINDOW, nullptr, nullptr, &startup, &process));
            Assert::AreEqual(static_cast<DWORD>(WAIT_OBJECT_0), WaitForSingleObject(process.hProcess, 30000));
            DWORD exitCode{};
            Assert::IsTrue(GetExitCodeProcess(process.hProcess, &exitCode));
            Assert::AreEqual(0UL, exitCode);
        }
    };
}
