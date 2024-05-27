#include "pch.h"
#include "AppLauncher.h"

#include <winrt/Windows.Management.Deployment.h>
#include <winrt/Windows.ApplicationModel.Core.h>

#include <ShellScalingApi.h>
#include <shellapi.h>

#include <iostream>

#include "../projects-common/MonitorEnumerator.h"

using namespace winrt;
using namespace Windows::Foundation;
using namespace Windows::Management::Deployment;

namespace FancyZones
{
    inline void SizeWindowToRect(HWND window, RECT rect, BOOL snapZone) noexcept;
}

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

    namespace Utils
    {
        namespace Elevation
        {
            // Run command as non-elevated user, returns true if succeeded, puts the process id into returnPid if returnPid != NULL
            inline bool run_non_elevated(const std::wstring& file, const std::wstring& params, DWORD* returnPid, const wchar_t* workingDir = nullptr, const bool showWindow = true, const RECT& windowRect = {})
            {
                //Logger::info(L"run_non_elevated with params={}", params);
                auto executable_args = L"\"" + file + L"\"";
                if (!params.empty())
                {
                    executable_args += L" " + params;
                }

                HWND hwnd = GetShellWindow();
                if (!hwnd)
                {
                    if (GetLastError() == ERROR_SUCCESS)
                    {
                        //Logger::warn(L"GetShellWindow() returned null. Shell window is not available");
                    }
                    else
                    {
                        //Logger::error(L"GetShellWindow() failed. {}", get_last_error_or_default(GetLastError()));
                    }

                    return false;
                }
                DWORD pid;
                GetWindowThreadProcessId(hwnd, &pid);

                winrt::handle process{ OpenProcess(PROCESS_CREATE_PROCESS, FALSE, pid) };
                if (!process)
                {
                    //Logger::error(L"OpenProcess() failed. {}", get_last_error_or_default(GetLastError()));
                    return false;
                }

                SIZE_T size = 0;

                InitializeProcThreadAttributeList(nullptr, 1, 0, &size);
                auto pproc_buffer = std::make_unique<char[]>(size);
                auto pptal = reinterpret_cast<PPROC_THREAD_ATTRIBUTE_LIST>(pproc_buffer.get());
                if (!pptal)
                {
                    //Logger::error(L"pptal failed to initialize. {}", get_last_error_or_default(GetLastError()));
                    return false;
                }

                if (!InitializeProcThreadAttributeList(pptal, 1, 0, &size))
                {
                    //Logger::error(L"InitializeProcThreadAttributeList() failed. {}", get_last_error_or_default(GetLastError()));
                    return false;
                }

                HANDLE process_handle = process.get();
                if (!UpdateProcThreadAttribute(pptal,
                    0,
                    PROC_THREAD_ATTRIBUTE_PARENT_PROCESS,
                    &process_handle,
                    sizeof(process_handle),
                    nullptr,
                    nullptr))
                {
                    //Logger::error(L"UpdateProcThreadAttribute() failed. {}", get_last_error_or_default(GetLastError()));
                    return false;
                }

                STARTUPINFOEX siex = { 0 };
                siex.lpAttributeList = pptal;
                siex.StartupInfo.cb = sizeof(siex);
                PROCESS_INFORMATION pi = { 0 };
                auto dwCreationFlags = EXTENDED_STARTUPINFO_PRESENT;

                if (!showWindow)
                {
                    siex.StartupInfo.dwFlags = STARTF_USESHOWWINDOW;
                    siex.StartupInfo.wShowWindow = SW_HIDE;
                    dwCreationFlags = CREATE_NO_WINDOW;
                }
                else
                {
                    siex.StartupInfo.dwFlags = STARTF_USEPOSITION | STARTF_USESIZE;
                    siex.StartupInfo.dwX = windowRect.left;
                    siex.StartupInfo.dwY = windowRect.top;
                    siex.StartupInfo.dwXSize = windowRect.right - windowRect.left;
                    siex.StartupInfo.dwYSize = windowRect.bottom - windowRect.top;
                }

                auto succeeded = CreateProcessW(file.c_str(),
                    &executable_args[0],
                    nullptr,
                    nullptr,
                    FALSE,
                    dwCreationFlags,
                    nullptr,
                    workingDir,
                    &siex.StartupInfo,
                    &pi);
                if (succeeded)
                {
                    if (pi.hProcess)
                    {
                        if (returnPid)
                        {
                            *returnPid = GetProcessId(pi.hProcess);
                        }

                        CloseHandle(pi.hProcess);
                    }
                    if (pi.hThread)
                    {
                        CloseHandle(pi.hThread);
                    }
                }
                else
                {
                    //Logger::error(L"CreateProcessW() failed. {}", get_last_error_or_default(GetLastError()));
                }

                return succeeded;
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

    inline void SizeWindowToRect(HWND window, RECT rect, BOOL snapZone) noexcept
    {

        WINDOWPLACEMENT placement{};
        ::GetWindowPlacement(window, &placement);

        // Wait if SW_SHOWMINIMIZED would be removed from window (Issue #1685)
        for (int i = 0; i < 5 && (placement.showCmd == SW_SHOWMINIMIZED); ++i)
        {
            std::this_thread::sleep_for(std::chrono::milliseconds(100));
            ::GetWindowPlacement(window, &placement);
        }

        BOOL maximizeLater = false;
        if (IsWindowVisible(window))
        {
            // If is not snap zone then need keep maximize state (move to active monitor)
            if (!snapZone && placement.showCmd == SW_SHOWMAXIMIZED)
            {
                maximizeLater = true;
            }

            // Do not restore minimized windows. We change their placement though so they restore to the correct zone.
            if ((placement.showCmd != SW_SHOWMINIMIZED) &&
                (placement.showCmd != SW_MINIMIZE))
            {
                // Remove maximized show command to make sure window is moved to the correct zone.
                if (placement.showCmd == SW_SHOWMAXIMIZED)
                    placement.flags &= ~WPF_RESTORETOMAXIMIZED;

                placement.showCmd = SW_RESTORE;
            }
        }
        else
        {
            placement.showCmd = SW_HIDE;
        }

        ScreenToWorkAreaCoords(window, rect);

        placement.rcNormalPosition = rect;
        placement.flags |= WPF_ASYNCWINDOWPLACEMENT;

        std::wcout << "Set window placement" << std::endl;
        auto result = ::SetWindowPlacement(window, &placement);
        if (!result)
        {
            std::wcout << "Set window placement failed" << std::endl;
            //Logger::error(L"SetWindowPlacement failed, {}", get_last_error_or_default(GetLastError()));
        }

        // make sure window is moved to the correct monitor before maximize.
        if (maximizeLater)
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
        }
    }
}

bool LaunchApp(const std::wstring& appPath, const std::wstring& commandLineArgs)
{
    SHELLEXECUTEINFO shExecInfo;
    shExecInfo.cbSize = sizeof(SHELLEXECUTEINFO);
    shExecInfo.fMask = NULL;
    shExecInfo.hwnd = NULL;
    shExecInfo.lpVerb = NULL;
    shExecInfo.lpFile = appPath.c_str();
    shExecInfo.lpParameters = commandLineArgs.c_str();
    shExecInfo.lpDirectory = NULL;
    shExecInfo.nShow = SW_MAXIMIZE;
    shExecInfo.hInstApp = NULL;

    BOOL result = ShellExecuteEx(&shExecInfo);

    return result;
}

bool LaunchPackagedApp(const std::wstring& packageFullName)
{
    try
    {
        // Create a PackageManager object to get the package information.
        PackageManager packageManager;

        // Find the package by its full name.
        for (const auto& package : packageManager.FindPackagesForUser({}))
        {
            if (package.Id().FullName() == packageFullName)
            {
                // Get the AppListEntry for the package.
                auto getAppListEntriesOperation = package.GetAppListEntriesAsync();
                auto appEntries = getAppListEntriesOperation.get();

                // Launch the first app in the list.
                if (appEntries.Size() > 0)
                {
                    IAsyncOperation<bool> launchOperation = appEntries.GetAt(0).LaunchAsync();
                    bool launchResult = launchOperation.get();
                    return launchResult;
                }
                else
                {
                    std::wcout << L"No app entries found for the package." << std::endl;
                    return false;
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
    bool launched = false;
    if (!app.packageFullName.empty() && app.commandLineArgs.empty())
    {
        std::wcout << L"Launching packaged " << app.name << std::endl;
        launched = LaunchPackagedApp(app.packageFullName);
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

        launched = LaunchApp(app.path, app.commandLineArgs);
    }

    if (launched)
    {
        std::wcout << L"Launched " << app.name << std::endl;
    }
    else
    {
        std::wcout << L"Failed to launch " << app.name << std::endl;
    }
    
    return launched;
}
