#pragma once
#define WIN32_LEAN_AND_MEAN
#include <windows.h>
#include <shlwapi.h>

#include <string>
#include <thread>

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

inline std::wstring get_module_filename(HMODULE mod = nullptr)
{
    wchar_t buffer[MAX_PATH + 1];
    DWORD actual_length = GetModuleFileNameW(mod, buffer, MAX_PATH);
    if (GetLastError() == ERROR_INSUFFICIENT_BUFFER)
    {
        const DWORD long_path_length = 0xFFFF; // should be always enough
        std::wstring long_filename(long_path_length, L'\0');
        actual_length = GetModuleFileNameW(mod, long_filename.data(), long_path_length);
        return long_filename.substr(0, actual_length);
    }
    return { buffer, actual_length };
}

inline std::wstring get_module_folderpath(HMODULE mod = nullptr, const bool removeFilename = true)
{
    wchar_t buffer[MAX_PATH + 1];
    DWORD actual_length = GetModuleFileNameW(mod, buffer, MAX_PATH);
    if (GetLastError() == ERROR_INSUFFICIENT_BUFFER)
    {
        const DWORD long_path_length = 0xFFFF; // should be always enough
        std::wstring long_filename(long_path_length, L'\0');
        actual_length = GetModuleFileNameW(mod, long_filename.data(), long_path_length);
        PathRemoveFileSpecW(long_filename.data());
        long_filename.resize(std::wcslen(long_filename.data()));
        long_filename.shrink_to_fit();
        return long_filename;
    }

    if (removeFilename)
    {
        PathRemoveFileSpecW(buffer);
    }
    return { buffer, static_cast<uint64_t>(lstrlenW(buffer))};
}
