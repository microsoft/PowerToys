#include "pch.h"
#include "common.h"
#include <dwmapi.h>
#pragma comment(lib, "dwmapi.lib")
#include <strsafe.h>
#include <sddl.h>

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

HWND get_filtered_active_window() {
  static auto desktop = GetDesktopWindow();
  static auto shell = GetShellWindow();
  auto active_window = GetForegroundWindow();
  active_window = GetAncestor(active_window, GA_ROOT);

  if (active_window == desktop || active_window == shell) {
    return nullptr;
  }
  
  auto window_styles = GetWindowLong(active_window, GWL_STYLE);
  if ((window_styles & WS_CHILD) || (window_styles & WS_DISABLED)) {
    return nullptr;
  }
  
  window_styles = GetWindowLong(active_window, GWL_EXSTYLE);
  if ((window_styles & WS_EX_TOOLWINDOW) ||(window_styles & WS_EX_NOACTIVATE)) {
    return nullptr;
  }
  
  char class_name[256] = "";
  GetClassNameA(active_window, class_name, 256);
  if (strcmp(class_name, "SysListView32") == 0 ||
      strcmp(class_name, "WorkerW") == 0 ||
      strcmp(class_name, "Shell_TrayWnd") == 0 ||
      strcmp(class_name, "Shell_SecondaryTrayWnd") == 0 ||
      strcmp(class_name, "Progman") == 0) {
    return nullptr;
  }
  return active_window;
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