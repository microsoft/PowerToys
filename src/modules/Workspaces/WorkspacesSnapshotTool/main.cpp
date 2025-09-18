#include "pch.h"

#include <algorithm>
#include <chrono>
#include <filesystem>
#include <vector>

#include <workspaces-common/GuidUtils.h>
#include <workspaces-common/MonitorUtils.h>

#include <WorkspacesLib/JsonUtils.h>
#include <WorkspacesLib/WorkspacesData.h>

#include <SnapshotUtils.h>

#include <common/utils/gpo.h>
#include <common/utils/logger_helper.h>
#include <common/utils/UnhandledExceptionHandler.h>
#include <WorkspacesLib/utils.h>

const std::wstring moduleName = L"Workspaces\\WorkspacesSnapshotTool";
const std::wstring internalPath = L"";

int APIENTRY WinMain(HINSTANCE hInst, HINSTANCE hInstPrev, LPSTR cmdLine, int cmdShow)
{
    LoggerHelpers::init_logger(moduleName, internalPath, LogSettings::workspacesSnapshotToolLoggerName);
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

    std::wstring cmdLineStr{ GetCommandLineW() };
    Logger::info(L"Raw command line: '{}'", cmdLineStr);
    auto cmdArgs = split(cmdLineStr, L" ");
    Logger::info(L"Command line arguments parsed: workspaceId='{}', forceSave={}, skipMinimized={}", cmdArgs.workspaceId, cmdArgs.forceSave, cmdArgs.skipMinimized);

    // create new project
    time_t creationTime = std::chrono::system_clock::to_time_t(std::chrono::system_clock::now());
    WorkspacesData::WorkspacesProject project;
    project.id = CreateGuidString();
    project.creationTime = creationTime;
    project.name = L"Workspace"; // Default name
    if (!cmdArgs.workspaceId.empty())
    {
        project.id = cmdArgs.workspaceId;
        project.name = cmdArgs.workspaceId; // Use the workspaceId as the name too
    }
    Logger::trace(L"Creating workspace {}:{}", project.name, project.id);

    project.monitors = MonitorUtils::IdentifyMonitors();
    bool isGuidNeeded = false; // Simplified since we removed invokePoint handling
    project.apps = SnapshotUtils::GetApps(isGuidNeeded, cmdArgs.skipMinimized, [&](HWND window) -> unsigned int {
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
        }, [&](unsigned int monitorId) -> WorkspacesData::WorkspacesProject::Monitor::MonitorRect {
        for (const auto& monitor : project.monitors)
        {
            if (monitor.number == monitorId)
            {
                return monitor.monitorRectDpiUnaware;
            }
        }
        return project.monitors[0].monitorRectDpiUnaware; });

    const auto tempWorkspacesFile = WorkspacesData::TempWorkspacesFile();

    Logger::info(L"Force save mode: {}", cmdArgs.forceSave);
    Logger::info(L"Temp workspaces file path: {}", tempWorkspacesFile);

    if (cmdArgs.forceSave)
    {
        const auto workspacesFilePath = WorkspacesData::WorkspacesFile();
        Logger::info(L"Force save enabled. Target file: {}", workspacesFilePath);
        std::vector<WorkspacesData::WorkspacesProject> workspaces;

        if (std::filesystem::exists(workspacesFilePath))
        {
            Logger::info(L"Existing workspaces file found, reading...");
            auto readResult = JsonUtils::ReadWorkspaces(workspacesFilePath);
            if (readResult.isOk())
            {
                workspaces = readResult.getValue();
                Logger::info(L"Successfully read {} existing workspaces", workspaces.size());
            }
            else
            {
                Logger::error(L"Failed to read existing workspaces file {}. Saving snapshot to {} instead.", workspacesFilePath, tempWorkspacesFile);
                if (!JsonUtils::Write(tempWorkspacesFile, project))
                {
                    Logger::error(L"Failed to write workspace snapshot to fallback path {}", tempWorkspacesFile);
                }
                CoUninitialize();
                return -1;
            }
        }
        else
        {
            Logger::info(L"No existing workspaces file found, creating new one");
        }

        bool replaced = false;
        if (!project.id.empty())
        {
            const auto existing = std::find_if(workspaces.begin(), workspaces.end(), [&](const auto& existingProject) {
                return existingProject.id == project.id;
                });
            if (existing != workspaces.end())
            {
                Logger::info(L"Replacing existing workspace with id: {}", project.id);
                *existing = project;
                replaced = true;
            }
        }

        if (!replaced)
        {
            if (project.id.empty())
            {
                project.id = CreateGuidString();
            }

            while (std::any_of(workspaces.begin(), workspaces.end(), [&](const auto& existingProject) {
                return existingProject.id == project.id;
                }))
            {
                project.id = CreateGuidString();
            }

            Logger::info(L"Adding new workspace with id: {}", project.id);
            workspaces.push_back(project);
        }

        Logger::info(L"Writing {} workspaces to file: {}", workspaces.size(), workspacesFilePath);
        if (!JsonUtils::Write(workspacesFilePath, workspaces))
        {
            Logger::error(L"Failed to write workspace snapshot to {}", workspacesFilePath);
            if (!JsonUtils::Write(tempWorkspacesFile, project))
            {
                Logger::error(L"Failed to write workspace snapshot to fallback path {}", tempWorkspacesFile);
            }
            CoUninitialize();
            return -1;
        }

        Logger::info(L"Successfully saved workspace snapshot to {} with id {}", workspacesFilePath, project.id);
    }
    else
    {
        Logger::info(L"Force save disabled, writing to temp file: {}", tempWorkspacesFile);
        if (!JsonUtils::Write(tempWorkspacesFile, project))
        {
            Logger::error(L"Failed to write workspace snapshot to {}", tempWorkspacesFile);
            CoUninitialize();
            return -1;
        }
        Logger::info(L"Successfully saved workspace snapshot to temp file");
    }

    Logger::trace(L"WorkspacesProject {}:{} created", project.name, project.id);

    CoUninitialize();
    return 0;
}
