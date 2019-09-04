#pragma once
#include <Windows.h>

/*
  win_hook_event - Windows Event Hook

  The PowerToys runner installs event hook functions for a range of events. See
  https://docs.microsoft.com/pl-pl/windows/win32/api/winuser/nf-winuser-setwineventhook
  for details.
  
  The intptr_t data event argument is a pointer to the WinHookEvent struct.

  The return value of the event handler is ignored.

  Example usage, that detects a window being resized:

  virtual intptr_t signal_event(const wchar_t* name, intptr_t data) override {
    if (wcscmp(name, win_hook_event) == 0) {
      auto& event = *(reinterpret_cast<WinHookEvent*>(data));
      switch (event.event) {
      case EVENT_SYSTEM_MOVESIZESTART:
        size_start(event.hwnd);
        break;
      case EVENT_SYSTEM_MOVESIZEEND:
        size_end(event.hwnd);
        break;
      default:
        break;
      }
    }
    return 0;
  }

  Taking to long to process the events has negative impact on the whole system
  performance. To address this, the events are signaled from a different
  thread, not from the event hook callback itself.
*/

namespace {
  const wchar_t* win_hook_event = L"win_hook_event";
}

struct WinHookEvent {
  DWORD event;
  HWND hwnd;
  LONG idObject;
  LONG idChild;
  DWORD idEventThread;
  DWORD dwmsEventTime;
};
