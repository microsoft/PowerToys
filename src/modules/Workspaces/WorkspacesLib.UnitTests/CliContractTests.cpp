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

    public:
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
