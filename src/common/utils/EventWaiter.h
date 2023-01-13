#include <functional>
#include <thread>
#include <string>
#include <windows.h>

class EventWaiter
{
public:
    EventWaiter() {}
    EventWaiter(const std::wstring& name, std::function<void(DWORD)> callback)
    {
        // Create localExitThreadEvent and localWaitingEvent for capturing. We can not capture 'this' as we implement move constructor.
        auto localExitThreadEvent = exitThreadEvent = CreateEvent(nullptr, false, false, nullptr);
        HANDLE localWaitingEvent = waitingEvent = CreateEvent(nullptr, false, false, name.c_str());
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

    EventWaiter(EventWaiter&) = delete;
    EventWaiter& operator=(EventWaiter&) = delete;

    EventWaiter(EventWaiter&& a) noexcept
    {
        this->exitThreadEvent = a.exitThreadEvent;
        this->waitingEvent = a.waitingEvent;

        a.exitThreadEvent = nullptr;
        a.waitingEvent = nullptr;
    }

    EventWaiter& operator=(EventWaiter&& a) noexcept
    {
        this->exitThreadEvent = a.exitThreadEvent;
        this->waitingEvent = a.waitingEvent;

        a.exitThreadEvent = nullptr;
        a.waitingEvent = nullptr;
        return *this;
    }

    ~EventWaiter()
    {
        if (exitThreadEvent)
        {
            SetEvent(exitThreadEvent);
            CloseHandle(exitThreadEvent);
        }

        if (waitingEvent)
        {
            CloseHandle(waitingEvent);
        }
    }

private:
    HANDLE exitThreadEvent = nullptr;
    HANDLE waitingEvent = nullptr;
};