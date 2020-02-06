#include "pch.h"
#include "common.h"
#include <dwmapi.h>
#pragma comment(lib, "dwmapi.lib")
#include <strsafe.h>
#include <sddl.h>
#include "version.h"

std::optional<RECT> get_button_pos(HWND hwnd) {
  RECT button;
  if (DwmGetWindowAttribute(hwnd, DWMWA_CAPTION_BUTTON_BOUNDS, &button, sizeof(RECT)) == S_OK) {
    return button;
  } else {
    return {};
  }
}

std::optional<RECT> get_window_pos(HWND hwnd) {
  RECT window;
  if (DwmGetWindowAttribute(hwnd, DWMWA_EXTENDED_FRAME_BOUNDS, &window, sizeof(window)) == S_OK) {
    return window;
  } else {
    return {};
  }
}

std::optional<POINT> get_mouse_pos() {
  POINT point;
  if (GetCursorPos(&point) == 0) {
    return {};
  } else {
    return point;
  }
}

static bool is_system_window(HWND hwnd, const char* class_name = nullptr) {
  static auto system_classes = { "SysListView32", "WorkerW", "Shell_TrayWnd", "Shell_SecondaryTrayWnd", "Progman" };
  static auto system_hwnds = { GetDesktopWindow(), GetShellWindow() };
  for (auto system_hwnd : system_hwnds) {
    if (hwnd == system_hwnd) {
      return true;
    }
  }
  std::array<char, 256> class_name_buff;
  if (class_name == nullptr) {
    GetClassNameA(hwnd, class_name_buff.data(), static_cast<int>(class_name_buff.size()));
    class_name = class_name_buff.data();
  }
  for (const auto& system_class : system_classes) {
    if (strcmp(system_class, class_name) == 0) {
      return true;
    }
  }
  return false;
}

WindowAndProcPath get_filtered_base_window_and_path(HWND window) {
  WindowAndProcPath result;
  auto root = GetAncestor(window, GA_ROOT);
  if (!IsWindowVisible(root)) { 
    return result;
  }
  auto style = GetWindowLong(root, GWL_STYLE);
  auto exStyle = GetWindowLong(root, GWL_EXSTYLE);
  // WS_POPUP need to have a border or minimize/maximize buttons,
  // otherwise the window is "not interesting"
  if ((style & WS_POPUP) == WS_POPUP &&
      (style & WS_THICKFRAME) == 0 &&
      (style & WS_MINIMIZEBOX) == 0 &&
      (style & WS_MAXIMIZEBOX) == 0) {
    return result;
  }
  if ((style & WS_CHILD) == WS_CHILD ||
      (style & WS_DISABLED) == WS_DISABLED ||
      (exStyle & WS_EX_TOOLWINDOW) == WS_EX_TOOLWINDOW ||
      (exStyle & WS_EX_NOACTIVATE) == WS_EX_NOACTIVATE) {
    return result;
  }
  std::array<char, 256> class_name;
  GetClassNameA(root, class_name.data(), static_cast<int>(class_name.size()));
  if (is_system_window(root, class_name.data())) {
    return result;
  }
  auto process_path = get_process_path(root);
  // Check for Cortana:
  if (strcmp(class_name.data(), "Windows.UI.Core.CoreWindow") == 0 &&
      process_path.ends_with(L"SearchUI.exe")) {
    return result;
  }
  result.hwnd = root;
  result.process_path = std::move(process_path);
  return result;
}

HWND get_filtered_active_window() {
  return get_filtered_base_window_and_path(GetForegroundWindow()).hwnd;
}

int width(const RECT& rect) {
  return rect.right - rect.left;
}

int height(const RECT& rect) {
  return rect.bottom - rect.top;
}

bool operator<(const RECT& lhs, const RECT& rhs) {
  auto lhs_tuple = std::make_tuple(lhs.left, lhs.right, lhs.top, lhs.bottom);
  auto rhs_tuple = std::make_tuple(rhs.left, rhs.right, rhs.top, rhs.bottom); 
  return lhs_tuple < rhs_tuple;
}

RECT keep_rect_inside_rect(const RECT& small_rect, const RECT& big_rect) {
  RECT result = small_rect;
  if ((result.right - result.left) > (big_rect.right - big_rect.left)) {
    // small_rect is too big horizontally. resize it.
    result.right = big_rect.right;
    result.left = big_rect.left;
  } else {
    if (result.right > big_rect.right) {
      // move the rect left.
      result.left -= result.right-big_rect.right;
      result.right -= result.right-big_rect.right;
    }

    if (result.left < big_rect.left) {
      // move the rect right.
      result.right += big_rect.left-result.left;
      result.left += big_rect.left-result.left;
    }
  }

  if ((result.bottom - result.top) > (big_rect.bottom - big_rect.top)) {
    // small_rect is too big vertically. resize it.
    result.bottom = big_rect.bottom;
    result.top = big_rect.top;
  } else {
    if (result.bottom > big_rect.bottom) {
      // move the rect up.
      result.top -= result.bottom-big_rect.bottom;
      result.bottom -= result.bottom-big_rect.bottom;
    }

    if (result.top < big_rect.top) {
      // move the rect down.
      result.bottom += big_rect.top-result.top;
      result.top += big_rect.top-result.top;
    }
  }
  return result;
}

int run_message_loop() {
  MSG msg;
  while (GetMessage(&msg, NULL, 0, 0)) {
    TranslateMessage(&msg);
    DispatchMessage(&msg);
  }
  return static_cast<int>(msg.wParam);
}

void show_last_error_message(LPCWSTR lpszFunction, DWORD dw) {
  // Retrieve the system error message for the error code
  LPWSTR lpMsgBuf = NULL;
  if (FormatMessageW(FORMAT_MESSAGE_ALLOCATE_BUFFER |
                     FORMAT_MESSAGE_FROM_SYSTEM |
                     FORMAT_MESSAGE_IGNORE_INSERTS,
                     NULL,
                     dw,
                     MAKELANGID(LANG_NEUTRAL, SUBLANG_DEFAULT),
                     lpMsgBuf,
                     0, NULL) > 0) {
    // Display the error message and exit the process
    LPWSTR lpDisplayBuf = (LPWSTR)LocalAlloc(LMEM_ZEROINIT, (lstrlenW(lpMsgBuf) + lstrlenW(lpszFunction) + 40) * sizeof(WCHAR));
    if (lpDisplayBuf != NULL) {
      StringCchPrintfW(lpDisplayBuf,
        LocalSize(lpDisplayBuf) / sizeof(WCHAR),
        L"%s failed with error %d: %s",
        lpszFunction, dw, lpMsgBuf);
      MessageBoxW(NULL, (LPCTSTR)lpDisplayBuf, L"Error", MB_OK);
      LocalFree(lpDisplayBuf);
    }
    LocalFree(lpMsgBuf);
  }
}

WindowState get_window_state(HWND hwnd) {
  WINDOWPLACEMENT placement;
  placement.length = sizeof(WINDOWPLACEMENT);

  if (GetWindowPlacement(hwnd, &placement) == 0) {
    return UNKNONW;
  }
  
  if (placement.showCmd == SW_MINIMIZE || placement.showCmd == SW_SHOWMINIMIZED || IsIconic(hwnd)) {
    return MINIMIZED;
  }
  
  if (placement.showCmd == SW_MAXIMIZE || placement.showCmd == SW_SHOWMAXIMIZED) {
    return MAXIMIZED;
  }
  
  auto rectp = get_window_pos(hwnd);
  if (!rectp) {
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
  
  if (top_left && bottom_left) return SNAPED_LEFT;
  if (top_left) return SNAPED_TOP_LEFT;
  if (bottom_left) return SNAPED_BOTTOM_LEFT;
  if (top_right && bottom_right) return SNAPED_RIGHT;
  if (top_right) return SNAPED_TOP_RIGHT;
  if (bottom_right) return SNAPED_BOTTOM_RIGHT;
  
  return RESTORED;
}

bool is_process_elevated() {
  HANDLE token = nullptr;
  bool elevated = false;

  if (OpenProcessToken(GetCurrentProcess(), TOKEN_QUERY, &token)) {
    TOKEN_ELEVATION elevation;
    DWORD size;
    if (GetTokenInformation(token, TokenElevation, &elevation, sizeof(elevation), &size)) {
      elevated = (elevation.TokenIsElevated != 0);
    }
  }

  if (token) {
    CloseHandle(token);
  }

  return elevated;
}

bool drop_elevated_privileges() {
  HANDLE token = nullptr;
  LPCTSTR lpszPrivilege = SE_SECURITY_NAME;
  if (!OpenProcessToken(GetCurrentProcess(), TOKEN_ADJUST_DEFAULT | WRITE_OWNER, &token)) {
    return false;
  }

  PSID medium_sid = NULL;
  if (!::ConvertStringSidToSid(SDDL_ML_MEDIUM, &medium_sid)) {
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

std::wstring get_process_path(DWORD pid) noexcept {
  auto process = OpenProcess(PROCESS_QUERY_INFORMATION | PROCESS_VM_READ, TRUE, pid);
  std::wstring name;
  if (process != INVALID_HANDLE_VALUE) {
    name.resize(MAX_PATH);
    DWORD name_length = static_cast<DWORD>(name.length());
    if (QueryFullProcessImageNameW(process, 0, (LPWSTR)name.data(), &name_length) == 0) {
      name_length = 0;
    }
    name.resize(name_length);
    CloseHandle(process);
  }
  return name;
}

bool run_elevated(const std::wstring& file, const std::wstring& params) {
  SHELLEXECUTEINFOW exec_info = { 0 };
  exec_info.cbSize = sizeof(SHELLEXECUTEINFOW);
  exec_info.lpVerb = L"runas";
  exec_info.lpFile = file.c_str();
  exec_info.lpParameters = params.c_str();
  exec_info.hwnd = 0;
  exec_info.fMask = SEE_MASK_NOCLOSEPROCESS;
  exec_info.lpDirectory = 0;
  exec_info.hInstApp = 0;

  if (ShellExecuteExW(&exec_info)) {
    return exec_info.hProcess != nullptr;
  } else {
    return false;
  }
}

bool run_non_elevated(const std::wstring& file, const std::wstring& params) {
  auto executable_args = L"\"" + file + L"\"";
  if (!params.empty()) {
    executable_args += L" " + params;
  }
  
  HWND hwnd = GetShellWindow();
  if (!hwnd) {
    return false;
  }
  DWORD pid;
  GetWindowThreadProcessId(hwnd, &pid);

  winrt::handle process{ OpenProcess(PROCESS_CREATE_PROCESS, FALSE, pid) };
  if (!process) {
    return false;
  }

  SIZE_T size = 0;

  InitializeProcThreadAttributeList(nullptr, 1, 0, &size);
  auto pproc_buffer = std::make_unique<char[]>(size);
  auto pptal = reinterpret_cast<PPROC_THREAD_ATTRIBUTE_LIST>(pproc_buffer.get());
  
  if (!InitializeProcThreadAttributeList(pptal, 1, 0, &size)) {
    return false;
  }

  HANDLE process_handle = process.get();
  if (!pptal || !UpdateProcThreadAttribute(pptal,
                                           0,
                                           PROC_THREAD_ATTRIBUTE_PARENT_PROCESS,
                                           &process_handle,
                                           sizeof(process_handle),
                                           nullptr,
                                           nullptr)) {
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
  if (process_info.hProcess) {
    CloseHandle(process_info.hProcess);
  }
  if (process_info.hThread) {
    CloseHandle(process_info.hThread);
  }
  return succedded;
}

bool run_same_elevation(const std::wstring& file, const std::wstring& params) {
  auto executable_args = L"\"" + file + L"\"";
  if (!params.empty()) {
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
  if (pi.hProcess) {
    CloseHandle(pi.hProcess);
  }
  if (pi.hThread) {
    CloseHandle(pi.hThread);
  }
  return succedded;
}


std::wstring get_process_path(HWND window) noexcept {
  const static std::wstring app_frame_host = L"ApplicationFrameHost.exe";
  DWORD pid{};
  GetWindowThreadProcessId(window, &pid);
  auto name = get_process_path(pid);
  if (name.length() >= app_frame_host.length() &&
      name.compare(name.length() - app_frame_host.length(), app_frame_host.length(), app_frame_host) == 0) {
    // It is a UWP app. We will enumarate the windows and look for one created
    // by something with a different PID
    DWORD new_pid = pid;
    EnumChildWindows(window, [](HWND hwnd, LPARAM param) -> BOOL {
      auto new_pid_ptr = reinterpret_cast<DWORD*>(param);
      DWORD pid;
      GetWindowThreadProcessId(hwnd, &pid);
      if (pid != *new_pid_ptr) {
        *new_pid_ptr = pid;
        return FALSE;
      } else {
        return TRUE;
      }
    }, reinterpret_cast<LPARAM>(&new_pid));
    // If we have a new pid, get the new name.
    if (new_pid != pid) {
      return get_process_path(new_pid);
    }
  }
  return name;
}

std::wstring get_product_version() {
  static std::wstring version = std::to_wstring(VERSION_MAJOR) +
    L"." + std::to_wstring(VERSION_MINOR) +
    L"." + std::to_wstring(VERSION_REVISION) +
    L"." + std::to_wstring(VERSION_BUILD);

  return version;
}

std::wstring get_resource_string(UINT resource_id, HINSTANCE instance, const wchar_t* fallback) {
  wchar_t* text_ptr;
  auto length = LoadStringW(instance, resource_id, reinterpret_cast<wchar_t*>(&text_ptr), 0);
  if (length == 0) {
    return fallback;
  } else {
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
