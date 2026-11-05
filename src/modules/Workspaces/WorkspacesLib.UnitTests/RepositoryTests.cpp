#include "pch.h"
#include <WorkspacesLib/WorkspacesRepository.h>
#include <WorkspacesSnapshotTool/SnapshotUtils.h>
#include <fstream>
#include <filesystem>
#include <random>

using namespace Microsoft::VisualStudio::CppUnitTestFramework;

namespace WorkspacesLibUnitTests
{
    TEST_CLASS(RepositoryTests)
    {
        static WorkspacesData::WorkspacesProject Project()
        {
            WorkspacesData::WorkspacesProject project;
            project.id = L"{00112233-4455-6677-8899-AABBCCDDEEFF}";
            project.name = L"Workspace";
            project.creationTime = 200;
            return project;
        }

    public:
        TEST_METHOD(WorkspaceIdentityAcceptsCanonicalCaseAndBraceVariants)
        {
            Assert::IsTrue(Workspaces::Repository::SameIdentity(L"{00112233-4455-6677-8899-AABBCCDDEEFF}", L"00112233-4455-6677-8899-aabbccddeeff"));
            Assert::ExpectException<PowerToys::ProtectedStorage::StorageError>([] {
                Workspaces::Repository::SameIdentity(L"Word.Application", L"{00112233-4455-6677-8899-AABBCCDDEEFF}");
            });
        }

        TEST_METHOD(SharedNativeManagedValidationVectorsAgree)
        {
            wchar_t module[MAX_PATH]{};
            GetModuleFileNameW(GetModuleHandleW(L"Workspaces.Lib.UnitTests.dll"), module, MAX_PATH);
            std::ifstream input(std::filesystem::path(module).parent_path() / L"StorageValidationVectors.json", std::ios::binary);
            Assert::IsTrue(input.is_open());
            const std::string bytes((std::istreambuf_iterator<char>(input)), std::istreambuf_iterator<char>());
            const auto vectors = json::JsonArray::Parse(PowerToys::ProtectedStorage::Wide(bytes));
            for (const auto& value : vectors)
            {
                const auto vector = value.GetObjectW();
                const auto text = PowerToys::ProtectedStorage::Utf8(vector.GetNamedString(L"document").c_str());
                bool accepted = true;
                try
                {
                    Workspaces::Repository::ParseDocument({text.begin(), text.end()});
                }
                catch (...) { accepted = false; }
                Assert::AreEqual(vector.GetNamedBoolean(L"valid"), accepted);
            }
        }

        TEST_METHOD(CodecRoundTripsValidatedPreview)
        {
            const auto project = Project();
            const auto bytes = Workspaces::Repository::SerializeProject(project);
            const auto parsed = Workspaces::Repository::ParseProject(bytes);
            Assert::AreEqual(project.id, parsed.id);
            Assert::AreEqual(project.name, parsed.name);
        }

        TEST_METHOD (CaptureAssignsUniqueIdsWithoutDroppingWindows)
        {
            auto project = Project();
            WorkspacesData::WorkspacesProject::Monitor monitor{};
            monitor.id = L"monitor";
            monitor.number = 1;
            monitor.dpi = 96;
            monitor.monitorRectDpiAware = { 0, 0, 1920, 1080 };
            monitor.monitorRectDpiUnaware = monitor.monitorRectDpiAware;
            project.monitors.push_back(monitor);
            WorkspacesData::WorkspacesProject::Application app{};
            app.id = L"{E6301185-2051-4BF7-8DE2-1AD014E379E3}";
            app.name = L"File Explorer";
            app.path = L"C:\\Windows\\explorer.exe";
            app.position = { 0, 0, 600, 400 };
            app.monitor = 1;
            project.apps = { app, app, app };
            project.apps[1].id = L"{e6301185-2051-4bf7-8de2-1ad014e379e3}";
            project.apps[1].position.x = 700;
            project.apps[2].id.clear();
            Assert::ExpectException<PowerToys::ProtectedStorage::StorageError>([&] {
                Workspaces::Repository::SerializeProject(project);
            });

            SnapshotUtils::AssignUniqueApplicationIds(project.apps);

            Assert::AreEqual(size_t{ 3 }, project.apps.size());
            Assert::AreEqual(app.id, project.apps[0].id);
            Assert::IsFalse(Workspaces::Repository::SameIdentity(project.apps[0].id, project.apps[1].id));
            Assert::IsFalse(Workspaces::Repository::SameIdentity(project.apps[1].id, project.apps[2].id));
            Assert::AreEqual(700, project.apps[1].position.x);
            const auto parsed = Workspaces::Repository::ParseProject(Workspaces::Repository::SerializeProject(project));
            Assert::AreEqual(size_t{ 3 }, parsed.apps.size());

            const auto normalized = project.apps;
            SnapshotUtils::AssignUniqueApplicationIds(project.apps);
            for (size_t i = 0; i < project.apps.size(); ++i)
                Assert::AreEqual(normalized[i].id, project.apps[i].id);
        }

        TEST_METHOD(MalformedAndTruncatedWorkspaceCorpusIsRejected)
        {
            const auto valid = Workspaces::Repository::SerializeProject(Project());
            for (size_t count = 0; count < valid.size(); ++count)
            {
                bool accepted = false;
                try
                {
                    Workspaces::Repository::ParseProject({valid.begin(), valid.begin() + count});
                    accepted = true;
                }
                catch (...) {}
                Assert::IsFalse(accepted);
            }
            std::mt19937 random(34901);
            for (unsigned iteration = 0; iteration < 1000; ++iteration)
            {
                std::vector<BYTE> bytes(random() % 512);
                for (auto& value : bytes) value = static_cast<BYTE>(random());
                bool accepted = false;
                try
                {
                    Workspaces::Repository::ParseDocument(bytes);
                    accepted = true;
                }
                catch (...) {}
                Assert::IsFalse(accepted);
            }
        }

        TEST_METHOD(RejectsDuplicateWorkspaceIdentity)
        {
            const auto project = Project();
            Assert::ExpectException<PowerToys::ProtectedStorage::StorageError>([&] {
                Workspaces::Repository::Validate({project, project});
            });
        }

        TEST_METHOD(RejectsMalformedLaunchDescriptors)
        {
            auto project = Project();
            WorkspacesData::WorkspacesProject::Application app{};
            app.id = L"{11223344-5566-7788-99AA-BBCCDDEEFF00}";
            app.name = L"App";
            app.path = L"relative.exe";
            app.position = {0, 0, 100, 100};
            project.apps.push_back(app);
            Assert::ExpectException<PowerToys::ProtectedStorage::StorageError>([&] {
                Workspaces::Repository::Validate({project});
            });
        }

        TEST_METHOD(LaunchTimestampMergePreservesUserEditsAndOtherWorkspaces)
        {
            auto original = Project();
            auto current = original;
            current.name = L"Renamed by the editor";
            current.lastLaunchedTime = 500;
            auto other = Project();
            other.id = L"{11223344-5566-7788-99AA-BBCCDDEEFF00}";
            std::vector<WorkspacesData::WorkspacesProject> latest{current, other};
            Workspaces::Repository::MergeLaunchMetadata(latest, original, original, 400);
            Assert::AreEqual(current.name, latest[0].name);
            Assert::AreEqual(static_cast<int64_t>(500), static_cast<int64_t>(*latest[0].lastLaunchedTime));
            Assert::AreEqual(other.id, latest[1].id);
        }

        TEST_METHOD(LaunchMetadataMergeDoesNotResurrectDeletedWorkspace)
        {
            const auto original = Project();
            std::vector<WorkspacesData::WorkspacesProject> latest;
            Assert::ExpectException<PowerToys::ProtectedStorage::StorageError>([&] {
                Workspaces::Repository::MergeLaunchMetadata(latest, original, original, 400);
            });
        }

        TEST_METHOD(LaunchMetadataMergeRejectsConcurrentApplicationEdit)
        {
            auto original = Project();
            WorkspacesData::WorkspacesProject::Application app{};
            app.id = L"{11223344-5566-7788-99AA-BBCCDDEEFF00}";
            app.path = L"C:\\old\\app.exe";
            original.apps.push_back(app);
            auto updated = original;
            updated.apps[0].path = L"C:\\new\\app.exe";
            auto current = original;
            current.apps[0].commandLineArgs = L"new user arguments";
            std::vector<WorkspacesData::WorkspacesProject> latest{current};
            Assert::ExpectException<PowerToys::ProtectedStorage::StorageError>([&] {
                Workspaces::Repository::MergeLaunchMetadata(latest, original, updated, std::nullopt);
            });
            Assert::AreEqual(current.apps[0].commandLineArgs, latest[0].apps[0].commandLineArgs);
        }
    };
}
