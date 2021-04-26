#include <windows.h>
#include <string>

class EventLocker
{
public:
    EventLocker(HANDLE h)
    {
        eventHandle = h;
        SetEvent(eventHandle);
    }

    static std::optional<EventLocker> Get(std::wstring eventName)
    {
        EventLocker locker(eventName);
        if (!locker.eventHandle)
        {
            return {};
        }

        return locker;
    }

    EventLocker(EventLocker& e) = delete;
    EventLocker& operator=(EventLocker& e) = delete;

    EventLocker(EventLocker&& e) noexcept
    {
        this->eventHandle = e.eventHandle;
        e.eventHandle = nullptr;
    }
    
    EventLocker& operator=(EventLocker&& e) noexcept
    {
        this->eventHandle = e.eventHandle;
        e.eventHandle = nullptr;
    }

    ~EventLocker()
    {
        if (eventHandle)
        {
            ResetEvent(eventHandle);
            CloseHandle(eventHandle);
            eventHandle = nullptr;
        }
    }
private:
    EventLocker(std::wstring eventName)
    {
        eventHandle = CreateEvent(nullptr, true, false, eventName.c_str());
        if (!eventHandle)
        {
            return;
        }

        SetEvent(eventHandle);
    }

    HANDLE eventHandle;
};
