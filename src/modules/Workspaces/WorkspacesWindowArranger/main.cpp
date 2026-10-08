#include "pch.h"

#include <WorkspacesLib/JsonUtils.h>
#include <WorkspacesLib/IPCHelper.h>
#include <WorkspacesLib/utils.h>

#include <common/utils/gpo.h>
#include <common/utils/logger_helper.h>
#include <common/utils/UnhandledExceptionHandler.h>
#include <common/utils/window.h>

#include <WindowArranger.h>
#include <WorkspacesLib/CliCommands.h>
#include <WorkspacesLib/OperationLifetime.h>
#include <condition_variable>
#include <shellapi.h>
#include <spdlog/sinks/null_sink.h>

namespace
{
    int RunCliArranger(int argc, wchar_t** argv)
    {
        Logger::init(std::vector<spdlog::sink_ptr>{ std::make_shared<spdlog::sinks::null_sink_mt>() });
        try
        {
            winrt::init_apartment();
            if (argc != 6)
                throw WorkspacesCli::Error(2, L"invalidArguments", "Invalid CLI arranger arguments.");
            const auto operationId = WorkspacesCli::NormalizeId(argv[3]);
            const auto parentPid = std::stoul(argv[4]);
            const auto seconds = std::stoul(argv[5]);
            if (!parentPid || !seconds || seconds > 600)
                throw WorkspacesCli::Error(2, L"invalidArguments", "Invalid operation lifetime.");
            WorkspacesCli::OperationLifetime lifetime(parentPid, seconds * 1000 + 5000);
            WorkspacesCli::CheckEnabled();
            SetProcessDpiAwarenessContext(DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2);
            std::mutex mutex;
            std::condition_variable received;
            std::optional<WorkspacesData::WorkspacesProject> project;
            bool invalid = false;
            auto pipe = std::make_unique<IPCHelper>(
                IPCHelperStrings::WindowArrangerPipeName + operationId,
                IPCHelperStrings::LauncherArrangerPipeName + operationId,
                [&](const std::wstring& message) {
                    std::lock_guard lock(mutex);
                    try
                    {
                        if (message.size() > WorkspacesCli::MaxPayload)
                            throw std::runtime_error("Oversized snapshot");
                        const auto snapshot = json::JsonObject::Parse(message);
                        if (snapshot.GetNamedNumber(L"protocolVersion") != 1 || snapshot.GetNamedString(L"operationId") != operationId)
                            throw std::runtime_error("Invalid snapshot protocol");
                        project = WorkspacesData::WorkspacesProjectJSON::FromJson(snapshot.GetNamedObject(L"workspace"));
                        invalid = !project || WorkspacesCli::NormalizeId(project->id) != WorkspacesCli::NormalizeId(argv[1]);
                    }
                    catch (const std::exception&)
                    {
                        invalid = true;
                    }
                    catch (const winrt::hresult_error&)
                    {
                        invalid = true;
                    }
                    received.notify_one();
                },
                IPCHelper::ModulePeer(L"PowerToys.WorkspacesCLI.exe", parentPid));
            pipe->send(L"snapshot-request");
            {
                std::unique_lock lock(mutex);
                if (!received.wait_for(lock, std::chrono::seconds(10), [&] { return project.has_value() || invalid; }) || invalid)
                    throw WorkspacesCli::Error(9, L"outcomeUnknown", "No valid operation snapshot received.");
            }
            WindowArranger arranger(*project, std::move(pipe), operationId);
            return 0;
        }
        catch (const WorkspacesCli::Error& error)
        {
            return error.exitCode;
        }
        catch (const winrt::hresult_error&)
        {
            OutputDebugStringW(L"Workspaces CLI: arranger Windows operation failed.\n");
            return 1;
        }
        catch (const std::exception&)
        {
            OutputDebugStringW(L"Workspaces CLI: invalid arranger operation.\n");
            return 1;
        }
    }
}

const std::wstring moduleName = L"Workspaces\\WorkspacesWindowArranger";
const std::wstring internalPath = L"";

int APIENTRY WinMain(HINSTANCE hInst, HINSTANCE hInstPrev, LPSTR cmdline, int cmdShow)
{
    int argc = 0;
    auto argv = CommandLineToArgvW(GetCommandLineW(), &argc);
    auto freeArguments = wil::scope_exit([&] { LocalFree(argv); });
    if (argv && argc > 2 && std::wstring_view(argv[2]) == L"--cli-worker")
        return RunCliArranger(argc, argv);
    LoggerHelpers::init_logger(moduleName, internalPath, LogSettings::workspacesWindowArrangerLoggerName);
    InitUnhandledExceptionHandler();  

    if (powertoys_gpo::getConfiguredWorkspacesEnabledValue() == powertoys_gpo::gpo_rule_configured_disabled)
    {
        Logger::warn(L"Tried to start with a GPO policy setting the utility to always be disabled. Please contact your systems administrator.");
        return 0;
    }

    SetProcessDpiAwarenessContext(DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2);
    
    std::wstring commandLine{ GetCommandLineW() };
    if (commandLine.empty())
    {
        Logger::warn("Empty command line arguments");
        return 1;
    }

    auto args = split(commandLine, L" ");
    if (args.workspaceId.empty())
    {
        Logger::warn("Incorrect command line arguments: no workspace id");
        return 1;
    }

    // read workspaces
    std::vector<WorkspacesData::WorkspacesProject> workspaces;
    WorkspacesData::WorkspacesProject projectToLaunch{};

    // check the temp file in case the project is just created and not saved to the workspaces.json yet
    if (std::filesystem::exists(WorkspacesData::TempWorkspacesFile()))
    {
        auto file = WorkspacesData::TempWorkspacesFile();
        auto res = JsonUtils::ReadSingleWorkspace(file);
        if (res.isOk() && res.value().id == args.workspaceId)
        {
            projectToLaunch = res.getValue();
        }
        else if (res.isError())
        {
            Logger::error(L"Error reading temp file");
            return 1;
        }
    }
    
    if (projectToLaunch.id.empty())
    {
        auto file = WorkspacesData::WorkspacesFile();
        auto res = JsonUtils::ReadWorkspaces(file);
        if (res.isOk())
        {
            workspaces = res.getValue();
        }
        else
        {
            return 1;
        }

        for (const auto& proj : workspaces)
        {
            if (proj.id == args.workspaceId)
            {
                projectToLaunch = proj;
                break;
            }
        }
    }

    if (projectToLaunch.id.empty())
    {
        Logger::critical(L"Workspace {} not found", args.workspaceId);
        return 1;
    }
    
    // arrange windows
    Logger::info(L"Arrange windows from Workspace {} : {}", projectToLaunch.name, projectToLaunch.id);
    WindowArranger windowArranger(projectToLaunch);
    //run_message_loop();
    
    Logger::debug(L"Arranger finished");
    
    CoUninitialize();
    return 0;
}
