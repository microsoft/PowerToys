#include <windows.h>
#include <string>

class event_locker
{
public:
    event_locker(HANDLE h)
    {
        eventHandle = h;
        SetEvent(eventHandle);
    }

    event_locker(std::wstring eventName)
    {
        eventHandle = CreateEvent(nullptr, true, false, eventName.c_str());
        SetEvent(eventHandle);
    }

    ~event_locker()
    {
        ResetEvent(eventHandle);
        CloseHandle(eventHandle);
    }
private:
    HANDLE eventHandle;
};