#pragma once

#include <Windows.h>
#include <ShellScalingApi.h>

#include <algorithm>

namespace Common
{
    namespace Display
    {
        namespace DPIAware
        {
            constexpr inline int DEFAULT_DPI = 96;

            inline void InverseConvert(HMONITOR monitor_handle, float& width, float& height)
            {
                if (monitor_handle == NULL)
                {
                    const POINT ptZero = { 0, 0 };
                    monitor_handle = MonitorFromPoint(ptZero, MONITOR_DEFAULTTOPRIMARY);
                }

                UINT dpi_x, dpi_y;
                if (GetDpiForMonitor(monitor_handle, MDT_EFFECTIVE_DPI, &dpi_x, &dpi_y) == S_OK)
                {
                    width = width * DPIAware::DEFAULT_DPI / dpi_x;
                    height = height * DPIAware::DEFAULT_DPI / dpi_y;
                }
            }
        }
    }

    namespace Utils
    {
        namespace ProcessPath
        {
            // Get the executable path or module name for modern apps
            inline std::wstring get_process_path(DWORD pid) noexcept
            {
                auto process = OpenProcess(PROCESS_QUERY_INFORMATION | PROCESS_VM_READ, TRUE, pid);
                std::wstring name;
                if (process != INVALID_HANDLE_VALUE)
                {
                    name.resize(MAX_PATH);
                    DWORD name_length = static_cast<DWORD>(name.length());
                    if (QueryFullProcessImageNameW(process, 0, name.data(), &name_length) == 0)
                    {
                        name_length = 0;
                    }
                    name.resize(name_length);
                    CloseHandle(process);
                }
                return name;
            }

            // Get the executable path or module name for modern apps
            inline std::wstring get_process_path(HWND window) noexcept
            {
                const static std::wstring app_frame_host = L"ApplicationFrameHost.exe";

                DWORD pid{};
                GetWindowThreadProcessId(window, &pid);
                auto name = get_process_path(pid);

                if (name.length() >= app_frame_host.length() &&
                    name.compare(name.length() - app_frame_host.length(), app_frame_host.length(), app_frame_host) == 0)
                {
                    // It is a UWP app. We will enumerate the windows and look for one created
                    // by something with a different PID
                    DWORD new_pid = pid;

                    EnumChildWindows(
                        window, [](HWND hwnd, LPARAM param) -> BOOL {
                            auto new_pid_ptr = reinterpret_cast<DWORD*>(param);
                            DWORD pid;
                            GetWindowThreadProcessId(hwnd, &pid);
                            if (pid != *new_pid_ptr)
                            {
                                *new_pid_ptr = pid;
                                return FALSE;
                            }
                            else
                            {
                                return TRUE;
                            }
                        },
                        reinterpret_cast<LPARAM>(&new_pid));

                    // If we have a new pid, get the new name.
                    if (new_pid != pid)
                    {
                        return get_process_path(new_pid);
                    }
                }

                return name;
            }

            inline std::wstring get_process_path_waiting_uwp(HWND window)
            {
                const static std::wstring appFrameHost = L"ApplicationFrameHost.exe";

                int attempt = 0;
                auto processPath = get_process_path(window);

                while (++attempt < 30 && processPath.length() >= appFrameHost.length() &&
                    processPath.compare(processPath.length() - appFrameHost.length(), appFrameHost.length(), appFrameHost) == 0)
                {
                    std::this_thread::sleep_for(std::chrono::milliseconds(5));
                    processPath = get_process_path(window);
                }

                return processPath;
            }
        }

        namespace ExcludedApps
        {
            inline bool find_folder_in_path(const std::wstring& where, const std::vector<std::wstring>& what)
            {
                for (const auto& row : what)
                {
                    const auto pos = where.rfind(row);
                    if (pos != std::wstring::npos)
                    {
                        return true;
                    }
                }
                return false;
            }

            // Checks if a process path is included in a list of strings.
            inline bool find_app_name_in_path(const std::wstring& where, const std::vector<std::wstring>& what)
            {
                for (const auto& row : what)
                {
                    const auto pos = where.rfind(row);
                    const auto last_slash = where.rfind('\\');
                    //Check that row occurs in where, and its last occurrence contains in itself the first character after the last backslash.
                    if (pos != std::wstring::npos && pos <= last_slash + 1 && pos + row.length() > last_slash)
                    {
                        return true;
                    }
                }
                return false;
            }

            inline bool check_excluded_app_with_title(const std::vector<std::wstring>& excludedApps, std::wstring title)
            {
                CharUpperBuffW(title.data(), static_cast<DWORD>(title.length()));

                for (const auto& app : excludedApps)
                {
                    if (title.contains(app))
                    {
                        return true;
                    }
                }
                return false;
            }

            inline bool check_excluded_app(const std::wstring& processPath, const std::wstring& title, const std::vector<std::wstring>& excludedApps)
            {
                bool res = find_app_name_in_path(processPath, excludedApps);

                if (!res)
                {
                    res = check_excluded_app_with_title(excludedApps, title);
                }

                return res;
            }
        }

        namespace Window
        {
            // Check if window is part of the shell or the taskbar.
            inline bool is_system_window(HWND hwnd, const char* class_name)
            {
                // We compare the HWND against HWND of the desktop and shell windows,
                // we also filter out some window class names know to belong to the taskbar.
                constexpr std::array system_classes = { "SysListView32", "WorkerW", "Shell_TrayWnd", "Shell_SecondaryTrayWnd", "Progman" };
                const std::array system_hwnds = { GetDesktopWindow(), GetShellWindow() };
                for (auto system_hwnd : system_hwnds)
                {
                    if (hwnd == system_hwnd)
                    {
                        return true;
                    }
                }
                for (const auto system_class : system_classes)
                {
                    if (!strcmp(system_class, class_name))
                    {
                        return true;
                    }
                }
                return false;
            }
        }
    }
}

// FancyZones WindowUtils
namespace WindowUtils
{
    // Non-Localizable strings
    namespace NonLocalizable
    {
        const wchar_t SystemAppsFolder[] = L"SYSTEMAPPS";
        const wchar_t System[] = L"WINDOWS/SYSTEM";
        const wchar_t System32[] = L"SYSTEM32";
        const wchar_t SystemWOW64[] = L"SYSTEMWOW64";
        const char SplashClassName[] = "MsoSplash";
        const wchar_t CoreWindow[] = L"WINDOWS.UI.CORE.COREWINDOW";
        const wchar_t SearchUI[] = L"SEARCHUI.EXE";
        const wchar_t ProjectsSnapshotTool[] = L"PROJECTSSNAPSHOTTOOL";
        const wchar_t ProjectsEditor[] = L"PROJECTSEDITOR";
        const wchar_t ProjectsLauncher[] = L"PROJECTSLAUNCHER";
    }

    inline bool IsRoot(HWND window) noexcept
    {
        return GetAncestor(window, GA_ROOT) == window;
    }

    inline bool IsMaximized(HWND window) noexcept
    {
        WINDOWPLACEMENT placement{};
        if (GetWindowPlacement(window, &placement) &&
            placement.showCmd == SW_SHOWMAXIMIZED)
        {
            return true;
        }
        return false;
    }

    constexpr bool HasStyle(LONG style, LONG styleToCheck) noexcept
    {
        return ((style & styleToCheck) == styleToCheck);
    }

    inline bool IsExcludedByDefault(HWND window, const std::wstring& processPath, const std::wstring& title)
    {
        std::wstring processPathUpper = processPath;
        CharUpperBuffW(processPathUpper.data(), static_cast<DWORD>(processPathUpper.length()));

        static std::vector<std::wstring> defaultExcludedFolders = { NonLocalizable::SystemAppsFolder, NonLocalizable::System, NonLocalizable::System32, NonLocalizable::SystemWOW64 };
        if (Common::Utils::ExcludedApps::find_folder_in_path(processPathUpper, defaultExcludedFolders))
        {
            return true;
        }

        std::array<char, 256> className;
        GetClassNameA(window, className.data(), static_cast<int>(className.size()));
        if (Common::Utils::Window::is_system_window(window, className.data()))
        {
            return true;
        }

        if (strcmp(NonLocalizable::SplashClassName, className.data()) == 0)
        {
            return true;
        }

        static std::vector<std::wstring> defaultExcludedApps = { NonLocalizable::CoreWindow, NonLocalizable::SearchUI, NonLocalizable::ProjectsEditor, NonLocalizable::ProjectsLauncher, NonLocalizable::ProjectsSnapshotTool };
        return (Common::Utils::ExcludedApps::check_excluded_app(processPathUpper, title, defaultExcludedApps));
    }

    inline RECT GetWindowRect(HWND window)
    {
        RECT rect;
        if (GetWindowRect(window, &rect))
        {
            float width = static_cast<float>(rect.right - rect.left);
            float height = static_cast<float>(rect.bottom - rect.top);
            float originX = static_cast<float>(rect.left);
            float originY = static_cast<float>(rect.top);

            Common::Display::DPIAware::InverseConvert(MonitorFromWindow(window, MONITOR_DEFAULTTONULL), width, height);
            Common::Display::DPIAware::InverseConvert(MonitorFromWindow(window, MONITOR_DEFAULTTONULL), originX, originY);

            return RECT(static_cast<LONG>(std::roundf(originX)),
                        static_cast<LONG>(std::roundf(originY)),
                        static_cast<LONG>(std::roundf(originX + width)),
                        static_cast<LONG>(std::roundf(originY + height)));
        }

        return rect;
    }
}

// addition for Projects
namespace WindowUtils
{
    inline bool IsMinimized(HWND window)
    {
        return IsIconic(window);
    }

    #define MAX_TITLE_LENGTH 255
    inline std::wstring GetWindowTitle(HWND window)
    {
        WCHAR title[MAX_TITLE_LENGTH];
        int len = GetWindowTextW(window, title, MAX_TITLE_LENGTH);
        if (len <= 0)
        {
            return {};
        }

        return std::wstring(title);
    }

    /*bool IsFullscreen(HWND window)
    {
        TODO
    }*/
}