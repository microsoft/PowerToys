#include "pch.h"
#include "WindowArranger.h"

#include <common/logger/logger.h>
#include <common/utils/process_path.h>
#include <common/utils/winapi_error.h>

#include <workspaces-common/MonitorUtils.h>
#include <workspaces-common/WindowEnumerator.h>
#include <workspaces-common/WindowFilter.h>
#include <workspaces-common/WindowUtils.h>

#include <WindowProperties/WorkspacesWindowPropertyUtils.h>
#include <WorkspacesLib/PwaHelper.h>
#include <WorkspacesLib/WindowUtils.h>

#include <algorithm>
#include <vector>

namespace NonLocalizable
{
    const std::wstring ApplicationFrameHost = L"ApplicationFrameHost.exe";
}

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
        if (isMinimized)
        {
            // Use ShowWindow with SW_FORCEMINIMIZE to avoid animation
            if (!ShowWindow(window, SW_FORCEMINIMIZE))
            {
                Logger::error(L"ShowWindow minimize failed, {}", get_last_error_or_default(GetLastError()));
                return false;
            }
            return true;
        }

        // For normal/maximized windows, use SetWindowPos which is faster and has better control over animations
        if (!isMaximized)
        {
            ScreenToWorkAreaCoords(window, monitor, rect);
            
            // First ensure window is visible but not activated
            ShowWindow(window, SW_SHOWNOACTIVATE);
            
            // Use SetWindowPos with flags to disable animations and avoid activation
            auto result = ::SetWindowPos(window, nullptr, 
                rect.left, rect.top, 
                rect.right - rect.left, rect.bottom - rect.top,
                SWP_NOZORDER | SWP_NOACTIVATE | SWP_NOCOPYBITS | SWP_DEFERERASE);
            
            if (!result)
            {
                Logger::error(L"SetWindowPos failed, {}", get_last_error_or_default(GetLastError()));
                return false;
            }
        }
        else
        {
            // For maximized windows, first move to correct monitor, then maximize
            ScreenToWorkAreaCoords(window, monitor, rect);
            
            // First ensure window is visible but not activated
            ShowWindow(window, SW_SHOWNOACTIVATE);
            
            // Move to correct position first (without animation flags)
            ::SetWindowPos(window, nullptr, 
                rect.left, rect.top, 
                rect.right - rect.left, rect.bottom - rect.top,
                SWP_NOZORDER | SWP_NOACTIVATE | SWP_NOCOPYBITS | SWP_DEFERERASE);
            
            // Then maximize without animation using ShowWindow instead of SetWindowPlacement
            if (!ShowWindow(window, SW_MAXIMIZE))
            {
                Logger::error(L"ShowWindow maximize failed, {}", get_last_error_or_default(GetLastError()));
                return false;
            }
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
        if (WindowFilter::FilterPopup(window))
        {
            continue;
        }

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
        std::wstring title = WindowUtils::GetWindowTitle(window);

        // fix for the packaged apps that are not caught when minimized, e.g. Settings, Microsoft ToDo, ...
        if (processPath.ends_with(NonLocalizable::ApplicationFrameHost))
        {
            for (auto otherWindow : m_windowsBefore)
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

        auto data = Utils::Apps::GetApp(processPath, pid, m_installedApps);

        if (!data->IsSteamGame() && !WindowUtils::HasThickFrame(window))
        {
            // Only care about steam games if it has no thick frame to remain consistent with
            // the behavior as before.
            continue;
        }

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
            auto windowAumid = Utils::GetAUMIDFromWindow(window);
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
                std::wstring browserType = isEdge ? L"Edge" : (isChrome ? L"Chrome" : L"unknown");
                Logger::info(L"Found {} PWA app with name {}, appId: {}", browserType, pwaName, pwaAppId.value());

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
    auto startTime = std::chrono::high_resolution_clock::now();
    Logger::info(L"WindowArranger construction started");

    // First, minimize all unmanaged windows sequentially for thread safety
    auto minimizeStart = std::chrono::high_resolution_clock::now();
    MinimizeUnmanagedWindowsParallel();
    auto minimizeEnd = std::chrono::high_resolution_clock::now();
    auto minimizeDuration = std::chrono::duration_cast<std::chrono::milliseconds>(minimizeEnd - minimizeStart);
    Logger::info(L"MinimizeUnmanagedWindows took {} ms", minimizeDuration.count());

    if (project.moveExistingWindows)
    {
        auto moveExistingStart = std::chrono::high_resolution_clock::now();
        Logger::info(L"Moving existing windows started");
        bool isMovePhase = true;
        bool movedAny = false;
        std::vector<HWND> movedWindows;
        std::vector<WorkspacesData::WorkspacesProject::Application> movedApps;
        Utils::PwaHelper pwaHelper{};

        int moveIterations = 0;
        while (isMovePhase)
        {
            auto iterationStart = std::chrono::high_resolution_clock::now();
            moveIterations++;
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

                auto findStart = std::chrono::high_resolution_clock::now();
                std::optional<WindowWithDistance> nearestWindowWithDistance;
                nearestWindowWithDistance = GetNearestWindow(app, movedWindows, pwaHelper);
                auto findEnd = std::chrono::high_resolution_clock::now();
                auto findDuration = std::chrono::duration_cast<std::chrono::milliseconds>(findEnd - findStart);
                
                if (nearestWindowWithDistance.has_value())
                {
                    Logger::trace(L"Found nearest window for {} in {} ms, distance: {}", app.name, findDuration.count(), nearestWindowWithDistance.value().distance);
                    if (nearestWindowWithDistance.value().distance < minDistance)
                    {
                        minDistance = nearestWindowWithDistance.value().distance;
                        appToMove = app;
                        windowToMove = nearestWindowWithDistance.value().window;
                    }
                }
                else
                {
                    Logger::info(L"The app {} is not found at launch, cannot be moved, has to be started (search took {} ms)", app.name, findDuration.count());
                    movedApps.push_back(app);
                }
            }
            
            auto iterationEnd = std::chrono::high_resolution_clock::now();
            auto iterationDuration = std::chrono::duration_cast<std::chrono::milliseconds>(iterationEnd - iterationStart);
            Logger::trace(L"Move iteration {} completed in {} ms", moveIterations, iterationDuration.count());
            
            if (minDistance < INT_MAX)
            {
                isMovePhase = true;
                movedAny = true;
                auto moveStart = std::chrono::high_resolution_clock::now();
                bool success = TryMoveWindow(appToMove, windowToMove);
                auto moveEnd = std::chrono::high_resolution_clock::now();
                auto moveDuration = std::chrono::duration_cast<std::chrono::milliseconds>(moveEnd - moveStart);
                Logger::info(L"TryMoveWindow for {} took {} ms, success: {}", appToMove.name, moveDuration.count(), success);
                
                movedApps.push_back(appToMove);
                if (success)
                {
                    movedWindows.push_back(windowToMove);
                }
            }
        }

        auto moveExistingEnd = std::chrono::high_resolution_clock::now();
        auto moveExistingDuration = std::chrono::duration_cast<std::chrono::milliseconds>(moveExistingEnd - moveExistingStart);
        Logger::info(L"Moving existing windows completed in {} ms ({} iterations)", moveExistingDuration.count(), moveIterations);

        if (movedAny)
        {
            // Wait if there were moved windows. This message might not arrive if sending immediately after the last "moved" message (status update)
            auto sleepStart = std::chrono::high_resolution_clock::now();
            std::this_thread::sleep_for(std::chrono::milliseconds(100));
            auto sleepEnd = std::chrono::high_resolution_clock::now();
            auto sleepDuration = std::chrono::duration_cast<std::chrono::milliseconds>(sleepEnd - sleepStart);
            Logger::trace(L"Sleep after moving windows: {} ms", sleepDuration.count());
        }

        Logger::info(L"Finished moving existing windows");
    }

    auto ipcStart = std::chrono::high_resolution_clock::now();
    m_ipcHelper.send(L"ready");
    auto ipcEnd = std::chrono::high_resolution_clock::now();
    auto ipcDuration = std::chrono::duration_cast<std::chrono::milliseconds>(ipcEnd - ipcStart);
    Logger::info(L"IPC ready message sent in {} ms", ipcDuration.count());

    // Optimized timeouts - but with early exit logic
    const long maxLaunchingWaitingTime = 3000, maxRepositionWaitingTime = 2000, ms = 50; // Further reduced timeouts
    long waitingTime{ 0 };
    bool hasAppsToLaunch = !m_project.apps.empty();

    // process launching windows
    auto launchingStart = std::chrono::high_resolution_clock::now();
    Logger::info(L"Starting to process launching windows (apps to launch: {})", m_project.apps.size());
    
    // If no apps to launch, skip the launching phase entirely
    if (!hasAppsToLaunch)
    {
        Logger::info(L"No apps to launch, skipping launching phase");
    }
    else
    {
        while (!m_launchingStatus.AllLaunched() && waitingTime < maxLaunchingWaitingTime)
        {
            auto processStart = std::chrono::high_resolution_clock::now();
            bool processed = processWindows(false);
            auto processEnd = std::chrono::high_resolution_clock::now();
            auto processDuration = std::chrono::duration_cast<std::chrono::milliseconds>(processEnd - processStart);
            
            if (processed)
            {
                Logger::info(L"processWindows(false) took {} ms, processed windows, resetting waitingTime", processDuration.count());
                waitingTime = 0;
            }
            else
            {
                Logger::trace(L"processWindows(false) took {} ms, no windows processed, waitingTime={}", processDuration.count(), waitingTime);
            }

            std::this_thread::sleep_for(std::chrono::milliseconds(ms));
            waitingTime += ms;
            
            // Add periodic status check every 1 second
            if (waitingTime % 1000 == 0)
            {
                Logger::info(L"Still waiting for apps to launch, elapsed: {} ms, AllLaunched: {}", waitingTime, m_launchingStatus.AllLaunched());
            }
        }
    }

    auto launchingEnd = std::chrono::high_resolution_clock::now();
    auto launchingDuration = std::chrono::duration_cast<std::chrono::milliseconds>(launchingEnd - launchingStart);
    Logger::info(L"Processing launching windows completed in {} ms", launchingDuration.count());

    if (waitingTime >= maxLaunchingWaitingTime)
    {
        Logger::info(L"Launching timeout expired after {} ms", waitingTime);
    }

    Logger::info(L"Finished moving new windows");

    // wait for repositioning after all apps launched
    auto repositionStart = std::chrono::high_resolution_clock::now();
    Logger::info(L"Starting repositioning phase");
    waitingTime = 0;
    
    // Skip repositioning if no apps were launched or if all are already in position
    if (!hasAppsToLaunch || m_launchingStatus.AllLaunchedAndMoved())
    {
        Logger::info(L"Skipping repositioning phase - no apps to reposition or all already in position");
    }
    else
    {
        while (!m_launchingStatus.AllLaunchedAndMoved() && waitingTime < maxRepositionWaitingTime)
        {
            auto reprocessStart = std::chrono::high_resolution_clock::now();
            bool reprocessed = processWindows(true);
            auto reprocessEnd = std::chrono::high_resolution_clock::now();
            auto reprocessDuration = std::chrono::duration_cast<std::chrono::milliseconds>(reprocessEnd - reprocessStart);
            
            if (reprocessed)
            {
                Logger::info(L"processWindows(true) took {} ms, processed windows", reprocessDuration.count());
                // If we processed windows, check if we're done before continuing to wait
                if (m_launchingStatus.AllLaunchedAndMoved())
                {
                    Logger::info(L"All windows launched and moved, early exit from repositioning");
                    break;
                }
            }
            else
            {
                Logger::trace(L"processWindows(true) took {} ms, no windows processed", reprocessDuration.count());
            }
            
            std::this_thread::sleep_for(std::chrono::milliseconds(ms));
            waitingTime += ms;
            
            // Add periodic status check every 1 second
            if (waitingTime % 1000 == 0)
            {
                Logger::info(L"Still repositioning windows, elapsed: {} ms", waitingTime);
            }
        }
    }

    auto repositionEnd = std::chrono::high_resolution_clock::now();
    auto repositionDuration = std::chrono::duration_cast<std::chrono::milliseconds>(repositionEnd - repositionStart);
    Logger::info(L"Repositioning phase completed in {} ms", repositionDuration.count());

    if (waitingTime >= maxRepositionWaitingTime)
    {
        Logger::info(L"Repositioning timeout expired after {} ms", waitingTime);
    }
    
    auto totalEnd = std::chrono::high_resolution_clock::now();
    auto totalDuration = std::chrono::duration_cast<std::chrono::milliseconds>(totalEnd - startTime);
    Logger::info(L"WindowArranger construction completed in {} ms total", totalDuration.count());
    
    // Send explicit completion signal to Launcher
    m_ipcHelper.send(L"completed");
    Logger::info(L"Sent completion signal to Launcher");
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
        Logger::trace(L"Skip {}", processPath);  // Changed from info to trace to reduce noise
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

void WindowArranger::MinimizeUnmanagedWindowsParallel()
{
    Logger::info(L"Starting sequential minimization of unmanaged windows");
    auto start = std::chrono::high_resolution_clock::now();

    // Get all current windows
    auto enumStart = std::chrono::high_resolution_clock::now();
    auto allWindows = WindowEnumerator::Enumerate(WindowFilter::Filter);
    auto enumEnd = std::chrono::high_resolution_clock::now();
    auto enumDuration = std::chrono::duration_cast<std::chrono::milliseconds>(enumEnd - enumStart);
    Logger::info(L"Window enumeration found {} windows in {} ms", allWindows.size(), enumDuration.count());
    
    // Use sequential filtering to classify windows for thread safety
    std::vector<HWND> unmanagedWindows;
    
    Utils::PwaHelper pwaHelper{};
    
    // Sequential classification of windows
    auto classifyStart = std::chrono::high_resolution_clock::now();
    for (HWND window : allWindows)
    {
        if (!IsWindowInAppList(window, pwaHelper))
        {
            unmanagedWindows.push_back(window);
        }
    }
    auto classifyEnd = std::chrono::high_resolution_clock::now();
    auto classifyDuration = std::chrono::duration_cast<std::chrono::milliseconds>(classifyEnd - classifyStart);
    Logger::info(L"Window classification completed in {} ms", classifyDuration.count());

    Logger::info(L"Found {} unmanaged windows to minimize out of {} total", unmanagedWindows.size(), allWindows.size());

    // Sequential minimization of unmanaged windows
    auto minimizeStart = std::chrono::high_resolution_clock::now();
    int minimizedCount = 0;
    for (HWND window : unmanagedWindows)
    {
        if (MinimizeWindowWithoutAnimation(window))
        {
            minimizedCount++;
        }
    }
    auto minimizeEnd = std::chrono::high_resolution_clock::now();
    auto minimizeDuration = std::chrono::duration_cast<std::chrono::milliseconds>(minimizeEnd - minimizeStart);
    Logger::info(L"Sequential window minimization completed in {} ms, successfully minimized {} windows", minimizeDuration.count(), minimizedCount);

    auto end = std::chrono::high_resolution_clock::now();
    auto totalDuration = std::chrono::duration_cast<std::chrono::milliseconds>(end - start);
    Logger::info(L"Sequential window minimization completed in {} ms total", totalDuration.count());
}

bool WindowArranger::IsWindowInAppList(HWND window, Utils::PwaHelper& pwaHelper)
{
    if (WindowFilter::FilterPopup(window))
    {
        return true; // Don't minimize system popups
    }

    std::wstring processPath = get_process_path(window);
    if (processPath.empty())
    {
        return true; // Skip windows without valid process path
    }

    DWORD pid{};
    GetWindowThreadProcessId(window, &pid);
    std::wstring title = WindowUtils::GetWindowTitle(window);

    // Handle ApplicationFrameHost (UWP apps)
    if (processPath.ends_with(NonLocalizable::ApplicationFrameHost))
    {
        // Get all current windows to find the actual process
        auto currentWindows = WindowEnumerator::Enumerate(WindowFilter::Filter);
        for (auto otherWindow : currentWindows)
        {
            DWORD otherPid{};
            GetWindowThreadProcessId(otherWindow, &otherPid);
            if (pid != otherPid && title == WindowUtils::GetWindowTitle(otherWindow))
            {
                processPath = get_process_path(otherPid);
                break;
            }
        }
    }

    auto data = Utils::Apps::GetApp(processPath, pid, m_installedApps);
    if (!data.has_value())
    {
        return false; // Unknown app, should be minimized
    }

    auto appData = data.value();

    // Handle PWA apps
    bool isEdge = appData.IsEdge();
    bool isChrome = appData.IsChrome();
    if (isEdge || isChrome)
    {
        auto windowAumid = Utils::GetAUMIDFromWindow(window);
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
            appData.pwaAppId = pwaAppId.value();
            appData.name = pwaName + L" (" + appData.name + L")";
        }
    }

    // Check if this app is in our workspace app list
    for (const auto& app : m_project.apps)
    {
        if ((app.name == appData.name || app.path == appData.installPath) && 
            (app.pwaAppId == appData.pwaAppId))
        {
            return true; // App is in our list, don't minimize
        }
    }

    return false; // App is not in our list, should be minimized
}

bool WindowArranger::MinimizeWindowWithoutAnimation(HWND window)
{
    WINDOWPLACEMENT placement{};
    placement.length = sizeof(WINDOWPLACEMENT);
    
    if (!GetWindowPlacement(window, &placement))
    {
        Logger::warn(L"Failed to get window placement for minimization");
        return false;
    }

    // Skip if already minimized
    if (placement.showCmd == SW_SHOWMINIMIZED)
    {
        Logger::trace(L"Window already minimized, skipping");
        return true;
    }

    // Use ShowWindow with SW_FORCEMINIMIZE to avoid animation and activation
    // SW_FORCEMINIMIZE minimizes the window without activating it, which avoids most animations
    if (!ShowWindow(window, SW_FORCEMINIMIZE))
    {
        Logger::warn(L"Failed to force minimize window: {}", get_last_error_or_default(GetLastError()));
        return false;
    }

    Logger::trace(L"Successfully force minimized window without animation");
    return true;
}
