#include "pch.h"

#include <chrono>

#include <workspaces-common/GuidUtils.h>
#include <workspaces-common/MonitorUtils.h>

#include <WorkspacesLib/WorkspacesData.h>

#include <JsonUtils.h>
#include <SnapshotUtils.h>

#include <common/utils/gpo.h>
#include <common/utils/logger_helper.h>
#include <common/utils/UnhandledExceptionHandler.h>

const std::wstring moduleName = L"Workspaces\\ProjectsSnapshotTool";
const std::wstring internalPath = L"";

int APIENTRY WinMain(HINSTANCE hInst, HINSTANCE hInstPrev, LPSTR cmdLine, int cmdShow)
{
    LoggerHelpers::init_logger(moduleName, internalPath, LogSettings::workspacesLauncherLoggerName);
    InitUnhandledExceptionHandler();

    if (powertoys_gpo::getConfiguredWorkspacesEnabledValue() == powertoys_gpo::gpo_rule_configured_disabled)
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

    // Set general COM security levels.
    comInitHres = CoInitializeSecurity(NULL, -1, NULL, NULL, RPC_C_AUTHN_LEVEL_DEFAULT, RPC_C_IMP_LEVEL_IMPERSONATE, NULL, EOAC_NONE, NULL);
    if (FAILED(comInitHres))
    {
        Logger::error(L"Failed to initialize security. Error code: {}", get_last_error_or_default(comInitHres));
        CoUninitialize();
        return -1;
    }

    std::wstring fileName = WorkspacesData::WorkspacesFile();
    std::string cmdLineStr(cmdLine);
    if (!cmdLineStr.empty())
    {
        std::wstring fileNameParam(cmdLineStr.begin(), cmdLineStr.end());
        fileName = fileNameParam;
    }

    // create new project
    time_t creationTime = std::chrono::system_clock::to_time_t(std::chrono::system_clock::now());
    WorkspacesData::WorkspacesProject project{ .id = CreateGuidString(), .creationTime = creationTime };
    Logger::trace(L"Creating workspace {}:{}", project.name, project.id);

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

    WorkspacesJsonUtils::Write(WorkspacesData::TempWorkspacesFile(), project);
    Logger::trace(L"WorkspacesProject {}:{} created", project.name, project.id);

    CoUninitialize();
    return 0;
}
