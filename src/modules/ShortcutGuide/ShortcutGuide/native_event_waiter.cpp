#include "pch.h"
#include "native_event_waiter.h"

void NativeEventWaiter::run()
{
    while (!aborting)
    {
        auto result = WaitForSingleObject(event_handle, timeout);
        if (!aborting && result == WAIT_OBJECT_0)
        {
            action();
        }
    }
}

NativeEventWaiter::NativeEventWaiter(const std::wstring& event_name, std::function<void()> action)
{
    event_handle = CreateEventW(NULL, FALSE, FALSE, event_name.c_str());
    this->action = action;
    running_thread = std::thread([&]() { run(); });
}

NativeEventWaiter::~NativeEventWaiter()
{
    aborting = true;
    SetEvent(event_handle);
    running_thread.join();
    CloseHandle(event_handle);
}
