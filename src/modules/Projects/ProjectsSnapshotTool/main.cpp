#include "pch.h"

#include <chrono>
#include <iostream>

#include "../projects-common/Data.h"
#include "../projects-common/GuidUtils.h"
#include "../projects-common/WindowEnumerator.h"

#include "MonitorUtils.h"
#include "PackagedAppUtils.h"
#include "WindowFilter.h"

int main(int argc, char* argv[])
{
    SetProcessDpiAwarenessContext(DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2);

    HRESULT comInitHres = CoInitializeEx(0, COINIT_MULTITHREADED);
    if (FAILED(comInitHres))
    {
        std::wcout << L"Failed to initialize COM library. " << comInitHres << std::endl;
        return -1;
    }

    std::wstring fileName = JsonUtils::ProjectsFile();
    if (argc > 1)
    {
        std::string fileNameParam = argv[1];
        std::wstring filenameStr(fileNameParam.begin(), fileNameParam.end());
        fileName = filenameStr;
    }

    // read previously saved projects 
    std::vector<Project> projects;
    try
    {
        auto savedProjectsJson = json::from_file(fileName);
        if (savedProjectsJson.has_value())
        {
            auto savedProjects = JsonUtils::ProjectsListJSON::FromJson(savedProjectsJson.value());
            if (savedProjects.has_value())
            {
                projects = savedProjects.value();
            }
        }
    }
    catch (std::exception)
    {
    }
    
    // new project name
    std::wstring defaultNamePrefix = L"Project"; // TODO: localizable
    int nextProjectIndex = 0;
    for (const auto& proj : projects)
    {
        const std::wstring& name = proj.name;
        if (name.starts_with(defaultNamePrefix))
        {
            try
            {
                int index = std::stoi(name.substr(defaultNamePrefix.length() + 1));
                if (nextProjectIndex < index)
                {
                    nextProjectIndex = index;
                }
            }
            catch (std::exception) {}
        }
    }

    std::wstring projectName = defaultNamePrefix + L" " + std::to_wstring(nextProjectIndex + 1);
    time_t creationTime = std::chrono::system_clock::to_time_t(std::chrono::system_clock::now());
    Project project{ .id = CreateGuidString(), .name = projectName, .creationTime = creationTime };

    // save monitor configuration
    project.monitors = MonitorUtils::IdentifyMonitors();

    // get list of windows
    auto windows = WindowEnumerator::Enumerate(WindowFilter::Filter);

    // get installed apps list
    auto apps = Utils::Apps::GetAppsList();

    for (const auto& window : windows)
    {
        // filter by window rect size
        RECT rect = WindowUtils::GetWindowRect(window);
        if (rect.right - rect.left <= 0 || rect.bottom - rect.top <= 0)
        {
            continue;
        }

        // filter by window title
        std::wstring title = WindowUtils::GetWindowTitle(window);
        if (title.empty())
        {
            continue;
        }

        // filter by app path
        std::wstring processPath = Common::Utils::ProcessPath::get_process_path_waiting_uwp(window);
        if (processPath.empty() || WindowUtils::IsExcludedByDefault(window, processPath, title))
        {
            continue;
        }

        auto data = Utils::Apps::GetApp(processPath, apps);
        if (!data.has_value())
        {
            continue;
        }

        auto windowMonitor = MonitorFromWindow(window, MONITOR_DEFAULTTOPRIMARY);
        int monitorNumber = 0;
        for (const auto& monitor : project.monitors)
        {
            if (monitor.monitor == windowMonitor)
            {
                monitorNumber = monitor.number;
                break;
            }
        }

        Project::Application app {
            .name = data.value().name,
            .title = title,
            .path = processPath,
            .packageFullName = data.value().packageFullName,
            .commandLineArgs = L"",
            .isMinimized = WindowUtils::IsMinimized(window),
            .isMaximized = WindowUtils::IsMaximized(window),
            .position = Project::Application::Position {
                .x = rect.left,
                .y = rect.top,
                .width = rect.right - rect.left,
                .height = rect.bottom - rect.top,
            },
            .monitor = monitorNumber,
        };

        project.apps.push_back(app);
    }

    projects.push_back(project);
    json::to_file(fileName, JsonUtils::ProjectsListJSON::ToJson(projects));
    return 0;
}
