#pragma once

#include <vector>
#include <string>
#include <Windows.h>

#include "common.h"

class HWNDDataCache {
public:
  WindowAndProcPath get_window_and_path(HWND hwnd);
  HWND get_window(HWND hwnd);
private:
  // Return pointer to our internal cache - we cannot pass this to user
  // since next call to get_* might invalidate that pointer
  WindowAndProcPath* get_internal(HWND hwnd);
  WindowAndProcPath* get_from_cache(HWND root, DWORD pid);
  WindowAndProcPath* put_in_cache(HWND root, DWORD pid);
  // Various validation routines
  bool is_invalid_hwnd(HWND hwnd) const;
  bool is_invalid_class(HWND hwnd) const;
  bool is_invalid_style(HWND hwnd) const;
  bool is_uwp_app(HWND hwnd) const;
  bool is_invalid_uwp_app(const std::wstring& binary_path) const;

  // List of HWNDs that are not interesting - like desktop, cortana, etc
  std::vector<HWND> invalid_hwnds = { GetDesktopWindow(), GetShellWindow() };
  // List of invalid window basic styles
  std::vector<LONG> invalid_basic_styles = { WS_CHILD, WS_DISABLED, DS_ABSALIGN, DS_SYSMODAL, DS_LOCALEDIT,
                                             DS_SETFONT, DS_MODALFRAME, DS_NOIDLEMSG, DS_SETFOREGROUND, DS_3DLOOK,
                                             DS_FIXEDSYS, DS_NOFAILCREATE, DS_CONTROL, DS_CENTER, DS_CENTERMOUSE,
                                             DS_CONTEXTHELP, DS_SHELLFONT };
  // List of invalid window extended styles
  std::vector<LONG> invalid_ext_styles = { WS_EX_TOOLWINDOW, WS_EX_NOACTIVATE };
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
    WindowAndProcPath data;
  };
  std::vector<Entry> cache{ 32 };
};

extern HWNDDataCache hwnd_cache;
