#include "pch.h"
#include "win_hook_event.h"
#include "powertoy_module.h"
#include <mutex>
#include <deque>
#include <thread>

static std::mutex s_mutex;
static std::deque<WinHookEvent> s_hook_events;
static std::condition_variable s_dispatch_cv;

void intercept_system_menu_action(intptr_t);

static void CALLBACK win_hook_event_proc(HWINEVENTHOOK winEventHook,
                                         DWORD event,
                                         HWND window,
                                         LONG object,
                                         LONG child,
                                         DWORD eventThread,
                                         DWORD eventTime)
{
    std::unique_lock lock(s_mutex);
    s_hook_events.push_back({ event,
                            window,
                            object,
                            child,
                            eventThread,
                            eventTime });
    lock.unlock();
    s_dispatch_cv.notify_one();
}

static bool s_running = false;
static std::thread s_dispatch_thread;
static void dispatch_thread_proc()
{
    std::unique_lock lock(s_mutex);
    while (s_running)
    {
        s_dispatch_cv.wait(lock, [] { return !s_running || !s_hook_events.empty(); });
        if (!s_running)
            return;
        while (!s_hook_events.empty())
        {
            auto event = s_hook_events.front();
            s_hook_events.pop_front();
            lock.unlock();
            intptr_t data = reinterpret_cast<intptr_t>(&event);
            intercept_system_menu_action(data);
            powertoys_events().signal_event(win_hook_event, data);
            lock.lock();
        }
    }
}

static HWINEVENTHOOK s_hook_handle;

void start_win_hook_event()
{
    std::lock_guard lock(s_mutex);
    if (s_running)
        return;
    s_running = true;
    s_dispatch_thread = std::thread(dispatch_thread_proc);
    s_hook_handle = SetWinEventHook(EVENT_MIN, EVENT_MAX, nullptr, win_hook_event_proc, 0, 0, WINEVENT_OUTOFCONTEXT | WINEVENT_SKIPOWNPROCESS);
}

void stop_win_hook_event()
{
    std::unique_lock lock(s_mutex);
    if (!s_running)
        return;
    s_running = false;
    UnhookWinEvent(s_hook_handle);
    lock.unlock();
    s_dispatch_cv.notify_one();
    s_dispatch_thread.join();
    lock.lock();
    s_hook_events.clear();
    s_hook_events.shrink_to_fit();
}

void intercept_system_menu_action(intptr_t data)
{
    WinHookEvent* evt = reinterpret_cast<WinHookEvent*>(data);
    if (evt->event == EVENT_SYSTEM_MENUSTART || evt->event == EVENT_OBJECT_INVOKED)
    {
        powertoys_events().handle_system_menu_action(*evt);
    }
}
