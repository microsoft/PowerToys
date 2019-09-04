#pragma once
#include <Windows.h>

/*
  ll_keyboard - Lowlevel Keyboard Hook

  The PowerToys runner installs low-level keyboard hook using
    SetWindowsHookEx(WH_KEYBOARD_LL, ...)
  See https://docs.microsoft.com/en-us/previous-versions/windows/desktop/legacy/ms644985(v%3Dvs.85)
  for details.

  When a keyboard event is signaled and ncCode equals HC_ACTION, the wParam
  and lParam event parameters are passed to all subscribed clients in
  the LowlevelKeyboardEvent struct.

  The intptr_t data event argument is a pointer to the LowlevelKeyboardEvent struct.

  A non-zero return value from any of the subscribed PowerToys will cause
  the runner hook proc to return 1, thus swallowing the keyboard event.

  Example usage, that makes Windows ignore the L key:

  virtual intptr_t signal_event(const wchar_t* name, intptr_t data) override {
    if (wcscmp(name, ll_keyboard) == 0) {
      auto& event = *(reinterpret_cast<LowlevelKeyboardEvent*>(data));
      // The L key has vkCode of 0x4C
      if (event.wParam ==  WM_KEYDOWN && event.lParam->vkCode == 0x4C) {
        return 1;
      } else {
        return 0;
      }
    } else {
      return 0;
    }
  }
*/

namespace {
  const wchar_t* ll_keyboard = L"ll_keyboard";
}

struct LowlevelKeyboardEvent {
  KBDLLHOOKSTRUCT* lParam;
  WPARAM wParam;
};
