// Copyright (c) Microsoft Corporation. Licensed under the MIT license.
// Exercises CLI process/stream behavior without launching workspace applications or requesting UAC.
#include <windows.h>
#include <filesystem>
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

namespace
{
    void Check(bool value)
    {
        if (!value)
            throw std::runtime_error("CLI runtime fixture assertion failed.");
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
        for (const auto argument : { L"--version", L"--invalid", L"--launch-worker" })
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
            Check(exitCode == (std::wstring_view(argument) == L"--version" ? 0ul : 2ul));
            Check(GetConsoleOutputCP() == 437);
        }
    }

    void CheckUiStartup(bool cancelStartup)
    {
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
            }, WorkspacesCli::NormalizeId(operationText));
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
            const auto output = WorkspacesCli::Failure(L"launch", { canceled ? 12 : 9, canceled ? L"canceled" : L"timeout", "Controlled worker completion." });
            std::cout << winrt::to_string(output.Stringify()) << '\n';
            return canceled ? 12 : 9;
        }
        Check(argc == 2);
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
            CheckUiStartup(std::wstring_view(argv[1]) == L"ui-startup-canceled");
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
