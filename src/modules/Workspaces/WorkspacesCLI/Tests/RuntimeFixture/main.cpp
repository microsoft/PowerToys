// Copyright (c) Microsoft Corporation. Licensed under the MIT license.
// Exercises CLI process/stream behavior without launching workspace applications or requesting UAC.
#include <windows.h>
#include <sddl.h>
#include <shellapi.h>
#include <filesystem>
#include <algorithm>
#include <future>
#include <iostream>
#include <atomic>
#include <sstream>
#include <common/logger/logger.h>
#include <spdlog/sinks/ostream_sink.h>
#include <spdlog/sinks/null_sink.h>
#include <WorkspacesLauncher/LauncherUIHelper.h>
#include <WorkspacesLib/LauncherUiMessage.h>
#include <wil/resource.h>
#include <WorkspacesCLI/JsonOutput.h>
#include <WorkspacesCLI/Resources.h>
#include <WorkspacesLib/CliCommands.h>
#include <WorkspacesLib/trace.h>

namespace
{
    std::atomic<bool> stallShell{};
    std::atomic<bool> shellEntered{};

    BOOL WINAPI FixtureShellExecute(SHELLEXECUTEINFO* information)
    {
        if (stallShell)
        {
            shellEntered = true;
            Sleep(10000);
            SetLastError(ERROR_CANCELLED);
            return FALSE;
        }
        return ShellExecuteExW(information);
    }
}

// Compile the existing launcher with a test-only Shell stall; normal fixture modes use the real API.
#pragma push_macro("ShellExecuteEx")
#undef ShellExecuteEx
#define ShellExecuteEx FixtureShellExecute
#include <WorkspacesLauncher/AppLauncher.cpp>
#pragma pop_macro("ShellExecuteEx")

namespace CliTelemetryProbe
{
    struct Event
    {
        std::wstring command;
        bool successful;
    };
    std::vector<Event> events;
    int registered{};
    int unregistered{};
    int launchExit{};
    struct AdapterExited
    {
    };

    struct Trace
    {
        struct Workspaces
        {
            static void RegisterProvider() { ++registered; }
            static void UnregisterProvider() { ++unregistered; }
            static void CLICommand(PCWSTR commandName, bool successful)
            {
                events.push_back({ commandName, successful });
            }
        };
    };

    namespace Commands
    {
        using namespace WorkspacesCli;
        void CheckEnabled() {}
        std::vector<WorkspacesData::WorkspacesProject> LoadWorkspaces()
        {
            return {};
        }
    }

    namespace Session
    {
        using Request = WorkspacesSession::Request;
        using Presentation = WorkspacesSession::Presentation;
        WorkspacesSession::Result Start(const Request&, HANDLE)
        {
            if (launchExit != 0 && launchExit != 10 && launchExit != 11)
                throw WorkspacesSession::Error(launchExit, L"controlledError", "Controlled test error.", launchExit == 9);
            WorkspacesSession::Result result;
            result.project.apps.emplace_back();
            result.project.apps[0].id = L"test-app";
            result.applications.emplace(result.project.apps[0], WorkspacesData::LaunchingAppState{ result.project.apps[0], nullptr, launchExit == 10 ? LaunchingState::Launched : LaunchingState::LaunchedAndMoved });
            if (launchExit == 11)
                result.persistence = WorkspaceStore::UpdateResult::Unverified;
            return result;
        }
        void ExitIfExecutionUnresponsive(const WorkspacesSession::Error& error)
        {
            if (error.executionStillRunning)
                throw AdapterExited{};
        }
    }

    namespace Resources
    {
        std::wstring Get(UINT)
        {
            return L"Test";
        }
        void LocalizeForOutput(const json::JsonObject&, bool) {}
    }
}

// Exercise the real frontend's telemetry lifetime without launching apps or emitting diagnostics.
#define wmain CliMainForTelemetryTest
#define Trace CliTelemetryProbe::Trace
#define WorkspacesCli CliTelemetryProbe::Commands
#define WorkspacesSession CliTelemetryProbe::Session
#define CliResources CliTelemetryProbe::Resources
#include <WorkspacesCLI/main.cpp>
#undef CliResources
#undef WorkspacesSession
#undef WorkspacesCli
#undef Trace
#undef wmain

namespace
{
    void Check(bool value)
    {
        if (!value)
            throw std::runtime_error("CLI runtime fixture assertion failed.");
    }

    void CheckTelemetry()
    {
        struct Case
        {
            std::vector<std::wstring> arguments;
            const wchar_t* category;
            int exitCode;
        };
        const auto id = L"{49330ECB-5917-4DF3-B70A-1F2D7FE4899B}";
        const Case cases[] = {
            { { L"list", L"--json" }, L"list", 0 },
            { { L"launch", L"--id", id, L"--json" }, L"launch", 0 },
            { { L"launch", L"--id", id, L"--json" }, L"launch", 10 },
            { { L"launch", L"--id", id, L"--json" }, L"launch", 12 },
            { { L"launch", L"--id", id, L"--json" }, L"launch", 9 },
            { { L"launch", L"--json" }, L"launch", 2 },
            { { L"private-command", L"--json" }, L"unknown", 2 },
            { { L"--help" }, L"help", 0 },
            { { L"--version" }, L"version", 0 },
        };
        for (const auto& test : cases)
        {
            CliTelemetryProbe::events.clear();
            CliTelemetryProbe::registered = CliTelemetryProbe::unregistered = 0;
            CliTelemetryProbe::launchExit = test.exitCode;
            auto arguments = test.arguments;
            arguments.insert(arguments.begin(), L"PowerToys.WorkspacesCLI.exe");
            std::vector<wchar_t*> argv;
            for (auto& argument : arguments)
                argv.push_back(argument.data());
            std::ostringstream output, errors;
            const auto previousOutput = std::cout.rdbuf(output.rdbuf());
            const auto previousError = std::cerr.rdbuf(errors.rdbuf());
            auto restore = wil::scope_exit([&] {
                std::cout.rdbuf(previousOutput);
                std::cerr.rdbuf(previousError);
            });
            bool exited = false;
            try
            {
                Check(CliMainForTelemetryTest(static_cast<int>(argv.size()), argv.data()) == test.exitCode);
            }
            catch (const CliTelemetryProbe::AdapterExited&)
            {
                exited = true;
            }
            Check(exited == (test.exitCode == 9));
            Check(CliTelemetryProbe::registered == 1 && CliTelemetryProbe::unregistered == 1);
            Check(CliTelemetryProbe::events.size() == 1);
            Check(CliTelemetryProbe::events[0].command == test.category);
            Check(CliTelemetryProbe::events[0].successful == (test.exitCode == 0));
        }
    }

    void CheckCodePage()
    {
        const auto original = GetConsoleOutputCP();
        Check(original != 0);
        auto restore = wil::scope_exit([&] { SetConsoleOutputCP(original); });
        Check(SetConsoleOutputCP(437) != FALSE);
        {
            WorkspacesCli::ConsoleOutputCodePage codePage;
            Check(GetConsoleOutputCP() == CP_UTF8);
        }
        Check(GetConsoleOutputCP() == 437);
        try
        {
            WorkspacesCli::ConsoleOutputCodePage codePage;
            Check(GetConsoleOutputCP() == CP_UTF8);
            throw std::runtime_error("Exercise exceptional scope exit");
        }
        catch (const std::runtime_error&)
        {
            Check(GetConsoleOutputCP() == 437);
        }
        {
            WorkspacesCli::ConsoleOutputCodePage codePage(false);
            Check(GetConsoleOutputCP() == 437);
        }
        wchar_t module[32768]{};
        Check(GetModuleFileNameW(nullptr, module, ARRAYSIZE(module)) != 0);
        const auto executable = std::filesystem::path(module).parent_path().parent_path().parent_path() / L"PowerToys.WorkspacesCLI.exe";
        for (const auto argument : { L"--help", L"--version", L"--invalid", L"--launch-worker" })
        {
            SECURITY_ATTRIBUTES attributes{ sizeof(attributes), nullptr, TRUE };
            wil::unique_handle nul(CreateFileW(L"NUL", GENERIC_READ | GENERIC_WRITE, FILE_SHARE_READ | FILE_SHARE_WRITE,
                                               &attributes, OPEN_EXISTING, 0, nullptr));
            Check(static_cast<bool>(nul));
            STARTUPINFOW startup{ sizeof(startup) };
            startup.dwFlags = STARTF_USESTDHANDLES;
            startup.hStdInput = startup.hStdOutput = startup.hStdError = nul.get();
            auto command = L"\"" + executable.wstring() + L"\" " + argument;
            wil::unique_process_information process;
            Check(CreateProcessW(executable.c_str(), command.data(), nullptr, nullptr, TRUE, 0, nullptr, nullptr, &startup, &process) != FALSE);
            auto cleanup = wil::scope_exit([&] {
                if (WaitForSingleObject(process.hProcess, 0) == WAIT_TIMEOUT)
                {
                    TerminateProcess(process.hProcess, 1);
                    WaitForSingleObject(process.hProcess, 2000);
                }
            });
            Check(WaitForSingleObject(process.hProcess, 5000) == WAIT_OBJECT_0);
            DWORD exitCode{};
            Check(GetExitCodeProcess(process.hProcess, &exitCode) != FALSE);
            Check(exitCode == (std::wstring_view(argument) == L"--help" || std::wstring_view(argument) == L"--version" ? 0ul : 2ul));
            Check(GetConsoleOutputCP() == 437);
        }
    }

    void CheckPersistenceOutput()
    {
        for (const bool asJson : { false, true })
        {
            CliTelemetryProbe::launchExit = 11;
            std::vector<std::wstring> arguments{ L"PowerToys.WorkspacesCLI.exe", L"launch", L"--name", L"Controlled workspace" };
            if (asJson)
                arguments.push_back(L"--json");
            std::vector<wchar_t*> argv;
            for (auto& argument : arguments)
                argv.push_back(argument.data());
            std::ostringstream output, errors;
            const auto previousOutput = std::cout.rdbuf(output.rdbuf());
            const auto previousError = std::cerr.rdbuf(errors.rdbuf());
            auto restore = wil::scope_exit([&] {
                std::cout.rdbuf(previousOutput);
                std::cerr.rdbuf(previousError);
            });
            Check(CliMainForTelemetryTest(static_cast<int>(argv.size()), argv.data()) == 11);
            if (output.str().empty())
                throw std::runtime_error("Persistence failure hid the observed application results.");
            const auto printed = json::JsonObject::Parse(winrt::to_hstring(output.str()));
            const auto result = asJson ? printed.GetNamedObject(L"result") : printed;
            Check(result.GetNamedString(L"persistenceStatus") == L"unverified");
            const auto app = result.GetNamedArray(L"applications").GetObjectAt(0);
            Check(app.GetNamedString(L"id") == L"test-app" && app.GetNamedString(L"state") == L"arranged");
            if (asJson)
                Check(errors.str().empty() && printed.GetNamedObject(L"error").GetNamedString(L"code") == L"persistenceUnverified");
            else
                Check(errors.str().find("persistenceUnverified") != std::string::npos);
        }
    }

    struct ConsoleProbeResult
    {
        DWORD processCount{};
        bool parentAttached{};
    };

    void RunConsoleChild(DWORD parentPid, const std::wstring& name)
    {
        wil::unique_handle mapping(OpenFileMappingW(FILE_MAP_WRITE, FALSE, name.c_str()));
        wil::unique_handle ready(OpenEventW(EVENT_MODIFY_STATE, FALSE, (name + L"-ready").c_str()));
        wil::unique_handle stop(OpenEventW(SYNCHRONIZE, FALSE, (name + L"-stop").c_str()));
        Check(mapping && ready && stop);
        wil::unique_mapview_ptr<ConsoleProbeResult> result(static_cast<ConsoleProbeResult*>(MapViewOfFile(mapping.get(), FILE_MAP_WRITE, 0, 0, sizeof(ConsoleProbeResult))));
        Check(static_cast<bool>(result));
        std::vector<DWORD> processes(16);
        DWORD count = GetConsoleProcessList(processes.data(), static_cast<DWORD>(processes.size()));
        if (count > processes.size())
        {
            processes.resize(count);
            count = GetConsoleProcessList(processes.data(), static_cast<DWORD>(processes.size()));
        }
        Check(count > 0 && count <= processes.size());
        result->processCount = count;
        result->parentAttached = std::find(processes.begin(), processes.begin() + count, parentPid) != processes.begin() + count;
        Check(SetEvent(ready.get()) != FALSE);
        Check(WaitForSingleObject(stop.get(), 10000) == WAIT_OBJECT_0);
    }

    void CheckConsoleLaunch()
    {
        Logger::init(std::vector<spdlog::sink_ptr>{ std::make_shared<spdlog::sinks::null_sink_mt>() });
        const auto console = GetConsoleWindow();
        const auto codePage = GetConsoleOutputCP();
        const auto input = GetStdHandle(STD_INPUT_HANDLE);
        const auto output = GetStdHandle(STD_OUTPUT_HANDLE);
        const auto foreground = GetForegroundWindow();
        Check(console != nullptr && codePage != 0);
        auto restoreForeground = wil::scope_exit([&] {
            if (foreground && IsWindow(foreground))
                SetForegroundWindow(foreground);
        });
        GUID guid{};
        winrt::check_hresult(CoCreateGuid(&guid));
        wchar_t id[40]{}, module[32768]{};
        StringFromGUID2(guid, id, ARRAYSIZE(id));
        Check(GetModuleFileNameW(nullptr, module, ARRAYSIZE(module)) != 0);
        const auto name = std::wstring(L"Local\\WorkspacesConsoleProbe-") + id;
        wil::unique_handle mapping(CreateFileMappingW(INVALID_HANDLE_VALUE, nullptr, PAGE_READWRITE, 0, sizeof(ConsoleProbeResult), name.c_str()));
        wil::unique_handle ready(CreateEventW(nullptr, TRUE, FALSE, (name + L"-ready").c_str()));
        wil::unique_handle stop(CreateEventW(nullptr, TRUE, FALSE, (name + L"-stop").c_str()));
        Check(mapping && ready && stop);
        wil::unique_mapview_ptr<ConsoleProbeResult> result(static_cast<ConsoleProbeResult*>(MapViewOfFile(mapping.get(), FILE_MAP_READ, 0, 0, sizeof(ConsoleProbeResult))));
        Check(static_cast<bool>(result));
        const auto launched = AppLauncher::LaunchApp(module, L"console-child " + std::to_wstring(GetCurrentProcessId()) + L" " + name, false);
        Check(launched.isOk());
        wil::unique_handle child(launched.value().hProcess);
        Check(static_cast<bool>(child));
        auto cleanup = wil::scope_exit([&] {
            SetEvent(stop.get());
            if (WaitForSingleObject(child.get(), 5000) == WAIT_TIMEOUT)
            {
                TerminateProcess(child.get(), 1);
                WaitForSingleObject(child.get(), 2000);
            }
        });
        Check(WaitForSingleObject(ready.get(), 10000) == WAIT_OBJECT_0);
        Check(result->processCount > 0);
        if (result->parentAttached)
            throw std::runtime_error("The workspace application inherited the frontend console.");
        Check(GetConsoleWindow() == console && GetConsoleOutputCP() == codePage);
        Check(GetStdHandle(STD_INPUT_HANDLE) == input && GetStdHandle(STD_OUTPUT_HANDLE) == output);
        Check(SetEvent(stop.get()) != FALSE);
        Check(WaitForSingleObject(child.get(), 5000) == WAIT_OBJECT_0);
        DWORD exitCode{};
        Check(GetExitCodeProcess(child.get(), &exitCode) != FALSE && exitCode == 0);
    }

    void CheckPipeStartup(bool cancelStartup)
    {
        Logger::init(std::vector<spdlog::sink_ptr>{ std::make_shared<spdlog::sinks::null_sink_mt>() });
        GUID guid{};
        winrt::check_hresult(CoCreateGuid(&guid));
        wchar_t id[40]{};
        StringFromGUID2(guid, id, ARRAYSIZE(id));
        const auto prefix = std::wstring(L"\\\\.\\pipe\\WorkspacesCliPipeStartup-") + id;
        wil::unique_handle request(CreateEventW(nullptr, TRUE, FALSE, nullptr));
        wil::unique_handle reply(CreateEventW(nullptr, TRUE, FALSE, nullptr));
        wil::unique_handle finish(CreateEventW(nullptr, TRUE, FALSE, nullptr));
        Check(request && reply && finish);
        IPCHelper client(prefix + L"-client", prefix + L"-server", [&](const std::wstring& message) {
            if (message == L"snapshot")
                SetEvent(reply.get());
        });
        if (cancelStartup)
        {
            const auto started = GetTickCount64();
            Check(!client.WaitForReady(100));
            int checks = 0;
            Check(!client.WaitForReady(10000, [&] { return ++checks > 1; }));
            Check(GetTickCount64() - started < 2000);
            return;
        }
        auto peer = std::async(std::launch::async, [&] {
            Sleep(200);
            IPCHelper server(prefix + L"-server", prefix + L"-client", [&](const std::wstring& message) {
                if (message == L"snapshot-request")
                    SetEvent(request.get());
            });
            Check(WaitForSingleObject(request.get(), 5000) == WAIT_OBJECT_0);
            server.send(L"snapshot");
            Check(WaitForSingleObject(finish.get(), 5000) == WAIT_OBJECT_0);
        });
        auto cleanup = wil::scope_exit([&] {
            SetEvent(finish.get());
            if (peer.valid())
                peer.wait();
        });
        Check(client.WaitForReady(5000));
        client.send(L"snapshot-request");
        Check(WaitForSingleObject(reply.get(), 5000) == WAIT_OBJECT_0);
        SetEvent(finish.get());
        peer.get();
    }

    void CheckUiStartup(bool cancelStartup, bool restrictedOwner)
    {
        std::vector<BYTE> originalSecurity;
        auto restoreSecurity = wil::scope_exit([&] {
            if (!originalSecurity.empty())
                SetKernelObjectSecurity(GetCurrentProcess(), DACL_SECURITY_INFORMATION, originalSecurity.data());
        });
        if (restrictedOwner)
        {
            DWORD size{};
            GetKernelObjectSecurity(GetCurrentProcess(), DACL_SECURITY_INFORMATION, nullptr, 0, &size);
            Check(size != 0);
            originalSecurity.resize(size);
            Check(GetKernelObjectSecurity(GetCurrentProcess(), DACL_SECURITY_INFORMATION, originalSecurity.data(), size, &size) != FALSE);
            wil::unique_handle token;
            Check(OpenProcessToken(GetCurrentProcess(), TOKEN_QUERY, token.put()) != FALSE);
            DWORD tokenSize{};
            GetTokenInformation(token.get(), TokenUser, nullptr, 0, &tokenSize);
            std::vector<BYTE> user(tokenSize);
            Check(GetTokenInformation(token.get(), TokenUser, user.data(), tokenSize, &tokenSize) != FALSE);
            wil::unique_hlocal_string sid;
            Check(ConvertSidToStringSidW(reinterpret_cast<TOKEN_USER*>(user.data())->User.Sid, sid.put()) != FALSE);
            const auto sddl = std::wstring(L"D:P(A;;0x00101000;;;") + sid.get() + L")";
            wil::unique_hlocal_security_descriptor descriptor;
            Check(ConvertStringSecurityDescriptorToSecurityDescriptorW(sddl.c_str(), SDDL_REVISION_1, descriptor.put(), nullptr) != FALSE);
            Check(SetKernelObjectSecurity(GetCurrentProcess(), DACL_SECURITY_INFORMATION, descriptor.get()) != FALSE);
            wil::unique_handle broad(OpenProcess(PROCESS_ALL_ACCESS, FALSE, GetCurrentProcessId()));
            Check(!broad);
            wil::unique_handle waitOnly(OpenProcess(SYNCHRONIZE, FALSE, GetCurrentProcessId()));
            Check(static_cast<bool>(waitOnly));
        }
        GUID operation{}, request{};
        winrt::check_hresult(CoCreateGuid(&operation));
        winrt::check_hresult(CoCreateGuid(&request));
        wchar_t operationText[40]{}, requestText[40]{};
        StringFromGUID2(operation, operationText, ARRAYSIZE(operationText));
        StringFromGUID2(request, requestText, ARRAYSIZE(requestText));
        std::ostringstream diagnostics;
        auto sink = std::make_shared<spdlog::sinks::ostream_sink_mt>(diagnostics);
        Logger::init(std::vector<spdlog::sink_ptr>{ sink });
        auto restoreLogger = wil::scope_exit([] {
            Logger::init(std::vector<spdlog::sink_ptr>{ std::make_shared<spdlog::sinks::null_sink_mt>() });
        });
        bool displayed = false;
        {
            std::atomic<bool> shown = false;
            LauncherUIHelper ui([&](const std::wstring& text) {
                const auto message = LauncherUiMessage::Parse(text);
                if (message && message->type == LauncherUiMessage::Type::WarningShown && message->requestId == requestText)
                    shown = true;
            }, WorkspacesCli::NormalizeId(operationText), true);
            if (cancelStartup)
            {
                int checks = 0;
                const auto started = GetTickCount64();
                Check(!ui.LaunchUI([&] { return ++checks > 1; }));
                Check(GetTickCount64() - started < 5000);
                Check(!ui.RequestApproval(requestText, L"Must not appear", L"C:\\not-executed.exe", L"",
                                          { SignatureVerification::Status::Unsigned, TRUST_E_NOSIGNATURE, {}, {} }));
                Check(!shown);
                return;
            }
            Check(ui.LaunchUI());
            Check(ui.RequestApproval(requestText, L"Controlled startup test - no application is executed",
                                     L"C:\\Workspaces.NoExecution.Test.exe", L"",
                                     { SignatureVerification::Status::Unsigned, TRUST_E_NOSIGNATURE, {}, {} }));
            const auto deadline = GetTickCount64() + 10000;
            while (!shown && ui.IsRunning() && GetTickCount64() < deadline)
                Sleep(20);
            ui.DismissApproval(requestText);
            displayed = shown;
        }
        if (!displayed)
            throw std::runtime_error("First warning was not displayed by the real LauncherUIHelper. " + diagnostics.str());
    }
}

int wmain(int argc, wchar_t** argv)
{
    try
    {
        winrt::init_apartment();
        if (argc == 4 && std::wstring_view(argv[1]) == L"console-child")
        {
            RunConsoleChild(std::stoul(argv[2]), argv[3]);
            return 0;
        }
        if (argc == 6 && std::wstring_view(argv[2]) == L"--session-worker")
        {
            // Controlled arranger startup stall; the shared session must stop only this owned process.
            Sleep(10000);
            return 1;
        }
        if (argc == 3 && std::wstring_view(argv[1]).starts_with(L"worker-result-"))
        {
            const std::wstring_view mode(argv[1]);
            const auto deadline = std::stoull(argv[2]);
            while (GetTickCount64() < deadline + (mode == L"worker-result-unresponsive" ? 10000 : 100))
                Sleep(5);
            if (mode == L"worker-result-missing")
                return 1;
            if (mode == L"worker-result-malformed")
            {
                std::cout << "{not-json}\n";
                return 1;
            }
            const bool canceled = mode == L"worker-result-canceled";
            const auto output = WorkspacesSession::SerializeError(
                { canceled ? 12 : 9, canceled ? L"canceled" : L"timeout", "Controlled worker completion." }, L"operation");
            std::cout << winrt::to_string(output.Stringify()) << '\n';
            return canceled ? 12 : 9;
        }
        Check(argc == 2);
        if (std::wstring_view(argv[1]) == L"persistence-output" || std::wstring_view(argv[1]) == L"console-launch")
        {
            if (std::wstring_view(argv[1]) == L"persistence-output")
                CheckPersistenceOutput();
            else
                CheckConsoleLaunch();
            std::cout << "{\"passed\":true}\n";
            return 0;
        }
        if (std::wstring_view(argv[1]) == L"telemetry")
        {
            CheckTelemetry();
            std::cout << "{\"passed\":true}\n";
            return 0;
        }
        if (std::wstring_view(argv[1]).starts_with(L"pipe-startup"))
        {
            CheckPipeStartup(std::wstring_view(argv[1]) == L"pipe-startup-canceled");
            std::cout << "{\"passed\":true}\n";
            return 0;
        }
        if (std::wstring_view(argv[1]).starts_with(L"session-"))
        {
            const bool gui = std::wstring_view(argv[1]) == L"session-gui" || std::wstring_view(argv[1]) == L"session-draft";
            const bool draft = std::wstring_view(argv[1]) == L"session-draft";
            const bool canceled = std::wstring_view(argv[1]) == L"session-canceled";
            const bool timedOut = std::wstring_view(argv[1]) == L"session-timeout";
            const bool unresponsive = std::wstring_view(argv[1]) == L"session-unresponsive";
            const bool oversized = std::wstring_view(argv[1]) == L"session-oversized";
            GUID guid{};
            winrt::check_hresult(CoCreateGuid(&guid));
            wchar_t id[40]{};
            StringFromGUID2(guid, id, ARRAYSIZE(id));
            WorkspacesData::WorkspacesProject project{};
            project.id = id;
            project.name = L"Isolated shared session";
            project.monitors.push_back({ .number = 1, .dpi = 96 });
            WorkspacesData::WorkspacesProject::Application app{};
            app.id = draft ? L"" : L"{49330ECB-5917-4DF3-B70A-1F2D7FE4899B}";
            app.name = L"WorkspacesNoExecutionFixture";
            app.path = L"C:\\Workspaces.NoExecution.Fixture.DoesNotExist.exe";
            app.position = { 10, 10, 640, 400 };
            app.monitor = 1;
            if (oversized)
                app.commandLineArgs.assign(WorkspacesSession::MaxPayload, L'x');
            project.apps.push_back(app);
            WorkspacesSession::Request request;
            request.id = project.id;
            request.presentation = gui ? WorkspacesSession::Presentation::Progress : WorkspacesSession::Presentation::ConfirmationOnly;
            request.invokePoint = draft ? InvokePoint::LaunchAndEdit : InvokePoint::EditorButton;
            request.timeoutSeconds = timedOut || unresponsive ? 1 : gui ? 0 :
                                                                          20;
            wchar_t module[32768]{};
            Check(GetModuleFileNameW(nullptr, module, ARRAYSIZE(module)) != 0);
            const auto directory = std::filesystem::path(module).parent_path();
            if (!gui)
                Check(!std::filesystem::exists(directory / L"PowerToys.WorkspacesLauncher.exe"));
            wil::unique_handle cancel(CreateEventW(nullptr, TRUE, canceled, nullptr));
            Check(static_cast<bool>(cancel));
            const auto began = GetTickCount64();
            if (canceled || timedOut || unresponsive || oversized)
            {
                stallShell = unresponsive;
                try
                {
                    WorkspacesSession::StartSnapshot(request, project, draft, cancel.get());
                    throw std::runtime_error("The stopped session unexpectedly completed.");
                }
                catch (const WorkspacesSession::Error& error)
                {
                    Check(error.exitCode == (canceled ? 12 : oversized ? 8 :
                                                                         9));
                    Check(error.code == (canceled ? L"canceled" : oversized ? L"invalidData" :
                                                              unresponsive  ? L"outcomeUnknown" :
                                                                              L"timeout"));
                    Check(GetTickCount64() - began < 5000);
                    if (unresponsive)
                    {
                        Check(shellEntered && error.executionStillRunning);
                        std::cout << "{\"passed\":true}\n"
                                  << std::flush;
                        WorkspacesSession::ExitIfExecutionUnresponsive(error);
                    }
                }
                std::cout << "{\"passed\":true}\n";
                return 0;
            }
            const auto result = WorkspacesSession::StartSnapshot(request, project, draft, cancel.get());
            Check(result.project.id == project.id);
            Check(result.applications.size() == 1 && result.applications.begin()->second.state == LaunchingState::Failed);
            Check(!result.project.apps[0].id.empty());
            Check(draft ? !result.persistence.has_value() : result.persistence == WorkspaceStore::UpdateResult::Conflict);
            int exitCode{};
            const auto output = WorkspacesCli::SessionResult(result, exitCode);
            Check(exitCode == 10 && output.GetNamedObject(L"result").GetNamedArray(L"applications").GetObjectAt(0).GetNamedString(L"state") == L"launchFailed");
            std::cout << "{\"passed\":true}\n";
            return 0;
        }
        if (std::wstring_view(argv[1]) == L"launched-localization")
        {
            WorkspacesData::WorkspacesProject project{};
            WorkspacesData::WorkspacesProject::Application app{};
            app.id = L"test-app";
            project.apps.push_back(app);
            WorkspacesData::LaunchingAppStateMap states;
            states.emplace(app, WorkspacesData::LaunchingAppState{ app, nullptr, LaunchingState::Launched });
            int exitCode{};
            const auto output = WorkspacesCli::LaunchResult(project, states, L"operation", exitCode);
            CliResources::Localize(output);
            const auto result = output.GetNamedObject(L"result").GetNamedArray(L"applications").GetObjectAt(0);
            Check(result.GetNamedString(L"state") == L"launched");
            Check(result.GetNamedString(L"message") == L"Localized unconfirmed arrangement test message.");
            Check(exitCode == 10);
            std::cout << "{\"passed\":true}\n";
            return 0;
        }
        if (std::wstring_view(argv[1]).starts_with(L"ui-startup"))
        {
            CheckUiStartup(std::wstring_view(argv[1]) == L"ui-startup-canceled",
                           std::wstring_view(argv[1]) == L"ui-startup-restricted-owner");
            std::cout << "{\"passed\":true}\n";
            return 0;
        }
        if (std::wstring_view(argv[1]) == L"worker-resource-error")
        {
            bool missingResources = false;
            try
            {
                CliResources::Get(IDS_CLIINVALIDARGUMENTS);
            }
            catch (const WorkspacesCli::Error&)
            {
                missingResources = true;
            }
            Check(missingResources);
            const auto output = WorkspacesCli::Failure(L"launch", { 2, L"invalidArguments", "Controlled worker error." });
            CliResources::LocalizeForOutput(output, true);
            std::cout << winrt::to_string(output.Stringify()) << '\n';
            return 2;
        }
        Check(std::wstring_view(argv[1]) == L"code-page");
        CheckCodePage();
        std::cout << "{\"passed\":true}\n";
        return 0;
    }
    catch (const std::exception& error)
    {
        json::JsonObject result;
        result.SetNamedValue(L"passed", json::value(false));
        result.SetNamedValue(L"error", json::value(winrt::to_hstring(error.what())));
        std::cout << winrt::to_string(result.Stringify()) << '\n';
        return 1;
    }
    catch (const winrt::hresult_error&)
    {
        std::cout << "{\"passed\":false,\"windowsError\":true}\n";
        return 1;
    }
}
