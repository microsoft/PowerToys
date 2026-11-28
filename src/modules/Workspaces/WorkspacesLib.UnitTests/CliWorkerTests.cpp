// Copyright (c) Microsoft Corporation. Licensed under the MIT license.
#include "pch.h"
#include <WorkspacesLib/CliCommands.h>
#include <wil/resource.h>
#include <algorithm>
#include <tlhelp32.h>
#include <UIAutomation.h>
#include <WorkspacesLib/LaunchSession.h>
#include <workspaces-common/MonitorUtils.h>

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

        static void CancelGuiLaunch(DWORD launcherPid)
        {
            wil::unique_handle snapshot(CreateToolhelp32Snapshot(TH32CS_SNAPPROCESS, 0));
            Assert::IsTrue(snapshot.get() != INVALID_HANDLE_VALUE);
            PROCESSENTRY32W entry{ sizeof(entry) };
            DWORD uiPid{};
            for (BOOL found = Process32FirstW(snapshot.get(), &entry); found; found = Process32NextW(snapshot.get(), &entry))
            {
                if (entry.th32ParentProcessID == launcherPid && _wcsicmp(entry.szExeFile, L"PowerToys.WorkspacesLauncherUI.exe") == 0)
                {
                    uiPid = entry.th32ProcessID;
                    break;
                }
            }
            Assert::IsTrue(uiPid != 0, L"The progress UI must belong to this session host.");
            winrt::com_ptr<IUIAutomation> automation;
            winrt::check_hresult(CoCreateInstance(__uuidof(CUIAutomation), nullptr, CLSCTX_INPROC_SERVER, IID_PPV_ARGS(automation.put())));
            winrt::com_ptr<IUIAutomationElement> desktop, window, button;
            winrt::check_hresult(automation->GetRootElement(desktop.put()));
            VARIANT processId{};
            processId.vt = VT_I4;
            processId.lVal = static_cast<LONG>(uiPid);
            winrt::com_ptr<IUIAutomationCondition> processCondition, buttonCondition;
            winrt::check_hresult(automation->CreatePropertyCondition(UIA_ProcessIdPropertyId, processId, processCondition.put()));
            winrt::check_hresult(desktop->FindFirst(TreeScope_Children, processCondition.get(), window.put()));
            Assert::IsNotNull(window.get(), L"The owned progress window must be visible.");
            wil::unique_bstr cancelId(SysAllocString(L"CancelButton"));
            Assert::IsNotNull(cancelId.get());
            VARIANT automationId{};
            automationId.vt = VT_BSTR;
            automationId.bstrVal = cancelId.get();
            winrt::check_hresult(automation->CreatePropertyCondition(UIA_AutomationIdPropertyId, automationId, buttonCondition.put()));
            winrt::check_hresult(window->FindFirst(TreeScope_Descendants, buttonCondition.get(), button.put()));
            Assert::IsNotNull(button.get(), L"The real progress Cancel button must be available.");
            winrt::com_ptr<IUIAutomationInvokePattern> invoke;
            winrt::check_hresult(button->GetCurrentPatternAs(UIA_InvokePatternId, IID_PPV_ARGS(invoke.put())));
            winrt::check_hresult(invoke->Invoke());
        }

        static void CheckCompletion(const std::wstring& mode, bool cancelRequested, const wchar_t* expectedCode)
        {
            wchar_t module[32768]{};
            Assert::IsTrue(GetModuleFileNameW(reinterpret_cast<HMODULE>(&__ImageBase), module, ARRAYSIZE(module)) != 0);
            const auto executable = std::filesystem::path(module).parent_path() / L"WorkspacesCliRuntimeFixture.exe";
            SECURITY_ATTRIBUTES attributes{ sizeof(attributes), nullptr, TRUE };
            wil::unique_handle read, write;
            Assert::IsTrue(CreatePipe(read.put(), write.put(), &attributes, 4096) != FALSE);
            Assert::IsTrue(SetHandleInformation(read.get(), HANDLE_FLAG_INHERIT, 0) != FALSE);
            wil::unique_handle input(CreateFileW(L"NUL", GENERIC_READ, FILE_SHARE_READ | FILE_SHARE_WRITE,
                                                 &attributes, OPEN_EXISTING, 0, nullptr));
            wil::unique_handle cancel(CreateEventW(nullptr, TRUE, cancelRequested, nullptr));
            Assert::IsTrue(input && cancel);
            STARTUPINFOW startup{ sizeof(startup) };
            startup.dwFlags = STARTF_USESTDHANDLES;
            startup.hStdInput = input.get();
            startup.hStdOutput = startup.hStdError = write.get();
            const auto deadline = GetTickCount64() + 500;
            auto command = L"\"" + executable.wstring() + L"\" " + mode + L" " + std::to_wstring(deadline);
            wil::unique_process_information process;
            Assert::IsTrue(CreateProcessW(executable.c_str(), command.data(), nullptr, nullptr, TRUE, CREATE_NO_WINDOW,
                                         nullptr, nullptr, &startup, &process) != FALSE);
            write.reset();
            auto cleanup = wil::scope_exit([&] {
                if (WaitForSingleObject(process.hProcess, 0) == WAIT_TIMEOUT)
                {
                    TerminateProcess(process.hProcess, 1);
                    WaitForSingleObject(process.hProcess, 2000);
                }
            });
            const auto started = GetTickCount64();
            try
            {
                WorkspacesSession::ReadResult(process.hProcess, read.get(), cancel.get(), deadline, L"operation");
                Assert::Fail(L"The controlled host must return an error outcome.");
            }
            catch (const WorkspacesCli::Error& error)
            {
                Assert::AreEqual(std::wstring(expectedCode), error.code);
                Assert::AreEqual(std::wstring(expectedCode) == L"canceled" ? 12 : 9, error.exitCode);
            }
            Assert::IsTrue(GetTickCount64() - started < 4000, L"Completion acknowledgement must remain bounded.");
            Assert::IsTrue(WaitForSingleObject(cancel.get(), 0) == (cancelRequested ? WAIT_OBJECT_0 : WAIT_TIMEOUT),
                           L"An elapsed deadline must not become an explicit cancellation.");
        }

        static std::pair<DWORD, json::JsonObject> Run(bool fixture, bool canceled, bool cancelAfterStart = false, bool expired = false, ULONGLONG* cancellationLatency = nullptr, bool minimized = false, bool maximized = false, bool guiCanceled = false)
        {
            RequireOptIn();
            wchar_t module[MAX_PATH]{};
            Assert::IsTrue(GetModuleFileNameW(reinterpret_cast<HMODULE>(&__ImageBase), module, ARRAYSIZE(module)) != 0);
            auto root = std::filesystem::path(module).parent_path().parent_path().parent_path();
            auto executable = root / L"PowerToys.WorkspacesLauncher.exe";
            Assert::IsTrue(std::filesystem::is_regular_file(executable));
            WorkspacesData::WorkspacesProject project{};
            project.id = L"{6CF910A2-D2E0-436D-A50E-41432A88452A}";
            project.name = L"Isolated CLI transport test";
            project.monitors.push_back({ .number = 1, .dpi = 96, .monitorRectDpiAware = { 0, 0, 1920, 1080 }, .monitorRectDpiUnaware = { 0, 0, 1920, 1080 } });
            if (minimized || maximized)
            {
                project.monitors = MonitorUtils::IdentifyMonitors();
                Assert::IsFalse(project.monitors.empty(), L"Window-state validation requires a current monitor, not the missing-monitor fallback.");
            }
            WorkspacesData::WorkspacesProject::Application app{};
            app.id = L"{49330ECB-5917-4DF3-B70A-1F2D7FE4899B}";
            app.name = L"WorkspacesCliWindowFixture";
            app.path = (root / L"tests" / L"Workspaces" /
                        (fixture ? L"WorkspacesCliWindowFixture.exe" : L"WorkspacesCliNonexistent-49330ECB.exe"))
                           .wstring();
            app.position = { 120, 120, 640, 400 };
            app.monitor = project.monitors.front().number;
            app.isMinimized = minimized;
            app.isMaximized = maximized;
            project.apps.push_back(app);
            GUID guid{};
            CoCreateGuid(&guid);
            wchar_t operation[40]{};
            StringFromGUID2(guid, operation, ARRAYSIZE(operation));
            project.id = operation;
            const auto startedName = std::wstring(L"Local\\WorkspacesCliFixtureStarted-") + operation;
            wil::unique_handle started(CreateEventW(nullptr, TRUE, FALSE, startedName.c_str()));
            Assert::IsTrue(static_cast<bool>(started));
            if (cancelAfterStart || guiCanceled)
                project.apps[0].commandLineArgs = L"--delay-window " + startedName;
            std::vector<wil::unique_handle> pendingStarted;
            if (guiCanceled)
            {
                for (const auto id : { L"{59330ECB-5917-4DF3-B70A-1F2D7FE4899B}", L"{69330ECB-5917-4DF3-B70A-1F2D7FE4899B}" })
                {
                    auto pending = project.apps.front();
                    pending.id = id;
                    const auto pendingName = startedName + L"-" + pending.id;
                    pendingStarted.emplace_back(CreateEventW(nullptr, TRUE, FALSE, pendingName.c_str()));
                    Assert::IsTrue(static_cast<bool>(pendingStarted.back()));
                    pending.commandLineArgs = L"--delay-window " + pendingName;
                    project.apps.push_back(std::move(pending));
                }
            }
            json::JsonObject request;
            request.SetNamedValue(L"protocolVersion", json::value(1));
            request.SetNamedValue(L"operationId", json::value(operation));
            request.SetNamedValue(L"timeout", json::value(guiCanceled ? 0 : 20));
            request.SetNamedValue(L"deadline", json::value(guiCanceled ? 0 : expired ? GetTickCount64() - 1 :
                                                                                       GetTickCount64() + 20000));
            request.SetNamedValue(L"presentation", json::value(static_cast<int>(guiCanceled ? WorkspacesSession::Presentation::Progress : WorkspacesSession::Presentation::ConfirmationOnly)));
            request.SetNamedValue(L"invokePoint", json::value(static_cast<int>(InvokePoint::EditorButton)));
            request.SetNamedValue(L"editorDraft", json::value(false));
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
            wil::unique_handle input(CreateFileW(L"NUL", GENERIC_READ, FILE_SHARE_READ | FILE_SHARE_WRITE, &sa, OPEN_EXISTING, 0, nullptr));
            wil::unique_handle owner;
            Assert::IsTrue(DuplicateHandle(GetCurrentProcess(), GetCurrentProcess(), GetCurrentProcess(), owner.put(), SYNCHRONIZE, TRUE, 0) != FALSE);
            STARTUPINFOW startup{ sizeof(startup) };
            startup.dwFlags = STARTF_USESTDHANDLES;
            startup.hStdInput = input.get();
            startup.hStdOutput = input.get();
            startup.hStdError = input.get();
            const auto operationId = WorkspacesCli::NormalizeId(operation);
            std::wstring command = L"\"" + executable.wstring() + L"\" --session-worker " + operationId + L" " +
                                   std::to_wstring(reinterpret_cast<uintptr_t>(mapping.get())) + L" " +
                                   std::to_wstring(reinterpret_cast<uintptr_t>(cancel.get())) + L" " +
                                   std::to_wstring(reinterpret_cast<uintptr_t>(write.get())) + L" " +
                                   std::to_wstring(reinterpret_cast<uintptr_t>(owner.get())) + L" " + std::to_wstring(GetCurrentProcessId());
            wil::unique_process_information process;
            Assert::IsTrue(CreateProcessW(executable.c_str(), command.data(), nullptr, nullptr, TRUE, CREATE_NO_WINDOW, nullptr, root.c_str(), &startup, &process) != FALSE);
            write.reset();
            auto cleanup = wil::scope_exit([&] {
                if (WaitForSingleObject(process.hProcess, 0) == WAIT_TIMEOUT)
                {
                    TerminateProcess(process.hProcess, 9);
                    WaitForSingleObject(process.hProcess, 2000);
                }
            });
            std::string output;
            ULONGLONG cancellationAt = 0;
            const auto deadline = GetTickCount64() + 25000;
            while (GetTickCount64() < deadline)
            {
                if ((cancelAfterStart || guiCanceled) && !cancellationAt && WaitForSingleObject(started.get(), 0) == WAIT_OBJECT_0)
                {
                    cancellationAt = GetTickCount64();
                    if (guiCanceled)
                        CancelGuiLaunch(GetProcessId(process.hProcess));
                    else
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
            if (cancelAfterStart || guiCanceled)
                Assert::IsTrue(cancellationAt != 0, L"Fixture must signal startup before requesting cancellation.");
            if (guiCanceled)
            {
                Assert::AreEqual(DWORD{ WAIT_TIMEOUT }, WaitForSingleObject(cancel.get(), 0), L"The GUI must cancel without the frontend signaling its cancellation event.");
                for (const auto& pending : pendingStarted)
                    Assert::AreEqual(DWORD{ WAIT_TIMEOUT }, WaitForSingleObject(pending.get(), 0), L"Pending applications must not start.");
            }
            if (cancellationLatency)
                *cancellationLatency = GetTickCount64() - cancellationAt;
            try
            {
                int resultCode{};
                const auto result = WorkspacesCli::SessionResult(
                    WorkspacesSession::DeserializeResult(json::JsonObject::Parse(winrt::to_hstring(output)), operationId), resultCode);
                Assert::AreEqual(DWORD{ 0 }, code);
                return { static_cast<DWORD>(resultCode), result };
            }
            catch (const WorkspacesSession::Error& error)
            {
                return { static_cast<DWORD>(error.exitCode), WorkspacesCli::Failure(L"launch", error) };
            }
        }

    public:
        TEST_METHOD (FrontendAcceptsTimeoutAcknowledgedAfterDeadline)
        {
            CheckCompletion(L"worker-result-timeout", false, L"timeout");
        }

        TEST_METHOD (FrontendRetainsCancellationAcknowledgement)
        {
            CheckCompletion(L"worker-result-canceled", true, L"canceled");
        }

        TEST_METHOD (FrontendReportsUnknownWhenWorkerDoesNotAcknowledge)
        {
            CheckCompletion(L"worker-result-unresponsive", false, L"outcomeUnknown");
            CheckCompletion(L"worker-result-unresponsive", true, L"outcomeUnknown");
        }

        TEST_METHOD (FrontendRejectsMissingOrMalformedFinalResult)
        {
            CheckCompletion(L"worker-result-missing", false, L"outcomeUnknown");
            CheckCompletion(L"worker-result-malformed", false, L"outcomeUnknown");
        }

#ifdef _DEBUG
        TEST_METHOD (GuiCancellationAcknowledgesPendingAppsAndReleasesLaunchSession)
        {
            RequireOptIn();
            Assert::IsNull(FindWindowW(L"WorkspacesCliIsolatedFixture", nullptr));
            auto cleanup = wil::scope_exit([] { CloseFixture(); });
            ULONGLONG latency{};
            const auto [code, result] = Run(true, false, false, false, &latency, false, false, true);
            Assert::AreEqual(DWORD{ 12 }, code);
            Assert::AreEqual(std::wstring(L"canceled"), std::wstring(result.GetNamedObject(L"error").GetNamedString(L"code")));
            Assert::IsTrue(latency < 5000, L"GUI cancellation must not wait for an ordinary arranger completion.");
            const auto deadline = GetTickCount64() + 5000;
            while (!FindWindowW(L"WorkspacesCliIsolatedFixture", nullptr) && GetTickCount64() < deadline)
                Sleep(20);
            Assert::IsNotNull(FindWindowW(L"WorkspacesCliIsolatedFixture", nullptr), L"The already-started fixture must survive cancellation.");
            CloseFixture();
            const auto [nextCode, nextResult] = Run(false, false);
            Assert::AreEqual(DWORD{ 10 }, nextCode, L"A following launch must run instead of reporting busy.");
            Assert::AreEqual(std::wstring(L"launchFailed"),
                             std::wstring(nextResult.GetNamedObject(L"result").GetNamedArray(L"applications").GetObjectAt(0).GetNamedString(L"state")));
        }

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

        TEST_METHOD (SessionHostPreservesMinimizedAndMaximizedPlacement)
        {
            RequireOptIn();
            for (const bool minimized : { true, false })
            {
                Assert::IsNull(FindWindowW(L"WorkspacesCliIsolatedFixture", nullptr));
                auto cleanup = wil::scope_exit([] { CloseFixture(); });
                const auto [code, result] = Run(true, false, false, false, nullptr, minimized, !minimized);
                Assert::AreEqual(DWORD{ 0 }, code);
                const auto window = FindWindowW(L"WorkspacesCliIsolatedFixture", nullptr);
                Assert::IsNotNull(window);
                Assert::IsTrue(minimized ? IsIconic(window) != FALSE : IsZoomed(window) != FALSE);
            }
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
