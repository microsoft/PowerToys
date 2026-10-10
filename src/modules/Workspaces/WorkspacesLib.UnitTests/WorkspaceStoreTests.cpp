// Copyright (c) Microsoft Corporation. Licensed under the MIT license.
#include "pch.h"
#include <WorkspacesLib/CliCommands.h>
#include <WorkspacesLib/WorkspaceStore.h>
#include <wil/resource.h>
#include <objbase.h>

using namespace Microsoft::VisualStudio::CppUnitTestFramework;

namespace WorkspacesLibUnitTests
{
    namespace
    {
        struct StoreFixture
        {
            std::filesystem::path directory;
            std::filesystem::path file;

            StoreFixture()
            {
                GUID guid{};
                Assert::IsTrue(SUCCEEDED(CoCreateGuid(&guid)));
                wchar_t value[40]{};
                StringFromGUID2(guid, value, ARRAYSIZE(value));
                directory = std::filesystem::temp_directory_path() / (std::wstring(L"WorkspacesStore-") + value);
                Assert::IsTrue(std::filesystem::create_directory(directory));
                file = directory / L"workspaces.json";
            }

            ~StoreFixture()
            {
                SetFileAttributesW(file.c_str(), FILE_ATTRIBUTE_NORMAL);
                std::error_code error;
                std::filesystem::remove_all(directory, error);
            }
        };

        const std::wstring Id = L"{6CF910A2-D2E0-436D-A50E-41432A88452A}";

        json::JsonObject Data()
        {
            return json::JsonObject::Parse(LR"({"workspaces":[{"id":"{6CF910A2-D2E0-436D-A50E-41432A88452A}","name":"Edited name","last-launched-time":10,"future":{"preserve":true},"applications":[]}],"extra":42})");
        }

        WorkspacesData::WorkspacesProject MetadataProject()
        {
            WorkspacesData::WorkspacesProject project{};
            project.id = Id;
            project.name = L"Original";
            WorkspacesData::WorkspacesProject::Application app{};
            app.id = L"{49330ECB-5917-4DF3-B70A-1F2D7FE4899B}";
            app.name = L"Packaged app";
            app.path = L"C:\\OldPackage\\app.exe";
            app.packageFullName = L"OldPackage";
            app.commandLineArgs = L"--original";
            app.position = { 10, 20, 640, 400 };
            app.monitor = 1;
            project.apps.push_back(app);
            return project;
        }

        WorkspacesData::WorkspacesProject Refreshed(WorkspacesData::WorkspacesProject project)
        {
            project.apps[0].path = L"C:\\NewPackage\\app.exe";
            project.apps[0].packageFullName = L"NewPackage";
            return project;
        }
    }

    TEST_CLASS (WorkspaceStoreTests)
    {
    public:
        TEST_METHOD (ApplicationMetadataRefreshPreservesConcurrentListAndConfigurationChanges)
        {
            StoreFixture fixture;
            const auto original = MetadataProject();
            const auto refreshed = Refreshed(original);
            auto deleted = original;
            deleted.id = L"{11111111-1111-1111-1111-111111111111}";
            Assert::IsTrue(WorkspaceStore::Write(fixture.file, WorkspacesData::WorkspacesListJSON::ToJson({ original, deleted })));

            auto edited = original;
            edited.name = L"Renamed while refreshing";
            edited.lastLaunchedTime = 500;
            edited.apps[0].commandLineArgs = L"--edited";
            edited.apps[0].position.x = 123;
            auto added = original;
            added.id = L"{22222222-2222-2222-2222-222222222222}";
            added.name = L"New workspace";
            auto fresh = WorkspacesData::WorkspacesListJSON::ToJson({ added, edited });
            fresh.SetNamedValue(L"futureRoot", json::value(true));
            auto app = fresh.GetNamedArray(L"workspaces").GetObjectAt(1).GetNamedArray(L"applications").GetObjectAt(0);
            app.SetNamedValue(L"futureApp", json::value(L"preserve"));
            Assert::IsTrue(WorkspaceStore::Write(fixture.file, fresh));
            auto expected = json::JsonObject::Parse(fresh.Stringify());
            auto expectedApp = expected.GetNamedArray(L"workspaces").GetObjectAt(1).GetNamedArray(L"applications").GetObjectAt(0);
            expectedApp.SetNamedValue(L"application-path", json::value(refreshed.apps[0].path));
            expectedApp.SetNamedValue(L"package-full-name", json::value(refreshed.apps[0].packageFullName));

            Assert::IsTrue(WorkspaceStore::UpdateApplicationMetadata(fixture.file, original, refreshed) == WorkspaceStore::UpdateResult::Updated);
            Assert::AreEqual(std::wstring(expected.Stringify()), std::wstring(WorkspacesCli::ReadJson(fixture.file)->Stringify()));
        }

        TEST_METHOD (ApplicationMetadataDoesNotRecreateDeletedWorkspaceOrApplication)
        {
            StoreFixture fixture;
            const auto original = MetadataProject();
            const auto refreshed = Refreshed(original);
            for (const bool deleteWorkspace : { false, true })
            {
                auto deleted = original;
                deleted.apps.clear();
                const auto current = WorkspacesData::WorkspacesListJSON::ToJson(
                    deleteWorkspace ? std::vector<WorkspacesData::WorkspacesProject>{} : std::vector{ deleted });
                Assert::IsTrue(WorkspaceStore::Write(fixture.file, current));
                Assert::IsTrue(WorkspaceStore::UpdateApplicationMetadata(fixture.file, original, refreshed) == WorkspaceStore::UpdateResult::Conflict);
                Assert::AreEqual(std::wstring(current.Stringify()), std::wstring(WorkspacesCli::ReadJson(fixture.file)->Stringify()));
            }
        }

        TEST_METHOD (ApplicationMetadataDoesNotOverwriteConcurrentTargetChanges)
        {
            StoreFixture fixture;
            const auto original = MetadataProject();
            auto current = original;
            current.apps[0].path = L"C:\\UserSelected\\different.exe";
            const auto data = WorkspacesData::WorkspacesListJSON::ToJson({ current });
            Assert::IsTrue(WorkspaceStore::Write(fixture.file, data));
            Assert::IsTrue(WorkspaceStore::UpdateApplicationMetadata(fixture.file, original, Refreshed(original)) == WorkspaceStore::UpdateResult::Conflict);
            Assert::AreEqual(std::wstring(data.Stringify()), std::wstring(WorkspacesCli::ReadJson(fixture.file)->Stringify()));
        }

        TEST_METHOD (LegacyApplicationIdRefreshMatchesUniqueOriginalAfterReordering)
        {
            StoreFixture fixture;
            auto original = MetadataProject();
            original.apps[0].id.clear();
            auto refreshed = Refreshed(original);
            refreshed.apps[0].id = L"{49330ECB-5917-4DF3-B70A-1F2D7FE4899B}";
            auto current = original;
            auto added = MetadataProject().apps[0];
            added.id = L"{33333333-3333-3333-3333-333333333333}";
            current.apps.insert(current.apps.begin(), added);
            auto data = WorkspacesData::WorkspacesListJSON::ToJson({ current });
            data.GetNamedArray(L"workspaces").GetObjectAt(0).GetNamedArray(L"applications").GetObjectAt(1).Remove(L"id");
            Assert::IsTrue(WorkspaceStore::Write(fixture.file, data));
            Assert::IsTrue(WorkspaceStore::UpdateApplicationMetadata(fixture.file, original, refreshed) == WorkspaceStore::UpdateResult::Updated);
            const auto apps = WorkspacesCli::ReadJson(fixture.file)->GetNamedArray(L"workspaces").GetObjectAt(0).GetNamedArray(L"applications");
            Assert::AreEqual(2u, apps.Size());
            Assert::AreEqual(added.id, std::wstring(apps.GetObjectAt(0).GetNamedString(L"id")));
            Assert::AreEqual(refreshed.apps[0].id, std::wstring(apps.GetObjectAt(1).GetNamedString(L"id")));
            Assert::AreEqual(refreshed.apps[0].path, std::wstring(apps.GetObjectAt(1).GetNamedString(L"application-path")));
        }

        TEST_METHOD (AmbiguousLegacyApplicationMetadataFailsWithoutWriting)
        {
            StoreFixture fixture;
            auto original = MetadataProject();
            original.apps[0].id.clear();
            auto refreshed = Refreshed(original);
            refreshed.apps[0].id = L"{49330ECB-5917-4DF3-B70A-1F2D7FE4899B}";
            auto current = original;
            current.apps.push_back(current.apps[0]);
            const auto data = WorkspacesData::WorkspacesListJSON::ToJson({ current });
            Assert::IsTrue(WorkspaceStore::Write(fixture.file, data));
            Assert::IsTrue(WorkspaceStore::UpdateApplicationMetadata(fixture.file, original, refreshed) == WorkspaceStore::UpdateResult::Conflict);
            Assert::AreEqual(std::wstring(data.Stringify()), std::wstring(WorkspacesCli::ReadJson(fixture.file)->Stringify()));
        }

        TEST_METHOD (ApplicationMetadataRefreshRejectsUnrelatedEditsAndReadOnlyStore)
        {
            StoreFixture fixture;
            const auto original = MetadataProject();
            const auto data = WorkspacesData::WorkspacesListJSON::ToJson({ original });
            Assert::IsTrue(WorkspaceStore::Write(fixture.file, data));
            auto refreshed = Refreshed(original);
            refreshed.apps[0].commandLineArgs = L"--must-not-be-written";
            Assert::IsTrue(WorkspaceStore::UpdateApplicationMetadata(fixture.file, original, refreshed) == WorkspaceStore::UpdateResult::Conflict);
            Assert::IsTrue(SetFileAttributesW(fixture.file.c_str(), FILE_ATTRIBUTE_READONLY) != FALSE);
            Assert::IsTrue(WorkspaceStore::UpdateApplicationMetadata(fixture.file, original, Refreshed(original)) == WorkspaceStore::UpdateResult::Failed);
            Assert::AreEqual(std::wstring(data.Stringify()), std::wstring(WorkspacesCli::ReadJson(fixture.file)->Stringify()));
        }

        TEST_METHOD (ApplicationMetadataConflictDoesNotCommitEarlierApplicationChanges)
        {
            StoreFixture fixture;
            auto original = MetadataProject();
            auto second = original.apps[0];
            second.id = L"{33333333-3333-3333-3333-333333333333}";
            original.apps.push_back(second);
            auto refreshed = Refreshed(original);
            refreshed.apps[1].packageFullName = L"NewPackage";
            refreshed.apps[1].path = L"C:\\NewPackage\\app.exe";
            auto current = original;
            current.apps[1].path = L"C:\\UserEdited\\app.exe";
            const auto data = WorkspacesData::WorkspacesListJSON::ToJson({ current });
            Assert::IsTrue(WorkspaceStore::Write(fixture.file, data));
            Assert::IsTrue(WorkspaceStore::UpdateApplicationMetadata(fixture.file, original, refreshed) == WorkspaceStore::UpdateResult::Conflict);
            Assert::AreEqual(std::wstring(data.Stringify()), std::wstring(WorkspacesCli::ReadJson(fixture.file)->Stringify()));
        }

        TEST_METHOD (ApplicationMetadataRefreshHonorsWriterLock)
        {
            StoreFixture fixture;
            const auto original = MetadataProject();
            const auto data = WorkspacesData::WorkspacesListJSON::ToJson({ original });
            Assert::IsTrue(WorkspaceStore::Write(fixture.file, data));
            wil::unique_handle lock(CreateFileW((fixture.file.wstring() + L".lock").c_str(),
                                                GENERIC_READ | GENERIC_WRITE, 0, nullptr, OPEN_EXISTING, 0, nullptr));
            Assert::IsTrue(static_cast<bool>(lock));
            const auto start = GetTickCount64();
            Assert::IsTrue(WorkspaceStore::UpdateApplicationMetadata(fixture.file, original, Refreshed(original)) == WorkspaceStore::UpdateResult::Failed);
            Assert::IsTrue(GetTickCount64() - start < 5000);
            Assert::AreEqual(std::wstring(data.Stringify()), std::wstring(WorkspacesCli::ReadJson(fixture.file)->Stringify()));
        }

        TEST_METHOD (HistoryUpdatePreservesFreshConfigurationAndUnknownFields)
        {
            StoreFixture fixture;
            Assert::IsTrue(WorkspaceStore::Write(fixture.file, Data()));
            Assert::IsTrue(WorkspaceStore::UpdateLastLaunched(fixture.file, Id, 100) == WorkspaceStore::UpdateResult::Updated);
            const auto saved = *WorkspacesCli::ReadJson(fixture.file);
            const auto workspace = saved.GetNamedArray(L"workspaces").GetObjectAt(0);
            Assert::AreEqual(42.0, saved.GetNamedNumber(L"extra"));
            Assert::AreEqual(std::wstring(L"Edited name"), std::wstring(workspace.GetNamedString(L"name")));
            Assert::IsTrue(workspace.GetNamedObject(L"future").GetNamedBoolean(L"preserve"));
            Assert::AreEqual(100.0, workspace.GetNamedNumber(L"last-launched-time"));
        }

        TEST_METHOD (CachedWriterDoesNotOverwriteNewLaunchHistory)
        {
            StoreFixture fixture;
            auto cached = Data();
            Assert::IsTrue(WorkspaceStore::Write(fixture.file, cached));
            Assert::IsTrue(WorkspaceStore::UpdateLastLaunched(fixture.file, Id, 100) == WorkspaceStore::UpdateResult::Updated);
            cached.GetNamedArray(L"workspaces").GetObjectAt(0).SetNamedValue(L"name", json::value(L"New editor name"));
            Assert::IsTrue(WorkspaceStore::Write(fixture.file, cached));
            const auto item = WorkspacesCli::ReadJson(fixture.file)->GetNamedArray(L"workspaces").GetObjectAt(0);
            Assert::AreEqual(100.0, item.GetNamedNumber(L"last-launched-time"));
            Assert::AreEqual(std::wstring(L"New editor name"), std::wstring(item.GetNamedString(L"name")));
        }

        TEST_METHOD (DeletedWorkspaceIsNotRecreated)
        {
            StoreFixture fixture;
            Assert::IsTrue(WorkspaceStore::Write(fixture.file, json::JsonObject::Parse(LR"({"workspaces":[]})")));
            Assert::IsTrue(WorkspaceStore::UpdateLastLaunched(fixture.file, Id, 100) == WorkspaceStore::UpdateResult::Conflict);
            Assert::AreEqual(0u, WorkspacesCli::ReadJson(fixture.file)->GetNamedArray(L"workspaces").Size());
        }

        TEST_METHOD (ReadOnlyDestinationReportsFailureAndKeepsOldData)
        {
            StoreFixture fixture;
            Assert::IsTrue(WorkspaceStore::Write(fixture.file, Data()));
            Assert::IsTrue(SetFileAttributesW(fixture.file.c_str(), FILE_ATTRIBUTE_READONLY) != FALSE);
            Assert::IsTrue(WorkspaceStore::UpdateLastLaunched(fixture.file, Id, 100) == WorkspaceStore::UpdateResult::Failed);
            Assert::AreEqual(10.0, WorkspacesCli::ReadJson(fixture.file)->GetNamedArray(L"workspaces").GetObjectAt(0).GetNamedNumber(L"last-launched-time"));
        }

        TEST_METHOD (CooperatingWriterLockIsBounded)
        {
            StoreFixture fixture;
            Assert::IsTrue(WorkspaceStore::Write(fixture.file, Data()));
            wil::unique_handle lock(CreateFileW((fixture.file.wstring() + L".lock").c_str(),
                                                GENERIC_READ | GENERIC_WRITE,
                                                0,
                                                nullptr,
                                                OPEN_EXISTING,
                                                0,
                                                nullptr));
            Assert::IsTrue(static_cast<bool>(lock));
            const auto start = GetTickCount64();
            Assert::IsTrue(WorkspaceStore::UpdateLastLaunched(fixture.file, Id, 100) == WorkspaceStore::UpdateResult::Failed);
            Assert::IsTrue(GetTickCount64() - start < 5000);
        }
    };
}
