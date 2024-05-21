#include "pch.h"
#include "AppLauncher.h"

#include <TlHelp32.h>

#include <future>
#include <iostream>
#include <string>
#include <vector>

#include <ShellScalingApi.h>

#include <winrt/Windows.UI.Notifications.h>
#include <winrt/Windows.Data.Xml.Dom.h>

#include "../projects-common/MonitorEnumerator.h"
#include "../projects-common/WindowEnumerator.h"

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

namespace KBM
{
    using namespace winrt;
    using namespace Windows::UI::Notifications;
    using namespace Windows::Data::Xml::Dom;

    // Use to find a process by its name
    std::wstring GetFileNameFromPath(const std::wstring& fullPath)
    {
        size_t found = fullPath.find_last_of(L"\\");
        if (found != std::wstring::npos)
        {
            return fullPath.substr(found + 1);
        }
        return fullPath;
    }

    DWORD GetProcessIdByName(const std::wstring& processName)
    {
        DWORD pid = 0;
        HANDLE snapshot = CreateToolhelp32Snapshot(TH32CS_SNAPPROCESS, 0);

        if (snapshot != INVALID_HANDLE_VALUE)
        {
            PROCESSENTRY32 processEntry;
            processEntry.dwSize = sizeof(PROCESSENTRY32);

            if (Process32First(snapshot, &processEntry))
            {
                do
                {
                    if (_wcsicmp(processEntry.szExeFile, processName.c_str()) == 0)
                    {
                        pid = processEntry.th32ProcessID;
                        break;
                    }
                } while (Process32Next(snapshot, &processEntry));
            }

            CloseHandle(snapshot);
        }

        return pid;
    }

    std::vector<DWORD> GetProcessesIdByName(const std::wstring& processName)
    {
        std::vector<DWORD> processIds;
        HANDLE snapshot = CreateToolhelp32Snapshot(TH32CS_SNAPPROCESS, 0);

        if (snapshot != INVALID_HANDLE_VALUE)
        {
            PROCESSENTRY32 processEntry;
            processEntry.dwSize = sizeof(PROCESSENTRY32);

            if (Process32First(snapshot, &processEntry))
            {
                do
                {
                    if (_wcsicmp(processEntry.szExeFile, processName.c_str()) == 0)
                    {
                        processIds.push_back(processEntry.th32ProcessID);
                    }
                } while (Process32Next(snapshot, &processEntry));
            }

            CloseHandle(snapshot);
        }

        return processIds;
    }

    struct handle_data
    {
        unsigned long process_id;
        HWND window_handle;
    };

    // used by FindMainWindow
    BOOL CALLBACK EnumWindowsCallbackAllowNonVisible(HWND handle, LPARAM lParam)
    {
        handle_data& data = *reinterpret_cast<handle_data*>(lParam);
        unsigned long process_id = 0;
        GetWindowThreadProcessId(handle, &process_id);

        if (data.process_id == process_id)
        {
            data.window_handle = handle;
            return FALSE;
        }
        return TRUE;
    }

    // used by FindMainWindow
    BOOL CALLBACK EnumWindowsCallback(HWND handle, LPARAM lParam)
    {
        handle_data& data = *reinterpret_cast<handle_data*>(lParam);
        unsigned long process_id = 0;
        GetWindowThreadProcessId(handle, &process_id);

        if (data.process_id != process_id || !(GetWindow(handle, GW_OWNER) == static_cast<HWND>(0) && IsWindowVisible(handle)))
        {
            return TRUE;
        }

        data.window_handle = handle;
        return FALSE;
    }

    // used for reactivating a window for a program we already started.
    HWND FindMainWindow(unsigned long process_id, const bool allowNonVisible)
    {
        handle_data data;
        data.process_id = process_id;
        data.window_handle = 0;

        if (allowNonVisible)
        {
            EnumWindows(EnumWindowsCallbackAllowNonVisible, reinterpret_cast<LPARAM>(&data));
        }
        else
        {
            EnumWindows(EnumWindowsCallback, reinterpret_cast<LPARAM>(&data));
        }

        return data.window_handle;
    }

    bool ShowProgram(DWORD pid, std::wstring programName, bool isNewProcess, bool minimizeIfVisible, const RECT& windowPosition, int retryCount)
    {
        // a good place to look for this...
        // https://github.com/ritchielawrence/cmdow

        // try by main window.
        auto allowNonVisible = true;
        HWND hwnd = FindMainWindow(pid, allowNonVisible);

        if (hwnd == NULL)
        {
            if (retryCount < 20)
            {
                auto future = std::async(std::launch::async, [=] {
                    std::this_thread::sleep_for(std::chrono::milliseconds(50));
                    ShowProgram(pid, programName, isNewProcess, minimizeIfVisible, windowPosition, retryCount + 1);
                    return false;
                });
            }
        }
        else
        {
            if (hwnd == GetForegroundWindow())
            {
                // only hide if this was a call from a already open program, don't make small if we just opened it.
                if (!isNewProcess && minimizeIfVisible)
                {
                    return ShowWindow(hwnd, SW_MINIMIZE);
                }

                FancyZones::SizeWindowToRect(hwnd, windowPosition, false);
                return false;
            }
            else
            {
                // Check if the window is minimized
                if (IsIconic(hwnd))
                {
                    // Show the window since SetForegroundWindow fails on minimized windows
                    if (!ShowWindow(hwnd, SW_RESTORE))
                    {
                        std::wcout << L"ShowWindow failed" << std::endl;
                    }
                }

                //INPUT inputs[1] = { {.type = INPUT_MOUSE } };
                //SendInput(ARRAYSIZE(inputs), inputs, sizeof(INPUT));

                if (!SetForegroundWindow(hwnd))
                {
                    return false;
                }
                else
                {
                    FancyZones::SizeWindowToRect(hwnd, windowPosition, false);
                    return true;
                }
            }
        }

        if (isNewProcess)
        {
            return true;
        }

        if (false)
        {
            // try by console.
            hwnd = FindWindow(nullptr, nullptr);
            if (AttachConsole(pid))
            {
                // Get the console window handle
                hwnd = GetConsoleWindow();
                auto showByConsoleSuccess = false;
                if (hwnd != NULL)
                {
                    ShowWindow(hwnd, SW_RESTORE);
                    if (SetForegroundWindow(hwnd))
                    {
                        showByConsoleSuccess = true;
                    }
                }

                // Detach from the console
                FreeConsole();
                if (showByConsoleSuccess)
                {
                    return true;
                }
            }
        }

        // try to just show them all (if they have a title)!.
        hwnd = FindWindow(nullptr, nullptr);
        if (hwnd)
        {
            while (hwnd)
            {
                DWORD pidForHwnd;
                GetWindowThreadProcessId(hwnd, &pidForHwnd);
                if (pid == pidForHwnd)
                {
                    int length = GetWindowTextLength(hwnd);

                    if (length > 0)
                    {
                        ShowWindow(hwnd, SW_RESTORE);

                        // hwnd is the window handle with targetPid
                        if (SetForegroundWindow(hwnd))
                        {
                            FancyZones::SizeWindowToRect(hwnd, windowPosition, false);
                            return true;
                        }
                    }
                }
                hwnd = FindWindowEx(NULL, hwnd, NULL, NULL);
            }
        }

        return false;
    }

    bool HideProgram(DWORD pid, std::wstring programName, int retryCount)
    {
        HWND hwnd = FindMainWindow(pid, false);
        if (hwnd == NULL)
        {
            if (retryCount < 20)
            {
                auto future = std::async(std::launch::async, [=] {
                    std::this_thread::sleep_for(std::chrono::milliseconds(50));
                    HideProgram(pid, programName, retryCount + 1);
                    return false;
                });
            }
        }

        hwnd = FindWindow(nullptr, nullptr);
        while (hwnd)
        {
            DWORD pidForHwnd;
            GetWindowThreadProcessId(hwnd, &pidForHwnd);
            if (pid == pidForHwnd)
            {
                if (IsWindowVisible(hwnd))
                {
                    ShowWindow(hwnd, SW_HIDE);
                }
            }
            hwnd = FindWindowEx(NULL, hwnd, NULL, NULL);
        }

        return true;
    }
}

void Launch(const std::wstring& appPath, bool startMinimized, const std::wstring& commandLineArgs, const RECT& windowPosition) noexcept
{
    WCHAR fullExpandedFilePath[MAX_PATH];
    ExpandEnvironmentStrings(appPath.c_str(), fullExpandedFilePath, MAX_PATH);

    auto fileNamePart = KBM::GetFileNameFromPath(fullExpandedFilePath);
    
    DWORD dwAttrib = GetFileAttributesW(fullExpandedFilePath);
    if (dwAttrib == INVALID_FILE_ATTRIBUTES)
    {
        return;
    }

    DWORD processId = 0;
    if (Common::Utils::Elevation::run_non_elevated(fullExpandedFilePath, commandLineArgs, &processId, nullptr, !startMinimized))
    {
        std::wcout << "Launched " << fileNamePart << std::endl;
    }
    else
    {
        std::wcout << "Failed to launch " << fileNamePart << std::endl;
    }

    if (processId == 0)
    {
        return;
    }

    auto targetPid = KBM::GetProcessIdByName(fileNamePart);
    if (!startMinimized)
    {
        KBM::ShowProgram(targetPid, fileNamePart, false, false, windowPosition, 0);
    }
    else
    {
        KBM::HideProgram(targetPid, fileNamePart, 0);
    }

    return;
}
