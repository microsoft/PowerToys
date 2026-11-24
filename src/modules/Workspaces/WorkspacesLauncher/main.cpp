#include "pch.h"
#include "LaunchSessionHost.h"

#include <algorithm>
#include <shellapi.h>
#include <string_view>
#include <spdlog/sinks/null_sink.h>
#include <wil/resource.h>
#include <common/utils/UnhandledExceptionHandler.h>
#include <common/utils/logger_helper.h>
#include <common/utils/resources.h>
#include <WorkspacesLib/CliCommands.h>
#include <WorkspacesLib/LaunchSession.h>
#include <workspaces-common/GuidUtils.h>
#include <Generated Files/resource.h>

namespace
{
    constexpr std::wstring_view EditorDraftPrefix = L"--editor-draft=";

    std::optional<WorkspacesData::WorkspacesProject> ReadEditorDraft(const std::wstring& workspaceId, const std::wstring& token)
    {
        if (token.size() != 38 || token.front() != L'{' || token.back() != L'}')
            return std::nullopt;
        const auto guid = GuidFromString(token);
        if (!guid)
            return std::nullopt;
        const auto canonicalToken = GuidToString(*guid);
        if (CompareStringOrdinal(token.c_str(), -1, canonicalToken.c_str(), -1, TRUE) != CSTR_EQUAL)
            return std::nullopt;

        const auto contents = WorkspacesCli::ReadJson(WorkspacesData::TempWorkspacesFile() + L"." + canonicalToken);
        auto project = contents ? WorkspacesData::WorkspacesProjectJSON::FromJson(*contents) : std::nullopt;
        if (!project || WorkspacesCli::NormalizeId(project->id) != WorkspacesCli::NormalizeId(workspaceId))
            return std::nullopt;
        return project;
    }
}

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
    const auto draftOption = std::ranges::find_if(arguments, [](const auto& arg) { return arg.starts_with(L"--editor-draft"); });
    WorkspacesSession::Request request;
    if (draftOption != arguments.end())
    {
        if (arguments.size() != 3 || draftOption != arguments.begin() + 2 || !draftOption->starts_with(EditorDraftPrefix) ||
            arguments[0].empty() || arguments[1] != std::to_wstring(static_cast<int>(InvokePoint::LaunchAndEdit)))
        {
            Logger::error("Invalid editor draft launch arguments.");
            return 1;
        }
        request.id = arguments[0];
        request.invokePoint = InvokePoint::LaunchAndEdit;
    }
    else
    {
        // Older callers can place the optional invocation point and "restarted" marker on either side of the ID.
        for (const auto& argument : arguments)
        {
            if (argument.size() == 36 || (argument.size() == 38 && argument.front() == L'{' && argument.back() == L'}'))
            {
                if (GuidFromString(argument.size() == 36 ? L"{" + argument + L"}" : argument))
                    request.id = argument;
            }
            else if (argument == L"0")
                request.invokePoint = InvokePoint::EditorButton;
            else if (argument == L"1")
                request.invokePoint = InvokePoint::Shortcut;
            else if (argument == L"2")
                request.invokePoint = InvokePoint::LaunchAndEdit;
        }
        if (request.id.empty())
        {
            MessageBoxW(nullptr, GET_RESOURCE_STRING(IDS_INCORRECT_ARGS).c_str(), GET_RESOURCE_STRING(IDS_WORKSPACES).c_str(), MB_ICONERROR | MB_OK);
            return 1;
        }
    }
    try
    {
        std::optional<WorkspacesData::WorkspacesProject> draft;
        if (draftOption != arguments.end())
        {
            try
            {
                draft = ReadEditorDraft(request.id, draftOption->substr(EditorDraftPrefix.size()));
            }
            catch (const WorkspacesSession::Error& error)
            {
                Logger::error("Cannot read the requested editor draft: {}", error.what());
                return error.exitCode;
            }
            if (!draft)
            {
                Logger::error("The requested editor draft is missing, invalid, or belongs to another workspace.");
                return 1;
            }
        }
        const auto result = draft ? WorkspacesSession::StartSnapshot(request, *draft, true) : WorkspacesSession::Start(request);
        if (result.persistence && *result.persistence != WorkspaceStore::UpdateResult::Updated)
            Logger::warn("Workspace launch history could not be saved.");
        if (request.invokePoint == InvokePoint::LaunchAndEdit)
        {
            return result.CanRecapture() ? 0 : 1;
        }
        return 0;
    }
    catch (const WorkspacesSession::Error& error)
    {
        Logger::error("Workspace session failed: {}", error.what());
        Logger::flush();
        WorkspacesSession::ExitIfExecutionUnresponsive(error);
        if (error.code == L"busy" || error.code == L"disabledByPolicy" || error.code == L"canceled")
        {
            // The editor only starts recapture after a successful Launch and edit.
            return request.invokePoint == InvokePoint::LaunchAndEdit ? error.exitCode : 0;
        }
        std::wstring message;
        if (error.code == L"workspaceNotFound")
            message = fmt::format(GET_RESOURCE_STRING(IDS_PROJECT_NOT_FOUND), request.id);
        else if (error.code == L"invalidArguments")
            message = GET_RESOURCE_STRING(IDS_INCORRECT_ARGS);
        else if (error.code == L"storageError" || error.code == L"invalidData")
        {
            const auto resource = error.code == L"storageError" ? IDS_FILE_READING_ERROR : IDS_INCORRECT_FILE_ERROR;
            message = fmt::format(GET_RESOURCE_STRING(resource), error.fileName.empty() ? WorkspacesData::WorkspacesFile() : error.fileName);
        }
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
