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
    }

    TEST_CLASS (WorkspaceStoreTests)
    {
    public:
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
