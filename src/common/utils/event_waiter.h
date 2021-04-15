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
        // Create localExitThreadEvent and localWaitingEvent for capturing. We can not capture 'this' as we implement move constructor.
        auto localExitThreadEvent = exit_thread_event = CreateEvent(nullptr, false, false, nullptr);
        HANDLE localWaitingEvent = waiting_event = CreateEvent(nullptr, false, false, name.c_str());
        std::thread([=]() {
            HANDLE events[2] = { localWaitingEvent, localExitThreadEvent };
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
                    callback(ERROR_SUCCESS);
                }
            }
        }).detach();
    }

    event_waiter(event_waiter&) = delete;
    event_waiter& operator=(event_waiter&) = delete;

    event_waiter(event_waiter&& a) noexcept
    {
        this->exit_thread_event = a.exit_thread_event;
        this->waiting_event = a.waiting_event;

        a.exit_thread_event = nullptr;
        a.waiting_event = nullptr;
    }

    event_waiter& operator=(event_waiter&& a) noexcept
    {
        this->exit_thread_event = a.exit_thread_event;
        this->waiting_event = a.waiting_event;

        a.exit_thread_event = nullptr;
        a.waiting_event = nullptr;
        return *this;
    }

    ~event_waiter()
    {
        if (exit_thread_event)
        {
            SetEvent(exit_thread_event);
            CloseHandle(exit_thread_event);
        }

        if (waiting_event)
        {
            CloseHandle(waiting_event);
        }
    }

private:
    HANDLE exit_thread_event = nullptr;
    HANDLE waiting_event = nullptr;
};