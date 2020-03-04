#include "pch.h"
#include "common.h"
#include <dwmapi.h>
#pragma comment(lib, "dwmapi.lib")
#include <strsafe.h>
#include <sddl.h>
#include "version.h"

#pragma comment(lib, "advapi32.lib")

namespace localized_strings
{
    const wchar_t LAST_ERROR_FORMAT_STRING[] = L"%s failed with error %d: %s";
    const wchar_t LAST_ERROR_TITLE_STRING[] = L"Error";
}

std::optional<RECT> get_button_pos(HWND hwnd)
{
    RECT button;
    if (DwmGetWindowAttribute(hwnd, DWMWA_CAPTION_BUTTON_BOUNDS, &button, sizeof(RECT)) == S_OK)
    {
        return button;
    }
    else
    {
        return {};
    }
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

std::optional<POINT> get_mouse_pos()
{
    POINT point;
    if (GetCursorPos(&point) == 0)
    {
        return {};
    }
    else
    {
        return point;
    }
}

// Test if a window is part of the shell or the task bar.
// We compare the HWND against HWND of the desktop and shell windows,
// we also filter out some window class names know to belong to
// the taskbar.
static bool is_system_window(HWND hwnd, const char* class_name)
{
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

static bool no_visible_owner(HWND window) noexcept
{
    auto owner = GetWindow(window, GW_OWNER);
    if (owner == nullptr)
    {
        return true; // There is no owner at all
    }
    if (!IsWindowVisible(owner))
    {
        return true; // Owner is invisible
    }
    RECT rect;
    if (!GetWindowRect(owner, &rect))
    {
        return false; // Could not get the rect, return true (and filter out the window) just in case
    }
    // Return false (and allow the window to be zonable) if the owner window size is zero
    // It is enough that the window is zero-sized in one dimension only.
    return rect.top == rect.bottom || rect.left == rect.right;
}

FancyZonesFilter get_fancyzones_filtered_window(HWND window)
{
    FancyZonesFilter result;
    if (GetAncestor(window, GA_ROOT) != window || !IsWindowVisible(window))
    {
        return result;
    }
    auto style = GetWindowLong(window, GWL_STYLE);
    auto exStyle = GetWindowLong(window, GWL_EXSTYLE);
    // WS_POPUP need to have a border or minimize/maximize buttons,
    // otherwise the window is "not interesting"
    if ((style & WS_POPUP) == WS_POPUP &&
        (style & WS_THICKFRAME) == 0 &&
        (style & WS_MINIMIZEBOX) == 0 &&
        (style & WS_MAXIMIZEBOX) == 0)
    {
        return result;
    }
    if ((style & WS_CHILD) == WS_CHILD ||
        (style & WS_DISABLED) == WS_DISABLED ||
        (exStyle & WS_EX_TOOLWINDOW) == WS_EX_TOOLWINDOW ||
        (exStyle & WS_EX_NOACTIVATE) == WS_EX_NOACTIVATE)
    {
        return result;
    }
    std::array<char, 256> class_name;
    GetClassNameA(window, class_name.data(), static_cast<int>(class_name.size()));
    if (is_system_window(window, class_name.data()))
    {
        return result;
    }
    auto process_path = get_process_path(window);
    // Check for Cortana:
    if (strcmp(class_name.data(), "Windows.UI.Core.CoreWindow") == 0 &&
        process_path.ends_with(L"SearchUI.exe"))
    {
        return result;
    }
    result.process_path = std::move(process_path);
    result.standard_window = true;
    result.no_visible_owner = no_visible_owner(window);
    result.zonable = result.standard_window && result.no_visible_owner;
    return result;
}

ShortcutGuideFilter get_shortcutguide_filtered_window()
{
    ShortcutGuideFilter result;
    auto active_window = GetForegroundWindow();
    active_window = GetAncestor(active_window, GA_ROOT);
    if (!IsWindowVisible(active_window))
    {
        return result;
    }
    auto style = GetWindowLong(active_window, GWL_STYLE);
    auto exStyle = GetWindowLong(active_window, GWL_EXSTYLE);
    if ((style & WS_CHILD) == WS_CHILD ||
        (style & WS_DISABLED) == WS_DISABLED ||
        (exStyle & WS_EX_TOOLWINDOW) == WS_EX_TOOLWINDOW ||
        (exStyle & WS_EX_NOACTIVATE) == WS_EX_NOACTIVATE)
    {
        return result;
    }
    std::array<char, 256> class_name;
    GetClassNameA(active_window, class_name.data(), static_cast<int>(class_name.size()));
    if (is_system_window(active_window, class_name.data()))
    {
        return result;
    }
    static HWND cortanda_hwnd = nullptr;
    if (cortanda_hwnd == nullptr)
    {
        if (strcmp(class_name.data(), "Windows.UI.Core.CoreWindow") == 0 &&
            get_process_path(active_window).ends_with(L"SearchUI.exe"))
        {
            cortanda_hwnd = active_window;
            return result;
        }
    }
    else if (cortanda_hwnd == active_window)
    {
        return result;
    }
    result.hwnd = active_window;
    // In reality, Windows Snap works if even one of those styles is set
    // for a window, it is just limited. If there is no WS_MAXIMIZEBOX using
    // WinKey + Up just won't maximize the window. Similary, without
    // WS_MINIMIZEBOX the window will not get minimized. A "Save As..." dialog
    // is a example of such window - it can be snapped to both sides and to
    // all screen conrers, but will not get maximized nor minimized.
    // For now, since ShortcutGuide can only disable entire "Windows Controls"
    // group, we require that the window supports all the options.
    result.snappable = ((style & WS_MAXIMIZEBOX) == WS_MAXIMIZEBOX) &&
                       ((style & WS_MINIMIZEBOX) == WS_MINIMIZEBOX) &&
                       ((style & WS_THICKFRAME) == WS_THICKFRAME);
    return result;
}

int width(const RECT& rect)
{
    return rect.right - rect.left;
}

int height(const RECT& rect)
{
    return rect.bottom - rect.top;
}

bool operator<(const RECT& lhs, const RECT& rhs)
{
    auto lhs_tuple = std::make_tuple(lhs.left, lhs.right, lhs.top, lhs.bottom);
    auto rhs_tuple = std::make_tuple(rhs.left, rhs.right, rhs.top, rhs.bottom);
    return lhs_tuple < rhs_tuple;
}

RECT keep_rect_inside_rect(const RECT& small_rect, const RECT& big_rect)
{
    RECT result = small_rect;
    if ((result.right - result.left) > (big_rect.right - big_rect.left))
    {
        // small_rect is too big horizontally. resize it.
        result.right = big_rect.right;
        result.left = big_rect.left;
    }
    else
    {
        if (result.right > big_rect.right)
        {
            // move the rect left.
            result.left -= result.right - big_rect.right;
            result.right -= result.right - big_rect.right;
        }

        if (result.left < big_rect.left)
        {
            // move the rect right.
            result.right += big_rect.left - result.left;
            result.left += big_rect.left - result.left;
        }
    }

    if ((result.bottom - result.top) > (big_rect.bottom - big_rect.top))
    {
        // small_rect is too big vertically. resize it.
        result.bottom = big_rect.bottom;
        result.top = big_rect.top;
    }
    else
    {
        if (result.bottom > big_rect.bottom)
        {
            // move the rect up.
            result.top -= result.bottom - big_rect.bottom;
            result.bottom -= result.bottom - big_rect.bottom;
        }

        if (result.top < big_rect.top)
        {
            // move the rect down.
            result.bottom += big_rect.top - result.top;
            result.top += big_rect.top - result.top;
        }
    }
    return result;
}

int run_message_loop()
{
    MSG msg;
    while (GetMessage(&msg, NULL, 0, 0))
    {
        TranslateMessage(&msg);
        DispatchMessage(&msg);
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
        return UNKNONW;
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
        return UNKNONW;
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

bool is_process_elevated()
{
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

bool run_elevated(const std::wstring& file, const std::wstring& params)
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

    if (ShellExecuteExW(&exec_info))
    {
        return exec_info.hProcess != nullptr;
    }
    else
    {
        return false;
    }
}

bool run_non_elevated(const std::wstring& file, const std::wstring& params)
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

    PROCESS_INFORMATION process_info = { 0 };
    auto succedded = CreateProcessW(file.c_str(),
                                    const_cast<LPWSTR>(executable_args.c_str()),
                                    nullptr,
                                    nullptr,
                                    FALSE,
                                    EXTENDED_STARTUPINFO_PRESENT,
                                    nullptr,
                                    nullptr,
                                    &siex.StartupInfo,
                                    &process_info);
    if (process_info.hProcess)
    {
        CloseHandle(process_info.hProcess);
    }
    if (process_info.hThread)
    {
        CloseHandle(process_info.hThread);
    }
    return succedded;
}

bool run_same_elevation(const std::wstring& file, const std::wstring& params)
{
    auto executable_args = L"\"" + file + L"\"";
    if (!params.empty())
    {
        executable_args += L" " + params;
    }
    STARTUPINFO si = { 0 };
    PROCESS_INFORMATION pi = { 0 };
    auto succedded = CreateProcessW(file.c_str(),
                                    const_cast<LPWSTR>(executable_args.c_str()),
                                    nullptr,
                                    nullptr,
                                    FALSE,
                                    0,
                                    nullptr,
                                    nullptr,
                                    &si,
                                    &pi);
    if (pi.hProcess)
    {
        CloseHandle(pi.hProcess);
    }
    if (pi.hThread)
    {
        CloseHandle(pi.hThread);
    }
    return succedded;
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
        // It is a UWP app. We will enumarate the windows and look for one created
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

std::wstring get_module_folderpath(HMODULE mod)
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

    PathRemoveFileSpecW(buffer);
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
