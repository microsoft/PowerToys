#include "pch.h"
#include "win_hook_event.h"
#include "powertoy_module.h"
#include <mutex>
#include <deque>
#include <thread>

static std::mutex mutex;
static std::deque<WinHookEvent> hook_events;
static std::condition_variable dispatch_cv;

void intercept_system_menu_action(intptr_t);

static void CALLBACK win_hook_event_proc(HWINEVENTHOOK winEventHook,
                                         DWORD event,
                                         HWND window,
                                         LONG object,
                                         LONG child,
                                         DWORD eventThread,
                                         DWORD eventTime) {
  std::unique_lock lock(mutex);
  hook_events.push_back({ event,
                          window,
                          object,
                          child,
                          eventThread,
                          eventTime });
  lock.unlock();
  dispatch_cv.notify_one();
}

static bool running = false;
static std::thread dispatch_thread;
static void dispatch_thread_proc() {
  std::unique_lock lock(mutex);
  while (running) {
    dispatch_cv.wait(lock, []{ return !running || !hook_events.empty(); });
    if (!running)
      return;
    while (!hook_events.empty()) {
      auto event = hook_events.front();
      hook_events.pop_front();
      lock.unlock();
      intptr_t data = reinterpret_cast<intptr_t>(&event);
      intercept_system_menu_action(data);
      powertoys_events().signal_event(win_hook_event, data);
      lock.lock();
    }
  }
}

static HWINEVENTHOOK hook_handle;

void start_win_hook_event() {
  std::lock_guard lock(mutex);
  if (running)
    return;
  running = true;
  dispatch_thread = std::thread(dispatch_thread_proc);
  hook_handle = SetWinEventHook(EVENT_MIN, EVENT_MAX, nullptr, win_hook_event_proc, 0, 0, WINEVENT_OUTOFCONTEXT | WINEVENT_SKIPOWNPROCESS);
}

void stop_win_hook_event() {
  std::unique_lock lock(mutex);
  if (!running)
    return;
  running = false;
  UnhookWinEvent(hook_handle);
  lock.unlock();
  dispatch_cv.notify_one();
  dispatch_thread.join();
  lock.lock();
  hook_events.clear();
  hook_events.shrink_to_fit();
}

void intercept_system_menu_action(intptr_t data) {
  WinHookEvent* evt = reinterpret_cast<WinHookEvent*>(data);
  if (evt->event == EVNET_OBJECT_CREATE ||
      evt->event == EVENT_SYSTEM_MENUSTART ||
      evt->event == EVENT_OBJECT_INVOKED) {
    powertoys_events().handle_system_menu_action(*evt);
  }
}
