#include "pch.h"
#include "common.h"
#include <dwmapi.h>
#pragma comment(lib, "dwmapi.lib")
#include <strsafe.h>
#include <sddl.h>
#include "version.h"

#include <wil/resource.h>

#pragma comment(lib, "advapi32.lib")
#pragma comment(lib, "shlwapi.lib")

namespace localized_strings
{
    const wchar_t LAST_ERROR_FORMAT_STRING[] = L"%s failed with error %d: %s";
    const wchar_t LAST_ERROR_TITLE_STRING[] = L"Error";
}

std::optional<RECT> get_window_pos(HWND hwnd)
{
    RECT window;
    if (DwmGetWindowAttribute(hwnd, DWMWA_EXTENDED_FRAME_BOUNDS, &window, sizeof(window)) == S_OK)
    {
        return window;
    }
    else
    {
        return {};
    }
}

bool is_system_window(HWND hwnd, const char* class_name)
{
    // We compare the HWND against HWND of the desktop and shell windows,
    // we also filter out some window class names know to belong to the taskbar.
    static auto system_classes = { "SysListView32", "WorkerW", "Shell_TrayWnd", "Shell_SecondaryTrayWnd", "Progman" };
    static auto system_hwnds = { GetDesktopWindow(), GetShellWindow() };
    for (auto system_hwnd : system_hwnds)
    {
        if (hwnd == system_hwnd)
        {
            return true;
        }
    }
    for (const auto& system_class : system_classes)
    {
        if (strcmp(system_class, class_name) == 0)
        {
            return true;
        }
    }
    return false;
}

int run_message_loop(const bool until_idle, const std::optional<uint32_t> timeout_seconds)
{
    MSG msg{};
    bool stop = false;
    UINT_PTR timerId = 0;
    if (timeout_seconds.has_value())
    {
        timerId = SetTimer(nullptr, 0, *timeout_seconds * 1000, nullptr);
    }

    while (!stop && GetMessageW(&msg, nullptr, 0, 0))
    {
        TranslateMessage(&msg);
        DispatchMessageW(&msg);
        stop = until_idle && !PeekMessageW(&msg, nullptr, 0, 0, PM_NOREMOVE);
        stop = stop || (msg.message == WM_TIMER && msg.wParam == timerId);
    }
    if (timeout_seconds.has_value())
    {
        KillTimer(nullptr, timerId);
    }
    return static_cast<int>(msg.wParam);
}

std::optional<std::wstring> get_last_error_message(const DWORD dw)
{
    std::optional<std::wstring> message;
    try
    {
        const auto msg = std::system_category().message(dw);
        message.emplace(begin(msg), end(msg));
    }
    catch (...)
    {
    }
    return message;
}

void show_last_error_message(LPCWSTR lpszFunction, DWORD dw)
{
    const auto system_message = get_last_error_message(dw);
    if (!system_message.has_value())
    {
        return;
    }
    LPWSTR lpDisplayBuf = (LPWSTR)LocalAlloc(LMEM_ZEROINIT, (system_message->size() + lstrlenW(lpszFunction) + 40) * sizeof(WCHAR));
    if (lpDisplayBuf != NULL)
    {
        StringCchPrintfW(lpDisplayBuf,
                         LocalSize(lpDisplayBuf) / sizeof(WCHAR),
                         localized_strings::LAST_ERROR_FORMAT_STRING,
                         lpszFunction,
                         dw,
                         system_message->c_str());
        MessageBoxW(NULL, (LPCTSTR)lpDisplayBuf, localized_strings::LAST_ERROR_TITLE_STRING, MB_OK);
        LocalFree(lpDisplayBuf);
    }
}

WindowState get_window_state(HWND hwnd)
{
    WINDOWPLACEMENT placement;
    placement.length = sizeof(WINDOWPLACEMENT);

    if (GetWindowPlacement(hwnd, &placement) == 0)
    {
        return UNKNOWN;
    }

    if (placement.showCmd == SW_MINIMIZE || placement.showCmd == SW_SHOWMINIMIZED || IsIconic(hwnd))
    {
        return MINIMIZED;
    }

    if (placement.showCmd == SW_MAXIMIZE || placement.showCmd == SW_SHOWMAXIMIZED)
    {
        return MAXIMIZED;
    }

    auto rectp = get_window_pos(hwnd);
    if (!rectp)
    {
        return UNKNOWN;
    }

    auto rect = *rectp;
    MONITORINFO monitor;
    monitor.cbSize = sizeof(MONITORINFO);
    auto h_monitor = MonitorFromWindow(hwnd, MONITOR_DEFAULTTONEAREST);
    GetMonitorInfo(h_monitor, &monitor);
    bool top_left = monitor.rcWork.top == rect.top && monitor.rcWork.left == rect.left;
    bool bottom_left = monitor.rcWork.bottom == rect.bottom && monitor.rcWork.left == rect.left;
    bool top_right = monitor.rcWork.top == rect.top && monitor.rcWork.right == rect.right;
    bool bottom_right = monitor.rcWork.bottom == rect.bottom && monitor.rcWork.right == rect.right;

    if (top_left && bottom_left)
        return SNAPED_LEFT;
    if (top_left)
        return SNAPED_TOP_LEFT;
    if (bottom_left)
        return SNAPED_BOTTOM_LEFT;
    if (top_right && bottom_right)
        return SNAPED_RIGHT;
    if (top_right)
        return SNAPED_TOP_RIGHT;
    if (bottom_right)
        return SNAPED_BOTTOM_RIGHT;

    return RESTORED;
}

bool is_process_elevated(const bool use_cached_value)
{
    auto detection_func = []() {
        HANDLE token = nullptr;
        bool elevated = false;

        if (OpenProcessToken(GetCurrentProcess(), TOKEN_QUERY, &token))
        {
            TOKEN_ELEVATION elevation;
            DWORD size;
            if (GetTokenInformation(token, TokenElevation, &elevation, sizeof(elevation), &size))
            {
                elevated = (elevation.TokenIsElevated != 0);
            }
        }

        if (token)
        {
            CloseHandle(token);
        }

        return elevated;
    };
    static const bool cached_value = detection_func();
    return use_cached_value ? cached_value : detection_func();
}

bool drop_elevated_privileges()
{
    HANDLE token = nullptr;
    LPCTSTR lpszPrivilege = SE_SECURITY_NAME;
    if (!OpenProcessToken(GetCurrentProcess(), TOKEN_ADJUST_DEFAULT | WRITE_OWNER, &token))
    {
        return false;
    }

    PSID medium_sid = NULL;
    if (!::ConvertStringSidToSid(SDDL_ML_MEDIUM, &medium_sid))
    {
        return false;
    }

    TOKEN_MANDATORY_LABEL label = { 0 };
    label.Label.Attributes = SE_GROUP_INTEGRITY;
    label.Label.Sid = medium_sid;
    DWORD size = (DWORD)sizeof(TOKEN_MANDATORY_LABEL) + ::GetLengthSid(medium_sid);

    BOOL result = SetTokenInformation(token, TokenIntegrityLevel, &label, size);
    LocalFree(medium_sid);
    CloseHandle(token);

    return result;
}

std::wstring get_process_path(DWORD pid) noexcept
{
    auto process = OpenProcess(PROCESS_QUERY_INFORMATION | PROCESS_VM_READ, TRUE, pid);
    std::wstring name;
    if (process != INVALID_HANDLE_VALUE)
    {
        name.resize(MAX_PATH);
        DWORD name_length = static_cast<DWORD>(name.length());
        if (QueryFullProcessImageNameW(process, 0, (LPWSTR)name.data(), &name_length) == 0)
        {
            name_length = 0;
        }
        name.resize(name_length);
        CloseHandle(process);
    }
    return name;
}

HANDLE run_elevated(const std::wstring& file, const std::wstring& params)
{
    SHELLEXECUTEINFOW exec_info = { 0 };
    exec_info.cbSize = sizeof(SHELLEXECUTEINFOW);
    exec_info.lpVerb = L"runas";
    exec_info.lpFile = file.c_str();
    exec_info.lpParameters = params.c_str();
    exec_info.hwnd = 0;
    exec_info.fMask = SEE_MASK_NOCLOSEPROCESS;
    exec_info.lpDirectory = 0;
    exec_info.hInstApp = 0;
    exec_info.nShow = SW_SHOWDEFAULT;

    return ShellExecuteExW(&exec_info) ? exec_info.hProcess : nullptr;
}

bool run_non_elevated(const std::wstring& file, const std::wstring& params, DWORD* returnPid)
{
    auto executable_args = L"\"" + file + L"\"";
    if (!params.empty())
    {
        executable_args += L" " + params;
    }

    HWND hwnd = GetShellWindow();
    if (!hwnd)
    {
        return false;
    }
    DWORD pid;
    GetWindowThreadProcessId(hwnd, &pid);

    winrt::handle process{ OpenProcess(PROCESS_CREATE_PROCESS, FALSE, pid) };
    if (!process)
    {
        return false;
    }

    SIZE_T size = 0;

    InitializeProcThreadAttributeList(nullptr, 1, 0, &size);
    auto pproc_buffer = std::make_unique<char[]>(size);
    auto pptal = reinterpret_cast<PPROC_THREAD_ATTRIBUTE_LIST>(pproc_buffer.get());

    if (!InitializeProcThreadAttributeList(pptal, 1, 0, &size))
    {
        return false;
    }

    HANDLE process_handle = process.get();
    if (!pptal || !UpdateProcThreadAttribute(pptal,
                                             0,
                                             PROC_THREAD_ATTRIBUTE_PARENT_PROCESS,
                                             &process_handle,
                                             sizeof(process_handle),
                                             nullptr,
                                             nullptr))
    {
        return false;
    }

    STARTUPINFOEX siex = { 0 };
    siex.lpAttributeList = pptal;
    siex.StartupInfo.cb = sizeof(siex);

    PROCESS_INFORMATION pi = { 0 };
    auto succeeded = CreateProcessW(file.c_str(),
                                    const_cast<LPWSTR>(executable_args.c_str()),
                                    nullptr,
                                    nullptr,
                                    FALSE,
                                    EXTENDED_STARTUPINFO_PRESENT,
                                    nullptr,
                                    nullptr,
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

    return succeeded;
}

bool run_same_elevation(const std::wstring& file, const std::wstring& params, DWORD* returnPid)
{
    auto executable_args = L"\"" + file + L"\"";
    if (!params.empty())
    {
        executable_args += L" " + params;
    }

    STARTUPINFO si = { 0 };
    PROCESS_INFORMATION pi = { 0 };
    auto succeeded = CreateProcessW(file.c_str(),
                                    const_cast<LPWSTR>(executable_args.c_str()),
                                    nullptr,
                                    nullptr,
                                    FALSE,
                                    0,
                                    nullptr,
                                    nullptr,
                                    &si,
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
    return succeeded;
}

std::wstring get_process_path(HWND window) noexcept
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

std::wstring get_product_version()
{
    static std::wstring version = L"v" + std::to_wstring(VERSION_MAJOR) +
                                  L"." + std::to_wstring(VERSION_MINOR) +
                                  L"." + std::to_wstring(VERSION_REVISION);

    return version;
}

std::wstring get_resource_string(UINT resource_id, HINSTANCE instance, const wchar_t* fallback)
{
    wchar_t* text_ptr;
    auto length = LoadStringW(instance, resource_id, reinterpret_cast<wchar_t*>(&text_ptr), 0);
    if (length == 0)
    {
        return fallback;
    }
    else
    {
        return { text_ptr, static_cast<std::size_t>(length) };
    }
}

std::wstring get_module_filename(HMODULE mod)
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

std::wstring get_module_folderpath(HMODULE mod, const bool removeFilename)
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
    return { buffer, (UINT)lstrlenW(buffer) };
}

// The function returns true in case of error since we want to return false
// only in case of a positive verification that the user is not an admin.
bool check_user_is_admin()
{
    auto freeMemory = [](PSID pSID, PTOKEN_GROUPS pGroupInfo) {
        if (pSID)
        {
            FreeSid(pSID);
        }
        if (pGroupInfo)
        {
            GlobalFree(pGroupInfo);
        }
    };

    HANDLE hToken;
    DWORD dwSize = 0, dwResult = 0;
    PTOKEN_GROUPS pGroupInfo;
    SID_IDENTIFIER_AUTHORITY SIDAuth = SECURITY_NT_AUTHORITY;
    PSID pSID = NULL;

    // Open a handle to the access token for the calling process.
    if (!OpenProcessToken(GetCurrentProcess(), TOKEN_QUERY, &hToken))
    {
        return true;
    }

    // Call GetTokenInformation to get the buffer size.
    if (!GetTokenInformation(hToken, TokenGroups, NULL, dwSize, &dwSize))
    {
        dwResult = GetLastError();
        if (dwResult != ERROR_INSUFFICIENT_BUFFER)
        {
            return true;
        }
    }

    // Allocate the buffer.
    pGroupInfo = (PTOKEN_GROUPS)GlobalAlloc(GPTR, dwSize);

    // Call GetTokenInformation again to get the group information.
    if (!GetTokenInformation(hToken, TokenGroups, pGroupInfo, dwSize, &dwSize))
    {
        freeMemory(pSID, pGroupInfo);
        return true;
    }

    // Create a SID for the BUILTIN\Administrators group.
    if (!AllocateAndInitializeSid(&SIDAuth, 2, SECURITY_BUILTIN_DOMAIN_RID, DOMAIN_ALIAS_RID_ADMINS, 0, 0, 0, 0, 0, 0, &pSID))
    {
        freeMemory(pSID, pGroupInfo);
        return true;
    }

    // Loop through the group SIDs looking for the administrator SID.
    for (DWORD i = 0; i < pGroupInfo->GroupCount; ++i)
    {
        if (EqualSid(pSID, pGroupInfo->Groups[i].Sid))
        {
            freeMemory(pSID, pGroupInfo);
            return true;
        }
    }

    freeMemory(pSID, pGroupInfo);
    return false;
}

bool find_app_name_in_path(const std::wstring& where, const std::vector<std::wstring>& what)
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

std::optional<std::string> exec_and_read_output(const std::wstring_view command, DWORD timeout_ms)
{
    SECURITY_ATTRIBUTES saAttr{ sizeof(saAttr) };
    saAttr.bInheritHandle = false;

    constexpr size_t bufferSize = 4096;
    // We must use a named pipe for async I/O
    char pipename[MAX_PATH + 1];
    if (!GetTempFileNameA(R"(\\.\pipe\)", "tmp", 1, pipename))
    {
        return std::nullopt;
    }

    wil::unique_handle readPipe{ CreateNamedPipeA(pipename, PIPE_ACCESS_INBOUND | FILE_FLAG_OVERLAPPED, PIPE_TYPE_BYTE | PIPE_READMODE_BYTE, PIPE_UNLIMITED_INSTANCES, bufferSize, bufferSize, 0, &saAttr) };

    saAttr.bInheritHandle = true;
    wil::unique_handle writePipe{ CreateFileA(pipename, GENERIC_WRITE, 0, &saAttr, OPEN_EXISTING, FILE_ATTRIBUTE_NORMAL, nullptr) };

    if (!readPipe || !writePipe)
    {
        return std::nullopt;
    }

    PROCESS_INFORMATION piProcInfo{};
    STARTUPINFOW siStartInfo{ sizeof(siStartInfo) };

    siStartInfo.hStdError = writePipe.get();
    siStartInfo.hStdOutput = writePipe.get();
    siStartInfo.dwFlags |= STARTF_USESTDHANDLES | STARTF_USESHOWWINDOW;
    siStartInfo.wShowWindow = SW_HIDE;

    std::wstring cmdLine{ command };
    if (!CreateProcessW(nullptr,
                        cmdLine.data(),
                        nullptr,
                        nullptr,
                        true,
                        NORMAL_PRIORITY_CLASS | CREATE_NEW_CONSOLE,
                        nullptr,
                        nullptr,
                        &siStartInfo,
                        &piProcInfo))
    {
        return std::nullopt;
    }
    // Child process inherited the write end of the pipe, we can close it now
    writePipe.reset();

    auto closeProcessHandles = wil::scope_exit([&] {
        CloseHandle(piProcInfo.hThread);
        CloseHandle(piProcInfo.hProcess);
    });

    std::string childOutput;
    bool processExited = false;
    for (;;)
    {
        char buffer[bufferSize];
        DWORD gotBytes = 0;
        wil::unique_handle IOEvent{ CreateEventW(nullptr, true, false, nullptr) };
        OVERLAPPED overlapped{ .hEvent = IOEvent.get() };
        ReadFile(readPipe.get(), buffer, sizeof(buffer), nullptr, &overlapped);

        const std::array<HANDLE, 2> handlesToWait = { overlapped.hEvent, piProcInfo.hProcess };
        switch (WaitForMultipleObjects(1 + !processExited, handlesToWait.data(), false, timeout_ms))
        {
        case WAIT_OBJECT_0 + 1:
            if (!processExited)
            {
                // When the process exits, we can reduce timeout and read the rest of the output w/o possibly big timeout
                timeout_ms = 1000;
                processExited = true;
                closeProcessHandles.reset();
            }
            [[fallthrough]];
        case WAIT_OBJECT_0:
            if (GetOverlappedResultEx(readPipe.get(), &overlapped, &gotBytes, timeout_ms, true))
            {
                childOutput += std::string_view{ buffer, gotBytes };
                break;
            }
            // Timeout
            [[fallthrough]];
        default:
            goto exit;
        }
    }
exit:
    CancelIo(readPipe.get());
    return childOutput;
}