#include "pch.h"

#include <WorkspacesLib/JsonUtils.h>
#include <WorkspacesLib/IPCHelper.h>
#include <WorkspacesLib/utils.h>

#include <common/utils/gpo.h>
#include <common/utils/logger_helper.h>
#include <common/utils/UnhandledExceptionHandler.h>
#include <common/utils/window.h>

#include <WindowArranger.h>

const std::wstring moduleName = L"Workspaces\\WorkspacesWindowArranger";
const std::wstring internalPath = L"";

int APIENTRY WinMain(HINSTANCE hInst, HINSTANCE hInstPrev, LPSTR cmdline, int cmdShow)
{
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
