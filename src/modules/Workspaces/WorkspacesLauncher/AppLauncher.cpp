#include "pch.h"
#include "AppLauncher.h"

#include <winrt/Windows.Management.Deployment.h>
#include <winrt/Windows.ApplicationModel.Core.h>

#include <shellapi.h>
#include <ShellScalingApi.h>

#include <filesystem>

#include <workspaces-common/MonitorEnumerator.h>
#include <workspaces-common/WindowEnumerator.h>
#include <workspaces-common/WindowFilter.h>

#include <WorkspacesLib/AppUtils.h>

#include <common/Display/dpi_aware.h>
#include <common/utils/winapi_error.h>

#include <LaunchingApp.h>
#include <LauncherUIHelper.h>
#include <RegistryUtils.h>
#include <WindowProperties/WorkspacesWindowPropertyUtils.h>

using namespace winrt;
using namespace Windows::Foundation;
using namespace Windows::Management::Deployment;

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
            // Packaged apps have version in the path, it will be outdated after update.
            // We need make sure the current package is up to date.
            if (!app.packageFullName.empty())
            {
                auto installedApp = std::find_if(installedApps.begin(), installedApps.end(), [&](const Utils::Apps::AppData& val) { return val.name == app.name; });
                if (installedApp != installedApps.end() && app.packageFullName != installedApp->packageFullName)
                {
                    std::wstring exeFileName = app.path.substr(app.path.find_last_of(L"\\") + 1);
                    app.packageFullName = installedApp->packageFullName;
                    app.path = installedApp->installPath + L"\\" + exeFileName;
                    Logger::trace(L"Updated package full name for {}: {}", app.name, app.packageFullName);
                }
            }

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

bool LaunchApp(const std::wstring& appPath, const std::wstring& commandLineArgs, bool elevated, ErrorList& launchErrors)
{
    std::wstring dir = std::filesystem::path(appPath).parent_path();

    SHELLEXECUTEINFO sei = { 0 };
    sei.cbSize = sizeof(SHELLEXECUTEINFO);
    sei.hwnd = nullptr;
    sei.fMask = SEE_MASK_NOCLOSEPROCESS | SEE_MASK_NO_CONSOLE;
    sei.lpVerb = elevated ? L"runas" : L"open";
    sei.lpFile = appPath.c_str();
    sei.lpParameters = commandLineArgs.c_str();
    sei.lpDirectory = dir.c_str();
    sei.nShow = SW_SHOWNORMAL;

    if (!ShellExecuteEx(&sei))
    {
        auto error = GetLastError();
        Logger::error(L"Failed to launch process. {}", get_last_error_or_default(error));
        launchErrors.push_back({ std::filesystem::path(appPath).filename(), get_last_error_or_default(error) });
        return false;
    }

    return true;
}

bool LaunchPackagedApp(const std::wstring& packageFullName, ErrorList& launchErrors)
{
    try
    {
        PackageManager packageManager;
        for (const auto& package : packageManager.FindPackagesForUser({}))
        {
            if (package.Id().FullName() == packageFullName)
            {
                auto getAppListEntriesOperation = package.GetAppListEntriesAsync();
                auto appEntries = getAppListEntriesOperation.get();

                if (appEntries.Size() > 0)
                {
                    IAsyncOperation<bool> launchOperation = appEntries.GetAt(0).LaunchAsync();
                    bool launchResult = launchOperation.get();
                    return launchResult;
                }
                else
                {
                    Logger::error(L"No app entries found for the package.");
                    launchErrors.push_back({ packageFullName, L"No app entries found for the package." });
                }
            }
        }
    }
    catch (const hresult_error& ex)
    {
        Logger::error(L"Packaged app launching error: {}", ex.message());
        launchErrors.push_back({ packageFullName, ex.message().c_str() });
    }

    return false;
}

bool Launch(const WorkspacesData::WorkspacesProject::Application& app, ErrorList& launchErrors)
{
    bool launched{ false };

    // packaged apps: check protocol in registry
    // usage example: Settings with cmd args
    if (!app.packageFullName.empty())
    {
        auto names = RegistryUtils::GetUriProtocolNames(app.packageFullName);
        if (!names.empty())
        {
            Logger::trace(L"Launching packaged by protocol with command line args {}", app.name);

            std::wstring uriProtocolName = names[0];
            std::wstring command = std::wstring(uriProtocolName + (app.commandLineArgs.starts_with(L":") ? L"" : L":") + app.commandLineArgs);

            launched = LaunchApp(command, L"", app.isElevated, launchErrors);
        }
        else
        {
            Logger::info(L"Uri protocol names not found for {}", app.packageFullName);
        }
    }

    // packaged apps: try launching first by AppUserModel.ID
    // usage example: elevated Terminal 
    if (!launched && !app.appUserModelId.empty() && !app.packageFullName.empty())
    {
        Logger::trace(L"Launching {} as {}", app.name, app.appUserModelId);
        launched = LaunchApp(L"shell:AppsFolder\\" + app.appUserModelId, app.commandLineArgs, app.isElevated, launchErrors);
    }

    // packaged apps: try launching by package full name
    // doesn't work for elevated apps or apps with command line args
    if (!launched && !app.packageFullName.empty() && app.commandLineArgs.empty() && !app.isElevated)
    {
        Logger::trace(L"Launching packaged app {}", app.name);
        launched = LaunchPackagedApp(app.packageFullName, launchErrors);
    }

    if (!launched)
    {
        Logger::trace(L"Launching {} at {}", app.name, app.path);

        DWORD dwAttrib = GetFileAttributesW(app.path.c_str());
        if (dwAttrib == INVALID_FILE_ATTRIBUTES)
        {
            Logger::error(L"File not found at {}", app.path);
            launchErrors.push_back({ std::filesystem::path(app.path).filename(), L"File not found" });
            return false;
        }

        launched = LaunchApp(app.path, app.commandLineArgs, app.isElevated, launchErrors);
    }

    Logger::trace(L"{} {} at {}", app.name, (launched ? L"launched" : L"not launched"), app.path);
    return launched;
}

bool Launch(WorkspacesData::WorkspacesProject& project, const std::vector<WorkspacesData::WorkspacesProject::Monitor>& monitors, ErrorList& launchErrors)
{
    bool launchedSuccessfully{ true };

    LauncherUIHelper uiHelper;
    uiHelper.LaunchUI();

    // Get the set of windows before launching the app
    std::vector<HWND> windowsBefore = WindowEnumerator::Enumerate(WindowFilter::Filter);
    auto installedApps = Utils::Apps::GetAppsList();
    auto launchedApps = Prepare(project.apps, installedApps);

    uiHelper.UpdateLaunchStatus(launchedApps);

    // Launch apps
    for (auto& app : launchedApps)
    {
        if (!app.window)
        {
            if (!Launch(app.application, launchErrors))
            {
                Logger::error(L"Failed to launch {}", app.application.name);
                app.state = L"failed";
                uiHelper.UpdateLaunchStatus(launchedApps);
                launchedSuccessfully = false;
            }
        }
    }

    // Get newly opened windows after launching apps, keep retrying for 5 seconds
    Logger::trace(L"Find new windows");
    for (int attempt = 0; attempt < 50 && !AllWindowsFound(launchedApps); attempt++)
    {
        std::vector<HWND> windowsAfter = WindowEnumerator::Enumerate(WindowFilter::Filter);
        std::vector<HWND> windowsDiff{};
        std::copy_if(windowsAfter.begin(), windowsAfter.end(), std::back_inserter(windowsDiff), [&](HWND window) { return std::find(windowsBefore.begin(), windowsBefore.end(), window) == windowsBefore.end(); });
        if (AddOpenedWindows(launchedApps, windowsDiff, installedApps))
        {
            uiHelper.UpdateLaunchStatus(launchedApps);
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
            uiHelper.UpdateLaunchStatus(launchedApps);
        }
    }

    // Place windows
    for (const auto& [app, window, status] : launchedApps)
    {
        if (window == nullptr)
        {
            Logger::warn(L"{} window not found.", app.name);
            launchedSuccessfully = false;
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
            launchedSuccessfully = false;
        }
    }

    return launchedSuccessfully;
}
