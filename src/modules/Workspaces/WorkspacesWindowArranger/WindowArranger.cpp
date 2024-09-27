#include "pch.h"
#include "WindowArranger.h"

#include <common/logger/logger.h>
#include <common/utils/OnThreadExecutor.h>
#include <common/utils/process_path.h>
#include <common/utils/winapi_error.h>

#include <workspaces-common/MonitorUtils.h>
#include <workspaces-common/WindowEnumerator.h>
#include <workspaces-common/WindowFilter.h>
#include <workspaces-common/WindowUtils.h>

#include <WindowProperties/WorkspacesWindowPropertyUtils.h>

namespace FancyZones
{
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


WindowArranger::WindowArranger(WorkspacesData::WorkspacesProject project, const IPCHelper& ipcHelper) :
    m_project(project),
    m_windowsBefore(WindowEnumerator::Enumerate(WindowFilter::Filter)),
    m_monitors(MonitorUtils::IdentifyMonitors()),
    m_installedApps(Utils::Apps::GetAppsList()),
    //m_windowCreationHandler(std::bind(&WindowArranger::onWindowCreated, this, std::placeholders::_1)),
    m_ipcHelper(ipcHelper)
{
    for (auto& app : project.apps)
    {
        m_launchingApps.insert({ app, { app, nullptr } });
    }

    m_ipcHelper.send(L"ready");

    for (int attempt = 0; attempt < 50 && !allWindowsFound(); attempt++)
    {
        std::this_thread::sleep_for(std::chrono::milliseconds(100));

        std::vector<HWND> windowsAfter = WindowEnumerator::Enumerate(WindowFilter::Filter);
        std::vector<HWND> windowsDiff{};
        std::copy_if(windowsAfter.begin(), windowsAfter.end(), std::back_inserter(windowsDiff), [&](HWND window) { return std::find(m_windowsBefore.begin(), m_windowsBefore.end(), window) == m_windowsBefore.end(); });
        
        for (HWND window : windowsDiff)
        {
            processWindow(window);
        }
    }

    bool allFound = allWindowsFound();
    Logger::info(L"Finished moving new windows, all windows found: {}", allFound);

    if (!allFound)
    {
        std::vector<HWND> allWindows = WindowEnumerator::Enumerate(WindowFilter::Filter);
        for (HWND window : allWindows)
        {
            processWindow(window);
        }
    }
}

//void WindowArranger::onWindowCreated(HWND window)
//{
//    if (!WindowFilter::Filter(window))
//    {
//        return;
//    }
//
//    processWindow(window);
//}

void WindowArranger::processWindow(HWND window)
{
    // check if this window is already handled
    auto windowIter = std::find_if(m_launchingApps.begin(), m_launchingApps.end(), [&](const auto& val) { return val.second.window == window; });
    if (windowIter != m_launchingApps.end())
    {
        return;
    }

    RECT rect = WindowUtils::GetWindowRect(window);
    if (rect.right - rect.left <= 0 || rect.bottom - rect.top <= 0)
    {
        return;
    }

    std::wstring title = WindowUtils::GetWindowTitle(window);
    if (title.empty())
    {
        return;
    }

    std::wstring processPath = get_process_path(window);
    if (processPath.empty())
    {
        return;
    }

    auto data = Utils::Apps::GetApp(processPath, m_installedApps);
    if (!data.has_value())
    {
        return;
    }

    auto iter = std::find_if(m_launchingApps.begin(), m_launchingApps.end(), [&](const auto& val) 
        { 
            return val.second.state == LaunchingState::Waiting && !val.second.window && (val.first.name == data.value().name || val.first.path == data.value().installPath); 
        });
    if (iter == m_launchingApps.end())
    {
        Logger::info(L"A window of {} is not in the project", processPath);
        return;
    }

    Logger::debug(L"Move {}", title);
    iter->second.window = window;
    if (moveWindow(window, iter->first))
    {
        iter->second.state = LaunchingState::LaunchedAndMoved;
    }
    else
    {
        iter->second.state = LaunchingState::Failed;
    }

    m_ipcHelper.send(WorkspacesData::AppLaunchInfoJSON::ToJson({iter->first, nullptr, iter->second.state}).ToString().c_str());
}

bool WindowArranger::moveWindow(HWND window, const WorkspacesData::WorkspacesProject::Application& app)
{
    auto snapMonitorIter = std::find_if(m_project.monitors.begin(), m_project.monitors.end(), [&](const WorkspacesData::WorkspacesProject::Monitor& val) { return val.number == app.monitor; });
    if (snapMonitorIter == m_project.monitors.end())
    {
        Logger::error(L"No monitor saved for launching the app");
        return false;
    }

    bool launchMinimized = app.isMinimized;
    bool launchMaximized = app.isMaximized;

    HMONITOR currentMonitor{};
    UINT currentDpi = DPIAware::DEFAULT_DPI;
    auto currentMonitorIter = std::find_if(m_monitors.begin(), m_monitors.end(), [&](const WorkspacesData::WorkspacesProject::Monitor& val) { return val.number == app.monitor; });
    if (currentMonitorIter != m_monitors.end())
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
        return true;
    }
    else
    {
        Logger::error(L"Failed placing {}", app.name);
        return false;
    }
}

bool WindowArranger::allWindowsFound() const
{
    return std::find_if(m_launchingApps.begin(), m_launchingApps.end(), [&](const std::pair<WorkspacesData::WorkspacesProject::Application, WorkspacesData::LaunchingAppState>& val) {
               return val.second.window == nullptr;
           }) == m_launchingApps.end();
}
