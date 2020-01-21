#pragma once
#include <vector>
#include <string>
#include <mutex>
#include <Windows.h>

struct WindowInfo {
  // Path to the process executable
  std::wstring process_path;
  // HWND of the window
  HWND hwnd = nullptr;
  // Does window have an owner or a parent
  bool has_owner = false;
  // Is window - more or less - a "standard" window - i.e. one that FancyZones will zone by default
  bool standard = false;
  // Is window resizable
  bool resizable = false;
};

class HWNDDataCache {
public:
  WindowInfo get_window_info(HWND hwnd);
private:
  // Return pointer to our internal cache - we cannot pass this to user
  // since next call to get_* might invalidate that pointer
  WindowInfo* get_internal(HWND hwnd);
  WindowInfo* get_from_cache(HWND root, DWORD pid);
  WindowInfo* put_in_cache(HWND root, DWORD pid);
  // Various validation routines
  bool is_invalid_hwnd(HWND hwnd) const;
  bool is_invalid_class(HWND hwnd) const;
  bool is_uwp_app(HWND hwnd) const;
  bool is_invalid_uwp_app(const std::wstring& binary_path) const;

  // List of HWNDs that are not interesting - like desktop, cortana, etc
  std::vector<HWND> invalid_hwnds = { GetDesktopWindow(), GetShellWindow() };
  // List of invalid window classes - things like start menu, etc.
  std::vector<const char*> invalid_classes = { "SysListView32", "WorkerW", "Shell_TrayWnd", "Shell_SecondaryTrayWnd", "Progman" };
  // List of invalid persistent UWP app - like Cortana
  std::vector<std::wstring> invalid_uwp_apps = { L"SearchUI.exe" };

  // Cache for HWND/PID pair to process path. A collision here, where a new process
  // not in cache gets to reuse a cached PID and then reuses the same HWND handle
  // seems unlikely.
  std::mutex mutex;
  // Handle timestamp wrap
  unsigned next_timestamp();
  unsigned current_timestamp = 0;
  struct Entry {
    DWORD pid = 0;
    // access time - when retiring element from cache we pick
    // one with minimal atime value. We update this value
    // every time we query the cache 
    unsigned atime = 0;
    WindowInfo data;
  };
  std::vector<Entry> cache{ 32 };
};

extern HWNDDataCache hwnd_cache;
