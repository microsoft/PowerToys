#include "pch.h"
#include "SnapshotUtils.h"

#include <Psapi.h>

#include <projects-common/AppUtils.h>
#include <projects-common/WindowEnumerator.h>
#include <projects-common/WindowFilter.h>

#include <common/utils/process_path.h>
#include <TlHelp32.h>

namespace SnapshotUtils
{
    std::vector<Project::Application> GetApps(const std::function<unsigned int(HWND)> getMonitorNumberFromWindowHandle)
    {
        std::vector<Project::Application> apps{};

        auto installedApps = Utils::Apps::GetAppsList();
        auto windows = WindowEnumerator::Enumerate(WindowFilter::Filter);

        for (const auto window : windows)
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
            std::wstring processPath = get_process_path_waiting_uwp(window);
            if (processPath.empty() || WindowUtils::IsExcludedByDefault(window, processPath, title))
            {
                continue;
            }

            auto data = Utils::Apps::GetApp(processPath, installedApps);
            if (!data.has_value() || data->name.empty())
            {
                continue;
            }

            Project::Application app{
                .name = data.value().name,
                .title = title,
                .path = processPath,
                .packageFullName = data.value().packageFullName,
                .commandLineArgs = L"",
                .isMinimized = WindowUtils::IsMinimized(window),
                .isMaximized = WindowUtils::IsMaximized(window),
                .position = Project::Application::Position{
                    .x = rect.left,
                    .y = rect.top,
                    .width = rect.right - rect.left,
                    .height = rect.bottom - rect.top,
                },
                .monitor = getMonitorNumberFromWindowHandle(window),
            };

            apps.push_back(app);
        }

        return apps;
    }
}