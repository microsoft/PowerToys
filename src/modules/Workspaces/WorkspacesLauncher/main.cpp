#include "pch.h"
#include "LaunchSessionHost.h"

#include <shellapi.h>
#include <spdlog/sinks/null_sink.h>
#include <wil/resource.h>
#include <common/utils/UnhandledExceptionHandler.h>
#include <common/utils/logger_helper.h>
#include <common/utils/resources.h>
#include <WorkspacesLib/LaunchSession.h>
#include <WorkspacesLib/utils.h>
#include <Generated Files/resource.h>

int APIENTRY WinMain(HINSTANCE, HINSTANCE, LPSTR, int)
{
    Logger::init(std::vector<spdlog::sink_ptr>{ std::make_shared<spdlog::sinks::null_sink_mt>() });
    InitUnhandledExceptionHandler();
    winrt::init_apartment();
    int count{};
    wil::unique_hlocal_ptr<wchar_t*> argv(CommandLineToArgvW(GetCommandLineW(), &count));
    if (!argv || !count)
        return 1;
    const std::vector<std::wstring> arguments(argv.get() + 1, argv.get() + count);
    if (const auto worker = WorkspacesSession::TryRunHost(arguments))
        return *worker;
    LoggerHelpers::init_logger(L"Workspaces\\WorkspacesLauncher", L"", LogSettings::workspacesLauncherLoggerName);
    const auto command = split(std::wstring(GetCommandLineW()), L" ");
    if (command.workspaceId.empty())
    {
        MessageBoxW(nullptr, GET_RESOURCE_STRING(IDS_INCORRECT_ARGS).c_str(), GET_RESOURCE_STRING(IDS_WORKSPACES).c_str(), MB_ICONERROR | MB_OK);
        return 1;
    }
    try
    {
        WorkspacesSession::Request request;
        request.id = command.workspaceId;
        request.invokePoint = command.invokePoint;
        const auto result = WorkspacesSession::Start(request);
        if (result.persistence && *result.persistence != WorkspaceStore::UpdateResult::Updated)
            Logger::warn("Workspace launch history could not be saved.");
        return 0;
    }
    catch (const WorkspacesSession::Error& error)
    {
        Logger::error("Workspace session failed: {}", error.what());
        Logger::flush();
        WorkspacesSession::ExitIfExecutionUnresponsive(error);
        if (error.code == L"busy" || error.code == L"disabledByPolicy" || error.code == L"canceled")
            return 0;
        std::wstring message;
        if (error.code == L"workspaceNotFound")
            message = fmt::format(GET_RESOURCE_STRING(IDS_PROJECT_NOT_FOUND), command.workspaceId);
        else if (error.code == L"invalidArguments")
            message = GET_RESOURCE_STRING(IDS_INCORRECT_ARGS);
        else if (error.code == L"invalidData")
            message = fmt::format(GET_RESOURCE_STRING(IDS_INCORRECT_FILE_ERROR), WorkspacesData::WorkspacesFile());
        if (!message.empty())
            MessageBoxW(nullptr, message.c_str(), GET_RESOURCE_STRING(IDS_WORKSPACES).c_str(), MB_ICONERROR | MB_OK);
        return 1;
    }
    catch (const winrt::hresult_error& error)
    {
        Logger::error(L"Workspace session Windows error: {}", error.message());
        return 1;
    }
    catch (const std::exception& error)
    {
        Logger::error("Workspace session error: {}", error.what());
        return 1;
    }
}
