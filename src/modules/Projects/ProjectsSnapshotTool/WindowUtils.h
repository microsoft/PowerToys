#pragma once

#include <Windows.h>

#include <algorithm>

namespace Common
{
    namespace Display
    {
        namespace DPIAware
        {
            void InverseConvert(HMONITOR monitor_handle, float& width, float& height);
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
    bool IsRoot(HWND window) noexcept;
    bool IsMaximized(HWND window) noexcept;

    constexpr bool HasStyle(LONG style, LONG styleToCheck) noexcept
    {
        return ((style & styleToCheck) == styleToCheck);
    }

    bool IsExcludedByDefault(HWND window, const std::wstring& processPath, const std::wstring& title);

    RECT GetWindowRect(HWND window);
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