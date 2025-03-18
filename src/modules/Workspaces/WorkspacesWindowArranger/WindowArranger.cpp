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
#include <WorkspacesLib/PwaHelper.h>

namespace PlacementHelper
{
    // When calculating the coordinates difference (== 'distance') between 2 windows, there are additional values added to the real distance
    // if both windows are minimized, the 'distance' is 0, the minimal value, this is the best match, we prefer this 'pairing'
    // if both are in normal state (non-minimized), we add 1 to the calculated 'distance', this is the 2nd best match
    // if one window is minimized and the other is maximized, we add a high value (10.000) to the result as
    //   this case is the least desired match, we want this pairing (matching) only if there is no other possibility left
    const int PlacementDistanceAdditionBothNormal = 1;
    const int PlacementDistanceAdditionNormalAndMinimized = 10000;

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
            placement.showCmd = SW_RESTORE;
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

    int CalculateDistance(const WorkspacesData::WorkspacesProject::Application& app, HWND window)
    {
        WINDOWPLACEMENT placement{};
        ::GetWindowPlacement(window, &placement);

        if (app.isMinimized && (placement.showCmd == SW_SHOWMINIMIZED))
        {
            // The most preferred case: both windows are minimized. The 'distance' between these 2 windows is 0, the lowest value
            return 0;
        }

        int placementDiffPenalty = PlacementDistanceAdditionBothNormal;
        if (app.isMinimized || (placement.showCmd == SW_SHOWMINIMIZED))
        {
            // The least preferred case: one window is minimized the other one isn't.
            // We add a high number to the real distance, as we want this 2 windows be matched only if there is no other match
            placementDiffPenalty = PlacementDistanceAdditionNormalAndMinimized;
        }

        RECT windowPosition;
        GetWindowRect(window, &windowPosition);
        DPIAware::InverseConvert(MonitorFromWindow(window, MONITOR_DEFAULTTOPRIMARY), windowPosition);

        return placementDiffPenalty + abs(app.position.x - windowPosition.left) + abs(app.position.y - windowPosition.top) + abs(app.position.x + app.position.width - windowPosition.right) + abs(app.position.y + app.position.height - windowPosition.bottom);
    }
}

bool WindowArranger::TryMoveWindow(const WorkspacesData::WorkspacesProject::Application& app, HWND windowToMove)
{
    Logger::info(L"The app {} is found at launch, moving it", app.name);
    auto appState = m_launchingStatus.Get(app);
    if (!appState.has_value())
    {
        Logger::info(L"The app {} is not found in the map of apps", app.name);
        return false;
    }

    bool success = moveWindow(windowToMove, app);
    if (success)
    {
        m_launchingStatus.Update(appState.value().application, windowToMove, LaunchingState::LaunchedAndMoved);
    }
    else
    {
        Logger::info(L"Failed to move the existing app {} ", app.name);
        m_launchingStatus.Update(appState.value().application, windowToMove, LaunchingState::Failed);
    }

    auto updatedState = m_launchingStatus.Get(app);
    if (updatedState.has_value())
    {
        m_ipcHelper.send(WorkspacesData::AppLaunchInfoJSON::ToJson(updatedState.value()).ToString().c_str());
    }

    return success;
}

std::optional<WindowWithDistance> WindowArranger::GetNearestWindow(const WorkspacesData::WorkspacesProject::Application& app, const std::vector<HWND>& movedWindows, Utils::PwaHelper& pwaHelper)
{
    std::optional<Utils::Apps::AppData> appDataNearest = std::nullopt;
    WindowWithDistance nearestWindowWithDistance{};

    for (HWND window : m_windowsBefore)
    {
        if (std::find(movedWindows.begin(), movedWindows.end(), window) != movedWindows.end())
        {
            continue;
        }

        std::wstring processPath = get_process_path(window);
        if (processPath.empty())
        {
            continue;
        }

        DWORD pid{};
        GetWindowThreadProcessId(window, &pid);

        auto data = Utils::Apps::GetApp(processPath, pid, m_installedApps);
        if (!data.has_value())
        {
            continue;
        }

        auto appData = data.value();

        // PWA apps
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

        if ((app.name == appData.name || app.path == appData.installPath) && (app.pwaAppId == appData.pwaAppId))
        {
            if (!appDataNearest.has_value())
            {
                appDataNearest = data;
                nearestWindowWithDistance.distance = PlacementHelper::CalculateDistance(app, window);
                nearestWindowWithDistance.window = window;
            }
            else
            {
                int currentDistance = PlacementHelper::CalculateDistance(app, window);
                if (currentDistance < nearestWindowWithDistance.distance)
                {
                    appDataNearest = data;
                    nearestWindowWithDistance.distance = currentDistance;
                    nearestWindowWithDistance.window = window;
                }
            }
        }
    }

    if (appDataNearest.has_value())
    {
        return nearestWindowWithDistance;
    }

    return std::nullopt;
}
WindowArranger::WindowArranger(WorkspacesData::WorkspacesProject project) :
    m_project(project),
    m_windowsBefore(WindowEnumerator::Enumerate(WindowFilter::Filter)),
    m_monitors(MonitorUtils::IdentifyMonitors()),
    m_installedApps(Utils::Apps::GetAppsList()),
    m_ipcHelper(IPCHelperStrings::WindowArrangerPipeName, IPCHelperStrings::LauncherArrangerPipeName, std::bind(&WindowArranger::receiveIpcMessage, this, std::placeholders::_1)),
    m_launchingStatus(m_project)
{
    if (project.moveExistingWindows)
    {
        Logger::info(L"Moving existing windows");
        bool isMovePhase = true;
        bool movedAny = false;
        std::vector<HWND> movedWindows;
        std::vector<WorkspacesData::WorkspacesProject::Application> movedApps;
        Utils::PwaHelper pwaHelper{};

        while (isMovePhase)
        {
            isMovePhase = false;
            int minDistance = INT_MAX;
            WorkspacesData::WorkspacesProject::Application appToMove;
            HWND windowToMove = NULL;
            for (auto& app : project.apps)
            {
                // move the apps which are set to "Move-If-Exists" and are already present (launched, running)
                if (std::find(movedApps.begin(), movedApps.end(), app) != movedApps.end())
                {
                    continue;
                }

                std::optional<WindowWithDistance> nearestWindowWithDistance;
                nearestWindowWithDistance = GetNearestWindow(app, movedWindows, pwaHelper);
                if (nearestWindowWithDistance.has_value())
                {
                    if (nearestWindowWithDistance.value().distance < minDistance)
                    {
                        minDistance = nearestWindowWithDistance.value().distance;
                        appToMove = app;
                        windowToMove = nearestWindowWithDistance.value().window;
                    }
                }
                else
                {
                    Logger::info(L"The app {} is not found at launch, cannot be moved, has to be started", app.name);
                    movedApps.push_back(app);
                }
            }
            if (minDistance < INT_MAX)
            {
                isMovePhase = true;
                movedAny = true;
                bool success = TryMoveWindow(appToMove, windowToMove);
                movedApps.push_back(appToMove);
                if (success)
                {
                    movedWindows.push_back(windowToMove);
                }
            }
        }

        if (movedAny)
        {
            // Wait if there were moved windows. This message might not arrive if sending immediately after the last "moved" message (status update)
            std::this_thread::sleep_for(std::chrono::milliseconds(100));
        }

        Logger::info(L"Finished moving existing windows");
    }

    m_ipcHelper.send(L"ready");

    const long maxLaunchingWaitingTime = 10000, maxRepositionWaitingTime = 3000, ms = 300;
    long waitingTime{ 0 };

    // process launching windows
    while (!m_launchingStatus.AllLaunched() && waitingTime < maxLaunchingWaitingTime)
    {
        if (processWindows(false))
        {
            waitingTime = 0;
        }

        std::this_thread::sleep_for(std::chrono::milliseconds(ms));
        waitingTime += ms;
    }

    if (waitingTime >= maxLaunchingWaitingTime)
    {
        Logger::info(L"Launching timeout expired");
    }

    Logger::info(L"Finished moving new windows");

    // wait for 3 seconds after all apps launched
    waitingTime = 0;
    while (!m_launchingStatus.AllLaunchedAndMoved() && waitingTime < maxRepositionWaitingTime)
    {
        processWindows(true);
        std::this_thread::sleep_for(std::chrono::milliseconds(ms));
        waitingTime += ms;
    }

    if (waitingTime >= maxRepositionWaitingTime)
    {
        Logger::info(L"Repositioning timeout expired");
    }
}

bool WindowArranger::processWindows(bool processAll)
{
    bool processedAnyWindow = false;
    std::vector<HWND> windows = WindowEnumerator::Enumerate(WindowFilter::Filter);

    if (!processAll)
    {
        std::vector<HWND> windowsDiff{};
        std::copy_if(windows.begin(), windows.end(), std::back_inserter(windowsDiff), [&](HWND window) { return std::find(m_windowsBefore.begin(), m_windowsBefore.end(), window) == m_windowsBefore.end(); });
        windows = windowsDiff;
    }

    for (HWND window : windows)
    {
        processedAnyWindow |= processWindow(window);
    }

    return processedAnyWindow;
}

bool WindowArranger::processWindow(HWND window)
{
    if (m_launchingStatus.IsWindowProcessed(window))
    {
        return false;
    }

    RECT rect = WindowUtils::GetWindowRect(window);
    if (rect.right - rect.left <= 0 || rect.bottom - rect.top <= 0)
    {
        return false;
    }

    std::wstring processPath = get_process_path(window);
    if (processPath.empty())
    {
        return false;
    }

    DWORD pid{};
    GetWindowThreadProcessId(window, &pid);

    auto data = Utils::Apps::GetApp(processPath, pid, m_installedApps);
    if (!data.has_value())
    {
        return false;
    }

    const auto& apps = m_launchingStatus.Get();
    auto iter = std::find_if(apps.begin(), apps.end(), [&](const auto& val) {
        return val.second.state == LaunchingState::Launched &&
               !val.second.window &&
               (val.first.name == data.value().name || val.first.path == data.value().installPath);
    });

    if (iter == apps.end())
    {
        Logger::info(L"Skip {}", processPath);
        return false;
    }

    if (moveWindow(window, iter->first))
    {
        m_launchingStatus.Update(iter->first, window, LaunchingState::LaunchedAndMoved);
    }
    else
    {
        m_launchingStatus.Update(iter->first, window, LaunchingState::Failed);
    }

    auto state = m_launchingStatus.Get(iter->first);
    if (state.has_value())
    {
        sendUpdatedState(state.value());
    }
    return true;
}

bool WindowArranger::moveWindow(HWND window, const WorkspacesData::WorkspacesProject::Application& app)
{
    auto snapMonitorIter = std::find_if(m_project.monitors.begin(), m_project.monitors.end(), [&](const WorkspacesData::WorkspacesProject::Monitor& val) { return val.number == app.monitor; });
    if (snapMonitorIter == m_project.monitors.end())
    {
        Logger::error(L"No monitor saved for launching the app");
        return false;
    }
    UINT snapDPI = snapMonitorIter->dpi;

    bool launchMinimized = app.isMinimized;
    bool launchMaximized = app.isMaximized;
    RECT rect = app.position.toRect();

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
        snapDPI = DPIAware::DEFAULT_DPI;
        launchMinimized = true;
        launchMaximized = false;
        MONITORINFOEX monitorInfo;
        monitorInfo.cbSize = sizeof(monitorInfo);
        if (GetMonitorInfo(currentMonitor, &monitorInfo))
        {
            rect = monitorInfo.rcWork;
        }
    }

    float mult = static_cast<float>(snapDPI) / currentDpi;
    rect.left = static_cast<long>(std::round(rect.left * mult));
    rect.right = static_cast<long>(std::round(rect.right * mult));
    rect.top = static_cast<long>(std::round(rect.top * mult));
    rect.bottom = static_cast<long>(std::round(rect.bottom * mult));

    if (PlacementHelper::SizeWindowToRect(window, currentMonitor, launchMinimized, launchMaximized, rect))
    {
        WorkspacesWindowProperties::StampWorkspacesLaunchedProperty(window);
        WorkspacesWindowProperties::StampWorkspacesGuidProperty(window, app.id);
        Logger::trace(L"Placed {} to ({},{}) [{}x{}]", app.name, rect.left, rect.top, rect.right - rect.left, rect.bottom - rect.top);
        return true;
    }
    else
    {
        Logger::error(L"Failed placing {}", app.name);
        return false;
    }
}

void WindowArranger::receiveIpcMessage(const std::wstring& message)
{
    try
    {
        auto data = WorkspacesData::AppLaunchInfoJSON::FromJson(json::JsonValue::Parse(message).GetObjectW());
        if (data.has_value())
        {
            m_launchingStatus.Update(data.value().application, data.value().state);
        }
        else
        {
            Logger::error(L"Failed to parse message from WorkspacesLauncher");
        }
    }
    catch (const winrt::hresult_error&)
    {
        Logger::error(L"Failed to parse message from WorkspacesLauncher");
    }
}

void WindowArranger::sendUpdatedState(const WorkspacesData::LaunchingAppState& data) const
{
    m_ipcHelper.send(WorkspacesData::AppLaunchInfoJSON::ToJson({ data.application, nullptr, data.state }).ToString().c_str());
}
