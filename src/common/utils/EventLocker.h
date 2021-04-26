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

    EventLocker(std::wstring eventName)
    {
        eventHandle = CreateEvent(nullptr, true, false, eventName.c_str());
        SetEvent(eventHandle);
    }

    ~EventLocker()
    {
        ResetEvent(eventHandle);
        CloseHandle(eventHandle);
    }
private:
    HANDLE eventHandle;
};