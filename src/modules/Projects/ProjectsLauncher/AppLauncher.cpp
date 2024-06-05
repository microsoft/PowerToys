#include "pch.h"
#include "AppLauncher.h"

#include <winrt/Windows.Management.Deployment.h>
#include <winrt/Windows.ApplicationModel.Core.h>

#include <iostream>

#include "../projects-common/AppUtils.h"
#include "../projects-common/MonitorEnumerator.h"
#include "../projects-common/WindowEnumerator.h"
#include "../projects-common/WindowFilter.h"

using namespace winrt;
using namespace Windows::Foundation;
using namespace Windows::Management::Deployment;

namespace Common
{
    namespace Display
    {
        namespace DPIAware
        {
            enum AwarenessLevel
            {
                UNAWARE,
                SYSTEM_AWARE,
                PER_MONITOR_AWARE,
                PER_MONITOR_AWARE_V2,
                UNAWARE_GDISCALED
            };

            AwarenessLevel GetAwarenessLevel(DPI_AWARENESS_CONTEXT system_returned_value)
            {
                const std::array levels{ DPI_AWARENESS_CONTEXT_UNAWARE,
                                         DPI_AWARENESS_CONTEXT_SYSTEM_AWARE,
                                         DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE,
                                         DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2,
                                         DPI_AWARENESS_CONTEXT_UNAWARE_GDISCALED };
                for (size_t i = 0; i < size(levels); ++i)
                {
                    if (AreDpiAwarenessContextsEqual(levels[i], system_returned_value))
                    {
                        return static_cast<DPIAware::AwarenessLevel>(i);
                    }
                }
                return AwarenessLevel::UNAWARE;
            }
        }
    }
}

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

    inline void ScreenToWorkAreaCoords(HWND window, RECT& rect)
    {
        // First, find the correct monitor. The monitor cannot be found using the given rect itself, we must first
        // translate it to relative workspace coordinates.
        HMONITOR monitor = MonitorFromRect(&rect, MONITOR_DEFAULTTOPRIMARY);
        MONITORINFOEXW monitorInfo{ sizeof(MONITORINFOEXW) };
        GetMonitorInfoW(monitor, &monitorInfo);

        auto xOffset = monitorInfo.rcWork.left - monitorInfo.rcMonitor.left;
        auto yOffset = monitorInfo.rcWork.top - monitorInfo.rcMonitor.top;

        auto referenceRect = rect;

        referenceRect.left -= xOffset;
        referenceRect.right -= xOffset;
        referenceRect.top -= yOffset;
        referenceRect.bottom -= yOffset;

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

        const auto level = Common::Display::DPIAware::GetAwarenessLevel(GetWindowDpiAwarenessContext(window));
        const bool accountForUnawareness = level < Common::Display::DPIAware::PER_MONITOR_AWARE;

        if (accountForUnawareness && !allMonitorsHaveSameDpiScaling())
        {
            rect.left = max(monitorInfo.rcMonitor.left, rect.left);
            rect.right = min(monitorInfo.rcMonitor.right - xOffset, rect.right);
            rect.top = max(monitorInfo.rcMonitor.top, rect.top);
            rect.bottom = min(monitorInfo.rcMonitor.bottom - yOffset, rect.bottom);
        }
    }

    inline bool SizeWindowToRect(HWND window, bool isMinimized, bool isMaximized, RECT rect) noexcept
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

            ScreenToWorkAreaCoords(window, rect);
            placement.rcNormalPosition = rect;
        }
        
        placement.flags |= WPF_ASYNCWINDOWPLACEMENT;

        auto result = ::SetWindowPlacement(window, &placement);
        if (!result)
        {
            std::wcout << "Set window placement failed" << std::endl;
            //Logger::error(L"SetWindowPlacement failed, {}", get_last_error_or_default(GetLastError()));
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
            std::wcout << "Set window placement failed" << std::endl;
            //Logger::error(L"SetWindowPlacement failed, {}", get_last_error_or_default(GetLastError()));
            return false;
        }

        return true;
    }
}

bool LaunchApp(const std::wstring& appPath, std::wstring commandLineArgs)
{
    STARTUPINFO startupInfo;
    ZeroMemory(&startupInfo, sizeof(startupInfo));
    startupInfo.cb = sizeof(startupInfo);

    PROCESS_INFORMATION processInfo;
    ZeroMemory(&processInfo, sizeof(processInfo));

    if (CreateProcess(appPath.c_str(), commandLineArgs.data(), nullptr, nullptr, FALSE, 0, nullptr, nullptr, &startupInfo, &processInfo))
    {
        CloseHandle(processInfo.hProcess);
        CloseHandle(processInfo.hThread);

        return true;
    }
    else
    {
        std::wcerr << L"Failed to launch process. Error code: " << GetLastError() << std::endl;
    }

    return false;
}

bool LaunchPackagedApp(const std::wstring& packageFullName)
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
                    std::wcout << L"No app entries found for the package." << std::endl;
                }
            }
        }
    }
    catch (const hresult_error& ex)
    {
        std::wcerr << L"Error: " << ex.message().c_str() << std::endl;
    }

    return false;
}

bool Launch(const Project::Application& app)
{
    // Get the set of windows before launching the app
    std::vector<HWND> windowsBefore = WindowEnumerator::Enumerate(WindowFilter::Filter);

    if (!app.packageFullName.empty() && app.commandLineArgs.empty())
    {
        std::wcout << L"Launching packaged " << app.name << std::endl;
        LaunchPackagedApp(app.packageFullName);
    }
    else
    {
        // TODO: verify app path is up to date. 
        // Packaged apps have version in the path, it will be outdated after update.
        std::wcout << L"Launching " << app.name << " at " << app.path << std::endl;

        DWORD dwAttrib = GetFileAttributesW(app.path.c_str());
        if (dwAttrib == INVALID_FILE_ATTRIBUTES)
        {
            std::wcout << L"  File not found at " << app.path << std::endl;
            return false;
        }

        LaunchApp(app.path, app.commandLineArgs);
    }

    std::this_thread::sleep_for(std::chrono::milliseconds(1000));

    // Get the set of windows after launching the app
    std::vector<HWND> launchedWindows{};
    for (int attempt = 0; attempt < 5 && launchedWindows.empty(); attempt++)
    {
        std::vector<HWND> windowsAfter = WindowEnumerator::Enumerate(WindowFilter::Filter);

        // Find the new window
        for (HWND window : windowsAfter)
        {
            if (std::find(windowsBefore.begin(), windowsBefore.end(), window) == windowsBefore.end())
            {
                launchedWindows.push_back(window);
            }
        }
    }

    // The app wasn't launched, check if it's already launched for single-instance apps
    if (launchedWindows.empty())
    {
        std::wcout << L"Failed to launch " << app.name << L", checking if it's launched already." << std::endl;

        auto apps = Utils::Apps::GetAppsList();
        auto windows = WindowEnumerator::Enumerate(WindowFilter::Filter);
        for (HWND window : windows)
        {
            std::wstring processPath = Common::Utils::ProcessPath::get_process_path_waiting_uwp(window);
            auto data = Utils::Apps::GetApp(processPath, apps);
            if (!data.has_value())
            {
                continue;
            }

            if (data.value().name == app.name)
            {
                launchedWindows.push_back(window);
            }
        }  
    }

    // The single-instance app not found
    if (launchedWindows.empty())
    {
        return false;
    }

    // Place the window
    for (auto window : launchedWindows)
    {
        if (!FancyZones::SizeWindowToRect(window, app.isMinimized, app.isMaximized, app.position.toRect()))
        {
            std::wcout << L"Failed placing " << app.name << std::endl;
        }
    }

    std::wcout << L"Launched " << app.name << std::endl;
    return true;
}
