// Copyright (c) Microsoft Corporation. Licensed under the MIT license.
#include "pch.h"
#include <WorkspacesLib/CliCommands.h>
#include <wil/resource.h>
#include <algorithm>
#include <WorkspacesCLI/ApprovalChannel.h>
#include <WorkspacesCLI/ConsoleApproval.h>

using namespace Microsoft::VisualStudio::CppUnitTestFramework;
extern "C" IMAGE_DOS_HEADER __ImageBase;

namespace WorkspacesLibUnitTests
{
    TEST_CLASS (CliWorkerTests)
    {
        static void RequireOptIn()
        {
            wchar_t value[2]{};
            if (!GetEnvironmentVariableW(L"WORKSPACES_CLI_LIVE_TESTS", value, ARRAYSIZE(value)) || value[0] != L'1')
                Assert::Fail(L"Set WORKSPACES_CLI_LIVE_TESTS=1 for this explicitly built live-test slice.");
            WorkspacesCli::CheckEnabled();
        }

        static void CloseFixture()
        {
            if (const auto window = FindWindowW(L"WorkspacesCliIsolatedFixture", nullptr))
            {
                DWORD_PTR result{};
                SendMessageTimeoutW(window, WM_CLOSE, 0, 0, SMTO_ABORTIFHUNG, 2000, &result);
            }
        }

        static std::pair<DWORD, json::JsonObject> Run(bool fixture, bool canceled, bool cancelAfterStart = false, bool expired = false, ULONGLONG* cancellationLatency = nullptr)
        {
            RequireOptIn();
            wchar_t module[MAX_PATH]{};
            Assert::IsTrue(GetModuleFileNameW(reinterpret_cast<HMODULE>(&__ImageBase), module, ARRAYSIZE(module)) != 0);
            auto root = std::filesystem::path(module).parent_path().parent_path().parent_path();
            auto executable = root / L"PowerToys.WorkspacesCLI.exe";
            Assert::IsTrue(std::filesystem::is_regular_file(executable));
            WorkspacesData::WorkspacesProject project{};
            project.id = L"{6CF910A2-D2E0-436D-A50E-41432A88452A}";
            project.name = L"Isolated CLI transport test";
            project.monitors.push_back({ .number = 1, .dpi = 96, .monitorRectDpiAware = { 0, 0, 1920, 1080 }, .monitorRectDpiUnaware = { 0, 0, 1920, 1080 } });
            WorkspacesData::WorkspacesProject::Application app{};
            app.id = L"{49330ECB-5917-4DF3-B70A-1F2D7FE4899B}";
            app.name = L"WorkspacesCliWindowFixture";
            app.path = (root / L"tests" / L"Workspaces" /
                        (fixture ? L"WorkspacesCliWindowFixture.exe" : L"WorkspacesCliNonexistent-49330ECB.exe"))
                           .wstring();
            app.position = { 120, 120, 640, 400 };
            app.monitor = 1;
            project.apps.push_back(app);
            GUID guid{};
            CoCreateGuid(&guid);
            wchar_t operation[40]{};
            StringFromGUID2(guid, operation, ARRAYSIZE(operation));
            project.id = operation;
            const auto startedName = std::wstring(L"Local\\WorkspacesCliFixtureStarted-") + operation;
            wil::unique_handle started(CreateEventW(nullptr, TRUE, FALSE, startedName.c_str()));
            Assert::IsTrue(static_cast<bool>(started));
            if (cancelAfterStart)
                project.apps[0].commandLineArgs = L"--delay-window " + startedName;
            json::JsonObject request;
            request.SetNamedValue(L"protocolVersion", json::value(1));
            request.SetNamedValue(L"operationId", json::value(operation));
            request.SetNamedValue(L"timeout", json::value(20));
            request.SetNamedValue(L"deadline", json::value(expired ? GetTickCount64() - 1 : GetTickCount64() + 20000));
            request.SetNamedValue(L"workspace", WorkspacesData::WorkspacesProjectJSON::ToJson(project));
            const auto bytes = winrt::to_string(request.Stringify());
            SECURITY_ATTRIBUTES sa{ sizeof(sa), nullptr, TRUE };
            wil::unique_handle mapping(CreateFileMappingW(INVALID_HANDLE_VALUE, &sa, PAGE_READWRITE, 0, static_cast<DWORD>(bytes.size() + sizeof(DWORD)), nullptr));
            wil::unique_handle cancel(CreateEventW(&sa, TRUE, canceled, nullptr));
            Assert::IsTrue(mapping && cancel);
            wil::unique_mapview_ptr<char> view(static_cast<char*>(MapViewOfFile(mapping.get(), FILE_MAP_WRITE, 0, 0, 0)));
            Assert::IsTrue(static_cast<bool>(view));
            *reinterpret_cast<DWORD*>(view.get()) = static_cast<DWORD>(bytes.size());
            memcpy(view.get() + sizeof(DWORD), bytes.data(), bytes.size());
            wil::unique_handle read, write;
            Assert::IsTrue(CreatePipe(read.put(), write.put(), &sa, 0) != FALSE);
            Assert::IsTrue(SetHandleInformation(read.get(), HANDLE_FLAG_INHERIT, 0) != FALSE);
            auto approvalRequests = WorkspacesCli::ApprovalPipe::Create();
            auto approvalReplies = WorkspacesCli::ApprovalPipe::Create();
            Assert::IsTrue(SetHandleInformation(approvalRequests.write.get(), HANDLE_FLAG_INHERIT, HANDLE_FLAG_INHERIT) != FALSE);
            Assert::IsTrue(SetHandleInformation(approvalReplies.read.get(), HANDLE_FLAG_INHERIT, HANDLE_FLAG_INHERIT) != FALSE);
            wil::unique_handle input(CreateFileW(L"NUL", GENERIC_READ, FILE_SHARE_READ | FILE_SHARE_WRITE, &sa, OPEN_EXISTING, 0, nullptr));
            STARTUPINFOW startup{ sizeof(startup) };
            startup.dwFlags = STARTF_USESTDHANDLES;
            startup.hStdInput = input.get();
            startup.hStdOutput = write.get();
            startup.hStdError = write.get();
            std::wstring command = L"\"" + executable.wstring() + L"\" --launch-worker " +
                                   std::to_wstring(reinterpret_cast<uintptr_t>(mapping.get())) + L" " +
                                   std::to_wstring(reinterpret_cast<uintptr_t>(cancel.get())) + L" " + std::to_wstring(GetCurrentProcessId()) + L" " +
                                   std::to_wstring(reinterpret_cast<uintptr_t>(approvalRequests.write.get())) + L" " +
                                   std::to_wstring(reinterpret_cast<uintptr_t>(approvalReplies.read.get()));
            wil::unique_process_information process;
            Assert::IsTrue(CreateProcessW(executable.c_str(), command.data(), nullptr, nullptr, TRUE, CREATE_NO_WINDOW, nullptr, root.c_str(), &startup, &process) != FALSE);
            write.reset();
            approvalRequests.write.reset();
            approvalReplies.read.reset();
            auto cleanup = wil::scope_exit([&] {
                if (WaitForSingleObject(process.hProcess, 0) == WAIT_TIMEOUT)
                {
                    TerminateProcess(process.hProcess, 9);
                    WaitForSingleObject(process.hProcess, 2000);
                }
            });
            std::string output;
            WorkspacesCli::ConsoleApproval console(input.get(), nullptr, {});
            WorkspacesCli::FrontendApproval presenter(approvalRequests.read.get(), approvalReplies.write.get(), console, cancel.get());
            ULONGLONG cancellationAt = 0;
            const auto deadline = GetTickCount64() + 25000;
            while (GetTickCount64() < deadline)
            {
                presenter.Poll();
                if (cancelAfterStart && !cancellationAt && WaitForSingleObject(started.get(), 0) == WAIT_OBJECT_0)
                {
                    cancellationAt = GetTickCount64();
                    SetEvent(cancel.get());
                }
                DWORD available{};
                if (PeekNamedPipe(read.get(), nullptr, 0, nullptr, &available, nullptr) && available)
                {
                    char buffer[4096];
                    DWORD count{};
                    Assert::IsTrue(ReadFile(read.get(), buffer, (std::min)(available, DWORD{ sizeof(buffer) }), &count, nullptr) != FALSE);
                    output.append(buffer, count);
                    continue;
                }
                if (WaitForSingleObject(process.hProcess, 20) == WAIT_OBJECT_0)
                {
                    // Drain the final bytes before parsing.
                    if (PeekNamedPipe(read.get(), nullptr, 0, nullptr, &available, nullptr) && available)
                        continue;
                    break;
                }
            }
            Assert::AreEqual(DWORD{ WAIT_OBJECT_0 }, WaitForSingleObject(process.hProcess, 0));
            DWORD code{};
            GetExitCodeProcess(process.hProcess, &code);
            Microsoft::VisualStudio::CppUnitTestFramework::Logger::WriteMessage(output.c_str());
            Assert::IsFalse(output.empty());
            if (cancelAfterStart)
                Assert::IsTrue(cancellationAt != 0, L"Fixture must signal startup before requesting cancellation.");
            if (cancellationLatency)
                *cancellationLatency = GetTickCount64() - cancellationAt;
            return { code, json::JsonObject::Parse(winrt::to_hstring(output)) };
        }

    public:
#ifdef _DEBUG
        TEST_METHOD (MissingApplicationProducesRealNonzeroResult)
        {
            const auto [code, result] = Run(false, false);
            Assert::AreEqual(DWORD{ 10 }, code);
            Assert::AreEqual(std::wstring(L"failed"), std::wstring(result.GetNamedString(L"state")));
            const auto state = result.GetNamedObject(L"result").GetNamedArray(L"applications").GetObjectAt(0);
            Assert::AreEqual(std::wstring(L"launchFailed"), std::wstring(state.GetNamedString(L"state")));
        }

        TEST_METHOD (PreCancelledRequestStartsNoApplications)
        {
            const auto [code, result] = Run(false, true);
            Assert::AreEqual(DWORD{ 12 }, code);
            Assert::AreEqual(std::wstring(L"canceled"), std::wstring(result.GetNamedObject(L"error").GetNamedString(L"code")));
        }

        TEST_METHOD (ControlledWindowIsArrangedWithoutWorkspacesUi)
        {
            RequireOptIn();
            if (FindWindowW(L"WorkspacesCliIsolatedFixture", nullptr))
                Assert::Fail(L"A fixture window already exists; do not touch another test's window.");
            auto cleanup = wil::scope_exit([] { CloseFixture(); });
            const auto [code, result] = Run(true, false);
            Assert::AreEqual(DWORD{ 0 }, code);
            Assert::AreEqual(std::wstring(L"completed"), std::wstring(result.GetNamedString(L"state")));
            Assert::AreEqual(std::wstring(L"conflict"), std::wstring(result.GetNamedObject(L"result").GetNamedString(L"persistenceStatus")));
            Assert::AreEqual(std::wstring(L"metadataSaveConflict"),
                             std::wstring(result.GetNamedArray(L"warnings").GetObjectAt(0).GetNamedString(L"code")));
            const auto window = FindWindowW(L"WorkspacesCliIsolatedFixture", nullptr);
            Assert::IsNotNull(window);
            Assert::IsTrue(GetPropW(window, L"PowerToys_LaunchedByWorkspaces") == reinterpret_cast<HANDLE>(1));
            RECT rect{};
            Assert::IsTrue(GetWindowRect(window, &rect) != FALSE);
            Assert::IsTrue(rect.right > rect.left && rect.bottom > rect.top);
        }

        TEST_METHOD (CancellationDoesNotKillAlreadyLaunchedApplication)
        {
            RequireOptIn();
            Assert::IsNull(FindWindowW(L"WorkspacesCliIsolatedFixture", nullptr));
            auto cleanup = wil::scope_exit([] { CloseFixture(); });
            ULONGLONG latency = 0;
            const auto [code, result] = Run(true, false, true, false, &latency);
            Assert::AreEqual(DWORD{ 12 }, code);
            Assert::AreEqual(std::wstring(L"canceled"), std::wstring(result.GetNamedObject(L"error").GetNamedString(L"code")));
            Assert::IsTrue(latency < 5000);
            const auto deadline = GetTickCount64() + 5000;
            HWND window = nullptr;
            while (!(window = FindWindowW(L"WorkspacesCliIsolatedFixture", nullptr)) && GetTickCount64() < deadline)
                Sleep(20);
            Assert::IsNotNull(window);
        }

        TEST_METHOD (ExpiredDeadlineStartsNoApplications)
        {
            const auto [code, result] = Run(false, false, false, true);
            Assert::AreEqual(DWORD{ 9 }, code);
            Assert::AreEqual(std::wstring(L"timeout"), std::wstring(result.GetNamedObject(L"error").GetNamedString(L"code")));
        }
#else
        TEST_METHOD (UnsignedReleasePeersAreRejected)
        {
            const auto [code, result] = Run(false, false);
            Assert::AreEqual(DWORD{ 9 }, code);
            Assert::AreEqual(std::wstring(L"outcomeUnknown"),
                             std::wstring(result.GetNamedObject(L"error").GetNamedString(L"code")));
        }
#endif
    };
}
