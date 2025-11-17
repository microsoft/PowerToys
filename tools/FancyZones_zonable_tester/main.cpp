#include <Windows.h>
#include <iostream>
#include <array>
#include <vector>

std::wstring get_process_path(DWORD pid) noexcept
{
  auto process = OpenProcess(PROCESS_QUERY_INFORMATION | PROCESS_VM_READ, TRUE, pid);
  std::wstring name;
  if (process != INVALID_HANDLE_VALUE)
  {
    name.resize(MAX_PATH);
    DWORD name_length = static_cast<DWORD>(name.length());
    if (QueryFullProcessImageNameW(process, 0, static_cast<LPWSTR>(name.data()), &name_length) == 0)
    {
      name_length = 0;
    }
    name.resize(name_length);
    CloseHandle(process);
  }
  return name;
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
    EnumChildWindows(window, [](HWND hwnd, LPARAM param) -> BOOL {
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
    }, reinterpret_cast<LPARAM>(&new_pid));
    // If we have a new pid, get the new name.
    if (new_pid != pid)
    {
      return get_process_path(new_pid);
    }
  }
  return name;
}

std::string window_styles(LONG style)
{
  std::string result;
  if (style == 0)
    result = "WS_OVERLAPPED ";
#define TEST_STYLE(x) if ((style & x) == x) result += #x " ";
  TEST_STYLE(WS_POPUP);
  TEST_STYLE(WS_CHILD);
  TEST_STYLE(WS_MINIMIZE);
  TEST_STYLE(WS_VISIBLE);
  TEST_STYLE(WS_DISABLED);
  TEST_STYLE(WS_CLIPSIBLINGS);
  TEST_STYLE(WS_CLIPCHILDREN);
  TEST_STYLE(WS_MAXIMIZE);
  TEST_STYLE(WS_CAPTION);
  TEST_STYLE(WS_BORDER);
  TEST_STYLE(WS_DLGFRAME);
  TEST_STYLE(WS_VSCROLL);
  TEST_STYLE(WS_HSCROLL);
  TEST_STYLE(WS_SYSMENU);
  TEST_STYLE(WS_THICKFRAME);
  TEST_STYLE(WS_GROUP);
  TEST_STYLE(WS_TABSTOP);
  TEST_STYLE(WS_MINIMIZEBOX);
  TEST_STYLE(WS_MAXIMIZEBOX);
  TEST_STYLE(WS_ICONIC);
  TEST_STYLE(WS_SIZEBOX);
  TEST_STYLE(WS_TILEDWINDOW);
  TEST_STYLE(WS_OVERLAPPEDWINDOW);
  TEST_STYLE(WS_POPUPWINDOW);
  TEST_STYLE(WS_CHILDWINDOW);
#undef TEST_STYLE
  if (result.size() > 0)
    result.pop_back();
  return result;
}

std::string window_exstyles(LONG style)
{
  std::string result;
#define TEST_STYLE(x) if ((style & x) == x) result += #x " ";
  TEST_STYLE(WS_EX_DLGMODALFRAME);
  TEST_STYLE(WS_EX_NOPARENTNOTIFY);
  TEST_STYLE(WS_EX_TOPMOST);
  TEST_STYLE(WS_EX_ACCEPTFILES);
  TEST_STYLE(WS_EX_TRANSPARENT);
  TEST_STYLE(WS_EX_MDICHILD);
  TEST_STYLE(WS_EX_TOOLWINDOW);
  TEST_STYLE(WS_EX_WINDOWEDGE);
  TEST_STYLE(WS_EX_CLIENTEDGE);
  TEST_STYLE(WS_EX_CONTEXTHELP);
  TEST_STYLE(WS_EX_RIGHT);
  TEST_STYLE(WS_EX_LEFT);
  TEST_STYLE(WS_EX_RTLREADING);
  TEST_STYLE(WS_EX_LTRREADING);
  TEST_STYLE(WS_EX_LEFTSCROLLBAR);
  TEST_STYLE(WS_EX_RIGHTSCROLLBAR);
  TEST_STYLE(WS_EX_CONTROLPARENT);
  TEST_STYLE(WS_EX_STATICEDGE);
  TEST_STYLE(WS_EX_APPWINDOW);
  TEST_STYLE(WS_EX_OVERLAPPEDWINDOW);
  TEST_STYLE(WS_EX_PALETTEWINDOW);
  TEST_STYLE(WS_EX_LAYERED);
  TEST_STYLE(WS_EX_NOINHERITLAYOUT);
  TEST_STYLE(WS_EX_NOREDIRECTIONBITMAP);
  TEST_STYLE(WS_EX_LAYOUTRTL);
  TEST_STYLE(WS_EX_COMPOSITED);
#undef TEST_STYLE
  if (result.size() > 0)
    result.pop_back();
  return result;
}


bool is_system_window(HWND hwnd, const char* class_name)
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

#define TEST_IF(condition) std::cout<<"\t" << #condition <<": " <<((condition) ? "true, window not zonable" : "false")<<"\n"; if (condition) rv = false;

bool test_window(HWND window)
{
  std::cout << "\n";
  std::cout << "HWND:         0x" << window << "\n";
  DWORD pid;
  GetWindowThreadProcessId(window, &pid);
  std::cout << "PID:          0x" << std::hex << pid << "\n";
  std::cout << "FOREGROUND:   0x" << GetForegroundWindow() << "\n";

  auto style = GetWindowLongPtr(window, GWL_STYLE);
  auto exStyle = GetWindowLongPtr(window, GWL_EXSTYLE);
  std::cout << "style:        0x" << std::hex << style << ": " << window_styles(static_cast<LONG>(style)) << "\n";
  std::cout << "exStyle:      0x" << std::hex << exStyle << ": " << window_exstyles(static_cast<LONG>(exStyle)) << " \n";
  std::array<char, 256> class_name;
  GetClassNameA(window, class_name.data(), static_cast<int>(class_name.size()));
  std::cout << "Window class: '" << class_name.data() << "' equals:\n";
  auto process_path = get_process_path(window);
  std::wcout<< L"Process path: " << process_path << L"\n";
  bool rv = true;
  std::cout << "Testing if the window is zonable:\n";
  TEST_IF(GetAncestor(window, GA_ROOT) != window);
  TEST_IF(!IsWindowVisible(window));
  if ((style & WS_POPUP) == WS_POPUP &&
      (style & WS_THICKFRAME) == 0 &&
      (style & WS_MINIMIZEBOX) == 0 &&
      (style & WS_MAXIMIZEBOX) == 0)
  {
    std::cout << "\t(style & WS_POPUP) && no frame nor max/min buttons: true, window not zonable\n";
  }
  else
  {
    std::cout << "\t(style & WS_POPUP) && no frame nor max/min buttons: false\n";
  }
  TEST_IF((style & WS_CHILD) == WS_CHILD);
  TEST_IF((style & WS_DISABLED) == WS_DISABLED);
  TEST_IF((exStyle & WS_EX_TOOLWINDOW) == WS_EX_TOOLWINDOW);
  TEST_IF((exStyle & WS_EX_NOACTIVATE) == WS_EX_NOACTIVATE);
  TEST_IF(is_system_window(window, class_name.data()));
  if (strcmp(class_name.data(), "Windows.UI.Core.CoreWindow") == 0 &&
      process_path.ends_with(L"SearchUI.exe"))
  {
    std::cout << "\tapp is Cortana: true, window not zonable\n";
  }
  else
  {
    std::cout << "\tapp is Cortana: false\n";
  }
  TEST_IF(!no_visible_owner(window));
  return rv;
}

LRESULT CALLBACK LowLevelMouseProc(int nCode, WPARAM wParam, LPARAM lParam)
{
  static HWND hwnd = nullptr;
  if (nCode == HC_ACTION)
  {
    POINT point;
    GetCursorPos(&point);
    auto new_hwnd = WindowFromPoint(point);
    if (hwnd != new_hwnd) {
      hwnd = new_hwnd;
      if (test_window(hwnd))
      {
        std::cout << "Window is zonable\n";
      }
      else
      {
        std::cout << "Window is NOT zonable\n";
      }
    }
  }
  return CallNextHookEx(NULL, nCode, wParam, lParam);
}

int main()
{
  HHOOK hhkLowLevelKybd = SetWindowsHookEx(WH_MOUSE_LL, LowLevelMouseProc, 0, 0);
  MSG msg;
  while (!GetMessage(&msg, NULL, NULL, NULL))
  {
    TranslateMessage(&msg);
    DispatchMessage(&msg);
  }
  UnhookWindowsHookEx(hhkLowLevelKybd);
  return(0);
}
