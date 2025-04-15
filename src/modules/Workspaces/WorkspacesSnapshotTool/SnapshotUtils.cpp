#include "pch.h"
#include "SnapshotUtils.h"

#include <common/utils/elevation.h>
#include <common/utils/process_path.h>
#include <common/utils/resources.h>
#include <common/notifications/NotificationUtil.h>

#include <workspaces-common/WindowEnumerator.h>
#include <workspaces-common/WindowFilter.h>

#include <WorkspacesLib/AppUtils.h>
#include <WorkspacesLib/PwaHelper.h>
#include <WindowProperties/WorkspacesWindowPropertyUtils.h>

#include "Generated Files/resource.h"

#pragma comment(lib, "ntdll.lib")

namespace SnapshotUtils
{
    namespace NonLocalizable
    {
        const std::wstring ApplicationFrameHost = L"ApplicationFrameHost.exe";
    }

    bool IsProcessElevated(DWORD processID)
    {
        wil::unique_handle hProcess{ OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION, FALSE, processID) };
        wil::unique_handle token;

        if (OpenProcessToken(hProcess.get(), TOKEN_QUERY, &token))
        {
            TOKEN_ELEVATION elevation;
            DWORD size;
            if (GetTokenInformation(token.get(), TokenElevation, &elevation, sizeof(elevation), &size))
            {
                return elevation.TokenIsElevated != 0;
            }
        }

        return false;
    }

    std::vector<WorkspacesData::WorkspacesProject::Application> GetApps(bool isGuidNeeded, const std::function<unsigned int(HWND)> getMonitorNumberFromWindowHandle, const std::function<WorkspacesData::WorkspacesProject::Monitor::MonitorRect(unsigned int)> getMonitorRect)
    {
        Utils::PwaHelper pwaHelper{};
        std::vector<WorkspacesData::WorkspacesProject::Application> apps{};

        auto installedApps = Utils::Apps::GetAppsList();
        auto windows = WindowEnumerator::Enumerate(WindowFilter::Filter);

        for (const auto window : windows)
        {
            if (WindowFilter::FilterPopup(window))
            {
                continue;
            }

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

            DWORD pid{};
            GetWindowThreadProcessId(window, &pid);

            // filter by app path
            std::wstring processPath = get_process_path(window);
            if (processPath.empty())
            {
                // When PT runs not as admin, it can't get the process path of the window of the elevated process.
                // Notify the user that running as admin is required to process elevated windows.
                if (!is_process_elevated() && IsProcessElevated(pid))
                {
                    auto notificationUtil = std::make_unique<notifications::NotificationUtil>();

                    notificationUtil->WarnIfElevationIsRequired(GET_RESOURCE_STRING(IDS_PROJECTS),
                                                                GET_RESOURCE_STRING(IDS_SYSTEM_FOREGROUND_ELEVATED),
                                                                GET_RESOURCE_STRING(IDS_SYSTEM_FOREGROUND_ELEVATED_LEARN_MORE),
                                                                GET_RESOURCE_STRING(IDS_SYSTEM_FOREGROUND_ELEVATED_DIALOG_DONT_SHOW_AGAIN));
                }

                continue;
            }

            if (WindowUtils::IsExcludedByDefault(window, processPath))
            {
                continue;
            }

            // fix for the packaged apps that are not caught when minimized, e.g. Settings, Microsoft ToDo, ...
            if (processPath.ends_with(NonLocalizable::ApplicationFrameHost))
            {
                for (auto otherWindow : windows)
                {
                    DWORD otherPid{};
                    GetWindowThreadProcessId(otherWindow, &otherPid);

                    // searching for the window with the same title but different PID
                    if (pid != otherPid && title == WindowUtils::GetWindowTitle(otherWindow))
                    {
                        processPath = get_process_path(otherPid);
                        break;
                    }
                }
            }

            auto data = Utils::Apps::GetApp(processPath, pid, installedApps);
            if (!data.has_value() || data->name.empty())
            {
                Logger::info(L"Installed app not found: {}", processPath);
                continue;
            }

            auto appData = data.value();

            bool isEdge = appData.IsEdge();
            bool isChrome = appData.IsChrome();
            if (isEdge || isChrome)
            {
                auto windowAumid = pwaHelper.GetAUMIDFromWindow(window);
                std::optional<std::wstring> pwaAppId{};

                if (isEdge)
                {
                    pwaAppId = pwaHelper.GetEdgeAppId(windowAumid);
                }
                else if (isChrome)
                {
                    pwaAppId = pwaHelper.GetChromeAppId(windowAumid);
                }

                if (pwaAppId.has_value())
                {
                    auto pwaName = pwaHelper.SearchPwaName(pwaAppId.value(), windowAumid);
                    Logger::info(L"Found {} PWA app with name {}, appId: {}", (isEdge ? L"Edge" : (isChrome ? L"Chrome" : L"unknown")), pwaName, pwaAppId.value());

                    appData.pwaAppId = pwaAppId.value();
                    appData.name = pwaName + L" (" + appData.name + L")";
                }
            }

            bool isMinimized = WindowUtils::IsMinimized(window);
            unsigned int monitorNumber = getMonitorNumberFromWindowHandle(window);

            if (isMinimized)
            {
                // set the screen area as position, the values we get for the minimized windows are out of the screens' area
                WorkspacesData::WorkspacesProject::Monitor::MonitorRect monitorRect = getMonitorRect(monitorNumber);
                rect.left = monitorRect.left;
                rect.top = monitorRect.top;
                rect.right = monitorRect.left + monitorRect.width;
                rect.bottom = monitorRect.top + monitorRect.height;
            }

            std::wstring guid = isGuidNeeded ? WorkspacesWindowProperties::GetGuidFromHwnd(window) : L"";

            WorkspacesData::WorkspacesProject::Application app{
                .id = guid,
                .name = appData.name,
                .title = title,
                .path = appData.installPath,
                .packageFullName = appData.packageFullName,
                .appUserModelId = appData.appUserModelId,
                .pwaAppId = appData.pwaAppId,
                .commandLineArgs = L"",
                .isElevated = IsProcessElevated(pid),
                .canLaunchElevated = appData.canLaunchElevated,
                .isMinimized = isMinimized,
                .isMaximized = WindowUtils::IsMaximized(window),
                .position = WorkspacesData::WorkspacesProject::Application::Position{
                    .x = rect.left,
                    .y = rect.top,
                    .width = rect.right - rect.left,
                    .height = rect.bottom - rect.top,
                },
                .monitor = monitorNumber,
            };

            apps.push_back(app);
        }

        return apps;
    }
}