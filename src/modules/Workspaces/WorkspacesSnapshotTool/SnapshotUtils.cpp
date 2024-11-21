#include "pch.h"
#include "SnapshotUtils.h"

#include <common/utils/elevation.h>
#include <common/utils/process_path.h>
#include <common/notifications/NotificationUtil.h>

#include <workspaces-common/WindowEnumerator.h>
#include <workspaces-common/WindowFilter.h>

#include <WorkspacesLib/AppUtils.h>
#include <PwaHelper.h>

#pragma comment(lib, "ntdll.lib")

namespace SnapshotUtils
{
    namespace NonLocalizable
    {
        const std::wstring ApplicationFrameHost = L"ApplicationFrameHost.exe";
        const std::wstring EdgeFilename = L"msedge.exe";
        const std::wstring ChromeFilename = L"chrome.exe";
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

    bool IsEdge(Utils::Apps::AppData appData)
    {
        return appData.installPath.ends_with(NonLocalizable::EdgeFilename);
    }

    bool IsChrome(Utils::Apps::AppData appData)
    {
        return appData.installPath.ends_with(NonLocalizable::ChromeFilename);
    }

    std::vector<WorkspacesData::WorkspacesProject::Application> GetApps(const std::function<unsigned int(HWND)> getMonitorNumberFromWindowHandle, const std::function<WorkspacesData::WorkspacesProject::Monitor::MonitorRect(unsigned int)> getMonitorRect)
    {
        PwaHelper pwaHelper{};
        std::vector<WorkspacesData::WorkspacesProject::Application> apps{};

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
                    notifications::WarnIfElevationIsRequired(GET_RESOURCE_STRING(IDS_PROJECTS),
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

            // fix for the packaged apps that are not caught when minimized, e.g., Settings.
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

            if (WindowFilter::FilterPopup(window))
            {
                continue;
            }

            auto data = Utils::Apps::GetApp(processPath, pid, installedApps);
            if (!data.has_value() || data->name.empty())
            {
                Logger::info(L"Installed app not found: {}", processPath);
                continue;
            }

            std::wstring pwaAppId = L"";
            std::wstring finalName = data.value().name;
            std::wstring pwaName = L"";
            if (IsEdge(data.value()))
            {
                pwaHelper.InitAumidToAppId();

                std::wstring windowAumid;
                pwaHelper.GetAppId(window, &windowAumid);
                Logger::info(L"Found an edge window with aumid {}", windowAumid);

                if (pwaHelper.GetPwaAppId(windowAumid, &pwaAppId))  
                {
                    Logger::info(L"The found edge window is a PWA app with appId {}", pwaAppId);
                    if (pwaHelper.SearchPwaName(pwaAppId, windowAumid ,& pwaName))
                    {
                        Logger::info(L"The found edge window is a PWA app with name {}", finalName);
                    }
                    finalName = pwaName + L" (" + finalName + L")";
                }
                else
                {
                    Logger::info(L"The found edge window does not contain a PWA app", pwaAppId);
                }
            }
            else if (IsChrome(data.value()))
            {
                pwaHelper.InitChromeAppIds();

                std::wstring windowAumid;
                pwaHelper.GetAppId(window, &windowAumid);
                Logger::info(L"Found a chrome window with aumid {}", windowAumid);

                if (pwaHelper.SearchPwaAppId(windowAumid, &pwaAppId))
                {
                    if (pwaHelper.SearchPwaName(pwaAppId, windowAumid, &pwaName))
                    {
                        finalName = pwaName + L" (" + finalName + L")";
                    }
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

            WorkspacesData::WorkspacesProject::Application app{
                .name = finalName,
                .title = title,
                .path = data.value().installPath,
                .packageFullName = data.value().packageFullName,
                .appUserModelId = data.value().appUserModelId,
                .pwaAppId = pwaAppId,
                .commandLineArgs = L"",
                .isElevated = IsProcessElevated(pid),
                .canLaunchElevated = data.value().canLaunchElevated,
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