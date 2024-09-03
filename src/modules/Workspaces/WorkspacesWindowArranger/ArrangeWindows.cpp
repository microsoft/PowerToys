#include "pch.h"
#include "ArrangeWindows.h"

#include <ShellScalingApi.h>

#include <common/Display/dpi_aware.h>
#include <common/utils/winapi_error.h>

#include <workspaces-common/MonitorEnumerator.h>
#include <workspaces-common/WindowEnumerator.h>
#include <workspaces-common/WindowFilter.h>

#include <WindowProperties/WorkspacesWindowPropertyUtils.h>

#include <WorkspacesLib/AppUtils.h>

#include <LaunchingApp.h>

namespace FancyZones
{
    inline bool allMonitorsHaveSameDpiScaling()
    {
        auto monitors = MonitorEnumerator::Enumerate();
        if (monitors.size() < 2)
        {
            return true;
        }

        UINT firstMonitorDpiX;
        UINT firstMonitorDpiY;

        if (S_OK != GetDpiForMonitor(monitors[0].first, MDT_EFFECTIVE_DPI, &firstMonitorDpiX, &firstMonitorDpiY))
        {
            return false;
        }

        for (int i = 1; i < monitors.size(); i++)
        {
            UINT iteratedMonitorDpiX;
            UINT iteratedMonitorDpiY;

            if (S_OK != GetDpiForMonitor(monitors[i].first, MDT_EFFECTIVE_DPI, &iteratedMonitorDpiX, &iteratedMonitorDpiY) ||
                iteratedMonitorDpiX != firstMonitorDpiX)
            {
                return false;
            }
        }

        return true;
    }

    inline void ScreenToWorkAreaCoords(HWND window, HMONITOR monitor, RECT& rect)
    {
        MONITORINFOEXW monitorInfo{ sizeof(MONITORINFOEXW) };
        GetMonitorInfoW(monitor, &monitorInfo);

        auto xOffset = monitorInfo.rcWork.left - monitorInfo.rcMonitor.left;
        auto yOffset = monitorInfo.rcWork.top - monitorInfo.rcMonitor.top;

        DPIAware::Convert(monitor, rect);

        auto referenceRect = RECT(rect.left - xOffset, rect.top - yOffset, rect.right - xOffset, rect.bottom - yOffset);

        // Now, this rect should be used to determine the monitor and thus taskbar size. This fixes
        // scenarios where the zone lies approximately between two monitors, and the taskbar is on the left.
        monitor = MonitorFromRect(&referenceRect, MONITOR_DEFAULTTOPRIMARY);
        GetMonitorInfoW(monitor, &monitorInfo);

        xOffset = monitorInfo.rcWork.left - monitorInfo.rcMonitor.left;
        yOffset = monitorInfo.rcWork.top - monitorInfo.rcMonitor.top;

        rect.left -= xOffset;
        rect.right -= xOffset;
        rect.top -= yOffset;
        rect.bottom -= yOffset;
    }

    inline bool SizeWindowToRect(HWND window, HMONITOR monitor, bool isMinimized, bool isMaximized, RECT rect) noexcept
    {
        WINDOWPLACEMENT placement{};
        ::GetWindowPlacement(window, &placement);

        if (isMinimized)
        {
            placement.showCmd = SW_MINIMIZE;
        }
        else
        {
            if ((placement.showCmd != SW_SHOWMINIMIZED) &&
                (placement.showCmd != SW_MINIMIZE))
            {
                if (placement.showCmd == SW_SHOWMAXIMIZED)
                    placement.flags &= ~WPF_RESTORETOMAXIMIZED;

                placement.showCmd = SW_RESTORE;
            }

            ScreenToWorkAreaCoords(window, monitor, rect);
            placement.rcNormalPosition = rect;
        }

        placement.flags |= WPF_ASYNCWINDOWPLACEMENT;

        auto result = ::SetWindowPlacement(window, &placement);
        if (!result)
        {
            Logger::error(L"SetWindowPlacement failed, {}", get_last_error_or_default(GetLastError()));
            return false;
        }

        // make sure window is moved to the correct monitor before maximize.
        if (isMaximized)
        {
            placement.showCmd = SW_SHOWMAXIMIZED;
        }

        // Do it again, allowing Windows to resize the window and set correct scaling
        // This fixes Issue #365
        result = ::SetWindowPlacement(window, &placement);
        if (!result)
        {
            Logger::error(L"SetWindowPlacement failed, {}", get_last_error_or_default(GetLastError()));
            return false;
        }

        return true;
    }
}

namespace
{
    LaunchingApps Prepare(std::vector<WorkspacesData::WorkspacesProject::Application>& apps, const Utils::Apps::AppList& installedApps)
    {
        LaunchingApps launchedApps{};
        launchedApps.reserve(apps.size());

        for (auto& app : apps)
        {
            launchedApps.push_back({ app, nullptr, L"waiting" });
        }

        return launchedApps;
    }

    bool AllWindowsFound(const LaunchingApps& launchedApps)
    {
        return std::find_if(launchedApps.begin(), launchedApps.end(), [&](const LaunchingApp& val) {
                   return val.window == nullptr;
               }) == launchedApps.end();
    };

    bool AddOpenedWindows(LaunchingApps& launchedApps, const std::vector<HWND>& windows, const Utils::Apps::AppList& installedApps)
    {
        bool statusChanged = false;
        for (HWND window : windows)
        {
            auto installedAppData = Utils::Apps::GetApp(window, installedApps);
            if (!installedAppData.has_value())
            {
                continue;
            }

            auto insertionIter = launchedApps.end();
            for (auto iter = launchedApps.begin(); iter != launchedApps.end(); ++iter)
            {
                if (iter->window == nullptr && installedAppData.value().name == iter->application.name)
                {
                    insertionIter = iter;
                }

                // keep the window at the same position if it's already opened
                WINDOWPLACEMENT placement{};
                ::GetWindowPlacement(window, &placement);
                HMONITOR monitor = MonitorFromWindow(window, MONITOR_DEFAULTTOPRIMARY);
                UINT dpi = DPIAware::DEFAULT_DPI;
                DPIAware::GetScreenDPIForMonitor(monitor, dpi);

                float x = static_cast<float>(placement.rcNormalPosition.left);
                float y = static_cast<float>(placement.rcNormalPosition.top);
                float width = static_cast<float>(placement.rcNormalPosition.right - placement.rcNormalPosition.left);
                float height = static_cast<float>(placement.rcNormalPosition.bottom - placement.rcNormalPosition.top);

                DPIAware::InverseConvert(monitor, x, y);
                DPIAware::InverseConvert(monitor, width, height);

                WorkspacesData::WorkspacesProject::Application::Position windowPosition{
                    .x = static_cast<int>(std::round(x)),
                    .y = static_cast<int>(std::round(y)),
                    .width = static_cast<int>(std::round(width)),
                    .height = static_cast<int>(std::round(height)),
                };
                if (iter->application.position == windowPosition)
                {
                    Logger::debug(L"{} window already found at {} {}.", iter->application.name, iter->application.position.x, iter->application.position.y);
                    insertionIter = iter;
                    break;
                }
            }

            if (insertionIter != launchedApps.end())
            {
                insertionIter->window = window;
                insertionIter->state = L"launched";
                statusChanged = true;
            }

            if (AllWindowsFound(launchedApps))
            {
                break;
            }
        }
        return statusChanged;
    }
}

bool ArrangeWindows(WorkspacesData::WorkspacesProject& project, const std::vector<WorkspacesData::WorkspacesProject::Monitor>& monitors)
{
    bool success { true };

    // Get newly opened windows after launching apps, keep retrying for 5 seconds
    Logger::trace(L"Find new windows");

    // Get the set of windows before launching the app
    std::vector<HWND> windowsBefore = WindowEnumerator::Enumerate(WindowFilter::Filter);
    auto installedApps = Utils::Apps::GetAppsList();
    auto launchedApps = Prepare(project.apps, installedApps);

    for (int attempt = 0; attempt < 50 && !AllWindowsFound(launchedApps); attempt++)
    {
        std::vector<HWND> windowsAfter = WindowEnumerator::Enumerate(WindowFilter::Filter);
        std::vector<HWND> windowsDiff{};
        std::copy_if(windowsAfter.begin(), windowsAfter.end(), std::back_inserter(windowsDiff), [&](HWND window) { return std::find(windowsBefore.begin(), windowsBefore.end(), window) == windowsBefore.end(); });
        if (AddOpenedWindows(launchedApps, windowsDiff, installedApps))
        {
            // uiHelper.UpdateLaunchStatus(launchedApps);
        }

        // check if all windows were found
        if (AllWindowsFound(launchedApps))
        {
            Logger::trace(L"All windows found.");
            break;
        }
        else
        {
            Logger::trace(L"Not all windows found, retry.");
            std::this_thread::sleep_for(std::chrono::milliseconds(100));
        }
    }

    // Check single-instance app windows
    Logger::trace(L"Find single-instance app windows");
    if (!AllWindowsFound(launchedApps))
    {
        if (AddOpenedWindows(launchedApps, WindowEnumerator::Enumerate(WindowFilter::Filter), installedApps))
        {
            // uiHelper.UpdateLaunchStatus(launchedApps);
        }
    }

    // Place windows
    for (const auto& [app, window, status] : launchedApps)
    {
        if (window == nullptr)
        {
            Logger::warn(L"{} window not found.", app.name);
            // launchedSuccessfully = false;
            success = false;
            continue;
        }

        auto snapMonitorIter = std::find_if(project.monitors.begin(), project.monitors.end(), [&](const WorkspacesData::WorkspacesProject::Monitor& val) { return val.number == app.monitor; });
        if (snapMonitorIter == project.monitors.end())
        {
            Logger::error(L"No monitor saved for launching the app");
            continue;
        }

        bool launchMinimized = app.isMinimized;
        bool launchMaximized = app.isMaximized;

        HMONITOR currentMonitor{};
        UINT currentDpi = DPIAware::DEFAULT_DPI;
        auto currentMonitorIter = std::find_if(monitors.begin(), monitors.end(), [&](const WorkspacesData::WorkspacesProject::Monitor& val) { return val.number == app.monitor; });
        if (currentMonitorIter != monitors.end())
        {
            currentMonitor = currentMonitorIter->monitor;
            currentDpi = currentMonitorIter->dpi;
        }
        else
        {
            currentMonitor = MonitorFromPoint(POINT{ 0, 0 }, MONITOR_DEFAULTTOPRIMARY);
            DPIAware::GetScreenDPIForMonitor(currentMonitor, currentDpi);
            launchMinimized = true;
            launchMaximized = false;
        }

        RECT rect = app.position.toRect();
        float mult = static_cast<float>(snapMonitorIter->dpi) / currentDpi;
        rect.left = static_cast<long>(std::round(rect.left * mult));
        rect.right = static_cast<long>(std::round(rect.right * mult));
        rect.top = static_cast<long>(std::round(rect.top * mult));
        rect.bottom = static_cast<long>(std::round(rect.bottom * mult));

        if (FancyZones::SizeWindowToRect(window, currentMonitor, launchMinimized, launchMaximized, rect))
        {
            WorkspacesWindowProperties::StampWorkspacesLaunchedProperty(window);
            Logger::trace(L"Placed {} to ({},{}) [{}x{}]", app.name, rect.left, rect.top, rect.right - rect.left, rect.bottom - rect.top);
        }
        else
        {
            Logger::error(L"Failed placing {}", app.name);
            success = false;
            // launchedSuccessfully = false;
        }
    }

    return success;
}