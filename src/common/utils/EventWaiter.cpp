#include "pch.h"
#include "EventWaiter.h"

#include <utility>

EventWaiter::EventWaiter(const std::wstring& name, std::function<void(DWORD)> callback)
{
    // Create localExitThreadEvent and localWaitingEvent for capturing. We cannot capture 'this' as we implement move constructor.
    const auto localExitThreadEvent = exitThreadEvent = CreateEvent(nullptr, false, false, nullptr);
    const HANDLE localWaitingEvent = waitingEvent = CreateEvent(nullptr, false, false, name.c_str());

    std::thread([=]() {
        HANDLE events[2] = { localWaitingEvent, localExitThreadEvent };
        while (true)
        {
            const auto waitResult = WaitForMultipleObjects(2, events, false, INFINITE);
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

EventWaiter::EventWaiter(EventWaiter&& other) noexcept :
    exitThreadEvent(other.exitThreadEvent),
    waitingEvent(other.waitingEvent)
{
    other.exitThreadEvent = nullptr;
    other.waitingEvent = nullptr;
}

EventWaiter& EventWaiter::operator=(EventWaiter&& other) noexcept
{
    if (this != &other)
    {
        exitThreadEvent = other.exitThreadEvent;
        waitingEvent = other.waitingEvent;
        other.exitThreadEvent = nullptr;
        other.waitingEvent = nullptr;
    }

    return *this;
}

EventWaiter::~EventWaiter()
{
    if (exitThreadEvent)
    {
        SetEvent(exitThreadEvent);
        CloseHandle(exitThreadEvent);
        exitThreadEvent = nullptr;
    }

    if (waitingEvent)
    {
        CloseHandle(waitingEvent);
        waitingEvent = nullptr;
    }
}
