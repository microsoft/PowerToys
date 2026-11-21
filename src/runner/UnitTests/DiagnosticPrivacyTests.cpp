// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#include "pch.h"
#include "CppUnitTest.h"

#include <iterator>
#include <sddl.h>
#include <sstream>
#include <spdlog/sinks/null_sink.h>
#include <spdlog/sinks/ostream_sink.h>

#include "../settings_window_logging.h"
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

        TEST_METHOD (RunnerSettingsLogRedactsNestedKeysWithoutChangingConfiguration)
        {
            const std::wstring message = LR"({"powertoys":{"MouseWithoutBorders":{"properties":{"SecurityKey":{"value":"synthetic-current-key"},"UseService":{"value":false},"MachineMatrixString":["PC-A","PC-B"]}}},"items":[{"\u0053ecurityKey":"synthetic-escaped-key","enabled":true},{"SecurityKey":{"value":"mwb-dpapi:v1:synthetic-service-blob"}},{"SecurityKey":null}],"SecurityKey":["synthetic-root-key"]})";
            const auto configuration = json::JsonObject::Parse(message);
            const auto original = configuration.Stringify();

            const auto logged = CaptureSettingsLog(message);

            Assert::IsTrue(logged.starts_with("dispatch_received_json: {"));
            Assert::IsTrue(logged.find("synthetic-") == std::string::npos);
            Assert::IsTrue(logged.find("mwb-dpapi:") == std::string::npos);
            const auto sanitized = json::JsonObject::Parse(winrt::to_hstring(logged.substr(std::string_view{ "dispatch_received_json: " }.size())));
            const auto properties = sanitized.GetNamedObject(L"powertoys").GetNamedObject(L"MouseWithoutBorders").GetNamedObject(L"properties");
            Assert::AreEqual(std::wstring{ L"<private_data>" }, std::wstring{ properties.GetNamedString(L"SecurityKey") });
            Assert::IsFalse(properties.GetNamedObject(L"UseService").GetNamedBoolean(L"value"));
            Assert::AreEqual(std::wstring{ L"PC-B" }, std::wstring{ properties.GetNamedArray(L"MachineMatrixString").GetStringAt(1) });
            const auto items = sanitized.GetNamedArray(L"items");
            Assert::IsTrue(items.GetObjectAt(0).GetNamedBoolean(L"enabled"));
            for (const auto& item : items)
            {
                Assert::AreEqual(std::wstring{ L"<private_data>" }, std::wstring{ item.GetObjectW().GetNamedString(L"SecurityKey") });
            }
            Assert::AreEqual(std::wstring{ L"<private_data>" }, std::wstring{ sanitized.GetNamedString(L"SecurityKey") });
            Assert::AreEqual(std::wstring{ original }, std::wstring{ configuration.Stringify() });
            Assert::AreEqual(std::wstring{ L"synthetic-current-key" }, std::wstring{ configuration.GetNamedObject(L"powertoys").GetNamedObject(L"MouseWithoutBorders").GetNamedObject(L"properties").GetNamedObject(L"SecurityKey").GetNamedString(L"value") });
        }

        TEST_METHOD (RunnerSettingsLogPreservesNonSensitivePayload)
        {
            const std::wstring message = LR"({"general":{"theme":"dark","enabled":{"MouseWithoutBorders":true},"sample":[42,false,null],"description":"line one\nline two \u4e2d\u6587"}})";

            const auto logged = CaptureSettingsLog(message);

            Assert::IsTrue(logged.starts_with("dispatch_received_json: {"));
            const auto sanitized = json::JsonObject::Parse(winrt::to_hstring(logged.substr(std::string_view{ "dispatch_received_json: " }.size())));
            Assert::AreEqual(std::wstring{ json::JsonObject::Parse(message).Stringify() }, std::wstring{ sanitized.Stringify() });
            Assert::AreEqual(static_cast<size_t>(1), static_cast<size_t>(std::count(logged.begin(), logged.end(), '\n')));
        }

        TEST_METHOD (RunnerSettingsLogFailureNeverFallsBackToRawPayload)
        {
            const std::vector<std::wstring> messages{
                LR"({"SecurityKey":{"value":"synthetic-partial-key")",
                LR"(["synthetic-non-object-key"])",
                LR"("synthetic-string-key")",
            };
            for (const auto& message : messages)
            {
                const auto logged = CaptureSettingsLog(message);
                Assert::IsTrue(logged == "dispatch_received_json: received settings message\n" ||
                               logged == "dispatch_received_json: received settings message\r\n");
            }
        }

        TEST_METHOD (PackagedReportRedactsSettingsAndPreservesSafeDiagnostics)
        {
            const auto original = m_directory / L"original";
            const auto report = m_directory / L"report";
            const auto output = m_directory / L"output";
            const auto extracted = m_directory / L"extracted";
            const std::string settings = R"({"properties":{"SecurityKey":{"value":"synthetic-current-key"},"UseService":{"value":false}}})";
            const std::string backup = R"({"properties":{"SecurityKey":{"value":"synthetic-backup-key"},"UseService":{"value":true}}})";
            const std::string overlap = R"({"properties":{"SecurityKey":{"value":"synthetic-overlap-key"}},"diagnostic":true})"
                                        "\r\n";
            const std::filesystem::path overlapPath = L"MouseWithoutBorders\\Logs\\MouseWithoutBorders.backup\\settings.json";
            const auto before = RunnerRecord("diagnostics before");
            const auto after = RunnerRecord("diagnostics after");
            auto partialRunnerLog = RunnerRecord("dispatch_received_json: {\r\n"
                                                 R"(  "SecurityKey":{"value":"synthetic-partial-key"},)"
                                                 "\r\n  \"other\": tru");
            partialRunnerLog.resize(partialRunnerLog.size() - 2);
            const std::string logs = before +
                                     RunnerRecord("dispatch_received_json: {\r\n"
                                                  R"(  "powertoys":{"MouseWithoutBorders":{"properties":{"SecurityKey":{"value":"synthetic-old-log-key"}}}})"
                                                  "\r\n}") +
                                     RunnerRecord(R"(dispatch_received_json: {"SecurityKey":{"value":"synthetic-current-key"}})") +
                                     after + partialRunnerLog;
            const auto mwbLogs = ManagedRecord("StartVerifiedIpcServer: All pipe instances are busy") +
                                 ManagedRecord("SettingsSync shutdown timeout; SaveSettings contention") +
                                 ClipboardRejectionRecord() + ManagedExceptionRecord();
            auto partialMwbLogs = mwbLogs + ManagedRecord("GETSECKEY: Key was already loaded/set: synthetic-partial-key\r\n    unfinished message");
            partialMwbLogs.resize(partialMwbLogs.size() - 2);
            const auto mixedLogs = before + R"([info] payload="{ "\u0053ecurityKey":{"value":"synthetic-mixed-key"}}")" + "\r\n" + after;
            const auto legacy = ManagedRecord("GETSECKEY: Key was already loaded/set: synthetic-legacy-key");
            const std::string otherLogs = "[info] unrelated module diagnostics\r\n";
            WriteFile(original / L"MouseWithoutBorders" / L"settings.json", settings);
            WriteFile(original / L"MouseWithoutBorders.backup" / L"settings.json", backup);
            WriteFile(original / overlapPath, overlap);
            WriteFile(original / L"RunnerLogs" / L"runner-log.log", logs);
            WriteFile(original / L"RunnerLogs" / L"mixed.log", mixedLogs);
            WriteFile(original / L"MouseWithoutBorders" / L"Logs" / L"0.103.3052.0" / L"Log_2026-11-09.log", partialMwbLogs);
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
            const auto packagedOverlap = ReadFile(extracted / overlapPath);
            Assert::AreEqual(before + after, ReadFile(extracted / L"RunnerLogs" / L"runner-log.log"));
            Assert::IsFalse(std::filesystem::exists(extracted / L"RunnerLogs" / L"mixed.log"));
            Assert::AreEqual(mwbLogs, ReadFile(extracted / L"MouseWithoutBorders" / L"Logs" / L"0.103.3052.0" / L"Log_2026-11-09.log"));
            Assert::IsFalse(std::filesystem::exists(extracted / L"MouseWithoutBorders" / L"Logs" / L"legacy.log"));
            Assert::IsTrue(packagedSettings.find("synthetic-current-key") == std::string::npos);
            Assert::IsTrue(packagedBackup.find("synthetic-backup-key") == std::string::npos);
            Assert::IsTrue(packagedOverlap.find("synthetic-overlap-key") == std::string::npos);
            Assert::IsTrue(winrt::Windows::Data::Json::JsonObject::Parse(winrt::to_hstring(packagedOverlap)).GetNamedBoolean(L"diagnostic"));
            Assert::AreEqual(otherLogs, ReadFile(extracted / L"FancyZones" / L"Logs" / L"diagnostics.log"));
            const auto parsed = winrt::Windows::Data::Json::JsonObject::Parse(winrt::to_hstring(packagedSettings));
            Assert::IsFalse(parsed.GetNamedObject(L"properties").GetNamedObject(L"UseService").GetNamedBoolean(L"value"));
            Assert::AreEqual(settings, ReadFile(original / L"MouseWithoutBorders" / L"settings.json"));
            Assert::AreEqual(backup, ReadFile(original / L"MouseWithoutBorders.backup" / L"settings.json"));
            Assert::AreEqual(overlap, ReadFile(original / overlapPath));
            Assert::AreEqual(logs, ReadFile(original / L"RunnerLogs" / L"runner-log.log"));
            Assert::AreEqual(mixedLogs, ReadFile(original / L"RunnerLogs" / L"mixed.log"));
            Assert::AreEqual(partialMwbLogs, ReadFile(original / L"MouseWithoutBorders" / L"Logs" / L"0.103.3052.0" / L"Log_2026-11-09.log"));
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

        TEST_METHOD (ConcurrentReportsKeepIndependentSanitizedSnapshots)
        {
            const auto original = m_directory / L"original";
            const auto temporary = m_directory / L"temporary";
            const std::filesystem::path backupPath = L"MouseWithoutBorders.backup\\settings.json";
            const std::string backup = R"({"SecurityKey":{"value":"synthetic-concurrent-key"},"diagnostic":true})";
            WriteFile(original / backupPath, backup);
            std::filesystem::create_directory(temporary);

            auto first = diagnostic_privacy::create_report_directory(temporary, original);
            std::filesystem::copy(original, first.directory, std::filesystem::copy_options::recursive);
            diagnostic_privacy::sanitize_report(first.directory);
            const auto sanitized = ReadFile(first.directory / backupPath);

            auto second = diagnostic_privacy::create_report_directory(temporary, original);
            std::filesystem::copy(original, second.directory, std::filesystem::copy_options::recursive);
            // A legacy collector may also clear and repopulate its old shared directory.
            const auto legacy = temporary / L"PowerToys";
            std::filesystem::remove_all(legacy);
            std::filesystem::copy(original, legacy, std::filesystem::copy_options::recursive);

            Assert::IsTrue(first.directory != second.directory);
            Assert::AreEqual(sanitized, ReadFile(first.directory / backupPath));
            Assert::AreEqual(backup, ReadFile(second.directory / backupPath));
            Assert::AreEqual(backup, ReadFile(legacy / backupPath));
            const std::vector<std::filesystem::path> reports{ first.directory, second.directory };
            for (size_t index = 0; index < reports.size(); ++index)
            {
                if (index != 0)
                {
                    diagnostic_privacy::sanitize_report(reports[index]);
                }
                const auto output = m_directory / (L"output-" + std::to_wstring(index));
                const auto extracted = m_directory / (L"extracted-" + std::to_wstring(index));
                std::filesystem::create_directory(output);
                std::filesystem::create_directory(extracted);
                auto structureFile = reports[index];
                structureFile += L"installationFolderStructure.txt";
                WriteFile(structureFile, "synthetic installation structure");
                ZipFolder(output, reports[index]);
                ExtractArchive(std::filesystem::directory_iterator{ output }->path(), extracted);
                const auto contents = ReadFile(extracted / backupPath);
                Assert::IsTrue(contents.find("synthetic-concurrent-key") == std::string::npos);
                Assert::IsTrue(winrt::Windows::Data::Json::JsonObject::Parse(winrt::to_hstring(contents)).GetNamedBoolean(L"diagnostic"));
                Assert::AreEqual(std::string{ "synthetic installation structure" }, ReadFile(extracted / L"installationFolderStructure.txt"));
                Assert::IsFalse(std::filesystem::exists(extracted / (index == 0 ? first.ownerFile : second.ownerFile).filename()));
            }
            Assert::AreEqual(backup, ReadFile(original / backupPath));
            Assert::IsTrue(diagnostic_privacy::remove_report_directory(first));
            Assert::IsTrue(std::filesystem::exists(second.directory / backupPath));
            Assert::IsFalse(std::filesystem::exists(first.ownerFile));
            Assert::IsTrue(diagnostic_privacy::remove_report_directory(second));
        }

        TEST_METHOD (AbandonedReportIsReclaimedWithoutDeletingUnownedData)
        {
            const auto temporary = m_directory / L"temporary";
            std::filesystem::create_directory(temporary);
            auto abandoned = diagnostic_privacy::create_report_directory(temporary, m_directory);
            const auto file = abandoned.directory / L"MouseWithoutBorders.backup" / L"settings.json";
            WriteFile(file, R"({"SecurityKey":"synthetic-abandoned-key"})");
            const auto unowned = temporary / L"PowerToys-BugReport-unowned" / L"diagnostics.txt";
            WriteFile(unowned, "unrelated diagnostics");
            auto unknownMarker = abandoned.ownerFile;
            unknownMarker.replace_filename(std::wstring{ L"PowerToys-BugReport-" } + winrt::to_hstring(winrt::guid{}).c_str() + L".owner");
            auto unknownDirectory = unknownMarker;
            unknownDirectory.replace_extension();
            WriteFile(unknownDirectory / L"diagnostics.txt", "unrelated diagnostics");
            WriteFile(unknownMarker, "not a report ownership marker");
            // Process termination closes this handle without running the collector's cleanup.
            abandoned.ownership.reset();

            auto next = diagnostic_privacy::create_report_directory(temporary, m_directory);

            Assert::IsFalse(std::filesystem::exists(file));
            Assert::IsFalse(std::filesystem::exists(abandoned.ownerFile));
            Assert::AreEqual(std::string{ "unrelated diagnostics" }, ReadFile(unowned));
            Assert::AreEqual(std::string{ "unrelated diagnostics" }, ReadFile(unknownDirectory / L"diagnostics.txt"));
            Assert::IsTrue(std::filesystem::exists(unknownMarker));
            Assert::IsTrue(diagnostic_privacy::remove_report_directory(next));
        }

        TEST_METHOD (FailedReportCleanupLeavesOwnershipForRetry)
        {
            const auto temporary = m_directory / L"temporary";
            std::filesystem::create_directory(temporary);
            auto failed = diagnostic_privacy::create_report_directory(temporary, m_directory);
            const auto file = failed.directory / L"MouseWithoutBorders.backup" / L"settings.json";
            WriteFile(file, R"({"SecurityKey":"synthetic-cleanup-key"})");
            wil::unique_handle lock{ CreateFileW(file.c_str(), GENERIC_READ, FILE_SHARE_READ, nullptr, OPEN_EXISTING, FILE_ATTRIBUTE_NORMAL, nullptr) };
            Assert::IsTrue(lock.is_valid());

            Assert::IsFalse(diagnostic_privacy::remove_report_directory(failed));
            Assert::IsTrue(std::filesystem::exists(failed.ownerFile));
            lock.reset();
            auto next = diagnostic_privacy::create_report_directory(temporary, m_directory);

            Assert::IsFalse(std::filesystem::exists(file));
            Assert::IsFalse(std::filesystem::exists(failed.ownerFile));
            Assert::IsTrue(diagnostic_privacy::remove_report_directory(next));
        }

        TEST_METHOD (RecoveryEnumerationFailureDoesNotPreventNewReport)
        {
            const auto temporary = m_directory / L"temporary";
            std::filesystem::create_directory(temporary);
            DWORD length{};
            GetFileSecurityW(temporary.c_str(), DACL_SECURITY_INFORMATION, nullptr, 0, &length);
            Assert::IsTrue(length != 0);
            std::vector<char> original(length);
            Assert::IsTrue(GetFileSecurityW(temporary.c_str(), DACL_SECURITY_INFORMATION, original.data(), length, &length));
            const auto restore = wil::scope_exit([&]() {
                SetFileSecurityW(temporary.c_str(), DACL_SECURITY_INFORMATION, original.data());
            });
            PSECURITY_DESCRIPTOR descriptor{};
            Assert::IsTrue(ConvertStringSecurityDescriptorToSecurityDescriptorW(L"D:(D;;0x1;;;WD)(A;;FA;;;OW)", SDDL_REVISION_1, &descriptor, nullptr));
            const auto release = wil::scope_exit([&]() { LocalFree(descriptor); });
            Assert::IsTrue(SetFileSecurityW(temporary.c_str(), DACL_SECURITY_INFORMATION, descriptor));
            // Listing is denied, but creating a new child directory is still permitted.
            Assert::IsTrue(std::filesystem::create_directory(temporary / L"control", m_directory));
            Assert::ExpectException<std::filesystem::filesystem_error>([&]() {
                const std::filesystem::directory_iterator entries{ temporary };
            });

            auto report = diagnostic_privacy::create_report_directory(temporary, m_directory);

            const auto file = report.directory / L"MouseWithoutBorders.backup" / L"settings.json";
            WriteFile(file, R"({"SecurityKey":"synthetic-recovery-key","diagnostic":true})");
            diagnostic_privacy::sanitize_report(report.directory);
            const auto contents = ReadFile(file);
            Assert::IsTrue(contents.find("synthetic-recovery-key") == std::string::npos);
            Assert::IsTrue(winrt::Windows::Data::Json::JsonObject::Parse(winrt::to_hstring(contents)).GetNamedBoolean(L"diagnostic"));
            Assert::IsTrue(diagnostic_privacy::remove_report_directory(report));
        }

        TEST_METHOD (RedactsEscapedPropertyNamesInNestedBackupSettings)
        {
            const auto file = m_directory / L"Backups" / L"mOuSeWiThOuTbOrDeRs.backup" / L"nested" / L"sEtTiNgS.JsOn";
            WriteFile(file, R"({"nested":[{"\u0053ecurityKey":{"value":"synthetic-escaped-name-key"}}],"diagnostic":true})");

            diagnostic_privacy::sanitize_report(m_directory);

            const auto contents = ReadFile(file);
            Assert::IsTrue(contents.find("synthetic-") == std::string::npos);
            Assert::IsTrue(winrt::Windows::Data::Json::JsonObject::Parse(winrt::to_hstring(contents)).GetNamedBoolean(L"diagnostic"));
        }

        TEST_METHOD (SettingsInsideManagedLogDirectoriesAreRedacted)
        {
            const std::string settings = R"({"properties":{"SecurityKey":{"value":"synthetic-overlap-key"}},"diagnostic":true})";
            const std::vector<std::pair<std::filesystem::path, std::string>> files{
                { L"MouseWithoutBorders\\Logs\\MouseWithoutBorders.backup\\settings.json", settings + "\r\n" },
                { L"MouseWithoutBorders\\Logs\\settings.json", settings },
                { L"Backups\\mOuSeWiThOuTbOrDeRs.backup\\lOgS\\nested\\sEtTiNgS.JsOn",
                  "\xef\xbb\xbf"
                  R"({"\u0053ecurityKey":{"value":"synthetic-overlap-key"},"diagnostic":true})"
                  "\n" }
            };
            for (const auto& [path, contents] : files)
            {
                WriteFile(m_directory / path, contents);
            }

            diagnostic_privacy::sanitize_report(m_directory);

            for (const auto& [path, original] : files)
            {
                const auto contents = ReadFile(m_directory / path);
                Assert::IsTrue(contents.find("synthetic-overlap-key") == std::string::npos);
                Assert::AreEqual(original.starts_with("\xef\xbb\xbf"), contents.starts_with("\xef\xbb\xbf"));
                const auto json = contents.substr(contents.starts_with("\xef\xbb\xbf") ? 3 : 0);
                Assert::IsTrue(winrt::Windows::Data::Json::JsonObject::Parse(winrt::to_hstring(json)).GetNamedBoolean(L"diagnostic"));
            }
        }

        TEST_METHOD (OverlappingSettingsRejectInvalidJsonAndKeepRunnerRestrictions)
        {
            const std::string settings = R"({"SecurityKey":{"value":"synthetic-overlap-key"}})";
            const std::vector<std::pair<std::filesystem::path, std::string>> files{
                { L"MouseWithoutBorders\\Logs\\MouseWithoutBorders.backup\\settings.json",
                  R"({"SecurityKey":{"value":"synthetic-overlap-key"},"diagnostic":)"
                  "\r\n" },
                { L"MouseWithoutBorders\\Logs\\RunnerLogs\\MouseWithoutBorders.backup\\settings.json", settings + "\r\n" },
                { L"RunnerLogs\\MouseWithoutBorders.backup\\settings.json", RunnerRecord(settings) }
            };
            for (const auto& [path, contents] : files)
            {
                WriteFile(m_directory / path, contents);
            }

            diagnostic_privacy::sanitize_report(m_directory);

            for (const auto& [path, contents] : files)
            {
                Assert::IsFalse(std::filesystem::exists(m_directory / path));
            }
        }

        TEST_METHOD (RejectsIncompleteJsonInsteadOfRecoveringFragments)
        {
            const std::vector<std::string> inputs{
                "[error] malformed json:\n"
                R"({"\u0053ecurityKey": {"value": "synthetic-escaped-log-key"}, "other":)",
                R"({"SecurityKey":{"value":"synthetic-secret"},"other":)",
                R"({"SecurityKey":{"value":"synthetic-incomplete-key)",
                R"("{\"SecurityKey\":{\"value\":\"synthetic-wrapped-key\"}}")",
                R"([{"SecurityKey":{"value":"synthetic-array-key"}}])",
                std::string{ "S\0e\0c\0u\0r\0i\0t\0y\0K\0e\0y\0", 22 }
            };
            for (size_t index = 0; index < inputs.size(); ++index)
            {
                auto contents = inputs[index];
                Assert::IsFalse(diagnostic_privacy::redact_security_keys(contents));
                WriteFile(m_directory / std::to_wstring(index) / L"MouseWithoutBorders.backup" / L"settings.json", inputs[index]);
            }

            diagnostic_privacy::sanitize_report(m_directory);

            for (size_t index = 0; index < inputs.size(); ++index)
            {
                Assert::IsFalse(std::filesystem::exists(m_directory / std::to_wstring(index) / L"MouseWithoutBorders.backup" / L"settings.json"));
            }
        }

        TEST_METHOD (SafeRiskSourceLogsAndUnrelatedFilesRemain)
        {
            const std::vector<std::pair<std::filesystem::path, std::string>> logs{
                { L"RunnerLogs\\runner-log.log", RunnerRecord("dispatch_received_json: received settings message") + RunnerRecord("dispatch_received_json: got malformed json") },
                { L"RunnerLogs\\rotated.data", RunnerRecord("ordinary UTF-8 diagnostics \xe4\xb8\xad") },
                { L"RunnerLogs\\action-runner-log.log", RunnerRecord("action completed") },
                { L"MouseWithoutBorders\\Logs\\0.103.3052.0\\Log_2026-11-09.log", ManagedRecord("StartVerifiedIpcServer: All pipe instances are busy") },
                { L"MouseWithoutBorders.backup\\Logs\\0.102.0.0\\legacy.txt", ManagedRecord("SettingsSync shutdown timeout") },
                { L"Backups\\rUnNeRlOgS\\archive.json", RunnerRecord("SaveSettings contention") },
                { L"Backups\\mOuSeWiThOuTbOrDeRs.backup\\lOgS\\archive.json", ManagedRecord("connection retry") }
            };
            for (const auto& [path, contents] : logs)
            {
                WriteFile(m_directory / path, contents);
            }
            const std::string otherLogs = R"([info] unrelated diagnostics with "quotes" and \u0075)";
            const std::string otherJson = R"({"message":"\u0075"})";
            const std::string malformedJson = R"({"diagnostic":)";
            WriteFile(m_directory / L"FancyZones" / L"Logs" / L"diagnostics.log", otherLogs);
            WriteFile(m_directory / L"diagnostics.json", otherJson);
            WriteFile(m_directory / L"FancyZones.backup" / L"settings.json", malformedJson);
            WriteFile(m_directory / L"MouseWithoutBorders" / L"diagnostics.json", malformedJson);

            diagnostic_privacy::sanitize_report(m_directory);

            for (const auto& [path, contents] : logs)
            {
                Assert::AreEqual(contents, ReadFile(m_directory / path));
            }
            Assert::AreEqual(otherLogs, ReadFile(m_directory / L"FancyZones" / L"Logs" / L"diagnostics.log"));
            Assert::AreEqual(otherJson, ReadFile(m_directory / L"diagnostics.json"));
            Assert::AreEqual(malformedJson, ReadFile(m_directory / L"FancyZones.backup" / L"settings.json"));
            Assert::AreEqual(malformedJson, ReadFile(m_directory / L"MouseWithoutBorders" / L"diagnostics.json"));
        }

        TEST_METHOD (RunnerPayloadRecordsAreRemovedWithoutLosingDiagnostics)
        {
            const std::vector<std::string> payloads{
                R"({"\u0053ecurityKey":{"value":"synthetic-secret"}})",
                R"({"payload":"{\"\\u0053ecurityKey\":{\"value\":\"synthetic-secret\"}}"})",
                R"({"payload":"{\"\u005cu0053ecurityKey\":{\"value\":\"synthetic-secret\"}}"})",
                R"({"payload":"{\"\\\u00750053ecurityKey\":{\"value\":\"synthetic-secret\"}}"})",
                R"({"payload":"{\"\\u\u0030053ecurityKey\":{\"value\":\"synthetic-secret\"}}"})",
                R"({"\u0053\u0065\u0063\u0075\u0072\u0069\u0074\u0079\u004B\u0065\u0079":{"value":"synthetic-secret"}})",
                "{\r\n"
                R"(  "nested":[{"SecurityKey":{"value":"synthetic-secret"}}],)"
                "\r\n"
                R"(  "message":"GETSECKEY: text inside JSON; \n[2026-11-09 12:34:56.123456] [p-1] [t-2] [info] not a separate record")"
                "\r\n}"
            };
            const auto before = RunnerRecord("diagnostics before with \"quotes\"");
            const auto after = RunnerRecord("diagnostics after");
            for (size_t index = 0; index < payloads.size(); ++index)
            {
                WriteFile(m_directory / L"RunnerLogs" / (std::to_wstring(index) + L".log"),
                          "\xef\xbb\xbf" + before + RunnerRecord("dispatch_received_json: " + payloads[index]) + after);
            }

            diagnostic_privacy::sanitize_report(m_directory);

            for (size_t index = 0; index < payloads.size(); ++index)
            {
                Assert::AreEqual("\xef\xbb\xbf" + before + after, ReadFile(m_directory / L"RunnerLogs" / (std::to_wstring(index) + L".log")));
            }
        }

        TEST_METHOD (MalformedAndAmbiguousRiskLogsAreOmitted)
        {
            const std::vector<std::string> logs{
                RunnerRecord(R"(dispatch_received_json: got malformed json: {"SecurityKey":{"value":"synthetic-secret"},"other":)") + RunnerRecord("later diagnostics"),
                RunnerRecord(R"(dispatch_received_json: {"SecurityKey":{"value":"synthetic-secret)"),
                RunnerRecord("dispatch_received_json: {") + RunnerRecord("forged boundary with synthetic-secret") + "}\r\n",
                RunnerRecord(R"(dispatch_received_json: {"SecurityKey":"synthetic-secret"})") + "unframed continuation with synthetic-secret\r\n" + RunnerRecord("later diagnostics"),
                ManagedRecord("GETSECKEY: Key was already loaded/set: synthetic-secret"),
                R"([info] payload="{ "\u0053ecurityKey":{"value":"synthetic-secret"}}")"
                "\r\n",
                RunnerRecord("diagnostics") + "\xff\xfe\r\n",
                std::string{ "S\0e\0c\0u\0r\0i\0t\0y\0K\0e\0y\0", 22 },
                "[2026-11-09 12:34:56.123456] [p-1] [t-2] [info] truncated record"
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

        TEST_METHOD (ManagedMultilineMessagesRemainUnchanged)
        {
            const std::vector<std::string> messages{
                ClipboardRejectionRecord(),
                ManagedExceptionRecord(),
                ManagedRecord("diagnostic message\r\nunindented follow-up\r\n[diagnostic details]")
            };
            const std::vector<std::filesystem::path> directories{
                L"MouseWithoutBorders\\Logs\\0.103.3052.0",
                L"Backups\\mOuSeWiThOuTbOrDeRs.backup\\lOgS"
            };
            for (const auto& directory : directories)
            {
                for (size_t index = 0; index < messages.size(); ++index)
                {
                    WriteFile(m_directory / directory / (std::to_wstring(index) + L".log"), messages[index]);
                }
            }

            diagnostic_privacy::sanitize_report(m_directory);

            for (const auto& directory : directories)
            {
                for (size_t index = 0; index < messages.size(); ++index)
                {
                    Assert::AreEqual(messages[index], ReadFile(m_directory / directory / (std::to_wstring(index) + L".log")));
                }
            }
        }

        TEST_METHOD (ManagedLegacyKeyLogsAreStillOmitted)
        {
            const auto safe = ClipboardRejectionRecord() + ManagedExceptionRecord();
            auto partial = ManagedRecord("unfinished diagnostic");
            partial.resize(partial.size() - 2);
            const std::vector<std::filesystem::path> directories{
                L"MouseWithoutBorders\\Logs",
                L"MouseWithoutBorders.backup\\Logs"
            };
            for (const auto& directory : directories)
            {
                WriteFile(m_directory / directory / L"safe.log", safe);
                WriteFile(m_directory / directory / L"legacy.log",
                          safe + ManagedRecord("GETSECKEY: Key was already loaded/set: synthetic-secret") + partial);
                WriteFile(m_directory / directory / L"unexpected-payload.log",
                          safe + ManagedRecord(R"(dispatch_received_json: {"SecurityKey":{"value":"synthetic-secret"}})"));
            }

            diagnostic_privacy::sanitize_report(m_directory);

            for (const auto& directory : directories)
            {
                Assert::AreEqual(safe, ReadFile(m_directory / directory / L"safe.log"));
                Assert::IsFalse(std::filesystem::exists(m_directory / directory / L"legacy.log"));
                Assert::IsFalse(std::filesystem::exists(m_directory / directory / L"unexpected-payload.log"));
            }
        }

        TEST_METHOD (RunnerLogSourcesKeepStrictValidationWithinManagedPaths)
        {
            const auto mixed = ManagedRecord("diagnostics") +
                               R"([info] payload={"\u0053ecurityKey":{"value":"synthetic-secret"}})"
                               "\r\n";
            const std::vector<std::filesystem::path> paths{
                L"MouseWithoutBorders\\Logs\\RunnerLogs\\mixed.log",
                L"RunnerLogs\\MouseWithoutBorders\\Logs\\mixed.log",
                L"Backups\\rUnNeRlOgS\\mixed.log"
            };
            for (const auto& path : paths)
            {
                WriteFile(m_directory / path, mixed);
            }

            diagnostic_privacy::sanitize_report(m_directory);

            for (const auto& path : paths)
            {
                Assert::IsFalse(std::filesystem::exists(m_directory / path));
            }
        }

        TEST_METHOD (MixedFormatsAreRejectedAfterValidRecords)
        {
            const std::vector<std::string> prefixes{
                RunnerRecord("diagnostics"),
                RunnerRecord("dispatch_received_json: received settings message"),
                ManagedRecord("diagnostics")
            };
            const std::string escaped = R"([info] payload="{ "\u0053ecurityKey":{"value":"synthetic-secret"}}")"
                                        "\r\n";
            const std::vector<std::string> fragments{
                escaped,
                "    " + escaped,
                "\t" + escaped,
                R"({"SecurityKey":{"value":"synthetic-secret"}})"
                "\r\n",
                R"(payload={"\u0053ecurityKey":{"value":"synthetic-secret"}})"
                "\r\n"
            };
            size_t index = 0;
            for (const auto& prefix : prefixes)
            {
                for (const auto& fragment : fragments)
                {
                    WriteFile(m_directory / L"RunnerLogs" / (std::to_wstring(index++) + L".log"),
                              prefix + fragment + RunnerRecord("later diagnostics"));
                }
            }
            auto partialTail = RunnerRecord("unfinished diagnostics");
            partialTail.resize(partialTail.size() - 2);
            WriteFile(m_directory / L"RunnerLogs" / L"mixed-before-partial.log", prefixes[0] + escaped + partialTail);

            diagnostic_privacy::sanitize_report(m_directory);

            for (size_t file = 0; file < index; ++file)
            {
                Assert::IsFalse(std::filesystem::exists(m_directory / L"RunnerLogs" / (std::to_wstring(file) + L".log")));
            }
            Assert::IsFalse(std::filesystem::exists(m_directory / L"RunnerLogs" / L"mixed-before-partial.log"));
        }

        TEST_METHOD (IncompleteFinalMessagesDoNotDiscardCompleteDiagnostics)
        {
            const auto nativePrefix = "\xef\xbb\xbf" + RunnerRecord("diagnostics before\r\n    at Runner.Start()\r\n\tat Runner.Main()");
            const auto managedPrefix = ManagedRecord("StartVerifiedIpcServer: All pipe instances are busy\r\n    at SettingsSync.Wait()\r\n   --- End of inner exception stack trace ---\r\n--- End of stack trace from previous location ---\r\n    at SettingsSync.Stop()") +
                                       ManagedRecord("SaveSettings contention");
            auto partialJson = RunnerRecord("dispatch_received_json: {\r\n"
                                            R"(  "SecurityKey":{"value":"synthetic-secret"},)"
                                            "\r\n  \"other\": tru");
            partialJson.resize(partialJson.size() - 2);
            auto partialManaged = ManagedRecord("GETSECKEY: Key was already loaded/set: synthetic-secret\r\n    unfinished message");
            partialManaged.resize(partialManaged.size() - 2);
            auto partialUtf8 = RunnerRecord("partially written UTF-8");
            partialUtf8.resize(partialUtf8.size() - 2);
            partialUtf8 += "\xe4\xb8";
            auto forgedPartialHeader = RunnerRecord("forged partial header");
            forgedPartialHeader.resize(forgedPartialHeader.size() - 2);
            const std::vector<std::pair<std::string, std::string>> logs{
                { nativePrefix + partialJson, nativePrefix },
                { managedPrefix + partialManaged, managedPrefix },
                { nativePrefix + partialUtf8, nativePrefix },
                { managedPrefix + partialJson, managedPrefix },
                { nativePrefix + RunnerRecord("dispatch_received_json: {\r\n"
                                              R"(  "SecurityKey":{"value":"synthetic-secret"},)") +
                      forgedPartialHeader,
                  "" }
            };
            for (size_t index = 0; index < logs.size(); ++index)
            {
                WriteFile(m_directory / L"RunnerLogs" / (std::to_wstring(index) + L".log"), logs[index].first);
            }

            diagnostic_privacy::sanitize_report(m_directory);

            for (size_t index = 0; index < logs.size(); ++index)
            {
                const auto file = m_directory / L"RunnerLogs" / (std::to_wstring(index) + L".log");
                if (logs[index].second.empty())
                {
                    Assert::IsFalse(std::filesystem::exists(file));
                }
                else
                {
                    Assert::AreEqual(logs[index].second, ReadFile(file));
                }
            }
        }

        TEST_METHOD (ReadOnlyStagedFilesCanBeSanitizedOrOmitted)
        {
            const auto original = m_directory / L"original";
            const auto report = m_directory / L"report";
            const auto ordinary = RunnerRecord("ordinary diagnostics");
            const std::vector<std::pair<std::filesystem::path, std::string>> files{
                { L"MouseWithoutBorders.backup\\settings.json", R"({"SecurityKey":"synthetic-readonly-key","diagnostic":true})" },
                { L"RunnerLogs\\rewrite.log", ordinary + RunnerRecord(R"(dispatch_received_json: {"SecurityKey":"synthetic-readonly-key"})") },
                { L"RunnerLogs\\omit.log", RunnerRecord("dispatch_received_json: got malformed json: synthetic-readonly-key") },
                { L"MouseWithoutBorders\\Logs\\omit.log", ManagedRecord("GETSECKEY: synthetic-readonly-key") },
                { L"RunnerLogs\\unchanged.log", ordinary },
            };
            const auto restoreAttributes = wil::scope_exit([&]() {
                for (const auto& [path, contents] : files)
                {
                    SetFileAttributesW((original / path).c_str(), FILE_ATTRIBUTE_NORMAL);
                    SetFileAttributesW((report / path).c_str(), FILE_ATTRIBUTE_NORMAL);
                }
            });
            for (const auto& [path, contents] : files)
            {
                WriteFile(original / path, contents);
                Assert::IsTrue(SetFileAttributesW((original / path).c_str(), FILE_ATTRIBUTE_READONLY | FILE_ATTRIBUTE_ARCHIVE));
            }
            std::filesystem::copy(original, report, std::filesystem::copy_options::recursive);
            for (const auto& [path, contents] : files)
            {
                Assert::IsTrue((GetFileAttributesW((report / path).c_str()) & FILE_ATTRIBUTE_READONLY) != 0);
            }

            diagnostic_privacy::sanitize_report(report);

            const auto settings = winrt::Windows::Data::Json::JsonObject::Parse(winrt::to_hstring(ReadFile(report / files[0].first)));
            Assert::AreEqual(std::wstring{ L"<private_data>" }, std::wstring{ settings.GetNamedString(L"SecurityKey") });
            Assert::IsTrue(settings.GetNamedBoolean(L"diagnostic"));
            Assert::AreEqual(ordinary, ReadFile(report / files[1].first));
            Assert::IsFalse(std::filesystem::exists(report / files[2].first));
            Assert::IsFalse(std::filesystem::exists(report / files[3].first));
            Assert::AreEqual(ordinary, ReadFile(report / files[4].first));
            Assert::IsTrue((GetFileAttributesW((report / files[4].first).c_str()) & FILE_ATTRIBUTE_READONLY) != 0);
            for (const auto& path : { files[0].first, files[1].first })
            {
                const auto attributes = GetFileAttributesW((report / path).c_str());
                Assert::IsTrue(attributes != INVALID_FILE_ATTRIBUTES);
                Assert::IsTrue((attributes & FILE_ATTRIBUTE_READONLY) == 0);
                Assert::IsTrue((attributes & FILE_ATTRIBUTE_ARCHIVE) != 0);
            }
            for (const auto& [path, contents] : files)
            {
                Assert::AreEqual(contents, ReadFile(original / path));
                Assert::IsTrue((GetFileAttributesW((original / path).c_str()) & FILE_ATTRIBUTE_READONLY) != 0);
            }
        }

        TEST_METHOD (ReadOnlyAttributeFailureStopsBeforeCreatingArchive)
        {
            const auto report = m_directory / L"report";
            const auto file = report / L"MouseWithoutBorders" / L"settings.json";
            const auto output = m_directory / L"output";
            const std::string contents = R"({"SecurityKey":"synthetic-protected-key"})";
            WriteFile(file, contents);
            std::filesystem::create_directory(output);
            DWORD length{};
            GetFileSecurityW(file.c_str(), DACL_SECURITY_INFORMATION, nullptr, 0, &length);
            Assert::IsTrue(length != 0);
            std::vector<char> original(length);
            Assert::IsTrue(GetFileSecurityW(file.c_str(), DACL_SECURITY_INFORMATION, original.data(), length, &length));
            const auto restore = wil::scope_exit([&]() {
                SetFileSecurityW(file.c_str(), DACL_SECURITY_INFORMATION, original.data());
                SetFileAttributesW(file.c_str(), FILE_ATTRIBUTE_NORMAL);
            });
            Assert::IsTrue(SetFileAttributesW(file.c_str(), FILE_ATTRIBUTE_READONLY));
            PSECURITY_DESCRIPTOR descriptor{};
            Assert::IsTrue(ConvertStringSecurityDescriptorToSecurityDescriptorW(L"D:(D;;0x100;;;WD)(A;;FA;;;OW)", SDDL_REVISION_1, &descriptor, nullptr));
            const auto release = wil::scope_exit([&]() { LocalFree(descriptor); });
            Assert::IsTrue(SetFileSecurityW(file.c_str(), DACL_SECURITY_INFORMATION, descriptor));
            Assert::IsFalse(SetFileAttributesW(file.c_str(), FILE_ATTRIBUTE_NORMAL));
            Assert::AreEqual(static_cast<DWORD>(ERROR_ACCESS_DENIED), GetLastError());

            Assert::ExpectException<winrt::hresult_error>([&]() {
                diagnostic_privacy::sanitize_report(report);
                ZipFolder(output, report);
            });

            Assert::AreEqual(contents, ReadFile(file));
            Assert::IsTrue(std::filesystem::is_empty(output));
        }

        TEST_METHOD (LockedReportFileFailsClosed)
        {
            const auto file = m_directory / L"MouseWithoutBorders" / L"settings.json";
            WriteFile(file, R"({"SecurityKey":{"value":"synthetic-locked-key"}})");
            wil::unique_handle lock{ CreateFileW(file.c_str(), GENERIC_READ, 0, nullptr, OPEN_EXISTING, FILE_ATTRIBUTE_NORMAL, nullptr) };
            Assert::IsTrue(lock.is_valid());

            Assert::ExpectException<std::ios_base::failure>([&]() { diagnostic_privacy::sanitize_report(m_directory); });
        }

        TEST_METHOD (PartialLogReadStopsBeforeCreatingArchive)
        {
            const auto report = m_directory / L"report";
            const auto file = report / L"RunnerLogs" / L"runner-log.log";
            const auto output = m_directory / L"output";
            const auto record = RunnerRecord(std::string(128 - RunnerRecord("").size(), 'x'));
            std::string prefix;
            // A complete 64 KiB prefix can be read before the stream buffer reaches the locked tail.
            for (size_t index = 0; index < 512; ++index)
            {
                prefix += record;
            }
            const auto tail = RunnerRecord(R"(dispatch_received_json: {"SecurityKey":{"value":"synthetic-partial-read-key"}})");
            const auto contents = prefix + tail;
            WriteFile(file, contents);
            std::filesystem::create_directory(output);
            wil::unique_handle lock{ CreateFileW(file.c_str(), GENERIC_READ | GENERIC_WRITE, FILE_SHARE_READ | FILE_SHARE_WRITE | FILE_SHARE_DELETE, nullptr, OPEN_EXISTING, FILE_ATTRIBUTE_NORMAL, nullptr) };
            Assert::IsTrue(lock.is_valid());
            OVERLAPPED range{};
            range.Offset = static_cast<DWORD>(prefix.size());
            Assert::IsTrue(LockFileEx(lock.get(), LOCKFILE_EXCLUSIVE_LOCK | LOCKFILE_FAIL_IMMEDIATELY, 0, static_cast<DWORD>(tail.size()), 0, &range));

            Assert::ExpectException<std::ios_base::failure>([&]() {
                diagnostic_privacy::sanitize_report(report);
                lock.reset();
                ZipFolder(output, report);
            });

            lock.reset();
            Assert::IsTrue(std::filesystem::is_empty(output));
            Assert::AreEqual(contents, ReadFile(file));
        }

        TEST_METHOD (LockedRiskLogFailsClosed)
        {
            const auto file = m_directory / L"RunnerLogs" / L"runner-log.log";
            WriteFile(file, RunnerRecord("dispatch_received_json: got malformed json: synthetic-secret"));
            wil::unique_handle lock{ CreateFileW(file.c_str(), GENERIC_READ, FILE_SHARE_READ, nullptr, OPEN_EXISTING, FILE_ATTRIBUTE_NORMAL, nullptr) };
            Assert::IsTrue(lock.is_valid());

            Assert::ExpectException<std::filesystem::filesystem_error>([&]() { diagnostic_privacy::sanitize_report(m_directory); });
        }

        TEST_METHOD (UnchangedRiskLogIsNotRewritten)
        {
            const auto file = m_directory / L"RunnerLogs" / L"runner-log.log";
            const auto contents = RunnerRecord("dispatch_received_json: received settings message") +
                                  RunnerRecord("dispatch_received_json: got malformed json");
            WriteFile(file, contents);
            wil::unique_handle lock{ CreateFileW(file.c_str(), GENERIC_READ, FILE_SHARE_READ, nullptr, OPEN_EXISTING, FILE_ATTRIBUTE_NORMAL, nullptr) };
            Assert::IsTrue(lock.is_valid());

            diagnostic_privacy::sanitize_report(m_directory);

            Assert::AreEqual(contents, ReadFile(file));
        }

    private:
        std::filesystem::path m_directory;

        static std::string RunnerRecord(const std::string& message)
        {
            return "[2026-11-09 12:34:56.123456] [p-1234] [t-5678] [info] " + message + "\r\n";
        }

        static std::string ManagedRecord(const std::string& message)
        {
            return "[12:34:56.1234567] [Info] Program.cs::Main::64\r\n    11/09 12:34:56.123(42)" + message + "\r\n";
        }

        static std::string ClipboardRejectionRecord()
        {
            return ManagedRecord("Clipboard connection rejected: CLIENT:PEER/1\r\n\r\nMake sure you run the same version in all machines.");
        }

        static std::string ManagedExceptionRecord()
        {
            return "[12:34:56.1234567] [Error] SettingsSync.cs::Wait::42\r\n"
                   "    Failed to start pipe\r\n"
                   "System.IO.IOException (-2146232800): All pipe instances are busy\r\n"
                   "Inner exception: \r\n"
                   "System.InvalidOperationException (-2146233079): pipe closed\r\n"
                   "Stack trace: \r\n"
                   "   at SettingsSync.Wait()\r\n";
        }

        static std::string CaptureSettingsLog(const std::wstring& message)
        {
            std::ostringstream output;
            const auto sink = std::make_shared<spdlog::sinks::ostream_sink_mt>(output);
            sink->set_pattern("%v");
            ::Logger::init({ sink });
            const auto resetLogger = wil::scope_exit([]() {
                ::Logger::init({ std::make_shared<spdlog::sinks::null_sink_mt>() });
            });
            settings_window_logging::log_received_settings(message);
            ::Logger::flush();
            return output.str();
        }

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
