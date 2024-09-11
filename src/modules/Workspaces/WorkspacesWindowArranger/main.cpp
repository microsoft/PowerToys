#include "pch.h"

#include <WorkspacesLib/JsonUtils.h>
#include <WorkspacesLib/utils.h>
#include <WorkspacesLib/WorkspacesData.h>

#include <workspaces-common/MonitorUtils.h>

#include <common/utils/gpo.h>
#include <common/utils/logger_helper.h>
#include <common/utils/process_path.h>
#include <common/utils/UnhandledExceptionHandler.h>

#include <ArrangeWindows.h>

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

    if (id.empty())
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
        if (res.isOk() && res.value().id == id)
        {
            projectToLaunch = res.getValue();
        }
        else
        {
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
            if (proj.id == id)
            {
                projectToLaunch = proj;
                break;
            }
        }
    }

    if (projectToLaunch.id.empty())
    {
        Logger::critical(L"Workspace {} not found", id);
        return 1;
    }

    // arrange windows
    Logger::info(L"Arrange widnows from Workspace {} : {}", projectToLaunch.name, projectToLaunch.id);
    auto monitors = MonitorUtils::IdentifyMonitors();
    ArrangeWindows(projectToLaunch, monitors);
    
    CoUninitialize();
    return 0;
}
