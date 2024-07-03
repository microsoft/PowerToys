#include "pch.h"

#include <chrono>

#include <projects-common/Data.h>
#include <projects-common/GuidUtils.h>
#include <projects-common/MonitorUtils.h>

#include <JsonUtils.h>
#include <NameUtils.h>
#include <SnapshotUtils.h>

#include <common/utils/gpo.h>
#include <common/utils/logger_helper.h>
#include <common/utils/UnhandledExceptionHandler.h>

const std::wstring moduleName = L"Projects\\ProjectsSnapshotTool";
const std::wstring internalPath = L"";

int APIENTRY WinMain(HINSTANCE hInst, HINSTANCE hInstPrev, LPSTR cmdLine, int cmdShow)
{
    LoggerHelpers::init_logger(moduleName, internalPath, LogSettings::projectsLauncherLoggerName);
    InitUnhandledExceptionHandler();

    if (powertoys_gpo::getConfiguredProjectsEnabledValue() == powertoys_gpo::gpo_rule_configured_disabled)
    {
        Logger::warn(L"Tried to start with a GPO policy setting the utility to always be disabled. Please contact your systems administrator.");
        return 0;
    }

    SetProcessDpiAwarenessContext(DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2);

    HRESULT comInitHres = CoInitializeEx(0, COINIT_MULTITHREADED);
    if (FAILED(comInitHres))
    {
        Logger::error(L"Failed to initialize COM library. {}", comInitHres);
        return -1;
    }

    std::wstring fileName = JsonUtils::ProjectsFile();
    std::string cmdLineStr(cmdLine);
    if (!cmdLineStr.empty())
    {
        std::wstring fileNameParam(cmdLineStr.begin(), cmdLineStr.end());
        fileName = fileNameParam;
    }

    // read previously saved projects 
    std::vector<Project> projects = ProjectsJsonUtils::Read(fileName);
    
    // create new project
    time_t creationTime = std::chrono::system_clock::to_time_t(std::chrono::system_clock::now());
    Project project{ .id = CreateGuidString(), .name = ProjectNameUtils::CreateProjectName(projects), .creationTime = creationTime };
    Logger::trace(L"Creating project {}:{}", project.name, project.id);

    project.monitors = MonitorUtils::IdentifyMonitors();
    project.apps = SnapshotUtils::GetApps([&](HWND window) -> unsigned int {
        auto windowMonitor = MonitorFromWindow(window, MONITOR_DEFAULTTOPRIMARY);
        unsigned int monitorNumber = 0;
        for (const auto& monitor : project.monitors)
        {
            if (monitor.monitor == windowMonitor)
            {
                monitorNumber = monitor.number;
                break;
            }
        }

        return monitorNumber;
    });

    projects.push_back(project);
    ProjectsJsonUtils::Write(fileName, projects);
    Logger::trace(L"Project {}:{} created", project.name, project.id);

    return 0;
}
