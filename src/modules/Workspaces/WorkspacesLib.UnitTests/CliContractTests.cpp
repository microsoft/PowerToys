// Copyright (c) Microsoft Corporation. Licensed under the MIT license.
#include "pch.h"
#include <WorkspacesLib/CliCommands.h>
#include <WorkspacesLib/LaunchingStatus.h>
#include <fstream>

using namespace Microsoft::VisualStudio::CppUnitTestFramework;

namespace WorkspacesLibUnitTests
{
    TEST_CLASS (CliContractTests)
    {
        static WorkspacesData::WorkspacesProject Project()
        {
            WorkspacesData::WorkspacesProject project{};
            project.id = L"{6CF910A2-D2E0-436D-A50E-41432A88452A}";
            project.name = L"Development";
            WorkspacesData::WorkspacesProject::Application app{};
            app.id = L"{49330ECB-5917-4DF3-B70A-1F2D7FE4899B}";
            app.name = L"Notepad";
            app.path = L"C:\\private\\notepad.exe";
            app.title = L"private title";
            app.commandLineArgs = L"private arguments";
            project.apps = { app, app };
            return project;
        }

        static json::JsonObject FinalResult(const WorkspacesData::WorkspacesProject& project, const std::vector<LaunchingState>& states)
        {
            json::JsonObject message;
            message.SetNamedValue(L"kind", json::value(L"arranger-result"));
            message.SetNamedValue(L"protocolVersion", json::value(1));
            message.SetNamedValue(L"operationId", json::value(L"operation"));
            json::JsonArray applications;
            for (size_t i = 0; i < states.size(); ++i)
                applications.Append(WorkspacesData::AppLaunchInfoJSON::ToJson({ project.apps[i], nullptr, states[i] }));
            message.SetNamedValue(L"applications", applications);
            return message;
        }

    public:
        TEST_METHOD (SharedSessionResultRoundTripsWithoutChangingTheWireEnvelope)
        {
            auto project = Project();
            project.apps.resize(1);
            project.monitors.push_back({ .number = 1, .dpi = 96 });
            project.apps[0].monitor = 1;
            project.apps[0].position = { 10, 20, 640, 400 };
            WorkspacesSession::Result original;
            original.operationId = L"operation";
            original.project = project;
            original.applications.emplace(project.apps[0], WorkspacesData::LaunchingAppState{ project.apps[0], nullptr, LaunchingState::Launched });
            original.persistence = WorkspaceStore::UpdateResult::Updated;
            original.loggingAvailable = false;
            const auto message = WorkspacesSession::SerializeResult(original);
            const auto before = std::wstring(message.Stringify());
            const auto decoded = WorkspacesSession::DeserializeResult(message, L"operation");
            Assert::AreEqual(before, std::wstring(message.Stringify()));
            Assert::AreEqual(project.id, decoded.project.id);
            Assert::IsTrue(decoded.applications.at(project.apps[0]).state == LaunchingState::Launched);
            Assert::IsTrue(decoded.persistence == WorkspaceStore::UpdateResult::Updated);
            Assert::IsFalse(decoded.loggingAvailable);
            Assert::IsFalse(message.HasKey(L"command"));
            Assert::ExpectException<WorkspacesSession::Error>([&] { WorkspacesSession::DeserializeResult(message, L"another-operation"); });
        }

        TEST_METHOD (SharedSessionErrorsRemainIndependentOfCliFormatting)
        {
            const auto message = WorkspacesSession::SerializeError({ 12, L"canceled", "Controlled cancellation." }, L"operation");
            Assert::AreEqual(std::wstring(L"session-error"), std::wstring(message.GetNamedString(L"kind")));
            Assert::IsFalse(message.HasKey(L"schemaVersion"));
            try
            {
                WorkspacesSession::DeserializeResult(message, L"operation");
                Assert::Fail(L"The private session error must propagate.");
            }
            catch (const WorkspacesSession::Error& error)
            {
                Assert::AreEqual(12, error.exitCode);
                Assert::AreEqual(std::wstring(L"canceled"), error.code);
                Assert::AreEqual(std::wstring(L"launch"), std::wstring(WorkspacesCli::Failure(L"launch", error).GetNamedString(L"command")));
            }
        }

        TEST_METHOD (CliAdapterPreservesSessionPersistenceAndProgress)
        {
            auto project = Project();
            project.apps.resize(1);
            WorkspacesSession::Result session;
            session.project = project;
            session.operationId = L"operation";
            session.applications.emplace(project.apps[0], WorkspacesData::LaunchingAppState{ project.apps[0], nullptr, LaunchingState::LaunchedAndMoved });
            session.persistence = WorkspaceStore::UpdateResult::Failed;
            int exitCode{};
            auto output = WorkspacesCli::SessionResult(session, exitCode);
            Assert::AreEqual(0, exitCode);
            Assert::AreEqual(std::wstring(L"completed"), std::wstring(output.GetNamedString(L"state")));
            Assert::AreEqual(std::wstring(L"metadataSaveFailed"), std::wstring(output.GetNamedArray(L"warnings").GetObjectAt(0).GetNamedString(L"code")));
            session.persistence = WorkspaceStore::UpdateResult::Unverified;
            output = WorkspacesCli::SessionResult(session, exitCode);
            Assert::AreEqual(11, exitCode);
            Assert::AreEqual(std::wstring(L"persistenceUnverified"), std::wstring(output.GetNamedObject(L"error").GetNamedString(L"code")));
            Assert::AreEqual(std::wstring(L"arranged"), std::wstring(output.GetNamedObject(L"result").GetNamedArray(L"applications").GetObjectAt(0).GetNamedString(L"state")));
        }

        TEST_METHOD (FinalArrangerResultRejectsWaiting)
        {
            auto project = Project();
            project.apps.resize(1);
            LaunchingStatus status(project);
            try
            {
                WorkspacesCli::ParseArrangerResult(FinalResult(project, { LaunchingState::Waiting }), status.Get(), L"operation");
                Assert::Fail(L"Waiting cannot be accepted as a final application outcome.");
            }
            catch (const WorkspacesCli::Error& error)
            {
                Assert::AreEqual(9, error.exitCode);
                Assert::AreEqual(std::wstring(L"outcomeUnknown"), error.code);
                const auto response = WorkspacesCli::Failure(L"launch", error);
                Assert::IsFalse(response.HasKey(L"result"));
                Assert::AreEqual(9.0, response.GetNamedObject(L"error").GetNamedNumber(L"exitCode"));
            }
        }

        TEST_METHOD (FinalArrangerResultRetainsLaunchedAndExistingOutcomes)
        {
            auto project = Project();
            project.apps.resize(1);
            LaunchingStatus status(project);
            for (const auto state : { LaunchingState::Launched, LaunchingState::LaunchedAndMoved, LaunchingState::Failed,
                                      LaunchingState::Canceled, LaunchingState::Skipped })
            {
                const auto result = WorkspacesCli::ParseArrangerResult(FinalResult(project, { state }), status.Get(), L"operation");
                Assert::AreEqual(size_t{ 1 }, result.size());
                Assert::IsTrue(result.at(project.apps[0]).state == state);
            }
        }

        TEST_METHOD (FinalArrangerResultRejectsWaitingAnywhereWithoutChangingExpectedState)
        {
            auto project = Project();
            project.apps[1].id = L"{682A3A9E-D18D-4DC6-B2AF-E6ABD92F42A9}";
            LaunchingStatus status(project);
            const auto expected = status.Get();
            for (const bool waitingFirst : { false, true })
            {
                const auto message = FinalResult(project, waitingFirst ?
                    std::vector{ LaunchingState::Waiting, LaunchingState::LaunchedAndMoved } :
                    std::vector{ LaunchingState::LaunchedAndMoved, LaunchingState::Waiting });
                Assert::ExpectException<WorkspacesCli::Error>([&] { WorkspacesCli::ParseArrangerResult(message, expected, L"operation"); });
                Assert::IsTrue(expected.at(project.apps[0]).state == LaunchingState::Waiting);
                Assert::IsTrue(expected.at(project.apps[1]).state == LaunchingState::Waiting);
            }
        }

        TEST_METHOD (FinalArrangerResultRejectsMissingDuplicateAndUnknownApplications)
        {
            auto project = Project();
            project.apps[1].id = L"{682A3A9E-D18D-4DC6-B2AF-E6ABD92F42A9}";
            LaunchingStatus status(project);
            const auto expected = status.Get();
            auto message = FinalResult(project, { LaunchingState::Launched });
            Assert::ExpectException<WorkspacesCli::Error>([&] { WorkspacesCli::ParseArrangerResult(message, expected, L"operation"); });
            message = FinalResult(project, { LaunchingState::Launched, LaunchingState::LaunchedAndMoved });
            auto applications = message.GetNamedArray(L"applications");
            applications.SetAt(1, applications.GetAt(0));
            Assert::ExpectException<WorkspacesCli::Error>([&] { WorkspacesCli::ParseArrangerResult(message, expected, L"operation"); });
            message = FinalResult(project, { LaunchingState::Launched, LaunchingState::LaunchedAndMoved });
            message.GetNamedArray(L"applications").GetObjectAt(0).GetNamedObject(L"application").SetNamedValue(L"id", json::value(L"unknown-app"));
            Assert::ExpectException<WorkspacesCli::Error>([&] { WorkspacesCli::ParseArrangerResult(message, expected, L"operation"); });
        }

        TEST_METHOD (FinalArrangerResultRejectsInvalidProtocolAndStates)
        {
            auto project = Project();
            project.apps.resize(1);
            LaunchingStatus status(project);
            const auto expected = status.Get();
            for (const auto key : { L"kind", L"operationId" })
            {
                auto message = FinalResult(project, { LaunchingState::Launched });
                message.SetNamedValue(key, json::value(L"wrong"));
                Assert::ExpectException<WorkspacesCli::Error>([&] { WorkspacesCli::ParseArrangerResult(message, expected, L"operation"); });
            }
            auto message = FinalResult(project, { LaunchingState::Launched });
            message.SetNamedValue(L"protocolVersion", json::value(2));
            Assert::ExpectException<WorkspacesCli::Error>([&] { WorkspacesCli::ParseArrangerResult(message, expected, L"operation"); });
            for (const auto state : { static_cast<LaunchingState>(-1), static_cast<LaunchingState>(6) })
            {
                message = FinalResult(project, { state });
                Assert::ExpectException<WorkspacesCli::Error>([&] { WorkspacesCli::ParseArrangerResult(message, expected, L"operation"); });
            }
        }

        TEST_METHOD (FinalArrangerResultRejectsFractionalAndOutOfRangeNumbers)
        {
            auto project = Project();
            project.apps[1].id = L"{682A3A9E-D18D-4DC6-B2AF-E6ABD92F42A9}";
            LaunchingStatus status(project);
            const auto expected = status.Get();
            for (const double state : { 1.5, 2.9, 5.5, -0.5, 1e100 })
            {
                auto message = FinalResult(project, { LaunchingState::LaunchedAndMoved, LaunchingState::Launched });
                message.GetNamedArray(L"applications").GetObjectAt(1).SetNamedValue(L"state", json::value(state));
                try
                {
                    WorkspacesCli::ParseArrangerResult(message, expected, L"operation");
                    Assert::Fail(L"A fractional or out-of-range number cannot be a final application state.");
                }
                catch (const WorkspacesCli::Error& error)
                {
                    Assert::AreEqual(9, error.exitCode);
                    Assert::AreEqual(std::wstring(L"outcomeUnknown"), error.code);
                }
                Assert::IsTrue(expected.at(project.apps[0]).state == LaunchingState::Waiting);
                Assert::IsTrue(expected.at(project.apps[1]).state == LaunchingState::Waiting);
            }
        }

        TEST_METHOD (LaunchResultRejectsPendingOrMissingOutcomes)
        {
            auto project = Project();
            project.apps.resize(1);
            LaunchingStatus status(project);
            int exitCode = 0;
            Assert::ExpectException<WorkspacesCli::Error>([&] { WorkspacesCli::LaunchResult(project, status.Get(), L"operation", exitCode); });
            Assert::ExpectException<WorkspacesCli::Error>([&] { WorkspacesCli::LaunchResult(project, {}, L"operation", exitCode); });
        }

        TEST_METHOD (SkippedAndUnavailableAreNotUacDenial)
        {
            WorkspacesData::WorkspacesProject project{};
            WorkspacesData::WorkspacesProject::Application app{};
            app.id = L"test-app";
            project.apps.push_back(app);
            WorkspacesData::LaunchingAppStateMap states;
            states.emplace(app, WorkspacesData::LaunchingAppState{ app, nullptr, LaunchingState::Skipped });
            int exitCode{};
            auto result = WorkspacesCli::LaunchResult(project, states, L"test", exitCode, { { app.id, ERROR_CANCELLED } }, { { app.id, LaunchDecision::Skipped } });
            auto item = result.GetNamedObject(L"result").GetNamedArray(L"applications").GetObjectAt(0);
            Assert::AreEqual(std::wstring(L"skipped"), std::wstring(item.GetNamedString(L"state")));
            Assert::AreEqual(std::wstring(L"skipped"), std::wstring(item.GetNamedString(L"code")));
            Assert::IsFalse(item.HasKey(L"nativeError"));
            Assert::AreEqual(10, exitCode);
            states.at(app).state = LaunchingState::Failed;
            result = WorkspacesCli::LaunchResult(project, states, L"test", exitCode, { { app.id, ERROR_NOT_READY } }, { { app.id, LaunchDecision::UiUnavailable } });
            item = result.GetNamedObject(L"result").GetNamedArray(L"applications").GetObjectAt(0);
            Assert::AreEqual(std::wstring(L"confirmationRequired"), std::wstring(item.GetNamedString(L"code")));
            states.at(app).state = LaunchingState::Canceled;
            result = WorkspacesCli::LaunchResult(project, states, L"test", exitCode, { { app.id, ERROR_CANCELLED } }, { { app.id, LaunchDecision::Approved } });
            item = result.GetNamedObject(L"result").GetNamedArray(L"applications").GetObjectAt(0);
            Assert::AreEqual(std::wstring(L"consentDenied"), std::wstring(item.GetNamedString(L"code")));
        }

        TEST_METHOD (PreliminaryLaunchValidationAllowsOnlyMissingIds)
        {
            auto project = Project();
            project.apps.resize(1);
            project.monitors.push_back({ .number = 1, .dpi = 96 });
            project.apps[0].monitor = 1;
            project.apps[0].position = { 10, 20, 640, 400 };
            project.apps[0].id.clear();
            WorkspacesCli::ValidateLaunch(project, true);
            Assert::ExpectException<WorkspacesCli::Error>([&] { WorkspacesCli::ValidateLaunch(project); });
            project.apps[0].id = L"malformed";
            Assert::ExpectException<WorkspacesCli::Error>([&] { WorkspacesCli::ValidateLaunch(project, true); });
            project.apps[0].id.clear();
            project.apps[0].position.width = 0;
            Assert::ExpectException<WorkspacesCli::Error>([&] { WorkspacesCli::ValidateLaunch(project, true); });
        }

        TEST_METHOD (ListingLegacyApplicationsDoesNotAssignIds)
        {
            auto project = Project();
            project.apps[0].id.clear();
            const auto before = std::wstring(WorkspacesData::WorkspacesProjectJSON::ToJson(project).Stringify());
            for (const bool details : { false, true })
                WorkspacesCli::ListResult({ project }, details);
            Assert::AreEqual(before, std::wstring(WorkspacesData::WorkspacesProjectJSON::ToJson(project).Stringify()));
            Assert::IsTrue(project.apps[0].id.empty());
        }

        TEST_METHOD (ListResponsesContainOnlyViewAndWorkspaces)
        {
            for (const bool details : { false, true })
            {
                const auto output = WorkspacesCli::ListResult({ Project() }, details);
                Assert::AreEqual(2u, output.Size());
                Assert::AreEqual(std::wstring(details ? L"detail" : L"summary"), std::wstring(output.GetNamedString(L"view")));
                Assert::AreEqual(1u, output.GetNamedArray(L"workspaces").Size());
                Assert::IsFalse(output.HasKey(L"schemaVersion"));
                Assert::IsFalse(output.HasKey(L"command"));
                Assert::IsFalse(output.HasKey(L"state"));
                Assert::IsFalse(output.HasKey(L"result"));
            }
        }

        TEST_METHOD (EmptyListsKeepArrayWithoutSuccessMetadata)
        {
            const auto output = WorkspacesCli::ListResult({}, false);
            Assert::AreEqual(2u, output.Size());
            Assert::AreEqual(std::wstring(L"summary"), std::wstring(output.GetNamedString(L"view")));
            Assert::AreEqual(0u, output.GetNamedArray(L"workspaces").Size());
        }

        TEST_METHOD (ListFailuresKeepStructuredErrorEnvelope)
        {
            const auto output = WorkspacesCli::Failure(L"list", { 3, L"workspaceNotFound", "No workspace matches the selector." });
            Assert::AreEqual(4u, output.Size());
            Assert::AreEqual(1.0, output.GetNamedNumber(L"schemaVersion"));
            Assert::AreEqual(std::wstring(L"list"), std::wstring(output.GetNamedString(L"command")));
            Assert::AreEqual(std::wstring(L"failed"), std::wstring(output.GetNamedString(L"state")));
            Assert::AreEqual(std::wstring(L"workspaceNotFound"), std::wstring(output.GetNamedObject(L"error").GetNamedString(L"code")));
            Assert::AreEqual(3.0, output.GetNamedObject(L"error").GetNamedNumber(L"exitCode"));
            Assert::IsFalse(output.HasKey(L"workspaces"));
        }

        TEST_METHOD (SummaryIsSubsetWithExistingNamesAndDuplicates)
        {
            const auto project = Project();
            const auto summary = WorkspacesCli::ListResult({ project }, false).GetNamedArray(L"workspaces").GetObjectAt(0);
            const auto detail = WorkspacesCli::ListResult({ project }, true).GetNamedArray(L"workspaces").GetObjectAt(0);
            Assert::AreEqual(3u, summary.Size());
            Assert::IsFalse(summary.HasKey(L"apps"));
            Assert::IsFalse(summary.HasKey(L"appCount"));
            Assert::AreEqual(project.id, std::wstring(summary.GetNamedString(L"id")));
            const auto apps = summary.GetNamedArray(L"applications");
            Assert::AreEqual(2u, apps.Size());
            for (uint32_t i = 0; i < apps.Size(); ++i)
            {
                const auto app = apps.GetObjectAt(i);
                Assert::AreEqual(1u, app.Size());
                Assert::IsFalse(app.HasKey(L"name"));
                Assert::AreEqual(std::wstring(detail.GetNamedArray(L"applications").GetObjectAt(i).GetNamedString(L"application")),
                                 std::wstring(app.GetNamedString(L"application")));
            }
        }

        TEST_METHOD (EmptyAndUnicodeApplicationNamesAreNotInvented)
        {
            auto project = Project();
            project.apps[0].name.clear();
            project.apps[1].name = L"\u8bb0\u4e8b\u672c";
            const auto apps = WorkspacesCli::ListResult({ project }, false).GetNamedArray(L"workspaces").GetObjectAt(0).GetNamedArray(L"applications");
            Assert::IsTrue(apps.GetObjectAt(0).GetNamedString(L"application").empty());
            Assert::AreEqual(project.apps[1].name, std::wstring(apps.GetObjectAt(1).GetNamedString(L"application")));
            project.apps.clear();
            Assert::AreEqual(0u, WorkspacesCli::ListResult({ project }, false).GetNamedArray(L"workspaces").GetObjectAt(0).GetNamedArray(L"applications").Size());
        }

        TEST_METHOD (DetailReusesSerializerWithoutChangingPayload)
        {
            const auto project = Project();
            const auto detail = WorkspacesCli::ListResult({ project }, true).GetNamedArray(L"workspaces").GetObjectAt(0);
            Assert::AreEqual(std::wstring(WorkspacesData::WorkspacesProjectJSON::ToJson(project).Stringify()),
                             std::wstring(detail.Stringify()));
        }

        TEST_METHOD (SelectorsUseGuidValuesAndExactCaseInsensitiveNames)
        {
            const auto project = Project();
            auto byId = WorkspacesCli::Parse({ L"list", L"--id", L"6cf910a2-d2e0-436d-a50e-41432a88452a" });
            Assert::AreEqual(size_t{ 1 }, WorkspacesCli::Select({ project }, byId).size());
            auto byName = WorkspacesCli::Parse({ L"launch", L"--name", L"development" });
            Assert::AreEqual(size_t{ 1 }, WorkspacesCli::Select({ project }, byName).size());
            auto another = project;
            another.id = L"{49330ECB-5917-4DF3-B70A-1F2D7FE4899B}";
            Assert::ExpectException<WorkspacesCli::Error>([&] { WorkspacesCli::Select({ project, another }, byName); });
            Assert::ExpectException<WorkspacesCli::Error>([&] { WorkspacesCli::Select({ project, project }, byId); });
        }

        TEST_METHOD (RejectsInvalidOptionsAndTimeoutBoundaries)
        {
            const std::vector<std::vector<std::wstring>> invalid{
                {}, { L"capture" }, { L"list", L"--details" }, { L"list", L"--id" }, { L"launch" }, { L"launch", L"--name", L" " }, { L"list", L"--name", L"x", L"--id", Project().id }, { L"launch", L"--name", L"x", L"--details" }, { L"launch", L"--name", L"x", L"--timeout", L"0" }, { L"launch", L"--name", L"x", L"--timeout", L"601" }, { L"launch", L"--name", L"x", L"--timeout", L"1x" }, { L"list", L"--json", L"--json" }
            };
            for (const auto& args : invalid)
                Assert::ExpectException<WorkspacesCli::Error>([&] { WorkspacesCli::Parse(args); });
            Assert::AreEqual(1ul, WorkspacesCli::Parse({ L"launch", L"--name", L"x", L"--timeout", L"1" }).timeoutSeconds);
            Assert::AreEqual(600ul, WorkspacesCli::Parse({ L"launch", L"--name", L"x", L"--timeout", L"600" }).timeoutSeconds);
        }

        TEST_METHOD (OutputModeDoesNotMistakeSelectorValueForFlag)
        {
            Assert::IsFalse(WorkspacesCli::WantsJson({ L"list", L"--name", L"--json" }));
            Assert::IsTrue(WorkspacesCli::WantsJson({ L"list", L"--name", L"--json", L"--json" }));
            Assert::AreEqual(std::wstring(L"--help"), WorkspacesCli::Parse({ L"launch", L"--help" }).command);
        }

        TEST_METHOD (TelemetryCommandCategoryNeverContainsUserArguments)
        {
            struct Case
            {
                std::vector<std::wstring> arguments;
                const wchar_t* category;
            };
            const Case cases[] = {
                { {}, L"none" },
                { { L"list", L"--name", L"Private workspace", L"--json" }, L"list" },
                { { L"launch", L"--id", Project().id, L"--timeout", L"42" }, L"launch" },
                { { L"--help" }, L"help" },
                { { L"list", L"--help" }, L"help" },
                { { L"launch", L"--help" }, L"help" },
                { { L"--version" }, L"version" },
                { { L"C:\\private\\document.txt", L"private argument" }, L"unknown" },
                { { std::wstring(L"launch\0private", 14) }, L"unknown" },
            };
            for (const auto& test : cases)
                Assert::AreEqual(std::wstring(test.category), std::wstring(WorkspacesCli::TelemetryCommandName(test.arguments)));
        }

        TEST_METHOD (TelemetryDoesNotInterpretSelectorValuesAsHelpOrCommands)
        {
            Assert::AreEqual(std::wstring(L"list"),
                             std::wstring(WorkspacesCli::TelemetryCommandName({ L"list", L"--name", L"--help" })));
            Assert::AreEqual(std::wstring(L"launch"),
                             std::wstring(WorkspacesCli::TelemetryCommandName({ L"launch", L"--name", L"list" })));
            Assert::AreEqual(std::wstring(L"unknown"),
                             std::wstring(WorkspacesCli::TelemetryCommandName({ L"private-value", L"launch" })));
        }

        TEST_METHOD (UserDisabledAndMalformedSettingsAreErrors)
        {
            Assert::ExpectException<WorkspacesCli::Error>([] { WorkspacesCli::CheckUserSetting(std::nullopt); });
            Assert::ExpectException<WorkspacesCli::Error>([] {
                WorkspacesCli::CheckUserSetting(json::JsonObject::Parse(LR"({"enabled":{"Workspaces":false}})"));
            });
            Assert::ExpectException<WorkspacesCli::Error>([] {
                WorkspacesCli::CheckUserSetting(json::JsonObject::Parse(LR"({"enabled":{"Workspaces":"yes"}})"));
            });
            WorkspacesCli::CheckUserSetting(json::JsonObject::Parse(LR"({"enabled":{"Workspaces":true}})"));
        }

        TEST_METHOD (ReadDistinguishesMissingAndMalformedFilesWithoutCreatingDefaults)
        {
            GUID guid{};
            CoCreateGuid(&guid);
            wchar_t id[40]{};
            StringFromGUID2(guid, id, ARRAYSIZE(id));
            auto file = std::filesystem::temp_directory_path() / (std::wstring(L"WorkspacesCliTest-") + id + L".json");
            Assert::IsFalse(WorkspacesCli::ReadJson(file).has_value());
            Assert::IsFalse(std::filesystem::exists(file));
            {
                std::ofstream stream(file);
                stream << "{invalid}";
            }
            try
            {
                Assert::ExpectException<WorkspacesCli::Error>([&] { WorkspacesCli::ReadJson(file); });
            }
            catch (...)
            {
                std::filesystem::remove(file);
                throw;
            }
            std::filesystem::remove(file);
        }

        TEST_METHOD (LaunchedWithoutArrangementConfirmationIsNotAnArrangementFailure)
        {
            auto project = Project();
            project.apps.resize(1);
            LaunchingStatus states(project);
            states.Update(project.apps[0], LaunchingState::Launched);
            int exitCode = 0;
            const auto result = WorkspacesCli::LaunchResult(project, states.Get(), L"operation", exitCode);
            const auto app = result.GetNamedObject(L"result").GetNamedArray(L"applications").GetObjectAt(0);
            Assert::AreEqual(project.apps[0].id, std::wstring(app.GetNamedString(L"id")));
            Assert::AreEqual(std::wstring(L"launched"), std::wstring(app.GetNamedString(L"state")));
            Assert::AreEqual(std::wstring(L"The application launch succeeded, but window arrangement was not confirmed."),
                             std::wstring(app.GetNamedString(L"message")));
            Assert::IsFalse(app.HasKey(L"code"));
            Assert::IsFalse(app.HasKey(L"nativeError"));
            Assert::AreEqual(10, exitCode);
            Assert::AreEqual(std::wstring(L"partial"), std::wstring(result.GetNamedString(L"state")),
                             L"Launch progress without confirmed arrangement is incomplete, not wholly failed.");
        }

        TEST_METHOD (AggregateResultDistinguishesLaunchProgressFromNoProgress)
        {
            struct Case
            {
                LaunchingState first;
                LaunchingState second;
                const wchar_t* expected;
                int exitCode;
            };
            const Case cases[] = {
                { LaunchingState::Skipped, LaunchingState::Launched, L"partial", 10 },
                { LaunchingState::Launched, LaunchingState::Skipped, L"partial", 10 },
                { LaunchingState::Launched, LaunchingState::Launched, L"partial", 10 },
                { LaunchingState::Launched, LaunchingState::Failed, L"partial", 10 },
                { LaunchingState::Launched, LaunchingState::Canceled, L"partial", 10 },
                { LaunchingState::LaunchedAndMoved, LaunchingState::Skipped, L"partial", 10 },
                { LaunchingState::LaunchedAndMoved, LaunchingState::LaunchedAndMoved, L"completed", 0 },
                { LaunchingState::Skipped, LaunchingState::Skipped, L"failed", 10 },
                { LaunchingState::Failed, LaunchingState::Canceled, L"failed", 10 },
                { LaunchingState::Failed, LaunchingState::Failed, L"failed", 10 },
            };
            auto project = Project();
            project.apps[1].id = L"{682A3A9E-D18D-4DC6-B2AF-E6ABD92F42A9}";
            for (const auto& test : cases)
            {
                LaunchingStatus states(project);
                states.Update(project.apps[0], test.first);
                states.Update(project.apps[1], test.second);
                int exitCode = -1;
                const auto result = WorkspacesCli::LaunchResult(project, states.Get(), L"operation", exitCode);
                Assert::AreEqual(std::wstring(test.expected), std::wstring(result.GetNamedString(L"state")));
                Assert::AreEqual(test.exitCode, exitCode);
                Assert::IsFalse(result.HasKey(L"error"));
                Assert::AreEqual(2u, result.GetNamedObject(L"result").GetNamedArray(L"applications").Size());
            }
        }

        TEST_METHOD (MixedArrangedAndUnconfirmedApplicationsRemainPartial)
        {
            auto project = Project();
            project.apps[1].id = L"{682A3A9E-D18D-4DC6-B2AF-E6ABD92F42A9}";
            LaunchingStatus states(project);
            states.Update(project.apps[0], LaunchingState::Launched);
            states.Update(project.apps[1], LaunchingState::LaunchedAndMoved);
            int exitCode = 0;
            const auto result = WorkspacesCli::LaunchResult(project, states.Get(), L"operation", exitCode);
            const auto apps = result.GetNamedObject(L"result").GetNamedArray(L"applications");
            Assert::AreEqual(std::wstring(L"launched"), std::wstring(apps.GetObjectAt(0).GetNamedString(L"state")));
            Assert::IsTrue(apps.GetObjectAt(0).HasKey(L"message"));
            Assert::AreEqual(std::wstring(L"arranged"), std::wstring(apps.GetObjectAt(1).GetNamedString(L"state")));
            Assert::IsFalse(apps.GetObjectAt(1).HasKey(L"message"));
            Assert::AreEqual(std::wstring(L"partial"), std::wstring(result.GetNamedString(L"state")));
            Assert::AreEqual(10, exitCode);
        }

        TEST_METHOD (TerminalFailureIsNotSuccessfulArrangement)
        {
            auto project = Project();
            project.apps.resize(1);
            LaunchingStatus states(project);
            states.Update(project.apps[0], LaunchingState::Failed);
            Assert::IsTrue(states.AllLaunchedAndMoved());
            int exitCode = 0;
            auto result = WorkspacesCli::LaunchResult(project, states.Get(), L"operation", exitCode);
            Assert::AreEqual(10, exitCode);
            Assert::AreEqual(5u, result.Size());
            Assert::AreEqual(1.0, result.GetNamedNumber(L"schemaVersion"));
            Assert::AreEqual(std::wstring(L"launch"), std::wstring(result.GetNamedString(L"command")));
            Assert::AreEqual(std::wstring(L"failed"), std::wstring(result.GetNamedString(L"state")));
            Assert::AreEqual(std::wstring(L"arrangeFailed"),
                             std::wstring(result.GetNamedObject(L"result").GetNamedArray(L"applications").GetObjectAt(0).GetNamedString(L"state")));
            result = WorkspacesCli::LaunchResult(project, states.Get(), L"operation", exitCode, { { project.apps[0].id, ERROR_FILE_NOT_FOUND } });
            Assert::AreEqual(std::wstring(L"launchFailed"),
                             std::wstring(result.GetNamedObject(L"result").GetNamedArray(L"applications").GetObjectAt(0).GetNamedString(L"state")));
            states.Update(project.apps[0], LaunchingState::LaunchedAndMoved);
            result = WorkspacesCli::LaunchResult(project, states.Get(), L"operation", exitCode);
            Assert::AreEqual(0, exitCode);
            Assert::AreEqual(std::wstring(L"completed"), std::wstring(result.GetNamedString(L"state")));
            Assert::AreEqual(0u, result.GetNamedArray(L"warnings").Size());
            Assert::AreEqual(std::wstring(L"unchanged"), std::wstring(result.GetNamedObject(L"result").GetNamedString(L"persistenceStatus")));
        }

        TEST_METHOD (StatusSnapshotDoesNotAliasLiveState)
        {
            auto project = Project();
            project.apps.resize(1);
            LaunchingStatus states(project);
            const auto snapshot = states.Get();
            states.Update(project.apps[0], LaunchingState::Failed);
            Assert::IsTrue(snapshot.begin()->second.state == LaunchingState::Waiting);
            Assert::IsTrue(states.Get().begin()->second.state == LaunchingState::Failed);
        }

        TEST_METHOD (BoundedMalformedArgumentCorpusIsRejectedWithoutSideEffects)
        {
            const std::wstring alphabet = L"{}-0123456789abcdefXYZ \t\u8bb0";
            for (size_t length = 0; length < 256; ++length)
            {
                std::wstring value;
                for (size_t i = 0; i < length; ++i)
                    value += alphabet[(i * 17 + length) % alphabet.size()];
                bool rejected = false;
                try
                {
                    WorkspacesCli::Parse({ L"launch", L"--id", value, L"--timeout", L"1" });
                }
                catch (const WorkspacesCli::Error& error)
                {
                    Assert::AreEqual(2, error.exitCode);
                    rejected = true;
                }
                Assert::IsTrue(rejected);
            }
        }
    };
}
