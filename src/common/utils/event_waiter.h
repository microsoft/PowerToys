#include <functional>
#include <thread>
#include <string>
#include <windows.h>

class event_waiter
{
public:
    event_waiter() {}
    event_waiter(const std::wstring& name, std::function<void(DWORD)> callback)
    {
        auto localExitThreadEvent = exit_thread_event = CreateEvent(nullptr, false, false, nullptr);
        std::thread([=]() {
            HANDLE globalEvent = CreateEvent(nullptr, false, false, name.c_str());
            HANDLE events[2] = { globalEvent, localExitThreadEvent };
            while (true)
            {
                auto waitResult = WaitForMultipleObjects(2, events, false, INFINITE);
                if (waitResult == WAIT_OBJECT_0 + 1)
                {
                    break;
                }

                if (waitResult == WAIT_FAILED)
                {
                    callback(GetLastError());
                    continue;
                }

                if (waitResult == WAIT_OBJECT_0)
                {
                    callback(0);
                }
            }
            
        }).detach();
    }

    event_waiter(event_waiter&) = delete;
    event_waiter& operator=(event_waiter&) = delete;

    event_waiter(event_waiter&& a) noexcept
    {
        this->exit_thread_event = a.exit_thread_event;
        a.exit_thread_event = nullptr;
    }

    event_waiter& operator=(event_waiter&& a) noexcept
    {
        this->exit_thread_event = a.exit_thread_event;
        a.exit_thread_event = nullptr;
        return *this;
    }

    ~event_waiter()
    {
        if (exit_thread_event)
        {
            SetEvent(exit_thread_event);
        }
    }

private:
    HANDLE exit_thread_event = nullptr;
};