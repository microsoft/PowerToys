// Copyright (c) Microsoft Corporation. Licensed under the MIT license.
#define WIN32_LEAN_AND_MEAN
#include <windows.h>
#include <atomic>
#include <iostream>
#include <spdlog/sinks/null_sink.h>
#include <wil/resource.h>
#include <common/logger/logger.h>
#include <common/version/version.h>
#include <WorkspacesLib/CliCommands.h>
#include <WorkspacesLib/LaunchSession.h>
#include <WorkspacesLib/trace.h>
#include "Resources.h"
#include "JsonOutput.h"

namespace
{
    std::atomic<HANDLE> cancelHandle{ nullptr };

    BOOL WINAPI Cancel(DWORD type)
    {
        const auto handle = cancelHandle.load();
        if ((type == CTRL_C_EVENT || type == CTRL_BREAK_EVENT) && handle)
        {
            SetEvent(handle);
            return TRUE;
        }
        return FALSE;
    }

    void Print(const json::JsonObject& output, bool asJson)
    {
        CliResources::LocalizeForOutput(output, false);
        WorkspacesCli::ConsoleOutputCodePage consoleCodePage;
        auto flushOutput = wil::scope_exit([] {
            std::cout.flush();
            std::cerr.flush();
        });
        if (asJson)
        {
            std::cout << WorkspacesCli::FormatJson(output) << '\n';
            return;
        }
        if (output.HasKey(L"error"))
        {
            const auto error = output.GetNamedObject(L"error");
            std::cerr << winrt::to_string(error.GetNamedString(L"code")) << ": "
                      << winrt::to_string(error.GetNamedString(L"message")) << '\n';
            if (output.HasKey(L"result"))
                std::cout << winrt::to_string(output.GetNamedObject(L"result").Stringify()) << '\n';
        }
        else if (output.HasKey(L"workspaces") && output.GetNamedString(L"view") == L"summary")
        {
            std::cout << winrt::to_string(CliResources::Get(IDS_CLITABLEHEADER));
            for (const auto& item : output.GetNamedArray(L"workspaces"))
            {
                const auto project = item.GetObjectW();
                std::cout << winrt::to_string(project.GetNamedString(L"id")) << '\t'
                          << winrt::to_string(project.GetNamedString(L"name")) << '\t';
                bool first = true;
                for (const auto& app : project.GetNamedArray(L"applications"))
                {
                    if (!first)
                        std::cout << ", ";
                    std::cout << winrt::to_string(app.GetObjectW().GetNamedString(L"application"));
                    first = false;
                }
                std::cout << '\n';
            }
        }
        else if (output.HasKey(L"workspaces"))
            std::cout << winrt::to_string(CliResources::Get(IDS_CLICOMPLETED)) << '\n'
                      << winrt::to_string(output.Stringify()) << '\n';
        else
            std::cout << winrt::to_string(CliResources::Get(output.GetNamedString(L"state") == L"completed" ? IDS_CLICOMPLETED :
                                                            output.GetNamedString(L"state") == L"partial"   ? IDS_CLIPARTIAL :
                                                                                                              IDS_CLIFAILED))
                      << '\n'
                      << winrt::to_string(output.GetNamedObject(L"result").Stringify()) << '\n';
        if (output.HasKey(L"warnings"))
        {
            for (const auto& warning : output.GetNamedArray(L"warnings"))
                std::cerr << winrt::to_string(CliResources::Get(IDS_CLIWARNINGPREFIX)) << winrt::to_string(warning.GetObjectW().GetNamedString(L"message")) << '\n';
        }
    }
}

int wmain(int argc, wchar_t** argv)
{
    const std::vector<std::wstring> args(argv + 1, argv + argc);
    const bool asJson = WorkspacesCli::WantsJson(args);
    const std::wstring command = args.empty() ? L"" : args.front();
    Logger::init(std::vector<spdlog::sink_ptr>{ std::make_shared<spdlog::sinks::null_sink_mt>() });
    Trace::Workspaces::RegisterProvider();
    bool successful = false;
    const auto reportTelemetry = [&] {
        Trace::Workspaces::CLICommand(WorkspacesCli::TelemetryCommandName(args), successful);
        Trace::Workspaces::UnregisterProvider();
    };
    auto telemetry = wil::scope_exit([&] { reportTelemetry(); });
    try
    {
        winrt::init_apartment();
        const auto options = WorkspacesCli::Parse(args);
        if (options.command == L"--help")
        {
            const auto help = winrt::to_string(CliResources::Get(IDS_CLIHELP));
            WorkspacesCli::ConsoleOutputCodePage consoleCodePage;
            std::cout << help << std::flush;
            successful = true;
            return 0;
        }
        if (options.command == L"--version")
        {
            std::cout << "PowerToys Workspaces CLI " << FILE_VERSION_STRING << '\n';
            successful = true;
            return 0;
        }
        int exitCode{};
        if (options.command == L"list")
        {
            WorkspacesCli::CheckEnabled();
            Print(WorkspacesCli::ListResult(WorkspacesCli::Select(WorkspacesCli::LoadWorkspaces(), options), options.details), options.json);
        }
        else
        {
            wil::unique_handle cancel(CreateEventW(nullptr, TRUE, FALSE, nullptr));
            if (!cancel)
                throw WorkspacesCli::Error(1, L"internalError", "Cannot create console cancellation.");
            cancelHandle = cancel.get();
            if (!SetConsoleCtrlHandler(Cancel, TRUE))
                throw WorkspacesCli::Error(1, L"internalError", "Cannot register console cancellation.");
            auto restore = wil::scope_exit([&] {
                SetConsoleCtrlHandler(Cancel, FALSE);
                cancelHandle = nullptr;
            });
            WorkspacesSession::Request request;
            request.id = options.id;
            request.name = options.name;
            request.presentation = WorkspacesSession::Presentation::ConfirmationOnly;
            request.timeoutSeconds = options.timeoutSeconds;
            Print(WorkspacesCli::SessionResult(WorkspacesSession::Start(request, cancel.get()), exitCode), options.json);
        }
        successful = exitCode == 0;
        return exitCode;
    }
    catch (const WorkspacesCli::Error& error)
    {
        Print(WorkspacesCli::Failure(command, error), asJson);
        if (error.executionStillRunning)
        {
            reportTelemetry();
            telemetry.release();
        }
        WorkspacesSession::ExitIfExecutionUnresponsive(error);
        return error.exitCode;
    }
    catch (const std::exception&)
    {
        Print(WorkspacesCli::Failure(command, { 1, L"internalError", "The command could not complete." }), asJson);
        return 1;
    }
    catch (const winrt::hresult_error&)
    {
        Print(WorkspacesCli::Failure(command, { 1, L"internalError", "A Windows operation failed." }), asJson);
        return 1;
    }
}
